using System;
using System.Threading.Tasks;
using Condor.Core.Catalog;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Core.Selection;
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
        CancellationToken cancellationToken = default)
    {
        var assessment = await _stateStore.LoadAssessmentAsync(cancellationToken);

        if (assessment is null && _assessmentService is not null)
        {
            assessment = await _assessmentService.ExecuteAsync(new AssessmentRequest(), cancellationToken);
            await _stateStore.SaveAssessmentAsync(assessment, cancellationToken);
        }

        var selection = ModelSelector.RecommendFromCatalog(assessment, ModelCatalog.Default);

        if (selection.Desired is null)
        {
            return selection;
        }

        if (selection.AlreadyInstalled)
        {
            return selection; // reutilizar, no descargar
        }

        var desired = selection.Desired;

        if (!IsOllamaReady(assessment))
        {
            selection.Limitations.Add("Ollama no esta disponible; no se puede obtener el modelo.");
            selection.Reason = "Ollama no esta disponible; no fue posible obtener el modelo.";
            return selection;
        }

        var pulled = await RetryPolicy.ExecuteAsync(
            _ => _operator.PullAsync(desired.PullName, _limits.PullTimeoutMilliseconds, cancellationToken),
            _limits.MaxPullAttempts,
            TimeSpan.FromSeconds(2),
            cancellationToken);

        if (pulled)
        {
            selection.AlreadyInstalled = true;
            selection.InstalledName = desired.PullName;
            selection.Reason = "Modelo obtenido automaticamente y verificado en Ollama.";
            await RefreshAssessmentAsync(cancellationToken);
        }
        else
        {
            selection.Limitations.Add("No fue posible obtener el modelo tras los reintentos limitados.");
            selection.Reason = "No fue posible obtener el modelo automaticamente.";
        }

        return selection;
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
