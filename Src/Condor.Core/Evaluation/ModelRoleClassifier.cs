using Condor.Core.Models;

namespace Condor.Core.Evaluation;

public static class ModelRoleClassifier
{
    public static double DevelopmentScore(ModelInfo model)
    {
        var name = (model.Name ?? "").ToLowerInvariant();
        var family = (model.Family ?? "").ToLowerInvariant();
        var capabilities = model.Capabilities ?? new List<string>();

        double score = 0;

        if (name.Contains("coder") || name.Contains("-code")) score += 0.6;
        if (name.Contains("tools")) score += 0.3;
        if (capabilities.Contains("tools", StringComparer.OrdinalIgnoreCase)) score += 0.25;
        if (capabilities.Contains("insert", StringComparer.OrdinalIgnoreCase)) score += 0.15;
        if (name.Contains("r1") || name.Contains("reasoning") || family.Contains("deepseek")) score += 0.15;
        if (capabilities.Contains("vision", StringComparer.OrdinalIgnoreCase)) score += 0.05;

        return Math.Min(score, 1.0);
    }

    public static bool HasCapability(ModelInfo model, string capability)
    {
        return (model.Capabilities ?? new List<string>()).Contains(capability, StringComparer.OrdinalIgnoreCase);
    }

    public static bool HasVision(ModelInfo model) => HasCapability(model, "vision");
}
