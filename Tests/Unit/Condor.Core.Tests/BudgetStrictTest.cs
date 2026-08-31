using System.Collections.Generic;
using Condor.Core.Catalog;
using Condor.Core.Evaluation;
using Condor.Core.Models;
using Condor.Core.Selection;
using Xunit;

namespace Condor.Core.Tests;

public class BudgetStrictTest
{
    [Fact]
    public void Budget_StrictHeadroom_Models_0_5B_1B_1_5B_3B_7B_Selects_Strictly_Below_Headroom()
    {
        // Con 6GB libre de 16GB total:
        // ModelMemoryBudget headroom = free - 1.5 - 1.5 - OperatingMarginGb(16)
        // OperatingMarginGb(16) = min(3, max(1.5, 16*0.08)) = 1.5
        // headroom = 6 - 4.5 = 1.5 GB
        // Peak estimates: 0.5B->~0.44, 1B->~0.98, 1.5B->~1.1, 3B->~2.16, 7B->~5.23
        // Solo 0.5B, 1B, 1.5B caben (< 1.5 GB headroom). 3B (2.16) NO cabe.
        var memory = new MemoryInfo
        {
            Status = DetectionStatus.Detected,
            TotalBytes = 16L * 1024 * 1024 * 1024,
            FreeBytes = 6L * 1024 * 1024 * 1024
        };

        var assessment = new AssessmentResult
        {
            Environment = new EnvironmentProfile { Memory = memory },
            Tools = new ToolsProfile
            {
                Ollama = new OllamaStatus
                {
                    Installed = true,
                    ServerRunning = true,
                    Models = new List<ModelInfo>
                    {
                        new() { Name = "qwen2.5-coder:0.5b", SizeBytes = 397L * 1024 * 1024, Capabilities = new List<string> { "completion" } },
                        new() { Name = "gemma3:1b", SizeBytes = 815L * 1024 * 1024, Capabilities = new List<string> { "completion" } },
                        new() { Name = "qwen2.5-coder:1.5b", SizeBytes = 986L * 1024 * 1024, Capabilities = new List<string> { "completion" } },
                        new() { Name = "qwen2.5-coder:3b", SizeBytes = 1848L * 1024 * 1024, Capabilities = new List<string> { "completion" } },
                        new() { Name = "qwen2.5-coder:7b", SizeBytes = 4706L * 1024 * 1024, Capabilities = new List<string> { "completion" } }
                    }
                }
            }
        };

        var r = ModelSelector.RecommendFromCatalog(assessment, ModelCatalog.Default, "agente");

        // Verificar: NO debe seleccionar 3B, 7B (>= headroom)
        Assert.NotNull(r.Desired);
        Assert.False(r.Desired.PullName.Contains("3b"), "No debe seleccionar 3B (superior al headroom)");
        Assert.False(r.Desired.PullName.Contains("7b"), "No debe seleccionar 7B (superior al headroom)");

        // Debe seleccionar 0.5B, 1B, o 1.5B (estrictamente inferior al headroom)
        Assert.True(
            r.Desired.PullName.Contains("0.5b") || r.Desired.PullName.Contains("1b") || r.Desired.PullName.Contains("gemma3:1b") || r.Desired.PullName.Contains("1.5b"),
            $"Debe seleccionar modelo < headroom. Seleccionado: {r.Desired.PullName}"
        );

        // Verificar pressure no es Insufficient
        Assert.NotEqual(ResourcePressure.Insufficient, r.Resources?.Pressure);
    }

    [Fact]
    public void Budget_StrictHeadroom_Only_0_5B_Available_Selects_Best_Fit()
    {
        // 6GB libre -> headroom 1.5GB
        // Solo 0.5B instalado, pero 1.5B (peak 1.1) cabe y es mas capaz
        // El selector prefiere el mas capaz que cabe -> 1.5B (para descargar)
        var memory = new MemoryInfo
        {
            Status = DetectionStatus.Detected,
            TotalBytes = 16L * 1024 * 1024 * 1024,
            FreeBytes = 6L * 1024 * 1024 * 1024
        };

        var assessment = new AssessmentResult
        {
            Environment = new EnvironmentProfile { Memory = memory },
            Tools = new ToolsProfile
            {
                Ollama = new OllamaStatus
                {
                    Installed = true,
                    ServerRunning = true,
                    Models = new List<ModelInfo>
                    {
                        new() { Name = "qwen2.5-coder:0.5b", SizeBytes = 397L * 1024 * 1024, Capabilities = new List<string> { "completion" } }
                    }
                }
            }
        };

        var r = ModelSelector.RecommendFromCatalog(assessment, ModelCatalog.Default, "agente");

        Assert.NotNull(r.Desired);
        // Selecciona 1.5B (mas capaz que cabe) en lugar de reutilizar 0.5B (menos capaz)
        Assert.Equal("qwen2.5-coder:1.5b", r.Desired.PullName);
        Assert.False(r.AlreadyInstalled); // Requiere descarga
    }

    [Fact]
    public void Budget_StrictHeadroom_No_Local_Models_Selects_Below_Headroom_For_Download()
    {
        // 6GB libre -> headroom 1.5GB
        var memory = new MemoryInfo
        {
            Status = DetectionStatus.Detected,
            TotalBytes = 16L * 1024 * 1024 * 1024,
            FreeBytes = 6L * 1024 * 1024 * 1024
        };

        var assessment = new AssessmentResult
        {
            Environment = new EnvironmentProfile { Memory = memory },
            Tools = new ToolsProfile
            {
                Ollama = new OllamaStatus
                {
                    Installed = true,
                    ServerRunning = true,
                    Models = new List<ModelInfo>() // Sin modelos locales
                }
            }
        };

        var r = ModelSelector.RecommendFromCatalog(assessment, ModelCatalog.Default, "agente");

        Assert.NotNull(r.Desired);
        Assert.False(r.AlreadyInstalled); // Requiere descarga
        // Debe seleccionar modelo < headroom 1.5GB (0.5B, 1B, 1.5B)
        Assert.DoesNotContain("3b", r.Desired.PullName);
        Assert.DoesNotContain("7b", r.Desired.PullName);
    }
}