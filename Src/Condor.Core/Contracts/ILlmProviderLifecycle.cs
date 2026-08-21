using System.Threading;
using System.Threading.Tasks;

namespace Condor.Core.Contracts;

/// <summary>
/// Ciclo de vida de la sesion de proveedor local. Condor es UN cliente de
/// Ollama (que gestiona internamente llama-server.exe); por tanto Condor nunca
/// mata procesos externos: la reutilizacion y la liberacion del modelo se hacen
/// mediante el mecanismo oficial de Ollama (keep_alive=0). Este contrato define
/// el ownership de esa sesion: una unica sesion activa reutilizable por
/// ejecucion de Condor, que se libera al final mediante ReleaseAsync.
/// </summary>
public interface ILlmProviderLifecycle
{
    /// <summary>
    /// Nombre del proveedor o motor de inferencia (p. ej. "Ollama").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Modelo activo de la sesion actual. Es null en la primera consulta y
    /// queda fijado tras la primera inicializacion.
    /// </summary>
    string? ActiveModel { get; }

    /// <summary>
    /// Asegura que existe una sesion compatible con <paramref name="model"/>.
    /// Si la sesion ya esta activa para ese modelo y el proveedor responde,
    /// se reutiliza sin recrear recurso alguno; en otro caso registra el nuevo
    /// modelo activo como la sesion de la ejecucion. Devuelve true si la sesion
    /// queda disponible para inferencia.
    /// </summary>
    Task<bool> EnsureAvailableAsync(string model, CancellationToken cancellationToken = default);

    /// <summary>
    /// Libera el modelo activo a traves del mecanismo oficial del proveedor
    /// (Ollama keep_alive=0), devolviendo la RAM retenida. Nunca cierra procesos
    /// de infraestructura externa. Es idempotente y seguro llamarlo en shutdown
    /// (normal o por finally).
    /// </summary>
    Task ReleaseAsync(CancellationToken cancellationToken = default);
}
