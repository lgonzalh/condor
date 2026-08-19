using Condor.Cli.Presentation;
using Condor.Core.Contracts;
using Condor.Core.Models;
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

        // Progreso visual solo en modo interactivo (no con --json, para no contaminar la salida).
        AgentProgressPresenter? presenter = null;
        try
        {
            AgentProgressObserverBridge? bridge = null;
            if (!outputJson)
            {
                presenter = new AgentProgressPresenter();
                bridge = new AgentProgressObserverBridge(presenter);
                presenter.Start(intent);
            }

            var result = await agentService.RunAsync(intent, bridge, cancellationToken);

            if (outputJson)
            {
                Console.WriteLine(AgentJson.Serialize(result));
            }
            else
            {
                var finalLine = result.Success
                    ? (IsInformational(result) ? "Condor termino." : "Cambios verificados.")
                    : "Condor no pudo completar la tarea.";
                presenter?.Stop(result.Success, finalLine);

                Terminal.WriteLine();
                AgentRenderer.RenderResult(result);
            }

            return result.Success ? 0 : 1;
        }
        finally
        {
            presenter?.Dispose();
        }
    }

    private static bool IsInformational(AgentResult result)
        => result.Checkpoint?.LastDecision == "describir";

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
