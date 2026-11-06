using Condor.Core.Models;

namespace Condor.Cli.Tui;

/// <summary>Resultado de una tecla procesada por el editor de la TUI.</summary>
public enum InputAction
{
    /// <summary>La tecla fue consumida editando el texto.</summary>
    None,

    /// <summary>El usuario confirmo la entrada (Enter).</summary>
    Submit,

    /// <summary>El usuario pidio salir (sin tarea en curso).</summary>
    Exit,

    /// <summary>El usuario interrumpio la tarea activa (Esc + Esc).</summary>
    Interrupt,

    /// <summary>Abre el selector de agentes (Tab).</summary>
    OpenAgents,

    /// <summary>Abre la paleta de comandos (Ctrl+P).</summary>
    OpenCommands,

    /// <summary>Confirma el modelo elegido en el selector.</summary>
    ModelSelected,

    /// <summary>Confirma el agente elegido para las tareas siguientes.</summary>
    AgentSelected,

    /// <summary>Tecla ignorada (no aplica en el estado actual).</summary>
    Ignored
}

/// <summary>
/// Editor de linea de entrada y adaptador de teclado para SelectList.
/// </summary>
public sealed class TuiInput
{
    private static readonly string[] KnownCommands =
    {
        "/modelo", "/agentes", "/analizar", "/avanzar", "/ayuda", "/construir", "/consultar",
        "/contexto", "/examinar", "/planear",
        "/preparar", "/recomendar", "/verificar", "/verificar-semantico", "/version", "/salir"
    };

    private static readonly string[] BusyCommands =
        { "/modelo", "/agentes", "/ayuda", "/model", "/models", "/modelos" };

    private readonly TuiHost _host;
    private readonly List<string> _history = new();
    private readonly List<string> _knownExtra;

    private string _buffer = "";
    private int _caret;
    private int _historyIndex;   // == _history.Count cuando se esta escribiendo nuevo texto
    private string _draft = "";
    private bool _escapePressed; // Para detectar Esc + Esc
    private SelectListState? _selectList;
    private OverlayState _overlayState;
    private string _returnBuffer = "";
    private int _returnCaret;
    private bool _hasSubmittedFirstInteraction;
    private string? _selectedModel;
    private string _selectedAgentId = "codificar";
    private string? _inputPlaceholder;
    private bool _slashSuggestionsDismissed;

    public TuiInput(
        TuiHost host,
        IEnumerable<string>? extraCompletions = null,
        TuiTheme? theme = null)
    {
        _host = host;
        _knownExtra = extraCompletions?.ToList() ?? new List<string>();
        _host.SetTuiTheme(theme ?? TuiTheme.Default);
        _historyIndex = 0;
    }

    public string Buffer => _buffer;
    public string? SelectedModel => _selectedModel ??
        (_overlayState == OverlayState.Modelos ? _selectList?.Selected?.Id : null);
    public string? SelectedAgentId => _overlayState == OverlayState.Agentes
        ? _selectList?.Selected?.Id
        : _selectedAgentId;
    public string? SelectedCommand => _overlayState == OverlayState.Paleta
        ? _selectList?.Selected?.Id
        : null;

    public static IReadOnlyList<string> CommandPaletteCommands => KnownCommands;
    public static IReadOnlyList<string> BusyCommandPaletteCommands => BusyCommands;

    /// <summary>Dibuja la linea de entrada en su region.</summary>
    public void Render()
    {
        var hideInitialPlaceholder = _hasSubmittedFirstInteraction &&
                                     _buffer.Length == 0 &&
                                     _inputPlaceholder is null;
        var showCursorWhenEmpty = hideInitialPlaceholder && _selectList is null;
        var placeholder = hideInitialPlaceholder ? string.Empty : _inputPlaceholder;
        if (_selectList is not null)
        {
            var options = _overlayState switch
            {
                OverlayState.Slash => new SelectListRenderOptions(
                    Title: "Comandos · clic ejecutar · Tab completar · rueda desplazar · Esc cerrar",
                    TwoColumns: true),
                OverlayState.Modelos => new SelectListRenderOptions(
                    Title: "Modelo local · clic o Enter elegir · rueda desplazar · Esc cerrar"),
                OverlayState.Paleta => new SelectListRenderOptions(
                    Title: "Comandos · clic o Enter elegir · rueda desplazar · Esc cerrar",
                    IncludeSearchField: true,
                    ShowShortcuts: true),
                OverlayState.Agentes => new SelectListRenderOptions(
                    Title: "Modos de trabajo · clic o Enter elegir · rueda desplazar · Esc cerrar"),
                _ => new SelectListRenderOptions()
            };
            var overlay = new SelectListOverlay(
                _selectList,
                options,
                _overlayState == OverlayState.Paleta
                    ? SelectListPlacement.Centered
                    : SelectListPlacement.AboveInput,
                MaximumWidth: _overlayState == OverlayState.Paleta ? 90 : null);
            _host.RenderInputWithSelectList(_buffer, _caret, overlay, placeholder);
            return;
        }

        _host.RenderInput(_buffer, _caret, placeholder: placeholder, showCursorWhenEmpty: showCursorWhenEmpty);
    }

