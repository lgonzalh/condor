namespace Condor.Architecture.Tests;

public class SolutionTests
{
    [Fact]
    public void CondorSlnx_ContieneLosProyectosEsperados()
    {
        var solutionPath = RepoRoot("Condor.slnx");
        var content = File.ReadAllText(solutionPath);

        Assert.Contains("Src/Condor.Cli/Condor.Cli.csproj", content);
        Assert.Contains("Src/Condor.Core/Condor.Core.csproj", content);
        Assert.Contains("Src/Condor.Infrastructure/Condor.Infrastructure.csproj", content);
        Assert.Contains("Tests/Unit/Condor.Core.Tests/Condor.Core.Tests.csproj", content);
        Assert.Contains("Tests/Integration/Condor.Infrastructure.Tests/Condor.Infrastructure.Tests.csproj", content);
        Assert.Contains("Tests/Architecture/Condor.Architecture.Tests/Condor.Architecture.Tests.csproj", content);
    }

    private static string RepoRoot(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Condor.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, relativePath);
    }
}
