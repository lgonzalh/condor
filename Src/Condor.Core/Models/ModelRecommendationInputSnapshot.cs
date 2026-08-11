namespace Condor.Core.Models;

public class ModelRecommendationInputSnapshot
{
    public double RamTotalGb { get; set; }
    public double RamFreeGb { get; set; }
    public double StorageFreeGb { get; set; }
    public bool GpuDetected { get; set; }
    public bool OllamaReady { get; set; }
    public int ModelsCount { get; set; }
}
