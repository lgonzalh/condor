using System.Collections.Generic;
using Condor.Core.Catalog;
using Condor.Core.Models;
using Condor.Core.Selection;

namespace Condor.Core.Tests;

public class ModelSelectorTests
{
    [Fact]
    public void Recommend_SinAssessment_NoSelecciona()
    {
        var r = ModelSelector.RecommendFromCatalog(null, ModelCatalog.Default);

        Assert.Null(r.Desired);
        Assert.Contains(r.Limitations, l => l.Contains("Assessment"));
    }

    [Fact]
    public void Recommend_ModeloDeseadoInstalado_ReutilizaSinDescargar()
    {
        var assessment = AssessmentConModelo("qwen2.5-coder:7b", ramFreeGb: 8, ramTotalGb: 16);

        var r = ModelSelector.RecommendFromCatalog(assessment, ModelCatalog.Default);

        Assert.NotNull(r.Desired);
        Assert.True(r.AlreadyInstalled);
        Assert.Equal("qwen2.5-coder:7b", r.InstalledName);
        Assert.Contains("reutiliza", r.Reason, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Recommend_ModeloNoInstalado_RequiereObtencion()
    {
        var assessment = AssessmentConModelo("otro-modelo", ramFreeGb: 8, ramTotalGb: 16);

        var r = ModelSelector.RecommendFromCatalog(assessment, ModelCatalog.Default);

        Assert.NotNull(r.Desired);
        Assert.False(r.AlreadyInstalled);
    }

    [Fact]
    public void Recommend_AlternativaMenosCapazNoPisaAlDeseadoMasCapaz()
    {
        // La alternativa instalada (llama3.2:3b, general) es MENOS capaz en
        // ingenieria que el deseado de mayor capacidad viable. No debe
        // reutilizarse la menos capaz si el deseado es viable y obtenible.
        var assessment = AssessmentConModelo("llama3.2:3b", ramFreeGb: 8, ramTotalGb: 16);

        var r = ModelSelector.RecommendFromCatalog(assessment, ModelCatalog.Default);

        Assert.NotNull(r.Desired);
        Assert.False(r.AlreadyInstalled);
        Assert.Equal("qwen2.5-coder:7b", r.Desired.PullName);
    }

    [Fact]
    public void Recommend_RamaInsuficiente_NoSeleccionaModeloGrande()
    {
        // Solo 1 GB libre -> ningun modelo del catalogo cabe.
        var assessment = AssessmentConModelo("vacio", ramFreeGb: 1, ramTotalGb: 2);

        var r = ModelSelector.RecommendFromCatalog(assessment, ModelCatalog.Default);

        Assert.Null(r.Desired);
        Assert.Contains(r.Limitations, l => l.Contains("compatible"));
    }

    [Fact]
    public void Recommend_Determinista_MismaEntradaMismoResultado()
    {
        var a = AssessmentConModelo("llama3.2:3b", ramFreeGb: 8, ramTotalGb: 16);

        var r1 = ModelSelector.RecommendFromCatalog(a, ModelCatalog.Default);
        var r2 = ModelSelector.RecommendFromCatalog(a, ModelCatalog.Default);

        Assert.Equal(r1.Desired?.PullName, r2.Desired?.PullName);
        Assert.Equal(r1.AlreadyInstalled, r2.AlreadyInstalled);
    }

    private static AssessmentResult AssessmentConModelo(string installedName, double ramFreeGb, double ramTotalGb)
    {
        return new AssessmentResult
        {
            Environment = new EnvironmentProfile
            {
                Memory = new MemoryInfo
                {
                    Status = DetectionStatus.Detected,
                    TotalBytes = (long)(ramTotalGb * 1024 * 1024 * 1024),
                    FreeBytes = (long)(ramFreeGb * 1024 * 1024 * 1024)
                }
            },
            Tools = new ToolsProfile
            {
                Ollama = new OllamaStatus
                {
                    Installed = true,
                    ServerRunning = true,
                    Models = new List<ModelInfo>
                    {
                        new() { Name = installedName, SizeBytes = 1024 * 1024 * 1024, Capabilities = new List<string> { "completion" } }
                    }
                }
            }
        };
    }
}
