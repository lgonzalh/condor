using System.Diagnostics;
using Condor.Core.Models;
using Condor.Infrastructure.Project;

namespace Condor.Infrastructure.Tests;

public class GitRepositoryProbeTests : IDisposable
{
    private readonly List<string> directoriosTemporales = new();

    [Fact]
    public async Task SinRepositorio_DevuelveNull()
    {
        if (!GitDisponible())
        {
            return;
        }

        var directorio = NuevoDirectorio();
        File.WriteAllText(Path.Combine(directorio, "archivo.txt"), "contenido");

        var resultado = await new GitRepositoryProbe().ProbeAsync(directorio, "git");

        Assert.Null(resultado.State);
        Assert.False(resultado.CouldNotVerify);
    }

    [Fact]
    public async Task RepositorioLimpio_ReportaRamaYCambios()
    {
        if (!GitDisponible())
        {
            return;
        }

        var directorio = InicializarRepositorio();
        ConfirmarCambio(directorio, "app.py", "print(1)\n", "primer cambio");

        var resultado = await new GitRepositoryProbe().ProbeAsync(directorio, "git");

        Assert.NotNull(resultado.State);
        Assert.Equal(DetectionStatus.Detected, resultado.State.Status);
        Assert.Equal("main", resultado.State.Branch);
        Assert.False(resultado.State.IsDirty);
        var cambio = Assert.Single(resultado.State.Commits);
        Assert.Equal(8, cambio.Hash.Length);
        Assert.Equal("primer cambio", cambio.Subject);
    }

    [Fact]
    public async Task RepositorioSucio_ReportaEstadoSucio()
    {
        if (!GitDisponible())
        {
            return;
        }

        var directorio = InicializarRepositorio();
        ConfirmarCambio(directorio, "app.py", "print(1)\n", "primer cambio");
        File.WriteAllText(Path.Combine(directorio, "app.py"), "print(2)\n");

        var resultado = await new GitRepositoryProbe().ProbeAsync(directorio, "git");

        Assert.NotNull(resultado.State);
        Assert.True(resultado.State.IsDirty);
    }

    [Fact]
    public async Task RepositorioSinCommits_NoReportaError()
    {
        if (!GitDisponible())
        {
            return;
        }

        var directorio = InicializarRepositorio();

        var resultado = await new GitRepositoryProbe().ProbeAsync(directorio, "git");

        Assert.NotNull(resultado.State);
        Assert.Equal(DetectionStatus.Detected, resultado.State.Status);
        Assert.Equal("main", resultado.State.Branch);
        Assert.False(resultado.State.IsDirty);
        Assert.Empty(resultado.State.Commits);
    }

    [Fact]
    public async Task HeadSeparado_BranchNuloSinError()
    {
        if (!GitDisponible())
        {
            return;
        }

        var directorio = InicializarRepositorio();
        ConfirmarCambio(directorio, "app.py", "print(1)\n", "primer cambio");
        Git(directorio, "checkout", "--detach");

        var resultado = await new GitRepositoryProbe().ProbeAsync(directorio, "git");

        Assert.NotNull(resultado.State);
        Assert.Equal(DetectionStatus.Detected, resultado.State.Status);
        Assert.Null(resultado.State.Branch);
        Assert.Single(resultado.State.Commits);
    }

    private string NuevoDirectorio()
    {
        var directorio = Path.Combine(Path.GetTempPath(), "condor-t004-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directorio);
        directoriosTemporales.Add(directorio);
        return directorio;
    }

    private string InicializarRepositorio()
    {
        var directorio = NuevoDirectorio();
        Git(directorio, "init");
        Git(directorio, "config", "user.name", "condor tests");
        Git(directorio, "config", "user.email", "condor@tests.local");
        Git(directorio, "branch", "-M", "main");
        return directorio;
    }

    private static void ConfirmarCambio(string directorio, string nombre, string contenido, string mensaje)
    {
        File.WriteAllText(Path.Combine(directorio, nombre), contenido);
        Git(directorio, "add", ".");
        Git(directorio, "commit", "-m", mensaje);
    }

    private static void Git(string directorio, params string[] argumentos)
    {
        var info = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = directorio,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argumento in argumentos)
        {
            info.ArgumentList.Add(argumento);
        }

        using var proceso = Process.Start(info);
        Assert.NotNull(proceso);
        proceso.WaitForExit(20000);
        Assert.Equal(0, proceso.ExitCode);
    }

    private static bool GitDisponible()
    {
        try
        {
            using var proceso = Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            Assert.NotNull(proceso);
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