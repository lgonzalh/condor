namespace Condor.Core.Models;

public class EnvironmentProfile
{
    public OperatingSystemInfo Os { get; set; } = new();
    public ProcessorInfo Cpu { get; set; } = new();
    public MemoryInfo Memory { get; set; } = new();
    public List<GpuInfo> GpuList { get; set; } = new();
    public DetectionStatus GpuStatus { get; set; } = DetectionStatus.NotDetected;
    public string? GpuReason { get; set; }
    public List<StorageInfo> StorageList { get; set; } = new();
    public DetectionStatus StorageStatus { get; set; } = DetectionStatus.NotDetected;
    public string? StorageReason { get; set; }
}
