using System;

namespace Condor.Cli.Tui;

/// <summary>
/// RECURSO DE IDENTIDAD VISUAL de Condor.
///
/// Pipeline visual (a.03):
///   ANSI ORIGINAL (AveV16Raw, geometria 232/242/167/97)
///      |
///      v
///   GAMA DE COLORES  (el cuerpo casi-negro 232 -> escala oscura visible 235/236/233)
///      |
///      v
///   GRANDE 100%  (aplica esa gama; misma geometria ANSI original)  [BIENVENIDA]
///      |
///      v
///   PEQUENA  (reduccion ~50% de la GRANDE, no ciega)  [SESION]
///
/// La mascota PEQUENA es una reduccion VISUALMENTE FIEL de la misma ave (�50%): fusiona
/// bloques 2x2 de la GRANDE conservando la identidad grafica (cabeza 167, punta de pico
/// blanca 97, sombreado 242, cuerpo 235/236/233) y NO destruye los rasgos que un downscale
/// ciego perderia. Sobre la silueta reducida se reconstruyen las PATAS y GARRAS (233/242)
/// y se refuerza el pico, de modo que la peque�a es inequivocamente el MISMO CONDOR.
/// No hay una segunda matriz ni un dibujo nuevo: todo deriva de la fuente ANSI unica.
/// </summary>
public static class CondorArt
{
    /// <summary>Ancho/alto de la mascota GRANDE (13 filas x 40 columnas visibles del arte ANSI original).</summary>
    public const int GrandeWidth = 40;
    public const int GrandeHeight = 13;

    /// <summary>Gama de colores del cuerpo (sustituye al casi-negro 232): escala oscura visible aprobada.</summary>
    private static readonly string[] GamaCuerpo = { "38;5;235", "38;5;236", "38;5;233" };

    /// <summary>
    /// Arte ANSI original (prototipo condor_unicode_v16.ps1), VERBATIM. Conserva la
    /// geometria: caracteres, filas, espacios, bloques y secuencias ANSI 256
    /// (232 cuerpo, 242 sombreado, 167 cabeza, 97 blanco) y sus resets. Es la fuente
    /// unica. La gama de colores se aplica al renderizar (ver ApplyGama).
    /// </summary>
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

    /// <summary>Mascota GRANDE (bienvenida): 100% del ANSI original con la gama de colores restituida.</summary>
    public static readonly string[] Grande = ApplyGama(AveV16Raw);

    /// <summary>Mascota compacta oficial: solo bloques Unicode y color ANSI truecolor.</summary>
    public static readonly string[] Ave =
    {
        "\x1b[38;2;12;12;12m······██········██····\x1b[0m",
        "\x1b[38;2;12;12;12m·\x1b[38;2;28;28;28m▀·····▀······▀·····▀\x1b[0m",
        "\x1b[38;2;28;28;28m▀··············▀··············▀\x1b[0m",
        "\x1b[38;2;28;28;28m·▀··············▀·\x1b[38;2;108;108;108m▀▀\x1b[38;2;28;28;28m·\x1b[0m",
        "\x1b[38;2;28;28;28m··▀············▀\x1b[38;2;108;108;108m▌▌▀\x1b[38;2;28;28;28m·\x1b[0m",
        "\x1b[38;2;205;83;98m▀▀▀\x1b[38;2;255;255;255m█\x1b[38;2;48;48;48m·······\x1b[38;2;108;108;108m▌▌▀\x1b[38;2;28;28;28m·\x1b[0m",
        "\x1b[38;2;128;128;128m▌\x1b[38;2;205;83;98m▀▀▀\x1b[38;2;255;255;255m█\x1b[38;2;48;48;48m·······\x1b[38;2;108;108;108m▄\x1b[0m",
        "\x1b[38;2;48;48;48m██████████\x1b[38;2;108;108;108m▌\x1b[0m",
        "\x1b[38;2;48;48;48m█████████\x1b[38;2;108;108;108m▌\x1b[0m",
        "\x1b[38;2;28;28;28m····\x1b[38;2;108;108;108m▄▄\x1b[0m",
        "\x1b[38;2;28;28;28m················\x1b[0m",
    };

    /// <summary>Ancho visible maximo de la mascota pequena (Ave), sin SGR.</summary>
    public static readonly int AveWidth = Ave.Max(Ansi.VisibleWidth);

    /// <summary>
    /// Aplica la gama de colores a la fuente ANSI: el casi-negro del cuerpo (38;5;232)
    /// se sustituye ciclicamente por la escala oscura visible (235/236/233), dando
    /// volumen. No cambia la geometria ni los demas colores (242/167/97).
    /// </summary>
    private static string[] ApplyGama(string[] raw)
    {
        var rows = new string[raw.Length];
        for (var r = 0; r < raw.Length; r++)
            rows[r] = RestoreBodyShade(raw[r]);
        return rows;
    }

    /// <summary>Sustituye cada secuencia 38;5;232 por 235/236/233 ciclicamente dentro de una fila.</summary>
    private static string RestoreBodyShade(string line)
    {
        var target = "\u001b[38;5;232m";
        var sb = new System.Text.StringBuilder();
        var cycle = 0;
        var i = 0;
        while (i < line.Length)
        {
            if (string.CompareOrdinal(line, i, target, 0, target.Length) == 0)
            {
                sb.Append('\u001b').Append('[').Append(GamaCuerpo[cycle % GamaCuerpo.Length]).Append('m');
                cycle++;
                i += target.Length;
            }
            else
            {
                sb.Append(line[i]);
                i++;
            }
        }
        return sb.ToString();
    }
}
