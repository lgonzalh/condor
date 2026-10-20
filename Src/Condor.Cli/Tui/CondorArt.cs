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
///   PEQUENA  (cabeza compacta 8x8 en 4 filas de bloques)  [SESION]
///
/// La mascota PEQUENA es la cabeza compacta de referencia ANSI 24-bit: conserva la
/// identidad grafica (cabeza roja, punta de pico blanca, grises de cuerpo y contorno)
/// y se muestra junto a la cabecera para no competir con el contenido. No es una
/// segunda mascota ni un dibujo independiente: deriva de la misma identidad visual.
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

    /// <summary>Cabeza de la mascota pequeña: matriz exacta de 8x8 en bloques Unicode.</summary>
    public static string[] Ave => BuildHeadArt();

    private static readonly string[] HeadRows =
    {
        "KKKLLLLK",
        "KKLDDDDL",
        "KKKDDKDL",
        "KKLDDDDL",
        "KGGGGDDL",
        "KKLDDDDL",
        "KKLDDDDW",
        "KKLDDDDW"
    };

    private static string[] BuildHeadArt()
    {
        // Cada pareja vertical de píxeles se convierte en una celda "▀":
        // foreground para el píxel superior y background para el inferior.
        // Así no se emiten espacios y la matriz 8x8 queda en cuatro líneas.
        var result = new string[HeadRows.Length / 2];
        for (var y = 0; y < result.Length; y++)
        {
            var row = new System.Text.StringBuilder();
            for (var x = 0; x < HeadRows[y * 2].Length; x++)
            {
                var top = HeadRows[y * 2][x];
                var bottom = HeadRows[y * 2 + 1][x];
                row.Append(Color(top, background: false));
                row.Append(Color(bottom, background: true));
                row.Append('▀');
            }

            row.Append(Ansi.Reset);
            result[y] = row.ToString();
        }

        return result;
    }

    private static string Color(char color, bool background)
    {
        var (r, g, b) = color switch
        {
            'L' => (255, 91, 91),
            'D' => (205, 83, 98),
            'G' => (100, 100, 115),
            'W' => (255, 255, 255),
            _ => (0, 0, 0)
        };

        return $"\x1b[{(background ? 48 : 38)};2;{r};{g};{b}m";
    }

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
