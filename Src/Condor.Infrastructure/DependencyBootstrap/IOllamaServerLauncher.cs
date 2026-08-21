using System.Threading;
using System.Threading.Tasks;

namespace Condor.Infrastructure.DependencyBootstrap;

/// <summary>
/// Respaldo de la decision de inicio del server de Ollama. Aprovisionador usa
/// esta abstraccion para iniciar "ollama serve" y saber si el server fue
/// iniciado por Condor (ownership) o ya existia.
/// </summary>
public interface IOllamaServerLauncher
{
    /// <summary>
    /// Inicia el server de Ollama (ollama serve). Devuelve true si Condor fue
    /// quien lo inicio y lo considera bajo su control; false si ya existia una
    /// instancia activa que Condor debe reutilizar (y NO cerrar).
    /// </summary>
    Task<bool> StartServerAsync(CancellationToken cancellationToken = default);
}
