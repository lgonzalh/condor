using Condor.Cli.Presentation;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Core.Serialization;

namespace Condor.Cli.Commands;

public static class AdvanceCommand
{
    public static async Task<int> ExecuteAsync(
        ICycleService cycleService,
        IStateStore stateStore,
        string[] args,
        CancellationToken cancellationToken = default)
    {
        var outputJson = args.Contains("--json", StringComparer.OrdinalIgnoreCase);
        var request = BuildRequest(args, outputJson);

        if (!outputJson)
        {
            RenderActivity();
        }

        var result = await cycleService.AdvanceAsync(request, cancellationToken);

        await stateStore.SaveCycleAsync(result, cancellationToken);

        if (outputJson)
        {
            Console.WriteLine(CycleJson.Serialize(result));
        }
        else
        {
            Terminal.WriteLine();
            if (result.Status == DetectionStatus.NotDetected)
            {
                Terminal.WriteError("No hay avance disponible.");
                Terminal.WriteDim(result.Reason ?? "Ejecuta 'condor contexto' y 'condor analizar' primero.");
            }
            else if (result.Status == DetectionStatus.Limited)
            {
                Terminal.WriteWarning("Condor avanzo con limitaciones.");
            }
            else
            {
                Terminal.WriteCyan("Condor completo un avance del ciclo de ingenieria.");
            }

            Terminal.WriteLine();
            AdvanceRenderer.RenderAdvance(result);
        }

        return result.Status == DetectionStatus.NotDetected ? 1 : 0;
    }

    private static string BuildRequest(string[] args, bool outputJson)
    {
        var request = new System.Text.StringBuilder();

        foreach (var arg in args)
        {
            if (string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (request.Length > 0)
            {
                request.Append(' ');
            }

            request.Append(arg);
        }

        return request.ToString().Trim();
    }

    private static void RenderActivity()
    {
        Terminal.WriteInfo("Condor ejecuta el ciclo de ingenieria...");
        Terminal.WriteDim("  Planificando (Planner)");
        Terminal.WriteDim("  Construyendo (Builder)");
        Terminal.WriteDim("  Verificando (Verifier)");
    }
}
