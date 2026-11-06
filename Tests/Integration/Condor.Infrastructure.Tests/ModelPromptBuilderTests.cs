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
        Assert.Contains("nunca combines acciones con '/'", p, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rutas exactas escritas por el usuario", p, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("read_file sobre esa ruta", p, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("patch/edit_file/create_file", p);
        Assert.DoesNotContain("build/test/restore", p);
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

    [Fact]
    public void BuildSystemPrompt_EngineeringGuidance_PrefiereMinimalSolution()
    {
        var p = ModelPromptBuilder.BuildSystemPrompt("/dev", null, null);

        Assert.Contains("MINIMALISMO TECNICO", p, System.StringComparison.Ordinal);
        Assert.Contains("solucion mas pequena que cumpla los criterios", p, System.StringComparison.Ordinal);
        Assert.Contains("no agregues componentes", p, System.StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSystemPrompt_EngineeringGuidance_ComparaAlternativasAntesDeEditar()
    {
        var p = ModelPromptBuilder.BuildSystemPrompt("/dev", null, null);

        Assert.Contains("genera como maximo tres alternativas basadas en el codigo observado", p, System.StringComparison.Ordinal);
        Assert.Contains("compara sus diferencias, ventajas y limitaciones", p, System.StringComparison.Ordinal);
        Assert.Contains("recomienda la que mejor cumple el objetivo antes de editar", p, System.StringComparison.Ordinal);
        Assert.Contains("solo si la decision depende de una preferencia que no haya acordado", p, System.StringComparison.Ordinal);
    }
}
