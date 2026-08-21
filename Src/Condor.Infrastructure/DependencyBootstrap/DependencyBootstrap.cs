using System.Threading;
using System.Threading.Tasks;
using Condor.Core.Contracts;
using Condor.Core.Models;

namespace Condor.Infrastructure.DependencyBootstrap;

/// <summary>
/// Bootstrap de dependencias de Condor: una etapa explicita antes del flujo
/// normal que detecta, prepara, verifica y continua. Hoy la unica dependencia
/// externa obligatoria es Ollama; la misma abstraccion se extiende a otras
/// dependencias reales (ningun componente de Windows se instala sin necesidad
/// tecnica comprobable).
///
/// FLUJO: detectar -> preparar (instalar/arrancar) -> verificar (endpoint) ->
///        continuar.
///
/// No cierra instancias de Ollama preexistentes; registra ownership y delega la
/// liberacion del modelo (keep_alive=0) a la sesion en el cierre de Condor.
/// </summary>
public sealed class DependencyBootstrapper
{
    private readonly OllamaProvisioner _ollamaProvisioner;

    public DependencyBootstrapper(OllamaProvisioner? ollamaProvisioner = null)
    {
        _ollamaProvisioner = ollamaProvisioner ?? new OllamaProvisioner();
    }

    /// <summary>
    /// Ejecuta el bootstrap. Devuelve un resultado indicando si el entorno esta
    /// listo. Nunca lanza excepciones al usuario; genera un resultado de error
    /// controlado con detalle de diagnostico separado.
    /// </summary>
    public async Task<DependencyBootstrapResult> RunAsync(
        IStartupProgressObserver? progress = null,
        CancellationToken cancellationToken = default)
    {
        Step(progress, StartupStage.BootstrappingDependencies, "Verificando dependencias...");
        var ollama = await _ollamaProvisioner.ProvisionAsync(progress, cancellationToken);
        Step(progress, StartupStage.BootstrappingDependencies, "Dependencias verificadas.", isCompleted: true);

        return new DependencyBootstrapResult
        {
            Ready = ollama.Ok,
            Ollama = ollama,
            Reason = ollama.Ok ? null : ollama.Reason,
            Diagnostic = ollama.Diagnostic
        };
    }

    private static void Step(IStartupProgressObserver? progress, StartupStage stage, string? message, bool isCompleted = false)
    {
        progress?.Report(StartupProgress.Of(stage, message, completed: isCompleted));
    }
}

/// <summary>Resultado del bootstrap de dependencias.</summary>
public sealed class DependencyBootstrapResult
{
    public bool Ready { get; init; }
    public OllamaProvisioningResult? Ollama { get; init; }
    public string? Reason { get; init; }

    /// <summary>Detalle tecnico para log/diagnostico (no UI).</summary>
    public string? Diagnostic { get; init; }
}
