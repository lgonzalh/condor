using Condor.Cli.Presentation;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Core.Serialization;

namespace Condor.Cli.Commands;

public static class BuildCommand
{
    public static async Task<int> ExecuteAsync(
        IBuildService buildService,
        IStateStore stateStore,
        string[] args,
        CancellationToken cancellationToken = default)
    {
        var outputJson = args.Contains("--json", StringComparer.OrdinalIgnoreCase);

        if (!outputJson)
        {
            RenderActivity();
        }

        var result = await buildService.ApplyPlanAsync(cancellationToken);

        await stateStore.SaveBuildAsync(result, cancellationToken);

        if (outputJson)
        {
            Console.WriteLine(BuildJson.Serialize(result));
        }
        else
        {
            Terminal.WriteLine();
            if (result.Status == DetectionStatus.NotDetected)
            {
                Terminal.WriteError("No hay cambios para aplicar.");
                Terminal.WriteDim(result.Reason ?? "Ejecuta 'condor contexto' y 'condor planear' primero.");
            }
            else if (result.Status == DetectionStatus.Limited)
            {
                Terminal.WriteWarning("Condor no pudo aplicar el plan.");
            }
            else
            {
                Terminal.WriteCyan("Condor aplico los cambios del plan.");
            }

            Terminal.WriteLine();
            BuildRenderer.RenderBuild(result);
        }

        return result.Status == DetectionStatus.NotDetected ? 1 : 0;
    }

    private static void RenderActivity()
    {
        Terminal.WriteInfo("Condor construye a partir del plan...");
        Terminal.WriteDim("  Cargando el plan de trabajo persistido");
        Terminal.WriteDim("  Derivando acciones de archivo");
        Terminal.WriteDim("  Aplicando cambios sobre el proyecto objetivo");
    }
}
