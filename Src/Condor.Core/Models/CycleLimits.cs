namespace Condor.Core.Models;

public sealed class CycleLimits
{
    public const string LimitIterations = "cycle-iterations";
    public const string LimitStages = "cycle-stages";
    public const string LimitTimeout = "cycle-timeout";

    public int MaxIterations { get; init; } = 3;
    public int MaxStages { get; init; } = 3;
    public int CycleTimeoutMilliseconds { get; init; } = 20_000;

    public static CycleLimits Default { get; } = new();
}
