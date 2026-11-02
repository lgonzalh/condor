using Condor.Cli.Presentation;
using Condor.Core.Models;
using Condor.Core.Serialization;

namespace Condor.Infrastructure.Tests;

public class DocumenterRenderingTests
{
    [Fact]
    public void AgentRenderer_MuestraRutaDelInformeGenerado()
    {
        var text = AgentRenderer.BuildResultText(new AgentResult
        {
            Success = true,
            Documentation = new DocumentationResult
            {
                Success = true,
                RelativePath = "docs/prueba.md"
            }
        });

        Assert.Contains("Informe Antes/Durante/Despues", text);
        Assert.Contains("docs/prueba.md", text);
    }

    [Fact]
    public void Json_OmiteInformeNuloYLoIncluyeCuandoExiste()
    {
        var oldAgentJson = AgentJson.Serialize(new AgentResult());
        var newAgentJson = AgentJson.Serialize(new AgentResult
        {
            Documentation = new DocumentationResult { Success = true, RelativePath = "docs/a.md" }
        });
        var oldCycleJson = CycleJson.Serialize(new CycleResult());
        var newCycleJson = CycleJson.Serialize(new CycleResult
        {
            Documentation = new DocumentationResult { Success = true, RelativePath = "docs/b.md" }
        });

        Assert.DoesNotContain("documentation", oldAgentJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("documentation", oldCycleJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("docs/a.md", newAgentJson);
        Assert.Contains("docs/b.md", newCycleJson);
    }
}
