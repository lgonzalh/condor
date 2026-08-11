using Condor.Core.Models;

namespace Condor.Core.Models;

public class ModelRecommendationEntry
{
    public ModelInfo Model { get; set; } = new();
    public double Score { get; set; }
    public List<string> Reasons { get; set; } = new();
}
