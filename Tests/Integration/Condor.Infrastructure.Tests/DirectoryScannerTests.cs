using Condor.Infrastructure.Project;
using System.Diagnostics;
using Condor.Core.Project;

namespace Condor.Infrastructure.Tests;

public class DirectoryScannerTests : IDisposable
{
    private readonly List<string> directoriosTemporales = new();

    [Fact]
    public void EscaneoBasico_ConteosTamanoYExtensiones()
    {
        var root = NuevoDirectorio();
        Escribir(root, "a.txt", 10);
        Escribir(root + "\\sub", "b.txt", 20);
        Escribir(root + "\\sub\\deep", "c.txt", 30);
        Directory.CreateDirectory(Path.Combine(root, "vacio"));
        Escribir(root + "\\.git", "x.txt", 100);
        Escribir(root + "\\node_modules\\pkg", "y.txt", 100);
        Escribir(root + "\\bin", "z.bin", 100);
        Escribir(root + "\\obj", "w.obj", 100);
        Escribir(root + "\\dist", "v.dll", 100);
        Escribir(root + "\\build", "u.dll", 100);
        Escribir(root + "\\.vs", "t.dat", 100);

        var scan = new DirectoryScanner().Scan(root);

        Assert.Equal(3, scan.Files.Count);
        Assert.Equal(3, scan.Directories.Count);
        Assert.Equal(60, scan.TotalSizeBytes);
        Assert.Equal(3, scan.ExtensionCounts[".txt"]);
        Assert.False(scan.TotalSizeExceeded);
        Assert.Empty(scan.LimitsApplied);
        Assert.Empty(scan.Degradations);
    }

    [Fact]
    public void Exclusiones_SeAplicanEnCualquierNivel()
    {
        var root = NuevoDirectorio();
        Escribir(root + "\\a\\b\\node_modules", "f.txt", 10);
        Escribir(root + "\\a\\b", "g.txt", 10);

        var scan = new DirectoryScanner().Scan(root);

        Assert.Single(scan.Files);
        Assert.Equal(new[] { "a", "a/b" }, scan.Directories.Select(d => d.RelativePath));
    }

    [Fact]
    public void ReparsePoint_NoSeDesciendePeroSeRegistra()
    {
        var root = NuevoDirectorio();
        var destino = NuevoDirectorio();
        Escribir(destino, "f.txt", 10);

        var enlace = Path.Combine(root, "enlace");
        if (!CrearJunction(enlace, destino))
        {
            return;
        }

        var scan = new DirectoryScanner().Scan(root);

        Assert.Equal(new[] { "enlace" }, scan.Directories.Select(d => d.RelativePath));
        Assert.True(scan.Directories[0].IsReparsePoint);
        Assert.Empty(scan.Files);
    }

    [Fact]
    public void ProfundidadMaxima_LosArchivosDelNivelSeisSeCuentan()
    {
        var root = NuevoDirectorio();
        Escribir(root + "\\d1\\d2\\d3\\d4\\d5", "f.txt", 10);
        Escribir(root + "\\d1\\d2\\d3\\d4\\d5\\d6", "fuera.txt", 10);

        var scan = new DirectoryScanner().Scan(root);

        Assert.Single(scan.Files);
        Assert.Equal(6, scan.Directories.Count);
        Assert.Equal("d1/d2/d3/d4/d5/f.txt", scan.Files[0].RelativePath);
    }

    [Fact]
    public void TopeDeArchivos_DetieneYDeclaraElLimite()
    {
        var root = NuevoDirectorio();
        for (var i = 0; i < 6; i++)
        {
            Escribir(root, "archivo" + i + ".txt", 1);
        }

        var scan = new DirectoryScanner(new DiscoveryLimits { MaxFiles = 5 }).Scan(root);

        Assert.Equal(5, scan.Files.Count);
        Assert.True(scan.Stopped);
        Assert.Contains(DiscoveryLimits.LimitFiles, scan.LimitsApplied);
    }

    [Fact]
    public void TopeDeDirectorios_DetieneYDeclaraElLimite()
    {
        var root = NuevoDirectorio();
        for (var i = 0; i < 4; i++)
        {
            Directory.CreateDirectory(Path.Combine(root, "carpeta" + i));
        }

        var scan = new DirectoryScanner(new DiscoveryLimits { MaxDirectories = 3 }).Scan(root);

        Assert.True(scan.Directories.Count <= 3);
        Assert.True(scan.Stopped);
        Assert.Contains(DiscoveryLimits.LimitDirectories, scan.LimitsApplied);
    }

    [Fact]
    public void DirectorioVacio_EscaneoValidoSinSenales()
    {
        var root = NuevoDirectorio();

        var scan = new DirectoryScanner().Scan(root);

        Assert.Empty(scan.Files);
        Assert.Empty(scan.Directories);
        Assert.Equal(0, scan.TotalSizeBytes);
        Assert.Empty(scan.LimitsApplied);
        Assert.Empty(scan.Degradations);
    }

    [Fact]
    public void TopeDeTamanoTotal_SeMarcaYSeDejaDeSumar()
    {
        var root = NuevoDirectorio();
        Escribir(root, "a.txt", 60);
        Escribir(root, "b.txt", 60);

        var scan = new DirectoryScanner(new DiscoveryLimits { MaxTotalSizeBytes = 100 }).Scan(root);

        Assert.True(scan.TotalSizeExceeded);
        Assert.Equal(60, scan.TotalSizeBytes);
        Assert.Contains(DiscoveryLimits.LimitTotalSize, scan.LimitsApplied);
    }

    private string NuevoDirectorio()
    {
        var directorio = Path.Combine(Path.GetTempPath(), "condor-t004-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directorio);
        directoriosTemporales.Add(directorio);
        return directorio;
    }

    private static void Escribir(string directorio, string nombre, int bytes)
    {
        Directory.CreateDirectory(directorio);
        File.WriteAllBytes(Path.Combine(directorio, nombre), new byte[bytes]);
    }

    private static bool CrearJunction(string enlace, string destino)
    {
        try
        {
            var info = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            info.ArgumentList.Add("/c");
            info.ArgumentList.Add("mklink");
            info.ArgumentList.Add("/J");
            info.ArgumentList.Add(enlace);
            info.ArgumentList.Add(destino);
            using var proceso = Process.Start(info);
            if (proceso is null)
            {
                return false;
            }

            proceso.WaitForExit(10000);
            return proceso.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        foreach (var directorio in directoriosTemporales)
        {
            try
            {
                Directory.Delete(directorio, true);
            }
            catch
            {
                // Directorio en uso o ya eliminado.
            }
        }
    }
}