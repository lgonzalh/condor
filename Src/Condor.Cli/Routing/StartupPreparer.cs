using System;
using System.Threading;
using System.Threading.Tasks;
using Condor.Core.Contracts;
using Condor.Core.Models;

namespace Condor.Cli.Routing;

public sealed class StartupPrepResult
{
    public bool Ready { get; init; }
    public string? Model { get; init; }
    public string? Reason { get; init; }
    public ModelSelectionResult? ModelSelection { get; init; }
    public bool NeedsIntervention { get; init; }
}

public sealed class StartupPreparer
{
    private readonly IAssessmentService _assessment;
    private readonly IStateStore _stateStore;
    private readonly ISetupService? _setup;
    private readonly IModelAutoSetupService? _modelAutoSetup;

    public StartupPreparer(
        IAssessmentService assessment,
        IStateStore stateStore,
        ISetupService? setup = null,
        IModelAutoSetupService? modelAutoSetup = null)
    {
        _assessment = assessment;
        _stateStore = stateStore;
        _setup = setup;
        _modelAutoSetup = modelAutoSetup;
    }

    /// <summary>
    /// Detecta y deja preparado el entorno de forma automatica y silenciosa:
    /// evalúa hardware, RAM libre, almacenamiento, GPU, Ollama y modelos,
    /// selecciona el modelo mas adecuado para la tarea/equipo y lo reutiliza
    /// u obtiene cuando es viable. Solo informa errores o decisiones que
    /// requieran intervencion del usuario.
    /// </summary>
    public async Task<StartupPrepResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var assessment = await _stateStore.LoadAssessmentAsync(cancellationToken);

        if (assessment is null)
        {
            try
            {
                assessment = await _assessment.ExecuteAsync(new AssessmentRequest(), cancellationToken);
                await _stateStore.SaveAssessmentAsync(assessment, cancellationToken);
            }
            catch
            {
                return new StartupPrepResult
                {
                    Ready = false,
                    Reason = "No fue posible observar el entorno automaticamente."
                };
            }
        }

        if (_modelAutoSetup is null || !IsOllamaRunning(assessment))
        {
            return new StartupPrepResult
            {
                Ready = true,
                Reason = assessment is null ? null : "Entorno listo."
            };
        }

        await _modelAutoSetup.EnsureModelAsync(null, cancellationToken);
        var refreshed = await _stateStore.LoadAssessmentAsync(cancellationToken);
        var model = refreshed?.Tools?.Ollama?.Models?.Count > 0
            ? refreshed.Tools.Ollama.Models.First().Name
            : null;

        return new StartupPrepResult
        {
            Ready = true,
            Model = model,
            Reason = "Entorno preparado."
        };
    }

    private static bool IsOllamaRunning(AssessmentResult? assessment)
    {
        return assessment?.Tools?.Ollama is { ServerRunning: true };
    }
}