    public InputAction HandleMouse(TuiMouseEvent mouseEvent)
    {
        if (_selectList is not null && mouseEvent.Kind == TuiMouseEventKind.Wheel &&
            _host.TryGetSelectListItemIndexAt(mouseEvent.Row, mouseEvent.Column, out _))
        {
            var steps = Math.Clamp(Math.Abs(mouseEvent.WheelDelta), 1, 5);
            for (var step = 0; step < steps; step++)
            {
                _selectList = mouseEvent.WheelDelta > 0
                    ? _selectList.MovePrev()
                    : _selectList.MoveNext();
            }

            SyncSelectionBuffer();
            Render();
            return InputAction.None;
        }

        if (mouseEvent.Kind != TuiMouseEventKind.LeftClick)
        {
            return InputAction.None;
        }

        if (_selectList is not null &&
            _host.TryGetSelectListItemIndexAt(mouseEvent.Row, mouseEvent.Column, out var filteredIndex))
        {
            var item = _selectList.Items[_selectList.Filtered[filteredIndex]];
            if (item.Disabled)
            {
                return InputAction.None;
            }

            _selectList = _selectList.SelectIndex(filteredIndex);
            return HandleSelectList(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));
        }

        if (_host.TryGetFooterActionAt(mouseEvent.Row, mouseEvent.Column, out var footerAction))
        {
            if (_selectList is not null)
            {
                CloseSelectList(restoreBuffer: true);
                Render();
            }

            return footerAction;
        }

        var keepCurrentBuffer = _selectList is null || _overlayState == OverlayState.Slash;
        var bufferForCaret = keepCurrentBuffer ? _buffer : _returnBuffer;
        var caretForBuffer = keepCurrentBuffer ? _caret : _returnCaret;
        if (_host.TryGetInputCaretIndexAt(
                mouseEvent.Row,
                mouseEvent.Column,
                bufferForCaret,
                caretForBuffer,
                out var nextCaret))
        {
            if (_selectList is not null)
            {
                CloseSelectList(restoreBuffer: !keepCurrentBuffer);
            }

            _caret = Math.Clamp(nextCaret, 0, _buffer.Length);
            Render();
        }

