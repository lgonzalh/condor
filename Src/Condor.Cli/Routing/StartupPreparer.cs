using System;
using System.Linq;
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
    /// Detecta y deja preparado el entorno: evalúa hardware, RAM libre,
    /// almacenamiento, GPU, Ollama y modelos, selecciona el modelo mas adecuado
    /// para la tarea/equipo y lo reutiliza u obtiene cuando es viable. Emite
    /// etapas reales de progreso (opcional) para reflejar que Condor esta
    /// trabajando, no bloqueado. No modifica la logica de seleccion/presupuesto.
    /// Cuando Ollama esta activo pero no hay un modelo utilizable (ni se pudo
    /// obtener tras una preparacion/descarga acotada), devuelve No listo con un
    /// motivo claro: nunca se declara "Entorno listo" sin capacidad operativa.
    /// </summary>
    public async Task<StartupPrepResult> RunAsync(
        IStartupProgressObserver? progress = null,
        CancellationToken cancellationToken = default)
    {
        var assessment = await _stateStore.LoadAssessmentAsync(cancellationToken);

        if (assessment is null)
        {
            try
            {
                progress?.Report(StartupProgress.Of(StartupStage.PreparingEnvironment));
                progress?.Report(StartupProgress.Of(StartupStage.ReviewingResources));
                assessment = await _assessment.ExecuteAsync(new AssessmentRequest(), cancellationToken);
                progress?.Report(StartupProgress.Of(StartupStage.ReviewingResources, completed: true));
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
            progress?.Report(StartupProgress.Of(StartupStage.PreparingEnvironment, completed: true));
            progress?.Report(StartupProgress.Of(StartupStage.Ready, completed: true));
            return new StartupPrepResult
            {
                Ready = true,
                Reason = assessment is null ? null : "Entorno listo."
            };
        }

        var selection = await _modelAutoSetup.EnsureModelAsync(null, cancellationToken, progress);
        var model = await ResolveReadyModelAsync(selection, cancellationToken);

        if (model is null && !selection.AlreadyInstalled)
        {
            // Ollama esta activo pero no quedó un modelo utilizable. Aunque se
            // evaluaron recursos y se intentó una preparacion/descarga acotada,
            // no se obtuvo un modelo compatible: NO se reporta listo ni se deja
            // que la sesion arranque sin capacidad operativa.
            progress?.Report(StartupProgress.Of(StartupStage.PreparingEnvironment, completed: true));
            progress?.Report(StartupProgress.Of(StartupStage.Ready, completed: true));

            return new StartupPrepResult
            {
                Ready = false,
                NeedsIntervention = true,
                Reason = BuildNoModelReason(selection)
            };
        }

        progress?.Report(StartupProgress.Of(StartupStage.PreparingEnvironment, completed: true));
        progress?.Report(StartupProgress.Of(StartupStage.Ready, completed: true));

        return new StartupPrepResult
        {
            Ready = true,
            Model = model,
            Reason = model is null ? "Ningun modelo local disponible; se usaran los recursos actuales." : "Entorno preparado."
        };
    }

    /// <summary>
    /// Motivo honesto y accionable de por que Condor no puede iniciar con un
    /// modelo utilizable. Distingue el bloqueo por recursos (sin modelo viable
    /// para el equipo) de la falla al obtener/verificar el modelo seleccionado.
    /// </summary>
    private static string BuildNoModelReason(ModelSelectionResult selection)
    {
        var blocked = selection.BlockedByResources || selection.Resources?.Pressure == ResourcePressure.Insufficient;
        var full = selection.Limitations.Count > 0 ? string.Join(" ", selection.Limitations) : null;
        var reason = selection.Reason ?? full;

        if (blocked)
        {
            var pressure = selection.Resources?.PressureLabel ?? "insuficiente";
            var message = "No hay ningun modelo viable para este equipo: " + pressure + " de recursos. " +
                          "Se evaluo el presupuesto seguro sin intentar cargas repetidas. " +
                          "Libera memoria u obtén un modelo mas pequeno e intenta de nuevo con 'condor /preparar'.";
            return string.IsNullOrWhiteSpace(full) ? message : message + " " + full;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return "No hay modelos locales disponibles y no fue posible obtener uno compatible automaticamente. " +
                   "Verifica la conexión y que Ollama este activo, luego reintenta con 'condor /preparar'.";
        }

        return reason;
    }

    /// <summary>
    /// Regla de autoridad: estado persistido != estado real. El modelo solo se
    /// declara "listo" si existe AHORA en el inventario real de Ollama (/api/tags).
    /// Nunca se confirma un modelo a partir de %LOCALAPPDATA%\Condor\state.
    /// </summary>
    private async Task<string?> ResolveReadyModelAsync(
        ModelSelectionResult selection,
        CancellationToken cancellationToken)
    {
        if (selection.Desired is null || !selection.AlreadyInstalled)
        {
            return null;
        }

        var expected = selection.InstalledName ?? selection.Desired.PullName;

        try
        {
            var live = await _assessment.ExecuteAsync(new AssessmentRequest(), cancellationToken);
            var names = live.Tools?.Ollama?.Models?.Select(m => m.Name) ?? Enumerable.Empty<string>();
            return names.Any(n => n.Equals(expected, StringComparison.OrdinalIgnoreCase) ||
                                  n.EndsWith(":" + expected.Split(':').Last(), StringComparison.OrdinalIgnoreCase))
                ? expected
                : null;
        }
        catch
        {
            // No se pudo sondear Ollama: no afirmamos que el modelo este listo.
            return null;
        }
    }

    private static bool IsOllamaRunning(AssessmentResult? assessment)
    {
        return assessment?.Tools?.Ollama is { ServerRunning: true };
    }
}
