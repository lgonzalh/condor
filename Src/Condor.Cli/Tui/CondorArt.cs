namespace Condor.Cli.Tui;

/// <summary>
/// RECURSO DE IDENTIDAD VISUAL de Condor (Docs/07_Interfaz/MASCOTA_CLI_UNICODE.md).
/// La mascota es arte Unicode de terminal; la matriz es el recurso de identidad y
/// la capa de presentacion solamente la pinta.
///
/// Dos presencias oficiales:
///   * Condor Grande: bienvenida e inicio (mockup "01. INICIO").
///   * Condor Ave V16: mascota de trabajo durante la sesion.
///
/// Condor Grande se deriva 1:1 de la mascota oficial Assets/condor_mascota.svg
/// (cabeza terracota #C2665A con ojos oscuros, pico dorado #D9A25A, collar blanco
/// crema #E8E4D8 y cuerpo #2A2924), proyectada sobre la rejilla oficial de 15x12
/// celdas con bloques Unicode; el pico usa medios bloques sobre las celdas que
/// cruza, igual que en el SVG. Condor Ave conserva las secuencias SGR exactas del
/// prototipo aprobado V16 (Docs/07_Interfaz/Mockups/condor_unicode_v16.ps1):
/// alas/cuerpo 232-242, cabeza/pico 167 y brillo ocular 97.
///
/// No se escala ni se transforma geometricamente (regla de proporcion del
/// documento de la mascota): la geometria vive unicamente en estas matrices.
///
/// AJUSTE DE CONTRASTE (T-018, aprobado como correccion visual): las zonas que
/// se representaban en negro puro o casi negro (cuerpo 232 del Ave V16 y cuerpo
/// del Grande) usan ahora una escala de grises oscuros aprobada para seguirse
/// viendo sobre el fondo oscuro de la TUI:
///   #111315 -> 38;5;233   #272727 -> 38;5;235   #2A2D30 -> 38;5;236
///   #454546 -> 38;5;238   (#4D4D4D -> 38;5;239, #808080 -> 38;5;244)
/// La cabeza terracota, el pico dorado y el collar blanco permanecen intactos;
/// la silueta y la proporcion no cambian.
/// </summary>
public static class CondorArt
{
    public const int GrandeWidth = 15;
    public const int GrandeHeight = 12;

    /// <summary>Tonos de la escala oscura aprobada para el cuerpo (con volumen).</summary>
    internal static readonly string[] TonosCuerpo =
    {
        "38;5;238", // #454546 - luz central
        "38;5;235", // #272727 - medio
        "38;5;233"  // #111315 - borde profundo
    };

    /// <summary>Matriz logica de Condor Grande (rejilla oficial del SVG).</summary>
    /// <remarks>
    /// R = cabeza terracota, D = cuerpo oscuro, W = collar blanco,
    /// b = mitad inferior del pico (dorado sobre rojo), t = mitad superior del
    /// pico (dorado sobre blanco), . = celda vacia.
    /// </remarks>
    private static readonly string[] GrandeMatrix =
    {
        "......RRR......",
        ".....RRRRR.....",
        ".....RDbDR.....",
        "....WWWtWWW....",
        "DDDDDDDDDDDDDDD",
        ".DDDDDDDDDDDDD.",
        "..DDDDDDDDDDD..",
        "...DDDDDDDDD...",
        ".....DDDDD.....",
        "....DDDDDDD....",
        ".....DDDDD.....",
        "......DDD......",
    };

    /// <summary>Filas SGR originales del prototipo V16 (unico origen del arte).</summary>
    private static readonly string[] AveV16Raw =
    [
        "\u001b[0m                                      \u001b[0m",
        "\u001b[0m  \u001b[38;5;232m▄▄\u001b[0m                        \u001b[38;5;232m██\u001b[0m \u001b[38;5;232m██\u001b[0m     \u001b[0m",
        "\u001b[38;5;232m████\u001b[0m                      \u001b[38;5;232m████████\u001b[0m    \u001b[0m",
        "\u001b[38;5;232m▀▀\u001b[38;5;242m██\u001b[38;5;232m█▄▄▄\u001b[0m                \u001b[38;5;232m▄▄████████▄▄\u001b[0m  \u001b[0m",
        "\u001b[0m  \u001b[38;5;242m▀\u001b[38;5;232m█████▄▄▄▄\u001b[0m           \u001b[38;5;232m▄██████\u001b[38;5;242m██\u001b[38;5;232m██▀▀\u001b[0m  \u001b[0m",
        "\u001b[0m    \u001b[38;5;242m▀\u001b[38;5;232m▀\u001b[38;5;242m█\u001b[38;5;232m█████\u001b[0m          \u001b[38;5;232m██████\u001b[38;5;242m████\u001b[38;5;232m██\u001b[0m    \u001b[0m",
        "\u001b[0m      \u001b[38;5;232m▀▀███████\u001b[0m \u001b[38;5;232m████████\u001b[38;5;242m██\u001b[38;5;232m█\u001b[38;5;242m███\u001b[38;5;232m█\u001b[38;5;242m██\u001b[0m     \u001b[0m",
        "\u001b[0m      \u001b[38;5;167m▄▄████\u001b[38;5;232m▀▀██████████\u001b[38;5;242m██\u001b[38;5;232m█\u001b[38;5;242m██\u001b[38;5;232m██\u001b[0m       \u001b[0m",
        "\u001b[0m     \u001b[38;5;242m███\u001b[38;5;167m████\u001b[97m█\u001b[38;5;232m▄██████████\u001b[38;5;242m██\u001b[38;5;232m▀▀\u001b[38;5;242m▀\u001b[38;5;232m▀\u001b[0m        \u001b[0m",
        "\u001b[0m     \u001b[38;5;242m▀\u001b[0m \u001b[38;5;232m▄▄▄████████████▄▄▄▄▄▄\u001b[0m          \u001b[0m",
        "\u001b[0m    \u001b[38;5;242m▀▀██\u001b[38;5;232m▀▀\u001b[0m  \u001b[38;5;232m██████████████████\u001b[0m        \u001b[0m",
        "\u001b[0m       \u001b[38;5;242m▄▄▄▄▄▀▀▀\u001b[0m \u001b[38;5;232m▀▀██████▀▀▀▀\u001b[0m          \u001b[0m",
        "\u001b[0m       \u001b[38;5;242m▀\u001b[0m                              \u001b[0m",
    ];

