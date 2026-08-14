using Condor.Core.Models;

namespace Condor.Cli.Presentation;

public static class ExamineRenderer
{
    public static void RenderExamine(VisionResult result)
    {
        Terminal.WriteHeader("EXAMINAR");

        Terminal.WriteLine("  Estado     : " + EstadoLine(result));
        Terminal.WriteLine("  Imagen     : " + (result.ImagePath.Length > 0 ? result.ImagePath : "(sin imagen)") +
                           (result.ImageBytes > 0 ? " (" + result.ImageBytes + " bytes)" : ""));
        Terminal.WriteLine("  Modelo     : " + (result.ModelUsed.Length > 0 ? result.ModelUsed : "(no usado)"));
        Terminal.WriteLine("  Descripcion: " + (result.Description.Length > 0 ? result.Description : "(sin descripcion)"));

        Terminal.WriteLine(
            "  Limites    : " +
            (result.LimitsApplied.Count > 0 ? string.Join(", ", result.LimitsApplied) : "ninguno"));

        if (result.Status != DetectionStatus.Detected && !string.IsNullOrWhiteSpace(result.Reason))
        {
            Terminal.WriteWarning("  - Vision: " + result.Reason);
        }
    }

    private static string EstadoLine(VisionResult result)
    {
        return result.Status switch
        {
            DetectionStatus.Detected => "detectado",
            DetectionStatus.NotDetected => "no detectado",
            DetectionStatus.Limited => "limitado",
            _ => "error"
        };
    }
}
