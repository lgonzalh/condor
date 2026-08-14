using Condor.Core.Models;

namespace Condor.Cli.Presentation;

public static class PrepareRenderer
{
    public static void RenderPrepare(SetupResult result)
    {
        Terminal.WriteHeader("PREPARAR");

        Terminal.WriteLine("  Estado            : " + EstadoLine(result));
        Terminal.WriteLine("  Plataforma        : " + result.Platform);
        Terminal.WriteLine("  Obligatorias      : " + result.RequiredPresent + "/" + result.RequiredTotal);
        Terminal.WriteLine("  Opcionales        : " + result.OptionalPresent + "/" + result.OptionalTotal);
        Terminal.WriteLine("  Estado local      : " + (result.StateUsable ? "usable" : "pendiente") +
                           " (" + result.StateDirectory + ")");

        RenderDependencies(result.Dependencies);

        Terminal.WriteLine(
            "  Limites aplicados : " +
            (result.LimitsApplied.Count > 0 ? string.Join(", ", result.LimitsApplied) : "ninguno"));

        if (result.Status != DetectionStatus.Detected && !string.IsNullOrWhiteSpace(result.Reason))
        {
            Terminal.WriteWarning("  - Preparacion: " + result.Reason);
        }
    }

    private static void RenderDependencies(List<SetupDependency> dependencies)
    {
        Terminal.WriteLine("  Dependencias      : " + (dependencies.Count > 0 ? dependencies.Count.ToString() : "ninguna"));

        foreach (var dep in dependencies)
        {
            var tag = dep.IsRequired ? "obligatoria" : "opcional";
            var state = dep.Present ? "presente" : "ausente";
            Terminal.WriteLine("    - [" + tag + "] " + dep.Name + " : " + state);

            if (!dep.Present && !string.IsNullOrWhiteSpace(dep.Guidance))
            {
                Terminal.WriteDim("      " + dep.Guidance);
            }
            else if (dep.Reason is { Length: > 0 })
            {
                Terminal.WriteDim("      " + dep.Reason);
            }
        }
    }

    private static string EstadoLine(SetupResult result)
    {
        return result.Status switch
        {
            DetectionStatus.Detected => "listo",
            DetectionStatus.NotDetected => "no listo",
            DetectionStatus.Limited => "limitado",
            _ => "error"
        };
    }
}
