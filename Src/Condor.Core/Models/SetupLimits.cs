namespace Condor.Core.Models;

public sealed class SetupLimits
{
    public const string LimitDependencies = "setup-dependencies";
    public const string LimitTimeout = "setup-timeout";

    public int MaxDependencies { get; init; } = 12;
    public int SetupTimeoutMilliseconds { get; init; } = 15_000;

    public static SetupLimits Default { get; } = new();
}
