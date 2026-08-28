namespace Condor.Cli.Tui;

/// <summary>Tono visual de una linea de la zona de actividad.</summary>
public enum ActivityKind
{
    /// <summary>Evento informativo del sistema (gris con punto verde).</summary>
    System,

    /// <summary>Resultado positivo (verde).</summary>
    Success,

    /// <summary>Advertencia honesta (amarillo).</summary>
    Warning,

    /// <summary>Error funcional presentado con claridad (rojo).</summary>
    Error,

    /// <summary>Entrada del usuario (blanco).</summary>
    User,

    /// <summary>Respuesta de Condor (crema).</summary>
    Condor
}

/// <summary>
/// Pantalla persistente de la TUI de Condor. Autoridad UNICA del dibujo: la
/// ventana vive en el buffer alternativo de la terminal y se actualiza POR
/// REGIONES (cabecera, actividad, estado, entrada); nunca se reimprime toda la
/// pantalla para refrescar un dato. Los hilos de trabajo solo publican estado;
/// el hilo de interfaz repinta las regiones sucias.
///
/// Estructura (sesion):
///
///   CONDOR  v1.0 · build interno X        Hecho en Colombia · Modo Local 100% [· modelo]
///   [ Condor Ave V16 a la derecha ]
///   ── Actividad del agente ────────────────────────────────
///   (historial vivo de la conversacion y la actividad)
///   ── Observa · Comprende · Planifica · Construye · Verifica ──
///   &gt; ¿que deseas construir...?
///   (barra de estado: workspace | modelo | estado | version)
///
/// La identidad institucional y el modelo REAL seleccionado comparten UNA sola
/// linea superior, fuera del area de la mascota; "Modo Local 100%" aparece una
/// unica vez en la cabecera. Si el modelo cambia, la linea se actualiza sola.
/// </summary>
public sealed class TuiHost : IDisposable
{
    private const string IdentityLine = "Hecho en Colombia · Modo Local 100%";
    private const string Slogan = "Observa · Comprende · Planifica · Construye · Verifica";
    private const string Placeholder = "¿Qué deseas construir? ...";
    internal const int MinWidth = 80;
    internal const int MinHeight = 24;

    private const int HeaderHeight = 15;          // titulo (1) + Ave V16 (13) + separador (1)
    private const int BottomRows = 4;             // separador entrada + entrada + progreso + barra estado

    private static readonly string[] SpinnerFrames = { "◐", "◓", "◑", "◒" };

    private readonly object _gate = new();
    private readonly List<(string Text, ActivityKind Kind)> _activity = new();
    private readonly List<(string Line, ActivityKind Kind)> _wrapped = new();

    private int _width;
    private int _height;
    private bool _entered;
    private bool _disposed;
    private bool _forceInteractive;

    private HostMode _mode = HostMode.Welcome;
    private string? _model;
    private string? _workspace;
    private string _estado = "Iniciando";
    private ActivityKind _estadoKind = ActivityKind.System;
    private string _progreso = "";
    private bool _busy;
    private int _spin;

    private bool _dirtyAll = true;
    private bool _dirtyHeader;
    private bool _dirtyActivity;
    private bool _dirtyStatus;
    private bool _dirtyInput;

    private int ActivityTop => HeaderHeight + 1;                 // debajo del separador de actividad
    private int SepActivityRow => HeaderHeight;                  // fila del separador "Actividad del agente"
    private int SepInputRow => _height - BottomRows;             // separador antes de la entrada
    public int InputRow => _height - BottomRows + 1;             // fila de entrada
    private int ProgresoRow => _height - BottomRows + 2;        // fila de progreso / iteracion
    private int StatusBarRow => _height - BottomRows + 3;        // fila de barra de estado (ultima)
    private int ActivityHeight => Math.Max(0, _height - BottomRows - HeaderHeight - 1);

    public TuiHost()
    {
        Supported = Ansi.TryEnableVirtualTerminal();
    }

