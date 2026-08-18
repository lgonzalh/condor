namespace Condor.Core.Models;

public sealed class ModelSetupLimits
{
    public const string LimitTimeout = "model-setup-timeout";
    public const string LimitAttempts = "model-setup-attempts";
    public const string LimitCatalog = "model-setup-catalog";

    public int MaxPullAttempts { get; init; } = 3;
    public int PullTimeoutMilliseconds { get; init; } = 300_000;
    public int MaxCandidates { get; init; } = 6;

    public static ModelSetupLimits Default { get; } = new();
}
