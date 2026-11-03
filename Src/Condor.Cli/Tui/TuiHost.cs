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
    Condor,

    /// <summary>Analisis y observaciones del agente.</summary>
    Analysis,

    /// <summary>Edicion o construccion de codigo.</summary>
    CodeEdit,

    /// <summary>Respuesta final del agente.</summary>
    Response,

    /// <summary>Pregunta que requiere respuesta del usuario.</summary>
    Question
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
///   [ Condor Ave compacta ]  CONDOR  v1.0 · build interno X
///                            Observa · Comprende · Planifica · Construye · Verifica
///                            Hecho en Colombia · Modo Local 100%  [· modelo real]
///                            ©Condor 2026
///   ── Actividad del agente ────────────────────────────────
///   (historial vivo de la conversacion y la actividad)
///   &gt; ¿que deseas construir...?
///   (barra de estado de una linea: segundos | RAM | ruta | atajos)
///
/// La identidad institucional y el modelo REAL seleccionado comparten la linea
/// de identidad de la cabecera, fuera del area del arte; "Modo Local 100%"
/// aparece una unica vez. Si el modelo cambia, la linea se actualiza sola.
/// </summary>
public sealed class TuiHost : IDisposable
{
    private const string IdentityLine = "Hecho en Colombia · Modo Local 100%";
    private const string CopyrightLine = "©Condor 2026";
    private const string Slogan = "Observa · Comprende · Planifica · Construye · Verifica";
    private const string Placeholder = "¿Qué deseas construir? ...";
    internal const int MinWidth = 80;
    internal const int MinHeight = 24;

    private const int BottomRows = 3;             // separador + entrada + estado/hints
    private const int HeaderRows = 5;             // cuatro líneas de mascota + separador inferior
    private const string AgentsHint = "tab agentes";
    private const string CommandsHint = "ctrl+p comandos";
    private const string HintGap = "  ";
    private static string Shortcuts => AgentsHint + HintGap + CommandsHint;

    private static readonly string[] SpinnerFrames = { "◐", "◑", "◒", "◕", "◔" };

    private readonly object _gate = new();
    private readonly List<(string Text, ActivityKind Kind)> _activity = new();
    private readonly List<(string Line, ActivityKind Kind)> _wrapped = new();
    private string? _overlayTitle;
    private List<string> _overlayItems = new();
    private List<bool> _overlayDisabledItems = new();
    private int _overlaySelectedIndex = -1;
    private SelectListOverlay? _selectListModel;
    private RenderedSelectListOverlay? _selectListOverlay;
    private readonly Dictionary<(int Row, int Column), string> _paintedSelectListRows = new();
    private readonly TuiMouseTrackingMode _mouseTrackingMode = new();
    private TuiTheme _tuiTheme = TuiTheme.Default;

    private int _width;
    private int _height;
    private bool _entered;
    private bool _disposed;
    private bool _forceInteractive;

    private HostMode _mode = HostMode.Welcome;
    private string? _model;
    private string? _workspace;
    private string _agentName = "Condor principal";
    private string _inputPlaceholder = Placeholder;
    private string _estado = "Iniciando";
    private ActivityKind _estadoKind = ActivityKind.System;
    private string _progreso = "";
    private string _modelStatus = "Modelo local · esperando una tarea";
    private ActivityKind _modelStatusKind = ActivityKind.System;
    private bool _modelWorking;
    private bool _busy;
    private int _spin;

    private bool _dirtyAll = true;
    private bool _dirtyHeader;
    private bool _dirtyActivity;
    private bool _dirtyStatus;
    private bool _dirtyInput;
    private bool _dirtyOverlay;
    private bool _showEmptyInputCursor;

    // Filas logicas (0-based) de la sesion. La convencion de pintura usa At(fila+1): "fila" 0-based.
    // La cabecera de Trabajo ocupa cuatro filas de mascota y una línea divisoria.
    private int ContentTop => HeaderRows + 1;
    private int SepInputRow => _height - BottomRows;         // separador antes de la entrada
    public int InputRow => _height - BottomRows + 1;         // fila de entrada
    private int EstadoRow => _height - BottomRows + 2;        // estado, equipo, ruta y atajos
    private int ContentBottom => SepInputRow - 1;            // ultima fila utilizable de contenido

