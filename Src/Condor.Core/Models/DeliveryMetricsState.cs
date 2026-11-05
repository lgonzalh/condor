namespace Condor.Core.Models;

/// <summary>
/// Registro local de eventos de entrega informados manualmente por una persona.
/// Condor nunca usa estos datos para ejecutar un despliegue.
/// </summary>
public sealed class DeliveryMetricsState
{
    public List<DeliveryChangeRecord> Changes { get; set; } = new();
}

public sealed class DeliveryChangeRecord
{
    public string ChangeId { get; set; } = string.Empty;
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? DeliveredAtUtc { get; set; }
    public DateTimeOffset? FailedAtUtc { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset? RecoveredAtUtc { get; set; }
    public DateTimeOffset? ReworkAtUtc { get; set; }
    public string? ReworkReason { get; set; }
    public List<string> RelatedTaskExecutionIds { get; set; } = new();
    public List<string> IncidentTaskExecutionIds { get; set; } = new();
}
