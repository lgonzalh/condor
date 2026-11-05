using System.Text.RegularExpressions;
using Condor.Core.Contracts;
using Condor.Core.Models;

namespace Condor.Infrastructure.Agent;

internal sealed record DeliveryMetricsTaskLinkResult(IReadOnlyList<string> LinkedRecords, string? Warning);

internal static class DeliveryMetricsTaskLinker
{
    private const int MaximumExecutionIdsPerRecord = 256;
    private static readonly Regex ReferencePattern = new(
        @"\[\[condor:(cambio|incidente):([^\]\r\n]{1,120})\]\]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static async Task<DeliveryMetricsTaskLinkResult> LinkAsync(
        IStateStore stateStore,
        string workingDirectory,
        string? intention,
        string executionId,
        CancellationToken cancellationToken)
    {
        var references = ReferencePattern.Matches(intention ?? string.Empty)
            .Select(match => new Reference(match.Groups[1].Value, match.Groups[2].Value.Trim()))
            .Where(reference => reference.Id.Length > 0)
            .Distinct()
            .ToList();
        if (references.Count == 0)
            return new DeliveryMetricsTaskLinkResult(Array.Empty<string>(), null);

        var state = await stateStore.LoadDeliveryMetricsAsync(workingDirectory, cancellationToken).ConfigureAwait(false);
        state.Changes ??= new List<DeliveryChangeRecord>();
        var linkedRecords = new List<string>();
        var warnings = new List<string>();
        var changed = false;

        foreach (var reference in references)
        {
            var matches = state.Changes
                .Where(change => string.Equals(change.ChangeId, reference.Id, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches.Count != 1)
            {
                warnings.Add($"No se encontró un único registro para '{reference.Id}'.");
                continue;
            }

            var change = matches[0];
            var executionIds = reference.Kind.Equals("incidente", StringComparison.OrdinalIgnoreCase)
                ? change.IncidentTaskExecutionIds
                : change.RelatedTaskExecutionIds;
            if (executionIds is null)
            {
                executionIds = new List<string>();
                if (reference.Kind.Equals("incidente", StringComparison.OrdinalIgnoreCase))
                    change.IncidentTaskExecutionIds = executionIds;
                else
                    change.RelatedTaskExecutionIds = executionIds;
            }

            if (reference.Kind.Equals("incidente", StringComparison.OrdinalIgnoreCase))
            {
                if (change.FailedAtUtc is null || change.RecoveredAtUtc is not null)
                {
                    warnings.Add($"El incidente '{reference.Id}' no está abierto para vincular una tarea.");
                    continue;
                }
            }
            else if (change.StartedAtUtc is null || change.DeliveredAtUtc is not null)
            {
                warnings.Add($"El cambio '{reference.Id}' no está en curso para vincular una tarea.");
                continue;
            }

            if (executionIds.Contains(executionId, StringComparer.Ordinal))
                continue;
            if (executionIds.Count >= MaximumExecutionIdsPerRecord)
            {
                warnings.Add($"El registro '{reference.Id}' alcanzó el límite de tareas vinculadas.");
                continue;
            }

            executionIds.Add(executionId);
            linkedRecords.Add($"{reference.Kind}:{reference.Id}");
            changed = true;
        }

        if (changed)
            await stateStore.SaveDeliveryMetricsAsync(workingDirectory, state, cancellationToken).ConfigureAwait(false);

        return new DeliveryMetricsTaskLinkResult(
            linkedRecords,
            warnings.Count == 0 ? null : string.Join(" ", warnings.Distinct()));
    }

    private sealed record Reference(string Kind, string Id);
}
