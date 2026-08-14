using Condor.Core.Models;

namespace Condor.Cli.Presentation;

public static class SemanticVerificationRenderer
{
    public static void Render(SemanticVerificationResult result)
    {
        Terminal.WriteHeader("VERIFICACION_SEMANTICA");

        Terminal.WriteLine("  Estado            : " + EstadoLine(result));
        Terminal.WriteLine("  Proyecto          : " + RootLine(result));

        RenderChecks(result.Checks);

        Terminal.WriteLine(
            "  Limites aplicados : " +
            (result.LimitsApplied.Count > 0 ? string.Join(", ", result.LimitsApplied) : "ninguno"));

        if (result.Status != DetectionStatus.Detected && !string.IsNullOrWhiteSpace(result.Reason))
        {
            Terminal.WriteWarning("  - Verificacion: " + result.Reason);
        }
    }

    private static void RenderChecks(List<SemanticCheck> checks)
    {
        Terminal.WriteLine("  Comprobaciones    : " + (checks.Count > 0 ? checks.Count.ToString() : "ninguna"));

        foreach (var check in checks)
        {
            var header = "    - [" + check.Kind + "] " + check.Status;

            if (check.ExitCode is not null)
            {
                header += " (exit " + check.ExitCode + ")";
            }

            Terminal.WriteLine(header);

            if (check.Reason is { Length: > 0 })
            {
                Terminal.WriteDim("      " + check.Reason);
            }

            if (check.Output.Length > 0)
            {
                Terminal.WriteDim("      " + Snippet(check.Output));
            }
        }
    }

    private static string Snippet(string output)
    {
        var trimmed = output.Trim();

        if (trimmed.Length <= 300)
        {
            return trimmed;
        }

        return trimmed.Substring(0, 300).TrimEnd();
    }

    private static string EstadoLine(SemanticVerificationResult result)
    {
        return result.Status switch
        {
            DetectionStatus.Detected => "detectado",
            DetectionStatus.NotDetected => "no detectado",
            DetectionStatus.Limited => "limitado",
            _ => "error"
        };
    }

    private static string RootLine(SemanticVerificationResult result)
    {
        return string.IsNullOrWhiteSpace(result.RootName)
            ? (result.WorkingDirectory.Length > 0 ? result.WorkingDirectory : "(sin proyecto)")
            : result.RootName + " | " + result.WorkingDirectory;
    }
}
