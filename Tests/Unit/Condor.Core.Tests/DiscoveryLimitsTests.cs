using Condor.Core.Project;

namespace Condor.Core.Tests;

public class DiscoveryLimitsTests
{
    [Fact]
    public void ValoresPredeterminados_CoincidenConLosAprobados()
    {
        var limits = DiscoveryLimits.Default;

        Assert.Equal(6, limits.MaxDepth);
        Assert.Equal(2000, limits.MaxDirectories);
        Assert.Equal(10000, limits.MaxFiles);
        Assert.Equal(64 * 1024, limits.MaxManifestBytes);
        Assert.Equal(2L * 1024 * 1024 * 1024, limits.MaxTotalSizeBytes);
        Assert.Equal(30_000, limits.DiscoveryTimeoutMilliseconds);
        Assert.Equal(10_000, limits.GitOperationTimeoutMilliseconds);
        Assert.Equal(50, limits.MaxManifests);
        Assert.Equal(100, limits.MaxDependencies);
        Assert.Equal(5, limits.MaxGitCommits);
        Assert.Equal(80, limits.MaxCommitSubjectLength);
        Assert.Equal(8, limits.CommitHashLength);
    }
}