    /// <summary>
    /// Constructor optimizado: cuando CanRun() ya verifico VT y obtuvo las
    /// dimensiones, se evita releer P/Invoke redundantes.
    /// </summary>
    public TuiHost(int width, int height)
    {
        Supported = true;
        _width = width;
        _height = height;
    }

    /// <summary>
    /// Seam de pruebas/demostracion: instancia la pantalla sin depender de una
    /// consola real (tamano fijo 110x34). Permite verificar el pintado por
    /// regiones y los fotogramas exactos que produce el codigo de produccion.
    /// </summary>
    internal TuiHost(bool forceInteractive)
    {
        Supported = true;
        _forceInteractive = true;
    }

    /// <summary>La TUI requiere terminal interactiva con soporte VT.</summary>
    public bool Supported { get; }

    public void Enter()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!Supported || _entered)
        {
            return;
        }

        if (_forceInteractive)
        {
            _width = 110;
            _height = 34;
        }
        else if (_width == 0 || _height == 0)
        {
            try
            {
                _width = Console.WindowWidth;
                _height = Console.WindowHeight;
            }
            catch
            {
                return;
            }
        }

        if (_width < MinWidth || _height < MinHeight)
        {
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.Append(Ansi.EnterAltScreen);
        sb.Append(Ansi.ClearScreen);
        sb.Append(Ansi.HideCursor);
        Console.Write(sb.ToString());
        _entered = true;
    }

    /// <summary>Pantalla de bienvenida: Condor Grande da la bienvenida (mockup inicio).</summary>
    public void ShowWelcome()
    {
        lock (_gate)
        {
            if (!_entered)
            {
                return;
            }

            _mode = HostMode.Welcome;
            _dirtyAll = true;
        }
    }

    /// <summary>Indica si la pantalla esta en la fase de bienvenida (Condor Grande).</summary>
    public bool IsWelcome
    {
        get
        {
            lock (_gate)
            {
                return _mode == HostMode.Welcome;
            }
        }
    }

    /// <summary>Transicion a la sesion de trabajo: Condor Grande -> Condor Ave.</summary>
    public void ShowSession(string? model)
    {
        lock (_gate)
        {
            if (!_entered)
            {
                return;
            }

            _mode = HostMode.Session;
            _model = model;
            _dirtyAll = true;
        }
    }

    public void SetModel(string? model)
    {
        lock (_gate)
        {
            if (_model == model)
            {
                return;
            }

            _model = model;
            _dirtyHeader = true;
            _dirtyStatus = true;
        }
    }

    public void SetWorkspace(string? workspace)
    {
        lock (_gate)
        {
            if (_workspace == workspace)
            {
                return;
            }

            _workspace = workspace;
            _dirtyStatus = true;
        }
    }

    /// <summary>Actualiza el ESTADO REAL actual (nunca texto generico sin detalle).</summary>
    public void SetEstado(string text, ActivityKind kind = ActivityKind.System)
    {
        lock (_gate)
        {
            _estado = text;
            _estadoKind = kind;
            _dirtyStatus = true;
        }
    }

    public void SetProgreso(string text)
    {
        lock (_gate)
        {
            if (_progreso == text)
            {
                return;
            }

            _progreso = text;
            _dirtyStatus = true;
        }
    }

    public void SetBusy(bool busy)
    {
        lock (_gate)
        {
            if (_busy == busy)
            {
                return;
            }

            _busy = busy;
            _spin = 0;
            _dirtyInput = true;
            _dirtyStatus = true;
        }
    }

    /// <summary>Avanza el indicador de actividad (solo cuando hay trabajo real).</summary>
    public void Tick()
    {
        lock (_gate)
        {
            if (!_busy)
            {
                return;
            }

            _spin++;
            _dirtyStatus = true;
        }
    }

    /// <summary>
    /// Publica una linea en la zona Conversacion / Actividad. El texto se ajusta
    /// al ancho actual; si la ventana cambia de tamano se reajusta completa.
    /// </summary>
    public void AddActivity(string text, ActivityKind kind)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        lock (_gate)
        {
            if (!_entered)
            {
                // Sin pantalla TUI activa: degradacion honesta a lineas simples.
                Console.WriteLine(text);
                return;
            }

            _activity.Add((text.TrimEnd(), kind));
            if (_activity.Count > 400)
            {
                _activity.RemoveRange(0, _activity.Count - 400);
            }

            RewrapLocked();
            _dirtyActivity = true;
        }
    }

    /// <summary>Ejecuta una accion fuera de la pantalla TUI (salida de comandos /).</summary>
    public void Suspend(Action action)
    {
        LeaveScreen();
        try
        {
            action();
        }
        finally
        {
            ReenterScreen();
        }
    }

    public async Task SuspendAsync(Func<Task> action)
    {
        LeaveScreen();
        try
        {
            await action().ConfigureAwait(false);
        }
        finally
        {
            ReenterScreen();
        }
    }

    private void LeaveScreen()
    {
        lock (_gate)
        {
            if (!_entered)
            {
                return;
            }

            Console.Write(Ansi.ShowCursor + Ansi.LeaveAltScreen);
        }
    }

    private void ReenterScreen()
    {
        lock (_gate)
        {
            if (!_entered)
            {
                return;
            }

            Console.Write(Ansi.EnterAltScreen + Ansi.HideCursor);
            _dirtyAll = true;
        }
    }

    /// <summary>Detecta cambios de tamano de la ventana y reacomoda la pantalla.</summary>
    public void HandleResizeIfNeeded()
    {
        if (_forceInteractive)
        {
            return;
        }

        int w;
        int h;
        lock (_gate)
        {
            w = _width;
            h = _height;
        }

        try
        {
            if (Console.WindowWidth == w && Console.WindowHeight == h)
            {
                return;
            }
        }
        catch
        {
            return;
        }

        lock (_gate)
        {
            _width = Console.WindowWidth;
            _height = Console.WindowHeight;
            RewrapLocked();
            _dirtyAll = true;
        }
    }

    /// <summary>
    /// Repinta SOLO las regiones sucias. Es el unico metodo que escribe en la
    /// pantalla durante la sesion (aparte de la entrada, que pinta el editor).
    /// </summary>
    public void Repaint()
    {
        var frame = BuildFrame();
        if (frame.Length > 0)
        {
            Console.Write(frame);
        }
    }

    /// <summary>Construye la secuencia de repintado de las regiones sucias.</summary>
    private string BuildFrame()
    {
        lock (_gate)
        {
            if (!_entered || (!_dirtyAll && !_dirtyHeader && !_dirtyActivity && !_dirtyStatus && !_dirtyInput))
            {
                return "";
            }

            if (_width < MinWidth || _height < MinHeight)
            {
                return ""; // ventana demasiado pequena: se conserva lo ultimo dibujado
            }

            var sb = new System.Text.StringBuilder();
            if (_dirtyAll)
            {
                sb.Append(Ansi.ClearScreen);
                if (_mode == HostMode.Welcome)
                {
                    PaintWelcomeHeaderLocked(sb);
                    PaintWelcomeStatusLocked(sb);
                }
                else
                {
                    PaintChromeLocked(sb);
                    PaintActivityLocked(sb);
                    PaintStatusLocked(sb);
                    PaintProgresoLocked(sb);
                    PaintInputRegionLocked(sb);
                }
            }
            else
            {
                if (_dirtyHeader)
                {
                    PaintHeaderLocked(sb);
                    if (_mode == HostMode.Session)
                    {
                        PaintSeparatorLocked(sb, SepActivityRow, "Actividad del agente");
                        PaintSeparatorLocked(sb, SepInputRow, null);
                    }
                }

                if (_dirtyActivity && _mode == HostMode.Session)
                {
                    PaintActivityLocked(sb);
                }

                if (_dirtyStatus)
                {
                    PaintStatusLocked(sb);
                    PaintProgresoLocked(sb);
                }

                if (_dirtyInput && _mode == HostMode.Session)
                {
                    PaintInputRegionLocked(sb);
                }
            }

            _dirtyAll = _dirtyHeader = _dirtyActivity = _dirtyStatus = _dirtyInput = false;
            return sb.ToString();
        }
    }

    /// <summary>
    /// Fotograma completo actual (solo pruebas/demostracion): devuelve la
    /// secuencia ANSI exacta de un redibujado total SIN escribir en consola.
    /// </summary>
    internal string SnapshotFullFrame()
    {
        lock (_gate)
        {
            _dirtyAll = true;
        }

        return BuildFrame();
    }

    // ------------------------------------------------------------------ pintura

    private void PaintChromeLocked(System.Text.StringBuilder sb)
    {
        if (_mode == HostMode.Welcome)
        {
            PaintWelcomeHeaderLocked(sb);
            return;
        }

        PaintTitleRowLocked(sb);
        PaintHeaderLocked(sb);
        PaintSeparatorLocked(sb, SepActivityRow, "Actividad del agente");
        PaintSeparatorLocked(sb, SepInputRow, null);
    }

    private void PaintTitleRowLocked(System.Text.StringBuilder sb)
    {
        // Linea superior UNICA: identidad institucional + modelo REAL actual.
        // "Modo Local 100%" aparece una sola vez y el modelo es dinamico.
        sb.Append(Ansi.At(1, 2));
        sb.Append(Ansi.ClearLine);
        sb.Append(Ansi.Bold + Ansi.FgBlanco + "CONDOR" + Ansi.Reset);
        sb.Append(Ansi.FgGris + "  " + VersionInfo.DisplayName + Ansi.Reset);

        var right = IdentityLine;
        if (!string.IsNullOrWhiteSpace(_model))
        {
            right += " · " + _model;
        }

        var col = Math.Max(18, _width - right.Length - 1);
        sb.Append(Ansi.At(1, col));
        sb.Append(Ansi.FgDorado + right + Ansi.Reset);
    }

    private void PaintHeaderLocked(System.Text.StringBuilder sb)
    {
        if (_mode == HostMode.Welcome)
        {
            PaintWelcomeHeaderLocked(sb);
            return;
        }

        // Mascota oficial de trabajo (Condor Ave V16) acompana toda la sesion,
        // anclada a la DERECHA. Ningun texto invade su area: la informacion
        // vive en la linea superior. El bloque se posiciona completo.
        var columna = ColumnaMascotaDerecha();
        for (var i = 0; i < CondorArt.Ave.Length && i + 2 <= HeaderHeight; i++)
        {
            var row = 2 + i; // fila 2..14
            sb.Append(Ansi.At(row, columna));
            sb.Append(Ansi.ClearLine);
            sb.Append(Ansi.Paint(CondorArt.Ave[i]) + Ansi.Reset);
        }
    }

    /// <summary>Columna (1-based) que ancla el Ave completo a la derecha del ancho actual.</summary>
    internal int ColumnaMascotaDerecha()
        => Math.Max(1, _width - AnchoVisibleMascota() - 5);

    /// <summary>Ancho visible maximo de las filas del Ave (sin secuencias SGR).</summary>
    internal static int AnchoVisibleMascota()
    {
        var maximo = 0;
        foreach (var fila in CondorArt.Ave)
        {
            maximo = Math.Max(maximo, Ansi.VisibleWidth(fila));
        }
        return maximo;
    }

    private void PaintWelcomeHeaderLocked(System.Text.StringBuilder sb)
    {
        // Bienvenida con Condor Grande centrado (mockup "01. INICIO").
        var title = "CONDOR";
        sb.Append(Ansi.At(1, Math.Max(1, (_width - title.Length) / 2)));
        sb.Append(Ansi.Bold + Ansi.FgBlanco + title + Ansi.Reset);

        sb.Append(Ansi.At(2, Math.Max(1, (_width - Slogan.Length) / 2)));
        sb.Append(Ansi.FgTerracota + Slogan + Ansi.Reset);

        sb.Append(Ansi.At(3, Math.Max(1, (_width - IdentityLine.Length) / 2)));
        sb.Append(Ansi.FgDorado + IdentityLine + Ansi.Reset);

        sb.Append(Ansi.At(4, Math.Max(1, (_width - VersionInfo.DisplayName.Length) / 2)));
        sb.Append(Ansi.FgGris + VersionInfo.DisplayName + Ansi.Reset);

        var artLeft = Math.Max(1, (_width - CondorArt.GrandeWidth) / 2);
        for (var i = 0; i < CondorArt.Grande.Length && 5 + i <= 18; i++)
        {
            sb.Append(Ansi.At(5 + i, artLeft));
            sb.Append(Ansi.Paint(CondorArt.Grande[i]) + Ansi.Reset);
        }
    }

    private void PaintSeparatorLocked(System.Text.StringBuilder sb, int row, string? label)
    {
        sb.Append(Ansi.At(row + 1, 1)); // At() es 1-based sobre filas logicas 0-based
        sb.Append(Ansi.ClearLine);
        sb.Append(Ansi.FgGris + "──" + Ansi.Reset);
        if (label is null)
        {
            sb.Append(Ansi.FgGris + new string('─', Math.Max(0, _width - 2)) + Ansi.Reset);
            return;
        }

        sb.Append(" " + Ansi.FgTerracota + label + Ansi.Reset + " ");
        var used = 4 + label.Length + 1;
        sb.Append(Ansi.FgGris + new string('─', Math.Max(0, _width - used)) + Ansi.Reset);
    }

    private void PaintActivityLocked(System.Text.StringBuilder sb)
    {
        var height = ActivityHeight;
        if (height <= 0)
        {
            return;
        }

        var take = Math.Min(height, _wrapped.Count);
        var skip = _wrapped.Count - take;
        for (var i = 0; i < height; i++)
        {
            sb.Append(Ansi.At(ActivityTop + 1 + i, 1));
            sb.Append(Ansi.ClearLine);
            if (i < take)
            {
                var (line, kind) = _wrapped[skip + i];
                sb.Append(ActivityPrefix(kind) + line + Ansi.Reset);
            }
        }
    }

    private string ActivityPrefix(ActivityKind kind)
    {
        return kind switch
        {
            ActivityKind.User => "  " + Ansi.FgBlanco + "› ",
            ActivityKind.Condor => "  " + Ansi.FgCrema + "◆ ",
            ActivityKind.Success => "  " + Ansi.FgVerde + "● ",
            ActivityKind.Warning => "  " + Ansi.FgAmarillo + "▲ ",
            ActivityKind.Error => "  " + Ansi.FgRojo + "✗ ",
            _ => "  " + Ansi.FgVerde + "● "
        };
    }

    private void PaintStatusLocked(System.Text.StringBuilder sb)
    {
        if (_mode == HostMode.Welcome)
        {
            PaintWelcomeStatusLocked(sb);
            return;
        }

        // Barra de estado persistente en la ULTIMA fila: workspace | modelo | estado | version.
        sb.Append(Ansi.At(StatusBarRow + 1, 1));
        sb.Append(Ansi.ClearLine);
        var frame = _busy ? SpinnerFrames[_spin % SpinnerFrames.Length] + " " : "";
        var version = VersionInfo.DisplayName;
        var versionCol = Math.Max(1, _width - version.Length - 2);
        // Anchos dinamicos generosos: el estado NUNCA se recorta (debe verse completo).
        var workspaceWidth = 28;
        var modelWidth = 22;
        var statusWidth = Math.Max(30, _width - version.Length - 2 - 4 - 3 - 3 - 3 - 3 - workspaceWidth - modelWidth - (_busy ? 2 : 0));
        var workspace = Clip(_workspace ?? "—", workspaceWidth);
        sb.Append(Ansi.FgGris + " " + Ansi.FgDorado + ">" + Ansi.Reset);
        sb.Append(Ansi.FgBlanco + " " + workspace + Ansi.Reset);
        sb.Append(Ansi.FgGris + " | " + Ansi.Reset);
        var model = Clip(_model ?? "—", modelWidth);
        sb.Append(Ansi.FgTerracota + "*" + Ansi.Reset);
        sb.Append(Ansi.FgBlanco + " " + model + Ansi.Reset);
        sb.Append(Ansi.FgGris + " | " + Ansi.Reset);
        var status = _estado; // nunca se recorta
        sb.Append(EstadoColor(_estadoKind) + frame + status + Ansi.Reset);
        sb.Append(Ansi.At(StatusBarRow + 1, versionCol));
        sb.Append(Ansi.FgGris + version + Ansi.Reset);
    }

    /// <summary>
    /// Linea de progreso/iteracion justo encima de la barra de estado. Mantiene
    /// visible la informacion de ejecucion (p. ej. "Iteracion 2") durante la sesion.
    /// </summary>
    private void PaintProgresoLocked(System.Text.StringBuilder sb)
    {
        if (_mode == HostMode.Welcome)
        {
            return; // el progreso se muestra en PaintWelcomeStatusLocked
        }

        sb.Append(Ansi.At(ProgresoRow + 1, 1));
        sb.Append(Ansi.ClearLine);
        var progreso = string.IsNullOrWhiteSpace(_progreso) ? "—" : _progreso;
        sb.Append(Ansi.FgGris + " " + Ansi.FgCian + progreso + Ansi.Reset);
    }

    private string EstadoColor(ActivityKind kind)
    {
        return kind switch
        {
            ActivityKind.Error => Ansi.FgRojo,
            ActivityKind.Warning => Ansi.FgAmarillo,
            ActivityKind.Success => Ansi.FgVerde,
            _ => Ansi.FgCrema
        };
    }

    /// <summary>
    /// Comunicacion de la fase de bienvenida: bajo el bloque de Condor Grande,
    /// sin el cromo de la sesion (mockup "01. INICIO"). Sin titulares.
    /// </summary>
    private void PaintWelcomeStatusLocked(System.Text.StringBuilder sb)
    {
        sb.Append(Ansi.At(18, 3));
        sb.Append(Ansi.ClearLine);
        sb.Append(Ansi.FgGris + "Workspace: " + Clip(_workspace ?? "—", _width - 16) + Ansi.Reset);

        sb.Append(Ansi.At(19, 3));
        sb.Append(Ansi.ClearLine);
        sb.Append(Ansi.FgGris + Clip(string.IsNullOrWhiteSpace(_progreso) ? "—" : _progreso, _width - 6) + Ansi.Reset);

        sb.Append(Ansi.At(20, 3));
        sb.Append(Ansi.ClearLine);
        sb.Append(EstadoColor(_estadoKind) + Clip(_estado, _width - 6) + Ansi.Reset);
    }

    /// <summary>Pinta la region de entrada. El contenido lo dibuja el editor.</summary>
    private void PaintInputRegionLocked(System.Text.StringBuilder sb)
    {
        if (_mode == HostMode.Welcome)
        {
            return;
        }

        sb.Append(Ansi.At(InputRow + 1, 1));
        sb.Append(Ansi.ClearLine);
        if (_busy)
        {
            sb.Append(" " + Ansi.FgAmarillo + "Condor esta trabajando...  Esc + Esc interrumpe la tarea" + Ansi.Reset);
        }
        else
        {
            // Estado inicial de la zona de entrada: la invitacion al usuario.
            sb.Append(" " + Ansi.Bold + Ansi.FgDorado + "> " + Ansi.Reset + Ansi.FgGris + Placeholder + Ansi.Reset);
        }
    }

    /// <summary>Dibuja la linea de entrada con cursor en bloque (caret inverso).</summary>
    public void RenderInput(string buffer, int caret)
    {
        string snapshot;
        lock (_gate)
        {
            if (!_entered || _mode != HostMode.Session || _busy)
            {
                return;
            }

            snapshot = BuildInputLocked(buffer, caret);
        }

        Console.Write(snapshot);
    }

    private string BuildInputLocked(string buffer, int caret)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(Ansi.At(InputRow + 1, 1));
        sb.Append(Ansi.ClearLine);

        var width = Math.Max(10, _width - 4);
        if (buffer.Length == 0)
        {
            sb.Append(" " + Ansi.Bold + Ansi.FgDorado + "> " + Ansi.Reset + Ansi.FgGris + Placeholder + Ansi.Reset);
            return sb.ToString();
        }

        sb.Append(" " + Ansi.Bold + Ansi.FgDorado + "> " + Ansi.Reset);
        var budget = width - 2;
        var start = 0;
        if (buffer.Length > budget)
        {
            start = Math.Max(0, Math.Min(caret - budget / 2, buffer.Length - budget));
        }

        for (var i = start; i < buffer.Length && (i - start) < budget; i++)
        {
            if (i == caret)
            {
                sb.Append(Ansi.Esc + "7m" + buffer[i] + Ansi.Reset);
            }
            else
            {
                sb.Append(Ansi.FgBlanco.ToString() + buffer[i]);
            }
        }

        if (caret >= buffer.Length)
        {
            sb.Append(Ansi.Esc + "7m" + " " + Ansi.Reset);
        }
        else
        {
            sb.Append(Ansi.Reset);
        }

        return sb.ToString();
    }

    // ------------------------------------------------------------------ utilidades

    private void RewrapLocked()
    {
        _wrapped.Clear();
        var width = Math.Max(20, _width - 4);
        foreach (var (text, kind) in _activity)
        {
            foreach (var line in WordWrap(text, width))
            {
                _wrapped.Add((line, kind));
            }
        }
    }

    private static IEnumerable<string> WordWrap(string text, int width)
    {
        if (text.Length <= width)
        {
            yield return text;
            yield break;
        }

        var words = text.Split(' ');
        var current = new System.Text.StringBuilder();
        foreach (var word in words)
        {
            if (current.Length == 0)
            {
                if (word.Length > width)
                {
                    // Palabra mas larga que el ancho: corte duro.
                    var rest = word;
                    while (rest.Length > width)
                    {
                        yield return rest[..width];
                        rest = rest[width..];
                    }

                    current.Append(rest);
                }
                else
                {
                    current.Append(word);
                }

                continue;
            }

            if (current.Length + 1 + word.Length > width)
            {
                yield return current.ToString();
                current.Clear();
                if (word.Length > width)
                {
                    var rest = word;
                    while (rest.Length > width)
                    {
                        yield return rest[..width];
                        rest = rest[width..];
                    }

                    current.Append(rest);
                }
                else
                {
                    current.Append(word);
                }
            }
            else
            {
                current.Append(' ').Append(word);
            }
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }

    private static string Clip(string text, int maxWidth)
    {
        if (maxWidth <= 3)
        {
            return text;
        }

        return text.Length <= maxWidth ? text : text[..(maxWidth - 1)] + "…";
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_entered)
            {
                Console.Write(Ansi.ShowCursor + Ansi.LeaveAltScreen);
                _entered = false;
            }
        }

        GC.SuppressFinalize(this);
    }

    private enum HostMode
    {
        Welcome,
        Session
    }
}