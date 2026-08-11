namespace Condor.Core.Models;

public class ModelRecommendationResult
{
    public string Purpose { get; set; } = "development";
    public bool HasRecommendation { get; set; }
    public ModelRecommendationEntry? Recommended { get; set; }
    public List<ModelRecommendationEntry> Alternatives { get; set; } = new();
    public List<ModelRecommendationEntry> Excluded { get; set; } = new();
    public List<string> Limitations { get; set; } = new();
    public DateTime GeneratedAtUtc { get; set; }
    public ModelRecommendationInputSnapshot Inputs { get; set; } = new();
}