    // Composicion de referencia: la mascota se ancla a la IZQUIERDA del area de
    // contenido (no vive en una columna lateral). El margen izquierdo del feed
    // de actividad reserva su ancho para que el texto dinamico no la pise.
    private int ArtCol => 2;                                                  // columna 1-based
    private int ArtRow => ContentTop;                                        // fila logica 0-based
    private int FeedLeftMargin => 0;                                           // el feed usa todo el ancho
    private int FeedWidth => Math.Max(20, _width - 4);                        // ancho del feed dinamico

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
    public int TerminalWidth
    {
        get
        {
            lock (_gate) return _width > 0 ? _width : MinWidth;
        }
    }

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
        _mouseTrackingMode.Disable();
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
            _modelStatus = string.IsNullOrWhiteSpace(model) ? "Modelo local · esperando una tarea" : model + " · listo";
            _modelStatusKind = ActivityKind.System;
            _modelWorking = false;
            _dirtyAll = true;
        }

        _mouseTrackingMode.Enable();
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
            _modelStatus = string.IsNullOrWhiteSpace(model) ? "Modelo local · esperando una tarea" : model + " · listo";
            _modelStatusKind = ActivityKind.System;
            _modelWorking = false;
            _dirtyHeader = true;
            _dirtyStatus = true;
        }
    }

    public void SetTuiTheme(TuiTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        lock (_gate)
        {
            _tuiTheme = theme;
            var rendered = _selectListModel is null ? null : RenderSelectListOverlayLocked(_selectListModel);
            if (!SelectListOverlayEquals(_selectListOverlay, rendered))
            {
                _selectListOverlay = rendered;
                _dirtyOverlay = true;
            }

            _dirtyInput = true;
        }
    }

    public void SetRuntimeMetadata(string? agentName, string? providerName, string? modelVariant)
    {
        lock (_gate)
        {
            _agentName = string.IsNullOrWhiteSpace(agentName) ? "Condor principal" : agentName.Trim();
            _dirtyHeader = true;
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

    public void SetModelStatus(string text, bool working = false, ActivityKind kind = ActivityKind.System)
    {
        lock (_gate)
        {
            if (_modelStatus == text && _modelWorking == working && _modelStatusKind == kind)
            {
                return;
            }

            _modelStatus = text;
            _modelWorking = working;
            _modelStatusKind = kind;
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

            _mouseTrackingMode.Disable();
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
            _mouseTrackingMode.Enable();
            _dirtyAll = true;
        }
    }

    /// <summary>Detecta cambios de tamano de la ventana y reacomoda la pantalla.</summary>
    public bool HandleResizeIfNeeded()
    {
        if (_forceInteractive)
        {
            return false;
        }

        try
        {
            return ResizeTo(Console.WindowWidth, Console.WindowHeight);
        }
        catch
        {
            return false;
        }
    }

    internal bool ResizeTo(int width, int height)
    {
        lock (_gate)
        {
            if (_width == width && _height == height)
            {
                return false;
            }

            _width = width;
            _height = height;
            RewrapLocked();
            _dirtyAll = true;
        }

        return true;
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
            if (!_entered || (!_dirtyAll && !_dirtyHeader && !_dirtyActivity && !_dirtyStatus &&
                              !_dirtyInput && !_dirtyOverlay))
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
                    PaintSelectListOverlayDiffLocked(sb, force: true, underlayPainted: true);
                    PaintEstadoLocked(sb);
                    PaintInputRegionLocked(sb);
                }
            }
            else
            {
                if (_dirtyHeader && _mode == HostMode.Session)
                {
                    PaintTitleRowLocked(sb);
                    PaintSeparatorLocked(sb, HeaderRows - 1, null);
                    PaintSeparatorLocked(sb, SepInputRow, null);
                }

                var activityPainted = false;
                if (_dirtyActivity && _mode == HostMode.Session)
                {
                    PaintActivityLocked(sb);
                    activityPainted = true;
                }

                if ((_dirtyOverlay || activityPainted) && _mode == HostMode.Session)
                {
                    PaintSelectListOverlayDiffLocked(sb, force: activityPainted, underlayPainted: activityPainted);
                }

                if (_dirtyStatus && _mode == HostMode.Session)
                {
                    PaintEstadoLocked(sb);
                }
                else if (_dirtyStatus && _mode == HostMode.Welcome)
                {
                    PaintWelcomeStatusLocked(sb);
                }

                if (_dirtyInput && _mode == HostMode.Session)
                {
                    PaintInputRegionLocked(sb);
                }
            }

            AppendInputCursorStateLocked(sb);
            _dirtyAll = _dirtyHeader = _dirtyActivity = _dirtyStatus = _dirtyInput = _dirtyOverlay = false;
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
        // Línea completa inmediatamente debajo de la última línea de la mascota.
        PaintSeparatorLocked(sb, HeaderRows - 1, null);
        // Separador de la zona de entrada inferior.
        PaintSeparatorLocked(sb, SepInputRow, null);
    }

    private void PaintTitleRowLocked(System.Text.StringBuilder sb)
    {
        // Cabecera limpia de Trabajo: sin marco superior ni columnas laterales.
        var aveWidth = AnchoVisibleMascota();
        var textLeftCol = ArtCol + aveWidth + 3;

        for (int row = 1; row <= HeaderRows; row++)
        {
            sb.Append(Ansi.At(row, 1));
            sb.Append(Ansi.ClearLine);

            if (row - 1 < CondorArt.Ave.Length)
            {
                sb.Append(Ansi.At(row, ArtCol));
                sb.Append(Ansi.Paint(CondorArt.Ave[row - 1]));
                sb.Append(Ansi.Reset);
            }

            switch (row)
            {
                case 1:
                    var title = "CONDOR    " + VersionInfo.DisplayName;
                    var titleCol = Math.Max(textLeftCol, (_width - title.Length) / 2 + 1);
                    PaintHeaderTextLocked(sb, row, titleCol, title, Ansi.FgBlanco);
                    break;
                case 2:
                    PaintHeaderTextLocked(sb, row, textLeftCol, Slogan, Ansi.FgTerracota);
                    break;
                case 3:
                    PaintHeaderTextLocked(
                        sb,
                        row,
                        textLeftCol,
                        IdentityLine + " · " + (_model ?? "—"),
                        Ansi.FgDorado);
                    break;
                case 4:
                    PaintHeaderTextLocked(sb, row, textLeftCol, CopyrightLine, Ansi.FgGris);
                    break;
            }
        }
    }

    private void PaintHeaderTextLocked(System.Text.StringBuilder sb, int row, int textLeftCol, string text, string color)
    {
        if (textLeftCol > _width)
        {
            return;
        }

        sb.Append(Ansi.At(row, textLeftCol));
        sb.Append(color + Clip(text, _width - textLeftCol + 1) + Ansi.Reset);
    }

    /// <summary>
    /// Integra la mascota PEQUENA como acento visual arriba a la derecha de la zona de
    /// contenido. No es un titulo, ni un panel independiente, ni repite metadata: solo el
    /// arte (una sola mascota). El contenido de la izquierda no invade su area
    /// (<see cref="FeedWidth"/> deja margen).
    /// </summary>
    private void PaintMascotaLocked(System.Text.StringBuilder sb)
    {
        // Mascota PEQUENA anclada a la IZQUIERDA, integrada al area de contenido.
        // Comparte filas con la actividad (no es panel lateral ni columna separada).
        var col = ArtCol;
        var row = ArtRow;
        var art = CondorArt.Ave;
        for (var i = 0; i < art.Length && row + i <= ContentBottom; i++)
        {
            sb.Append(Ansi.At(row + i + 1, col));
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
        // Bienvenida original: Condor Grande centrado (mockup "01. INICIO").
        var title = "CONDOR";
        sb.Append(Ansi.At(1, Math.Max(1, (_width - title.Length) / 2)));
        sb.Append(Ansi.Bold + Ansi.FgBlanco + title + Ansi.Reset);

        sb.Append(Ansi.At(2, Math.Max(1, (_width - Slogan.Length) / 2)));
        sb.Append(Ansi.FgTerracota + Slogan + Ansi.Reset);

        sb.Append(Ansi.At(3, Math.Max(1, (_width - IdentityLine.Length) / 2)));
        sb.Append(Ansi.FgDorado + IdentityLine + Ansi.Reset);

        var versionLine = VersionInfo.DisplayName + " · " + CopyrightLine;
        sb.Append(Ansi.At(4, Math.Max(1, (_width - versionLine.Length) / 2 + 1)));
        sb.Append(Ansi.FgGris + versionLine + Ansi.Reset);

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
        // Usar box-drawing ─ en lugar de ASCII - para los separadores horizontales
        sb.Append(Ansi.FgGris + "─" + Ansi.Reset);
        if (label is null)
        {
            sb.Append(Ansi.FgGris + new string('─', Math.Max(0, width - 1)) + Ansi.Reset);
            return;
        }

        sb.Append(" " + Ansi.FgTerracota + label + Ansi.Reset + " ");
        var used = 1 + label.Length + 1;
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
        var left = 2;
        for (var i = 0; i < height; i++)
        {
            sb.Append(Ansi.At(ContentTop + i + 1, left));
            sb.Append(Ansi.ClearLine);
            if (i < take)
            {
                var (line, kind) = _wrapped[skip + i];
                sb.Append(ActivityPrefix(kind) + line + Ansi.Reset);
            }
        }

        PaintOverlayLocked(sb, height);
    }

    private void PaintSelectListOverlayDiffLocked(
        System.Text.StringBuilder sb,
        bool force,
        bool underlayPainted)
    {
        var nextRows = GetSelectListRowsLocked();
        var next = nextRows.ToDictionary(row => (row.Row, row.Column), row => row.Line);

        if (underlayPainted)
        {
            _paintedSelectListRows.Clear();
            force = true;
        }
        else
        {
            foreach (var oldRow in _paintedSelectListRows.Keys)
            {
                if (!next.ContainsKey(oldRow))
                {
                    PaintActivityRowLocked(sb, oldRow.Row - 1);
                }
            }
        }

        foreach (var row in nextRows)
        {
            var key = (row.Row, row.Column);
            if (force || !_paintedSelectListRows.TryGetValue(key, out var oldLine) ||
                !string.Equals(oldLine, row.Line, StringComparison.Ordinal))
            {
                sb.Append(Ansi.At(row.Row, row.Column));
                sb.Append(row.Line);
            }
        }

        _paintedSelectListRows.Clear();
        foreach (var row in next)
        {
            _paintedSelectListRows[row.Key] = row.Value;
        }
    }

    private List<(int Row, int Column, string Line)> GetSelectListRowsLocked()
    {
        var overlay = _selectListOverlay;
        if (overlay is null || overlay.Lines.Count == 0)
        {
            return new List<(int Row, int Column, string Line)>();
        }

        var contentHeight = Math.Max(0, ContentBottom - ContentTop + 1);
        var firstRow = overlay.Placement == SelectListPlacement.Centered
            ? ContentTop + Math.Max(0, (contentHeight - overlay.Lines.Count) / 2)
            : Math.Max(ContentTop, ContentBottom - overlay.Lines.Count + 1);
        var firstColumn = Math.Clamp(overlay.Column + 1, 1, Math.Max(1, _width - overlay.Width + 1));
        var lines = new List<(int Row, int Column, string Line)>(overlay.Lines.Count);
        for (var index = 0; index < overlay.Lines.Count; index++)
        {
            var row = firstRow + index;
            if (row > ContentBottom)
            {
                break;
            }

            lines.Add((row + 1, firstColumn, overlay.Lines[index]));
        }

        return lines;
    }

    private RenderedSelectListOverlay RenderSelectListOverlayLocked(SelectListOverlay overlay)
    {
        var terminalWidth = Math.Max(MinWidth, _width);
        var widthLimit = overlay.MaximumWidth.HasValue
            ? Math.Min(terminalWidth, Math.Max(1, overlay.MaximumWidth.Value))
            : terminalWidth;
        var column = overlay.Placement == SelectListPlacement.Centered
            ? Math.Max(0, (terminalWidth - widthLimit) / 2)
            : 0;
        var lines = SelectListRenderer.Render(overlay.State, widthLimit, overlay.Options, _tuiTheme).ToArray();
        return new RenderedSelectListOverlay(lines, overlay.Placement, widthLimit, column);
    }

    private void PaintActivityRowLocked(System.Text.StringBuilder sb, int row)
    {
        if (row < ContentTop || row > ContentBottom)
        {
            return;
        }

        var contentHeight = ContentBottom - ContentTop + 1;
        var rowIndex = row - ContentTop;
        var take = Math.Min(contentHeight, _wrapped.Count);
        var skip = Math.Max(0, _wrapped.Count - take);
        sb.Append(Ansi.At(row + 1, 2));
        sb.Append(Ansi.ClearLine);
        if (rowIndex < take)
        {
            var (line, kind) = _wrapped[skip + rowIndex];
            sb.Append(ActivityPrefix(kind) + line + Ansi.Reset);
        }
    }

    private static bool SelectListOverlayEquals(RenderedSelectListOverlay? left, RenderedSelectListOverlay? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        return left.Placement == right.Placement && left.Width == right.Width && left.Column == right.Column &&
               left.Lines.SequenceEqual(right.Lines, StringComparer.Ordinal);
    }

    private void PaintOverlayLocked(System.Text.StringBuilder sb, int contentHeight)
    {
        if (string.IsNullOrWhiteSpace(_overlayTitle) || _overlayItems.Count == 0 || contentHeight < 2)
        {
            return;
        }

        var visibleItems = Math.Min(_overlayItems.Count, Math.Min(8, contentHeight - 1));
        var firstItem = _overlaySelectedIndex >= 0
            ? Math.Clamp(_overlaySelectedIndex - visibleItems + 1, 0, Math.Max(0, _overlayItems.Count - visibleItems))
            : Math.Max(0, _overlayItems.Count - visibleItems);
        var rows = visibleItems + 1;
        var firstRow = ContentBottom - rows + 1;
        var maxTextWidth = Math.Max(1, _width - 4);

        sb.Append(Ansi.At(firstRow + 1, 1));
        sb.Append(Ansi.ClearLine);
        sb.Append("  " + Ansi.Bold + Ansi.FgGris + Clip(_overlayTitle, maxTextWidth) + Ansi.Reset);

        for (var row = 0; row < visibleItems; row++)
        {
            var itemIndex = firstItem + row;
            var disabled = itemIndex < _overlayDisabledItems.Count && _overlayDisabledItems[itemIndex];
            var selected = itemIndex == _overlaySelectedIndex && !disabled;
            var prefix = selected ? "▸ " : disabled ? "× " : "  ";
            var text = prefix + Clip(_overlayItems[itemIndex], Math.Max(1, maxTextWidth - 2));
            sb.Append(Ansi.At(firstRow + row + 2, 1));
            sb.Append(Ansi.ClearLine);
            if (disabled)
            {
                sb.Append(Ansi.Dim + Ansi.FgGris + text + Ansi.Reset);
            }
            else if (selected)
            {
                sb.Append(Ansi.Esc + "48;5;250m" + Ansi.Esc + "38;5;16m" + text + Ansi.Reset);
            }
            else
            {
                sb.Append(Ansi.FgGris + text + Ansi.Reset);
            }
        }
    }

    private string ActivityPrefix(ActivityKind kind)
    {
        var color = kind switch
        {
            ActivityKind.User => Ansi.FgBlanco,
            ActivityKind.Condor => Ansi.FgCrema,
            ActivityKind.Analysis => Ansi.FgCian,
            ActivityKind.CodeEdit => Ansi.FgDorado,
            ActivityKind.Response => Ansi.FgCrema,
            ActivityKind.Question => Ansi.FgAmarillo,
            ActivityKind.Success => Ansi.FgVerde,
            ActivityKind.Warning => Ansi.FgAmarillo,
            ActivityKind.Error => Ansi.FgRojo,
            _ => Ansi.FgVerde
        };

        return "  " + color;
    }

    /// <summary>
    /// Una sola fila de estado combina la accion de Condor, la actividad real
    /// del modelo y los atajos globales; siempre se repinta en el mismo sitio.
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
        var frame = _busy || _modelWorking ? SpinnerFrames[_spin % SpinnerFrames.Length] + " " : "";
        var availableWidth = Math.Max(1, _width - Shortcuts.Length - 2);
        var showStatus = _busy || _estadoKind is ActivityKind.Warning or ActivityKind.Error;
        var parts = new List<string>();
        if (showStatus && !string.IsNullOrWhiteSpace(_estado))
        {
            parts.Add(_estado);
        }

        if (!_busy && EsDuracionEnSegundos(_progreso))
        {
            parts.Add(_progreso);
        }

        parts.Add("RAM " + (Environment.WorkingSet / (1024 * 1024)) + "MB");
        var workspace = string.IsNullOrWhiteSpace(_workspace) ? Environment.CurrentDirectory : _workspace;
        parts.Add(workspace);
        var footer = frame + string.Join(" · ", parts);
        var color = showStatus ? EstadoColor(_estadoKind) : Ansi.FgGris;
        sb.Append(color + Clip(footer, availableWidth) + Ansi.Reset);
        var shortcutsCol = Math.Max(1, _width - Shortcuts.Length + 1);
        sb.Append(Ansi.At(EstadoRow + 1, shortcutsCol));
        sb.Append(Ansi.FgGris + Shortcuts + Ansi.Reset);
    }

    private static bool EsDuracionEnSegundos(string value)
    {
        var text = value.Trim();
        if (!text.EndsWith(" s", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var number = text[..^2].Trim();
        return number.Length > 0 && number.All(character => char.IsDigit(character) || character is ',' or '.');
    }

    internal bool TryGetFooterActionAt(int screenRow, int screenColumn, out InputAction action)
    {
        lock (_gate)
        {
            action = InputAction.None;
            if (_mode != HostMode.Session || screenRow != EstadoRow + 1 || screenColumn < 1)
            {
                return false;
            }

            var start = Math.Max(1, _width - Shortcuts.Length + 1);
            var offset = screenColumn - start;
            if (offset >= 0 && offset < AgentsHint.Length)
            {
                action = InputAction.OpenAgents;
                return true;
            }

            var commandsStart = AgentsHint.Length + HintGap.Length;
            if (offset >= commandsStart && offset < commandsStart + CommandsHint.Length)
            {
                action = InputAction.OpenCommands;
                return true;
            }

            return false;
        }
    }

    private void AppendInputCursorStateLocked(System.Text.StringBuilder sb)
    {
        if (_mode == HostMode.Session && !_busy && _showEmptyInputCursor)
        {
            sb.Append(Ansi.At(InputRow + 1, 6));
            sb.Append(Ansi.ShowCursor);
        }
        else
        {
            sb.Append(Ansi.HideCursor);
        }
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
        var frame = _busy || _modelWorking ? SpinnerFrames[_spin % SpinnerFrames.Length] + " " : "";
        var condorText = "Condor · " + _estado;
        if (!string.IsNullOrWhiteSpace(_progreso) && _progreso != "—")
        {
            condorText += " · " + _progreso;
        }

        var modelText = string.IsNullOrWhiteSpace(_modelStatus) ? "Modelo local · esperando" : _modelStatus;
        var width = Math.Max(1, _width - 6);
        var contentWidth = Math.Max(2, width - frame.Length - 3);
        var condorBudget = Math.Min(condorText.Length, Math.Max(1, (contentWidth * 55) / 100));
        var modelBudget = Math.Min(modelText.Length, Math.Max(1, contentWidth - condorBudget));
        var remainingWidth = contentWidth - condorBudget - modelBudget;
        var extraCondor = Math.Min(remainingWidth, Math.Max(0, condorText.Length - condorBudget));
        condorBudget += extraCondor;
        remainingWidth -= extraCondor;
        modelBudget += Math.Min(remainingWidth, Math.Max(0, modelText.Length - modelBudget));
        var combined = frame + Clip(condorText, condorBudget) + " · " + Clip(modelText, modelBudget);
        sb.Append(EstadoColor(_estadoKind) + Clip(combined, width) + Ansi.Reset);

        // Borra la antigua segunda fila de estado al pasar al diseño de una linea.
        sb.Append(Ansi.At(19, 3));
        sb.Append(Ansi.ClearLine);
    }

    /// <summary>Pinta la region de entrada. El contenido lo dibuja el editor.</summary>
    private void PaintInputRegionLocked(System.Text.StringBuilder sb)
    {
        if (_busy)
        {
            sb.Append(Ansi.At(InputRow + 1, 1));
            sb.Append(Ansi.ClearLine);
            sb.Append(" ").Append(_tuiTheme.Accent).Append("┃").Append(Ansi.Reset).Append(" ")
                .Append(Ansi.FgAmarillo).Append("Condor esta trabajando...  Esc + Esc interrumpe la tarea")
                .Append(Ansi.Reset);
        }
        else
        {
            sb.Append(BuildInputLocked("", 0, _inputPlaceholder));
        }
    }

    /// <summary>Dibuja la linea de entrada con cursor en bloque (caret inverso).</summary>
    public void RenderInput(
        string buffer,
        int caret,
        string? overlayTitle = null,
        IReadOnlyList<string>? overlayItems = null,
        int overlaySelectedIndex = -1,
        IReadOnlyList<bool>? overlayDisabledItems = null,
        string? placeholder = null,
        bool showCursorWhenEmpty = false)
    {
        string? snapshot = null;
        lock (_gate)
        {
            if (_selectListOverlay is not null)
            {
                _selectListModel = null;
                _selectListOverlay = null;
                _dirtyOverlay = true;
                _dirtyActivity = true;
            }

            var nextItems = overlayItems?.ToList() ?? new List<string>();
            var nextDisabledItems = overlayDisabledItems?.ToList() ?? new List<bool>();
            var overlayChanged = !string.Equals(_overlayTitle, overlayTitle, StringComparison.Ordinal) ||
                                 _overlaySelectedIndex != overlaySelectedIndex ||
                                 !_overlayItems.SequenceEqual(nextItems, StringComparer.Ordinal) ||
                                 !_overlayDisabledItems.SequenceEqual(nextDisabledItems);
            _overlayTitle = overlayTitle;
            _overlayItems = nextItems;
            _overlayDisabledItems = nextDisabledItems;
            _overlaySelectedIndex = overlaySelectedIndex;
            _inputPlaceholder = placeholder ?? Placeholder;
            _showEmptyInputCursor = showCursorWhenEmpty && buffer.Length == 0 &&
                                    string.IsNullOrEmpty(overlayTitle) && nextItems.Count == 0;
            if (overlayChanged && _mode == HostMode.Session)
            {
                _dirtyActivity = true;
            }

            if (!_entered || _mode != HostMode.Session || _busy)
            {
                return;
            }

            var output = new System.Text.StringBuilder(BuildInputLocked(buffer, caret, _inputPlaceholder));
            AppendInputCursorStateLocked(output);
            snapshot = output.ToString();
        }

        Console.Write(snapshot);
    }

    public void RenderInputWithSelectList(
        string buffer,
        int caret,
        SelectListOverlay? overlay,
        string? placeholder = null)
    {
        string? snapshot = null;
        lock (_gate)
        {
            var legacyOverlayActive = !string.IsNullOrWhiteSpace(_overlayTitle) && _overlayItems.Count > 0;
            _overlayTitle = null;
            _overlayItems.Clear();
            _overlayDisabledItems.Clear();
            _overlaySelectedIndex = -1;
            if (legacyOverlayActive)
            {
                _dirtyActivity = true;
            }

            var renderedOverlay = overlay is null ? null : RenderSelectListOverlayLocked(overlay);
            var changed = !SelectListOverlayEquals(_selectListOverlay, renderedOverlay);
            var closing = _selectListOverlay is not null && renderedOverlay is null;
            _selectListModel = overlay;
            _selectListOverlay = renderedOverlay;
            _inputPlaceholder = placeholder ?? Placeholder;
            _showEmptyInputCursor = false;
            if (changed)
            {
                _dirtyOverlay = true;
                if (closing)
                {
                    _dirtyActivity = true;
                }
            }

            if (!_entered || _mode != HostMode.Session || _busy)
            {
                return;
            }

            var output = new System.Text.StringBuilder(BuildInputLocked(buffer, caret, _inputPlaceholder));
            AppendInputCursorStateLocked(output);
            snapshot = output.ToString();
        }

        Console.Write(snapshot);
    }

    internal bool TryGetSelectListItemIndexAt(int screenRow, int screenColumn, out int filteredIndex)
    {
        lock (_gate)
        {
            filteredIndex = -1;
            if (_selectListModel is null || _selectListOverlay is null || screenRow < 1 || screenColumn < 1)
            {
                return false;
            }

            var state = _selectListModel.State;
            var visible = Math.Min(state.VisibleRows, state.Filtered.Count);
            if (visible == 0)
            {
                return false;
            }

            var contentHeight = Math.Max(0, ContentBottom - ContentTop + 1);
            var firstRow = _selectListOverlay.Placement == SelectListPlacement.Centered
                ? ContentTop + Math.Max(0, (contentHeight - _selectListOverlay.Lines.Count) / 2)
                : Math.Max(ContentTop, ContentBottom - _selectListOverlay.Lines.Count + 1);
            var firstColumn = Math.Clamp(
                _selectListOverlay.Column + 1,
                1,
                Math.Max(1, _width - _selectListOverlay.Width + 1));
            var firstItemRow = firstRow + 1 +
                (string.IsNullOrWhiteSpace(_selectListModel.Options.Title) ? 0 : 1) +
                (_selectListModel.Options.IncludeSearchField ? 1 : 0);
            var rowOffset = screenRow - firstItemRow;
            if (rowOffset < 0 || rowOffset >= visible || screenColumn < firstColumn ||
                screenColumn >= firstColumn + _selectListOverlay.Width)
            {
                return false;
            }

            filteredIndex = state.ScrollOffset + rowOffset;
            return filteredIndex < state.Filtered.Count;
        }
    }

    internal bool TryGetInputCaretIndexAt(
        int screenRow,
        int screenColumn,
        string buffer,
        int currentCaret,
        out int caret)
    {
        lock (_gate)
        {
            caret = 0;
            if (!_entered || _mode != HostMode.Session || screenRow != InputRow + 1 ||
                screenColumn < 1 || screenColumn > _width)
            {
                return false;
            }

            const int prefixWidth = 5;
            const int textStartColumn = prefixWidth + 1;
            var budget = Math.Max(1, _width - prefixWidth - 1);
            if (buffer.Length == 0 || screenColumn < textStartColumn)
            {
                return true;
            }

            var runes = new List<(int Start, int Length, int Width)>();
            var utf16Offset = 0;
            foreach (var rune in buffer.EnumerateRunes())
            {
                var runeText = rune.ToString();
                runes.Add((utf16Offset, rune.Utf16SequenceLength, Math.Max(0, Ansi.VisibleWidth(runeText))));
                utf16Offset += rune.Utf16SequenceLength;
            }

            currentCaret = Math.Clamp(currentCaret, 0, buffer.Length);
            var caretRune = runes.FindIndex(rune => rune.Start >= currentCaret);
            if (caretRune < 0) caretRune = runes.Count;
            var startRune = caretRune;
            var used = 0;
            if (caretRune == runes.Count)
            {
                var textBudget = Math.Max(0, budget - 1);
                while (startRune > 0 && used + runes[startRune - 1].Width <= textBudget)
                {
                    startRune--;
                    used += runes[startRune].Width;
                }
            }
            else
            {
                while (startRune > 0 && used < budget / 2)
                {
                    startRune--;
                    used += runes[startRune].Width;
                }
            }

            var targetWidth = Math.Clamp(screenColumn - textStartColumn, 0, budget);
            var visibleWidth = 0;
            for (var index = startRune; index < runes.Count; index++)
            {
                var rune = runes[index];
                if (visibleWidth + rune.Width > budget)
                {
                    break;
                }

                if (targetWidth < visibleWidth + rune.Width / 2d)
                {
                    caret = rune.Start;
                    return true;
                }

                visibleWidth += rune.Width;
                if (targetWidth < visibleWidth)
                {
                    caret = rune.Start + rune.Length;
                    return true;
                }
            }

            caret = buffer.Length;
            return true;
        }
    }

    private string BuildInputLocked(string buffer, int caret, string? placeholder)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(Ansi.At(InputRow + 1, 1));
        sb.Append(Ansi.ClearLine);

        var prefixWidth = 5; // margen, barra, separador y prompt
        var budget = Math.Max(1, _width - prefixWidth - 1);
        sb.Append(" ").Append(_tuiTheme.Accent).Append("┃").Append(Ansi.Reset).Append(" ");
        sb.Append(Ansi.Bold).Append(Ansi.FgDorado).Append("> ").Append(Ansi.Reset);

        if (buffer.Length == 0)
        {
            if (!string.IsNullOrEmpty(placeholder))
            {
                sb.Append(Ansi.FgGris).Append(Ansi.ClipVisible(placeholder, budget)).Append(Ansi.Reset);
            }

            return sb.ToString();
        }

        var runes = new List<(string Text, int Start, int Width)>();
        var utf16Offset = 0;
        foreach (var rune in buffer.EnumerateRunes())
        {
            var runeText = rune.ToString();
            runes.Add((runeText, utf16Offset, Math.Max(0, Ansi.VisibleWidth(runeText))));
            utf16Offset += rune.Utf16SequenceLength;
        }

        caret = Math.Clamp(caret, 0, buffer.Length);
        var caretRune = runes.FindIndex(rune => rune.Start >= caret);
        if (caretRune < 0) caretRune = runes.Count;
        var startRune = caretRune;
        var used = 0;
        if (caretRune == runes.Count)
        {
            var textBudget = Math.Max(0, budget - 1);
            while (startRune > 0 && used + runes[startRune - 1].Width <= textBudget)
            {
                startRune--;
                used += runes[startRune].Width;
            }
        }
        else
        {
            while (startRune > 0 && used < budget / 2)
            {
                startRune--;
                used += runes[startRune].Width;
            }
        }

        var visibleWidth = 0;
        for (var index = startRune; index < runes.Count; index++)
        {
            var rune = runes[index];
            if (visibleWidth + rune.Width > budget)
            {
                break;
            }

            if (index == caretRune)
            {
                sb.Append(Ansi.Esc).Append("7m").Append(rune.Text).Append(Ansi.Reset);
            }
            else
            {
                sb.Append(Ansi.FgBlanco).Append(rune.Text);
            }

            visibleWidth += rune.Width;
            if (visibleWidth >= budget)
            {
                break;
            }
        }

        if (caretRune == runes.Count && visibleWidth < budget)
        {
            sb.Append(Ansi.Esc).Append("7m ").Append(Ansi.Reset);
        }

        sb.Append(Ansi.Reset);

        return sb.ToString();
    }

    // ------------------------------------------------------------------ utilidades

    private void RewrapLocked()
    {
        var width = Math.Max(30, FeedWidth - 4);
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
        text = text.Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ');
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
                _mouseTrackingMode.Dispose();
                Console.Write(Ansi.ShowCursor + Ansi.LeaveAltScreen);
                _entered = false;
            }
        }

        GC.SuppressFinalize(this);
    }

    private sealed record RenderedSelectListOverlay(
        IReadOnlyList<string> Lines,
        SelectListPlacement Placement,
        int Width,
        int Column);

    private enum HostMode
    {
        Welcome,
        Session
    }
}
