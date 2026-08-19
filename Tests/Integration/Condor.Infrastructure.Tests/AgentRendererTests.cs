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
}
