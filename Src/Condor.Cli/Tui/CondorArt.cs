using System;

namespace Condor.Cli.Tui;

/// <summary>
/// RECURSO DE IDENTIDAD VISUAL de Condor (Docs/07_Interfaz/MASCOTA_CLI_UNICODE.md).
/// La mascota es arte Unicode de terminal; la matriz es el recurso de identidad y
/// la capa de presentacion solamente la pinta.
///
/// Dos presencias oficiales (T-020 P5):
///   * Condor Grande: bienvenida e inicio (arriba, 1:1 del SVG, 15x12, 100%).
///   * Condor Ave pequena: mascota de trabajo durante la sesion, reducida al ~50%
///     del Grande (SmallCondorMatrix), reutilizando su paleta aprobada.
///
/// Condor Grande se proyecta 1:1 sobre la rejilla oficial de 15x12 celdas (sin
/// escalar: la geometria del Grande vive unicamente en GrandeMatrix). La Ave
/// pequena es una matriz manuscrita independiente a menor escala (no es un 'scale'
/// geometrico programatico) que reproduce la identidad del Grande: cabeza
/// terracota, pico dorado, collar blanco y cuerpo #2A2924; sobre la rejilla el
/// pico usa medios bloques sobre las celdas que cruza, igual que en el SVG.
///
/// No se escala ni se transforma geometricamente (regla de proporcion del
/// documento de la mascota): cada presencia tiene su propia matriz; la capa de
/// presentacion solo pinta.
///
/// AJUSTE DE CONTRASTE (T-018, aprobado como correccion visual): las zonas que
/// se representaban en negro puro o casi negro (cuerpo 232 del Ave V16 y cuerpo
/// del Grande) usan ahora una escala de grises oscuros aprobada para seguirse
/// viendo sobre el fondo oscuro de la TUI:
///   #111315 -> 38;5;233   #272727 -> 38;5;235   #2A2D30 -> 38;5;236   #454546 -> 38;5;238
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

    /// <summary>
    /// Matriz logica de la mascota pequena de trabajo (T-020 P5): reduccion al
    /// ~50% del arte del Grande, hecha a mano sobre la misma rejilla oficial.
    /// Reutiliza la paleta del Grande (R/W/b/t/D). No es un scale programatico.
    /// </summary>
    private static readonly string[] SmallCondorMatrix =
    {
        "..RRR...",
        ".RWWbtD.",
        "..DDDDD.",
        ".DDDDDDD",
        "DDDDDDDD",
        "..DDD...",
    };

    /// <summary>Filas ya coloreadas de Condor Grande, listas para pintar (1:1 SVG, 15x12).</summary>
    public static readonly string[] Grande = BuildFromMatrix(GrandeMatrix);

    /// <summary>Mascota de trabajo pequena: Grande reducido al ~50% (T-020 P5).</summary>
    public static readonly string[] Ave = BuildFromMatrix(SmallCondorMatrix);

    /// <summary>Ancho visible maximo de Condor Ave (columnas de terminal).</summary>
    public static readonly int AveWidth = Ave.Max(Ansi.VisibleWidth);

    /// <summary>
    /// Convierte una matriz logica en filas SGR (identidad -> presentacion).
    /// El cuerpo 'D' recibe volumen con la escala oscura aprobada: luz central,
    /// tono medio y borde profundo; cabeza, collar y pico no cambian.
    /// </summary>
    private static string[] BuildFromMatrix(string[] matrix)
    {
        var rows = new string[matrix.Length];
        for (var r = 0; r < matrix.Length; r++)
        {
            var sb = new System.Text.StringBuilder();
            string? currentSgr = null;
            for (var i = 0; i < matrix[r].Length; i++)
            {
                var c = matrix[r][i];
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
}