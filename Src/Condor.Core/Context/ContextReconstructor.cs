using System.Text.RegularExpressions;
using Condor.Core.Models;

namespace Condor.Core.Context;

public static partial class ContextReconstructor
{
    private const string ReasonNoAssessment =
        "No hay assessment disponible o ilegible. Ejecuta 'condor analizar' primero.";

    private const string ReasonNoEvidence = "No existe evidencia suficiente de actividad previa.";

    private const string ReasonAccessDenied = "No fue posible leer un artefacto operativo.";

    public static ProjectContext Reconstruct(
        AssessmentResult? assessment,
        IReadOnlyList<OperativeArtifact> artifacts,
        ContextLimits limits)
    {
        if (assessment is null)
        {
            return Degraded(new ProjectContext
            {
                Status = DetectionStatus.NotDetected,
                Reason = ReasonNoAssessment
            });
        }

        var project = assessment.Project;
        var availableArtifacts = artifacts
            .Where(artifact => artifact.Status == DetectionStatus.Detected)
            .ToList();

        var limitsApplied = new List<string>();
        AddArtifactLimits(artifacts, limits, limitsApplied);
        AddLineLimit(artifacts, limits, limitsApplied);

        var summary = BuildSummary(project, availableArtifacts);
        var continuation = BuildContinuationPoint(project, availableArtifacts, limits);
        var risks = BuildRisks(project);
        var dependencies = BuildDependencies(assessment, project);
        var recommendations = BuildRecommendations(project, continuation, risks, limitsApplied, limits);

        var context = new ProjectContext
        {
            SchemaVersion = "1.0.0",
            Status = artifacts.Any(artifact => artifact.Status != DetectionStatus.Detected)
                ? DetectionStatus.Limited
                : DetectionStatus.Detected,
            Reason = BuildReason(artifacts),
            WorkingDirectory = assessment.WorkingDirectory,
            RootName = project?.RootName ?? "",
            Summary = summary,
            ContinuationPoint = continuation,
            Risks = risks,
            RelevantDependencies = dependencies,
            Recommendations = recommendations,
            LimitsApplied = OrderOrdinal(limitsApplied.Distinct(StringComparer.Ordinal)),
            GeneratedAtUtc = DateTime.UtcNow
        };

        return context;
    }

    private static ProjectContext Degraded(ProjectContext context)
    {
        context.GeneratedAtUtc = DateTime.UtcNow;
        return context;
    }

    private static ProjectContextSummary BuildSummary(
        ProjectProfile? project,
        List<OperativeArtifact> availableArtifacts)
    {
        var summary = new ProjectContextSummary
        {
            Languages = project is null
                ? new List<string>()
                : OrderOrdinal(project.Languages.Select(language => language.Name)),
            Frameworks = project is null
                ? new List<string>()
                : OrderOrdinal(project.Frameworks.Select(framework => framework.Name)),
            ManifestCount = project?.Manifests.Count ?? 0,
            DocumentationCount = project?.Documentation.Count ?? 0,
            IsGitRepository = project?.IsGitRepository ?? false,
            GitBranch = project?.Git?.Branch,
            GitIsDirty = project?.Git?.IsDirty ?? false,
            LastCommits = project is null || project.Git is null
                ? new List<string>()
                : project.Git.Commits.Select(commit => commit.Hash + " " + commit.Subject).ToList(),
            HasOperativeArtifacts = availableArtifacts.Count > 0
        };

        return summary;
    }

    private static ContinuationPoint BuildContinuationPoint(
        ProjectProfile? project,
        List<OperativeArtifact> artifacts,
        ContextLimits limits)
    {
        var evidence = new List<string>();
        var suggestedNext = FindSuggestedNext(artifacts, limits, evidence);
        var pendingWork = FindPendingWork(artifacts, limits, evidence);
        var lastActivity = BuildLastActivity(artifacts, project, evidence);

        if (evidence.Count == 0)
        {
            var reason = project is null
                ? "No hay proyecto descubierto ni artefactos operativos."
                : ReasonNoEvidence;

            return new ContinuationPoint
            {
                Status = DetectionStatus.NotDetected,
                Reason = reason,
                Evidence = new List<string>(),
                PendingWork = new List<string>()
            };
        }

        return new ContinuationPoint
        {
            Status = DetectionStatus.Detected,
            Evidence = evidence,
            LastActivity = lastActivity,
            PendingWork = pendingWork,
            SuggestedNext = suggestedNext
        };
    }

