using Condor.Core.Models;

namespace Condor.Core.Contracts;

/// <summary>
/// Capacidad opcional del cliente LLM para diagnosticar la salud del proveedor
/// (modelo/servidor) sin acoplar los consumidores existentes a la interfaz base.
/// Permite a Condor comprobar health antes de continuar tras un fallo.
/// </summary>
public interface ILlmProviderDiagnostics
{
    /// <summary>Determina si el proveedor esta disponible (servidor responde).</summary>
    Task<bool> IsAvailableAsync(System.Threading.CancellationToken cancellationToken = default);

    /// <summary>Nombre del proveedor o motor de inferencia (p. ej. "Ollama").</summary>
    string ProviderName { get; }
}
