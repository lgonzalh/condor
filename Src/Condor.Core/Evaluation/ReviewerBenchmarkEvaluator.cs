using Condor.Core.Models;

namespace Condor.Core.Evaluation;

public static class ReviewerBenchmarkEvaluator
{
    private static readonly Dictionary<string, int> Severity = new(StringComparer.OrdinalIgnoreCase)
    {
        ["low"] = 1,
        ["medium"] = 2,
        ["high"] = 3,
        ["critical"] = 4
    };

    public static ReviewerBenchmarkScore Score(
        IReadOnlyList<ReviewerBenchmarkCase> suite,
        ReviewerBenchmarkRun candidate,
        ReviewerBenchmarkRun? baseline = null)
    {
        var truePositives = 0;
        var falsePositives = 0;
        var falseNegatives = 0;
        var regressions = 0;

        foreach (var benchmarkCase in suite)
        {
            var current = candidate.Cases.FirstOrDefault(result => result.CaseId == benchmarkCase.Id)?.Findings ?? new List<AgentReviewFinding>();
            var baselineFindings = baseline?.Cases.FirstOrDefault(result => result.CaseId == benchmarkCase.Id)?.Findings ?? new List<AgentReviewFinding>();
            var matchedCurrent = Match(benchmarkCase.ExpectedFindings, current);
            var matchedBaseline = Match(benchmarkCase.ExpectedFindings, baselineFindings);
            var expectedCount = benchmarkCase.ExpectedFindings.Count;
            var actualCount = current.Count;
            var currentTruePositives = matchedCurrent.Count;

            truePositives += currentTruePositives;
            falseNegatives += expectedCount - currentTruePositives;
            falsePositives += actualCount - currentTruePositives;
            regressions += matchedBaseline.Count(expectedIndex => !matchedCurrent.Contains(expectedIndex));
        }

        var precision = truePositives + falsePositives == 0
            ? (double?)null
            : (double)truePositives / (truePositives + falsePositives);
        var recall = truePositives + falseNegatives == 0
            ? (double?)null
            : (double)truePositives / (truePositives + falseNegatives);
        var f1 = precision is null || recall is null || precision + recall == 0
            ? null
            : 2 * precision * recall / (precision + recall);

        return new ReviewerBenchmarkScore(
            truePositives,
            falsePositives,
            falseNegatives,
            regressions,
            precision,
            recall,
            f1);
    }

    private static HashSet<int> Match(IReadOnlyList<ReviewerBenchmarkExpectation> expected, IReadOnlyList<AgentReviewFinding> actual)
    {
        var matchedExpected = new HashSet<int>();
        var matchedActual = new HashSet<int>();
        for (var expectedIndex = 0; expectedIndex < expected.Count; expectedIndex++)
        {
            var expectation = expected[expectedIndex];
            for (var actualIndex = 0; actualIndex < actual.Count; actualIndex++)
            {
                if (matchedActual.Contains(actualIndex)) continue;
                var finding = actual[actualIndex];
                if (!SamePath(expectation.Path, finding.Path) ||
                    Math.Abs((long)expectation.Line - finding.Line) > 2 ||
                    Severity.GetValueOrDefault(finding.Severity) < Severity.GetValueOrDefault(expectation.MinimumSeverity))
                {
                    continue;
                }

                matchedExpected.Add(expectedIndex);
                matchedActual.Add(actualIndex);
                break;
            }
        }
        return matchedExpected;
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(left.Replace('\\', '/'), right.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
}
