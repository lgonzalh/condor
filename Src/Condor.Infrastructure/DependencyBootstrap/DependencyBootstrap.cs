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
    private readonly IAssessmentService? _assessmentService;

    public DependencyBootstrapper(
        OllamaProvisioner? ollamaProvisioner = null,
        IAssessmentService? assessmentService = null)
    {
        _ollamaProvisioner = ollamaProvisioner ?? new OllamaProvisioner();
        _assessmentService = assessmentService;
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

        // Ejecutar una unica vez el assessment completo (detectores de hardware,
        // Ollama, etc.) para que los componentes posteriores (StartupPreparer,
        // ModelAutoSetupService) no lo repitan innecesariamente.
        AssessmentResult? assessment = null;
        if (_assessmentService is not null)
        {
            try
            {
                assessment = await _assessmentService.ExecuteAsync(new AssessmentRequest(), cancellationToken);
            }
            catch
            {
                // Si falla, los componentes posteriores manejan su propio fallback.
            }
        }

        return new DependencyBootstrapResult
        {
            Ready = ollama.Ok,
            Ollama = ollama,
            Reason = ollama.Ok ? null : ollama.Reason,
            Diagnostic = ollama.Diagnostic,
            Assessment = assessment
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

    /// <summary>
    /// Assessment completo ejecutado una sola vez durante el bootstrap.
    /// Los componentes posteriores lo reutilizan para evitar repetir la
    /// deteccion secuencial de hardware (6+ detectores).
    /// </summary>
    public AssessmentResult? Assessment { get; init; }
}
