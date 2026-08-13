using Condor.Core.Models;
using Condor.Core.Planning;
using Condor.Infrastructure.Planning;
using Condor.Infrastructure.State;

namespace Condor.Infrastructure.Tests;

public class PlanServiceTests
{
    [Fact]
    public async Task BuildPlanAsync_ConContextoExistente_GeneraPlanDetectado()
    {
        var projectDirectory = DirectorioTemporal();
        Directory.CreateDirectory(projectDirectory);
        var store = new LocalStateStore(DirectorioTemporal());
        await store.SaveContextAsync(ContextoConProyecto(projectDirectory));
        var service = new PlanService(store);

        var plan = await service.BuildPlanAsync("modificar el modulo de reportes", CancellationToken.None);

        Assert.Equal(DetectionStatus.Detected, plan.Status);
        Assert.Equal(PlanIntent.Modificar, plan.Intention);
        Assert.Equal("condor", plan.RootName);
    }

    [Fact]
    public async Task BuildPlanAsync_SinContextoNiAssessment_DevuelveNotDetected()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        var service = new PlanService(store);

        var plan = await service.BuildPlanAsync("crear algo", CancellationToken.None);

        Assert.Equal(DetectionStatus.NotDetected, plan.Status);
        Assert.Contains("condor contexto", plan.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildPlanAsync_ContextoSinProyecto_GeneraPlanParcial()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        await store.SaveContextAsync(new ProjectContext
        {
            SchemaVersion = "1.0.0",
            Status = DetectionStatus.Detected,
            WorkingDirectory = "C:\\proyecto",
            RootName = "",
            Risks = new List<ContextRisk>(),
            RelevantDependencies = new List<RelevantDependency>(),
            Recommendations = new List<PlannerRecommendation>(),
            LimitsApplied = new List<string>()
        });
        var service = new PlanService(store);

        var plan = await service.BuildPlanAsync("modificar algo", CancellationToken.None);

        Assert.Equal(DetectionStatus.Detected, plan.Status);
        Assert.Equal("", plan.RootName);
    }

    [Fact]
    public async Task BuildPlanAsync_ContextoLimited_DejaPlanEnLimited()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        await store.SaveContextAsync(new ProjectContext
        {
            SchemaVersion = "1.0.0",
            Status = DetectionStatus.Limited,
            Reason = "artefacto degradado",
            WorkingDirectory = "C:\\proyecto",
            RootName = "condor",
            Risks = new List<ContextRisk>(),
            RelevantDependencies = new List<RelevantDependency>(),
            Recommendations = new List<PlannerRecommendation>(),
            LimitsApplied = new List<string> { "artifact-size" }
        });
        var service = new PlanService(store);

        var plan = await service.BuildPlanAsync("modificar algo", CancellationToken.None);

        Assert.Equal(DetectionStatus.Limited, plan.Status);
        Assert.Contains("artifact-size", plan.LimitsApplied);
    }

    [Fact]
    public async Task BuildPlanAsync_SolicitudVacia_DevuelveLimited()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        await store.SaveContextAsync(ContextoConProyecto(DirectorioTemporal()));
        var service = new PlanService(store);

        var plan = await service.BuildPlanAsync("   ", CancellationToken.None);

        Assert.Equal(DetectionStatus.Limited, plan.Status);
        Assert.Contains("solicitud", plan.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    private static ProjectContext ContextoConProyecto(string workingDirectory)
    {
        return new ProjectContext
        {
            SchemaVersion = "1.0.0",
            Status = DetectionStatus.Detected,
            WorkingDirectory = workingDirectory,
            RootName = "condor",
            Summary = new ProjectContextSummary
            {
                Languages = new List<string> { "C#" },
                IsGitRepository = true,
                HasOperativeArtifacts = true
            },
            Risks = new List<ContextRisk>
            {
                new() { Kind = "sin-git", Severity = "alta", Evidence = workingDirectory }
            },
            RelevantDependencies = new List<RelevantDependency>(),
            Recommendations = new List<PlannerRecommendation>
            {
                new() { Text = "Revisa los manifiestos con error de parseo antes de planificar.", Evidence = "package.json" }
            },
            LimitsApplied = new List<string>(),
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static string DirectorioTemporal()
    {
        return Path.Combine(Path.GetTempPath(), "condor-plan-" + Guid.NewGuid().ToString("N"));
    }
}
