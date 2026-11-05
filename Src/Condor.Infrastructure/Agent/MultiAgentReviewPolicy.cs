namespace Condor.Infrastructure.Agent;

/// <summary>Defines the independent reviewer roles required for an explicit multi-agent review.</summary>
public static class MultiAgentReviewPolicy
{
    public static bool IsRequested(string? intention)
    {
        var request = intention ?? string.Empty;
        return request.Contains("revisión multiagente", StringComparison.OrdinalIgnoreCase) ||
            request.Contains("revision multiagente", StringComparison.OrdinalIgnoreCase) ||
            request.Contains("revisión con varios revisores", StringComparison.OrdinalIgnoreCase) ||
            request.Contains("revision con varios revisores", StringComparison.OrdinalIgnoreCase) ||
            request.Contains("multi-agent review", StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasIndependentReviewers(IEnumerable<string?> profileIds)
    {
        var reviewers = profileIds
            .Where(profileId => !string.IsNullOrWhiteSpace(profileId))
            .Select(profileId => profileId!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return reviewers.Contains("reviewer") && reviewers.Contains("security-reviewer");
    }
}
