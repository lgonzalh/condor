using Condor.Cli.Presentation;
using Condor.Core.Models;
using Condor.Core.Serialization;
namespace Condor.Infrastructure.Tests;

public class AgentRendererTests
{
    private static AgentStep Obs(string action, string path, bool ok = true, string? preview = null)
        => new() { Iteration = 1, Action = action, Path = path, Success = ok, ResultPreview = preview };

    [Fact]
    public void ReadFile_NoImprimeElContenidoCompleto()
    {
        var content = "body { margin: 0; }" + Environment.NewLine + "h1 { color: red; }" + string.Concat(System.Linq.Enumerable.Repeat(Environment.NewLine + "/* linea */", 30));
        var result = new AgentResult
        {
            Success = true,
            Reason = "El proyecto es una pagina web estatica sin errores evidentes.",
            Steps = { Obs("read_file", "deepseek_css_20260819_53fb29.css", preview: "--- CONTENIDO ---" + content + "--- FIN ---") }
        };

        var text = AgentRenderer.BuildResultText(result);

        // No debe volcarse el contenido completo del archivo leido.
        Assert.DoesNotContain("/* linea */", text);
        Assert.DoesNotContain("h1 { color: red; }", text);
        // El nombre del archivo observado aparece resumido.
        Assert.Contains("deepseek_css_20260819_53fb29.css", text);
    }

    [Fact]
    public void Json_ConservaElContenidoParaConsumidoresExternos()
    {
        var content = "body { margin: 0; }";
        var result = new AgentResult
        {
            Success = true,
            Model = "qwen2.5-coder:3b",
            Objective = "revisa el proyecto",
            Reason = "Sin errores",
            Steps = { Obs("read_file", "a.css", preview: content) }
        };

        var json = AgentJson.Serialize(result);

        // El contrato --json conserva el contenido completo (la presentacion
        // humana y la salida estructurada estan desacopladas).
        Assert.Contains("body { margin: 0; }", json);
    }

    [Fact]
    public void Bloques_SeparadosProgresoAnalisisVerificacionResultado()
    {
        var result = new AgentResult
        {
            Success = true,
            Reason = "Correccion aplicada y verificada.",
            Steps =
            {
                Obs("read_file", "src/Program.cs", preview: "contenido completo"),
                Obs("patch", "src/Program.cs", preview: "+1/-1"),
                Obs("build", "", ok: true, preview: "Compilacion correcta"),
                Obs("test", "", ok: true, preview: "2/2")
            }
        };

        var text = AgentRenderer.BuildResultText(result);

        Assert.Contains("[PROGRESO]", text);
        Assert.Contains("[ANALISIS]", text);
        Assert.Contains("[CAMBIOS]", text);
        Assert.Contains("[VERIFICACION]", text);
        Assert.Contains("[RESULTADO]", text);
    }

    [Fact]
    public void Cambios_MuestranResumenSinContenido()
    {
        var result = new AgentResult
        {
            Success = true,
            Reason = "Ok",
            Steps = { Obs("edit_file", "src/Program.cs", preview: "M contenido largo que no debe volcarse por completo en la salida humana") }
        };

        var text = AgentRenderer.BuildResultText(result);

        // El bloque CAMBIOS cita el archivo y una metrica, no vierte el documento completo.
        Assert.Contains("[CAMBIOS]", text);
        Assert.Contains("src/Program.cs", text);
        Assert.DoesNotContain("contenido largo que no debe volcarse", text);
    }

    [Fact]
    public void IntencionInformativa_ProduceSalidaCompacta()
    {
        var result = new AgentResult
        {
            Success = true,
            Reason = "Proyecto web estatico: HTML + CSS + JavaScript, sin errores evidentes.",
            Steps =
            {
                Obs("read_file", "index.html"),
                Obs("read_file", "estilos.css"),
                Obs("read_file", "app.js")
            }
        };

        var text = AgentRenderer.BuildResultText(result);

        Assert.Contains("index.html", text);
        Assert.Contains("estilos.css", text);
        Assert.Contains("app.js", text);
        Assert.Contains("Proyecto web estatico", text);
    }

    [Fact]
    public void Hallazgos_SonEvidenciaDistintaDelResultado()
    {
        // REQUISITO: [HALLAZGOS] y [RESULTADO] deben ser distintos. HALLAZGOS es la
        // evidencia objetiva observada (archivos inspeccionados); RESULTADO es el
        // analisis derivado del modelo (Reason). No deben repetirse.
        var result = new AgentResult
        {
            Success = true,
            Reason = "La aplicacion calcula la suma de dos enteros pasados por consola.",
            Steps =
            {
                Obs("read_file", "src/Program.cs", preview: "class Program { static void Main(string[] args) { ... } }"),
                Obs("read_file", "src/Calculator.cs", preview: "public int Add(int a, int b)")
            }
        };

        var text = AgentRenderer.BuildResultText(result);

        // RESULTADO contiene el analisis del modelo.
        Assert.Contains("[RESULTADO]", text);
        Assert.Contains("calcula la suma", text);
        // HALLAZGOS contiene la EVIDENCIA observada (archivos inspeccionados), NO la
        // sintesis; por tanto la sintesis solo aparecera una vez (en RESULTADO).
        Assert.Contains("[HALLAZGOS]", text);
        Assert.Contains("Se inspecciono 'src/Program.cs'", text);
        // El analisis no debe repetirse como hallazgo.
        var resultOnly = text.IndexOf("calcula la suma", System.StringComparison.Ordinal);
        Assert.True(resultOnly >= 0);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(text, "calcula la suma"));
    }

    [Fact]
    public void Inventario_SePresentaCuandoExiste()
    {
        var result = new AgentResult
        {
            Success = true,
            Reason = "Analisis.",
            Inventory = new AgentInventory
            {
                RamTotalGb = 15.4,
                RamFreeGb = 7.0,
                SafeBudgetGb = 2.5,
                PressureLabel = "Normal",
                Cpu = "Intel Core i5\n4 nucleos\n8 hebras",
                FreeDiskGb = 100.0,
                SelectedModel = "qwen2.5-coder:3b",
                SelectionReason = "El modelo deseado ya existe en Ollama; se reutiliza.",
                ModelCapabilities = new() { "completion", "structured-output", "coding" }
            }
        };

        var text = AgentRenderer.BuildResultText(result);

        Assert.Contains("[INVENTARIO]", text);
        Assert.Contains("qwen2.5-coder:3b", text);
        Assert.Contains("structured-output", text);
        Assert.Contains("presupuesto seguro", text);
        Assert.Contains("Modelo: qwen2.5-coder:3b", text);
    }
}
