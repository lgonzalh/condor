namespace Condor.Core.Evaluation;

using System;
using Condor.Core.Models;

/// <summary>
/// Resultado de una reevaluacion de presupuesto en un punto seguro.
/// </summary>
public enum BudgetTransition
{
    /// <summary>Ningun cambio de modelo necesario (contnua con el actual).</summary>
    KeepCurrent,

    /// <summary>La RAM aumento y conviene usar el 1+ (mejor candidato).</summary>
    UpgradeToNext,

    /// <summary>La RAM disminuyo; hay que degradar a una alternativa viable segura.</summary>
    Downgrade
}

/// <summary>
/// Resultado accionable de una reevaluacion periodica del presupuesto.
/// Contempla limite (evita loops) y motivo.
/// </summary>
public sealed class BudgetReevaluation
{
    public BudgetTransition Transition { get; init; } = BudgetTransition.KeepCurrent;
    public string? Reason { get; init; }
    public BudgetAssessment? Budget { get; init; }
    public string? SuggestedModel { get; init; }
    public bool ExhaustedAttempts { get; init; }
}

/// <summary>
/// Reevaluador dinamico del presupuesto. Decide, en un punto seguro, si mantener
/// el modelo actual, subir a 1+ o bajar a una alternativa viable, en funcion del
/// presupuesto real y con un limite de reevaluaciones para no caer en loops.
///
/// Reglas:
///   - Nunca cambia de modelo en medio de una inferencia.
///   - El cambio busca CONTINUIDAD del trabajo: si no hay alternativa segura,
///     mantiene lo que hay.
///   - Cada reevaluacion de una transicion cuenta como intento; tras el limite,
///     se reporta ExhaustedAttempts (detencion honesta, sin bucle).
/// </summary>
public sealed class BudgetReevaluator
{
    private readonly BudgetPolicy _policy;
    private readonly int _maxReevaluations;
    private readonly TimeSpan _reevaluationInterval;

    public BudgetReevaluator(BudgetPolicy policy, int maxReevaluations = 6, TimeSpan? reevaluationInterval = null)
    {
        _policy = policy;
        _maxReevaluations = Math.Max(1, maxReevaluations);
        _reevaluationInterval = reevaluationInterval ?? DefaultReevaluationInterval;
    }

    /// <summary>Intervalo por defecto de reevaluacion periodica (30 min, configurable).</summary>
    public static TimeSpan DefaultReevaluationInterval => TimeSpan.FromMinutes(30);

    /// <summary>Intervalo configurado de reevaluacion periodica.</summary>
    public TimeSpan ReevaluationInterval => _reevaluationInterval;

    public BudgetReevaluation Decide(
        MemoryInfo? memory,
        ModelCandidate? current,
        ModelCandidate? next,
        TaskModelRequirement requirement,
        int alreadyChanged)
    {
        var budget = _policy.Assess(memory);

        if (!budget.IsBudgeted)
        {
            return new BudgetReevaluation
            {
                Transition = BudgetTransition.KeepCurrent,
                Budget = budget,
                Reason = "Sin presupuesto real (reservas y margen agotados); se conserva el modelo actual sin consumo adicional.",
                ExhaustedAttempts = alreadyChanged >= _maxReevaluations
            };
        }

        // Proteccion anti-bucle: si ya cambiamos de modelo demasiadas veces en puntos
        // seguidos, detenemos los cambios y conservamos el actual hasta estabilizarse.
        if (alreadyChanged >= _maxReevaluations)
        {
            return new BudgetReevaluation
            {
                Transition = BudgetTransition.KeepCurrent,
                Budget = budget,
                Reason = "Se alcanzo el limite de reevaluaciones (sin bucle); se conserva el modelo actual para estabilidad.",
                ExhaustedAttempts = true
            };
        }

        if (current is null)
        {
            return new BudgetReevaluation
            {
                Transition = BudgetTransition.KeepCurrent,
                Budget = budget,
                Reason = "No hay modelo activo que evaluar en este punto seguro."
            };
        }

        var currentPeak = ModelEfficiencyEvaluator.PeakGb(current);

        // RAM aumenta: si el modelo actual no es el suficiente mas eficiente y el 1+
        // ahora cabe con margen y es significativamente mejor, sugerir subir.
        if (next is not null && next.Name != current.Name)
        {
            var nextPeak = ModelEfficiencyEvaluator.PeakGb(next);
            var meaningfullyBetter = next.CodingLevel > current.CodingLevel || next.MultiFileLevel > current.MultiFileLevel;
            if (meaningfullyBetter &&
                budget.Admits(nextPeak) &&
                ModelEfficiencyEvaluator.LeavesMargin(next, budget) &&
                nextPeak > currentPeak + 0.3) // solo cuando haya margen real para dar el salto
            {
                return new BudgetReevaluation
                {
                    Transition = BudgetTransition.UpgradeToNext,
                    Budget = budget,
                    SuggestedModel = next.PullName,
                    Reason = "El presupuesto aumento (" + budget.BudgetGb.ToString("0.0") +
                             " GB) y el candidato 1+ (" + next.PullName +
                             ") ahora cabe con margen; cambio en punto seguro."
                };
            }
        }

        // RAM disminuye: si el actual ya no cabe con margen, degradar a una alternativa
        // viable (el propio 1+ si es menor, o el menor del catalogo que quepa).
        if (!budget.Admits(currentPeak) || !ModelEfficiencyEvaluator.LeavesMargin(current, budget))
        {
            var nextPeak = next is null ? double.MaxValue : ModelEfficiencyEvaluator.PeakGb(next);
            if (next is not null && next.Name != current.Name &&
                budget.Admits(nextPeak) && ModelEfficiencyEvaluator.LeavesMargin(next, budget))
            {
                return new BudgetReevaluation
                {
                    Transition = BudgetTransition.Downgrade,
                    Budget = budget,
                    SuggestedModel = next.PullName,
                    Reason = "El presupuesto disminuyo (" + budget.BudgetGb.ToString("0.0") +
                             " GB); el modelo actual deja poco margen; se degrada a " + next.PullName + " en punto seguro."
                };
            }

            return new BudgetReevaluation
            {
                Transition = BudgetTransition.KeepCurrent,
                Budget = budget,
                Reason = "Presupuesto reducido pero sin alternativa segura; se conserva el actual para continuidad.",
                ExhaustedAttempts = false
            };
        }

        return new BudgetReevaluation
        {
            Transition = BudgetTransition.KeepCurrent,
            Budget = budget,
            Reason = "Presupuesto estable y el modelo actual sigue siendo suficiente y eficiente (1-)."
        };
    }
}
