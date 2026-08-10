namespace Condor.Core.Models;

public class CapabilitiesSummary
{
    public bool LocalLlm { get; set; }
    public bool GpuDetected { get; set; }
    public bool VisionCapable { get; set; }
    public bool OllamaReady { get; set; }
    public int DetectedToolsCount { get; set; }
    public int ModelsCount { get; set; }
    public List<CapabilityIssue> Issues { get; set; } = new();
}
