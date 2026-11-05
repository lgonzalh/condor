namespace Condor.Core.Models;

public sealed record ReviewerBenchmarkExpectation(string Path, int Line, string MinimumSeverity);

public sealed record ReviewerBenchmarkCase(
    string Id,
    string RelativePath,
    string Source,
    IReadOnlyList<ReviewerBenchmarkExpectation> ExpectedFindings);

public sealed class ReviewerBenchmarkCaseResult
{
    public string CaseId { get; set; } = string.Empty;
    public List<AgentReviewFinding> Findings { get; set; } = new();
    public string? Error { get; set; }
}

public sealed class ReviewerBenchmarkRun
{
    public int SchemaVersion { get; set; } = 1;
    public string SuiteVersion { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string PromptHash { get; set; } = string.Empty;
    public DateTimeOffset ExecutedAtUtc { get; set; }
    public List<ReviewerBenchmarkCaseResult> Cases { get; set; } = new();
    public ReviewerBenchmarkScore? Score { get; set; }
    public string? BaselineModel { get; set; }
    public string? BaselinePromptHash { get; set; }
}

public sealed record ReviewerBenchmarkScore(
    int TruePositives,
    int FalsePositives,
    int FalseNegatives,
    int Regressions,
    double? Precision,
    double? Recall,
    double? F1);
