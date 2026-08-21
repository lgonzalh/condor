namespace Condor.Core.Models;

/// <summary>Estado de salud de Ollama distinguido por el bootstrap.</summary>
public enum OllamaHealth
{
    /// <summary>Ollama no esta instalado (ni ejecutable presente).</summary>
    NotInstalled,

    /// <summary>Ollama instalado, hay proceso de la app, pero el server no responde.</summary>
    InstalledServerDown,

    /// <summary>Ollama instalado y el server real responde en el endpoint local.</summary>
    ServerAvailable
}

/// <summary>Propiedad de la instancia de Ollama para la politica de cierre.</summary>
public enum OllamaOwnership
{
    /// <summary>Ollama ya estaba ejecutandose antes de Condor; Condor no lo cierra.</summary>
    Existing,

    /// <summary>Condor inicio el proceso del server; Condor gestiona su cierre si corresponde.</summary>
    StartedByCondor
}

/// <summary>
/// Resultado del aprovisionamiento de Ollama (bootstrap de dependencias).
/// Aporta el estado de salud alcanzado, la propiedad de la instancia, el server
/// version cuando esta disponible, y la accion/remedio tomado (instalado,
/// server iniciado o nada). Los detalles tecnicos de diagnostico se mantienen
/// separados de la UI (nunca se muestran como stack traces).
/// </summary>
public sealed class OllamaProvisioningResult
{
    public bool Ok { get; init; }
    public OllamaHealth Health { get; init; } = OllamaHealth.ServerAvailable;
    public OllamaOwnership Ownership { get; init; } = OllamaOwnership.Existing;
    public string? ServerVersion { get; init; }
    public string? Reason { get; init; }

    /// <summary>Descripcion legible de la accion tomada (instalado / server iniciado / reutilizado).</summary>
    public string? Action { get; init; }

    /// <summary>Detalle tecnico para log/diagnostico (nunca para el usuario final).</summary>
    public string? Diagnostic { get; init; }
}
