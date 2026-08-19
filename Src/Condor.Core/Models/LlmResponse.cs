namespace Condor.Core.Models;

/// <summary>
/// Clasificacion honesta del resultado de una solicitud al proveedor LLM.
/// Permite a Condor distinguir estados que antes se colapsaban en un unico
/// "Error" (pensando lento, server no disponible, proceso terminado, timeout,
/// respuesta invalida) y actuar en consecuencia (p. ej. no consumir iteraciones
/// del agente cuando el proveedor dejo de existir).
/// </summary>
public enum LlmOutcome
{
    /// <summary>El proveedor respondio correctamente.</summary>
    Ok,

    /// <summary>El proveedor esta respondiendo pero tarda (pensando lento). No es un fallo.</summary>
    Thinking,

    /// <summary>El servidor no responde (no lanzado por nosotros; no es un proceso que Condor gestione).</summary>
    ServerUnavailable,

    /// <summary>El proceso del proveedor modelo termino inesperadamente (crash).</summary>
    ProcessEnded,

    /// <summary>Se supero el tiempo de espera de una solicitud.</summary>
    Timeout,

    /// <summary>El proveedor devolvio una respuesta no utilizable / no valida.</summary>
    InvalidResponse
}

public class LlmResponse
{
    public bool Success { get; set; }
    public string? Content { get; set; }
    public string? Model { get; set; }
    public string? Error { get; set; }

    /// <summary>Clasificacion estructurada del resultado (para no mezclar estados).</summary>
    public LlmOutcome Outcome { get; set; } = LlmOutcome.Ok;

    /// <summary>Codigo de salida del proceso del proveedor, si estuvo disponible.</summary>
    public int? ProcessExitCode { get; set; }

    /// <summary>Contenido de stderr del proceso del proveedor, si estuvo disponible.</summary>
    public string? ProcessStderr { get; set; }

    /// <summary>Marca temporal en UTC del fallo, si lo hubo.</summary>
    public System.DateTime? FailedAtUtc { get; set; }
}
