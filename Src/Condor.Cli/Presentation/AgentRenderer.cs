using Condor.Core.Models;

namespace Condor.Cli.Presentation;

public static class AgentRenderer
{
    public static void RenderResult(AgentResult result)
    {
        Terminal.WriteHeader("CONDOR HIZO");

        Terminal.WriteLine("  Estado  : " + (result.Success ? "ok" : "no completo"));
        Terminal.WriteLine("  Modelo  : " + (result.Model.Length > 0 ? result.Model : "(sin modelo)"));
        if (!string.IsNullOrWhiteSpace(result.Objective)) Terminal.WriteLine("  Tarea   : " + result.Objective);
        if (!string.IsNullOrWhiteSpace(result.Reason)) Terminal.WriteDim("  Motivo  : " + result.Reason);
        Terminal.WriteLine("  Pasos   : " + result.Steps.Count);

        foreach (var step in result.Steps)
        {
            var line = "    - [iter " + step.Iteration + "] " + step.Action + (string.IsNullOrWhiteSpace(step.Path) ? "" : " " + step.Path);
            Terminal.WriteLine(line + " " + (step.Success ? "(ok)" : "(falla)"));
            if (step.ResultPreview is { Length: > 0 })
            {
                Terminal.WriteDim("      " + step.ResultPreview);
            }
        }

        if (result.Checkpoint is not null)
        {
            Terminal.WriteLine("  Checkpoint: iter=" + result.Checkpoint.Iteration +
                " estado=" + (result.Checkpoint.HarnessState ?? "-") +
                " next=" + (result.Checkpoint.NextAction ?? "-"));
        }
    }
}
