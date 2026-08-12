using Condor.Cli.Presentation;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Core.Serialization;

namespace Condor.Cli.Commands;

public static class ContextCommand
{
    public static async Task<int> ExecuteAsync(
        IContextService contextService,
        IStateStore stateStore,
        string[] args,
        CancellationToken cancellationToken = default)
    {
        var outputJson = args.Contains("--json", StringComparer.OrdinalIgnoreCase);

        if (!outputJson)
        {
            RenderActivity();
        }

        var context = await contextService.BuildContextAsync(cancellationToken);

        await stateStore.SaveContextAsync(context, cancellationToken);

        if (outputJson)
        {
            Console.WriteLine(ContextJson.Serialize(context));
        }
        else
        {
            Terminal.WriteLine();
            if (context.Status == DetectionStatus.NotDetected)
            {
                Terminal.WriteError("No hay contexto operativo disponible.");
                Terminal.WriteDim(context.Reason ?? "Ejecuta 'condor analizar' primero.");
            }
            else
            {
                Terminal.WriteSuccess("Condor reconstruyo el contexto del proyecto.");
            }

            Terminal.WriteLine();
            ContextRenderer.RenderSummary(context);
        }

        return context.Status == DetectionStatus.NotDetected ? 1 : 0;
    }

    private static void RenderActivity()
    {
        Terminal.WriteInfo("Condor reconstruye el contexto del proyecto...");
        Terminal.WriteDim("  Cargando el assessment persistido");
        Terminal.WriteDim("  Leyendo artefactos operativos (operacion/)");
        Terminal.WriteDim("  Detectando el punto de continuacion");
    }
}
