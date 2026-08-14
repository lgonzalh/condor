using Condor.Core.Models;

namespace Condor.Cli.Presentation;

public static class BuildRenderer
{
    public static void RenderBuild(BuildResult result)
    {
        Terminal.WriteHeader("BUILD");

        Terminal.WriteLine("  Estado            : " + EstadoLine(result));
        Terminal.WriteLine("  Proyecto          : " + RootLine(result));
        Terminal.WriteLine("  Intencion         : " + result.Intention);
        Terminal.WriteLine("  Objetivo          : " + (result.Objective.Length > 0 ? result.Objective : "(sin objetivo)"));
        Terminal.WriteLine("  Resumen           : " + result.Applied + " aplicadas, " +
                           result.Omitted + " omitidas, " + result.Failed + " fallidas");

        RenderActions(result.Actions);

        Terminal.WriteLine(
            "  Limites aplicados : " +
            (result.LimitsApplied.Count > 0 ? string.Join(", ", result.LimitsApplied) : "ninguno"));

        if (result.Status != DetectionStatus.Detected && !string.IsNullOrWhiteSpace(result.Reason))
        {
            Terminal.WriteWarning("  - Build: " + result.Reason);
        }
    }

    private static void RenderActions(List<BuildAction> actions)
    {
        Terminal.WriteLine("  Acciones          : " + (actions.Count > 0 ? actions.Count.ToString() : "ninguna"));

        foreach (var action in actions)
        {
            Terminal.WriteLine("    - [" + KindLine(action.Kind) + "] " + action.Id + " " + action.RelativePath);

            if (!string.IsNullOrWhiteSpace(action.Status))
            {
                var status = action.Status == BuildAction.StatusApplied ? action.Status : action.Status;

                if (action.Status == BuildAction.StatusApplied)
                {
                    Terminal.WriteDim("      estado: " + status);
                }
                else
                {
                    Terminal.WriteWarning("      estado: " + status + (action.StatusReason is { Length: > 0 } r ? " - " + r : ""));
                }
            }
        }
    }

    private static string EstadoLine(BuildResult result)
    {
        return result.Status switch
        {
            DetectionStatus.Detected => "detectado",
            DetectionStatus.NotDetected => "no detectado",
            DetectionStatus.Limited => "limitado",
            _ => "error"
        };
    }

    private static string RootLine(BuildResult result)
    {
        return string.IsNullOrWhiteSpace(result.RootName)
            ? "(sin proyecto)"
            : result.RootName + " | " + result.WorkingDirectory;
    }

    private static string KindLine(BuildActionKind kind)
    {
        return kind switch
        {
            BuildActionKind.Crear => "crear",
            _ => "actualizar"
        };
    }
}
