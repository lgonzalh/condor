using System.Text.RegularExpressions;
using System.Linq;
using Condor.Cli.Tui;
using Condor.Core.Models;

namespace Condor.Cli.Tests;

/// <summary>
/// Identidad de la TUI: la mascota oficial y la leyenda institucional son parte
/// fija e invariante de la interfaz (T-018).
/// </summary>
public class IdentidadTuiTests
{
    [Fact]
    public void Leyenda_institucional_EsParteFijaDeLaCabecera()
    {
        var field = typeof(TuiHost).GetField("IdentityLine",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var value = field?.GetValue(null) as string;

        Assert.Equal("Hecho en Colombia · Modo Local 100%", value);
    }

    [Fact]
    public void Mascota_Grande_EsElAnsiOriginalV16()
    {
        // T-018: Grande es 100% del ANSI original (condor_unicode_v16.ps1) con la gama
        // de colores restituida: el cuerpo (antes casi-negro 232) usa la escala oscura
        // visible 235/236/233; se conservan 242 (sombreado), 167 (cabeza) y 97 (blanco).
        Assert.Equal(13, CondorArt.Grande.Length);

        Assert.Contains(CondorArt.Grande, row => row.Contains("38;5;235"));
        Assert.Contains(CondorArt.Grande, row => row.Contains("38;5;236"));
        Assert.Contains(CondorArt.Grande, row => row.Contains("38;5;233"));
        Assert.Contains(CondorArt.Grande, row => row.Contains("\u001b[38;5;242m"));
        Assert.Contains(CondorArt.Grande, row => row.Contains("\u001b[38;5;167m"));
        Assert.Contains(CondorArt.Grande, row => row.Contains("\u001b[97m"));

        // Ya no existe casi-negro puro, ni paleta SVG (collar 255 / pico dorado 179).
        Assert.DoesNotContain(CondorArt.Grande, row => row.Contains("\u001b[38;5;232m"));
        Assert.DoesNotContain(CondorArt.Grande, row => row.Contains("38;5;255"));
        Assert.DoesNotContain(CondorArt.Grande, row => row.Contains("38;5;179"));
    }

    [Fact]
    public void Mascota_Ave_Pequena_EsLaReferenciaANSI_ConservaIdentidad()
    {
        // La mascota de sesión es la cabeza compacta de referencia ANSI 24-bit
        // (matriz 8x8 en cuatro filas de bloques). Conserva la identidad cromática:
        // cabeza roja, punta de pico blanca, grises de cuerpo y contorno.
        Assert.Equal(4, CondorArt.Ave.Length);

        Assert.Contains(CondorArt.Ave, row => row.Contains("38;2;205;83;98"));   // cabeza
        Assert.Contains(CondorArt.Ave, row => row.Contains("38;2;255;255;255")); // punta de pico
        Assert.Contains(CondorArt.Ave, row => row.Contains("38;2;100;100;115")); // gris de cuerpo
        Assert.Contains(CondorArt.Ave, row => row.Contains("38;2;255;91;91"));   // luz de cabeza
        Assert.Contains(CondorArt.Ave, row => row.Contains("48;2;205;83;98"));   // fondo de cabeza

        // No usa la paleta 256 del arte grande ni una silueta monocromática.
        Assert.DoesNotContain(CondorArt.Ave, row => row.Contains("38;5;"));
        Assert.DoesNotContain(CondorArt.Ave, row => row.Contains("38;2;128;128;128"));
    }
}

/// <summary>
/// Estados honestos: cada texto deriva del estado real del sistema y explica
/// QUE se esta verificando o haciendo (nada de "Verificando..." ambiguo).
/// </summary>
public class EstadosHonestosTests
{
    [Theory]
    [InlineData(StartupStage.VerifyingOllamaServer, "Verificando disponibilidad de Ollama Server")]
    [InlineData(StartupStage.VerifyingModel, "Verificando modelo obtenido")]
    [InlineData(StartupStage.DownloadingModel, "Descargando modelo")]
    [InlineData(StartupStage.SelectingModel, "Seleccionando modelo adecuado para el equipo")]
    [InlineData(StartupStage.ReviewingResources, "Revisando recursos del equipo")]
    public void Estado_de_arranque_ExplicaLaOperacionReal(StartupStage stage, string esperado)
    {
        Assert.Equal(esperado, TuiStartupView.StageEstado(stage));
    }

