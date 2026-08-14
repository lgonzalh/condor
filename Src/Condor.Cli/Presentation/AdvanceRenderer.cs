using Condor.Core.Models;

namespace Condor.Cli.Presentation;

public static class AdvanceRenderer
{
    public static void RenderAdvance(CycleResult result)
    {
        Terminal.WriteHeader("AVANCE");

        Terminal.WriteLine("  Estado            : " + EstadoLine(result));
        Terminal.WriteLine("  Proyecto          : " + RootLine(result));
        Terminal.WriteLine("  Intencion         : " + result.Intention);
        Terminal.WriteLine("  Objetivo          : " + (result.Objective.Length > 0 ? result.Objective : "(sin objetivo)"));
        Terminal.WriteLine("  Iteraciones       : " + result.Iterations);
        Terminal.WriteLine("  Etapas por iter.  : " + result.Stages);
        Terminal.WriteLine("  Cambios aplicados : " + result.Applied);
        Terminal.WriteLine("  Cambios verificados: " + result.Verified);

        RenderSemantic(result);

        RenderCheckpoint(result.Checkpoint);

        Terminal.WriteLine(
            "  Limites aplicados : " +
            (result.LimitsApplied.Count > 0 ? string.Join(", ", result.LimitsApplied) : "ninguno"));

        if (result.Status != DetectionStatus.Detected && !string.IsNullOrWhiteSpace(result.Reason))
        {
            Terminal.WriteWarning("  - Avance: " + result.Reason);
        }
    }

    private static void RenderSemantic(CycleResult result)
    {
        if (result.SemanticAvailable != true)
        {
            Terminal.WriteLine("  Verif. semantica  : (no ejecutada)");
            return;
        }

        var status = result.SemanticStatus ?? "desconocido";
        Terminal.WriteLine("  Verif. semantica  : " + status);

        if (!string.IsNullOrWhiteSpace(result.SemanticSummary))
        {
            Terminal.WriteDim("    resumen: " + result.SemanticSummary);
        }
    }

    private static void RenderCheckpoint(CycleCheckpoint checkpoint)
    {
        Terminal.WriteLine("  Checkpoint        :");
        Terminal.WriteLine("    ciclo: " + checkpoint.CycleId);
        Terminal.WriteLine("    iteracion: " + checkpoint.Iteration);
        Terminal.WriteLine("    etapa: " + checkpoint.Stage);
        Terminal.WriteLine("    estado: " + (checkpoint.StatusCycle ?? "(sin estado)"));
        Terminal.WriteLine("    recuperacion: " + (checkpoint.RecoveryState.Length > 0 ? checkpoint.RecoveryState : "(ninguna)"));
        Terminal.WriteLine("    siguiente: " + (checkpoint.NextAction.Length > 0 ? checkpoint.NextAction : "(ninguna)"));
    }

    private static string EstadoLine(CycleResult result)
    {
        return result.Status switch
        {
            DetectionStatus.Detected => "detectado",
            DetectionStatus.NotDetected => "no detectado",
            DetectionStatus.Limited => "limitado",
            _ => "error"
        };
    }

    private static string RootLine(CycleResult result)
    {
        return string.IsNullOrWhiteSpace(result.RootName)
            ? "(sin proyecto)"
            : result.RootName + " | " + result.WorkingDirectory;
    }
}