        return InputAction.None;
    }

    /// <summary>Limpia el editor y redibuja.</summary>
    public void Clear()
    {
        _selectList = null;
        _overlayState = OverlayState.None;
        _buffer = "";
        _caret = 0;
        _historyIndex = _history.Count;
        _escapePressed = false;
        _selectedModel = null;
        _inputPlaceholder = null;
        _slashSuggestionsDismissed = false;
        Render();
    }

    /// <summary>Fuerza un texto en el editor (p. ej. reencolar una tarea).</summary>
    public void SetText(string text)
    {
        _selectList = null;
        _overlayState = OverlayState.None;
        _buffer = text ?? "";
        _caret = _buffer.Length;
        _selectedModel = null;
        _inputPlaceholder = null;
        UpdateSlashSuggestionsFromBuffer();
        Render();
    }

    public InputAction Handle(ConsoleKeyInfo key)
    {
        if (_selectList is not null)
        {
            if ((key.Modifiers & ConsoleModifiers.Control) != 0 && key.Key == ConsoleKey.P)
            {
                if (_overlayState == OverlayState.Paleta)
                {
                    CloseSelectList(restoreBuffer: true);
                    Render();
                    return InputAction.None;
                }

                return InputAction.OpenCommands;
            }

            if (key.Key == ConsoleKey.Tab)
            {
                if (_overlayState == OverlayState.Slash)
                {
                    CompleteSelectedCommand();
                    return InputAction.None;
                }

                if (_overlayState == OverlayState.Agentes)
                {
                    CloseSelectList(restoreBuffer: true);
                    Render();
                    return InputAction.None;
                }

                CloseSelectList(restoreBuffer: true);
                return InputAction.OpenAgents;
            }

            return HandleSelectList(key);
        }

        if ((key.Modifiers & ConsoleModifiers.Control) != 0 && key.Key == ConsoleKey.P)
        {
            return InputAction.OpenCommands;
        }

        if (key.Key == ConsoleKey.Tab)
        {
            return InputAction.OpenAgents;
        }

        if ((key.Modifiers & ConsoleModifiers.Control) != 0 && key.Key == ConsoleKey.Spacebar)
        {
            Complete();
            return InputAction.None;
        }

        switch (key.Key)
        {
            case ConsoleKey.Enter:
                var text = _buffer.Trim();
                if (text.Length > 0)
                {
                    _hasSubmittedFirstInteraction = true;
                }

                if (text.Length > 0 && (_history.Count == 0 || _history[^1] != text))
                {
                    _history.Add(text);
                }

                _historyIndex = _history.Count;
                _draft = "";
                return InputAction.Submit;

            case ConsoleKey.Escape:
                if (_buffer.Length > 0)
                {
                    _buffer = "";
                    _caret = 0;
                    _escapePressed = false;
                    _slashSuggestionsDismissed = false;
                    Render();
                    return InputAction.None;
                }

                // Doble ESC para interrumpir tarea
                if (_escapePressed)
                {
                    _escapePressed = false;
                    return InputAction.Interrupt;
                }

                _escapePressed = true;
                return InputAction.None;

            case ConsoleKey.Backspace:
                if (_caret > 0)
                {
                    _caret--;
                    _buffer = _buffer.Remove(_caret, 1);
                    UpdateSlashSuggestionsFromBuffer();
                    Render();
                }

                return InputAction.None;

            case ConsoleKey.Delete:
                if (_caret < _buffer.Length)
                {
                    _buffer = _buffer.Remove(_caret, 1);
                    UpdateSlashSuggestionsFromBuffer();
                    Render();
                }

                return InputAction.None;

            case ConsoleKey.LeftArrow:
                if (_caret > 0)
                {
                    _caret--;
                    Render();
                }

                return InputAction.None;

            case ConsoleKey.RightArrow:
                if (_caret < _buffer.Length)
                {
                    _caret++;
                    Render();
                }

                return InputAction.None;

            case ConsoleKey.Home:
                if (_caret != 0)
                {
                    _caret = 0;
                    Render();
                }

                return InputAction.None;

            case ConsoleKey.End:
                if (_caret != _buffer.Length)
                {
                    _caret = _buffer.Length;
                    Render();
                }

                return InputAction.None;

            case ConsoleKey.UpArrow:
                HistoryPrevious();
                return InputAction.None;

            case ConsoleKey.DownArrow:
                HistoryNext();
                return InputAction.None;

            default:
                if (!char.IsControl(key.KeyChar))
                {
                    Insert(key.KeyChar.ToString());
                }

                return InputAction.None;
        }
    }

    public void BeginModelPicker(
        IEnumerable<ModelInfo> models,
        Func<ModelInfo, string>? describeCapabilities = null,
        Func<ModelInfo, bool>? isSelectable = null,
        string? activeModel = null)
    {
        _selectedModel = null;
        var items = models
            .Where(model => !string.IsNullOrWhiteSpace(model.Name))
            .GroupBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
            .Select(model =>
            {
                var selectable = isSelectable?.Invoke(model) ?? true;
                return new SelectItem(
                    model.Name,
                    model.Name,
                    describeCapabilities?.Invoke(model) ?? "",
                    Array.Empty<string>(),
                    Disabled: !selectable,
                    DisabledReason: selectable ? null : "presupuesto no disponible",
                    Shortcut: null)
                {
                    IsCurrent = string.Equals(model.Name, activeModel, StringComparison.OrdinalIgnoreCase)
                };
            })
            .ToList();
        CaptureReturnBuffer();
        _overlayState = OverlayState.Modelos;
        _selectList = SelectList.Create(items, selectedId: activeModel);
        _buffer = "";
        _caret = 0;
        _inputPlaceholder = "Filtrar modelos locales";
        Render();
    }

    public void BeginCommandPalette(IEnumerable<string>? commands = null)
    {
        CaptureReturnBuffer();
        _selectedModel = null;
        var items = (commands ?? KnownCommands)
            .Where(command => !string.IsNullOrWhiteSpace(command))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(BuildCommandItem)
            .ToList();
        _overlayState = OverlayState.Paleta;
        _selectList = SelectList.Create(items);
        _buffer = "";
        _caret = 0;
        _inputPlaceholder = null;
        Render();
    }

    public void BeginAgentPicker(IEnumerable<SelectItem> agents, string? activeAgentId = null)
    {
        CaptureReturnBuffer();
        _selectedModel = null;
        if (!string.IsNullOrWhiteSpace(activeAgentId))
        {
            _selectedAgentId = activeAgentId;
        }

        var items = agents
            .Where(agent => !string.IsNullOrWhiteSpace(agent.Id) && !string.IsNullOrWhiteSpace(agent.Label))
            .Select(agent => agent with { IsCurrent = string.Equals(agent.Id, _selectedAgentId, StringComparison.OrdinalIgnoreCase) })
            .ToList();
        _overlayState = OverlayState.Agentes;
        _selectList = SelectList.Create(items, selectedId: _selectedAgentId);
        _buffer = "";
        _caret = 0;
        _inputPlaceholder = "Filtrar modos";
        Render();
    }

    private InputAction HandleSelectList(ConsoleKeyInfo key)
    {
        if (_selectList is null)
        {
            return InputAction.None;
        }

        switch (key.Key)
        {
            case ConsoleKey.Enter:
                var selected = _selectList.Selected;
                if (selected is null || selected.Disabled)
                {
                    return InputAction.None;
                }

                if (_overlayState == OverlayState.Modelos)
                {
                    _selectedModel = selected.Id;
                    _buffer = "";
                    _caret = 0;
                    CloseSelectList(restoreBuffer: false);
                    Render();
                    return InputAction.ModelSelected;
                }

                if (_overlayState == OverlayState.Agentes)
                {
                    _selectedAgentId = selected.Id;
                    CloseSelectList(restoreBuffer: true);
                    Render();
                    return InputAction.AgentSelected;
                }

                _buffer = selected.Id;
                _caret = _buffer.Length;
                _slashSuggestionsDismissed = false;
                CloseSelectList(restoreBuffer: false);
                Render();
                return InputAction.Submit;

            case ConsoleKey.UpArrow:
                _selectList = _selectList.MovePrev();
                SyncSelectionBuffer();
                Render();
                return InputAction.None;

            case ConsoleKey.DownArrow:
                _selectList = _selectList.MoveNext();
                SyncSelectionBuffer();
                Render();
                return InputAction.None;

            case ConsoleKey.Escape:
                if (_overlayState == OverlayState.Slash)
                {
                    _slashSuggestionsDismissed = true;
                    CloseSelectList(restoreBuffer: false);
                }
                else
                {
                    CloseSelectList(restoreBuffer: true);
                }

                Render();
                return InputAction.None;

            case ConsoleKey.Backspace:
                if (_overlayState == OverlayState.Slash || _overlayState == OverlayState.Modelos)
                {
                    if (_caret > 0)
                    {
                        _caret--;
                        _buffer = _buffer.Remove(_caret, 1);
                        if (_overlayState == OverlayState.Slash)
                        {
                            UpdateSlashSuggestionsFromBuffer();
                        }
                        else
                        {
                            _selectList = _selectList.SetQuery(_buffer);
                        }
                    }
                }
                else if (_selectList.Query.Length > 0)
                {
                    _selectList = _selectList.SetQuery(_selectList.Query[..^1]);
                }

                Render();
                return InputAction.None;

            case ConsoleKey.Delete:
                if ((_overlayState is OverlayState.Slash or OverlayState.Modelos) && _caret < _buffer.Length)
                {
                    _buffer = _buffer.Remove(_caret, 1);
                    if (_overlayState == OverlayState.Slash)
                    {
                        UpdateSlashSuggestionsFromBuffer();
                    }
                    else
                    {
                        _selectList = _selectList.SetQuery(_buffer);
                    }
                    Render();
                }
                else if ((_overlayState is OverlayState.Paleta or OverlayState.Agentes) && _selectList.Query.Length > 0)
                {
                    _selectList = _selectList.SetQuery(_selectList.Query[..^1]);
                    Render();
                }
                return InputAction.None;

            case ConsoleKey.Tab:
                if (_overlayState == OverlayState.Slash)
                {
                    CompleteSelectedCommand();
                    return InputAction.None;
                }

                if (_overlayState == OverlayState.Agentes)
                {
                    CloseSelectList(restoreBuffer: true);
                    Render();
                    return InputAction.None;
                }

                CloseSelectList(restoreBuffer: true);
                return InputAction.OpenAgents;

            case ConsoleKey.LeftArrow:
                if ((_overlayState is OverlayState.Slash or OverlayState.Modelos) && _caret > 0)
                {
                    _caret--;
                    Render();
                }
                return InputAction.None;

            case ConsoleKey.RightArrow:
                if ((_overlayState is OverlayState.Slash or OverlayState.Modelos) && _caret < _buffer.Length)
                {
                    _caret++;
                    Render();
                }
                return InputAction.None;

            case ConsoleKey.Home:
                if (_overlayState is OverlayState.Slash or OverlayState.Modelos)
                {
                    _caret = 0;
                    Render();
                }
                return InputAction.None;

            case ConsoleKey.End:
                if (_overlayState is OverlayState.Slash or OverlayState.Modelos)
                {
                    _caret = _buffer.Length;
                    Render();
                }
                return InputAction.None;

            default:
                if (!char.IsControl(key.KeyChar))
                {
                    if (_overlayState is OverlayState.Paleta or OverlayState.Agentes)
                    {
                        _selectList = _selectList.SetQuery(_selectList.Query + key.KeyChar);
                        Render();
                    }
                    else if (_overlayState == OverlayState.Modelos)
                    {
                        _buffer = _buffer.Insert(_caret, key.KeyChar.ToString());
                        _caret++;
                        _selectList = _selectList.SetQuery(_buffer);
                        Render();
                    }
                    else
                    {
                        Insert(key.KeyChar.ToString());
                        return InputAction.None;
                    }
                }

                return InputAction.None;
        }
    }

    private void CompleteSelectedCommand()
    {
        var selected = _selectList?.Selected;
        if (selected is null || selected.Disabled)
        {
            return;
        }

        _buffer = selected.Id + " ";
        _caret = _buffer.Length;
        _slashSuggestionsDismissed = false;
        CloseSelectList(restoreBuffer: false);
        Render();
    }

    private void SyncSelectionBuffer()
    {
        if (_overlayState == OverlayState.Slash)
        {
            _caret = _buffer.Length;
        }
    }

    private void UpdateSlashSuggestionsFromBuffer()
    {
        if (_overlayState == OverlayState.Slash)
        {
            if (!_buffer.StartsWith("/", StringComparison.Ordinal) || _buffer.Contains(' '))
            {
                CloseSelectList(restoreBuffer: false);
                if (_buffer.Length == 0) _slashSuggestionsDismissed = false;
                return;
            }

            _selectList = _selectList!.SetQuery(_buffer[1..]);
            return;
        }

        if (_overlayState != OverlayState.None)
        {
            return;
        }

        if (_buffer.Length == 0)
        {
            _slashSuggestionsDismissed = false;
            return;
        }

        if (!_slashSuggestionsDismissed &&
            _buffer.StartsWith("/", StringComparison.Ordinal) &&
            !_buffer.Contains(' '))
        {
            _overlayState = OverlayState.Slash;
            _selectList = SelectList.Create(BuildCommandItems(KnownCommands.Concat(_knownExtra)));
            _selectList = _selectList.SetQuery(_buffer[1..]);
        }
    }

    private void CaptureReturnBuffer()
    {
        if (_selectList is not null && (_overlayState is OverlayState.Paleta or OverlayState.Modelos or OverlayState.Agentes))
        {
            return;
        }

        _returnBuffer = _buffer;
        _returnCaret = _caret;
    }

    private void CloseSelectList(bool restoreBuffer)
    {
        _selectList = null;
        _overlayState = OverlayState.None;
        _inputPlaceholder = null;
        if (restoreBuffer)
        {
            _buffer = _returnBuffer;
            _caret = Math.Clamp(_returnCaret, 0, _buffer.Length);
            UpdateSlashSuggestionsFromBuffer();
        }
    }

    private static IReadOnlyList<SelectItem> BuildCommandItems(IEnumerable<string> commands) =>
        commands
            .Where(command => !string.IsNullOrWhiteSpace(command))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(BuildCommandItem)
            .ToArray();

    private static SelectItem BuildCommandItem(string command) => new(
        command,
        command,
        CommandDescription(command),
        Array.Empty<string>(),
        Disabled: false,
        DisabledReason: null,
        Shortcut: "Enter");

    private static string CommandDescription(string command) => command.ToLowerInvariant() switch
    {
        "/agentes" => "Elegir modo de trabajo",
        "/analizar" => "Analizar estructura y recursos del proyecto",
        "/avanzar" => "Planificar, construir y verificar una tarea",
        "/ayuda" => "Mostrar ayuda de Condor",
        "/construir" => "Construir el proyecto",
        "/consultar" => "Consultar al modelo local",
        "/contexto" => "Consultar el contexto del proyecto",
        "/examinar" => "Analizar una imagen local",
        "/model" or "/modelo" or "/models" => "Cambiar el modelo local",
        "/planear" => "Crear un plan de trabajo",
        "/preparar" => "Comprobar dependencias y modelo",
        "/recomendar" => "Recomendar un modelo local",
        "/verificar" => "Ejecutar verificaciones del proyecto",
        "/verificar-semantico" => "Comprobar la capa semantica",
        "/version" => "Mostrar la version instalada",
        "/salir" => "Cerrar Condor",
        _ => "Ejecutar comando Condor"
    };

    private enum OverlayState
    {
        None,
        Slash,
        Paleta,
        Modelos,
        Agentes
    }

    private void Insert(string text)
    {
        var startsSlash = _buffer.Length == 0 && text.StartsWith("/", StringComparison.Ordinal);
        _buffer = _buffer.Insert(_caret, text);
        _caret += text.Length;
        _historyIndex = _history.Count;
        if (startsSlash || _overlayState == OverlayState.Slash)
        {
            UpdateSlashSuggestionsFromBuffer();
        }

        Render();
    }

    private void HistoryPrevious()
    {
        if (_history.Count == 0 || _historyIndex == 0)
        {
            return;
        }

        if (_historyIndex == _history.Count)
        {
            _draft = _buffer;
        }

        _historyIndex--;
        SetTextInternal(_history[_historyIndex]);
    }

    private void HistoryNext()
    {
        if (_historyIndex >= _history.Count)
        {
            return;
        }

        _historyIndex++;
        SetTextInternal(_historyIndex == _history.Count ? _draft : _history[_historyIndex]);
    }

    private void SetTextInternal(string text)
    {
        _buffer = text;
        _caret = text.Length;
        UpdateSlashSuggestionsFromBuffer();
        Render();
    }

    /// <summary>Autocompletado sobrio del primer token "/" con Ctrl+Space.</summary>
    private void Complete()
    {
        var trimmed = _buffer.TrimStart();
        if (trimmed.Length == 0 || !trimmed.StartsWith('/') || trimmed.Contains(' '))
        {
            return;
        }

        var candidates = KnownCommands.Concat(_knownExtra)
            .Where(c => c.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase) &&
                        !c.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidates.Count == 0)
        {
            return;
        }

        if (candidates.Count == 1)
        {
            SetTextInternal(candidates[0] + " ");
            return;
        }

        // Prefijo comun mas largo entre los candidatos.
        var prefix = candidates[0];
        foreach (var candidate in candidates.Skip(1))
        {
            var i = 0;
            while (i < prefix.Length && i < candidate.Length &&
                   char.ToLowerInvariant(prefix[i]) == char.ToLowerInvariant(candidate[i]))
            {
                i++;
            }

            prefix = prefix[..i];
        }

        SetTextInternal(prefix.Length > trimmed.Length ? prefix : candidates[0]);
    }
}
