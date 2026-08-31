using Condor.Cli.Presentation;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Core.Serialization;

namespace Condor.Cli.Commands;

public static class VerifyCommand
{
    public static async Task<int> ExecuteAsync(
        IVerificationService verificationService,
        IStateStore stateStore,
        string[] args,
        CancellationToken cancellationToken = default)
    {
        var outputJson = args.Contains("--json", StringComparer.OrdinalIgnoreCase);

        if (!outputJson)
        {
            RenderActivity();
        }

        var result = await verificationService.VerifyAsync(cancellationToken);

        await stateStore.SaveVerificationAsync(result, cancellationToken);

        if (outputJson)
        {
            Console.WriteLine(VerificationJson.Serialize(result));
        }
        else
        {
            Terminal.WriteLine();
            if (result.Status == DetectionStatus.NotDetected)
            {
                Terminal.WriteError("No hay cambios para verificar.");
                Terminal.WriteDim(result.Reason ?? "Ejecuta 'condor contexto', 'condor planear' y 'condor construir' primero.");
            }
            else if (result.Status == DetectionStatus.Limited)
            {
                Terminal.WriteWarning("Condor no pudo verificar los cambios.");
            }
            else
            {
                Terminal.WriteCyan("Condor verifico los cambios del proyecto.");
            }

            Terminal.WriteLine();
            VerifyRenderer.RenderVerification(result);
        }

        return result.Status == DetectionStatus.NotDetected ? 1 : 0;
    }

    private static void RenderActivity()
    {
        Terminal.WriteInfo("Condor verifica los cambios aplicados...");
        Terminal.WriteDim("  Cargando el resultado de build persistido");
        Terminal.WriteDim("  Leyendo el estado del proyecto objetivo");
        Terminal.WriteDim("  Comparando contenido e integridad");
    }
}