    [Fact]
    public void Estado_de_agente_Verificacion_NombraSuObjeto()
    {
        var estado = TuiAgentProgressView.PhaseEstado(AgentProgress.Of(AgentPhase.Verifying));
        Assert.Equal("Verificando resultado de los cambios", estado);
    }

    [Fact]
    public void Estado_de_agente_Observacion_IncluyeAccionYRuta()
    {
        var estado = TuiAgentProgressView.PhaseEstado(
            AgentProgress.Of(AgentPhase.Observing, action: "list_dir", path: "Src"));
        Assert.Equal("Observando el proyecto (list_dir Src)", estado);
    }

    [Fact]
    public void Estado_de_agente_ErrorDeProveedor_SeExpresaClaro()
    {
        var estado = TuiAgentProgressView.PhaseEstado(
            AgentProgress.Of(AgentPhase.Finalizing, flag: ProgressFlag.ProviderError));
        Assert.Equal("El proveedor local no esta disponible ahora", estado);
    }
}

/// <summary>
/// Fotogramas de la pantalla persistente generados por el pintor real
/// (seam interno sin consola): estructura, regiones e identidad visibles.
/// </summary>
public class FotogramasTuiTests
{
    /// <summary>Rejilla 110x34 del fotograma (los marcos usan posicionamiento absoluto).</summary>
    private static string[] Grid(string frame)
    {
        const int cols = 110;
        const int rows = 34;
        var g = new char[rows][];
        for (var r = 0; r < rows; r++)
        {
            g[r] = new string(' ', cols).ToCharArray();
        }

        var row = 0;
        var col = 0;
        for (var i = 0; i < frame.Length; i++)
        {
            if (frame[i] == '\u001b' && i + 1 < frame.Length && frame[i + 1] == '[')
            {
                var j = i + 2;
                while (j < frame.Length && !char.IsLetter(frame[j]))
                {
                    j++;
                }

                if (j >= frame.Length)
                {
                    break;
                }

                var finalChar = frame[j];
                var body = frame.Substring(i + 2, j - i - 2);
                if (finalChar == 'H')
                {
                    var p = body.Split(';');
                    row = Math.Max(0, int.Parse(p[0]) - 1);
                    var c = p.Length > 1 && p[1].Length > 0 ? int.Parse(p[1]) : 1;
                    col = Math.Max(0, c - 1);
                }
                else if (finalChar == 'J')
                {
                    for (var r = 0; r < rows; r++)
                    {
                        g[r] = new string(' ', cols).ToCharArray();
                    }
                }
                else if (finalChar == 'K')
                {
                    for (var c = col; c < cols; c++)
                    {
                        g[row][c] = ' ';
                    }
                }

                i = j;
                continue;
            }

            if (!char.IsControl(frame[i]) && row < rows && col < cols)
            {
                g[row][col] = frame[i];
                col++;
            }
        }

        return g.Select(line => Ansi.StripSgr(new string(line)).TrimEnd()).ToArray();
    }

    private static TuiHost HostSesion()
    {
        var host = new TuiHost(forceInteractive: true);
        host.Enter();
        host.ShowSession("qwen2.5-coder:3b");
        return host;
    }

