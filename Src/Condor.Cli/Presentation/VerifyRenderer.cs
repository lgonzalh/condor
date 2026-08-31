using Condor.Core.Models;

namespace Condor.Cli.Presentation;

public static class VerifyRenderer
{
    public static void RenderVerification(VerificationResult result)
    {
        Terminal.WriteHeader("VERIFICACION");

        Terminal.WriteLine("  Estado            : " + EstadoLine(result));
        Terminal.WriteLine("  Proyecto          : " + RootLine(result));
        Terminal.WriteLine("  Objetivo          : " + (result.Objective.Length > 0 ? result.Objective : "(sin objetivo)"));
        Terminal.WriteLine("  Resumen           : " + result.Passed + " pasadas, " +
                           result.Failed + " fallidas, " + result.Informative + " informativas");

        RenderChecks(result.Checks);

        Terminal.WriteLine(
            "  Limites aplicados : " +
            (result.LimitsApplied.Count > 0 ? string.Join(", ", result.LimitsApplied) : "ninguno"));

        if (result.Status != DetectionStatus.Detected && !string.IsNullOrWhiteSpace(result.Reason))
        {
            Terminal.WriteWarning("  - Verificacion: " + result.Reason);
        }
    }

    private static void RenderChecks(List<VerificationCheck> checks)
    {
        Terminal.WriteLine("  Comprobaciones    : " + (checks.Count > 0 ? checks.Count.ToString() : "ninguna"));

        foreach (var check in checks)
        {
            Terminal.WriteLine("    - [" + KindLine(check.Kind) + "] " + check.Id + " " + check.RelativePath);

            if (check.Status == VerificationCheck.StatusPassed)
            {
                Terminal.WriteDim("      estado: " + check.Status);
            }
            else
            {
                Terminal.WriteWarning("      estado: " + check.Status + (check.Reason is { Length: > 0 } r ? " - " + r : ""));
            }
        }
    }

    private static string EstadoLine(VerificationResult result)
    {
        return result.Status switch
        {
            DetectionStatus.Detected => "detectado",
            DetectionStatus.NotDetected => "no detectado",
            DetectionStatus.Limited => "limitado",
            _ => "error"
        };
    }

    private static string RootLine(VerificationResult result)
    {
        return string.IsNullOrWhiteSpace(result.RootName)
            ? "(sin proyecto)"
            : result.RootName + " - " + result.WorkingDirectory;
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
