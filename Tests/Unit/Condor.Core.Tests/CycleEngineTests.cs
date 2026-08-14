using System;
using System.Collections.Generic;
using Condor.Core.Cycle;
using Condor.Core.Models;

namespace Condor.Core.Tests;

public class CycleEngineTests
{
    [Fact]
    public void Decide_SinPlan_SeDetiene()
    {
        var d = CycleEngine.EvaluateDecision(null, null, null, 1, CycleLimits.Default);

        Assert.True(d.Stopped);
        Assert.False(d.Complete);
        Assert.Equal(CycleStage.Detenido, d.Stage);
    }

    [Fact]
    public void Decide_PlanNotDetected_SeDetiene()
    {
        var plan = PlanConEstado(DetectionStatus.NotDetected);

        var d = CycleEngine.EvaluateDecision(plan, null, null, 1, CycleLimits.Default);

        Assert.True(d.Stopped);
    }

    [Fact]
    public void Decide_PlanLimited_SeDetiene()
    {
        var plan = PlanConEstado(DetectionStatus.Limited);

        var d = CycleEngine.EvaluateDecision(plan, BuildOk(), VerOk(), 1, CycleLimits.Default);

        Assert.True(d.Stopped);
    }

    [Fact]
    public void Decide_SinBuild_SeDetiene()
    {
        var plan = PlanOk();

        var d = CycleEngine.EvaluateDecision(plan, null, null, 1, CycleLimits.Default);

        Assert.True(d.Stopped);
    }

    [Fact]
    public void Decide_SinVerificacion_SeDetiene()
    {
        var plan = PlanOk();

        var d = CycleEngine.EvaluateDecision(plan, BuildOk(), null, 1, CycleLimits.Default);

        Assert.True(d.Stopped);
    }

    [Fact]
    public void Decide_VerificacionCorrecta_Completa()
    {
        var d = CycleEngine.EvaluateDecision(PlanOk(), BuildOk(), VerOk(), 1, CycleLimits.Default);

        Assert.True(d.Complete);
        Assert.False(d.Stopped);
        Assert.Equal(CycleStage.Completado, d.Stage);
    }

    [Fact]
    public void Decide_VerificacionFallida_IteracionMenorMax_Regenera()
    {
        var d = CycleEngine.EvaluateDecision(PlanOk(), BuildOk(), VerFallida(), 1, CycleLimits.Default);

        Assert.True(d.Regenerate);
        Assert.Equal(CycleStage.Regenerar, d.Stage);
    }

    [Fact]
    public void Decide_VerificacionFallida_IteracionAlLimite_SeDetiene()
    {
        var limits = new CycleLimits { MaxIterations = 3 };
        var d = CycleEngine.EvaluateDecision(PlanOk(), BuildOk(), VerFallida(), 3, limits);

        Assert.False(d.Regenerate);
        Assert.True(d.Stopped);
        Assert.Contains("limite", d.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decide_VerificacionLimited_NoCompleta()
    {
        var d = CycleEngine.EvaluateDecision(PlanOk(), BuildOk(), VerLimited(), 1, CycleLimits.Default);

        Assert.False(d.Complete);
    }

    private static WorkPlan PlanOk()
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

    private static WorkPlan PlanConEstado(DetectionStatus status)
    {
        var plan = PlanOk();
        plan.Status = status;
        return plan;
    }

    private static BuildResult BuildOk()
    {
        return new BuildResult
        {
            Status = DetectionStatus.Detected,
            WorkingDirectory = "C:\\proyecto",
            Applied = 1,
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static VerificationResult VerOk()
    {
        return new VerificationResult
        {
            Status = DetectionStatus.Detected,
            Passed = 1,
            Failed = 0,
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static VerificationResult VerFallida()
    {
        return new VerificationResult
        {
            Status = DetectionStatus.Detected,
            Passed = 0,
            Failed = 1,
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static VerificationResult VerLimited()
    {
        return new VerificationResult
        {
            Status = DetectionStatus.Limited,
            Passed = 0,
            Failed = 0,
            GeneratedAtUtc = DateTime.UtcNow
        };
    }
}
