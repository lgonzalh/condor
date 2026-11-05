using System.Text.Json;
using Condor.Core.Evaluation;
using Condor.Core.Models;
using Condor.Infrastructure.Agent;
using Condor.Core.Contracts;

namespace Condor.Cli.Commands;

public static class ReviewerBenchmarkCommand
{
    public static async Task<int> ExecuteAsync(
        ILlmClient llm,
        string model,
        string workingDirectory,
        string[] args,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            Console.Error.WriteLine("No hay un modelo activo para ejecutar el benchmark del revisor.");
            return 2;
        }

        var directory = Path.Combine(Path.GetFullPath(workingDirectory), ".condor", "reviewer-benchmark");
        var latestPath = Path.Combine(directory, "latest.json");
        var baselinePath = Path.Combine(directory, "baseline.json");
        ReviewerBenchmarkRun? baseline = null;
        if (File.Exists(baselinePath))
        {
            try
            {
                baseline = JsonSerializer.Deserialize<ReviewerBenchmarkRun>(
                    await File.ReadAllTextAsync(baselinePath, cancellationToken).ConfigureAwait(false),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (!IsBaselineScoringCompatible(baseline, ReviewerBenchmarkSuite.Version)) baseline = null;
            }
            catch (JsonException)
            {
                baseline = null;
            }
            catch (IOException)
            {
                baseline = null;
            }
        }

        var run = await new ReviewerBenchmarkRunner(llm)
            .RunAsync(model, Console.WriteLine, cancellationToken)
            .ConfigureAwait(false);
        run.BaselineModel = baseline?.Model;
        run.BaselinePromptHash = baseline?.PromptHash;
        var score = ReviewerBenchmarkEvaluator.Score(ReviewerBenchmarkSuite.Cases, run, baseline);
        run.Score = score;

        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(run, new JsonSerializerOptions { WriteIndented = true });
        var timestamp = run.ExecutedAtUtc.ToString("yyyyMMdd-HHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var reportPath = Path.Combine(directory, "run-" + timestamp + ".json");
        await File.WriteAllTextAsync(reportPath, json, cancellationToken).ConfigureAwait(false);
        var temporaryPath = latestPath + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, json, cancellationToken).ConfigureAwait(false);
        File.Move(temporaryPath, latestPath, overwrite: true);
        var hasCompleteRun = run.Cases.All(result => result.Error is null);
        var hasPerfectScore = hasCompleteRun && score.FalseNegatives == 0 && score.FalsePositives == 0;
        var updateBaselineRequested = args.Contains("--actualizar-base", StringComparer.OrdinalIgnoreCase);
        var saveBaseline = ShouldSaveBaseline(hasCompleteRun, hasPerfectScore, baseline is not null, updateBaselineRequested);
        if (saveBaseline)
        {
            var baselineTemporaryPath = baselinePath + ".tmp";
            await File.WriteAllTextAsync(baselineTemporaryPath, json, cancellationToken).ConfigureAwait(false);
            File.Move(baselineTemporaryPath, baselinePath, overwrite: true);
        }

        if (hasPerfectScore)
        {
            Console.WriteLine(reportPath);
            return 0;
        }

        var errors = run.Cases.Count(result => result.Error is not null);
        Console.Error.WriteLine(FormatNonPerfectMessage(
            score.TruePositives,
            score.FalsePositives,
            score.FalseNegatives,
            score.Regressions,
            errors,
            baselineSaved: saveBaseline));
        Console.Error.WriteLine(reportPath);
        return 1;
    }

    internal static string FormatNonPerfectMessage(
        int truePositives,
        int falsePositives,
        int falseNegatives,
        int regressions,
        int errors,
        bool baselineSaved = false)
    {
        var resultStatus = errors > 0 ? "incompleto por errores de ejecución" : "completo, pero no perfecto";
        var message = $"Benchmark {resultStatus}: TP {truePositives}, FP {falsePositives}, FN {falseNegatives}, regresiones {regressions}, errores {errors}.";
        return errors > 0
            ? message
            : message + Environment.NewLine + (baselineSaved
                ? "Se guardó la línea base de comparación solicitada; conserva este puntaje como referencia, no como aprobación de calidad."
                : "No se guardó una línea base porque el resultado no fue perfecto.");
    }

    internal static bool ShouldSaveBaseline(bool complete, bool perfect, bool hasBaseline, bool updateRequested) =>
        complete && (updateRequested || (perfect && !hasBaseline));

    internal static bool IsBaselineScoringCompatible(ReviewerBenchmarkRun? baseline, string currentSuiteVersion) =>
        baseline is not null && string.Equals(baseline.SuiteVersion, currentSuiteVersion, StringComparison.Ordinal);
}
