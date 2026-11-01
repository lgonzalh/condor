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

    public DocumentingAgentService(
        IAgentService inner,
        IDocumenter documenter,
        Func<string>? workingDirectoryProvider = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _documenter = documenter ?? throw new ArgumentNullException(nameof(documenter));
        _workingDirectoryProvider = workingDirectoryProvider ?? (() => Environment.CurrentDirectory);
    }

    public async Task<AgentResult> RunAsync(
        string intention,
        IAgentProgressObserver? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startedAtUtc = DateTime.UtcNow;
        var workingDirectory = _workingDirectoryProvider();
        var result = await _inner.RunAsync(intention, progress, cancellationToken).ConfigureAwait(false);
        var completedAtUtc = DateTime.UtcNow;

        var during = result.Steps.Count == 0
            ? new List<string> { "El agente no registro pasos de herramienta." }
            : result.Steps.Select(step =>
            {
                var path = string.IsNullOrWhiteSpace(step.Path) ? "" : $" — {step.Path}";
                return $"{step.Action}: {(step.Success ? "correcto" : "fallido")}{path}";
            }).ToList();

        var request = new DocumentationRequest
        {
            TaskType = "Agente en lenguaje natural",
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

        result.Documentation = await WriteSafelyAsync(request).ConfigureAwait(false);
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
