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

    /// <summary>Abre el panel del agente activo (Tab).</summary>
    OpenAgents,

    /// <summary>Abre la paleta de comandos (Ctrl+P).</summary>
    OpenCommands,

    /// <summary>Confirma el modelo elegido en el selector.</summary>
    ModelSelected,

    /// <summary>Cierra el panel abierto y vuelve a la entrada principal.</summary>
    ClosePanel,

    /// <summary>Tecla ignorada (no aplica en el estado actual).</summary>
    Ignored
}

/// <summary>
/// Editor de linea de la zona de entrada de la TUI: escritura con acentos,
/// cursor de movimiento (flechas/Inicio/Fin), historial con ↑↓, borrado,
/// limpiar con Esc, agentes con Tab y autocompletado con Ctrl+Space.
/// </summary>
public sealed class TuiInput
{
    private static readonly string[] KnownCommands =
    {
        "/agentes", "/analizar", "/avanzar", "/ayuda", "/construir", "/consultar",
        "/contexto", "/examinar", "/model", "/modelo", "/models", "/planear",
        "/preparar", "/recomendar", "/verificar", "/verificar-semantico", "/version", "/salir"
    };

    private static readonly string[] BusyCommands = { "/agentes", "/ayuda", "/models" };

    private readonly TuiHost _host;
    private readonly List<string> _history = new();
    private readonly List<string> _knownExtra;

    private string _buffer = "";
    private int _caret;
    private int _historyIndex;   // == _history.Count cuando se esta escribiendo nuevo texto
    private string _draft = "";
    private bool _escapePressed; // Para detectar Esc + Esc
    private List<ModelInfo> _modelChoices = new();
    private List<bool> _modelSelectable = new();
    private Func<ModelInfo, string>? _describeModelCapabilities;
    private int _modelChoiceIndex;
    private bool _modelPickerActive;
    private List<string> _commandChoices = new(KnownCommands);
    private int _commandChoiceIndex;
    private string _commandQuery = "";
    private bool _commandPaletteActive;
    private List<string> _agentPanelItems = new();
    private bool _agentPanelActive;

    public TuiInput(TuiHost host, IEnumerable<string>? extraCompletions = null)
    {
        _host = host;
        _knownExtra = extraCompletions?.ToList() ?? new List<string>();
        _historyIndex = 0;
    }

    public string Buffer => _buffer;
    public string? SelectedModel => IsModelSelectable(_modelChoiceIndex)
        ? _modelChoices[_modelChoiceIndex].Name
        : null;
    public string? SelectedCommand => _commandPaletteActive
        ? FilteredCommands().ElementAtOrDefault(_commandChoiceIndex)
        : null;

    public static IReadOnlyList<string> CommandPaletteCommands => KnownCommands;
    public static IReadOnlyList<string> BusyCommandPaletteCommands => BusyCommands;

    /// <summary>Dibuja la linea de entrada en su region.</summary>
    public void Render()
    {
        var title = _commandPaletteActive
            ? "Comandos · ↑/↓ elegir · Enter ejecutar · Esc cerrar"
            : _modelPickerActive
                ? "Modelos locales · ↑/↓ elegir · Enter cambiar · Esc cerrar"
                : _agentPanelActive
                    ? "Agentes · Tab o Esc vuelve a la tarea"
                    : null;
        IReadOnlyList<string>? items = _commandPaletteActive
            ? FilteredCommands().Select(FormatCommandChoice).ToList()
            : _modelPickerActive
                ? _modelChoices.Select((model, index) => FormatModelChoice(model, index)).ToList()
                : _agentPanelActive
                    ? _agentPanelItems
                    : null;
        IReadOnlyList<bool>? disabledItems = _modelPickerActive
            ? _modelSelectable.Select(selectable => !selectable).ToList()
            : null;
        var selectedIndex = _commandPaletteActive
            ? _commandChoiceIndex
            : _modelPickerActive
                ? _modelChoiceIndex
                : -1;
        _host.RenderInput(_buffer, _caret, title, items, selectedIndex, disabledItems);
    }

    /// <summary>Limpia el editor y redibuja.</summary>
    public void Clear()
    {
        _modelPickerActive = false;
        _commandPaletteActive = false;
        _agentPanelActive = false;
        _modelChoices.Clear();
        _modelSelectable.Clear();
        _commandChoices.Clear();
        _agentPanelItems.Clear();
        _buffer = "";
        _caret = 0;
        _historyIndex = _history.Count;
        _escapePressed = false;
        Render();
    }

