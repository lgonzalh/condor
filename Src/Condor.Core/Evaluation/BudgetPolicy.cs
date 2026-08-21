namespace Condor.Core.Evaluation;

using Condor.Core.Models;

/// <summary>
/// Politica central de presupuesto de RAM de Cóndor.
///
/// Trata la memoria como un recurso con STOCK, PRESUPUESTO y RESERVA:
///
///   RAM libre real
///   - reserva operativa de seguridad        (no debe tocarse)
///   - reserva de Condor + runtime + build
///   - margen de estabilidad (anti-swapping)
///   -------------------------------------------------
///   = presupuesto real de Cóndor (disponible para el modelo)
///
/// La reserva operativa NUNCA se considera disponible para el modelo. Condor no
/// elige un modelo solo porque "quepa" si dejar al sistema sin margen.
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

    /// <summary>Reserva operativa de seguridad, en GB. NO se presta al modelo.</summary>
    public double OperationalReserveGb { get; init; }

    /// <summary>Margen de estabilidad esperado durante la ejecucion (anti-swapping), en GB.</summary>
    public double StabilityMarginGb { get; init; }

    /// <summary>
    /// Reserva operativa de seguridad como FRACCION de la RAM libre real.
    /// Se aplica el mayor entre el valor absoluto y esta fraccion (protege
    /// equipos de poca RAM y de mucha RAM).
    /// </summary>
    public double OperationalReserveRatio { get; init; }

    public BudgetPolicy(
        double systemReserveGb,
        double condorReserveGb,
        double operationalReserveGb,
        double stabilityMarginGb,
        double operationalReserveRatio)
    {
        SystemReserveGb = systemReserveGb;
        CondorReserveGb = condorReserveGb;
        OperationalReserveGb = operationalReserveGb;
        StabilityMarginGb = stabilityMarginGb;
        OperationalReserveRatio = operationalReserveRatio;
    }

    /// <summary>
    /// Politica por defecto. La reserva operativa de 2 GB (o el 25% de la RAM
    /// libre, lo que sea mayor) protege al sistema antes de llegar a
    /// "RAM libre ~ presupuesto ~ 0".
    /// </summary>
    public static BudgetPolicy Default { get; } = new(
        systemReserveGb: 1.5,
        condorReserveGb: 1.5,
        operationalReserveGb: 2.0,
        stabilityMarginGb: 1.0,
        operationalReserveRatio: 0.25);

    /// <summary>Reserva operativa efectiva para una RAM libre dada (max absoluto/fraccion).</summary>
    public double EffectiveOperationalReserve(double ramFreeGb)
        => System.Math.Max(OperationalReserveGb, ramFreeGb * OperationalReserveRatio);

    /// <summary>
    /// Presupuesto real de Cóndor para el modelo.
    ///
    ///   ramFreeGb - SystemReserve - CondorReserve - ReserveOperativa - StabilityMargin
    ///
    /// Nunca supera la RAM libre real y nunca es negativo. Documentacion:
    /// la reserva operativa se calcula en funcion de la RAM libre para que
    /// equipos con poca RAM conserven un colchon proporcional y equipos con
    /// mucha RAM nunca comprometan el margen por un monto absoluto bajo.
    /// </summary>
    public double BudgetGb(double ramTotalGb, double ramFreeGb)
    {
        var operative = EffectiveOperationalReserve(ramFreeGb);
        var budget = ramFreeGb - SystemReserveGb - CondorReserveGb - operative - StabilityMarginGb;
        return System.Math.Max(0, budget);
    }

    /// <summary>Total de reservas (sistema + Condor + operativa efectiva), sin margen de estabilidad.</summary>
    public double ReserveGb(double ramFreeGb)
        => SystemReserveGb + CondorReserveGb + EffectiveOperationalReserve(ramFreeGb);

    /// <summary>
    /// Evalua el presupuesto para una instantanea de memoria y produce un
    /// veredicto auditable (stock, reserva, presupuesto, margen).
    /// </summary>
    public BudgetAssessment Assess(MemoryInfo? memory)
    {
        if (memory is null || memory.Status != DetectionStatus.Detected || memory.TotalBytes <= 0)
        {
            return BudgetAssessment.NoData();
        }

        var totalGb = memory.TotalBytes / (double)ModelMemoryBudget.BytesPerGb;
        var freeGb = memory.FreeBytes / (double)ModelMemoryBudget.BytesPerGb;
        var operative = EffectiveOperationalReserve(freeGb);
        var reserveGb = SystemReserveGb + CondorReserveGb + operative;
        var budget = System.Math.Max(0, freeGb - reserveGb - StabilityMarginGb);

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
