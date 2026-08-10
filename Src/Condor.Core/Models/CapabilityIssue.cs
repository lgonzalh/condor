namespace Condor.Core.Models;

public class CapabilityIssue
{
    public string Capability { get; set; } = "";
    public DetectionStatus Status { get; set; } = DetectionStatus.NotDetected;
    public string Reason { get; set; } = "";
}
