using System.Collections.Generic;
using Condor.Core.Models;
using Condor.Infrastructure.Agent;

namespace Condor.Infrastructure.Tests;

/// <summary>Adaptacion del prompt al modelo seleccionado (harness).</summary>
public class ModelPromptBuilderTests
{
    [Fact]
    public void SeAdaptaAlModelo_SinHerramientasNiEstructura()
    {
        var sin = new ModelCandidate
        {
            PullName = "tiny",
            ToolUse = false,
            StructuredOutput = false,
            MultiFileLevel = 0,
            Capabilities = new List<string> { "completion" }
        };

        var p = ModelPromptBuilder.BuildSystemPrompt("/dev", null, sin);

        Assert.Contains("NO ejecuta herramientas externas", p, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sin JSON", p, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IncluyeElModeloEnUso()
    {
        var model = new ModelCandidate
        {
            PullName = "qwen2.5-coder:3b",
            ToolUse = true,
            StructuredOutput = true,
            MultiFileLevel = 2,
            Capabilities = new List<string> { "tool-use", "structured-output", "coding" }
        };

        var p = ModelPromptBuilder.BuildSystemPrompt("/dev", null, model);

        Assert.Contains("qwen2.5-coder:3b", p, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConHerramientas_ExigeJsonDeAcciones()
    {
        var model = new ModelCandidate
        {
            PullName = "qwen2.5-coder:3b",
            ToolUse = true,
            StructuredOutput = true,
            MultiFileLevel = 2
        };

        var p = ModelPromptBuilder.BuildSystemPrompt("/dev", null, model);

        Assert.Contains("JSON valido", p, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("create_file", p);
        Assert.Contains("edit_file", p);
        Assert.Contains("patch", p);
        Assert.Contains("build ->", p);
        Assert.Contains("test ->", p);
        Assert.Contains("restore ->", p);
        Assert.Contains("git_log ->", p);
        Assert.Contains("git_show con path=<hash hexadecimal>", p);
        Assert.Contains("inspeccionar cada hash", p, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nunca combines acciones con '/'", p, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rutas exactas escritas por el usuario", p, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("para un archivo existente usa read_file sobre esa ruta", p, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("para un archivo nuevo, lista con list_dir su directorio padre existente", p, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no repitas list_dir ni read_file con las mismas rutas", p, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Lee el archivo y la linea que indique el compilador o runner", p, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No edites un archivo distinto para responder a un error localizado", p, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("content lleva el contenido fuente completo", p, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nunca copies la ruta o el nombre como contenido", p, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("planItems", p, System.StringComparison.Ordinal);
        Assert.Contains("expectedResult", p, System.StringComparison.Ordinal);
        Assert.Contains("no distribuyas esos datos en listas paralelas", p, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("una tarea a la vez", p, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("patch/edit_file/create_file", p);
        Assert.DoesNotContain("build/test/restore", p);
    }

    [Fact]
    public void PerfilReviewer_ComparaObjetivoDiffYComponentesRelacionados()
    {
        var model = new ModelCandidate
        {
            PullName = "qwen2.5-coder:3b",
            ToolUse = true,
            StructuredOutput = true,
            MultiFileLevel = 2
        };

        var prompt = ModelPromptBuilder.BuildSystemPrompt(
            "/dev",
            "app.csproj",
            model,
            profile: Condor.Core.Evaluation.AgentProfileCatalog.Reviewer);

        Assert.Contains("objetivo con el diff frente a HEAD", prompt, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("interfaces, llamadores o dependientes", prompt, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("componentes relacionados", prompt, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("origen uncertain", prompt, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PerfilesDeIngenieriaYPruebas_ExigenReproducirLaCausaAntesDeCorregir()
    {
        var model = new ModelCandidate
        {
            PullName = "qwen2.5-coder:3b",
            ToolUse = true,
            StructuredOutput = true,
            MultiFileLevel = 2
        };

        var engineerPrompt = ModelPromptBuilder.BuildSystemPrompt(
            "/dev",
            "app.csproj",
            model,
            profile: Condor.Core.Evaluation.AgentProfileCatalog.Engineer);
        var testEngineerPrompt = ModelPromptBuilder.BuildSystemPrompt(
            "/dev",
            "app.csproj",
            model,
            profile: Condor.Core.Evaluation.AgentProfileCatalog.TestEngineer);

        Assert.Contains("reprodúcelo contra la versión base", engineerPrompt, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sigue la causa", engineerPrompt, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"planItems\":[{\"task\":\"Crear la prueba de regresión\",\"expectedResult\":\"El archivo de prueba contiene el caso solicitado\",\"verificationStep\":\"Ejecutar el caso con dotnet test\"}]", engineerPrompt, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Respeta literalmente las rutas indicadas por el usuario también en scopePaths", engineerPrompt, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("falla contra la versión base y pasa con la corrección", testEngineerPrompt, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"action\":\"plan\",\"objective\":\"Agregar la prueba solicitada\",\"scopePaths\":[\"Tests/CalculatorTests.cs\"]", testEngineerPrompt, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("crea o ajusta pruebas unitarias", testEngineerPrompt, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("usa exactamente los nombres devueltos, sin inventar rutas", testEngineerPrompt, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lista esa subcarpeta antes de leer o editar", testEngineerPrompt, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("prueba de extremo a extremo", testEngineerPrompt, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("capturas o estados renderizados", testEngineerPrompt, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TuiVisualSnapshotTests", testEngineerPrompt, System.StringComparison.Ordinal);
        Assert.Contains("Update-TuiVisualSnapshots.ps1", testEngineerPrompt, System.StringComparison.Ordinal);
        Assert.Contains("No afirmes cobertura que no verificaste", testEngineerPrompt, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("un flag desactivado por defecto", engineerPrompt, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("un entorno controlado", engineerPrompt, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("habilitación gradual con criterios de avance y reversión", engineerPrompt, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no ejecutes la publicación ni la reversión sin autorización humana", engineerPrompt, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("flag desactivado por defecto", testEngineerPrompt, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProyectoMultiArchivo_SeRefuerzaElContextoRelacional()
    {
        var model = new ModelCandidate
        {
            PullName = "qwen2.5-coder:3b",
            ToolUse = true,
            StructuredOutput = true,
            MultiFileLevel = 3
        };

        var p = ModelPromptBuilder.BuildSystemPrompt("/dev", "app.csproj", model);

        Assert.Contains("proyecto multi-archivo", p, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("app.csproj", p, System.StringComparison.OrdinalIgnoreCase);
    }
}
