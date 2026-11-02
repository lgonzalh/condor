using System;
using System.Collections.Generic;
using System.Linq;
using Condor.Core.Catalog;
using Condor.Core.Evaluation;
using Condor.Core.Models;

namespace Condor.Core.Selection;

public static class ModelSelector
{
    public static ModelSelectionResult RecommendFromCatalog(
        AssessmentResult? assessment,
        IReadOnlyList<ModelCandidate> catalog,
        string? purpose = null)
    {
        var result = new ModelSelectionResult();

        if (assessment is null)
        {
            result.Limitations.Add("No hay Assessment; ejecuta 'condor analizar' primero.");
            return result;
        }

        var memory = assessment.Environment?.Memory;
        var freeDiskBytes = assessment.Environment?.StorageList?.FirstOrDefault()?.FreeBytes ?? 0;

        if (memory?.Status != DetectionStatus.Detected)
        {
            result.Limitations.Add("La memoria RAM no esta disponible; la seleccion es incierta.");
        }

        // Presupuesto estricto usando BudgetPolicy (consistente con SelectForTask).
        var budget = BudgetPolicy.Default.Assess(memory);
        result.Budget = budget;
        result.Resources = ModelMemoryBudget.Snapshot(memory, candidatePeakGb: null);

        var installedModels = assessment.Tools?.Ollama?.Models ?? new List<ModelInfo>();
        var installed = installedModels
            .Select(m => m.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<ModelCandidate> ordered;
        var effectiveCatalog = IncludeInstalledModels(catalog, installedModels);

        try
        {
            ordered = OrderByCompatibility(effectiveCatalog, memory, freeDiskBytes, purpose ?? "agente", budget);
            if (string.Equals(purpose ?? "agente", "agente", StringComparison.OrdinalIgnoreCase))
            {
                ordered = ordered.Where(candidate => candidate.ToolUse).ToList();
            }
        }
        catch
        {
            result.Limitations.Add("Error al evaluar el catalogo.");
            return result;
        }

        // Primero se aprovecha el mejor modelo instalado que realmente cabe.
        // Solo se propone una descarga cuando el inventario local no ofrece un
        // candidato viable para el propósito solicitado.
        var installedViable = ordered
            .Where(candidate => installed.Contains(candidate.Name) || installed.Contains(candidate.PullName))
            .FirstOrDefault();
        var desired = installedViable ?? ordered.FirstOrDefault();

        if (desired is null)
        {
            var hasCandidates = effectiveCatalog.Count > 0;
            if (hasCandidates)
            {
                var smallestPeak = effectiveCatalog
                    .Where(c => c.WeightGb > 0)
                    .Select(c => ModelMemoryBudget.EstimatePeakGb(c.WeightGb, EstimateContextKbGb(c)))
                    .DefaultIfEmpty(1.0)
                    .Min();
                result.Resources = ModelMemoryBudget.Snapshot(memory, smallestPeak);
                result.BlockedByResources = result.Resources.Pressure == ResourcePressure.Insufficient;
                if (result.Resources.Pressure == ResourcePressure.Insufficient)
                {
                    result.Limitations.Add(
                        "Ningun modelo del catalogo cumple ambas condiciones: el porcentaje de RAM total permitido y el presupuesto seguro (" +
                        result.Resources.PressureLabel + ", " + result.Resources.CandidatePercentage?.ToString("0") + "% de la RAM total). No se intenta cargar repetidamente; libera memoria o usa un modelo mas pequeno.");
                    result.Limitations.Add("Presupuesto seguro (RAM libre real - reservas): " + result.Resources.SafeBudgetGb + " GB; la cache no se cuenta como garantia.");
                }
                else
                {
                    result.Limitations.Add("El catalogo de modelos esta vacio o ningun modelo es viable para este equipo.");
                }
            }
            else
            {
                result.Limitations.Add("El catalogo de modelos esta vacio; no hay candidatos.");
            }

            return result;
        }

        // Clasificar la presion respecto al candidato elegido (para alertas honestas).
        var candidatePeak = ModelMemoryBudget.EstimatePeakGb(desired.WeightGb, EstimateContextKbGb(desired));
        result.Resources = ModelMemoryBudget.Snapshot(memory, candidatePeak);

        AddPressureGuidance(result, desired);

        // Instalado: reutilizarlo antes de considerar cualquier descarga.
        if (installed.Contains(desired.Name) || installed.Contains(desired.PullName))
        {
            result.Desired = desired;
            result.AlreadyInstalled = true;
            result.InstalledName = desired.PullName;
            result.Reason = "El modelo deseado ya existe en Ollama; se reutiliza sin descargar.";
            return result;
        }

        result.Desired = desired;
        result.AlreadyInstalled = false;
        result.Reason = "El modelo deseado no existe en Ollama; requerriria obtencion automatica.";
        result.Alternatives = ordered.Skip(1).Select(c => c.PullName).ToList();

        return result;
    }

    /// <summary>Orienta segun el estado de presion del candidato elegido (advertencias honestas).</summary>
    private static void AddPressureGuidance(ModelSelectionResult result, ModelCandidate desired)
    {
        var resources = result.Resources;
        if (resources is null)
        {
            return;
        }

        switch (resources.Pressure)
        {
            case ResourcePressure.Adjusted:
                result.Limitations.Add(
                    "El modelo " + desired.PullName + " esta en estado Ajustado (" +
                    resources.CandidatePercentage?.ToString("0") + "% de la RAM total). Se permite porque el margen es suficiente, pero vigila la memoria.");
                break;
            case ResourcePressure.Pressure:
                result.Limitations.Add(
                    "El modelo " + desired.PullName + " esta en estado Presion (" +
                    resources.CandidatePercentage?.ToString("0") + "% de la RAM total). Condor degradara la carga y recomienda cerrar los procesos de alto consumo para estabilidad.");
                if (resources.TopConsumers.Count > 0)
                {
                    var names = string.Join(", ", resources.TopConsumers.Select(c => c.ProcessName));
                    result.Limitations.Add("Consumidores relevantes detectados (solo lectura; Condor no cierra procesos): " + names + ".");
                }
                break;
            case ResourcePressure.Insufficient:
                result.Limitations.Add(
                    "El modelo " + desired.PullName + " es insuficiente (" +
                    resources.CandidatePercentage?.ToString("0") + "% de la RAM total); no cumple ambas condiciones. No se descarga ni se carga y no se reintenta en bucle.");
                break;
        }
    }

    private static bool IsAtLeastAsCapable(ModelCandidate candidate, ModelCandidate reference)
    {
        if (candidate.MultiFileLevel != reference.MultiFileLevel)
        {
            return candidate.MultiFileLevel >= reference.MultiFileLevel;
        }
        if (candidate.CodingLevel != reference.CodingLevel)
        {
            return candidate.CodingLevel >= reference.CodingLevel;
        }
        return candidate.StructuredOutput == reference.StructuredOutput;
    }

    private static List<ModelCandidate> OrderByCompatibility(
        IReadOnlyList<ModelCandidate> catalog,
        MemoryInfo? memory,
        long freeDiskBytes,
        string purpose,
        BudgetAssessment budget)
    {
        var viable = new List<ModelCandidate>();

        foreach (var candidate in catalog)
        {
            if (candidate.WeightGb <= 0)
            {
                continue;
            }

            var contextGb = EstimateContextKbGb(candidate);

            if (memory is not null && memory.Status == DetectionStatus.Detected)
            {
                var totalGb = memory.TotalBytes / (double)ModelMemoryBudget.BytesPerGb;
                var freeGb = memory.FreeBytes / (double)ModelMemoryBudget.BytesPerGb;

                // Ambas condiciones de carga: porcentaje de RAM total permitido Y
                // presupuesto estricto (BudgetPolicy: peak < budget).
                var peak = ModelMemoryBudget.EstimatePeakGb(candidate.WeightGb, contextGb);

                // Condicion 1: porcentaje de RAM total
                var byRatio = ModelMemoryBudget.ClassifyByRatio(peak, totalGb);
                if (byRatio == ResourcePressure.Insufficient)
                {
                    continue;
                }

                // Condicion 2: presupuesto estricto (peak < budget)
                if (!budget.IsBudgeted || !budget.Admits(peak))
                {
                    continue;
                }
            }

            if (freeDiskBytes > 0)
            {
                var workReserveGb = 4.0; // obj/bin/logs/checkpoints y repos del harness
                var freeDiskGb = freeDiskBytes / (double)ModelMemoryBudget.BytesPerGb;

                if (!ModelMemoryBudget.FitsInDisk(candidate.WeightGb, workReserveGb, freeDiskGb))
                {
                    continue;
                }
            }

            if (candidate.Purpose is not null &&
                !candidate.Purpose.Equals(purpose, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            viable.Add(candidate);
        }

        // Orden: maxima capacidad de ingenieria dentro del presupuesto seguro.
        // No "el mas pequeno que cabe" ni "el mas potente": mejor capacidad real
        // ejecutable de forma estable. Desempates por menor peso (mas estable).
        return viable
            .OrderByDescending(c => HasCapability(c.Capabilities, "thinking"))
            .ThenByDescending(c => c.MultiFileLevel)
            .ThenByDescending(c => c.CodingLevel)
            .ThenByDescending(c => c.ToolUse ? 1 : 0)
            .ThenByDescending(c => StructuredOutputBonus(c))
            .ThenBy(c => c.WeightGb)
            .ThenBy(c => c.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Seleccion por TAREA + presupuesto (harness dinamico).
    ///
    /// Flujo:
    ///   1. Evalua el presupuesto real (stock - reservas - margen) con la politica.
    ///   2. Filtra el catalogo por SUFICIENCIA funcional para el requisito de la tarea.
    ///   3. Entre los suficientes dentro del presupuesto, elige el MENOR suficiente
    ///      (eficiencia) que deje margen operativo (1-).
    ///   4. Determina el siguiente candidato razonable para cuando aumente el presupuesto (1+).
    ///   5. Considera los modelos INSTALADOS del usuario como candidatos validos,
    ///      aunque no sean la primera opcion del catalogo.
    ///
    /// Conserva la compatibilidad con la seleccion clasica; esta entrada enriquece
    /// con 1-, 1+, presupuesto, reserva e insuficientes. Es una funcion pura (sin IO).
    /// </summary>
    public static ModelSelectionResult SelectForTask(
        AssessmentResult? assessment,
        IReadOnlyList<ModelCandidate> catalog,
        TaskModelRequirement requirement,
        BudgetPolicy policy,
        string? currentModelName = null,
        double lastSelectionBudgetGb = 0,
        double lastSelectionFreeRamGb = 0)
    {
        var result = new ModelSelectionResult { Requirement = requirement };

        if (assessment is null)
        {
            result.Limitations.Add("No hay Assessment; el presupuesto no se puede calcular.");
            return result;
        }

        var memory = assessment.Environment?.Memory;
        result.Budget = policy.Assess(memory);
        BudgetAssessment budget = result.Budget ?? BudgetAssessment.NoData();
        result.Resources = ModelMemoryBudget.Snapshot(memory, candidatePeakGb: null);

        var installedModels = assessment.Tools?.Ollama?.Models ?? new List<ModelInfo>();
        var installed = installedModels
            .Select(m => m.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var effectiveCatalog = IncludeInstalledModels(catalog, installedModels);

        // La sesion no salta niveles cuando mejora la memoria: la promocion
        // siempre avanza al siguiente modelo de la misma familia. Por ejemplo,
        // qwen2.5-coder:0.5b solo puede promocionar primero a 1.5b, nunca a 3b.
        var current = FindCatalogModel(effectiveCatalog, currentModelName);
        var immediateUpgrade = FindImmediateUpgrade(effectiveCatalog, current);

        // Check if active model is sufficient and installed
        // Rule: ActiveModel stays stable while sufficient AND conditions haven't improved
        // Rule: Promotion only happens if there's a REAL improvement in budget/RAM
        // Rule: New task start alone does NOT constitute a condition change
        // Rule: Promotion is always only to the next tier (immediate upgrade)
        // Rule: If next tier doesn't fit, stay on ActiveModel
        // Rule: Detect improvement by comparing BOTH budget AND free RAM
        //   - Budget increase: headroom grew
        //   - Free RAM increase: memory was freed (may not affect budget in edge cases)
        var currentBudgetGb = budget?.BudgetGb ?? 0;
        // Use the rounded FreeGb from Resources (same as what gets stored/persisted)
        var currentFreeRamGb = result.Resources?.FreeGb ?? 0;
        var budgetImproved = lastSelectionBudgetGb > 0 && currentBudgetGb > lastSelectionBudgetGb + 0.01;
        var freeRamImproved = lastSelectionFreeRamGb > 0 && currentFreeRamGb > lastSelectionFreeRamGb + 0.01;
        var conditionsImproved = budgetImproved || freeRamImproved;

        if (current is not null &&
            ModelEfficiencyEvaluator.IsSufficient(current, requirement) &&
            (installed.Contains(current.Name) || installed.Contains(current.PullName)))
        {
            if (!conditionsImproved)
            {
                // Conditions haven't improved - stay on current model
                result.Desired = current;
                result.NodeInCurrent = current;
                result.AlreadyInstalled = true;
                result.InstalledName = current.PullName;
                result.Reason = "Modelo activo actual suficiente para la tarea; presupuesto y RAM sin mejora.";
                return result;
            }

            // Conditions improved (budget and/or RAM) - check if immediate upgrade (next tier) fits and is sufficient
            if (immediateUpgrade is not null &&
                ModelEfficiencyEvaluator.IsSufficient(immediateUpgrade, requirement) &&
                CanUse(immediateUpgrade, budget))
            {
                // Promote to immediate upgrade (next tier only)
                result.NodeInCurrent = immediateUpgrade;
                result.Desired = immediateUpgrade;
                result.AlreadyInstalled = installed.Contains(immediateUpgrade.Name) || installed.Contains(immediateUpgrade.PullName);
                result.InstalledName = immediateUpgrade.PullName;
                result.NextCandidate = FindImmediateUpgrade(catalog, immediateUpgrade);
                result.Reason = "Promocion por mejora de recursos: la memoria permite el siguiente nivel " + immediateUpgrade.PullName + " sin saltar modelos.";
                return result;
            }

            // Conditions improved but immediate upgrade doesn't fit - stay on current model
            result.Desired = current;
            result.NodeInCurrent = current;
            result.AlreadyInstalled = true;
            result.InstalledName = current.PullName;
            result.Reason = "Modelo activo actual suficiente; el siguiente nivel no cabe pese a mejora de recursos.";
            return result;
        }

        // Candidatos del catalogo suficientes para la tarea y dentro del presupuesto.
        var candidates = new List<(ModelCandidate c, double peak, bool installed)>();
        foreach (var c in effectiveCatalog)
        {
            if (c.WeightGb <= 0) continue;
            var isInstalled = installed.Contains(c.Name) || installed.Contains(c.PullName);

            if (!ModelEfficiencyEvaluator.IsSufficient(c, requirement))
            {
                result.InsufficientCandidates.Add(c.PullName);
                continue;
            }

            candidates.Add((c, ModelEfficiencyEvaluator.PeakGb(c), isInstalled));
        }

        // Primero se aplican los limites de RAM y margen seguro. Para consultas
        // directas gana el menor pico; para tareas de agente, gana el perfil
        // funcional mas fuerte que cabe, con menor pico como desempate de rapidez.
        var budgetedCandidates = candidates
            .Where(x => budget!.IsBudgeted && budget!.Admits(x.peak) && ModelEfficiencyEvaluator.LeavesMargin(x.c, budget!))
            .ToList();
        var viable = requirement.PreferSmallestSufficient
            ? budgetedCandidates
                .OrderBy(x => x.peak)
                .ThenBy(x => x.c.WeightGb)
                .ThenBy(x => x.c.Name, StringComparer.Ordinal)
                .ToList()
            : budgetedCandidates
                .OrderByDescending(x => HasCapability(x.c.Capabilities, "thinking"))
                .ThenByDescending(x => x.c.MultiFileLevel)
                .ThenByDescending(x => x.c.CodingLevel)
                .ThenByDescending(x => x.c.ToolUse)
                .ThenByDescending(x => x.c.StructuredOutput)
                .ThenByDescending(x => x.c.Stability)
                .ThenBy(x => x.peak)
                .ThenBy(x => x.c.WeightGb)
                .ThenBy(x => x.c.Name, StringComparer.Ordinal)
                .ToList();

        // Una consulta de texto no requiere el protocolo de herramientas. Si
        // la politica completa no deja presupuesto positivo (por ejemplo, por
        // una caida momentanea de RAM libre), habilitar la reserva minima y
        // volver a evaluar el candidato mas pequeno. Las tareas de ingenieria
        // conservan la politica completa y nunca usan esta via degradada.
        if (viable.Count == 0 && requirement.IntentKind == TaskIntentKinds.Consult)
        {
            var smallBudget = policy.AssessForSmallModel(memory);
            if (smallBudget.IsBudgeted)
            {
                budget = smallBudget;
                result.Budget = smallBudget;
                viable = candidates
                    .Where(x => budget.Admits(x.peak) && ModelEfficiencyEvaluator.LeavesMargin(x.c, budget))
                    .OrderBy(x => x.peak)
                    .ThenBy(x => x.c.WeightGb)
                    .ToList();
            }
        }

        // 1- : menor suficiente viable dentro del presupuesto con margen.
        var node = viable.FirstOrDefault();
        if (node.c is not null)
        {
            result.NodeInCurrent = node.c;
            result.Desired = node.c;
            result.AlreadyInstalled = node.installed || installed.Contains(node.c.Name) || installed.Contains(node.c.PullName);
            result.InstalledName = node.c.PullName;
            result.Reason = ResultReason(requirement, node.c, budget, "1-");
        }

        // 1+ : siguiente candidato razonable (aun viable pero no elegido por ser
        // el mas eficiente; o insuficiente hoy por margen) para cuando aumente el presupuesto.
        var nextCandidates = candidates
            .Where(x => budget!.IsBudgeted && x.peak < budget!.BudgetGb * 1.6)
            .Where(x => x.c.Name != node.c?.Name)
            .ToList();
        var next = requirement.PreferSmallestSufficient
            ? nextCandidates.OrderBy(x => x.peak).ThenBy(x => x.c.WeightGb).FirstOrDefault()
            : nextCandidates
                .OrderByDescending(x => HasCapability(x.c.Capabilities, "thinking"))
                .ThenByDescending(x => x.c.MultiFileLevel)
                .ThenByDescending(x => x.c.CodingLevel)
                .ThenByDescending(x => x.c.ToolUse)
                .ThenByDescending(x => x.c.StructuredOutput)
                .ThenByDescending(x => x.c.Stability)
                .ThenBy(x => x.peak)
                .ThenBy(x => x.c.WeightGb)
                .FirstOrDefault();
        if (next.c is not null)
        {
                result.NextCandidate = next.c;
            if (next.c.Name != node.c?.Name && node.c is not null)
            {
                result.Alternatives.Add(next.c.PullName);
                var nextPeak = ModelEfficiencyEvaluator.PeakGb(next.c);
                if (!budget!.Admits(nextPeak) || !ModelEfficiencyEvaluator.LeavesMargin(next.c, budget))
                {
                    result.Reason += " La alternativa de mayor capacidad " + next.c.PullName +
                        " requiere un pico estimado de " + nextPeak.ToString("0.00") +
                        " GB y excede el presupuesto seguro actual de " + budget.BudgetGb.ToString("0.00") + " GB.";
                }
            }
        }

        // Modelo instalado del usuario NO en catalogo: considerarlo candidato si es
        // suficiente y cabe; lo usamos como 1-/desired cuando el catalogo no aporta.
        if (node.c is null && result.Desired is null)
        {
            var userInstalled = ConsiderUserInstalledModel(assessment, requirement, budget);
            if (userInstalled is not null)
            {
                result.NodeInCurrent = userInstalled;
                result.Desired = userInstalled;
                result.AlreadyInstalled = true;
                result.InstalledName = userInstalled.PullName;
                result.Reason = "Modelo instalado por el usuario con capacidad suficiente para la tarea y dentro del presupuesto (1-).";
            }
        }

        if (result.Desired is null)
        {
            // Bloqueo por recursos cuando el presupuesto no admite ningun modelo
            // viable (o no hay presupuesto real). Coherente con la seleccion clasica,
            // que marca BlockedByResources cuando la presion es Insuficiente.
            result.BlockedByResources =
                (!budget!.IsBudgeted) ||
                result.Resources?.Pressure == ResourcePressure.Insufficient ||
                viable.Count == 0;

            // Referencia del minimo suficiente para la tarea (aunque no quepa hoy):
            // es lo que informa "RAM requerida estimada" en MODELO NO EJECUTABLE.
            var minimum = candidates.OrderBy(x => x.peak).FirstOrDefault();
            result.MinimumViable = minimum.c;

            result.Limitations.Add(
                "Ningun modelo es suficiente para la tarea dentro del presupuesto real (" +
                (budget is null ? "sin datos" : budget.BudgetGb.ToString("0.0") + " GB") +
                "), conservando la reserva operativa. Se informa de forma honesta, sin reintentos en bucle.");
        }

        return result;
    }

    private static bool CanUse(ModelCandidate candidate, BudgetAssessment? budget)
        => budget is not null && budget.Admits(ModelEfficiencyEvaluator.PeakGb(candidate)) &&
           ModelEfficiencyEvaluator.LeavesMargin(candidate, budget);

    private static ModelCandidate? FindCatalogModel(
        IReadOnlyList<ModelCandidate> catalog,
        string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return null;
        }

        return catalog.FirstOrDefault(c =>
            c.Name.Equals(modelName, StringComparison.OrdinalIgnoreCase) ||
            c.PullName.Equals(modelName, StringComparison.OrdinalIgnoreCase));
    }

    private static ModelCandidate? FindImmediateUpgrade(
        IReadOnlyList<ModelCandidate> catalog,
        ModelCandidate? current)
    {
        if (current is null || current.WeightGb <= 0)
        {
            return null;
        }

        var currentFamily = FamilyKey(current.PullName);
        var currentPeak = ModelEfficiencyEvaluator.PeakGb(current);
        return catalog
            .Where(c => c.WeightGb > 0 &&
                        FamilyKey(c.PullName).Equals(currentFamily, StringComparison.OrdinalIgnoreCase) &&
                        ModelEfficiencyEvaluator.PeakGb(c) > currentPeak + 0.01)
            .OrderBy(c => ModelEfficiencyEvaluator.PeakGb(c))
            .ThenBy(c => c.WeightGb)
            .FirstOrDefault();
    }

    private static string FamilyKey(string modelName)
    {
        var separator = modelName.IndexOf(':');
        return (separator < 0 ? modelName : modelName[..separator]).Trim();
    }

    private static string ResultReason(TaskModelRequirement req, ModelCandidate c, BudgetAssessment? budget, string tag)
    {
        var b = budget is null ? "-" : budget.BudgetGb.ToString("0.0") + " GB de presupuesto";
        var strategy = req.PreferSmallestSufficient
            ? "menor pico entre los modelos suficientes"
            : "mayor perfil de agente viable (razonamiento, herramientas y programacion), con rapidez como desempate";
        var peak = ModelEfficiencyEvaluator.PeakGb(c).ToString("0.00") + " GB de pico estimado";
        return "Modelo " + tag + " (" + c.PullName + "): suficiente para '" +
               (req.Label ?? req.IntentKind) + "'; seleccionado por " + strategy + ", " + peak + " dentro de " + b + ".";
    }

    /// <summary>
    /// Construye un candidato sintetico a partir de un modelo instalado del usuario
    /// que no esta en el catalogo de Condor. Con estimacion conservadora de peso y
    /// capacidades conocidas de Ollama. Null si no es suficiente ni cabe.
    /// </summary>
    private static ModelCandidate? ConsiderUserInstalledModel(
        AssessmentResult? assessment,
        TaskModelRequirement req,
        BudgetAssessment? budget)
    {
        var installedModels = assessment?.Tools?.Ollama?.Models ?? new List<ModelInfo>();
        if (installedModels.Count == 0 || budget is null || !budget.IsBudgeted)
        {
            return null;
        }

        var candidates = new List<(ModelCandidate Candidate, double Peak)>();

        foreach (var m in installedModels)
        {
            var candidate = BuildInstalledCandidate(m, template: null);
            if (!ModelEfficiencyEvaluator.IsSufficient(candidate, req))
            {
                continue;
            }

            var peak = ModelEfficiencyEvaluator.PeakGb(candidate);
            if (budget.Admits(peak) && ModelEfficiencyEvaluator.LeavesMargin(candidate, budget))
            {
                candidates.Add((candidate, peak));
            }
        }

        // El inventario puede venir en un orden arbitrario. Las consultas
        // favorecen rapidez; las tareas de agente, el perfil funcional mas
        // fuerte que cabe en el presupuesto seguro.
        var ordered = req.PreferSmallestSufficient
            ? candidates.OrderBy(x => x.Peak).ThenBy(x => x.Candidate.WeightGb)
            : candidates
                .OrderByDescending(x => HasCapability(x.Candidate.Capabilities, "thinking"))
                .ThenByDescending(x => x.Candidate.MultiFileLevel)
                .ThenByDescending(x => x.Candidate.CodingLevel)
                .ThenByDescending(x => x.Candidate.ToolUse)
                .ThenByDescending(x => x.Candidate.StructuredOutput)
                .ThenByDescending(x => x.Candidate.Stability)
                .ThenBy(x => x.Peak)
                .ThenBy(x => x.Candidate.WeightGb);

        return ordered
            .Select(x => x.Candidate)
            .FirstOrDefault();
    }

    private static List<ModelCandidate> IncludeInstalledModels(
        IReadOnlyList<ModelCandidate> catalog,
        IReadOnlyList<ModelInfo> installedModels)
    {
        var candidates = catalog.ToList();
        foreach (var model in installedModels)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                continue;
            }

            var index = candidates.FindIndex(candidate =>
                candidate.Name.Equals(model.Name, StringComparison.OrdinalIgnoreCase) ||
                candidate.PullName.Equals(model.Name, StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
            {
                candidates[index] = BuildInstalledCandidate(model, candidates[index]);
            }
            else if (HasToolUse(model.Capabilities))
            {
                // Los modelos fuera del catálogo se consideran con los datos
                // reales que expone Ollama, sin inventar niveles de código,
                // JSON ni uso de herramientas.
                candidates.Add(BuildInstalledCandidate(model, template: null));
            }
        }

        return candidates;
    }

    private static ModelCandidate BuildInstalledCandidate(ModelInfo model, ModelCandidate? template)
    {
        var capabilities = model.Capabilities ?? new List<string>();
        var hasReportedCapabilities = capabilities.Count > 0;
        var codingLevel = template?.CodingLevel ?? EstimateCodingLevel(model);
        var weightGb = model.SizeBytes > 0
            ? EstimateWeightFromSizeBytes(model.SizeBytes)
            : template?.WeightGb ?? 1.0;

        return new ModelCandidate
        {
            Name = model.Name,
            PullName = model.Name,
            SizeBytes = model.SizeBytes > 0 ? model.SizeBytes : template?.SizeBytes ?? 0,
            Family = model.Family ?? template?.Family ?? FamilyKey(model.Name),
            ParameterSize = model.ParameterSize ?? template?.ParameterSize,
            Quantization = model.Quantization ?? template?.Quantization,
            Capabilities = hasReportedCapabilities
                ? new List<string>(capabilities)
                : new List<string>(template?.Capabilities ?? new List<string>()),
            WeightGb = weightGb,
            ContextWindow = model.ContextLength is { } context
                ? (int)System.Math.Clamp(context, 1, int.MaxValue)
                : template?.ContextWindow ?? 8192,
            CodingLevel = codingLevel,
            MultiFileLevel = template?.MultiFileLevel ?? (codingLevel >= 3 ? 2 : codingLevel > 0 ? 1 : 0),
            // Ollama reporta capabilities conocidas, pero no suele reportar
            // JSON. Se conserva el perfil verificado del catálogo cuando exista.
            StructuredOutput = HasCapability(capabilities, "structured-output") ||
                               HasCapability(capabilities, "json") ||
                               (template?.StructuredOutput ?? false),
            ToolUse = hasReportedCapabilities ? HasToolUse(capabilities) : template?.ToolUse ?? false,
            Stability = template?.Stability ?? true,
            Purpose = template?.Purpose
        };
    }

    private static int EstimateCodingLevel(ModelInfo model)
    {
        var identity = (model.Name + " " + model.Family).ToLowerInvariant();
        if (!identity.Contains("coder", StringComparison.Ordinal) &&
            !identity.Contains("code", StringComparison.Ordinal))
        {
            return 0;
        }

        var parameterSize = (model.ParameterSize ?? "").Trim().TrimEnd('B', 'b');
        if (!double.TryParse(parameterSize, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parameters))
        {
            return 1;
        }

        if (parameters >= 7) return 4;
        if (parameters >= 3) return 3;
        if (parameters >= 1.5) return 2;
        return 1;
    }

    private static bool HasToolUse(IEnumerable<string>? capabilities)
        => HasCapability(capabilities, "tool-use") || HasCapability(capabilities, "tools");

    private static bool HasCapability(IEnumerable<string>? capabilities, string capability)
        => capabilities?.Any(value => value.Equals(capability, StringComparison.OrdinalIgnoreCase)) == true;

    private static bool SufficientFromCapabilities(ModelInfo m, TaskModelRequirement req)
    {
        var candidate = BuildInstalledCandidate(m, template: null);
        return ModelEfficiencyEvaluator.IsSufficient(candidate, req);
    }

    private static double EstimateWeightFromSizeBytes(long sizeBytes)
    {
        // Estimacion conservadora del peso en GB a partir del tamano real en disco.
        const double BytesPerGb = 1024.0 * 1024 * 1024;
        return sizeBytes > 0 ? sizeBytes / BytesPerGb : 1.0;
    }

    // KV cache estimada para un contexto moderado de tarea de ingenieria.
    private static double EstimateContextKbGb(ModelCandidate candidate)
    {
        // Estimacion conservadora: peso-KV proporcional al contexto usado (8k).
        const double ContextTokens = 8192;
        const double BytesPerToken = 0.125F; // Q8 KV ~0.125 bytes/token por capa reduce
        return ContextTokens * BytesPerToken / ModelMemoryBudget.BytesPerGb;
    }

    internal static int StructuredOutputBonus(ModelCandidate candidate) => candidate.StructuredOutput ? 1 : 0;
}
