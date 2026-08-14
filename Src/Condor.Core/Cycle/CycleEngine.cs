using System;
using Condor.Core.Models;

namespace Condor.Core.Cycle;

public readonly record struct CycleDecision(
    bool Complete,
    bool Regenerate,
    bool Stopped,
    CycleStage Stage,
    string? Reason);

public static class CycleEngine
{
    private const string ReasonNoWorkDirection =
        "No hay direccion de trabajo. Ejecuta 'condor analizar' y 'condor contexto' primero.";

    private const string ReasonNoPlan =
        "No fue posible generar el plan; el ciclo no puede continuar.";

    private const string ReasonBuildMissing =
        "No se obtuvo un resultado de build; el ciclo se detiene.";

    private const string ReasonVerificationMissing =
        "No se obtuvo un resultado de verificacion; el ciclo se detiene.";

    private const string ReasonMaxIterations =
        "Se alcanzo el limite de iteraciones; el ciclo se detiene.";

    public static CycleDecision EvaluateDecision(
        WorkPlan? plan,
        BuildResult? build,
        VerificationResult? verification,
        int iteration,
        CycleLimits limits)
    {
        if (plan is null || plan.Status == DetectionStatus.NotDetected)
        {
            return new CycleDecision(false, false, true, CycleStage.Detenido, ReasonNoWorkDirection);
        }

        if (plan.Status == DetectionStatus.Limited || plan.Intention == "indefinida")
        {
            return new CycleDecision(false, false, true, CycleStage.Detenido, ReasonNoPlan);
        }

        if (build is null || build.Status == DetectionStatus.NotDetected)
        {
            return new CycleDecision(false, false, true, CycleStage.Detenido, ReasonBuildMissing);
        }

        if (verification is null || verification.Status == DetectionStatus.NotDetected)
        {
            return new CycleDecision(false, false, true, CycleStage.Detenido, ReasonVerificationMissing);
        }

        if (verification.Status == DetectionStatus.Limited)
        {
            return new CycleDecision(false, false, true, CycleStage.Degradado,
                "La verificacion quedo degradada; el ciclo se detiene sin regenerar.");
        }

        if (verification.Failed == 0)
        {
            return new CycleDecision(true, false, false, CycleStage.Completado, null);
        }

        if (iteration < limits.MaxIterations)
        {
            return new CycleDecision(false, true, false, CycleStage.Regenerar,
                "La verificacion no fue satisfactoria; se realiza una nueva iteracion.");
        }

        return new CycleDecision(false, false, true, CycleStage.Detenido, ReasonMaxIterations);
    }
}
