using System.Runtime.InteropServices;

namespace Condor.Cli.Tui;

/// <summary>
/// Utilidades ANSI/VT de la TUI de Condor. Unica capa que emite secuencias de
/// escape: pantalla alternativa, posicionamiento absoluto y color. Degrada sin
/// color si el entorno lo pide (NO_COLOR) y desactiva la TUI si la terminal no
/// soporta VT o la salida esta redirigida.
/// </summary>
public static class Ansi
{
    public const string Esc = "\u001b[";

    public const string Reset = Esc + "0m";
    public const string Bold = Esc + "1m";
    public const string Dim = Esc + "2m";

    // Paleta institucional de Condor (coherente con la mascota oficial):
    // terracota #C2665A -> 167, dorado #D9A25A -> 179, crema #E8E4D8 -> 255,
    // cuerpo oscuro #2A2924 -> 235.
    public const string FgTerracota = Esc + "38;5;167m";
    public const string FgDorado = Esc + "38;5;179m";
    public const string FgCrema = Esc + "38;5;255m";
    public const string FgBlanco = Esc + "97m";
    public const string FgGris = Esc + "38;5;243m";
    public const string FgVerde = Esc + "38;5;114m";
    public const string FgRojo = Esc + "38;5;174m";
    public const string FgAmarillo = Esc + "38;5;180m";
    public const string FgCian = Esc + "38;5;152m";

    public static bool ColorEnabled { get; } =
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));

    /// <summary>Posiciona el cursor (1-based).</summary>
    public static string At(int row, int col) => Esc + row.ToString() + ";" + col.ToString() + "H";

    public const string ClearScreen = Esc + "2J";
    public const string ClearLine = Esc + "2K";
    public const string HideCursor = Esc + "?25l";
    public const string ShowCursor = Esc + "?25h";
    public const string EnterAltScreen = Esc + "?1049h";
    public const string LeaveAltScreen = Esc + "?1049l";

    private const int StdOutputHandle = -11;
    private const uint EnableVirtualTerminalProcessing = 0x0004;

    /// <summary>
    /// La TUI requiere terminal interactiva con VT. Si hay salida o entrada
    /// redirigida (E2E/pipelines), o la terminal no habilita VT, devuelve false:
    /// Condor usa entonces la experiencia CLI clasica sin ninguna perdida.
    /// </summary>
    public static bool TryEnableVirtualTerminal()
    {
        if (Console.IsOutputRedirected || Console.IsInputRedirected)
        {
            return false;
        }

        if (!OperatingSystem.IsWindows())
        {
            // En terminales POSIX el VT es nativo.
            return true;
        }

        try
        {
            var handle = GetStdHandle(StdOutputHandle);
            if (!GetConsoleMode(handle, out var mode))
            {
                return false;
            }

            return SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Quita secuencias SGR para medir ancho visible o degradar sin color.</summary>
    public static string StripSgr(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('\u001b') < 0)
        {
            return text;
        }

        var sb = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\u001b' && i + 1 < text.Length && text[i + 1] == '[')
            {
                while (i < text.Length && !char.IsLetter(text[i]))
                {
                    i++;
                }

                continue;
            }

            sb.Append(text[i]);
        }

        return sb.ToString();
    }

    /// <summary>Ancho visible (sin SGR) de una linea ya coloreada.</summary>
    public static int VisibleWidth(string text)
    {
        return StripSgr(text).Length;
    }

    /// <summary>Texto tal cual se debe emitir: sin SGR si NO_COLOR esta activo.</summary>
    public static string Paint(string text)
    {
        return ColorEnabled ? text : StripSgr(text);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
}
