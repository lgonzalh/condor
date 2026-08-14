using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Core.Serialization;
using Condor.Infrastructure.Cycle;
using Condor.Infrastructure.State;

namespace Condor.Infrastructure.Tests;

public class CycleServiceTests
{
    [Fact]
    public async Task AdvanceAsync_CicloCompleto_DevuelveCompletado()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        var service = new CycleService(
            new StubPlan(WorkPlanDetectado()),
            new StubBuild(BuildDetectado()),
            new StubVerify(VerDetectado()),
            store,
            new CycleLimits { MaxIterations = 3 });

        var result = await service.AdvanceAsync("avanzar", CancellationToken.None);

        Assert.Equal(DetectionStatus.Detected, result.Status);
        Assert.Equal(CycleStage.Completado, result.Checkpoint.Stage);
        Assert.Equal(1, result.Iterations);
    }

    [Fact]
    public async Task AdvanceAsync_VerificacionFallida_RegeneraHastaLimite()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        var service = new CycleService(
            new StubPlan(WorkPlanDetectado()),
            new StubBuild(BuildDetectado()),
            new StubVerify(VerFallida()),
            store,
            new CycleLimits { MaxIterations = 2 });

        var result = await service.AdvanceAsync("avanzar", CancellationToken.None);

        Assert.Equal(DetectionStatus.Limited, result.Status);
        Assert.Equal(CycleStage.Detenido, result.Checkpoint.Stage);
        Assert.Equal(2, result.Iterations);
        Assert.Contains(CycleLimits.LimitIterations, result.LimitsApplied);
    }

    [Fact]
    public async Task AdvanceAsync_SinPlan_DevuelveDetenidoSinIteracionesExtra()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        var service = new CycleService(
            new StubPlan(PlanNotDetected()),
            new StubBuild(BuildDetectado()),
            new StubVerify(VerDetectado()),
            store,
            new CycleLimits { MaxIterations = 3 });

        var result = await service.AdvanceAsync("avanzar", CancellationToken.None);

        Assert.Equal(DetectionStatus.NotDetected, result.Status);
        Assert.Equal(CycleStage.Detenido, result.Checkpoint.Stage);
    }

    [Fact]
    public async Task Determinismo_DosCiclosCompletos_ProducenElMismoResultado()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        var service = new CycleService(
            new StubPlan(WorkPlanDetectado()),
            new StubBuild(BuildDetectado()),
            new StubVerify(VerDetectado()),
            store);

        var first = await service.AdvanceAsync("avanzar", CancellationToken.None);
        var second = await service.AdvanceAsync("avanzar", CancellationToken.None);

        first.GeneratedAtUtc = DateTime.MinValue;
        first.Checkpoint.GeneratedAtUtc = DateTime.MinValue;
        second.GeneratedAtUtc = DateTime.MinValue;
        second.Checkpoint.GeneratedAtUtc = DateTime.MinValue;

        Assert.Equal(CycleJson.Serialize(first), CycleJson.Serialize(second));
    }

    private static WorkPlan WorkPlanDetectado()
    {
        return new WorkPlan
        {
            Status = DetectionStatus.Detected,
            Intention = "modificar",
            WorkingDirectory = "C:\\proyecto",
            RootName = "condor",
            Objective = "avanzar",
            Tasks = new List<PlanTask>(),
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static WorkPlan PlanNotDetected()
    {
        return new WorkPlan { Status = DetectionStatus.NotDetected, GeneratedAtUtc = DateTime.UtcNow };
    }

    private static BuildResult BuildDetectado()
    {
        return new BuildResult
        {
            Status = DetectionStatus.Detected,
            WorkingDirectory = "C:\\proyecto",
            Applied = 1,
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static VerificationResult VerDetectado()
    {
        return new VerificationResult { Status = DetectionStatus.Detected, Passed = 1, Failed = 0, GeneratedAtUtc = DateTime.UtcNow };
    }

    private static VerificationResult VerFallida()
    {
        return new VerificationResult { Status = DetectionStatus.Detected, Passed = 0, Failed = 1, GeneratedAtUtc = DateTime.UtcNow };
    }

    private static string DirectorioTemporal()
    {
        return Path.Combine(Path.GetTempPath(), "condor-cycle-" + Guid.NewGuid().ToString("N"));
    }

    private sealed class StubPlan : IPlanService
    {
        private readonly WorkPlan _plan;

        public StubPlan(WorkPlan plan) => _plan = plan;

        public Task<WorkPlan> BuildPlanAsync(string userRequest, CancellationToken ct = default) => Task.FromResult(_plan);
    }

    private sealed class StubBuild : IBuildService
    {
        private readonly BuildResult _build;

        public StubBuild(BuildResult build) => _build = build;

        public Task<BuildResult> ApplyPlanAsync(CancellationToken ct = default) => Task.FromResult(_build);
    }

    private sealed class StubVerify : IVerificationService
    {
        private readonly VerificationResult _verification;

        public StubVerify(VerificationResult verification) => _verification = verification;

        public Task<VerificationResult> VerifyAsync(CancellationToken ct = default) => Task.FromResult(_verification);
    }
}
