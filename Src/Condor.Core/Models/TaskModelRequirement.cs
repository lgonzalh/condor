namespace Condor.Core.Models;

/// <summary>
/// Requisito funcional de una tarea para la seleccion inteligente de modelo.
/// Captura QUÉ necesita hacer la tarea (capacidades) y una polaridad de
/// eficiencia: preferir el modelo MENOR/suficiente (eficiencia) sobre el mayor
/// que cabe. El tamaño no es el único criterio: lo es la suficiencia funcional
/// dentro del presupuesto, conservando una reserva operativa.
/// </summary>
public sealed class TaskModelRequirement
{
    /// <summary>Identificador estandar de la necesidad (consulta|agente|coding|analisis).</summary>
    public string IntentKind { get; init; } = TaskIntentKinds.Agent;

    /// <summary>Nivel de capacidad de codigo minimo requerido (0..5).</summary>
    public int RequiredCodingLevel { get; init; }

    /// <summary>Nivel de archivos multiples / proyecto minimo requerido (0..4).</summary>
    public int RequiredMultiFileLevel { get; init; }

    /// <summary>Requiere tool-use (acciones sobre el sistema de archivos).</summary>
    public bool RequiresToolUse { get; init; }

    /// <summary>Requiere salida estructurada (JSON de accion).</summary>
    public bool RequiresStructuredOutput { get; init; }

    /// <summary>
    /// True si se prefiere el modelo MAS PEQUENO que sea suficiente (eficiencia).
    /// False si la tarea se beneficia de la maxima capacidad viable.
    /// </summary>
    public bool PreferSmallestSufficient { get; init; }

    /// <summary>Descripcion legible de la necesidad (para diagnostico/inventario).</summary>
    public string? Label { get; init; }
}

public static class TaskIntentKinds
{
    public const string Agent = "agente";
    public const string Consult = "consulta";
    public const string Coding = "coding";
    public const string Analysis = "analisis";
    public const string Vision = "vision";
}
