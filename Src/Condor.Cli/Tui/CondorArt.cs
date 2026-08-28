using System;

namespace Condor.Cli.Tui;

/// <summary>
/// RECURSO DE IDENTIDAD VISUAL de Condor, reconstruido en T-018 desde la UNICA
/// fuente: el ANSI original (Docs/07_Interfaz/Mockups/condor_unicode_v16.ps1).
///
/// Pipeline visual unico:
///   ANSI ORIGINAL (AveV16Raw, geometria 232/242/167/97)
///      |
///      v
///   GAMA DE COLORES  (el cuerpo casi-negro 232 -> escala oscura visible 235/236/233)
///      |
///      v
///   GRANDE 100%  (aplica esa gama; misma geometria ANSI original)
///      |
///      v
///   PEQUENA ~50%  (Scale50: fusion determinista 2x2 -> 1 celda densa, conservando detalles y colores)
///
/// Gama de colores restituida en AMBAS mascotas: las zonas que en el ANSI original
/// eran casi-negro (38;5;232) se sustituyen ciclicamente por la escala oscura
/// aprobada y visible sobre fondo oscuro (38;5;235 / 38;5;236 / 38;5;233), que da
/// volumen al cuerpo. Se conservan 242 (sombreado), 167 (cabeza/acento) y 97 (blanco).
/// No hay una segunda representacion ni un segundo diseno (no SVG, no matriz R/W/b/t/D,
/// no SmallCondorMatrix). La pequena deriva visualmente de la misma AVE GRANDE.
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

    /// <summary>Mascota PEQUENA (sesion): el Grande (con gama) reducido a ~50% mediante fusion densa determinista.</summary>
    public static readonly string[] Ave = Scale50(Grande);

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
    /// Reduce la mascota grande a ~50% mediante una transformacion DETERMINISTA que no es un
    /// downscale ciego: fusiona cada bloque 2x2 de celdas en una unica celda usando caracteres de
    /// densidad (▀ ▄ █ ▌ ▐ ▖ ▗ ▘ ▝), preservando detalle y color caracteristico (prioridad:
    /// cabeza 167 > blanco 97 > sombreado 242 > gama del cuerpo). Misma identidad grafica que la
    /// AVE GRANDE; la pequena se deriva de la misma fuente, sin una segunda matriz ni un dibujo.
    /// </summary>
    internal static string[] Scale50(string[] grande)
    {
        var parsed = new Cell[grande.Length][];
        for (var r = 0; r < grande.Length; r++)
            parsed[r] = TrimLeading(ParseRow(grande[r]));

        var outRows = new System.Collections.Generic.List<string>();
        for (var r = 0; r < parsed.Length; r += 2)
        {
            var top = parsed[r];
            var bot = (r + 1 < parsed.Length) ? parsed[r + 1] : Array.Empty<Cell>();
            var cols = Math.Max(top.Length, bot.Length);
            var sb = new System.Text.StringBuilder();
            string? cur = null;
            for (var c = 0; c < cols; c += 2)
            {
                var (glyph, color) = MergeBucket(CellAt(top, c), CellAt(top, c + 1), CellAt(bot, c), CellAt(bot, c + 1));
                if (glyph == ' ')
                {
                    if (cur != null) { sb.Append("\u001b[0m"); cur = null; }
                    sb.Append(' ');
                    continue;
                }
                if (cur != color)
                {
                    if (color.Length > 0) sb.Append("\u001b[").Append(color).Append('m');
                    cur = color;
                }
                sb.Append(glyph);
            }
            if (cur != null) sb.Append("\u001b[0m");
            outRows.Add(sb.ToString());
        }
        return outRows.ToArray();
    }

    private readonly struct Cell
    {
        public readonly char Glyph;
        public readonly string Color;
        public Cell(char glyph, string color) { Glyph = glyph; Color = color; }
    }

    private static bool IsFilled(Cell c) => c.Glyph != ' ';

    private static Cell CellAt(Cell[] cells, int idx) => idx < cells.Length ? cells[idx] : new Cell(' ', "");

    private static Cell[] ParseRow(string line)
    {
        var cells = new System.Collections.Generic.List<Cell>();
        var color = "";
        var i = 0;
        while (i < line.Length)
        {
            if (line[i] == '\u001b' && i + 1 < line.Length && line[i + 1] == '[')
            {
                i += 2;
                var sb = new System.Text.StringBuilder();
                while (i < line.Length && line[i] != 'm') { sb.Append(line[i]); i++; }
                if (i < line.Length) i++;
                color = sb.ToString() == "0" ? "" : sb.ToString();
            }
            else
            {
                cells.Add(new Cell(line[i], color));
                i++;
            }
        }
        return cells.ToArray();
    }

    // Recorta el relleno inicial para alinear el ave en cada fila (el arte original tiene
    // sangrado asimetrico); asi la fusion 2x2 no desalinea el ave entre filas.
    private static Cell[] TrimLeading(Cell[] cells)
    {
        var start = 0;
        while (start < cells.Length && cells[start].Glyph == ' ') start++;
        if (start == 0) return cells;
        var res = new Cell[cells.Length - start];
        Array.Copy(cells, start, res, 0, res.Length);
        return res;
    }

    // Fusiona 4 celdas (TL,TR,BL,BR) en una unica celda densa con el color mas caracteristico.
    private static (char, string) MergeBucket(Cell tl, Cell tr, Cell bl, Cell br)
    {
        var fTL = IsFilled(tl); var fTR = IsFilled(tr); var fBL = IsFilled(bl); var fBR = IsFilled(br);
        var n = (fTL ? 1 : 0) + (fTR ? 1 : 0) + (fBL ? 1 : 0) + (fBR ? 1 : 0);
        if (n == 0) return (' ', "");
        if (n == 4) return ('█', Best(tl, tr, bl, br).Color);
        if (fTL && fTR && !fBL && !fBR) return ('▀', Best(tl, tr).Color);
        if (!fTL && !fTR && fBL && fBR) return ('▄', Best(bl, br).Color);
        if (fTL && fBL && !fTR && !fBR) return ('▌', Best(tl, bl).Color);
        if (fTR && fBR && !fTL && !fBL) return ('▐', Best(tr, br).Color);
        if (fTL && !fTR && !fBL && !fBR) return ('▘', tl.Color);
        if (fTR && !fTL && !fBL && !fBR) return ('▝', tr.Color);
        if (fBL && !fTL && !fTR && !fBR) return ('▖', bl.Color);
        if (fBR && !fTL && !fTR && !fBL) return ('▗', br.Color);
        return ('█', Best(tl, tr, bl, br).Color);
    }

    private static Cell Best(Cell a, Cell b)
    {
        var pa = IsFilled(a) ? Prio(a.Color) : -1;
        var pb = IsFilled(b) ? Prio(b.Color) : -1;
        return pa >= pb ? a : b;
    }

    private static Cell Best(Cell a, Cell b, Cell c, Cell d)
    {
        var best = a; var bp = IsFilled(a) ? Prio(a.Color) : -1;
        foreach (var x in new[] { b, c, d })
        {
            var p = IsFilled(x) ? Prio(x.Color) : -1;
            if (p > bp) { bp = p; best = x; }
        }
        return best;
    }

    // Prioriza el color mas caracteristico presente: cabeza 167 > blanco 97 > sombreado 242 > gama del cuerpo.
    private static int Prio(string color)
    {
        if (color.Contains(";167")) return 6;
        if (color == "97") return 5;
        if (color.Contains(";242")) return 4;
        if (color.Contains(";235") || color.Contains(";236") || color.Contains(";233")) return 3;
        return 1;
    }
}