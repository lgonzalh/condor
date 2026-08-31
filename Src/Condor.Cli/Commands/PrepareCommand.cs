using Condor.Cli.Presentation;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Core.Serialization;

namespace Condor.Cli.Commands;

public static class PrepareCommand
{
    public static async Task<int> ExecuteAsync(
        ISetupService setupService,
        IModelAutoSetupService? modelAutoSetup,
        string[] args,
        CancellationToken cancellationToken = default)
    {
        var outputJson = args.Contains("--json", StringComparer.OrdinalIgnoreCase);
        var refresh = args.Contains("--actualizar", StringComparer.OrdinalIgnoreCase);

        if (!outputJson)
        {
            RenderActivity();
        }

        var result = await setupService.PrepareAsync(refresh, cancellationToken);

        ModelSelectionResult? model = null;
        if (modelAutoSetup is not null)
        {
            model = await modelAutoSetup.EnsureModelAsync(null, cancellationToken);
        }

        if (outputJson)
        {
            Console.WriteLine(SetupJson.Serialize(result));
        }
        else
        {
            Terminal.WriteLine();
            if (result.Status == DetectionStatus.Detected)
            {
                Terminal.WriteCyan("Condor esta listo para operar.");
            }
            else
            {
                Terminal.WriteWarning("Condor indica dependencias o estado pendiente.");
            }

            Terminal.WriteLine();
            PrepareRenderer.RenderPrepare(result);

            if (model is not null)
            {
                Terminal.WriteLine();
                ModelSetupRenderer.RenderModel(model);
            }
        }

        return result.Status == DetectionStatus.Detected ? 0 : 1;
    }

    private static void RenderActivity()
    {
        Terminal.WriteInfo("Condor prepara el entorno...");
        Terminal.WriteDim("  Leyendo el Assessment del entorno");
        Terminal.WriteDim("  Verificando dependencias y estado local");
        Terminal.WriteDim("  Asegurando el modelo LLM local");
    }
}