    [Fact]
    public void Sesion_MuestraIdentidad_Modelo_YContenidoDinamico()
    {
        using var host = HostSesion();
        host.SetEstado("Listo", ActivityKind.Success);
        host.AddActivity("Entorno listo. Modo Local 100% activo.", ActivityKind.Success);
        var grid = Grid(host.SnapshotFullFrame());

        // Cabecera: marca + version a la izquierda (una vez) y modelo real en la
        // linea de identidad, fuera del area del arte.
        var titulo = grid[0];
        Assert.Contains("CONDOR", titulo);
        Assert.Contains("v1.0 · build interno beta", titulo);
        Assert.Single(Regex.Matches(titulo, "build interno"));

        var cabecera = string.Join("\n", grid.Take(4));
        Assert.Contains("qwen2.5-coder:3b", cabecera);

        // La version NO se repite en ningun otro lugar (estado/panel/pie).
        Assert.DoesNotContain(grid.Skip(1), line => line.Contains("build interno"));

        // Sin titulos conceptuales ni paneles titulados (UX tipo CLI profesional).
        Assert.DoesNotContain(grid, line => line.Contains("ACTIVIDAD DEL AGENTE"));
        Assert.DoesNotContain(grid, line => line.Contains("REVISIÓN DEL CÓDIGO"));
        Assert.DoesNotContain(grid, line => line.Contains("ANÁLISIS DEL AGENTE"));
        Assert.DoesNotContain(grid, line => line.Contains("RESPUESTA / INFORME DEL AGENTE"));
        Assert.DoesNotContain(grid, line => line.Contains("Modo: Agente"));
        Assert.DoesNotContain(grid, line => line.Contains("Modelo:"));

        // Sin texto de espera redundante.
        Assert.DoesNotContain(grid, line => line.Contains("En espera de tu intencion"));

        // Comunicacion directa sin titulares "Estado:"/"Progreso:".
        Assert.DoesNotContain(grid, line => line.Contains("Estado:"));
        Assert.DoesNotContain(grid, line => line.Contains("Progreso:"));
        Assert.Contains(grid, line => line.Contains("Entorno listo"));

        // Placeholder oficial vigente (unico indicador de entrada).
        Assert.Contains(grid, line => line.Contains("¿Qué deseas construir? ..."));
        Assert.DoesNotContain(grid, line => line.Contains("Escriba una intencion"));

        // La mascota PEQUENA esta anclada a la izquierda: la primera fila visible
        // del arte aparece dentro de las primeras 5 columnas del area de contenido.
        var filaArte0 = Ansi.StripSgr(CondorArt.Ave[0]).TrimEnd();
        if (filaArte0.Length > 0)
        {
            var idxMascota = grid[0].IndexOf(filaArte0.TrimStart(), StringComparison.Ordinal);
            Assert.True(idxMascota <= 5, $"Mascota deberia estar a la izquierda, columna {idxMascota}");
        }
    }
    [Fact]
    public void Mascota_AncladaALaIzquierda()
    {
        using var host = HostSesion();
        var grid = Grid(host.SnapshotFullFrame());

        // La mascota PEQUENA esta anclada a la IZQUIERDA del area de contenido
        // (integrada al feed, no en columna lateral derecha).
        var filaArte = Ansi.StripSgr(CondorArt.Ave[0]).TrimEnd();
        if (filaArte.Length > 0)
        {
            var pintada = grid[0];
            var idx = pintada.IndexOf(filaArte.TrimStart(), StringComparison.Ordinal);
            Assert.True(idx <= 5, $"La mascota deberia estar a la izquierda, no en columna {idx}");
            // El modelo puede estar a la derecha, pero no dentro del ancho del arte.
            var aveWidth = TuiHost.AnchoVisibleMascota();
            for (var r = 0; r < CondorArt.Ave.Length; r++)
            {
                var modelIdx = grid[r].IndexOf("qwen2.5-coder", StringComparison.Ordinal);
                if (modelIdx >= 0)
                {
                    Assert.True(modelIdx > aveWidth,
                        $"El modelo no debe invadir el arte (fila {r}, columna {modelIdx})");
                }
            }
        }
    }

