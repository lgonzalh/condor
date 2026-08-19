using Condor.Core.Evaluation;
using Condor.Core.Models;

namespace Condor.Core.Tests;

public class ModelRecommenderTests
{
    private static readonly double Gb = 1024 * 1024 * 1024;

    private static ModelInfo Modelo(
        string name,
        double sizeGb,
        string family = "qwen2",
        List<string>? capabilities = null)
    {
        return new ModelInfo
        {
            Name = name,
            SizeBytes = (long)(sizeGb * Gb),
            Family = family,
            ParameterSize = "7B",
            Quantization = "Q4_K_M",
            ContextLength = 32768,
            Capabilities = capabilities ?? new List<string> { "completion" }
        };
    }

    private static AssessmentResult AssessmentConModelos(params ModelInfo[] models) => AssessmentConModelos(7.1, models);

    private static AssessmentResult AssessmentConModelos(double freeGb, params ModelInfo[] models)
    {
        return new AssessmentResult
        {
            Environment = new EnvironmentProfile
            {
                Memory = new MemoryInfo
                {
                    TotalBytes = (long)(15.4 * Gb),
                    FreeBytes = (long)(freeGb * Gb),
                    Status = DetectionStatus.Detected
                },
                StorageList = new List<StorageInfo> { new StorageInfo { FreeBytes = (long)(100 * Gb) } }
            },
            Tools = new ToolsProfile
            {
                Ollama = new OllamaStatus { Installed = true, ServerRunning = true, Models = models.ToList() }
            },
            Capabilities = new CapabilitiesSummary { OllamaReady = true, ModelsCount = models.Length }
        };
    }

    [Fact]
    public void Recommend_AssessmentNulo_DevuelveResultadoDegradadoSinExcepcion()
    {
        var result = new ModelRecommender().Recommend(null, "development");

        Assert.False(result.HasRecommendation);
        Assert.Null(result.Recommended);
        Assert.Empty(result.Alternatives);
        Assert.Contains(result.Limitations, limitation => limitation.Contains("Assessment"));
    }

    [Fact]
    public void Recommend_OllamaNoDisponible_DegradadoConLimitacion()
    {
        var assessment = AssessmentConModelos(Modelo("qwen-tools:7b", 4.36));
        assessment.Tools.Ollama = new OllamaStatus { Installed = true, ServerRunning = false };

        var result = new ModelRecommender().Recommend(assessment, "development");

        Assert.False(result.HasRecommendation);
        Assert.Contains(result.Limitations, limitation => limitation.Contains("Ollama"));
    }

    [Fact]
    public void Recommend_SinModelos_DegradadoConLimitacion()
    {
        var result = new ModelRecommender().Recommend(AssessmentConModelos(), "development");

        Assert.False(result.HasRecommendation);
        Assert.Contains(result.Limitations, limitation => limitation.Contains("modelos"));
    }

    [Fact]
    public void Recommend_UnUnicoModeloViable_LoRecomiendaYAdvierteSinAlternativas()
    {
        var result = new ModelRecommender().Recommend(
            AssessmentConModelos(Modelo("qwen-tools:7b", 4.36, "qwen2", new() { "completion", "tools", "insert" })),
            "development");

        Assert.True(result.HasRecommendation);
        Assert.Equal("qwen-tools:7b", result.Recommended?.Model.Name);
        Assert.Empty(result.Alternatives);
        Assert.Contains(result.Limitations, limitation => limitation.Contains("un modelo viable"));
    }

    [Fact]
    public void Recommend_ModeloDemasiadoGrandeParaRam_NoRecomiendaNada()
    {
        var result = new ModelRecommender().Recommend(
            AssessmentConModelos(Modelo("qwen3:30b", 20)),
            "development");

        Assert.False(result.HasRecommendation);
        Assert.Single(result.Excluded);
        Assert.Contains(result.Limitations, limitation => limitation.Contains("viable"));
    }

    [Fact]
    public void Recommend_ConMemoriaLimitada_ExcluyeAlMasGrande()
    {
        var coderGrande = Modelo("qwen-coder-grande:14b", 10, "qwen2", new() { "completion", "tools" });
        var genericoPequeno = Modelo("llama3.2:3b", 2, "llama", new() { "completion" });

        var result = new ModelRecommender().Recommend(
            AssessmentConModelos(coderGrande, genericoPequeno),
            "development");

        Assert.True(result.HasRecommendation);
        Assert.Equal("llama3.2:3b", result.Recommended?.Model.Name);
        Assert.Contains(result.Excluded, entry => entry.Model.Name == "qwen-coder-grande:14b");
        Assert.Contains(
            result.Excluded.Single().Reasons,
            reason => reason.Contains("supera el presupuesto"));
    }

    [Fact]
    public void Recommend_VariosModelos_PriorizaElOrientadoACodigo()
    {
        var codeA = Modelo("qwen2.5-coder:7b", 4.36);
        var generalB = Modelo("llama3.2:3b", 3.56, "llama", new() { "completion" });

        var result = new ModelRecommender().Recommend(
            AssessmentConModelos(generalB, codeA),
            "development");

        Assert.True(result.HasRecommendation);
        Assert.Equal("qwen2.5-coder:7b", result.Recommended?.Model.Name);
    }

