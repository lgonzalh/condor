namespace Condor.Core.Project;

public sealed class DiscoveryLimits
{
    public const string LimitDepth = "max-depth";
    public const string LimitDirectories = "max-directories";
    public const string LimitFiles = "max-files";
    public const string LimitManifestSize = "manifest-size";
    public const string LimitTotalSize = "max-total-size";
    public const string LimitTimeout = "timeout";
    public const string LimitManifests = "max-manifests";
    public const string LimitDependencies = "dependencies";

    public int MaxDepth { get; init; } = 6;
    public int MaxDirectories { get; init; } = 2000;
    public int MaxFiles { get; init; } = 10000;
    public int MaxManifestBytes { get; init; } = 64 * 1024;
    public long MaxTotalSizeBytes { get; init; } = 2L * 1024 * 1024 * 1024;
    public int DiscoveryTimeoutMilliseconds { get; init; } = 30_000;
    public int GitOperationTimeoutMilliseconds { get; init; } = 10_000;
    public int MaxManifests { get; init; } = 50;
    public int MaxDependencies { get; init; } = 100;
    public int MaxGitCommits { get; init; } = 5;
    public int MaxCommitSubjectLength { get; init; } = 80;
    public int CommitHashLength { get; init; } = 8;

    public static DiscoveryLimits Default { get; } = new();
}