namespace Condor.Core.Evaluation;

using Condor.Core.Models;

/// <summary>
/// Evalua la SUFICIENCIA y la EFICIENCIA de un modelo candidato para un
/// requisito de tarea, dentro de un presupuesto. La decisión no es "el más
/// grande que cabe" ni "el más pequeño que cabe": es "el más pequeño que sea
/// funcionalmente suficiente dentro del presupuesto conservando la reserva".
///
/// Suficiencia funcional:
///   - cumple el nivel de codigo requerido?
///   - cumple el nivel de archivos multiples requerido?
///   - soporta tool-use si la tarea lo exige?
///   - soporta salida estructurada si la tarea lo exige?
///   - cabe en el presupuesto (peak estimado <= presupuesto real)?
///
/// Eficiencia:
///   - menor peso => mayor eficiencia entre los candidatos suficientes.
/// </summary>
public static class ModelEfficiencyEvaluator
{
    /// <summary>True si el candidato es funcionalmente suficiente para la tarea.</summary>
    public static bool IsSufficient(ModelCandidate c, TaskModelRequirement req)
    {
        if (c.CodingLevel < req.RequiredCodingLevel) return false;
        if (c.MultiFileLevel < req.RequiredMultiFileLevel) return false;
        if (req.RequiresToolUse && !c.ToolUse) return false;
        if (req.RequiresStructuredOutput && !c.StructuredOutput) return false;
        return true;
    }

    /// <summary>Puede la RAM real soportar este candidato (peak dentro del presupuesto)?</summary>
    public static bool FitsBudget(ModelCandidate c, BudgetAssessment budget)
    {
        if (!budget.IsBudgeted) return false;
        var peak = ModelMemoryBudget.EstimatePeakGb(c.WeightGb, EstimateContext(c));
        return budget.Admits(peak);
    }

    /// <summary>Costo pico estimado (GB) de un candidato en el contexto de tarea de ingenieria.</summary>
    public static double PeakGb(ModelCandidate c)
        => ModelMemoryBudget.EstimatePeakGb(c.WeightGb, EstimateContext(c));

    /// <summary>
    /// Indica si este candidato deja margen razonable sobre la reserva, de modo
    /// que su uso no consume el colchón operativo (evita presupuesto -> 0).
    /// </summary>
    public static bool LeavesMargin(ModelCandidate c, BudgetAssessment budget)
    {
        if (!budget.IsBudgeted) return false;
        var remaining = budget.BudgetGb - PeakGb(c);
        // margen residual >= 10% del presupuesto (o >= 0.5 GB) para estabilidad.
        return remaining >= System.Math.Min(0.5, budget.BudgetGb * 0.10);
    }

    private static double EstimateContext(ModelCandidate c)
    {
        // KV estimada proporcional al contexto usado (8k) como referencia.
        const double ContextTokens = 8192;
        const double BytesPerToken = 0.125;
        return ContextTokens * BytesPerToken / ModelMemoryBudget.BytesPerGb;
    }
}
