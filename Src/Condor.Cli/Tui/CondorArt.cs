using System;

namespace Condor.Cli.Tui;

/// <summary>
/// RECURSO DE IDENTIDAD VISUAL de Condor.
///
/// Pipeline visual (α.03):
///   ANSI ORIGINAL (AveV16Raw, geometria 232/242/167/97)
///      |
///      v
///   GAMA DE COLORES  (el cuerpo casi-negro 232 -> escala oscura visible 235/236/233)
///      |
///      v
///   GRANDE 100%  (aplica esa gama; misma geometria ANSI original)  [BIENVENIDA]
///      |
///      v
///   PEQUENA  (adaptacion grafica compacta de la MISMA ave: cabeza/pico/cuerpo/ala/cola,
///             con patas y garras visibles)  [SESION]
///
/// La mascota GRANDE conserva intacta la fuente ANSI original (bienvenida). La mascota
/// PEQUENA es una adaptacion para tamano reducido: misma identidad cromatica y misma
/// anatomia (cabeza terracota 167, punta de pico blanca 97, cuerpo gris 236, sombreado
/// 242 y separaciones 233), pero en silueta compacta horizontal con patas y garras
/// visibles. No es un downscale ciego (perdia patas/garras/pico): es una matriz propia
/// reducida que conserva todos los rasgos de la mascota grande.
///
/// Capa de color: 167 cabeza · 97 punta de pico · 236 cuerpo · 242 sombreado/ala/cola
/// clara · 235 volumen · 233 separaciones muy oscuras y patas/garras.
/// </summary>
public static class CondorArt
{
    /// <summary>Ancho/alto de la mascota GRANDE (13 filas x 40 columnas visibles del arte ANSI original).</summary>
    public const int GrandeWidth = 40;
    public const int GrandeHeight = 13;

    /// <summary>Gama de colores del cuerpo (sustituye al casi-negro 232): escala oscura visible aprobada.</summary>
    private static readonly string[] GamaCuerpo = { "38;5;235", "38;5;236", "38;5;233" };

    /// <summary>
    /// Matriz de la mascota PEQUENA. Silueta compacta horizontal de la misma ave:
    /// cabeza a la izquierda (167), punta de pico blanca (97), cuerpo gris oscuro (236),
    /// ala/cola con gris medio y claro (242/235) y separaciones muy oscuras (233).
    /// La mitad inferior dedicada a PATAS y GARRAS (233/242) visibles.
    ///
    /// Leyenda:  H=cabeza (167) · P=pico (97) · B=cuerpo (236) · V=volumen (235)
    ///           W=ala/cola clara (242) · T=cola (236) · t=punto de cola (242)
    ///           S=separacion muy oscura (233) · G=pata/garra (233) · F=garra (242)
    /// </summary>
    internal static readonly string[] PequenaMatrix =
    [
        "....H......TTT..",
        "P...HHH....tTTT..",
        "P..HHHHH..W.TTtt.",
        "..HHHHH.SWWtBTtt.",
        "...BBBBB.SWWBBT..",
        "...BBBBBBWWBBBB..",
        "....BBBBVBBBBB...",
        ".....G......G....",
        "....GGG....GGG...",
        "....FFF....FFF...",
    ];

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

    /// <summary>
    /// Mascota PEQUENA (sesion): adaptacion grafica compacta de la MISMA ave, optimizada
    /// para tamano reducido. Conserva cabeza (167), punta de pico blanca (97), cuerpo (236),
    /// sombreado (242/235), separaciones (233), y a diferencia de un downscale ciego incluye
    /// PATAS y GARRAS visibles. Deriva de la misma identidad, nunca de un segundo diseno.
    /// </summary>
    public static readonly string[] Ave = RenderSmall();

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

    /// <summary>
    /// Renderiza la mascota pequena desde PequenaMatrix a filas ANSI. Cada celda no vacia
    /// se dibuja como bloque solido (█) con su color; el silencio es espacio con reset.
    /// Conserva la geometria exacta de la matriz: no hay transformacion ni redimensionado.
    /// </summary>
    internal static string[] RenderSmall()
    {
        var rows = new string[PequenaMatrix.Length];
        for (var r = 0; r < PequenaMatrix.Length; r++)
        {
            var source = PequenaMatrix[r];
            var sb = new System.Text.StringBuilder();
            string? cur = null;
            for (var c = 0; c < source.Length; c++)
            {
                var glyph = source[c];
                if (glyph == '.')
                {
                    if (cur != null) { sb.Append("\u001b[0m"); cur = null; }
                    sb.Append(' ');
                    continue;
                }

                var color = PixelColor(glyph);
                if (color != cur)
                {
                    if (color.Length > 0) sb.Append("\u001b[").Append(color).Append('m');
                    cur = color;
                }
                sb.Append('█');
            }
            if (cur != null) sb.Append("\u001b[0m");
            rows[r] = sb.ToString();
        }
        return rows;
    }

    private static string PixelColor(char glyph) => glyph switch
    {
        'H' => "38;5;167",
        'P' => "97",
        'B' => "38;5;236",
        'V' => "38;5;235",
        'W' => "38;5;242",
        'T' => "38;5;236",
        't' => "38;5;242",
        'S' => "38;5;233",
        'G' => "38;5;233",
        'F' => "38;5;242",
        _ => ""
    };
}