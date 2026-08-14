using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Condor.Cli.Commands;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Infrastructure.Cycle;
using Condor.Infrastructure.State;

namespace Condor.Infrastructure.Tests;

public class AdvanceCommandTests
{
    [Fact]
    public async Task ExecuteAsync_ConCicloCompletado_DevuelveExitCodeCero()
    {
        var storeDirectory = DirectorioTemporal();
        var store = new LocalStateStore(storeDirectory);
        var service = new CycleService(
            new StubPlan(WorkPlanDetectado()),
            new StubBuild(BuildDetectado()),
            new StubVerify(VerDetectado()),
            store);

        var exitCode = await AdvanceCommand.ExecuteAsync(
            service,
            store,
            new[] { "avanzar", "--json" });

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_ConCicloCompletado_PersisteCycleJson()
    {
        var storeDirectory = DirectorioTemporal();
        var store = new LocalStateStore(storeDirectory);
        var service = new CycleService(
            new StubPlan(WorkPlanDetectado()),
            new StubBuild(BuildDetectado()),
            new StubVerify(VerDetectado()),
            store);

        var exitCode = await AdvanceCommand.ExecuteAsync(
            service,
            store,
            new[] { "avanzar", "--json" });

        var persisted = await new LocalStateStore(storeDirectory).LoadCycleAsync();

        Assert.Equal(0, exitCode);
        Assert.NotNull(persisted);
        Assert.Equal("1.0.0", persisted.SchemaVersion);
    }

    [Fact]
    public async Task ExecuteAsync_SinPlan_DevuelveExitCodeUno()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        var service = new CycleService(
            new StubPlan(PlanNotDetected()),
            new StubBuild(BuildDetectado()),
            new StubVerify(VerDetectado()),
            store);

        var exitCode = await AdvanceCommand.ExecuteAsync(
            service,
            store,
            new[] { "avanzar", "--json" });

        Assert.Equal(1, exitCode);
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

    private static string DirectorioTemporal()
    {
        return Path.Combine(Path.GetTempPath(), "condor-cyclecli-" + Guid.NewGuid().ToString("N"));
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
