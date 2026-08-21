namespace Condor.Core.Models;

/// <summary>
/// Veredicto auditable de presupuesto de RAM: stock, presupuesto y reserva.
/// La RAM se trata como un recurso limitado con reserva operativa de seguridad;
/// el presupuesto real = RAM libre - reservas - margen de estabilidad.
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

    /// <summary>True si el coste (peak estimado) del modelo entra en el presupuesto.</summary>
    public bool Admits(double candidatePeakGb) => IsBudgeted && BudgetGb >= candidatePeakGb;

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
