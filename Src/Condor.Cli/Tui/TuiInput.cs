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
        "/analizar", "/contexto", "/planear", "/construir", "/verificar",
        "/verificar-semantico", "/avanzar", "/examinar", "/recomendar",
        "/consultar", "/preparar", "/modelo", "/ayuda", "/version", "/salir"
    };

    private readonly TuiHost _host;
    private readonly List<string> _history = new();
    private readonly List<string> _knownExtra;

    private string _buffer = "";
    private int _caret;
    private int _historyIndex;   // == _history.Count cuando se esta escribiendo nuevo texto
    private string _draft = "";
    private bool _escapePressed; // Para detectar Esc + Esc
    private List<ModelInfo> _modelChoices = new();
    private int _modelChoiceIndex;
    private bool _modelPickerActive;
    private int _commandChoiceIndex;
    private bool _commandPaletteActive;

    public TuiInput(TuiHost host, IEnumerable<string>? extraCompletions = null)
    {
        _host = host;
        _knownExtra = extraCompletions?.ToList() ?? new List<string>();
        _historyIndex = 0;
    }

    public string Buffer => _buffer;
    public string? SelectedModel => _modelPickerActive || _modelChoices.Count > 0
        ? _modelChoices.ElementAtOrDefault(_modelChoiceIndex)?.Name
        : null;

    public static IReadOnlyList<string> CommandPaletteCommands => KnownCommands;

    /// <summary>Dibuja la linea de entrada en su region.</summary>
    public void Render()
    {
        _host.RenderInput(_buffer, _caret);
    }

    /// <summary>Limpia el editor y redibuja.</summary>
    public void Clear()
    {
        _modelPickerActive = false;
        _commandPaletteActive = false;
        _modelChoices.Clear();
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
        _modelChoices.Clear();
        _buffer = text ?? "";
        _caret = _buffer.Length;
        Render();
    }

    public InputAction Handle(ConsoleKeyInfo key)
    {
        if ((key.Modifiers & ConsoleModifiers.Control) != 0 && key.Key == ConsoleKey.P)
        {
            return InputAction.OpenCommands;
        }

        if (key.Key == ConsoleKey.Tab)
        {
            if (_modelPickerActive || _commandPaletteActive)
            {
                _modelPickerActive = false;
                _commandPaletteActive = false;
                _modelChoices.Clear();
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

    public void BeginModelPicker(IEnumerable<ModelInfo> models)
    {
        _modelChoices = models
            .Where(model => !string.IsNullOrWhiteSpace(model.Name))
            .GroupBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _modelChoiceIndex = 0;
        _modelPickerActive = _modelChoices.Count > 0;
        _commandPaletteActive = false;
        if (_modelPickerActive)
        {
            SetTextInternal(_modelChoices[0].Name);
        }
    }

    public void BeginCommandPalette()
    {
        _modelPickerActive = false;
        _modelChoices.Clear();
        _commandPaletteActive = true;
        _commandChoiceIndex = 0;
        SetTextInternal(KnownCommands[_commandChoiceIndex]);
    }

    private InputAction HandleModelPicker(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Enter:
                _modelPickerActive = false;
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
                _commandPaletteActive = false;
                return InputAction.Submit;
            case ConsoleKey.UpArrow:
                MoveCommandChoice(-1);
                return InputAction.None;
            case ConsoleKey.DownArrow:
                MoveCommandChoice(1);
                return InputAction.None;
            case ConsoleKey.Escape:
                _commandPaletteActive = false;
                SetTextInternal("");
                return InputAction.None;
            default:
                _commandPaletteActive = false;
                return InputAction.None;
        }
    }

    private void MoveModelChoice(int delta)
    {
        if (_modelChoices.Count == 0)
        {
            _modelPickerActive = false;
            return;
        }

        _modelChoiceIndex = (_modelChoiceIndex + delta + _modelChoices.Count) % _modelChoices.Count;
        SetTextInternal(_modelChoices[_modelChoiceIndex].Name);
    }

    private void MoveCommandChoice(int delta)
    {
        _commandChoiceIndex = (_commandChoiceIndex + delta + KnownCommands.Length) % KnownCommands.Length;
        SetTextInternal(KnownCommands[_commandChoiceIndex]);
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
