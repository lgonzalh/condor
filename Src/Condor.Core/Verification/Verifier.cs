using System;
using System.Collections.Generic;
using System.Linq;
using Condor.Core.Models;

namespace Condor.Core.Verification;

public static class Verifier
{
    private const string ReasonNoBuild =
        "No hay resultado de build. Ejecuta 'condor planear' y 'condor construir' primero.";

    private const string ReasonBuildDegraded =
        "El resultado de build esta degradado y no permite verificar cambios.";

    private const string ReasonNoBase =
        "No hay directorio de trabajo para verificar los cambios.";

    public static VerificationResult Verify(
        BuildResult? build,
        string workingDirectory,
        IReadOnlyDictionary<string, ProbedFile?> probed,
        VerificationLimits limits)
    {
        if (build is null || build.Status == DetectionStatus.NotDetected)
        {
            return NotDetected(ReasonNoBuild);
        }

        if (build.Status == DetectionStatus.Limited || build.Actions.Count == 0)
        {
            return Limited(build, workingDirectory, ReasonBuildDegraded);
        }

        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            return Limited(build, workingDirectory, ReasonNoBase);
        }

        var checks = new List<VerificationCheck>();
        var limitsApplied = new List<string>();
        var passed = 0;
        var failed = 0;
        var informative = 0;

        foreach (var action in build.Actions)
        {
            if (checks.Count >= limits.MaxChecks)
            {
                limitsApplied.Add(VerificationLimits.LimitChecks);
                break;
            }

            if (!IsValidRelativePath(action.RelativePath))
            {
                checks.Add(Check(action, VerificationCheck.StatusFailed, "Ruta fuera del proyecto objetivo."));
                failed++;
                continue;
            }

            var expectedContent = action.Content.Length > limits.MaxContentLength
                ? action.Content.Substring(0, limits.MaxContentLength)
                : action.Content;

            switch (action.Status)
            {
                case BuildAction.StatusApplied:
                    if (TryGetContent(probed, action.RelativePath, out var actual))
                    {
                        var match = string.Equals(actual, expectedContent, StringComparison.Ordinal);
                        checks.Add(match
                            ? Check(action, VerificationCheck.StatusPassed)
                            : Check(action, VerificationCheck.StatusFailed, "El contenido del archivo no coincide con el declarado."));
                        if (match)
                        {
                            passed++;
                        }
                        else
                        {
                            failed++;
                        }
                    }
                    else
                    {
                        checks.Add(Check(action, VerificationCheck.StatusFailed, "El archivo declarado como aplicado no existe."));
                        failed++;
                    }

                    break;

                case BuildAction.StatusOmitted:
                case BuildAction.StatusFailed:
                    checks.Add(Check(action, VerificationCheck.StatusInformative,
                        action.StatusReason ?? "Accion no aplicada segun el resultado de build."));
                    informative++;
                    break;

                default:
                    checks.Add(Check(action, VerificationCheck.StatusInformative,
                        "Estado de accion no reconocido; se registra como informativo."));
                    informative++;
                    break;
            }
        }

        return new VerificationResult
        {
            SchemaVersion = "1.0.0",
            Status = DetectionStatus.Detected,
            RootName = build.RootName,
            WorkingDirectory = workingDirectory,
            Objective = build.Objective,
            Checks = checks,
            Passed = passed,
            Failed = failed,
            Informative = informative,
            LimitsApplied = limitsApplied.Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static bool TryGetContent(
        IReadOnlyDictionary<string, ProbedFile?> probed,
        string relativePath,
        out string content)
    {
        content = string.Empty;

        if (probed.TryGetValue(relativePath, out var file) && file is not null)
        {
            content = file.Content ?? "";
            return true;
        }

        return false;
    }

    private static VerificationCheck Check(BuildAction action, string status, string? reason = null)
    {
        var id = action.Id;

        if (id.StartsWith("B", StringComparison.Ordinal) &&
            id.Length > 1 &&
            int.TryParse(id.Substring(1), out var number))
        {
            id = number.ToString();
        }

        return new VerificationCheck
        {
            Id = "V" + id,
            BuildActionId = action.Id,
            Kind = action.Kind,
            RelativePath = action.RelativePath,
            Status = status,
            Reason = reason,
            Evidence = action.Evidence
        };
    }

    private static VerificationResult NotDetected(string reason)
    {
        return new VerificationResult
        {
            SchemaVersion = "1.0.0",
            Status = DetectionStatus.NotDetected,
            Reason = reason,
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static VerificationResult Limited(BuildResult build, string workingDirectory, string reason)
    {
        return new VerificationResult
        {
            SchemaVersion = "1.0.0",
            Status = DetectionStatus.Limited,
            Reason = reason,
            RootName = build.RootName,
            WorkingDirectory = workingDirectory,
            Objective = build.Objective,
            GeneratedAtUtc = DateTime.UtcNow
        };
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
}
