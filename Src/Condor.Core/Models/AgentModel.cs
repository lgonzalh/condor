using System;
using System.Collections.Generic;

namespace Condor.Core.Models;

public class AgentStep
{
    public int Iteration { get; set; }
    public string Action { get; set; } = "";
    public string? Path { get; set; }
    public bool Success { get; set; }
    public string? ResultPreview { get; set; }
    public DateTime AtUtc { get; set; }
}

public class AgentCheckpoint
{
    public string SchemaVersion { get; set; } = "1.0.0";
    public string Task { get; set; } = "";
    public int Iteration { get; set; }
    public string Model { get; set; } = "";
    public string Strategy { get; set; } = "";
    public string? LastDecision { get; set; }
    public string? LastAction { get; set; }
    public string? LastResult { get; set; }
    public string? HarnessState { get; set; }
    public string? LastError { get; set; }
    public string? NextAction { get; set; }

    /// <summary>Estado de presion de recursos en el ultimo punto evaluado (informativo).</summary>
    public string? ResourcesPressure { get; set; }

    /// <summary>Headroom de RAM (GB) en el ultimo punto evaluado (informativo).</summary>
    public double? HeadroomGb { get; set; }

    public DateTime GeneratedAtUtc { get; set; }
}

public class AgentResult
{
    public bool Success { get; set; }
    public string? Reason { get; set; }
    public string Model { get; set; } = "";
    public string? Objective { get; set; }
    public List<AgentStep> Steps { get; set; } = new();
    public AgentCheckpoint? Checkpoint { get; set; }

    /// <summary>
    /// Inventario del entorno y de la decision de modelo recopilado por Condor
    /// antes/para la tarea (recursos, CPU, almacenamiento, modelos instalados,
    /// modelo seleccionado y motivo, capacidades verificadas del catalogo).
    /// Opcional: informativo; cuando es null el renderer omite el bloque.
    /// </summary>
    public AgentInventory? Inventory { get; set; }
}

/// <summary>
/// Inventario objetivo que orienta la decision de Condor y se presenta en el
/// analisis. Solo se rellena con datos reales detectados o del catalogo; nunca
/// se inventan capacidades.
/// </summary>
public sealed class AgentInventory
{
    public double RamTotalGb { get; set; }
    public double RamFreeGb { get; set; }
    public double SafeBudgetGb { get; set; }
    public string? PressureLabel { get; set; }
    public string? Cpu { get; set; }
    public double FreeDiskGb { get; set; }
    public List<string>? InstalledModels { get; set; }
    public string? SelectedModel { get; set; }
    public string? SelectionReason { get; set; }
    public List<string>? ModelCapabilities { get; set; }
}

public sealed class AgentLimits
{
    public const string LimitIterations = "agent-iterations";
    public const string LimitTimeout = "agent-timeout";
    public const string LimitModifications = "agent-modifications";
    public const string LimitRepeated = "agent-repeated-action";
    public const string LimitRedundantObservations = "agent-redundant-observations";
    public const string LimitInvalidOutputs = "agent-invalid-outputs";

    public int MaxIterations { get; init; } = 8;
    public int TimeoutMilliseconds { get; init; } = 300_000;
    public int MaxModifications { get; init; } = 8;
    public int MaxRepeatedAction { get; init; } = 3;
    public int MaxRedundantObservations { get; init; } = 2;
    public int MaxInvalidOutputs { get; init; } = 8;
    public int MaxContentLength { get; init; } = 200_000;

    public static AgentLimits Default { get; } = new();
}
