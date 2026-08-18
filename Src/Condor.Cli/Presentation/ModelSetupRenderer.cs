using Condor.Core.Models;

namespace Condor.Cli.Presentation;

public static class ModelSetupRenderer
{
    public static void RenderModel(ModelSelectionResult result)
    {
        Terminal.WriteHeader("MODELO");

        Terminal.WriteLine("  Deseado           : " + (result.Desired?.PullName ?? "(ninguno)"));

        if (result.AlreadyInstalled && result.InstalledName is { Length: > 0 })
        {
            Terminal.WriteLine("  Estado            : disponible");
            Terminal.WriteDim("    utilizado: " + result.InstalledName);
        }
        else
        {
            Terminal.WriteLine("  Estado            : no disponible");
        }

        if (result.Reason is { Length: > 0 })
        {
            Terminal.WriteDim("  Motivo            : " + result.Reason);
        }

        foreach (var limitation in result.Limitations)
        {
            Terminal.WriteWarning("  - " + limitation);
        }

        if (result.Alternatives.Count > 0)
        {
            Terminal.WriteDim("  Alternativas      : " + string.Join(", ", result.Alternatives));
        }
    }
}
