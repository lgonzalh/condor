namespace Condor.Core.Models;

public class ContinuationPoint
{
    public DetectionStatus Status { get; set; } = DetectionStatus.NotDetected;
    public string? Reason { get; set; }
    public List<string> Evidence { get; set; } = new();
    public string? LastActivity { get; set; }
    public List<string> PendingWork { get; set; } = new();
    public string? SuggestedNext { get; set; }
}