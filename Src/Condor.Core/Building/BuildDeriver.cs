using System;
using System.Collections.Generic;
using System.Linq;
using Condor.Core.Models;

namespace Condor.Core.Building;

public static class BuildDeriver
{
    private const string ReasonNoPlan =
        "No hay plan disponible. Ejecuta 'condor planear \"<solicitud>\"' primero.";

    private const string ReasonInstructive =
        "No hay plan disponible. Ejecuta 'condor contexto' y 'condor planear' primero.";

    private const string ReasonNoActions =
        "El plan no permite derivar acciones de implementacion. Declara rutas con '[ruta:...]' o '[archivo:...]' en las tareas.";

    private const string ReasonPlanDegraded =
        "El plan esta degradado y no permite derivar acciones de implementacion.";

    private static readonly string[] PathMarkers = { "[ruta:", "[archivo:" };
    private static readonly string[] UpdateTerms =
        { "modificar", "modifica", "actualizar", "actualiza", "corregir", "corrige", "mejorar", "mejora",
          "sobrescribir", "sobrescribe", "extender", "extiende", "refactoriza", "refactorizar" };

    public static BuildResult Derive(WorkPlan? plan, BuildLimits limits)
    {
        if (plan is null)
        {
            return NotDetected(ReasonNoPlan);
        }

        if (plan.Status == DetectionStatus.NotDetected)
        {
            return NotDetected(ReasonInstructive);
        }

        if (plan.Status == DetectionStatus.Limited || plan.Intention == "indefinida")
        {
            return Limited(plan, ReasonPlanDegraded);
        }

        var actions = new List<BuildAction>();
        var limitsApplied = new List<string>();

        foreach (var task in plan.Tasks)
        {
            if (actions.Count >= limits.MaxActions)
            {
                limitsApplied.Add(BuildLimits.LimitActions);
                break;
            }

            var relativePath = ExtractRelativePath(task);

            if (relativePath is null)
            {
                continue;
            }

            if (relativePath.Length > limits.MaxRelativePathLength)
            {
                limitsApplied.Add(BuildLimits.LimitPath);
                continue;
            }

            var content = BuildContent(task, limits);
            var kind = ClassifyKind(task.Title);

            actions.Add(new BuildAction
            {
                Id = "B" + actions.Count,
                Kind = kind,
                RelativePath = relativePath,
                Content = content,
                Evidence = NormalizeEvidence(task)
            });
        }

        if (actions.Count == 0)
        {
            return Limited(plan, ReasonNoActions);
        }

        return new BuildResult
        {
            SchemaVersion = "1.0.0",
            Status = DetectionStatus.Detected,
            RootName = plan.RootName,
            WorkingDirectory = plan.WorkingDirectory,
            Intention = plan.Intention,
            Objective = plan.Objective,
            Actions = actions,
            LimitsApplied = limitsApplied.Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static BuildResult NotDetected(string reason)
    {
        return new BuildResult
        {
            SchemaVersion = "1.0.0",
            Status = DetectionStatus.NotDetected,
            Reason = reason,
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static BuildResult Limited(WorkPlan plan, string reason)
    {
        return new BuildResult
        {
            SchemaVersion = "1.0.0",
            Status = DetectionStatus.Limited,
            Reason = reason,
            RootName = plan.RootName,
            WorkingDirectory = plan.WorkingDirectory,
            Intention = plan.Intention,
            Objective = plan.Objective,
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static string? ExtractRelativePath(PlanTask task)
    {
        var text = (task.Detail ?? "") + " " + task.Title;

        foreach (var marker in PathMarkers)
        {
            var index = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

            if (index < 0)
            {
                continue;
            }

            var start = index + marker.Length;
            var end = text.IndexOf(']', start);

            if (end < 0)
            {
                end = text.Length;
            }

            var path = text.Substring(start, end - start).Trim();

            if (IsValidRelativePath(path))
            {
                return path;
            }
        }

        return null;
    }

    private static string BuildContent(PlanTask task, BuildLimits limits)
    {
        var title = task.Title ?? "";
        var detail = task.Detail ?? "";
        var content = title.Trim() + (detail.Length > 0 ? "\n" + detail.Trim() : "");

        if (content.Length > limits.MaxContentLength)
        {
            content = content.Substring(0, limits.MaxContentLength).TrimEnd();
        }

        return content;
    }

    private static BuildActionKind ClassifyKind(string title)
    {
        var normalized = Normalize(title);

        if (UpdateTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal)))
        {
            return BuildActionKind.Actualizar;
        }

        return BuildActionKind.Crear;
    }

    private static string NormalizeEvidence(PlanTask task)
    {
        return string.IsNullOrWhiteSpace(task.Evidence)
            ? task.Title ?? ""
            : task.Evidence;
    }

    private static bool IsValidRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var trimmed = path.Trim();

        if (trimmed.StartsWith("/", StringComparison.Ordinal) ||
            trimmed.StartsWith("\\", StringComparison.Ordinal))
        {
            return false;
        }

        if (trimmed.Contains(":", StringComparison.Ordinal))
        {
            return false;
        }

        var segments = trimmed.Split(new[] { '/', '\\' }, StringSplitOptions.None);

        foreach (var segment in segments)
        {
            if (segment == ".." || segment == ".")
            {
                return false;
            }
        }

        return true;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrEmpty(value)
            ? value
            : value.ToLowerInvariant();
    }
}