    [Fact]
    public void Mascota_ZonaLibre_DeTextoDeModelo()
    {
        using var host = HostSesion();
        host.AddActivity("Linea de actividad a la derecha de la mascota.", ActivityKind.System);
        var grid = Grid(host.SnapshotFullFrame());

        // La mascota ocupa las primeras filas de la cabecera. El modelo puede
        // aparecer a su derecha, pero nunca dentro del ancho visible del arte.
        var aveWidth = TuiHost.AnchoVisibleMascota();
        for (var r = 0; r < CondorArt.Ave.Length; r++)
        {
            var modelIdx = grid[r].IndexOf("qwen2.5-coder", StringComparison.Ordinal);
            if (modelIdx >= 0)
            {
                Assert.True(modelIdx > aveWidth,
                    $"El modelo no debe invadir el arte (fila {r}, columna {modelIdx})");
            }

            Assert.DoesNotContain("Modelo:", grid[r]);
            Assert.DoesNotContain("Modo:", grid[r]);
        }

        // La actividad dinamica se dibuja debajo de la cabecera/mascota.
        var activityRow = Array.FindIndex(grid, line => line.Contains("Linea de actividad"));
        Assert.True(activityRow >= CondorArt.Ave.Length,
            $"La actividad debe estar debajo del arte, no en fila {activityRow}");
    }

    [Fact]
    public void Cabecera_Modelo_Dinamico_SigueAlModeloReal()
    {
        using var host = HostSesion();
        host.SetEstado("x");

        host.SetModel("qwen2.5-coder:0.5b");
        var conMedio = string.Join("\n", Grid(host.SnapshotFullFrame()).Take(4));
        Assert.Contains("qwen2.5-coder:0.5b", conMedio);

        host.SetModel("qwen2.5-coder:3b");
        var conOtro = string.Join("\n", Grid(host.SnapshotFullFrame()).Take(4));
        Assert.Contains("qwen2.5-coder:3b", conOtro);
        Assert.DoesNotContain("qwen2.5-coder:0.5b", conOtro);
    }

    [Fact]
    public void Sesion_EnMarcha_MuestraEstadoRealDeVerificacion()
    {
        using var host = HostSesion();
        var view = new TuiAgentProgressView(host);
        view.Start("hola");
        view.Report(AgentProgress.Of(AgentPhase.Verifying, iteration: 2));
        var frame = Ansi.StripSgr(host.SnapshotFullFrame());

        // Estado real de la fase + indicador de trabajo; la metadata tecnica es minima.
        Assert.Contains("Verificando resultado de los cambios", frame);
        Assert.Contains("Condor esta trabajando", frame);
    }

    [Fact]
    public void Bienvenida_PresentaMascotaGrande()
    {
        var host = new TuiHost(forceInteractive: true);
        host.Enter();
        host.ShowWelcome();
        var frame = Ansi.StripSgr(host.SnapshotFullFrame());

        // La mascota Grande ocupa su bloque completo en la bienvenida y la
        // identidad institucional aparece bajo ella.
        Assert.Contains("CONDOR", frame);
        Assert.Contains("Observa · Comprende · Planifica · Construye · Verifica", frame);
        Assert.Contains("Hecho en Colombia · Modo Local 100%", frame);
    }

    [Fact]
    public void Bienvenida_SinTitularesEstadoProgreso()
    {
        var host = new TuiHost(forceInteractive: true);
        host.Enter();
        host.ShowWelcome();
        host.SetEstado("Iniciando ...");
        host.SetProgreso("etapa 1/5");
        var frame = Ansi.StripSgr(host.SnapshotFullFrame());

        // La comunicacion es directa, sin titulares artificiales.
        Assert.DoesNotContain("Estado:", frame);
        Assert.DoesNotContain("Progreso:", frame);
        Assert.Contains("Iniciando ...", frame);
    }
}

/// <summary>
/// Comentarios del usuario (-texto-): se distinguen de instrucciones/comandos
/// y nunca se interpretan como tarea a ejecutar (T-018).
/// </summary>
public class ComentariosUsuarioTests
{
    [Theory]
    [InlineData("-asi de esta manera-")]
    [InlineData("-nota interna-")]
    [InlineData("-a-")]
    public void TextoEntreGuiones_EsComentario(string texto)
    {
        Assert.True(CondorTui.EsComentarioUsuario(texto));
    }

