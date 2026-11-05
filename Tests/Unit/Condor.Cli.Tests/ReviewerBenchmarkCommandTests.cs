using Condor.Cli.Commands;

namespace Condor.Cli.Tests;

public sealed class ReviewerBenchmarkCommandTests
{
    [Fact]
    public void FormatNonPerfectMessage_SuiteCompletaConFalsoPositivo_NoLaLlamaIncompleta()
    {
        var message = ReviewerBenchmarkCommand.FormatNonPerfectMessage(
            truePositives: 3,
            falsePositives: 1,
            falseNegatives: 0,
            regressions: 0,
            errors: 0);

        Assert.Contains("Benchmark completo, pero no perfecto", message);
        Assert.DoesNotContain("incompleto", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TP 3, FP 1, FN 0", message);
        Assert.Contains("No se guardó una línea base", message);
    }

    [Fact]
    public void FormatNonPerfectMessage_ConErrorDeEjecucion_SiInformaIncompleto()
    {
        var message = ReviewerBenchmarkCommand.FormatNonPerfectMessage(
            truePositives: 0,
            falsePositives: 0,
            falseNegatives: 3,
            regressions: 0,
            errors: 4);

        Assert.Contains("Benchmark incompleto por errores de ejecución", message);
        Assert.DoesNotContain("No se guardó una línea base", message);
    }

    [Fact]
    public void FormatNonPerfectMessage_CuandoSeGuardaBaselineAclaraQueNoEsAprobacion()
    {
        var message = ReviewerBenchmarkCommand.FormatNonPerfectMessage(
            truePositives: 0,
            falsePositives: 4,
            falseNegatives: 3,
            regressions: 0,
            errors: 0,
            baselineSaved: true);

        Assert.Contains("Se guardó la línea base de comparación solicitada", message);
        Assert.Contains("no como aprobación de calidad", message);
    }

    [Theory]
    [InlineData(true, false, false, false, false)]
    [InlineData(true, false, false, true, true)]
    [InlineData(false, true, false, true, false)]
    [InlineData(true, true, true, false, false)]
    [InlineData(true, false, true, true, true)]
    public void ShouldSaveBaseline_ConservaSoloCorridasCompletasYPermiteReferenciasImperfectasSolicitadas(
        bool complete,
        bool perfect,
        bool hasBaseline,
        bool updateRequested,
        bool expected)
    {
        Assert.Equal(expected, ReviewerBenchmarkCommand.ShouldSaveBaseline(complete, perfect, hasBaseline, updateRequested));
    }

    [Fact]
    public void IsBaselineScoringCompatible_PermiteCompararHashesDePromptDistintosEnLaMismaSuite()
    {
        var baseline = new Condor.Core.Models.ReviewerBenchmarkRun
        {
            SuiteVersion = "reviewer-v2",
            PromptHash = "prompt-anterior"
        };

        Assert.True(ReviewerBenchmarkCommand.IsBaselineScoringCompatible(baseline, "reviewer-v2"));
        Assert.False(ReviewerBenchmarkCommand.IsBaselineScoringCompatible(baseline, "reviewer-v3"));
    }
}
