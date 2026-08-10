namespace Condor.Core.Models;

public class ProcessorInfo
{
    public string Name { get; set; } = "";
    public int Cores { get; set; }
    public int LogicalProcessors { get; set; }
    public double MaxClockMhz { get; set; }
    public DetectionStatus Status { get; set; } = DetectionStatus.NotDetected;
    public string? Reason { get; set; }
}
