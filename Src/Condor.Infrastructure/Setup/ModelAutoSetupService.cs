using System;
using System.Threading.Tasks;
using Condor.Core.Catalog;
using Condor.Core.Contracts;
using Condor.Core.Evaluation;
using Condor.Core.Models;
using Condor.Core.Selection;
using Condor.Infrastructure.Detection;
using Condor.Infrastructure.Llm;
using Condor.Infrastructure.Retry;

namespace Condor.Infrastructure.Setup;

public sealed class ModelAutoSetupService : IModelAutoSetupService
{
    private readonly IStateStore _stateStore;
    private readonly IAssessmentService? _assessmentService;
    private readonly ModelSetupLimits _limits;
    private readonly OllamaModelOperator _operator;

    public ModelAutoSetupService(
        IStateStore stateStore,
        IAssessmentService? assessmentService = null,
        ModelSetupLimits? limits = null,
        OllamaModelOperator? modelOperator = null)
    {
        _stateStore = stateStore;
        _assessmentService = assessmentService;
        _limits = limits ?? ModelSetupLimits.Default;
        _operator = modelOperator ?? new OllamaModelOperator();
    }

    public async Task<ModelSelectionResult> EnsureModelAsync(
        string? purpose = null,
        CancellationToken cancellationToken = default,
        IStartupProgressObserver? progress = null)
    {
        var assessment = await _stateStore.LoadAssessmentAsync(cancellationToken);

        if (assessment is null && _assessmentService is not null)
        {
            assessment = await _assessmentService.ExecuteAsync(new AssessmentRequest(), cancellationToken);
            await _stateStore.SaveAssessmentAsync(assessment, cancellationToken);
        }

        // El estado persistido es SOLO una sugerencia. La autoridad del
        // inventario real es /api/tags de Ollama en este momento.
        var authoritativeAssessment = await LoadAuthoritativeAssessmentAsync(assessment, cancellationToken);

        var selection = ModelSelector.RecommendFromCatalog(authoritativeAssessment, ModelCatalog.Default);

        if (selection.Desired is null)
        {
            // Ningun modelo es viable: si es por recursos, exponemos los procesos
            // de alto consumo (solo lectura) y NO reintentamos la carga en bucle.
            if (selection.BlockedByResources)
            {
                var consumers = new ProcessRamDetector().DetectTopConsumers();
                selection.Resources = ModelMemoryBudget.Snapshot(
                    authoritativeAssessment?.Environment?.Memory,
                    candidatePeakGb: null,
                    consumers);
            }

            await RefreshAssessmentAsync(cancellationToken);
            return selection;
        }

        // Declara "no encontrado" y avanza visualmente: evaluando recursos,
        // seleccionando, preparando descarga. Nunca una terminal congelada.
        if (selection.AlreadyInstalled is false &&
            authoritativeAssessment?.Tools?.Ollama is { ServerRunning: true } &&
            (authoritativeAssessment.Tools.Ollama.Models is null ||
             authoritativeAssessment.Tools.Ollama.Models.Count == 0))
        {
            progress?.Report(StartupProgress.Of(
                StartupStage.EvaluatingModels,
                message: "Modelo no encontrado: evaluando recursos"));
            progress?.Report(StartupProgress.Of(
                StartupStage.SelectingModel,
                message: selection.Desired.PullName));
        }

        // Confiar en Ollama, jamas en el assessment persistido: si el assessment
        // dice "instalado" pero Ollama no lo tiene, NO declararlo listo.
        if (selection.AlreadyInstalled)
        {
            progress?.Report(StartupProgress.Of(
                StartupStage.SelectingModel,
                message: selection.InstalledName ?? selection.Desired.PullName));
            progress?.Report(StartupProgress.Of(
                StartupStage.VerifyingModel,
                message: selection.InstalledName ?? selection.Desired.PullName));

            var installedNow = await _operator.IsInstalledAsync(
                selection.InstalledName ?? selection.Desired.PullName, cancellationToken);
            if (installedNow)
            {
                progress?.Report(StartupProgress.Of(
                    StartupStage.VerifyingModel,
                    message: selection.InstalledName ?? selection.Desired.PullName,
                    completed: true));
                await RefreshAssessmentAsync(cancellationToken);
                return selection; // reutilizar, no descargar
            }

            // El modelo declarado en el assessment NO existe en Ollama (fue
            // eliminado): el inventario vacio manda antes que el estado antiguo.
            progress?.Report(StartupProgress.Of(
                StartupStage.SelectingModel,
                message: selection.InstalledName ?? selection.Desired.PullName));
            await RefreshAssessmentAsync(cancellationToken);

            var fresh = await _stateStore.LoadAssessmentAsync(cancellationToken);
            selection = ModelSelector.RecommendFromCatalog(fresh, ModelCatalog.Default);

            if (selection.Desired is null)
            {
                await RefreshAssessmentAsync(cancellationToken);
                return selection;
            }
        }

        var desired = selection.Desired;

        if (!IsOllamaReady(authoritativeAssessment))
        {
            selection.Limitations.Add("Ollama no esta disponible; no se puede obtener el modelo.");
            selection.Reason = "Ollama no esta disponible; no fue posible obtener el modelo.";
            await RefreshAssessmentAsync(cancellationToken);
            return selection;
        }

        progress?.Report(StartupProgress.Of(
            StartupStage.SelectingModel,
            message: desired.PullName));

        progress?.Report(StartupProgress.Of(
            StartupStage.DownloadingModel,
            message: desired.PullName));

        var pulled = await RetryPolicy.ExecuteAsync(
            _ => _operator.PullAsync(
                desired.PullName,
                _limits.PullTimeoutMilliseconds,
                percent => progress?.Report(StartupProgress.Of(
                    StartupStage.DownloadingModel,
                    message: desired.PullName,
                    downloadPercent: percent)),
                cancellationToken),
            _limits.MaxPullAttempts,
            TimeSpan.FromSeconds(2),
            cancellationToken);

        if (pulled)
        {
            selection.AlreadyInstalled = true;
            selection.InstalledName = desired.PullName;
            selection.Reason = "Modelo obtenido automaticamente y verificado en Ollama.";
            progress?.Report(StartupProgress.Of(
                StartupStage.VerifyingModel,
                message: desired.PullName,
                completed: true));
        }
        else
        {
            selection.Limitations.Add("No fue posible obtener el modelo tras los reintentos limitados.");
            selection.Reason = "No fue posible obtener el modelo automaticamente.";
        }

        // Regla de autoridad: el estado persistido no puede afirmar que un modelo
        // existe si Ollama no lo confirma. Se refresca el assessment al final SIEMPRE
        // (exito o fracaso) para que %LOCALAPPDATA% refleje el inventario real.
        await RefreshAssessmentAsync(cancellationToken);

        return selection;
    }

