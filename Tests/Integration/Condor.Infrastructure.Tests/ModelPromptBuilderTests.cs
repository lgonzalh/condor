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
            PullName = "tiny", ToolUse = false, StructuredOutput = false,
            MultiFileLevel = 0, Capabilities = new List<string> { "completion" }
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
            PullName = "qwen2.5-coder:3b", ToolUse = true, StructuredOutput = true,
            MultiFileLevel = 2, Capabilities = new List<string> { "tool-use", "structured-output", "coding" }
        };

        var p = ModelPromptBuilder.BuildSystemPrompt("/dev", null, model);

        Assert.Contains("qwen2.5-coder:3b", p, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConHerramientas_ExigeJsonDeAcciones()
    {
        var model = new ModelCandidate
        {
            PullName = "qwen2.5-coder:3b", ToolUse = true, StructuredOutput = true,
            MultiFileLevel = 2
        };

        var p = ModelPromptBuilder.BuildSystemPrompt("/dev", null, model);

        Assert.Contains("JSON valido", p, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("patch/edit_file/create_file", p);
    }

    [Fact]
    public void ProyectoMultiArchivo_SeRefuerzaElContextoRelacional()
    {
        var model = new ModelCandidate
        {
            PullName = "qwen2.5-coder:3b", ToolUse = true, StructuredOutput = true,
            MultiFileLevel = 3
        };

        var p = ModelPromptBuilder.BuildSystemPrompt("/dev", "app.csproj", model);

        Assert.Contains("proyecto multi-archivo", p, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("app.csproj", p, System.StringComparison.OrdinalIgnoreCase);
    }
}
