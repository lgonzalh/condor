using Condor.Core.Context;
using Condor.Core.Models;
using Condor.Infrastructure.Context;
using Condor.Infrastructure.State;

namespace Condor.Infrastructure.Tests;

public class ContextServiceTests
{
    [Fact]
    public async Task BuildContextAsync_SinAssessment_DevuelveNotDetectedSinExcepcion()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        var service = new ContextService(store);

        var context = await service.BuildContextAsync(CancellationToken.None);

        Assert.Equal(DetectionStatus.NotDetected, context.Status);
        Assert.Contains("condor analizar", context.Reason!);
        Assert.Null(context.ContinuationPoint);
    }

    [Fact]
    public async Task BuildContextAsync_ConAssessmentSinProyecto_DevuelveContextoParcial()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        await store.SaveAssessmentAsync(new AssessmentResult
        {
            SchemaVersion = "1.0.0",
            WorkingDirectory = "C:\\proyecto",
            Project = null
        });
        var service = new ContextService(store);

        var context = await service.BuildContextAsync(CancellationToken.None);

        Assert.Equal(DetectionStatus.Detected, context.Status);
        Assert.Equal("1.0.0", context.SchemaVersion);
        Assert.Empty(context.Risks);
        Assert.Equal(DetectionStatus.NotDetected, context.ContinuationPoint!.Status);
    }

    [Fact]
    public async Task BuildContextAsync_ConProyectoYOperacion_ReconstruyeContextoReal()
    {
        var projectDirectory = DirectorioTemporal();
        Directory.CreateDirectory(Path.Combine(projectDirectory, "operacion"));
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "operacion", "KANBAN.md"),
            "# KANBAN\n\n## Siguiente\n\nT-006 Flujo de intencion a plan.\n");
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "operacion", "REGISTRO_CAMBIOS.md"),
            "CH-012   T-004   Cierre\nCH-013   T-005   Formalizacion\n");

        var store = new LocalStateStore(DirectorioTemporal());
        await store.SaveAssessmentAsync(AssessmentConProyecto(projectDirectory));
        var service = new ContextService(store);

        var context = await service.BuildContextAsync(CancellationToken.None);

        Assert.Equal(DetectionStatus.Detected, context.Status);
        Assert.Equal("condor", context.RootName);
        Assert.True(context.Summary.IsGitRepository);
        Assert.Contains("T-006", context.ContinuationPoint!.SuggestedNext);
        Assert.Contains("a1b2c3d4 Update RELEVO.md", context.Summary.LastCommits);
        Assert.Equal(new[] { "C#", "Python" }, context.Summary.Languages);
        Assert.True(context.Summary.HasOperativeArtifacts);
    }

    [Fact]
    public async Task BuildContextAsync_ConProyecto_SinOperacionNoGeneraContextoArtefactual()
    {
        var projectDirectory = DirectorioTemporal();
        Directory.CreateDirectory(projectDirectory);

        var store = new LocalStateStore(DirectorioTemporal());
        await store.SaveAssessmentAsync(AssessmentConProyecto(projectDirectory));
        var service = new ContextService(store);

        var context = await service.BuildContextAsync(CancellationToken.None);

        Assert.Equal(DetectionStatus.Detected, context.Status);
        Assert.False(context.Summary.HasOperativeArtifacts);
        Assert.Equal(DetectionStatus.Detected, context.ContinuationPoint!.Status);
        Assert.Contains("a1b2c3d4", context.ContinuationPoint.LastActivity);
    }

    [Fact]
    public async Task BuildContextAsync_PersisteContextJsonComoArtefactoDerivado()
    {
        var projectDirectory = DirectorioTemporal();
        Directory.CreateDirectory(projectDirectory);
        var storeDirectory = DirectorioTemporal();
        var store = new LocalStateStore(storeDirectory);
        await store.SaveAssessmentAsync(AssessmentConProyecto(projectDirectory));
        var service = new ContextService(store);

        var exitCode = await Condor.Cli.Commands.ContextCommand.ExecuteAsync(
            service,
            store,
            new[] { "--json" });

        var persisted = await new LocalStateStore(storeDirectory).LoadContextAsync();

        Assert.Equal(0, exitCode);
        Assert.NotNull(persisted);
        Assert.Equal("1.0.0", persisted.SchemaVersion);
    }

    [Fact]
    public async Task BuildContextAsync_ArtefactoExcesivo_DegradaYDeclaraLimite()
    {
        var projectDirectory = DirectorioTemporal();
        Directory.CreateDirectory(Path.Combine(projectDirectory, "operacion"));
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "operacion", "BACKLOG.md"),
            new string('a', (64 * 1024) + 1));

        var store = new LocalStateStore(DirectorioTemporal());
        await store.SaveAssessmentAsync(AssessmentConProyecto(projectDirectory));
        var service = new ContextService(store);

        var context = await service.BuildContextAsync(CancellationToken.None);

        Assert.Equal(DetectionStatus.Limited, context.Status);
        Assert.Contains(ContextLimits.LimitArtifactSize, context.LimitsApplied);
        Assert.Contains("BACKLOG.md", context.Reason!);
    }

    private static AssessmentResult AssessmentConProyecto(string workingDirectory)
    {
        return new AssessmentResult
        {
            SchemaVersion = "1.0.0",
            WorkingDirectory = workingDirectory,
            Tools = new ToolsProfile
            {
                Git = new ToolInfo { Name = "git", Status = DetectionStatus.Detected, Version = "2.40.0" },
                Ollama = new OllamaStatus { Installed = false, ServerRunning = false }
            },
            Project = new ProjectProfile
            {
                Status = DetectionStatus.Detected,
                RootPath = workingDirectory,
                RootName = "condor",
                IsGitRepository = true,
                Git = new GitProjectState
                {
                    Branch = "main",
                    IsDirty = false,
                    Status = DetectionStatus.Detected,
                    Commits = new List<GitCommitSummary>
                    {
                        new() { Hash = "a1b2c3d4", Subject = "Update RELEVO.md" }
                    }
                },
                Languages = new List<LanguageEvidence>
                {
                    new() { Name = "Python" },
                    new() { Name = "C#" }
                },
                Frameworks = new List<FrameworkEvidence>
                {
                    new() { Name = "ASP.NET Core", Signal = "Sdk web", Manifest = "condor.csproj" }
                },
                Manifests = new List<ManifestInfo>
                {
                    new()
                    {
                        Kind = "csproj",
                        Path = "condor.csproj",
                        Name = "condor",
                        Dependencies = new List<string> { "System.Text.Json" }
                    }
                },
                Documentation = new List<DocumentationInfo>
                {
                    new() { Kind = "README", Path = "README.md" }
                },
                FilesCount = 10,
                DirectoriesCount = 4
            }
        };
    }

    private static string DirectorioTemporal()
    {
        return Path.Combine(Path.GetTempPath(), "condor-context-" + Guid.NewGuid().ToString("N"));
    }
}
