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
    public void RespuestaEsConversacional_SinEtiquetasTecnicas()
    {
        // REQUISITO: la respuesta final debe ser natural; no se exponen como
        // etiquetas obligatorias [PROGRESO]/[ANALISIS]/[HALLAZGOS]/[VERIFICACION]/[RESULTADO].
        var result = new AgentResult
        {
            Success = true,
            Model = "qwen2.5-coder:3b",
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

        Assert.DoesNotContain("[PROGRESO]", text);
        Assert.DoesNotContain("[ANALISIS]", text);
        Assert.DoesNotContain("[HALLAZGOS]", text);
        Assert.DoesNotContain("[VERIFICACION]", text);
        Assert.DoesNotContain("[RESULTADO]", text);
        Assert.Contains("Correccion aplicada y verificada.", text);
    }

    [Fact]
    public void Cambios_SeMencionanEnProsaSinVolcarContenido()
    {
        var result = new AgentResult
        {
            Success = true,
            Reason = "Ok",
            Steps = { Obs("edit_file", "src/Program.cs", preview: "M contenido largo que no debe volcarse por completo en la salida humana") }
        };

        var text = AgentRenderer.BuildResultText(result);

        Assert.Contains("modificado", text);
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
        Assert.Contains("app.js", text);
        Assert.Contains("Proyecto web estatico", text);
    }

    [Fact]
    public void ConclusionUnica_NoSeRepite()
    {
        // La sintesis del modelo aparece una sola vez (como respuesta), no repetida.
        var result = new AgentResult
        {
            Success = true,
            Reason = "La aplicacion calcula la suma de dos enteros por consola.",
            Steps =
            {
                Obs("read_file", "src/Program.cs", preview: "class Program { ... }"),
                Obs("read_file", "src/Calculator.cs", preview: "public int Add(int a, int b)")
            }
        };

        var text = AgentRenderer.BuildResultText(result);

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(text, System.Text.RegularExpressions.Regex.Escape("La aplicacion calcula la suma de dos enteros por consola.")));
        Assert.Contains("src/Program.cs", text);
    }

    [Fact]
    public void Inventario_SePresentaComoContextoNatural()
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
                Cpu = "Intel Core i5 4 nucleos 8 hebras",
                FreeDiskGb = 100.0,
                SelectedModel = "qwen2.5-coder:3b",
                ModelCapabilities = new() { "completion", "structured-output", "coding" }
            }
        };

        var text = AgentRenderer.BuildResultText(result);

        Assert.DoesNotContain("[INVENTARIO]", text);
        Assert.Contains("Contexto del entorno", text);
        Assert.Contains("qwen2.5-coder:3b", text);
        Assert.Contains("structured-output", text);
        Assert.Contains("presupuesto", text);
    }

    [Fact]
    public void FirmaFinal_IncluyeModeloYTiempo()
    {
        var result = new AgentResult { Success = true, Model = "qwen2.5-coder:3b" };

        var text = AgentRenderer.BuildResultText(result, TimeSpan.FromSeconds(32.7));

        Assert.Contains("©Condor - qwen2.5-coder:3b -", text);
        Assert.Contains(" s", text.Substring(text.Length - 8)); // segundos en la firma
    }

    [Fact]
    public void Actividad_NoIncluyeCabeceraRepetida()
    {
        // La marca superior y el eslogan viven una sola vez en la cabecera de la TUI,
        // no se repiten en cada entrada de la zona de actividad (UX minimalista).
        var result = new AgentResult { Success = true, Model = "qwen2.5-coder:3b", Reason = "Ok." };

        var text = AgentRenderer.BuildResultText(result);

        Assert.DoesNotContain("Condor" + Environment.NewLine + "Observa", text);
        Assert.DoesNotContain("Planifica · Construye · Verifica", text);
    }
}