    [Theory]
    [InlineData("hola")]
    [InlineData("/ayuda")]
    [InlineData("/salir")]
    [InlineData("-")]
    [InlineData("--")]
    [InlineData("---")]
    [InlineData("")]
    [InlineData("a-")]
    [InlineData("-a")]
    [InlineData("texto normal -con guion- dentro")]
    public void OtrosTextos_NoSonComentario(string texto)
    {
        Assert.False(CondorTui.EsComentarioUsuario(texto));
    }

    [Fact]
    public void Nulo_NoEsComentario()
    {
        Assert.False(CondorTui.EsComentarioUsuario(null!));
    }
}

public class UtilidadesAnsiTests
{
    [Fact]
    public void StripSgr_DejaSoloTextoVisible()
    {
        var text = "\u001b[38;5;167m▄▄\u001b[0m hola";
        Assert.Equal("▄▄ hola", Ansi.StripSgr(text));
    }

    [Fact]
    public void VisibleWidth_IgnoraSecuencias()
    {
        Assert.Equal(4, Ansi.VisibleWidth("\u001b[97m████\u001b[0m"));
    }
}

/// <summary>Verificaciones de arquitectura TUI 1/TUI 2/CLI 3 (T-018).</summary>
public class ArquitecturaInteraccionesTests
{
    private static string[] Grid(string frame)
    {
        const int cols = 110;
        const int rows = 34;
        var g = new char[rows][];
        for (var r = 0; r < rows; r++) g[r] = new string(' ', cols).ToCharArray();
        var row = 0; var col = 0;
        for (var i = 0; i < frame.Length; i++)
        {
            if (frame[i] == '\u001b' && i + 1 < frame.Length && frame[i + 1] == '[')
            {
                var j = i + 2;
                while (j < frame.Length && !char.IsLetter(frame[j])) j++;
                if (j >= frame.Length) break;
                var fc = frame[j]; var body = frame.Substring(i + 2, j - i - 2);
                if (fc == 'H') { var p = body.Split(';'); row = Math.Max(0, int.Parse(p[0]) - 1); col = Math.Max(0, (p.Length > 1 && p[1].Length > 0 ? int.Parse(p[1]) : 1) - 1); }
                else if (fc == 'J') { for (var r = 0; r < rows; r++) g[r] = new string(' ', cols).ToCharArray(); }
                else if (fc == 'K') { for (var c = col; c < cols; c++) g[row][c] = ' '; }
                i = j; continue;
            }
            if (!char.IsControl(frame[i]) && row < rows && col < cols) { g[row][col] = frame[i]; col++; }
        }
        return g.Select(line => Ansi.StripSgr(new string(line)).TrimEnd()).ToArray();
    }

    private static TuiHost HostSesion()
    {
        var host = new TuiHost(forceInteractive: true);
        host.Enter();
        host.ShowSession("qwen2.5-coder:3b");
        host.SetWorkspace("C:\\GitHub\\condor");
        return host;
    }
    [Fact]
    public static void Tui1_Bienvenida_AparecePrimero()
    {
        var host = new TuiHost(forceInteractive: true);
        host.Enter();
        host.ShowWelcome();
        host.SetWorkspace(Environment.CurrentDirectory);
        host.SetEstado("Iniciando ...");
        var grid = Grid(host.SnapshotFullFrame());

        Assert.Contains("CONDOR", grid[0]);
        Assert.Contains(grid, line => line.Contains("Iniciando ..."));
    }

    [Fact]
    public void Tui2_Sesion_MuestraMascotaPequena_NoGrande()
    {
        using var host = HostSesion();
        host.SetEstado("En espera de tu intencion", ActivityKind.Success);
        var grid = Grid(host.SnapshotFullFrame());

        // La sesion usa la cabeza compacta; el arte grande queda para la bienvenida.
        Assert.True(CondorArt.Ave.Length < CondorArt.Grande.Length);
        var primeraAve = Ansi.StripSgr(CondorArt.Ave[0]).TrimStart();
        Assert.Contains(grid, line => line.Contains(primeraAve));
        var muestraGrande = Ansi.StripSgr(CondorArt.Grande[1]).Trim();
        Assert.DoesNotContain(grid, line => line.Contains(muestraGrande));
        Assert.True(TuiHost.AnchoVisibleMascota() > 0);
    }

