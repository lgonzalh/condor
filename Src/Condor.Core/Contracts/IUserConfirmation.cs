using System.Threading;
using System.Threading.Tasks;

namespace Condor.Core.Contracts;

/// <summary>
/// Interfaz opcional que Condor consulta cuando, tras evaluar/reevaluar recursos,
/// no existe un modelo que pueda ejecutarse con la RAM disponible. Permite al
/// vehiculo de presentacion (consola) preguntar al usuario si desea liberar
/// memoria y continuar. Es SIEMPRE una accion OPCIONAL: Condor nunca cierra
/// aplicaciones por su cuenta. Si no se conecta un confirmador, Condor conserva
/// la tarea y termina de forma limpia sin preguntar.
/// </summary>
public interface IUserConfirmation
{
    /// <summary>
    /// Pregunta al usuario si desea liberar memoria para reintentar. Devuelve true
    /// si el usuario confirma (respuesta SI), false si no desea hacerlo.
    /// </summary>
    Task<bool> AskToReleaseRamAsync(string prompt, CancellationToken cancellationToken = default);
}
