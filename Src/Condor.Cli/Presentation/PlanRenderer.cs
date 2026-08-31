using Condor.Core.Models;

namespace Condor.Cli.Presentation;

public static class PlanRenderer
{
    public static void RenderPlan(WorkPlan plan)
    {
        Terminal.WriteHeader("PLAN");

        Terminal.WriteLine("  Estado            : " + EstadoLine(plan));
        Terminal.WriteLine("  Proyecto          : " + RootLine(plan));
        Terminal.WriteLine("  Intencion         : " + plan.Intention);
        Terminal.WriteLine("  Objetivo          : " + (plan.Objective.Length > 0 ? plan.Objective : "(sin objetivo)"));

        RenderTasks(plan.Tasks);

        Terminal.WriteLine("  Recomendaciones   : " + (plan.Evidence.Count > 0 ? plan.Evidence.Count.ToString() : "ninguna"));
        Terminal.WriteLine("  Riegos            : " + (plan.RisksConsidered.Count > 0 ? plan.RisksConsidered.Count.ToString() : "ninguno"));
        Terminal.WriteLine(
            "  Limites aplicados : " +
            (plan.LimitsApplied.Count > 0 ? string.Join(", ", plan.LimitsApplied) : "ninguno"));

        if (plan.Status != DetectionStatus.Detected && !string.IsNullOrWhiteSpace(plan.Reason))
        {
            Terminal.WriteWarning("  - Plan: " + plan.Reason);
        }
    }

    private static void RenderTasks(List<PlanTask> tasks)
    {
        Terminal.WriteLine("  Tareas            : " + (tasks.Count > 0 ? tasks.Count.ToString() : "ninguna"));

        foreach (var task in tasks)
        {
            var depends = task.DependsOn.Count > 0 ? " (despues de " + string.Join(", ", task.DependsOn) + ")" : "";
            Terminal.WriteLine("    - [" + task.Priority + "] " + task.Id + " " + task.Title + depends);

            if (!string.IsNullOrWhiteSpace(task.Detail))
            {
                Terminal.WriteDim("      detalle: " + task.Detail);
            }
        }
    }

    private static string EstadoLine(WorkPlan plan)
    {
        return plan.Status switch
        {
            DetectionStatus.Detected => "detectado",
            DetectionStatus.NotDetected => "no detectado",
            DetectionStatus.Limited => "limitado",
            _ => "error"
        };
    }

    private static string RootLine(WorkPlan plan)
    {
        return string.IsNullOrWhiteSpace(plan.RootName)
            ? "(sin proyecto)"
            : plan.RootName + " - " + plan.WorkingDirectory;
    }
}
