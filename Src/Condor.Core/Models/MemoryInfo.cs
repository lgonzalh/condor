namespace Condor.Core.Models;

public class MemoryInfo
{
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public DetectionStatus Status { get; set; } = DetectionStatus.NotDetected;
    public string? Reason { get; set; }

    public double TotalGb => Math.Round(TotalBytes / 1024.0 / 1024.0 / 1024.0, 1);
    public double FreeGb => Math.Round(FreeBytes / 1024.0 / 1024.0 / 1024.0, 1);
}
