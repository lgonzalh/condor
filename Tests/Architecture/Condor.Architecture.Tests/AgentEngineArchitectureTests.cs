namespace Condor.Architecture.Tests;

public class AgentEngineArchitectureTests
{
    [Theory]
    [InlineData("Src/Condor.Core/Agent/AgentEngine.cs")]
    [InlineData("Src/Condor.Core/Agent/AgentActionParser.cs")]
    public void AgentLogica_EsPura_NoRealizaIO(string relative)
    {
        var path = ArchivoFuente(relative);
        var content = File.ReadAllText(path);

        Assert.DoesNotContain("System.IO", content);
        Assert.DoesNotContain("File.", content);
        Assert.DoesNotContain("Directory.", content);
        Assert.DoesNotContain("HttpClient", content);
        Assert.DoesNotContain("System.Net.Http", content);
    }

    private static string ArchivoFuente(string relative)
    {
        return Path.Combine(RepoRoot(), relative);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Condor.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
