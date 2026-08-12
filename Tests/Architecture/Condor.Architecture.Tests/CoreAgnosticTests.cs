namespace Condor.Architecture.Tests;

public class CoreAgnosticTests
{
    [Fact]
    public void CondorCore_NoUsaSystemNetHttp()
    {
        var archivos = ArchivosFuente("Src/Condor.Core");

        foreach (var file in archivos)
        {
            var content = File.ReadAllText(file);

            Assert.DoesNotContain("System.Net.Http", content);
            Assert.DoesNotContain("HttpClient", content);
        }
    }

    [Fact]
    public void CondorCore_NoHaceReferenciaAlProcesoNiAlSistemaOperativo()
    {
        var archivos = ArchivosFuente("Src/Condor.Core");

        foreach (var file in archivos)
        {
            var content = File.ReadAllText(file);

            Assert.DoesNotContain("System.Diagnostics.Process", content);
            Assert.DoesNotContain("powershell", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("RuntimeInformation", content);
        }
    }

    [Fact]
    public void CondorCore_NoAccedeDirectamenteAlSistemaDeArchivos()
    {
        var archivos = ArchivosFuente("Src/Condor.Core");

        foreach (var file in archivos)
        {
            var content = File.ReadAllText(file);

            Assert.DoesNotContain("System.IO.File", content);
            Assert.DoesNotContain("System.IO.Directory", content);
            Assert.DoesNotContain("FileInfo", content);
            Assert.DoesNotContain("DirectoryInfo", content);
            Assert.DoesNotContain("StreamReader", content);
            Assert.DoesNotContain("StreamWriter", content);
            Assert.DoesNotContain("FileStream", content);
            Assert.DoesNotContain("DriveInfo", content);
        }
    }

    private static string[] ArchivosFuente(string relativePath)
    {
        var directory = RepoRoot(relativePath);

        return Directory
            .GetFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(archivo => !archivo.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase)
                           && !archivo.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase))
            .ToArray();
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