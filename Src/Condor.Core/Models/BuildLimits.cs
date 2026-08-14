namespace Condor.Core.Models;

public sealed class BuildLimits
{
    public const string LimitActions = "build-actions";
    public const string LimitContent = "build-content";
    public const string LimitPath = "build-path";
    public const string LimitTimeout = "build-timeout";

    public int MaxActions { get; init; } = 24;
    public int MaxContentLength { get; init; } = 64_000;
    public int MaxRelativePathLength { get; init; } = 260;
    public int BuildTimeoutMilliseconds { get; init; } = 15_000;

    public static BuildLimits Default { get; } = new();
}