    [Fact]
    public void Recommend_ConCapacidadesFaltantes_NoCrasheaYAdvierte()
    {
        var incompleto = new ModelInfo
        {
            Name = "sin-datos:latest",
            SizeBytes = (long)(1 * Gb)
        };

        var result = new ModelRecommender().Recommend(
            AssessmentConModelos(incompleto),
            "development");

        Assert.True(result.HasRecommendation);
        Assert.Equal("sin-datos:latest", result.Recommended?.Model.Name);
        Assert.Contains(result.Limitations, limitation => limitation.Contains("Datos incompletos"));
    }

    [Fact]
    public void Recommend_EsDeterminista()
    {
        var modelos = new[]
        {
            Modelo("qwen-tools:7b", 4.36, "qwen2", new() { "completion", "tools", "insert" }),
            Modelo("qwen2.5-coder:7b", 4.36),
            Modelo("deepseek-coder:6.7b", 3.56, "llama", new() { "completion" })
        };
        var assessment = AssessmentConModelos(modelos[2], modelos[0], modelos[1]);

        var primero = new ModelRecommender().Recommend(assessment, "development");
        var segundo = new ModelRecommender().Recommend(assessment, "development");

        Assert.Equal(primero.Recommended?.Model.Name, segundo.Recommended?.Model.Name);
        Assert.Equal(
            primero.Alternatives.Select(entry => entry.Model.Name),
            segundo.Alternatives.Select(entry => entry.Model.Name));
        Assert.Equal(
            primero.Alternatives.Select(entry => entry.Score),
            segundo.Alternatives.Select(entry => entry.Score));
    }

    [Fact]
    public void Recommend_ExplicaElResultado()
    {
        var result = new ModelRecommender().Recommend(
            AssessmentConModelos(Modelo("qwen-tools:7b", 4.36, "qwen2", new() { "completion", "tools" })),
            "development");

        Assert.NotNull(result.Recommended);
        Assert.NotEmpty(result.Recommended.Reasons);
        Assert.All(result.Recommended.Reasons, reason => Assert.False(string.IsNullOrWhiteSpace(reason)));
    }

    [Fact]
    public void Recommend_PurposeVision_SinModelosCompatibles_Degradado()
    {
        var result = new ModelRecommender().Recommend(
            AssessmentConModelos(Modelo("qwen2.5-coder:7b", 4.36)),
            "vision");

        Assert.False(result.HasRecommendation);
        Assert.Contains(result.Limitations, limitation => limitation.Contains("vision"));
    }

    [Fact]
    public void Recommend_PurposeVision_ConModeloCapaz_LoRecomienda()
    {
        var visionModel = Modelo("llama3.2-vision:11b", 3, "llama", new() { "completion", "vision" });
        var coderModel = Modelo("qwen2.5-coder:7b", 4.36);

        var result = new ModelRecommender().Recommend(
            AssessmentConModelos(coderModel, visionModel),
            "vision");

        Assert.True(result.HasRecommendation);
        Assert.Equal("llama3.2-vision:11b", result.Recommended?.Model.Name);
    }

    [Fact]
    public void Recommend_ConCatalogoReal_RecomiendaModeloCoderTools()
    {
        // Catalogo real detectado en el equipo de desarrollo (respuesta real de /api/tags).
        var modelos = new[]
        {
            Modelo("qwen-tools:7b", 4.683, "qwen2", new() { "completion", "tools", "insert" }),
            Modelo("qwen3:8b", 5.225, "qwen3", new() { "completion", "tools", "thinking" }),
            Modelo("hhao/qwen2.5-coder-tools:7b", 4.683, "qwen2", new() { "completion", "tools", "insert" }),
            Modelo("qwen2.5-coder:7b", 4.683, "qwen2", new() { "completion", "tools", "insert" }),
            Modelo("deepseek-r1:7b", 4.683, "qwen2", new() { "completion", "thinking" }),
            Modelo("deepseek-coder:6.7b", 3.827, "llama", new() { "completion" })
        };

        var result = new ModelRecommender().Recommend(
            AssessmentConModelos(7.3, modelos[3], modelos[5], modelos[1], modelos[0], modelos[4], modelos[2]),
            "development");

        Assert.True(result.HasRecommendation);
        Assert.NotNull(result.Recommended);
        Assert.Equal("hhao/qwen2.5-coder-tools:7b", result.Recommended.Model.Name);
        Assert.Equal(4, result.Alternatives.Count);
        // Con el presupuesto seguro, qwen3:8b (pico > margen) queda excluido.
        Assert.Contains(result.Excluded, e => e.Model.Name == "qwen3:8b");
        Assert.Contains(result.Alternatives, entry => entry.Model.Name != "qwen3:8b");
    }
}
