using Condor.Core.Models;

namespace Condor.Cli.Presentation;

public static class AdvanceRenderer
{
    public static void RenderAdvance(CycleResult result)
    {
        Terminal.WriteHeader("AVANCE");

        Terminal.WriteLine("  Proyecto          : " + RootLine(result));
        Terminal.WriteLine("  Intencion         : " + result.Intention);
        Terminal.WriteLine("  Objetivo          : " + (result.Objective.Length > 0 ? result.Objective : "(sin objetivo)"));
        Terminal.WriteLine("  Cambios aplicados : " + result.Applied);
        Terminal.WriteLine("  Cambios verificados: " + result.Verified);

        RenderSemantic(result);

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

    private static string RootLine(CycleResult result)
    {
        return string.IsNullOrWhiteSpace(result.RootName)
            ? "(sin proyecto)"
            : result.RootName + " - " + result.WorkingDirectory;
    }
}
