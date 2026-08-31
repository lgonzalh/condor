using System.Diagnostics;
using Condor.Core.Models;
using Condor.Core.Project;
using Condor.Core.Serialization;
using Condor.Infrastructure.Project;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Condor.Infrastructure.Tests;

public class ProjectDetectorTests : IDisposable
{
    private readonly List<string> directoriosTemporales = new();

    [Fact]
    public async Task ProyectoNetBasico_SeDescubreCompleto()
    {
        var directorio = NuevoDirectorio();
        Escribir(directorio, "App.csproj", """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
          </PropertyGroup>
        </Project>
        """);
        Escribir(directorio, "README.md", "# Aplicacion");
        Escribir(directorio, "Program.cs", "class Program {}");
        Escribir(directorio, "Otro.cs", "class Otro {}");
        Escribir(directorio, "Mas.cs", "class Mas {}");

        var perfil = await new ProjectDetector().DiscoverAsync(directorio, GitTool());

        Assert.Equal(DetectionStatus.Detected, perfil.Status);
        Assert.Equal(new DirectoryInfo(directorio).Name, perfil.RootName);
        var csharp = Assert.Single(perfil.Languages);
        Assert.Equal("C#", csharp.Name);
        Assert.True(csharp.Primary);
        var manifiesto = Assert.Single(perfil.Manifests);
        Assert.Equal("csproj", manifiesto.Kind);
        Assert.Contains(perfil.Documentation, d => d.Kind == "README" && d.Path == "README.md");
        Assert.True(perfil.FilesCount >= 4);
        Assert.Empty(perfil.LimitsApplied);
    }

    [Fact]
    public async Task DirectorioVacio_PerfilValidoSinEstructura()
    {
        var directorio = NuevoDirectorio();

        var perfil = await new ProjectDetector().DiscoverAsync(directorio, GitTool());

        Assert.Equal(DetectionStatus.Detected, perfil.Status);
        Assert.Null(perfil.Reason);
        Assert.Equal(0, perfil.FilesCount);
        Assert.Equal(0, perfil.DirectoriesCount);
        Assert.Empty(perfil.Languages);
        Assert.Empty(perfil.Manifests);
        Assert.False(perfil.IsGitRepository);
    }

    [Fact]
    public async Task RutaInexistente_NotDetectedConMotivo()
    {
        var inexistente = Path.Combine(Path.GetTempPath(), "condor-no-existe-" + Guid.NewGuid().ToString("N"));

        var perfil = await new ProjectDetector().DiscoverAsync(inexistente, null);

        Assert.Equal(DetectionStatus.NotDetected, perfil.Status);
        Assert.NotNull(perfil.Reason);
        Assert.Equal(inexistente, perfil.RootPath);
    }

    [Fact]
    public async Task EstructuraDesconocida_SinLenguajesInventados()
    {
        var directorio = NuevoDirectorio();
        Escribir(directorio, "datos.txt", "dato");
        Directory.CreateDirectory(Path.Combine(directorio, "src"));
        Directory.CreateDirectory(Path.Combine(directorio, "notas"));

        var perfil = await new ProjectDetector().DiscoverAsync(directorio, GitTool());

        Assert.Equal(DetectionStatus.Detected, perfil.Status);
        Assert.Empty(perfil.Languages);
        Assert.Equal(new[] { "notas", "src" }, perfil.TopLevelDirectories);
        Assert.Equal(new[] { "datos.txt" }, perfil.TopLevelFiles);
        Assert.Equal(".txt", Assert.Single(perfil.ExtensionCounts).Name);
    }

    [Fact]
    public async Task ProyectoNodeConReact_DetectaFramework()
    {
        var directorio = NuevoDirectorio();
        Escribir(directorio, "package.json", """
        {
          "name": "interfaz",
          "version": "1.0.0",
          "dependencies": { "react": "18.2.0" }
        }
        """);
        Escribir(directorio, "main.js", "import r from 'react';");
        Escribir(directorio, "app.js", "import r from 'react';");
        Escribir(directorio, "utils.js", "import r from 'react';");

        var perfil = await new ProjectDetector().DiscoverAsync(directorio, GitTool());

        Assert.Equal(DetectionStatus.Detected, perfil.Status);
        var javaScript = Assert.Single(perfil.Languages);
        Assert.Equal("JavaScript/TypeScript", javaScript.Name);
        Assert.True(javaScript.Primary);
        var react = Assert.Single(perfil.Frameworks);
        Assert.Equal("React", react.Name);
        Assert.Equal("package.json", react.Manifest);
    }