    [Fact]
    public void Tui2_Sesion_MuestraModeloRealEnCabecera()
    {
        using var host = HostSesion();
        host.SetEstado("En espera de tu intencion", ActivityKind.Success);
        var cabecera = string.Join("\n", Grid(host.SnapshotFullFrame()).Take(4));
        Assert.Contains("qwen2.5-coder:3b", cabecera);
    }
    [Fact]
    public void Tui2_Sesion_BarraEstadoMuestraWorkspaceYEstado()
    {
        using var host = HostSesion();
        host.SetWorkspace("C:\\GitHub\\prueba");
        host.SetEstado("Listo", ActivityKind.Success);
        var grid = Grid(host.SnapshotFullFrame());

        var statusBar = grid[grid.Length - 1];
        // La barra inferior muestra el workspace real y el estado; el modelo y la
        // version viven SOLO en la cabecera. No debe caer en una ruta fija.
        Assert.Contains("Listo", statusBar);
        Assert.Contains("C:\\GitHub\\prueba", statusBar);
        Assert.DoesNotContain("C:\\GitHub\\condor", statusBar);
        Assert.Contains("RAM", statusBar);
        Assert.DoesNotContain("qwen2.5-coder", statusBar);
        Assert.DoesNotContain("build interno", statusBar);
        Assert.DoesNotContain("Workspace:", statusBar);
        Assert.DoesNotContain("Modo:", statusBar);
        Assert.DoesNotContain("Modelo:", statusBar);
    }
    [Fact]
    public void Tui2_Sesion_SeparaZonasConSeparadores()
    {
        using var host = HostSesion();
        host.SetEstado("Listo", ActivityKind.Success);
        var grid = Grid(host.SnapshotFullFrame());

        // UX tipo CLI profesional: UNA zona de trabajo con contenido dinamico, sin titulos
        // conceptuales permanentes ni paneles titulados.
        Assert.DoesNotContain(grid, line => line.Contains("ACTIVIDAD DEL AGENTE"));
        Assert.DoesNotContain(grid, line => line.Contains("REVISIÓN DEL CÓDIGO"));
        Assert.DoesNotContain(grid, line => line.Contains("ANÁLISIS DEL AGENTE"));
        Assert.DoesNotContain(grid, line => line.Contains("RESPUESTA / INFORME DEL AGENTE"));
        Assert.Contains(grid, line => line.Contains("CONDOR"));
    }

    [Fact]
    public void Tui2_Sesion_PlaceholderExacto()
    {
        using var host = HostSesion();
        host.SetEstado("En espera de tu intencion", ActivityKind.Success);
        var grid = Grid(host.SnapshotFullFrame());
        Assert.Contains(grid, line => line.Contains("> ¿Qué deseas construir? ..."));
        Assert.DoesNotContain(grid, line => line.Contains("¿que deseas construir...?"));
        Assert.DoesNotContain(grid, line => line.Contains("Escriba una intencion"));
    }

    [Fact]
    public void Tui2_Sesion_SinTitularesEstadoProgreso()
    {
        using var host = HostSesion();
        host.SetEstado("En espera de tu intucion", ActivityKind.Success);
        host.SetProgreso("—");
        var grid = Grid(host.SnapshotFullFrame());
        Assert.DoesNotContain(grid, line => line.Contains("Estado:"));
        Assert.DoesNotContain(grid, line => line.Contains("Progreso:"));
    }

    [Fact]
    public void Tui2_Sesion_MascotaNoInvadeTextoDeModelo()
    {
        using var host = HostSesion();
        host.SetEstado("En espera de tu intucion", ActivityKind.Success);
        var grid = Grid(host.SnapshotFullFrame());
        var aveWidth = TuiHost.AnchoVisibleMascota();
        for (var r = 0; r < CondorArt.Ave.Length; r++)
        {
            var modelIdx = grid[r].IndexOf("qwen2.5-coder", StringComparison.Ordinal);
            if (modelIdx >= 0)
            {
                Assert.True(modelIdx > aveWidth,
                    $"El modelo no debe invadir el arte (fila {r}, columna {modelIdx})");
            }

            Assert.DoesNotContain("Modelo:", grid[r]);
            Assert.DoesNotContain("Workspace:", grid[r]);
        }
    }

