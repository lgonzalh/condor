namespace Condor.Core.Models;

/// <summary>
/// Veredicto auditable de presupuesto de RAM: stock, presupuesto y reserva.
/// La RAM se trata como un recurso limitado; el presupuesto real
/// = RAM libre - reservas (sistema + Condor + margen operativo combinado),
/// donde el margen operativo abarca reserva de seguridad y estabilidad
/// (OperatingMarginGb sobre la RAM total). La formula esta unificada con
/// ModelMemoryBudget para garantizar coincidencia entre admision y reporte.
/// </summary>
public sealed class BudgetAssessment
{
    public double RamTotalGb { get; init; }
    public double RamFreeGb { get; init; }
    public double SystemReserveGb { get; init; }
    public double CondorReserveGb { get; init; }

    /// <summary>Reserva operativa de seguridad (no se presta al modelo).</summary>
    public double OperationalReserveGb { get; init; }

    public double StabilityMarginGb { get; init; }

    /// <summary>Total de reservas (sistema + Condor + operativa).</summary>
    public double ReserveGb { get; init; }

    /// <summary>Presupuesto real de Cóndor disponible para el modelo.</summary>
    public double BudgetGb { get; init; }

    /// <summary>True si hay presupuesto positivo (headroom real y seguro).</summary>
    public bool IsBudgeted { get; init; }

    /// <summary>True si el coste (peak estimado) del modelo entra en el presupuesto (ESTRICTO: menor al presupuesto).</summary>
    public bool Admits(double candidatePeakGb) => IsBudgeted && BudgetGb > candidatePeakGb;

    public static BudgetAssessment NoData() => new()
    {
        RamTotalGb = 0,
        RamFreeGb = 0,
        SystemReserveGb = 0,
        CondorReserveGb = 0,
        OperationalReserveGb = 0,
        StabilityMarginGb = 0,
        ReserveGb = 0,
        BudgetGb = 0,
        IsBudgeted = false
    };

    public string StockLabel =>
        "RAM libre " + RamFreeGb.ToString("0.0") + " GB | reserva " + ReserveGb.ToString("0.0") +
        " GB | presupuesto " + BudgetGb.ToString("0.0") + " GB";
}
