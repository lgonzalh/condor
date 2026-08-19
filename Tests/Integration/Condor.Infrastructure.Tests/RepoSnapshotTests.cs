using Condor.Infrastructure.Agent;

namespace Condor.Infrastructure.Tests;

public class RepoSnapshotTests
{
    [Fact]
    public void ChangedTestFiles_DetectaModificacionDePruebas()
    {
        var root = TempDir();
        Directory.CreateDirectory(Path.Combine(root, "Calc.Tests"));
        File.WriteAllText(Path.Combine(root, "Calc.Tests", "Tests.cs"), "using Xunit;\nclass T { [Fact] void Add() => Assert.Equal(7, X.Add(3,4)); }\n");

        var snapshot = RepoSnapshot.Capture(root);

        File.WriteAllText(Path.Combine(root, "Calc.Tests", "Tests.cs"), "using Xunit;\nclass T { [Fact] void Add() => Assert.Equal(-1, X.Add(3,4)); }\n");

        var changed = snapshot.ChangedTestFiles(root);

        Assert.Contains(changed, p => p.Contains("Tests.cs", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ChangedTestFiles_SinCambios_DevuelveVacio()
    {
        var root = TempDir();
        Directory.CreateDirectory(Path.Combine(root, "Calc.Tests"));
        File.WriteAllText(Path.Combine(root, "Calc.Tests", "Tests.cs"), "using Xunit;\nclass T { }\n");

        var snapshot = RepoSnapshot.Capture(root);
        var changed = snapshot.ChangedTestFiles(root);

        Assert.Empty(changed);
    }

    [Fact]
    public void Capture_IgnoraBinYObj()
    {
        var root = TempDir();
        Directory.CreateDirectory(Path.Combine(root, "bin"));
        Directory.CreateDirectory(Path.Combine(root, "obj"));
        File.WriteAllText(Path.Combine(root, "bin", "out.cs"), "x");
        File.WriteAllText(Path.Combine(root, "obj", "tmp.cs"), "y");
        File.WriteAllText(Path.Combine(root, "Real.cs"), "z");

        Assert.Empty(RepoSnapshot.Capture(root).ChangedTestFiles(root));
    }

    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "condor-snapshot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
