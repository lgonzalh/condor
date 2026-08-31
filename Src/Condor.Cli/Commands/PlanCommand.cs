using Condor.Cli.Presentation;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Core.Serialization;

namespace Condor.Cli.Commands;

public static class PlanCommand
{
    public static async Task<int> ExecuteAsync(
        IPlanService planService,
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

        var plan = await planService.BuildPlanAsync(request, cancellationToken);

        await stateStore.SavePlanAsync(plan, cancellationToken);

        if (outputJson)
        {
            Console.WriteLine(PlanJson.Serialize(plan));
        }
        else
        {
            Terminal.WriteLine();
            if (plan.Status == DetectionStatus.NotDetected)
            {
                Terminal.WriteError("No hay plan disponible.");
                Terminal.WriteDim(plan.Reason ?? "Ejecuta 'condor contexto' y 'condor analizar' primero.");
            }
            else
            {
                Terminal.WriteCyan("Condor genero el plan de trabajo.");
            }

            Terminal.WriteLine();
            PlanRenderer.RenderPlan(plan);
        }

        return plan.Status == DetectionStatus.NotDetected ? 1 : 0;
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
        Terminal.WriteInfo("Condor elabora el plan de trabajo...");
        Terminal.WriteDim("  Cargando el contexto operativo persistido");
        Terminal.WriteDim("  Interpretando la intencion del usuario");
        Terminal.WriteDim("  Descomponiendo el objetivo en tareas");
    }
}
