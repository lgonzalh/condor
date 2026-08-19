using Condor.Cli.Presentation;
using Condor.Core.Contracts;
using Condor.Core.Serialization;

namespace Condor.Cli.Commands;

/// <summary>
/// Entrada de intencion libre: recibe el texto natural del usuario y lo entrega
/// al motor agente. No es un comando interno; es la via principal de Condor
/// cuando el usuario expresa con palabras lo que necesita.
/// </summary>
public static class AgentCommand
{
    public static async Task<int> ExecuteAsync(
        IAgentService agentService,
        string[] args,
        CancellationToken cancellationToken = default)
    {
        var outputJson = args.Contains("--json", StringComparer.OrdinalIgnoreCase);
        var intent = BuildIntent(args, outputJson);

        if (string.IsNullOrWhiteSpace(intent))
        {
            Terminal.WriteError("Indica que quieres que Condor haga.");
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(intent) && !outputJson)
        {
            Terminal.WriteInfo("Condor analiza tu solicitud y actua sobre el proyecto...");
            Terminal.WriteDim("  Comprendiendo la intencion");
            Terminal.WriteDim("  Seleccionando modelo y estrategia");
            Terminal.WriteDim("  Usando herramientas reales y harness");
        }

        var result = await agentService.RunAsync(intent, cancellationToken);

        if (outputJson)
        {
            Console.WriteLine(AgentJson.Serialize(result));
        }
        else
        {
            Terminal.WriteLine();
            if (result.Success) Terminal.WriteSuccess("Condor completo la tarea con evidencia verificada.");
            else Terminal.WriteWarning("Condor no pudo completar la tarea.");
            Terminal.WriteLine();
            AgentRenderer.RenderResult(result);
        }

        return result.Success ? 0 : 1;
    }

    private static string BuildIntent(string[] args, bool outputJson)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var arg in args)
        {
            if (string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase)) continue;
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(arg);
        }

        return sb.ToString().Trim();
    }
}
