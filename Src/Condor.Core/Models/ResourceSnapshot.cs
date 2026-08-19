using System.Collections.Generic;

namespace Condor.Core.Models;

/// <summary>Estado de presion de recursos de memoria, sin inventar metricas.</summary>
public enum ResourcePressure
{
    /// <summary>Recursos suficientes para el modelo deseado.</summary>
    Normal,

    /// <summary>El modelo cabe pero con margen reducido; se sugiere un modelo menor si existe.</summary>
    Adjusted,

    /// <summary>El modelo mas pequeno apenas cabe (headroom <= 0); se recomienda liberar memoria.</summary>
    Pressure,

    /// <summary>Ningun modelo cabe de forma segura; no se debe intentar cargar en bucle.</summary>
    Insufficient
}

/// <summary>Un proceso consumidor de RAM detectado (solo lectura; nunca se cierra).</summary>
public class RamConsumer
{
    public string ProcessName { get; init; } = "";
    public double WorkingSetGb { get; init; }
    public int ProcessId { get; init; }
}

/// <summary>
/// Instantanea calculada de recursos con desglose explicito y un veredicto de
/// presion. La cache NO cuenta como RAM garantizada: el presupuesto seguro se
/// basa en la RAM libre real menos las reservas del sistema, Condor y el margen.
/// </summary>
public class ResourceSnapshot
{
    public double TotalGb { get; init; }
    public double FreeGb { get; init; }
    public double AvailableGb { get; init; }
    public double CacheGb { get; init; }

    public double SystemReserveGb { get; init; }
    public double CondorReserveGb { get; init; }
    public double SafetyMarginGb { get; init; }

    /// <summary>RAM libre real menos reservas (sistema + Condor + margen). Base del presupuesto seguro.</summary>
    public double HeadroomGb { get; init; }

    /// <summary>Presupuesto seguro dinamico = RAM libre real - reservaSO - reservaCondor - margenSeguridad. La cache NO entra.</summary>
    public double SafeBudgetGb { get; init; }

    /// <summary>Veredicto de presion (porcentaje de RAM total + presupuesto seguro).</summary>
    public ResourcePressure Pressure { get; init; }

    /// <summary>Procesos de alto consumo detectados (solo lectura; nunca se cierran).</summary>
    public IReadOnlyList<RamConsumer> TopConsumers { get; init; } = System.Array.Empty<RamConsumer>();

    /// <summary>Costo estimado del modelo candidato (peak), si se evalúa contra uno.</summary>
    public double? CandidatePeakGb { get; init; }

    /// <summary>Porcentaje de la RAM total que ocupa el candidato (RAM total = 100%), si se evalúa contra uno.</summary>
    public double? CandidatePercentage { get; init; }

    public bool HeadroomAllows(double peakGb) => HeadroomGb >= peakGb;

    /// <summary>True si la carga del modelo debe degradarse (Presion) y recomendarse cerrar procesos de alto consumo.</summary>
    public bool AdviseDegradeLoad => Pressure == ResourcePressure.Pressure;

    /// <summary>True si se recomienda al usuario cerrar procesos de alto consumo para estabilizar el equipo.</summary>
    public bool AdviseCloseConsumers => Pressure is ResourcePressure.Pressure or ResourcePressure.Insufficient;


    public string PressureLabel => Pressure switch
    {
        ResourcePressure.Normal => "Normal",
        ResourcePressure.Adjusted => "Ajustado",
        ResourcePressure.Pressure => "Presion",
        ResourcePressure.Insufficient => "Insuficiente",
        _ => Pressure.ToString()
    };
}
