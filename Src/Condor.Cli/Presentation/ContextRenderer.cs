using Condor.Core.Models;

namespace Condor.Cli.Presentation;

public static class ContextRenderer
{
    public static void RenderSummary(ProjectContext context)
    {
        Terminal.WriteHeader("CONTEXTO");

        Terminal.WriteLine("  Estado            : " + EstadoLine(context));
        Terminal.WriteLine("  Proyecto          : " + RootLine(context));

        RenderSummarySection(context.Summary);
        RenderContinuationPoint(context.ContinuationPoint);
        RenderRisks(context.Risks);
        RenderDependencies(context.RelevantDependencies);
        RenderRecommendations(context.Recommendations);

        Terminal.WriteLine(
            "  Limites aplicados : " +
            (context.LimitsApplied.Count > 0 ? string.Join(", ", context.LimitsApplied) : "ninguno"));

        if (context.Status != DetectionStatus.Detected && !string.IsNullOrWhiteSpace(context.Reason))
        {
            Terminal.WriteWarning("  - Contexto: " + context.Reason);
        }
    }

    private static void RenderSummarySection(ProjectContextSummary summary)
    {
        Terminal.WriteLine("  Lenguajes         : " + (summary.Languages.Count > 0 ? string.Join(", ", summary.Languages) : "(ninguno)"));
        Terminal.WriteLine("  Frameworks        : " + (summary.Frameworks.Count > 0 ? string.Join(", ", summary.Frameworks) : "(ninguno)"));
        Terminal.WriteLine("  Manifiestos       : " + summary.ManifestCount);
        Terminal.WriteLine("  Documentacion     : " + summary.DocumentationCount);

        var git = summary.IsGitRepository
            ? "rama " + (summary.GitBranch ?? "(sin rama)") +
              (summary.GitIsDirty ? ", estado sucio" : ", estado limpio")
            : "no es un repositorio Git";
        Terminal.WriteLine("  Git               : " + git);

        if (summary.LastCommits.Count > 0)
        {
            foreach (var commit in summary.LastCommits)
            {
                Terminal.WriteLine("  Cambios           : " + commit);
            }
        }

        Terminal.WriteLine("  Artefactos        : " + (summary.HasOperativeArtifacts ? "operacion/ presente" : "operacion/ ausente"));
    }

    private static void RenderContinuationPoint(ContinuationPoint? continuation)
    {
        if (continuation is null)
        {
            return;
        }

        Terminal.WriteLine("  Continuacion      : " +
            (continuation.Status == DetectionStatus.Detected ? "detectada" : "no detectada"));

        if (!string.IsNullOrWhiteSpace(continuation.Reason))
        {
            Terminal.WriteDim("    Motivo: " + continuation.Reason);
        }

        if (!string.IsNullOrWhiteSpace(continuation.LastActivity))
        {
            Terminal.WriteLine("    Ultima actividad: " + continuation.LastActivity);
        }

        if (!string.IsNullOrWhiteSpace(continuation.SuggestedNext))
        {
            Terminal.WriteLine("    Siguiente: " + continuation.SuggestedNext);
        }

        if (continuation.PendingWork.Count > 0)
        {
            Terminal.WriteLine("    Pendiente:");
            foreach (var task in continuation.PendingWork)
            {
                Terminal.WriteDim("      - " + task);
            }
        }

        if (continuation.Evidence.Count > 0)
        {
            Terminal.WriteLine("    Evidencia:");
            foreach (var evidence in continuation.Evidence)
            {
                Terminal.WriteDim("      - " + evidence);
            }
        }
    }

    private static void RenderRisks(List<ContextRisk> risks)
    {
        if (risks.Count == 0)
        {
            Terminal.WriteLine("  Riesgos           : ninguno detectado");
            return;
        }

        Terminal.WriteLine("  Riesgos           :");
        foreach (var risk in risks)
        {
            Terminal.WriteWarning(
                "    - [" + risk.Severity + "] " + risk.Kind + (risk.Evidence.Length > 0 ? " (" + risk.Evidence + ")" : ""));
        }
    }

    private static void RenderDependencies(List<RelevantDependency> dependencies)
    {
        if (dependencies.Count == 0)
        {
            Terminal.WriteLine("  Dependencias      : ninguna relevante");
            return;
        }

        Terminal.WriteLine("  Dependencias      :");
        foreach (var dependency in dependencies)
        {
            var detail = dependency.Detail is null ? "" : " (" + dependency.Detail + ")";
            Terminal.WriteDim("    - " + dependency.Name + " [" + dependency.Source + "]" + detail);
        }
    }

    private static void RenderRecommendations(List<PlannerRecommendation> recommendations)
    {
        if (recommendations.Count == 0)
        {
            Terminal.WriteLine("  Recomendaciones   : ninguna");
            return;
        }

        Terminal.WriteLine("  Recomendaciones   :");
        foreach (var recommendation in recommendations)
        {
            Terminal.WriteDim("    - " + recommendation.Text);
        }
    }

    private static string EstadoLine(ProjectContext context)
    {
        return context.Status switch
        {
            DetectionStatus.Detected => "detectado",
            DetectionStatus.NotDetected => "no detectado",
            DetectionStatus.Limited => "limitado",
            _ => "error"
        };
    }

    private static string RootLine(ProjectContext context)
    {
        return string.IsNullOrWhiteSpace(context.RootName)
            ? "(sin proyecto)"
            : context.RootName + " | " + context.WorkingDirectory;
    }
}