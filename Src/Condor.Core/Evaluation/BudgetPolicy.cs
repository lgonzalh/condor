namespace Condor.Core.Evaluation;

using Condor.Core.Models;

/// <summary>
/// Politica central de presupuesto de RAM de Cóndor.
///
/// Trata la memoria como un recurso con STOCK, PRESUPUESTO y RESERVA:
///
///   RAM libre real
///   - reserva del sistema operativo (SO), en GB
///   - reserva de Condor + runtime + build
///   - margen operativo (combinado: reserva de seguridad + margen de estabilidad),
///     calculado con ModelMemoryBudget.OperatingMarginGb(totalGb)
///   -------------------------------------------------
///   = presupuesto real de Cóndor (disponible para el modelo)
///
/// El margen operativo NUNCA se considera disponible para el modelo: Condor no
/// elige un modelo solo porque "quepa" si deja al sistema sin margen. La formula
/// unificada garantiza que BudgetPolicy.Assess y ModelMemoryBudget.Snapshot reporten
/// el mismo presupuesto seguro, eliminando contradicciones entre la admision del
/// modelo y el reporte de recursos.
///
/// La politica es configurable (arquitectura) y no queda enterrada como numero
/// magico dentro del selector: el selector recibe una instancia de politica.
/// </summary>
public sealed class BudgetPolicy
{
    /// <summary>Reserva minima del sistema operativo, en GB.</summary>
    public double SystemReserveGb { get; init; }

    /// <summary>Reserva de Condor + runtime + build/test, en GB.</summary>
    public double CondorReserveGb { get; init; }

    /// <summary>Margen de estabilidad esperado durante la ejecucion (anti-swapping), en GB.</summary>
    public double StabilityMarginGb { get; init; }

    public BudgetPolicy(
        double systemReserveGb,
        double condorReserveGb,
        double stabilityMarginGb)
    {
        SystemReserveGb = systemReserveGb;
        CondorReserveGb = condorReserveGb;
        StabilityMarginGb = stabilityMarginGb;
    }

    /// <summary>
    /// Politica por defecto. El margen operativo se calcula con
    /// ModelMemoryBudget.OperatingMarginGb(totalGb), basado en la RAM total,
    /// combinando reserva operativa y margen de estabilidad en un unico valor.
    /// </summary>
    public static BudgetPolicy Default { get; } = new(
        systemReserveGb: ModelMemoryBudget.SystemReserveGb,
        condorReserveGb: ModelMemoryBudget.CondorReserveGb,
        stabilityMarginGb: 1.0);

    /// <summary>
    /// Presupuesto real de Cóndor para el modelo (formula unificada con ModelMemoryBudget).
    ///
    ///   ramFreeGb - SystemReserve - CondorReserve - OperatingMargin(totalGb)
    ///
    /// El margen operativo (reserva de seguridad + margen de estabilidad) se obtiene
    /// de ModelMemoryBudget.OperatingMarginGb(totalGb), basado en la RAM TOTAL, de
    /// modo que este metodo devuelve el mismo valor que BudgetAssessment.BudgetGb
    /// producido por Assess(). Nunca supera la RAM libre real y nunca es negativo.
    /// </summary>
    public double BudgetGb(double ramTotalGb, double ramFreeGb)
    {
        var operative = ModelMemoryBudget.OperatingMarginGb(ramTotalGb);
        var budget = ramFreeGb - SystemReserveGb - CondorReserveGb - operative;
        return System.Math.Max(0, budget);
    }

    /// <summary>Total de reservas (sistema + Condor + margen operativo).</summary>
    public double ReserveGb(double ramTotalGb)
        => SystemReserveGb + CondorReserveGb + ModelMemoryBudget.OperatingMarginGb(ramTotalGb);

    /// <summary>
    /// Evalua el presupuesto para una instantanea de memoria y produce un
    /// veredicto auditable (stock, reserva, presupuesto, margen).
    ///
    /// El margen operativo se calcula con ModelMemoryBudget.OperatingMarginGb(totalGb),
    /// que combina la reserva operativa y el margen de estabilidad en un unico
    /// valor basado en la RAM total (no en la libre), de modo que el presupuesto
    /// real coincide con el headroom reportado por ModelMemoryBudget.Snapshot.
    /// </summary>
    public BudgetAssessment Assess(MemoryInfo? memory)
    {
        if (memory is null || memory.Status != DetectionStatus.Detected || memory.TotalBytes <= 0)
        {
            return BudgetAssessment.NoData();
        }

        var totalGb = memory.TotalBytes / (double)ModelMemoryBudget.BytesPerGb;
        var freeGb = memory.FreeBytes / (double)ModelMemoryBudget.BytesPerGb;
        var operative = ModelMemoryBudget.OperatingMarginGb(totalGb);
        var reserveGb = SystemReserveGb + CondorReserveGb + operative;
        var budget = System.Math.Max(0, freeGb - reserveGb);

        return new BudgetAssessment
        {
            RamTotalGb = System.Math.Round(totalGb, 1),
            RamFreeGb = System.Math.Round(freeGb, 1),
            SystemReserveGb = SystemReserveGb,
            CondorReserveGb = CondorReserveGb,
            OperationalReserveGb = System.Math.Round(operative, 1),
            StabilityMarginGb = StabilityMarginGb,
            ReserveGb = System.Math.Round(reserveGb, 1),
            BudgetGb = System.Math.Round(budget, 1),
            IsBudgeted = budget > 0
        };
    }
}
