using Condor.Core.Models;

namespace Condor.Core.Evaluation;

public static class LlmModelSelector
{
    public static string? Select(AssessmentResult? assessment, string? explicitModel)
    {
        if (!string.IsNullOrWhiteSpace(explicitModel))
        {
            return explicitModel;
        }

        return assessment?.Tools?.Ollama?.Models?.FirstOrDefault()?.Name;
    }
}
