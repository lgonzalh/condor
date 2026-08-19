namespace Condor.Core.Models;

public enum AgentPhase
{
    Understanding,
    Observing,
    Analyzing,
    Building,
    Verifying,
    Finalizing
}

/// <summary>Matiz visual honesto del progreso (sin porcentajes inventados).</summary>
public enum ProgressFlag
{
    /// <summary>Progreso normal.</summary>
    Normal,

    /// <summary>El proveedor del modelo se esta recuperando o reintentando.</summary>
    Recovering,

    /// <summary>El proveedor del modelo fallo (proceso terminado / no disponible).</summary>
    ProviderError
}

/// <summary>
/// Evento de progreso honesto del agente: fase, accion actual (si hay), ruta
/// afectada (si hay), iteracion (si hay), un mensaje opcional y un matiz visual.
/// NO contiene porcentajes: solo refleja lo que el agente realmente esta haciendo.
/// </summary>
public sealed class AgentProgress
{
    public AgentPhase Phase { get; init; }
    public string? Action { get; init; }
    public string? Path { get; init; }
    public int? Iteration { get; init; }
    public string? Message { get; init; }
    public ProgressFlag Flag { get; init; } = ProgressFlag.Normal;

    /// <summary>Estado de presion de recursos (informativo): "Normal", "Ajustado", "Presion", "Insuficiente".</summary>
    public string? ResourceState { get; init; }

    /// <summary>RAM disponible libre (GB) en el punto del evento, si estaba disponible.</summary>
    public double? AvailableGb { get; init; }

    /// <summary>Presupuesto seguro dinamico (GB) en el punto del evento, si estaba disponible.</summary>
    public double? SafeBudgetGb { get; init; }

    public static AgentProgress Of(AgentPhase phase, string? action = null, string? path = null, int? iteration = null, string? message = null, ProgressFlag flag = ProgressFlag.Normal, string? resourceState = null, double? availableGb = null, double? safeBudgetGb = null)
        => new() { Phase = phase, Action = action, Path = path, Iteration = iteration, Message = message, Flag = flag, ResourceState = resourceState, AvailableGb = availableGb, SafeBudgetGb = safeBudgetGb };
}