    private static string? FindSuggestedNext(
        List<OperativeArtifact> artifacts,
        ContextLimits limits,
        List<string> evidence)
    {
        foreach (var artifact in artifacts)
        {
            var lines = SplitLines(artifact);
            var scanned = Math.Min(lines.Count, limits.MaxScannedLinesPerArtifact);

            for (var i = 0; i < scanned; i++)
            {
                if (!ContainsOrdinal(lines[i], "siguiente"))
                {
                    continue;
                }

                var upper = Math.Min(i + 5, scanned);
                for (var j = i + 1; j < upper; j++)
                {
                    var candidate = NormalizeTaskLine(lines[j]);
                    if (candidate is null)
                    {
                        continue;
                    }

                    evidence.Add(artifact.RelativePath + " linea " + (j + 1) + ": " + candidate);
                    return candidate;
                }
            }
        }

        return null;
    }

    private static List<string> FindPendingWork(
        List<OperativeArtifact> artifacts,
        ContextLimits limits,
        List<string> evidence)
    {
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var artifact in artifacts)
        {
            var lines = SplitLines(artifact);
            var scanned = Math.Min(lines.Count, limits.MaxScannedLinesPerArtifact);

            for (var i = 0; i < scanned; i++)
            {
                var line = lines[i];
                if (!ContainsOrdinal(line, "pendiente") || !TaskIdRegex().IsMatch(line))
                {
                    continue;
                }

                var task = NormalizeTaskLine(line);
                if (task is null || !seen.Add(task))
                {
                    continue;
                }

                found.Add(task);
                evidence.Add(artifact.RelativePath + " linea " + (i + 1) + ": " + task);
            }
        }