    [Fact]
    public async Task Determinismo_DosEjecucionesIgualesExceptoGeneratedAt()
    {
        var directorio = NuevoDirectorio();
        Escribir(directorio, "package.json", """
        {
          "name": "demo",
          "dependencies": { "express": "4.0" }
        }
        """);
        Escribir(directorio, "index.js", "console.log(1);");
        Escribir(directorio, "lib.js", "module.exports = {};");
        Escribir(directorio, "util.js", "module.exports = {};");
        Escribir(directorio, "README.md", "# Demo");

        var detector = new ProjectDetector();
        var perfilA = await detector.DiscoverAsync(directorio, null);
        var perfilB = await detector.DiscoverAsync(directorio, null);

        var jsonA = Normalizar(JsonSerializer.Serialize(perfilA, AssessmentJson.Options));
        var jsonB = Normalizar(JsonSerializer.Serialize(perfilB, AssessmentJson.Options));
        Assert.Equal(jsonA, jsonB);
    }

    [Fact]
    public async Task ProyectoGrande_DeclaraLimitesAplicados()
    {
        var directorio = NuevoDirectorio();
        for (var i = 0; i < 12; i++)
        {
            Escribir(directorio, "archivo" + i + ".txt", "x");
        }

        var perfil = await new ProjectDetector(new DiscoveryLimits { MaxFiles = 10 }).DiscoverAsync(directorio, null);

        Assert.Equal(DetectionStatus.Limited, perfil.Status);
        Assert.Contains(DiscoveryLimits.LimitFiles, perfil.LimitsApplied);
        Assert.Equal(10, perfil.FilesCount);
        Assert.NotNull(perfil.Reason);
    }

    [Fact]
    public async Task E2E_RepositorioCondorReal()
    {
        if (!GitDisponible())
        {
            return;
        }

        var raiz = RaizDelRepositorio();

        var perfil = await new ProjectDetector().DiscoverAsync(raiz, GitTool());

        Assert.Equal(DetectionStatus.Detected, perfil.Status);
        Assert.Equal("condor", perfil.RootName);
        Assert.True(perfil.IsGitRepository);
        Assert.NotNull(perfil.Git);
        Assert.Empty(perfil.LimitsApplied);
        Assert.Contains(perfil.Languages, l => l.Name == "C#" && l.Primary);
        Assert.Contains(perfil.Manifests, m => m.Kind == "csproj");
        Assert.Contains(perfil.Manifests, m => m.Kind == "slnx");
    }

    [Fact]
    public async Task E2E_DirectorioSinGit_PerfilSinGit()
    {
        if (!GitDisponible())
        {
            return;
        }

        var directorio = NuevoDirectorio();
        Escribir(directorio, "a.py", "print(1)");
        Escribir(directorio, "b.py", "print(2)");
        Escribir(directorio, "c.py", "print(3)");

        var perfil = await new ProjectDetector().DiscoverAsync(directorio, GitTool());

        Assert.Equal(DetectionStatus.Detected, perfil.Status);
        Assert.False(perfil.IsGitRepository);
        Assert.Null(perfil.Git);
        var python = Assert.Single(perfil.Languages);
        Assert.Equal("Python", python.Name);
        Assert.False(python.Primary);
    }

    [Fact]
    public async Task E2E_RepositorioGitLimpio()
    {
        if (!GitDisponible())
        {
            return;
        }

        var directorio = NuevoDirectorio();
        Git(directorio, "init");
        Git(directorio, "config", "user.name", "condor tests");
        Git(directorio, "config", "user.email", "condor@tests.local");
        Git(directorio, "branch", "-M", "main");
        Escribir(directorio, "script.py", "print(1)");
        Git(directorio, "add", ".");
        Git(directorio, "commit", "-m", "cambio inicial");

        var perfil = await new ProjectDetector().DiscoverAsync(directorio, GitTool());

        Assert.Equal(DetectionStatus.Detected, perfil.Status);
        Assert.True(perfil.IsGitRepository);
        Assert.NotNull(perfil.Git);
        Assert.Equal(DetectionStatus.Detected, perfil.Git.Status);
        Assert.Equal("main", perfil.Git.Branch);
        Assert.False(perfil.Git.IsDirty);
        Assert.Single(perfil.Git.Commits);
    }

    private static ToolInfo? GitTool() { return GitDisponible() ? new ToolInfo { Name = "git", Path = "git", Status = DetectionStatus.Detected } : null; }
    private string NuevoDirectorio()
    {
        var directorio = Path.Combine(Path.GetTempPath(), "condor-t004-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directorio);
        directoriosTemporales.Add(directorio);
        return directorio;
    }

    private static void Escribir(string directorio, string nombre, string contenido)
    {
        File.WriteAllText(Path.Combine(directorio, nombre), contenido);
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

    private static string RaizDelRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null && !File.Exists(Path.Combine(directorio.FullName, "Condor.slnx")))
        {
            directorio = directorio.Parent;
        }

        Assert.NotNull(directorio);
        return directorio.FullName;
    }

    private static string Normalizar(string json)
    {
        var nodo = JsonNode.Parse(json)!.AsObject();
        nodo.Remove("generatedAtUtc");
        return nodo.ToJsonString();
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