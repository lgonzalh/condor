namespace Condor.Core.Models;

public class OperatingSystemInfo
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Build { get; set; } = "";
    public string Architecture { get; set; } = "";
    public DetectionStatus Status { get; set; } = DetectionStatus.NotDetected;
    public string? Reason { get; set; }
}
