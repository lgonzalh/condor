namespace Condor.Architecture.Tests;

public class VerifyEngineArchitectureTests
{
    [Theory]
    [InlineData("Verifier.cs")]
    public void VerifyLogica_EsPura_NoRealizaIO(string fileName)
    {
        var path = ArchivoFuente("Src/Condor.Core/Verification", fileName);
        var content = File.ReadAllText(path);

        Assert.DoesNotContain("System.IO", content);
        Assert.DoesNotContain("File.", content);
        Assert.DoesNotContain("Directory.", content);
        Assert.DoesNotContain("HttpClient", content);
        Assert.DoesNotContain("System.Net.Http", content);
    }

    private static string ArchivoFuente(string relativeFolder, string fileName)
    {
        return Path.Combine(RepoRoot(relativeFolder), fileName);
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
