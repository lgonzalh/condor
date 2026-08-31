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

    private const int BottomRows = 3;             // separador entrada + entrada + estado minimo

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

    // Filas logicas (0-based) de la sesion. La convencion de pintura usa At(fila+1): "fila" 0-based.
    private int ContentTop => 2;                             // primera fila de contenido (tras cabecera + separador)
    private int SepInputRow => _height - BottomRows;         // separador antes de la entrada
    public int InputRow => _height - BottomRows + 1;         // fila de entrada
    private int EstadoRow => _height - BottomRows + 2;       // fila de estado minimo (ultima)
    private int ContentBottom => SepInputRow - 1;            // ultima fila utilizable de contenido

    // Mascota integrada (un solo acento visual, sin panel ni titulo) y margen reservado.
    private int ArtCol => Math.Max(1, _width - CondorArt.AveWidth - 2);   // columna 1-based
    private int FeedWidth => Math.Max(20, ArtCol - 5);                    // ancho del contenido (no choca con la mascota)

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
    /// Publica una linea en la zona de contenido de la sesion. Es una unica fuente viva de
    /// actividad (como un log de CLI): el contenido determina que se muestra, sin encabezados
    /// conceptuales. El texto se ajusta al ancho actual; si la ventana cambia de tamano se
    /// reajusta completo.
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
                    PaintMascotaLocked(sb);
                    PaintEstadoLocked(sb);
                    PaintInputRegionLocked(sb);
                }
            }
            else
            {
                if (_dirtyHeader && _mode == HostMode.Session)
                {
                    PaintTitleRowLocked(sb);
                    PaintSeparatorLocked(sb, 1, null);
                    PaintSeparatorLocked(sb, SepInputRow, null);
                }

                if (_dirtyActivity && _mode == HostMode.Session)
                {
                    PaintActivityLocked(sb);
                    PaintMascotaLocked(sb); // tras la actividad: no es borrada por ClearLine
                }

                if (_dirtyStatus && _mode == HostMode.Session)
                {
                    PaintEstadoLocked(sb);
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
        PaintSeparatorLocked(sb, 1, null);        // separador bajo la cabecera
        PaintSeparatorLocked(sb, SepInputRow, null); // separador sobre la entrada
    }

    private void PaintTitleRowLocked(System.Text.StringBuilder sb)
    {
        // Linea superior UNICA: marca + version, y a la derecha el modelo REAL actual.
        // La version aparece aqui UNA unica vez; el modelo aparece UNA unica vez.
        sb.Append(Ansi.At(1, 2));
        sb.Append(Ansi.ClearLine);
        sb.Append(Ansi.Bold + Ansi.FgBlanco + "CONDOR" + Ansi.Reset);
        sb.Append(Ansi.FgGris + "  " + VersionInfo.DisplayName + Ansi.Reset);

        var model = _model ?? "—";
        var col = Math.Max(18, _width - model.Length - 2);
        sb.Append(Ansi.At(1, col));
        sb.Append(Ansi.FgTerracota + model + Ansi.Reset);
    }

    /// <summary>
    /// Integra la mascota PEQUENA como acento visual arriba a la derecha de la zona de
    /// contenido. No es un titulo, ni un panel independiente, ni repite metadata: solo el
    /// arte (una sola mascota). El contenido de la izquierda no invade su area
    /// (<see cref="FeedWidth"/> deja margen).
    /// </summary>
    private void PaintMascotaLocked(System.Text.StringBuilder sb)
    {
        var col = ArtCol;
        var art = CondorArt.Ave;
        for (var i = 0; i < art.Length && ContentTop + i <= ContentBottom; i++)
        {
            sb.Append(Ansi.At(ContentTop + i + 1, col));
            sb.Append(Ansi.Paint(art[i]));
            sb.Append(Ansi.Reset);
        }
    }

    /// <summary>
    /// Ancho visible maximo de las filas del Ave (sin secuencias SGR).
    /// </summary>
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
        PaintSeparatorLocked(sb, row, label, _width);
    }

    private void PaintSeparatorLocked(System.Text.StringBuilder sb, int row, string? label, int width)
    {
        sb.Append(Ansi.At(row + 1, 1)); // At() es 1-based sobre filas logicas 0-based
        sb.Append(Ansi.ClearLine);
        sb.Append(Ansi.FgGris + "──" + Ansi.Reset);
        if (label is null)
        {
            sb.Append(Ansi.FgGris + new string('─', Math.Max(0, width - 2)) + Ansi.Reset);
            return;
        }

        sb.Append(" " + Ansi.FgTerracota + label + Ansi.Reset + " ");
        var used = 4 + label.Length + 1;
        sb.Append(Ansi.FgGris + new string('─', Math.Max(0, width - used)) + Ansi.Reset);
    }

    /// <summary>
    /// Zona de contenido: UNA unica fuente viva de actividad (estilo log de CLI). No hay
    /// encabezados conceptuales; el contenido determina que se muestra. Muestra el final
    /// del flujo (lo mas reciente abajo) dentro del area disponible.
    /// </summary>
    private void PaintActivityLocked(System.Text.StringBuilder sb)
    {
        var height = ContentBottom - ContentTop + 1;
        if (height <= 0)
        {
            return;
        }

        var take = Math.Min(height, _wrapped.Count);
        var skip = Math.Max(0, _wrapped.Count - take);
        for (var i = 0; i < height; i++)
        {
            sb.Append(Ansi.At(ContentTop + i + 1, 1));
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

    /// <summary>
    /// Estado minimo en la ultima fila: el estado real actual (con spinner si hay trabajo)
    /// y, a la derecha, el workspace. No repite version ni modelo (viven una sola vez en la
    /// cabecera).
    /// </summary>
    private void PaintEstadoLocked(System.Text.StringBuilder sb)
    {
        if (_mode == HostMode.Welcome)
        {
            PaintWelcomeStatusLocked(sb);
            return;
        }

        sb.Append(Ansi.At(EstadoRow + 1, 1));
        sb.Append(Ansi.ClearLine);
        var frame = _busy ? SpinnerFrames[_spin % SpinnerFrames.Length] + " " : "";
        sb.Append(EstadoColor(_estadoKind) + frame + _estado + Ansi.Reset);

        var ws = _workspace ?? "—";
        sb.Append(Ansi.At(EstadoRow + 1, Math.Max(1, _width - ws.Length - 2)));
        sb.Append(Ansi.FgGris + ws + Ansi.Reset);
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
        var width = Math.Max(20, FeedWidth - 4);
        _wrapped.Clear();
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