        return OrderOrdinal(found).Take(limits.MaxPendingTasks).ToList();
    }

    private static string? BuildLastActivity(
        List<OperativeArtifact> artifacts,
        ProjectProfile? project,
        List<string> evidence)
    {
        foreach (var artifact in artifacts)
        {
            if (artifact.Kind != OperativeArtifactKind.RegistroCambios)
            {
                continue;
            }

            var lines = SplitLines(artifact);
            string? lastLine = null;
            var lastIndex = -1;

            for (var i = 0; i < lines.Count; i++)
            {
                if (ChangeIdRegex().IsMatch(lines[i]))
                {
                    lastLine = lines[i].Trim();
                    lastIndex = i;
                }
            }

            if (lastIndex < 0)
            {
                continue;
            }

            var next = lines.Skip(lastIndex + 1)
                .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));

            var activity = next is null
                ? "ultimo cambio registrado: " + lastLine
                : "ultimo cambio registrado: " + lastLine + " | " + next.Trim();

            evidence.Add(artifact.RelativePath + ": " + activity);
            return activity;
        }

        if (project?.Git is { Commits.Count: > 0 } git)
        {
            var commit = git.Commits[0];
            var activity = "ultimo cambio Git: " + commit.Hash + " " + commit.Subject;
            evidence.Add("Git: " + activity);
            return activity;
        }

        return null;
    }

    private static List<ContextRisk> BuildRisks(ProjectProfile? project)
    {
        if (project is null)
        {
            return new List<ContextRisk>();
        }

        var risks = new List<ContextRisk>();

        if (!project.IsGitRepository)
        {
            risks.Add(new ContextRisk
            {
                Kind = "sin-git",
                Severity = "alta",
                Evidence = project.RootPath
            });
        }

        if (project.Manifests.Any(manifest => manifest.ParseError))
        {
            risks.Add(new ContextRisk
            {
                Kind = "manifiesto-error",
                Severity = "alta",
                Evidence = string.Join(", ", project.Manifests
                    .Where(manifest => manifest.ParseError)
                    .Select(manifest => manifest.Path))
            });
        }

        if (project.Status != DetectionStatus.Detected)
        {
            risks.Add(new ContextRisk
            {
                Kind = "perfil-degradado",
                Severity = "alta",
                Evidence = project.Reason ?? project.Status.ToString()
            });
        }

        if (project.IsGitRepository && project.Git is { IsDirty: true })
        {
            risks.Add(new ContextRisk
            {
                Kind = "git-sucio",
                Severity = "media",
                Evidence = "rama " + (project.Git.Branch ?? "(sin rama)")
            });
        }

        if (project.Languages.Count == 0)
        {
            risks.Add(new ContextRisk
            {
                Kind = "sin-senales-lenguaje",
                Severity = "media",
                Evidence = "ninguna senal de lenguaje identificada"
            });
        }

        if (project.FilesCount == 0 && project.DirectoriesCount == 0)
        {
            risks.Add(new ContextRisk
            {
                Kind = "proyecto-vacio",
                Severity = "media",
                Evidence = "0 archivos y 0 directorios"
            });
        }

        if (project.TotalSizeExceeded)
        {
            risks.Add(new ContextRisk
            {
                Kind = "volumen-excedido",
                Severity = "media",
                Evidence = "TotalSizeExceeded: true"
            });
        }

        if (project.Documentation.Count == 0)
        {
            risks.Add(new ContextRisk
            {
                Kind = "documentacion-ausente",
                Severity = "baja",
                Evidence = "sin documentacion detectada"
            });
        }

        return risks
            .OrderByDescending(risk => SeverityRank(risk.Severity))
            .ThenBy(risk => risk.Kind, StringComparer.Ordinal)
            .ToList();
    }

    private static List<RelevantDependency> BuildDependencies(
        AssessmentResult assessment,
        ProjectProfile? project)
    {
        var dependencies = new List<RelevantDependency>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (project is not null)
        {
            foreach (var manifest in project.Manifests)
            {
                foreach (var dependency in manifest.Dependencies)
                {
                    if (!seen.Add(dependency))
                    {
                        continue;
                    }

                    dependencies.Add(new RelevantDependency
                    {
                        Name = dependency,
                        Source = "Manifest",
                        Detail = manifest.Path
                    });
                }
            }
        }

        AddTool(assessment.Tools.Git, dependencies, seen);
        AddTool(assessment.Tools.Ollama, dependencies, seen);

        foreach (var tool in assessment.Tools.DetectedTools)
        {
            if (tool.Status != DetectionStatus.Detected)
            {
                continue;
            }

            var name = tool.Name;
            if (!seen.Add(name))
            {
                continue;
            }

            dependencies.Add(new RelevantDependency
            {
                Name = name,
                Source = "Tools",
                Detail = tool.Version ?? "instalado"
            });
        }

        return dependencies
            .OrderBy(dependency => dependency.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static void AddTool(
        ToolInfo tool,
        List<RelevantDependency> dependencies,
        HashSet<string> seen)
    {
        if (tool.Status != DetectionStatus.Detected || !seen.Add(tool.Name))
        {
            return;
        }

        dependencies.Add(new RelevantDependency
        {
            Name = tool.Name,
            Source = "Tools",
            Detail = tool.Version ?? "instalado"
        });
    }

    private static void AddTool(
        OllamaStatus ollama,
        List<RelevantDependency> dependencies,
        HashSet<string> seen)
    {
        if (!ollama.Installed || !seen.Add("Ollama"))
        {
            return;
        }

        dependencies.Add(new RelevantDependency
        {
            Name = "Ollama",
            Source = "Tools",
            Detail = ollama.ServerRunning ? "servidor activo" : "instalado, servidor inactivo"
        });
    }

    private static List<PlannerRecommendation> BuildRecommendations(
        ProjectProfile? project,
        ContinuationPoint continuation,
        List<ContextRisk> risks,
        List<string> limitsApplied,
        ContextLimits limits)
    {
        var recommendations = new List<PlannerRecommendation>();

        if (project is not null)
        {
            AddRecommendation(recommendations, risks, "manifiesto-error",
                "Revisa los manifiestos con error de parseo antes de planificar.");

            AddRecommendation(recommendations, risks, "perfil-degradado",
                "Revisa las degradaciones del perfil del proyecto antes de continuar.");

            AddRecommendation(recommendations, risks, "git-sucio",
                "Considera el estado sucio del repositorio en el plan.");

            AddRecommendation(recommendations, risks, "sin-git",
                "El proyecto no tiene repositorio Git; considera inicializar versionado.");

            AddRecommendation(recommendations, risks, "volumen-excedido",
                "Planifica con los limites de volumen ya aplicados al descubrimiento.");

            AddRecommendation(recommendations, risks, "sin-senales-lenguaje",
                "Confirma la estructura del proyecto antes de planificar.");

            AddRecommendation(recommendations, risks, "documentacion-ausente",
                "Incorpora documentacion basica antes de continuar.");
        }

        if (continuation.Status == DetectionStatus.NotDetected)
        {
            recommendations.Add(new PlannerRecommendation
            {
                Text = "Define el punto de inicio con el usuario; no existe evidencia de continuacion.",
                Evidence = continuation.Reason ?? ReasonNoEvidence
            });
        }

        if (limitsApplied.Contains(ContextLimits.LimitArtifactSize, StringComparer.Ordinal))
        {
            recommendations.Add(new PlannerRecommendation
            {
                Text = "Algun artefacto operativo supero el limite de tamano y quedo fuera del contexto.",
                Evidence = ContextLimits.LimitArtifactSize
            });
        }

        return recommendations
            .Take(limits.MaxRecommendations)
            .ToList();
    }

    private static void AddRecommendation(
        List<PlannerRecommendation> recommendations,
        List<ContextRisk> risks,
        string kind,
        string text)
    {
        var risk = risks.FirstOrDefault(candidate => candidate.Kind == kind);
        if (risk is null)
        {
            return;
        }

        recommendations.Add(new PlannerRecommendation
        {
            Text = text,
            Evidence = risk.Evidence
        });
    }

    private static void AddArtifactLimits(
        IReadOnlyList<OperativeArtifact> artifacts,
        ContextLimits limits,
        List<string> limitsApplied)
    {
        foreach (var artifact in artifacts)
        {
            if (artifact.Status == DetectionStatus.Detected)
            {
                continue;
            }

            if (string.Equals(artifact.Reason, "supera el limite de tamano", StringComparison.Ordinal))
            {
                limitsApplied.Add(ContextLimits.LimitArtifactSize);
            }
            else
            {
                limitsApplied.Add("artifact-access");
            }
        }
    }

    private static void AddLineLimit(
        IReadOnlyList<OperativeArtifact> artifacts,
        ContextLimits limits,
        List<string> limitsApplied)
    {
        if (artifacts.Any(artifact =>
                artifact.Status == DetectionStatus.Detected &&
                SplitLines(artifact).Count > limits.MaxScannedLinesPerArtifact))
        {
            limitsApplied.Add(ContextLimits.LimitLines);
        }
    }

    private static string? BuildReason(IReadOnlyList<OperativeArtifact> artifacts)
    {
        var degraded = artifacts
            .Where(artifact => artifact.Status != DetectionStatus.Detected)
            .Select(artifact => artifact.RelativePath + ": " + artifact.Reason)
            .ToList();

        if (degraded.Count == 0)
        {
            return null;
        }

        return ReasonAccessDenied + " " + string.Join(" | ", degraded);
    }

    private static IReadOnlyList<string> SplitLines(OperativeArtifact artifact)
    {
        return artifact.Content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Split('\n');
    }

    private static string? NormalizeTaskLine(string line)
    {
        if (!TaskIdRegex().IsMatch(line))
        {
            return null;
        }

        var candidate = line.Trim().Trim('`', '*').Trim();

        return string.IsNullOrWhiteSpace(candidate) ? null : candidate;
    }

    private static bool ContainsOrdinal(string text, string value)
    {
        return text.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    private static int SeverityRank(string severity)
    {
        return severity switch
        {
            "alta" => 3,
            "baja" => 1,
            _ => 2
        };
    }

    private static List<string> OrderOrdinal(IEnumerable<string> values)
    {
        return values.OrderBy(value => value, StringComparer.Ordinal).ToList();
    }

    [GeneratedRegex(@"T-\d{3}")]
    private static partial Regex TaskIdRegex();

    [GeneratedRegex(@"CH-\d{3}")]
    private static partial Regex ChangeIdRegex();
}