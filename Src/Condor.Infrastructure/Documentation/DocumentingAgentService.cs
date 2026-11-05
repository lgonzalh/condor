using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Condor.Core.Contracts;
using Condor.Core.Models;

namespace Condor.Infrastructure.Documentation;

public sealed class DocumentingAgentService : IAgentService
{
    private readonly IAgentService _inner;
    private readonly IDocumenter _documenter;
    private readonly Func<string> _workingDirectoryProvider;
    private readonly IncidentLearningService _incidentLearningService;

    public DocumentingAgentService(
        IAgentService inner,
        IDocumenter documenter,
        Func<string>? workingDirectoryProvider = null,
        IncidentLearningService? incidentLearningService = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _documenter = documenter ?? throw new ArgumentNullException(nameof(documenter));
        _workingDirectoryProvider = workingDirectoryProvider ?? (() => Environment.CurrentDirectory);
        _incidentLearningService = incidentLearningService ?? new IncidentLearningService();
    }

    public async Task<AgentResult> RunAsync(
        string intention,
        IAgentProgressObserver? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startedAtUtc = DateTime.UtcNow;
        var workingDirectory = _workingDirectoryProvider();
        var result = await _inner.RunAsync(intention, progress, cancellationToken).ConfigureAwait(false);
        if (result.AwaitingUserInput)
            return result;
        var completedAtUtc = DateTime.UtcNow;

        var during = result.Steps.Count == 0
            ? new List<string> { "El agente no registro pasos de herramienta." }
            : result.Steps.Select(step =>
            {
                var path = string.IsNullOrWhiteSpace(step.Path) ? "" : $" — {step.Path}";
                return $"{step.Action}: {(step.Success ? "correcto" : "fallido")}{path}";
            }).ToList();
        var changedPaths = result.Steps
            .Where(step => step.Success && step.Action is "create_file" or "edit_file" or "patch" or "undo_file")
            .Select(step => step.Path)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var request = new DocumentationRequest
        {
            TaskType = result.Success ? "Agente en lenguaje natural" : "Incidente del agente",
            Model = result.Model,
            Intention = intention,
            WorkingDirectory = workingDirectory,
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = completedAtUtc,
            Outcome = result.Success ? "completado" : "fallido",
            Reason = result.Reason,
            Before = new List<string>
            {
                $"Intencion recibida: {intention}",
                $"Directorio de trabajo: {workingDirectory}",
                $"Inicio UTC: {startedAtUtc:O}"
            },
            During = during,
            After = new List<string>
            {
                $"Resultado del agente: {(result.Success ? "completado" : "fallido")}",
                $"Pasos registrados: {result.Steps.Count}; pasos exitosos: {result.Steps.Count(step => step.Success)}",
                $"Fin UTC: {completedAtUtc:O}"
            }
        };
        if (!result.Success)
        {
            request.After.Add("Tipo de registro: incidente de ejecución");
            request.After.Add("Impacto: la tarea no se completó; " + (changedPaths.Count == 0
                ? "no hay acciones de cambio exitosas registradas."
                : "se registraron acciones sobre: " + string.Join(", ", changedPaths) + "."));
            request.After.Add("Causa reportada (no confirmada como causa raíz): " +
                (string.IsNullOrWhiteSpace(result.Reason) ? "no determinada por esta ejecución." : result.Reason));
        }
        if (result.ReviewFindings is { } findings)
        {
            var resolved = findings.Count(finding => finding.Status == "resolved");
            var discarded = findings.Count(finding => finding.Status == "accepted");
            var open = findings.Count(finding => finding.Status == "open");
            var adjudicated = resolved + discarded;
            request.After.Add($"Hallazgos de revisión: {findings.Count} total; {resolved} resueltos; {discarded} aceptados/descartados; {open} abiertos.");
            request.After.Add(adjudicated == 0
                ? "Proporción de hallazgos descartados: no calculable; no hay hallazgos adjudicados."
                : $"Proporción de hallazgos descartados entre los adjudicados: {(double)discarded / adjudicated:P1} ({discarded}/{adjudicated}).");

            var history = await ReviewOutcomeJournal.RecordAndSummarizeAsync(
                workingDirectory,
                findings,
                CancellationToken.None).ConfigureAwait(false);
            if (history is null)
            {
                request.After.Add("Historial de revisión entre ejecuciones: no disponible; Condor no pudo guardar o leer las métricas locales.");
            }
            else
            {
                request.After.Add($"Historial de revisión: {history.Executions} ejecuciones; {history.ExecutionsLast30Days} en los últimos 30 días; {history.Useful} útiles; {history.Discarded} descartados; {history.Open} abiertos; {history.Unclassified} sin clasificar.");
                request.After.Add(history.UsefulRate is null
                    ? "Proporción histórica de hallazgos útiles: no calculable; no hay hallazgos adjudicados."
                    : $"Proporción histórica de hallazgos útiles entre los adjudicados: {history.UsefulRate:P1} ({history.Useful}/{history.Useful + history.Discarded}).");
                request.After.Add(history.UsefulRateLast30Days is null
                    ? "Proporción de hallazgos útiles en los últimos 30 días: no calculable; no hay hallazgos adjudicados en ese periodo."
                    : $"Proporción de hallazgos útiles en los últimos 30 días: {history.UsefulRateLast30Days:P1} ({history.UsefulLast30Days}/{history.UsefulLast30Days + history.DiscardedLast30Days}).");
            }
        }

        result.Documentation = await WriteSafelyAsync(request).ConfigureAwait(false);
        if (!result.Success && result.Documentation.Success)
        {
            var failedActions = result.Steps
                .Where(step => !step.Success)
                .Select(step => step.Action);
            _ = await _incidentLearningService.RecordAsync(
                workingDirectory,
                result.Reason,
                failedActions,
                CancellationToken.None).ConfigureAwait(false);
        }
        return result;
    }

    private async Task<DocumentationResult> WriteSafelyAsync(DocumentationRequest request)
    {
        try
        {
            return await _documenter.WriteAsync(request, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return new DocumentationResult
            {
                Success = false,
                Reason = $"{exception.GetType().Name}: {exception.Message}",
                GeneratedAtUtc = DateTime.UtcNow
            };
        }
    }
}
