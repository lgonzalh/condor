using System.Collections.Generic;
using Condor.Core.Models;
using Condor.Core.Vision;

namespace Condor.Core.Tests;

public class VisionGateTests
{
    [Fact]
    public void Evaluate_SinAssessment_NoDisponible()
    {
        var r = VisionGate.Evaluate(null);

        Assert.False(r.Available);
        Assert.Contains("vision", r.Reason!, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_VisionCapableFalse_NoDisponible()
    {
        var assessment = new AssessmentResult
        {
            Capabilities = new CapabilitiesSummary { VisionCapable = false }
        };

        var r = VisionGate.Evaluate(assessment);

        Assert.False(r.Available);
    }

    [Fact]
    public void Evaluate_SinModeloVision_NoDisponible()
    {
        var assessment = new AssessmentResult
        {
            Capabilities = new CapabilitiesSummary { VisionCapable = true },
            Tools = new ToolsProfile
            {
                Ollama = new OllamaStatus
                {
                    Models = new List<ModelInfo>
                    {
                        new() { Name = "modelo-texto", Capabilities = new List<string> { "completion" } }
                    }
                }
            }
        };

        var r = VisionGate.Evaluate(assessment);

        Assert.False(r.Available);
        Assert.Contains("vision", r.Reason!, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_ConModeloVision_DisponibleYSeleccionaDeterminista()
    {
        var assessment = new AssessmentResult
        {
            Capabilities = new CapabilitiesSummary { VisionCapable = true },
            Tools = new ToolsProfile
            {
                Ollama = new OllamaStatus
                {
                    Models = new List<ModelInfo>
                    {
                        new() { Name = "llm3.2-vision", Capabilities = new List<string> { "vision" } },
                        new() { Name = "codigo", Capabilities = new List<string> { "completion" } }
                    }
                }
            }
        };

        var r = VisionGate.Evaluate(assessment);

        Assert.True(r.Available);
        Assert.Equal("llm3.2-vision", r.SelectedModel);
    }

    [Fact]
    public void Determinismo_ConModelosVision_SeleccionEstable()
    {
        var assessment = new AssessmentResult
        {
            Capabilities = new CapabilitiesSummary { VisionCapable = true },
            Tools = new ToolsProfile
            {
                Ollama = new OllamaStatus
                {
                    Models = new List<ModelInfo>
                    {
                        new() { Name = "b", Capabilities = new List<string> { "vision" } },
                        new() { Name = "a", Capabilities = new List<string> { "vision" } }
                    }
                }
            }
        };

        var first = VisionGate.Evaluate(assessment);
        var second = VisionGate.Evaluate(assessment);

        Assert.Equal(first.SelectedModel, second.SelectedModel);
        Assert.Equal("a", first.SelectedModel);
    }
}
