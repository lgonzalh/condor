using System;
using System.Threading;
using System.Threading.Tasks;

namespace Condor.Cli.Routing;

/// <summary>
/// Bucle interactivo de Condor. Presenta un prompt ">" y enruta cada entrada:
/// los que comienzan con "/" son comandos de control explicitos del usuario y
/// el resto es una intencion natural que se entrega directamente al motor
/// agente. Termina con "/salir", "salir", "exit" o fin de entrada (EOF).
/// </summary>
public sealed class Interpreter
{
    private readonly Func<SlashRoute, Task<int>> _slashHandler;
    private readonly Func<FreeIntentionRoute, Task<int>> _freeIntentionHandler;
    private readonly Func<string?> _readLine;

    public Interpreter(
        Func<SlashRoute, Task<int>> slashHandler,
        Func<FreeIntentionRoute, Task<int>> freeIntentionHandler,
        Func<string?>? readLine = null)
    {
        _slashHandler = slashHandler;
        _freeIntentionHandler = freeIntentionHandler;
        _readLine = readLine ?? (() => Console.ReadLine());
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        string? line;

        while ((line = await ReadLineAsync(cancellationToken)) is not null)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var trimmed = line.Trim();

            if (IsExit(trimmed))
            {
                return 0;
            }

            if (string.IsNullOrWhiteSpace(trimmed) || IsComment(trimmed))
            {
                continue;
            }

            var route = IntentionRouter.Route(trimmed);

            if (route is SlashRoute slash)
            {
                await _slashHandler(slash);
            }
            else if (route is FreeIntentionRoute free)
            {
                await _freeIntentionHandler(free);
            }
        }

        return 0;
    }

    private async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        if (!Console.IsInputRedirected)
        {
            Console.Write("> ");
        }

        var line = _readLine();

        // Permitir interrupcion cooperativa si la lectura bloquea el hilo.
        await Task.Yield();
        ct.ThrowIfCancellationRequested();

        return line;
    }

    private static bool IsExit(string line)
    {
        return line.Equals("/salir", StringComparison.OrdinalIgnoreCase) ||
               line.Equals("/quit", StringComparison.OrdinalIgnoreCase) ||
               line.Equals("/exit", StringComparison.OrdinalIgnoreCase) ||
               line.Equals("salir", StringComparison.OrdinalIgnoreCase) ||
               line.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
               line.Equals("quit", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsComment(string line)
    {
        return line.StartsWith('#');
    }
}
