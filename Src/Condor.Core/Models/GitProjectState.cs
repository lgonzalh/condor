namespace Condor.Core.Models;

public class GitProjectState
{
    public string? Branch { get; set; }
    public bool IsDirty { get; set; }
    public List<GitCommitSummary> Commits { get; set; } = new();
    public DetectionStatus Status { get; set; } = DetectionStatus.Detected;
    public string? Reason { get; set; }
}