    /// <summary>Fuerza un texto en el editor (p. ej. reencolar una tarea).</summary>
    public void SetText(string text)
    {
        _modelPickerActive = false;
        _commandPaletteActive = false;
        _agentPanelActive = false;
        _modelChoices.Clear();
        _modelSelectable.Clear();
        _commandChoices.Clear();
        _agentPanelItems.Clear();
        _buffer = text ?? "";
        _caret = _buffer.Length;
        Render();
    }

    public InputAction Handle(ConsoleKeyInfo key)
    {
        if ((key.Modifiers & ConsoleModifiers.Control) != 0 && key.Key == ConsoleKey.P)
        {
            if (_commandPaletteActive)
            {
                _commandPaletteActive = false;
                _commandChoices.Clear();
                SetTextInternal("");
                return InputAction.ClosePanel;
            }

            return InputAction.OpenCommands;
        }

        if (key.Key == ConsoleKey.Tab)
        {
            if (_agentPanelActive)
            {
                _agentPanelActive = false;
                _agentPanelItems.Clear();
                SetTextInternal("");
                return InputAction.ClosePanel;
            }

            if (_modelPickerActive || _commandPaletteActive)
            {
                _modelPickerActive = false;
                _commandPaletteActive = false;
                _modelChoices.Clear();
                _modelSelectable.Clear();
                _commandChoices.Clear();
                SetTextInternal("");
            }

            return InputAction.OpenAgents;
        }

        if ((key.Modifiers & ConsoleModifiers.Control) != 0 && key.Key == ConsoleKey.Spacebar)
        {
            Complete();
            return InputAction.None;
        }

        if (_modelPickerActive)
        {
            return HandleModelPicker(key);
        }

        if (_commandPaletteActive)
        {
            return HandleCommandPalette(key);
        }

        if (_agentPanelActive)
        {
            return HandleAgentPanel(key);
        }

        switch (key.Key)
        {
            case ConsoleKey.Enter:
                var text = _buffer.Trim();
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
                    Render();
                }

                return InputAction.None;

            case ConsoleKey.Delete:
                if (_caret < _buffer.Length)
                {
                    _buffer = _buffer.Remove(_caret, 1);
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
        Func<ModelInfo, bool>? isSelectable = null)
    {
        _describeModelCapabilities = describeCapabilities;
        _modelChoices = models
            .Where(model => !string.IsNullOrWhiteSpace(model.Name))
            .GroupBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _modelSelectable = _modelChoices
            .Select(model => isSelectable?.Invoke(model) ?? true)
            .ToList();
        _modelChoiceIndex = -1;
        _modelPickerActive = _modelChoices.Count > 0;
        _commandPaletteActive = false;
        _commandChoices.Clear();
        _agentPanelActive = false;
        _agentPanelItems.Clear();
        if (_modelPickerActive)
        {
            _modelChoiceIndex = FindNextSelectableModel(1);
            SetTextInternal(SelectedModel ?? "");
        }
        else
        {
            Render();
        }
    }

    private string FormatModelChoice(ModelInfo model, int index)
    {
        var capabilities = _describeModelCapabilities?.Invoke(model);
        var description = string.IsNullOrWhiteSpace(capabilities)
            ? model.Name
            : model.Name + " · " + capabilities;
        return IsModelSelectable(index)
            ? description
            : model.Name + " · presupuesto no disponible" +
              (string.IsNullOrWhiteSpace(capabilities) ? "" : " · " + capabilities);
    }

    public void BeginCommandPalette(IEnumerable<string>? commands = null)
    {
        _modelPickerActive = false;
        _modelChoices.Clear();
        _modelSelectable.Clear();
        _agentPanelActive = false;
        _agentPanelItems.Clear();
        _commandChoices = (commands ?? KnownCommands)
            .Where(command => !string.IsNullOrWhiteSpace(command))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        _commandPaletteActive = true;
        _commandChoiceIndex = 0;
        _commandQuery = "/";
        SetTextInternal(_commandQuery);
    }

    public void BeginAgentsPanel(IEnumerable<string> items)
    {
        _modelPickerActive = false;
        _commandPaletteActive = false;
        _modelChoices.Clear();
        _modelSelectable.Clear();
        _commandChoices.Clear();
        _agentPanelItems = items.Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
        _agentPanelActive = true;
        SetTextInternal("");
    }

    private InputAction HandleModelPicker(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Enter:
                if (SelectedModel is null)
                {
                    return InputAction.None;
                }

                _modelPickerActive = false;
                Render();
                return InputAction.ModelSelected;
            case ConsoleKey.UpArrow:
                MoveModelChoice(-1);
                return InputAction.None;
            case ConsoleKey.DownArrow:
                MoveModelChoice(1);
                return InputAction.None;
            case ConsoleKey.Escape:
                _modelPickerActive = false;
                _modelChoices.Clear();
                _modelSelectable.Clear();
                _modelChoiceIndex = -1;
                SetTextInternal("");
                return InputAction.None;
            default:
                return InputAction.None;
        }
    }