    /// <summary>Filas ya coloreadas de Condor Grande, listas para pintar.</summary>
    public static readonly string[] Grande = BuildGrande();

    /// <summary>Mascota de trabajo Condor Ave V16 con el contraste corregido.</summary>
    public static readonly string[] Ave = BuildAve();


    /// <summary>Ancho visible maximo de Condor Ave (columnas de terminal).</summary>
    public static readonly int AveWidth = Ave.Max(Ansi.VisibleWidth);

    /// <summary>
    /// Convierte la matriz logica en filas SGR (identidad -> presentacion).
    /// El cuerpo 'D' recibe volumen con la escala oscura aprobada: luz central,
    /// tono medio y borde profundo; cabeza, collar y pico no cambian.
    /// </summary>
    private static string[] BuildGrande()
    {
        var rows = new string[GrandeMatrix.Length];
        for (var r = 0; r < GrandeMatrix.Length; r++)
        {
            var sb = new System.Text.StringBuilder();
            string? currentSgr = null;
            for (var i = 0; i < GrandeMatrix[r].Length; i++)
            {
                var c = GrandeMatrix[r][i];
                switch (c)
                {
                    case '.':
                        sb.Append(' ');
                        currentSgr = null;
                        break;
                    case 'b':
                        sb.Append(Ansi.Esc + "38;5;179m" + Ansi.Esc + "48;5;167m\u2584" + Ansi.Reset);
                        currentSgr = null;
                        break;
                    case 't':
                        sb.Append(Ansi.Esc + "38;5;179m" + Ansi.Esc + "48;5;255m\u2580" + Ansi.Reset);
                        currentSgr = null;
                        break;
                    default:
                        string sgr;
                        if (c == 'R')
                        {
                            sgr = "38;5;167";
                        }
                        else if (c == 'W')
                        {
                            sgr = "38;5;255";
                        }
                        else
                        {
                            // Volumen del cuerpo por distancia al centro visual.
                            var distancia = Math.Abs(i - 7) + Math.Abs(r - 7.5) * 0.8;
                            sgr = distancia <= 2.9 ? TonosCuerpo[0]
                                : distancia <= 5.9 ? TonosCuerpo[1]
                                : TonosCuerpo[2];
                        }

                        if (currentSgr != sgr)
                        {
                            sb.Append(Ansi.Esc + sgr + "m");
                            currentSgr = sgr;
                        }

                        sb.Append('\u2588');
                        break;
                }
            }

            rows[r] = sb.ToString();
        }

        return rows;
    }

    /// <summary>
    /// Aplica la correccion de contraste a las zonas casi negras del Ave V16:
    /// cada tramo 232 (negro puro) se sustituye ciclicamente por la escala
    /// aprobada (#272727 / #2A2D30 / #111315). Geometria, cabeza terracota,
    /// collar blanco y grises medios del prototipo permanecen intactos.
    /// </summary>
    private static string[] BuildAve()
    {
        const string zonaOscuraOriginal = "\u001b[38;5;232m";
        var tonos = new[] { "\u001b[38;5;235m", "\u001b[38;5;236m", "\u001b[38;5;233m" };
        var rows = new string[AveV16Raw.Length];
        for (var r = 0; r < AveV16Raw.Length; r++)
        {
            var row = AveV16Raw[r];
            var sb = new System.Text.StringBuilder();
            var index = 0;
            var run = 0;
            while (index < row.Length)
            {
                var at = row.IndexOf(zonaOscuraOriginal, index, StringComparison.Ordinal);
                if (at < 0)
                {
                    sb.Append(row[index..]);
                    break;
                }

                sb.Append(row[index..at]);
                sb.Append(tonos[run % tonos.Length]);
                run++;
                index = at + zonaOscuraOriginal.Length;
            }

            rows[r] = sb.ToString();
        }

        return rows;
    }
}