    /// <summary>
    /// Inventario real de Ollama (/api/tags) como autoridad. El estado persistido
    /// solo se usa si no se puede sondear Ollama; nunca para inventar modelos.
    /// </summary>
    private async Task<AssessmentResult?> LoadAuthoritativeAssessmentAsync(
        AssessmentResult? persisted,
        CancellationToken cancellationToken)
    {
        if (_assessmentService is null)
        {
            return persisted;
        }

        try
        {
            var live = await _assessmentService.ExecuteAsync(new AssessmentRequest(), cancellationToken);
            await _stateStore.SaveAssessmentAsync(live, cancellationToken);
            return live;
        }
        catch
        {
            // Si Ollama no responde, se conserva el estado persistido como sugerencia.
            return persisted;
        }
    }

    private static bool IsOllamaReady(AssessmentResult? assessment)
    {
        return assessment?.Tools?.Ollama is not null &&
               assessment.Tools.Ollama.ServerRunning;
    }

    private async Task RefreshAssessmentAsync(CancellationToken cancellationToken)
    {
        if (_assessmentService is null)
        {
            return;
        }

        try
        {
            var refreshed = await _assessmentService.ExecuteAsync(new AssessmentRequest(), cancellationToken);
            await _stateStore.SaveAssessmentAsync(refreshed, cancellationToken);
        }
        catch
        {
            // Si falla el refresco, se conserva el Assessment anterior.
        }
    }
}
