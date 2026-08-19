namespace Condor.Core.Models;

/// <summary>Etapas reales de la preparacion del entorno al arrancar Condor.</summary>
public enum StartupStage
{
    /// <summary>Inicio generico: preparando el entorno.</summary>
    PreparingEnvironment,

    /// <summary>Revisando recursos del sistema (hardware, RAM, almacenamiento, GPU).</summary>
    ReviewingResources,

    /// <summary>Detectando Ollama y su estado.</summary>
    DetectingOllama,

    /// <summary>Evaluando modelos disponibles y viables.</summary>
    EvaluatingModels,

    /// <summary>Seleccionando el modelo mas adecuado.</summary>
    SelectingModel,

    /// <summary>Descargando el modelo (porcentaje real si Ollama lo reporta).</summary>
    DownloadingModel,

    /// <summary>Verificando que el modelo quedo disponible en Ollama.</summary>
    VerifyingModel,

    /// <summary>Entorno preparado y listo.</summary>
    Ready
}

/// <summary>
/// Evento de progreso HONESTO de la preparacion al arrancar Condor. Solo
/// refleja lo que realmente esta ocurriendo en cada etapa. El porcentaje se
/// usa EXCLUSIVAMENTE cuando existe progreso real de descarga reportado por
/// Ollama; en el resto de etapas la UI debe mostrar animacion indeterminada.
/// </summary>
public sealed class StartupProgress
{
    /// <summary>Etapa actual de la preparacion.</summary>
    public StartupStage Stage { get; init; }

    /// <summary>Detalle textual opcional de la etapa (p. ej. modelo seleccionado).</summary>
    public string? Message { get; init; }

    /// <summary>Progreso real de descarga (0-100) SOLO cuando Ollama lo reporte; null en otro caso.</summary>
    public double? DownloadPercent { get; init; }

    /// <summary>True si la etapa termino con exito y se marco correcta (✓).</summary>
    public bool Completed { get; init; }

    public static StartupProgress Of(
        StartupStage stage,
        string? message = null,
        double? downloadPercent = null,
        bool completed = false)
        => new()
        {
            Stage = stage,
            Message = message,
            DownloadPercent = downloadPercent,
            Completed = completed
        };
}
