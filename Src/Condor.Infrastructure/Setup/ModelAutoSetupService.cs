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
using Condor.Infrastructure.State;

namespace Condor.Infrastructure.Setup;

public sealed class ModelAutoSetupService : IModelAutoSetupService
{
    private readonly IStateStore _stateStore;
    private readonly IAssessmentService? _assessmentService;
    private readonly ModelSetupLimits _limits;
    private readonly OllamaModelOperator _operator;
    private readonly ModelKardex _kardex = new();
    private bool _assessmentCached;

    public ModelAutoSetupService(
        IStateStore stateStore,
        IAssessmentService? assessmentService = null,
        ModelSetupLimits? limits = null,
        OllamaModelOperator? modelOperator = null,
        System.Net.Http.HttpClient? httpClient = null)
    {
        _stateStore = stateStore;
        _assessmentService = assessmentService;
        _limits = limits ?? ModelSetupLimits.Default;
        _operator = modelOperator ?? new OllamaModelOperator(httpClient);
    }

    public async Task<ModelSelectionResult> EnsureModelAsync(
        string? purpose = null,
        CancellationToken cancellationToken = default,
        IStartupProgressObserver? progress = null,
        AssessmentResult? cachedAssessment = null)
    {
        // Seleccion clasica (misma comportamiento previo) para el arranque y usos
        // que no requieren el harness por tarea. Conserva la regresion existente.
        // Si se proporciona un assessment cacheado (ejecutado una sola vez en el
        // bootstrap), se reutiliza para evitar repetir la deteccion secuencial.
        var assessment = cachedAssessment ?? await _stateStore.LoadAssessmentAsync(cancellationToken);

        if (assessment is null && _assessmentService is not null)
        {
            assessment = await _assessmentService.ExecuteAsync(new AssessmentRequest(), cancellationToken);
            await _stateStore.SaveAssessmentAsync(assessment, cancellationToken);
        }

        // Con assessment cacheado, no es necesario ejecutar LoadAuthoritativeAssessmentAsync
        // (que realizaria otra ejecucion completa de AssessmentService.ExecuteAsync).
        _assessmentCached = cachedAssessment is not null;
        var authoritativeAssessment = cachedAssessment is not null
            ? assessment
            : await LoadAuthoritativeAssessmentAsync(assessment, cancellationToken);

        var selection = ModelSelector.RecommendFromCatalog(authoritativeAssessment, ModelCatalog.Default);

        if (selection.Desired is null)
        {
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

        if (selection.AlreadyInstalled)
        {
            var installedNow = await _operator.IsInstalledAsync(
                selection.InstalledName ?? selection.Desired.PullName, cancellationToken);
            if (installedNow)
            {
                await RefreshAssessmentAsync(cancellationToken);
                return selection; // reutilizar, no descargar
            }

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

        progress?.Report(StartupProgress.Of(StartupStage.SelectingModel, message: desired.PullName));
        progress?.Report(StartupProgress.Of(StartupStage.DownloadingModel, message: desired.PullName));

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
            await _kardex.RecordAsync(desired.PullName, ModelKardexStatus.Instalado,
                "Obtenido automaticamente y verificado en Ollama.");
            progress?.Report(StartupProgress.Of(
                StartupStage.VerifyingModel,
                message: desired.PullName,
                completed: true));
        }
        else
        {
            selection.Limitations.Add("No fue posible obtener el modelo tras los reintentos limitados.");
            selection.Reason = "No fue posible obtener el modelo automaticamente.";
            await _kardex.RecordAsync(desired.PullName, ModelKardexStatus.FalloObtencion,
                "Fallo la obtencion tras reintentos acotados.");
        }

        await RefreshAssessmentAsync(cancellationToken);
        return selection;
    }

    /// <summary>
    /// Seleccion inteligente por TAREA + presupuesto (harness dinamico). Usa la
    /// politico de reserva, el requisito de la tarea y la eficiencia (1- y 1+).
    /// </summary>
    public async Task<ModelSelectionResult> EnsureModelForRequirementAsync(
        TaskModelRequirement requirement,
        CancellationToken cancellationToken = default,
        IStartupProgressObserver? progress = null)
    {
        var assessment = await LoadAuthoritativeAssessmentAsync(
            await _stateStore.LoadAssessmentAsync(cancellationToken), cancellationToken);

        var selection = ModelSelector.SelectForTask(assessment, ModelCatalog.Default, requirement, BudgetPolicy.Default);

        if (selection.Desired is null)
        {
            // Kardex: si hay un modelo minimo viable que no cabe, se registra el
            // rechazo por presupuesto (historial local para diagnostico).
            if (selection.BlockedByResources && selection.MinimumViable is not null)
            {
                await _kardex.RecordAsync(
                    selection.MinimumViable.PullName,
                    ModelKardexStatus.RechazadoPorPresupuesto,
                    "El presupuesto de RAM no admite el modelo minimo suficiente para la tarea.");
            }

            await RefreshAssessmentAsync(cancellationToken);
            return selection;
        }

        if (selection.AlreadyInstalled)
        {
            var installedNow = await _operator.IsInstalledAsync(
                selection.InstalledName ?? selection.Desired.PullName, cancellationToken);
            if (installedNow)
            {
                await RefreshAssessmentAsync(cancellationToken);
                return selection; // reutilizar, no descargar
            }
        }

        var desired = selection.Desired;
        if (!IsOllamaReady(assessment))
        {
            selection.Limitations.Add("Ollama no esta disponible; no se puede obtener el modelo.");
            selection.Reason = "Ollama no esta disponible; no fue posible obtener el modelo.";
            await RefreshAssessmentAsync(cancellationToken);
            return selection;
        }

        if (selection.AlreadyInstalled is false)
        {
            progress?.Report(StartupProgress.Of(StartupStage.SelectingModel, message: desired.PullName));
            progress?.Report(StartupProgress.Of(StartupStage.DownloadingModel, message: desired.PullName));

            var pulled = await RetryPolicy.ExecuteAsync(
                _ => _operator.PullAsync(desired.PullName, _limits.PullTimeoutMilliseconds, null, cancellationToken),
                _limits.MaxPullAttempts,
                TimeSpan.FromSeconds(2),
                cancellationToken);

            if (pulled)
            {
                selection.AlreadyInstalled = true;
                selection.InstalledName = desired.PullName;
                selection.Reason = "Modelo obtenido automaticamente y verificado en Ollama (harness).";
                await _kardex.RecordAsync(desired.PullName, ModelKardexStatus.Instalado,
                    "Obtenido automaticamente por el harness de tarea y verificado en Ollama.");
            }
            else
            {
                selection.Limitations.Add("No fue posible obtener el modelo tras los reintentos limitados.");
                selection.Reason = "No fue posible obtener el modelo automaticamente.";
                await _kardex.RecordAsync(desired.PullName, ModelKardexStatus.FalloObtencion,
                    "Fallo la obtencion tras reintentos acotados (harness).");
            }
        }

        await RefreshAssessmentAsync(cancellationToken);
        return selection;
    }

    private static TaskModelRequirement ModelSelectionRequirementFor(string? purpose)
    {
        var p = (purpose ?? "").ToLowerInvariant();
        if (p == TaskIntentKinds.Consult)
        {
            return new TaskModelRequirement
            {
                IntentKind = TaskIntentKinds.Consult,
                RequiredCodingLevel = 0,
                RequiredMultiFileLevel = 0,
                RequiresToolUse = false,
                RequiresStructuredOutput = false,
                PreferSmallestSufficient = true,
                Label = "consulta directa al modelo"
            };
        }

        return new TaskModelRequirement
        {
            IntentKind = TaskIntentKinds.Agent,
            RequiredCodingLevel = 3,
            RequiredMultiFileLevel = 2,
            RequiresToolUse = true,
            RequiresStructuredOutput = true,
            PreferSmallestSufficient = true,
            Label = "agente de ingenieria"
        };
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

        // Si se proporciono un assessment cacheado en EnsureModelAsync, no es
        // necesario refrescar: el assessment ya esta actualizado. Esto evita
        // ejecuciones redundantes de los 6+ detectores secuenciales.
        if (_assessmentCached)
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
