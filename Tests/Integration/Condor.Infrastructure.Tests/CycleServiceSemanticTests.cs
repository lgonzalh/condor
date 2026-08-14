using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Infrastructure.Cycle;
using Condor.Infrastructure.State;

namespace Condor.Infrastructure.Tests;

public class CycleServiceSemanticTests
{
    [Fact]
    public async Task AdvanceAsync_SemanticaCorrecta_CicloCompleta()
    {
        var service = NewService(PlanOk(), BuildOk(), VerOk(), SemCorrecta());

        var result = await service.AdvanceAsync("avanzar", CancellationToken.None);

        Assert.Equal(CycleStage.Completado, result.Checkpoint.Stage);
        Assert.Equal("correcta", result.SemanticStatus);
        Assert.True(result.SemanticAvailable);
    }

    [Fact]
    public async Task AdvanceAsync_SemanticaFallida_NoCompleta()
    {
        var service = NewService(PlanOk(), BuildOk(), VerOk(), SemFallida());

        var result = await service.AdvanceAsync("avanzar", CancellationToken.None);

        Assert.NotEqual(CycleStage.Completado, result.Checkpoint.Stage);
        Assert.Equal("fallida", result.SemanticStatus);
    }

    [Fact]
    public async Task AdvanceAsync_SemanticaNoDisponible_NoFalsaFalla()
    {
        var service = NewService(PlanOk(), BuildOk(), VerOk(), SemNoDisponible());

        var result = await service.AdvanceAsync("avanzar", CancellationToken.None);

        Assert.Equal(CycleStage.Degradado, result.Checkpoint.Stage);
        Assert.Equal("no_disponible", result.SemanticStatus);
    }

    [Fact]
    public async Task AdvanceAsync_SemanticaIncompleta_DegradaSinExito()
    {
        var service = NewService(PlanOk(), BuildOk(), VerOk(), SemIncompleta());

        var result = await service.AdvanceAsync("avanzar", CancellationToken.None);

        Assert.Equal(CycleStage.Degradado, result.Checkpoint.Stage);
        Assert.Equal("incompleta", result.SemanticStatus);
    }

    [Fact]
    public async Task AdvanceAsync_SinServicioSemantico_SemanticaOmitida()
    {
        var service = new CycleService(
            new StubPlan(PlanOk()),
            new StubBuild(BuildOk()),
            new StubVerify(VerOk()),
            new LocalStateStore(DirectorioTemporal()),
            new CycleLimits { MaxIterations = 3 });

        var result = await service.AdvanceAsync("avanzar", CancellationToken.None);

        Assert.Equal(CycleStage.Completado, result.Checkpoint.Stage);
        Assert.Null(result.SemanticAvailable);
    }

    [Fact]
    public async Task AdvanceAsync_SemanticaFallida_RegeneraHastaMaxIteraciones()
    {
        var service = NewService(PlanOk(), BuildOk(), VerOk(), SemFallida(),
            new CycleLimits { MaxIterations = 2 });

        var result = await service.AdvanceAsync("avanzar", CancellationToken.None);

        Assert.Equal(2, result.Iterations);
        Assert.NotEqual(CycleStage.Completado, result.Checkpoint.Stage);
    }

    private static CycleService NewService(
        WorkPlan plan,
        BuildResult build,
        VerificationResult verification,
        SemanticVerificationResult semantic,
        CycleLimits? limits = null)
    {
        return new CycleService(
            new StubPlan(plan),
            new StubBuild(build),
            new StubVerify(verification),
            new LocalStateStore(DirectorioTemporal()),
            limits ?? new CycleLimits { MaxIterations = 3 },
            new StubSemantic(semantic));
    }

    private static WorkPlan PlanOk()
    {
        return new WorkPlan
        {
            Status = DetectionStatus.Detected,
            Intention = "modificar",
            WorkingDirectory = "C:\\proyecto",
            RootName = "app",
            Objective = "avanzar",
            Tasks = new List<PlanTask>(),
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static BuildResult BuildOk()
    {
        return new BuildResult { Status = DetectionStatus.Detected, WorkingDirectory = "C:\\proyecto", Applied = 1, GeneratedAtUtc = DateTime.UtcNow };
    }

    private static VerificationResult VerOk()
    {
        return new VerificationResult { Status = DetectionStatus.Detected, Passed = 1, Failed = 0, GeneratedAtUtc = DateTime.UtcNow };
    }

    private static SemanticVerificationResult SemCorrecta()
    {
        return new SemanticVerificationResult
        {
            Status = DetectionStatus.Detected,
            Checks = new List<SemanticCheck>
            {
                new() { Kind = SemanticCheck.KindCompile, Status = SemanticCheck.StatusCorrect },
                new() { Kind = SemanticCheck.KindTest, Status = SemanticCheck.StatusCorrect }
            },
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static SemanticVerificationResult SemFallida()
    {
        return new SemanticVerificationResult
        {
            Status = DetectionStatus.Limited,
            Checks = new List<SemanticCheck>
            {
                new() { Kind = SemanticCheck.KindCompile, Status = SemanticCheck.StatusFailed }
            },
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static SemanticVerificationResult SemNoDisponible()
    {
        return new SemanticVerificationResult
        {
            Status = DetectionStatus.Limited,
            Checks = new List<SemanticCheck>
            {
                new() { Kind = SemanticCheck.KindCompile, Status = SemanticCheck.StatusNotAvailable }
            },
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static SemanticVerificationResult SemIncompleta()
    {
        return new SemanticVerificationResult
        {
            Status = DetectionStatus.Limited,
            Checks = new List<SemanticCheck>
            {
                new() { Kind = SemanticCheck.KindCompile, Status = SemanticCheck.StatusTimeout }
            },
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static string DirectorioTemporal()
    {
        return Path.Combine(Path.GetTempPath(), "condor-cyclesem-" + Guid.NewGuid().ToString("N"));
    }

    private sealed class StubPlan : IPlanService
    {
        private readonly WorkPlan _plan;
        public StubPlan(WorkPlan plan) => _plan = plan;
        public Task<WorkPlan> BuildPlanAsync(string req, CancellationToken ct = default) => Task.FromResult(_plan);
    }

    private sealed class StubBuild : IBuildService
    {
        private readonly BuildResult _build;
        public StubBuild(BuildResult build) => _build = build;
        public Task<BuildResult> ApplyPlanAsync(CancellationToken ct = default) => Task.FromResult(_build);
    }

    private sealed class StubVerify : IVerificationService
    {
        private readonly VerificationResult _v;
        public StubVerify(VerificationResult v) => _v = v;
        public Task<VerificationResult> VerifyAsync(CancellationToken ct = default) => Task.FromResult(_v);
    }

    private sealed class StubSemantic : ISemanticVerificationService
    {
        private readonly SemanticVerificationResult _s;
        public StubSemantic(SemanticVerificationResult s) => _s = s;
        public Task<SemanticVerificationResult> VerifySemanticAsync(bool c, bool t, CancellationToken ct = default) => Task.FromResult(_s);
    }
}
