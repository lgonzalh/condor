using System.Collections.Generic;
using Condor.Core.Models;
using Condor.Core.Setup;
using Condor.Core.Serialization;

namespace Condor.Core.Tests;

public class SetupEvaluatorTests
{
    [Fact]
    public void Evaluate_SinAssessment_NotDetected()
    {
        var r = SetupEvaluator.Evaluate(null, "C:\\state", false, false, null, SetupLimits.Default);

        Assert.Equal(DetectionStatus.NotDetected, r.Status);
        Assert.Contains("analizar", r.Reason!, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_ConTodoListo_Detected()
    {
        var r = SetupEvaluator.Evaluate(AssessmentListo(), "C:\\state", true, true, null, SetupLimits.Default);

        Assert.Equal(DetectionStatus.Detected, r.Status);
        Assert.True(r.RequiredPresent == r.RequiredTotal);
    }

    [Fact]
    public void Evaluate_FaltaDotnet_ObligatoriaAusente_NotDetected()
    {
        var r = SetupEvaluator.Evaluate(AssessmentSinDotnet(), "C:\\state", true, true, null, SetupLimits.Default);

        Assert.Equal(DetectionStatus.NotDetected, r.Status);
        Assert.Contains(r.Dependencies, d => d.IsRequired && !d.Present);
    }

    [Fact]
    public void Evaluate_EstadoLocalAusente_Limited()
    {
        var r = SetupEvaluator.Evaluate(AssessmentListo(), "C:\\state", false, false, "no existe", SetupLimits.Default);

        Assert.Equal(DetectionStatus.Limited, r.Status);
        Assert.False(r.StateUsable);
    }

    [Fact]
    public void Evaluate_EstadoLocalIlegible_Limited()
    {
        var r = SetupEvaluator.Evaluate(AssessmentListo(), "C:\\state", true, false, "ilegible", SetupLimits.Default);

        Assert.Equal(DetectionStatus.Limited, r.Status);
    }

    [Fact]
    public void Evaluate_OpcionOpcionalAusente_MarcaPeroNoObligatoria()
    {
        var r = SetupEvaluator.Evaluate(AssessmentSinOllama(), "C:\\state", true, true, null, SetupLimits.Default);

        Assert.Contains(r.Dependencies, d => !d.IsRequired && !d.Present);
        Assert.Equal(DetectionStatus.Detected, r.Status);
    }

    [Fact]
    public void Evaluate_DistinguirObligatoriasYOpcionales()
    {
        var r = SetupEvaluator.Evaluate(AssessmentListo(), "C:\\state", true, true, null, SetupLimits.Default);

        Assert.True(r.RequiredTotal >= 2);
        Assert.Contains(r.Dependencies, d => d.IsRequired);
        Assert.Contains(r.Dependencies, d => !d.IsRequired);
    }

    [Fact]
    public void Evaluate_PlataformaWindows()
    {
        var r = SetupEvaluator.Evaluate(AssessmentListo(), "C:\\state", true, true, null, SetupLimits.Default);

        Assert.Equal("windows", r.Platform);
    }

    [Fact]
    public void Determinismo_InvocacionesIgualesProducenMismoResultado()
    {
        var assessment = AssessmentListo();
        var first = SetupEvaluator.Evaluate(assessment, "C:\\state", true, true, null, SetupLimits.Default);
        var second = SetupEvaluator.Evaluate(assessment, "C:\\state", true, true, null, SetupLimits.Default);

        first.GeneratedAtUtc = System.DateTime.MinValue;
        second.GeneratedAtUtc = System.DateTime.MinValue;

        Assert.Equal(SetupJson.Serialize(first), SetupJson.Serialize(second));
    }

    private static AssessmentResult AssessmentListo()
    {
        return new AssessmentResult
        {
            Capabilities = new CapabilitiesSummary
            {
                LocalLlm = true,
                GpuDetected = true,
                VisionCapable = true,
                OllamaReady = true,
                ModelsCount = 2
            },
            Tools = new ToolsProfile
            {
                Git = new ToolInfo { Name = "git", Status = DetectionStatus.Detected },
                Ollama = new OllamaStatus
                {
                    Installed = true,
                    ServerRunning = true,
                    Models = new List<ModelInfo>
                    {
                        new() { Name = "a", Capabilities = new List<string> { "completion" } },
                        new() { Name = "b", Capabilities = new List<string> { "completion" } }
                    }
                },
                DetectedTools = new List<ToolInfo>
                {
                    new() { Name = "dotnet", Status = DetectionStatus.Detected },
                    new() { Name = "python", Status = DetectionStatus.Detected }
                }
            }
        };
    }

    private static AssessmentResult AssessmentSinDotnet()
    {
        var a = AssessmentListo();
        a.Tools.DetectedTools.RemoveAll(t => t.Name == "dotnet");
        return a;
    }

    private static AssessmentResult AssessmentSinOllama()
    {
        var a = AssessmentListo();
        a.Tools.Ollama.Installed = false;
        a.Capabilities.LocalLlm = false;
        a.Tools.Ollama.Models.Clear();
        a.Capabilities.ModelsCount = 0;
        return a;
    }
}
