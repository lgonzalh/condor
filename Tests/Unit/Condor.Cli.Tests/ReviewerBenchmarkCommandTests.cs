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
    public void SelectCompatibleBaseline_ExigeMismaVersionYHashDePrompt()
    {
        var baseline = new Condor.Core.Models.ReviewerBenchmarkRun
        {
            SuiteVersion = "reviewer-v2",
            PromptHash = "prompt-anterior",
            Model = "modelo-base"
        };

        Assert.Null(ReviewerBenchmarkCommand.SelectCompatibleBaseline(baseline, "reviewer-v2", "prompt-nuevo"));
        Assert.Null(ReviewerBenchmarkCommand.SelectCompatibleBaseline(baseline, "reviewer-v3", "prompt-anterior"));
        Assert.Same(baseline, ReviewerBenchmarkCommand.SelectCompatibleBaseline(baseline, "reviewer-v2", "prompt-anterior"));
    }
}
