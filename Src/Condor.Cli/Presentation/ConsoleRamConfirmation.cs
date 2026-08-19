using System;
using System.Threading;
using System.Threading.Tasks;
using Condor.Core.Contracts;

namespace Condor.Cli.Presentation;

/// <summary>
/// Confirmacion interactiva opcional de RAM, solo para la consola interactiva
/// (nunca con salida JSON ni entrada redirigida). Cóndor NUNCA cierra aplicaciones
/// por su cuenta: solo pregunta si el usuario desea liberar memoria y continuar.
/// Si no hay respuesta valida en un reintento acotado, se niega la confirmacion
/// (salida limpia sin perder la tarea). Sin bucles infinitos.
/// </summary>
public sealed class ConsoleRamConfirmation : IUserConfirmation
{
    private const int MaxAttempts = 3;

    public async Task<bool> AskToReleaseRamAsync(string prompt, CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            Console.Write(prompt + " ");
            var answer = await ReadAnswerAsync(cancellationToken);

            if (string.Equals(answer, "S", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(answer, "si", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(answer, "sí", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(answer, "N", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(answer, "no", StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // Sin respuesta valida: se trata como una negativa para no bloquear.
        return false;
    }

    private static async Task<string?> ReadAnswerAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        var line = Console.ReadLine();
        return string.IsNullOrWhiteSpace(line) ? null : line.Trim();
    }
}