    private InputAction HandleCommandPalette(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Enter:
                var selected = FilteredCommands().ElementAtOrDefault(_commandChoiceIndex);
                if (selected is null)
                {
                    return InputAction.None;
                }

                _commandPaletteActive = false;
                _commandChoices.Clear();
                _buffer = selected;
                _caret = _buffer.Length;
                Render();
                return InputAction.Submit;
            case ConsoleKey.UpArrow:
                MoveCommandChoice(-1);
                return InputAction.None;
            case ConsoleKey.DownArrow:
                MoveCommandChoice(1);
                return InputAction.None;
            case ConsoleKey.Escape:
                _commandPaletteActive = false;
                _commandChoices.Clear();
                SetTextInternal("");
                return InputAction.None;
            case ConsoleKey.Backspace:
                if (_commandQuery.Length > 0)
                {
                    _commandQuery = _commandQuery[..^1];
                    _commandChoiceIndex = 0;
                    SetTextInternal(_commandQuery);
                }
                return InputAction.None;
            default:
                if (!char.IsControl(key.KeyChar))
                {
                    if (key.KeyChar != '/' || _commandQuery.Length == 0)
                    {
                        _commandQuery += key.KeyChar;
                        _commandChoiceIndex = 0;
                        SetTextInternal(_commandQuery);
                    }
                }
                return InputAction.None;
        }
    }

    private InputAction HandleAgentPanel(ConsoleKeyInfo key)
    {
        if (key.Key is ConsoleKey.Enter or ConsoleKey.Escape)
        {
            _agentPanelActive = false;
            _agentPanelItems.Clear();
            SetTextInternal("");
            return InputAction.ClosePanel;
        }

        return InputAction.None;
    }

    private void MoveModelChoice(int delta)
    {
        var next = FindNextSelectableModel(delta);
        if (next < 0)
        {
            return;
        }

        _modelChoiceIndex = next;
        SetTextInternal(SelectedModel ?? "");
    }

    private int FindNextSelectableModel(int delta)
    {
        if (_modelChoices.Count == 0)
        {
            return -1;
        }

        var start = _modelChoiceIndex < 0 ? (delta > 0 ? -1 : 0) : _modelChoiceIndex;
        for (var step = 1; step <= _modelChoices.Count; step++)
        {
            var candidate = (start + delta * step) % _modelChoices.Count;
            if (candidate < 0) candidate += _modelChoices.Count;
            if (IsModelSelectable(candidate))
            {
                return candidate;
            }
        }

        return -1;
    }

    private bool IsModelSelectable(int index)
        => index >= 0 && index < _modelChoices.Count &&
           (index >= _modelSelectable.Count || _modelSelectable[index]);

    private void MoveCommandChoice(int delta)
    {
        var choices = FilteredCommands();
        if (choices.Count == 0)
        {
            _commandChoiceIndex = 0;
            Render();
            return;
        }

        _commandChoiceIndex = (_commandChoiceIndex + delta + choices.Count) % choices.Count;
        Render();
    }

    private List<string> FilteredCommands()
    {
        if (_commandChoices.Count == 0)
        {
            return new List<string>();
        }

        return _commandChoices
            .Where(command => string.IsNullOrWhiteSpace(_commandQuery) ||
                              command.Contains(_commandQuery, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static string FormatCommandChoice(string command)
    {
        var description = command.ToLowerInvariant() switch
        {
            "/agentes" => "Panel de agentes",
            "/modelo" or "/model" or "/models" => "Cambiar modelo local",
            "/ayuda" => "Ver ayuda",
            "/salir" => "Cerrar Condor",
            _ => "Ejecutar comando Condor"
        };
        return command.PadRight(23) + description;
    }

    private void Insert(string text)
    {
        _buffer = _buffer.Insert(_caret, text);
        _caret += text.Length;
        _historyIndex = _history.Count;
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
