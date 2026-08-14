using System;
using System.Collections.Generic;
using System.Linq;
using Condor.Core.Models;

namespace Condor.Core.Setup;

public static class SetupEvaluator
{
    private const string ReasonAssessmentMissing =
        "No hay Assessment disponible. Ejecuta 'condor analizar' para detectar el entorno.";

    public static SetupResult Evaluate(
        AssessmentResult? assessment,
        string stateDirectory,
        bool stateDirectoryExists,
        bool stateUsable,
        string? stateReason,
        SetupLimits limits)
    {
        var dependencies = new List<SetupDependency>();
        var limitsApplied = new List<string>();

        if (assessment is null)
        {
            return NotDetected(ReasonAssessmentMissing, stateDirectory, stateDirectoryExists, stateUsable, stateReason);
        }

        // Obligatorias
        BuildRequired(dependencies, assessment);

        // Opcionales
        BuildOptional(dependencies, assessment);

        if (dependencies.Count > limits.MaxDependencies)
        {
            limitsApplied.Add(SetupLimits.LimitDependencies);
        }

        var requiredPresent = dependencies.Count(d => d.IsRequired && d.Present);
        var requiredTotal = dependencies.Count(d => d.IsRequired);
        var optionalPresent = dependencies.Count(d => !d.IsRequired && d.Present);
        var optionalTotal = dependencies.Count(d => !d.IsRequired);

        var status = ResolveStatus(requiredPresent, requiredTotal, stateDirectoryExists, stateUsable, assessment);

        return new SetupResult
        {
            SchemaVersion = "1.0.0",
            Status = status,
            Platform = "windows",
            RequiredPresent = requiredPresent,
            RequiredTotal = requiredTotal,
            OptionalPresent = optionalPresent,
            OptionalTotal = optionalTotal,
            Dependencies = dependencies.Take(limits.MaxDependencies).ToList(),
            StateDirectory = stateDirectory,
            StateUsable = stateUsable,
            StateReason = stateReason,
            LimitsApplied = limitsApplied.Distinct(StringComparer.Ordinal)
                .OrderBy(v => v, StringComparer.Ordinal)
                .ToList(),
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static void BuildRequired(List<SetupDependency> dependencies, AssessmentResult assessment)
    {
        var dotnet = HasTool(assessment, "dotnet");
        dependencies.Add(new SetupDependency
        {
            Name = "Runtime de .NET",
            IsRequired = true,
            Present = dotnet,
            Reason = dotnet ? null : "No se detecto el runtime de .NET necesario para ejecutar Condor.",
            Guidance = dotnet
                ? "Listo."
                : "Instala manualmente el runtime de .NET requerido por Condor."
        });

        var currentRuntime = IsCurrentRuntimeDetected(assessment);
        dependencies.Add(new SetupDependency
        {
            Name = "Capacidad para ejecutar Condor",
            IsRequired = true,
            Present = currentRuntime,
            Reason = currentRuntime ? null : "No se pudo confirmar la capacidad de ejecutar Condor.",
            Guidance = currentRuntime ? "Listo." : "Asegurate de contar con el entorno de ejecucion de Condor."
        });
    }

    private static void BuildOptional(List<SetupDependency> dependencies, AssessmentResult assessment)
    {
        var ollama = assessment.Tools?.Ollama is not null &&
                      (assessment.Tools.Ollama.Installed || assessment.Capabilities.LocalLlm);
        dependencies.Add(new SetupDependency
        {
            Name = "Ollama (modelos locales)",
            IsRequired = false,
            Present = ollama,
            Reason = ollama ? null : "Ollama no fue detectado.",
            Guidance = ollama ? "Listo." : "Opcional: instala Ollama para usar modelos locales."
        });

        var hasModels = (assessment.Tools?.Ollama?.Models?.Count ?? 0) > 0;
        dependencies.Add(new SetupDependency
        {
            Name = "Modelos locales",
            IsRequired = false,
            Present = hasModels,
            Reason = hasModels ? null : "No se detectaron modelos locales.",
            Guidance = hasModels ? "Listo." : "Opcional: descarga un modelo compatible con 'condor recomendar'."
        });

        var gpu = assessment.Capabilities?.GpuDetected ?? false;
        dependencies.Add(new SetupDependency
        {
            Name = "GPU",
            IsRequired = false,
            Present = gpu,
            Reason = gpu ? null : "No se detecto una GPU.",
            Guidance = gpu ? "Listo." : "Opcional: una GPU amplia capacidades."
        });

        var git = assessment.Tools?.Git is not null &&
                  assessment.Tools.Git.Status == DetectionStatus.Detected;
        dependencies.Add(new SetupDependency
        {
            Name = "Git",
            IsRequired = false,
            Present = git,
            Reason = git ? null : "Git no fue detectado.",
            Guidance = git ? "Listo." : "Opcional: Git permite trazar los proyectos."
        });

        var tools = (assessment.Tools?.DetectedTools?.Count ?? 0) > 0;
        dependencies.Add(new SetupDependency
        {
            Name = "Herramientas de desarrollo",
            IsRequired = false,
            Present = tools,
            Reason = tools ? null : "No se detectaron herramientas de desarrollo.",
            Guidance = tools ? "Listo." : "Opcional: herramientas del entorno amplian el discovery."
        });
    }

    private static DetectionStatus ResolveStatus(
        int requiredPresent,
        int requiredTotal,
        bool stateExists,
        bool stateUsable,
        AssessmentResult assessment)
    {
        if (requiredPresent < requiredTotal)
        {
            return DetectionStatus.NotDetected;
        }

        if (!stateExists || !stateUsable)
        {
            return DetectionStatus.Limited;
        }

        return DetectionStatus.Detected;
    }

    private static bool HasTool(AssessmentResult assessment, string toolName)
    {
        return (assessment.Tools?.DetectedTools ?? new List<ToolInfo>())
            .Any(t => string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase) &&
                      t.Status == DetectionStatus.Detected);
    }

    private static bool IsCurrentRuntimeDetected(AssessmentResult assessment)
    {
        return (assessment.Tools?.DetectedTools ?? new List<ToolInfo>())
            .Any(t => t.Status == DetectionStatus.Detected &&
                      string.Equals(t.Name, "dotnet", StringComparison.OrdinalIgnoreCase));
    }

    private static SetupResult NotDetected(
        string reason,
        string stateDirectory,
        bool stateExists,
        bool stateUsable,
        string? stateReason)
    {
        return new SetupResult
        {
            SchemaVersion = "1.0.0",
            Status = DetectionStatus.NotDetected,
            Reason = reason,
            Platform = "windows",
            StateDirectory = stateDirectory,
            StateUsable = stateUsable,
            StateReason = stateReason,
            GeneratedAtUtc = DateTime.UtcNow
        };
    }
}
