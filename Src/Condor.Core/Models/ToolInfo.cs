namespace Condor.Core.Models;

public class ToolInfo
{
    public string Name { get; set; } = "";
    public string? Version { get; set; }
    public string? Path { get; set; }
    public DetectionStatus Status { get; set; } = DetectionStatus.NotDetected;
    public string? Reason { get; set; }
}