    [Fact]
    public void Tui2_Sesion_CambioDeModelo_ActualizaCabecera()
    {
        using var host = HostSesion();
        host.SetEstado("En espera de tu intencion", ActivityKind.Success);
        host.SetModel("qwen2.5-coder:0.5b");
        var conMedio = string.Join("\n", Grid(host.SnapshotFullFrame()).Take(4));
        Assert.Contains("qwen2.5-coder:0.5b", conMedio);

        host.SetModel("qwen2.5-coder:3b");
        var conOtro = string.Join("\n", Grid(host.SnapshotFullFrame()).Take(4));
        Assert.Contains("qwen2.5-coder:3b", conOtro);
        Assert.DoesNotContain("qwen2.5-coder:0.5b", conOtro);
    }

    [Fact]
    public void Tui2_Ayuda_DentroDeSession_SinSuspender()
    {
        using var host = HostSesion();
        host.AddActivity("/ayuda", ActivityKind.User);
        host.Repaint();
        CondorTui.RenderHelpInTuiAccessible(host);
        host.SetEstado("Listo", ActivityKind.Success);
        host.SetProgreso("—");
        host.Repaint();

        var frame = Ansi.StripSgr(host.SnapshotFullFrame()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // La ayuda se agrega como actividades; la cabecera siempre muestra DisplayName.
        Assert.Contains(frame, line => line.Contains("build interno"));
        // Algunas lineas de ayuda deben estar visibles en la zona de actividad.
        Assert.Contains(frame, line => line.Contains("/salir"));
        Assert.Contains(frame, line => line.Contains("Contracciones:"));
    }

    [Fact]
    public void Tui2_Salir_CierraSesion()
    {
        Assert.True(CondorTui.IsExitAccessible("/salir"));
        Assert.True(CondorTui.IsExitAccessible("salir"));
        Assert.True(CondorTui.IsExitAccessible("/exit"));
        Assert.False(CondorTui.IsExitAccessible("no salir"));
    }

    [Fact]
    public void ComentarioUsuario_NoSeEjecutaComoIntencion()
    {
        Assert.True(CondorTui.EsComentarioUsuario("-asi de esta manera-"));
        Assert.True(CondorTui.EsComentarioUsuario("-nota interna-"));
        Assert.False(CondorTui.EsComentarioUsuario("hola"));
        Assert.False(CondorTui.EsComentarioUsuario("/ayuda"));
        Assert.False(CondorTui.EsComentarioUsuario(""));
    }

    [Fact]
    public void Workspace_Real_NoApareceComoPanelEnBienvenida()
    {
        var host = new TuiHost(forceInteractive: true);
        host.Enter();
        host.ShowWelcome();
        host.SetWorkspace(Environment.CurrentDirectory);
        host.SetEstado("Iniciando ...");
        var grid = Grid(host.SnapshotFullFrame());

        // La bienvenida NO muestra "Workspace:" como panel etiquetado.
        Assert.DoesNotContain(grid, line => line.Contains("Workspace:"));
        Assert.DoesNotContain(grid, line => line.Contains("Modelo:"));
        Assert.DoesNotContain(grid, line => line.Contains("Modo:"));
        // El estado de bienvenida aparece en su propia linea (sin titular).
        Assert.Contains(grid, line => line.Contains("Iniciando ..."));
    }

    [Fact]
    public void Tui2_Sesion_EntradaEnParteInferior()
    {
        using var host = HostSesion();
        host.SetEstado("En espera de tu intencion", ActivityKind.Success);
        var grid = Grid(host.SnapshotFullFrame());

        var placeholderRow = Array.FindIndex(grid, line => line.Contains("¿Qué deseas construir"));
        Assert.True(placeholderRow >= 28, $"Placeholder debe estar abajo, no en fila {placeholderRow}");
    }

}

