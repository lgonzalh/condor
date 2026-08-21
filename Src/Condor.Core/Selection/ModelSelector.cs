using System;
using System.Collections.Generic;
using System.Linq;
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

        // Instantanea de recursos con desglose y veredicto de presion (sin contar la cache).
        result.Resources = ModelMemoryBudget.Snapshot(memory, candidatePeakGb: null);

        List<ModelCandidate> ordered;

        try
        {
            ordered = OrderByCompatibility(catalog, memory, freeDiskBytes, purpose ?? "agente");
        }
        catch
        {
            result.Limitations.Add("Error al evaluar el catalogo.");
            return result;
        }

        var installed = (assessment.Tools?.Ollama?.Models ?? new List<ModelInfo>())
            .Select(m => m.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var desired = ordered.FirstOrDefault();

        if (desired is null)
        {
            // Distinguimos "catalogo vacio" de "ninguno cabe por recursos".
            var hasCandidates = catalog.Count > 0;
            if (hasCandidates)
            {
                // Ninguno cabe: veredicto basado en el modelo mas pequeno del catalogo.
                // El estado debe reflejar el motivo real: porcentaje de RAM total y
                // presupuesto seguro, nunca ambos-reintentos.
                var smallestPeak = catalog
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
        // En este punto el candidato YA cumplio ambas condiciones (porcentaje + presupuesto
        // seguro), asi que su estado es Normal/Ajustado/Presion segun su porcentaje de RAM.
        var candidatePeak = (double)catalog
            .Where(c => c.Name.Equals(desired.Name, StringComparison.OrdinalIgnoreCase) ||
                        c.PullName.Equals(desired.PullName, StringComparison.OrdinalIgnoreCase))
            .Select(c => ModelMemoryBudget.EstimatePeakGb(c.WeightGb, EstimateContextKbGb(c)))
            .FirstOrDefault();
        result.Resources = ModelMemoryBudget.Snapshot(memory, candidatePeak);

        AddPressureGuidance(result, desired);

        // Instalado: buscar el deseado primero (reutilizar sin descargar).
        if (installed.Contains(desired.Name) || installed.Contains(desired.PullName))
        {
            result.Desired = desired;
            result.AlreadyInstalled = true;
            result.InstalledName = desired.PullName;
            result.Reason = "El modelo deseado ya existe en Ollama; se reutiliza sin descargar.";
            return result;
        }

        // El deseado no esta instalado. Reutilizar una alternativa instalada SOLO
        // si es tan capaz como el deseado. Si el deseado cabe y es mas capaz
        // (p. ej. familia coder frente a un general), es preferible obtenerlo:
        // la seleccion debe buscar la maxima capacidad de ingenieria viable.
        var installedViable = ordered
            .Where(c => installed.Contains(c.Name) || installed.Contains(c.PullName))
            .FirstOrDefault();

        if (installedViable is not null &&
            IsAtLeastAsCapable(installedViable, desired))
        {
            result.Desired = installedViable;
            result.AlreadyInstalled = true;
            result.InstalledName = installedViable.PullName;
            result.Alternatives.Add(desired.PullName);
            result.Reason = "Modelo alternativo ya instalado con capacidad equivalente; se reutiliza.";
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
        string purpose)
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
                // presupuesto seguro (RAM libre real - reservas - margen).
                if (!ModelMemoryBudget.FitsInRamStrict(candidate.WeightGb, contextGb, totalGb, freeGb))
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
            .OrderByDescending(c => c.MultiFileLevel)
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
    ///      (eficiencia) que deje margen operativo (1−).
    ///   4. Determina el siguiente candidato razonable para cuando aumente el presupuesto (1+).
    ///   5. Considera los modelos INSTALADOS del usuario como candidatos validos,
    ///      aunque no sean la primera opcion del catalogo.
    ///
    /// Conserva la compatibilidad con la seleccion clasica; esta entrada enriquece
    /// con 1−, 1+, presupuesto, reserva e insuficientes. Es una funcion pura (sin IO).
    /// </summary>
    public static ModelSelectionResult SelectForTask(
        AssessmentResult? assessment,
        IReadOnlyList<ModelCandidate> catalog,
        TaskModelRequirement requirement,
        BudgetPolicy policy)
    {
        var result = new ModelSelectionResult { Requirement = requirement };

        if (assessment is null)
        {
            result.Limitations.Add("No hay Assessment; el presupuesto no se puede calcular.");
            return result;
        }

        var memory = assessment.Environment?.Memory;
        result.Budget = policy.Assess(memory);
        var budget = result.Budget;
        result.Resources = ModelMemoryBudget.Snapshot(memory, candidatePeakGb: null);

        var installed = (assessment.Tools?.Ollama?.Models ?? new List<ModelInfo>())
            .Select(m => m.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Candidatos del catalogo suficientes para la tarea y dentro del presupuesto.
        var candidates = new List<(ModelCandidate c, double peak, bool installed)>();
        foreach (var c in catalog)
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

        // Ordenar por eficiencia: menor coste primero; desempate por menor peso.
        // El menor suficiente y viable es el 1− por defecto (eficiencia > tamaño).
        var viable = candidates
            .Where(x => budget.IsBudgeted && budget.Admits(x.peak) && ModelEfficiencyEvaluator.LeavesMargin(x.c, budget))
            .OrderBy(x => x.peak)
            .ThenBy(x => x.c.WeightGb)
            .ToList();

        // 1− : menor suficiente viable dentro del presupuesto con margen.
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
        var next = candidates
            .Where(x => budget.IsBudgeted && x.peak < budget.BudgetGb * 1.6)
            .Where(x => x.c.Name != node.c?.Name)
            .OrderBy(x => x.peak)
            .FirstOrDefault();
        if (next.c is not null)
        {
            result.NextCandidate = next.c;
            if (next.c.Name != node.c?.Name && node.c is not null)
            {
                result.Alternatives.Add(next.c.PullName);
            }
        }

        // Modelo instalado del usuario NO en catalogo: considerarlo candidato si es
        // suficiente y cabe; lo usamos como 1−/desired cuando el catalogo no aporta.
        if (node.c is null)
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
                (!budget.IsBudgeted) ||
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

    private static string ResultReason(TaskModelRequirement req, ModelCandidate c, BudgetAssessment? budget, string tag)
    {
        var b = budget is null ? "-" : budget.BudgetGb.ToString("0.0") + " GB de presupuesto";
        return "Modelo " + tag + " (" + c.PullName + "): suficiente para '" +
               (req.Label ?? req.IntentKind) + "' y eficiente dentro de " + b + ".";
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

        foreach (var m in installedModels)
        {
            if (!SufficientFromCapabilities(m, req))
            {
                continue;
            }

            var weightGb = EstimateWeightFromSizeBytes(m.SizeBytes);
            var candidate = new ModelCandidate
            {
                Name = m.Name,
                PullName = m.Name,
                Family = m.Family,
                ParameterSize = m.ParameterSize,
                Quantization = m.Quantization,
                ContextWindow = m.ContextLength is { } ctx ? (int)ctx : 8192,
                SizeBytes = m.SizeBytes,
                WeightGb = weightGb,
                CodingLevel = req.RequiredCodingLevel,
                MultiFileLevel = req.RequiredMultiFileLevel,
                StructuredOutput = m.Capabilities.Contains("structured-output") || req.RequiresStructuredOutput,
                ToolUse = m.Capabilities.Contains("tool-use") || req.RequiresToolUse,
                Stability = true,
                Capabilities = new List<string>(m.Capabilities)
            };

            var peak = ModelEfficiencyEvaluator.PeakGb(candidate);
            if (budget.Admits(peak) && ModelEfficiencyEvaluator.LeavesMargin(candidate, budget))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool SufficientFromCapabilities(ModelInfo m, TaskModelRequirement req)
    {
        var caps = m.Capabilities ?? new List<string>();
        if (req.RequiresToolUse && !caps.Contains("tool-use")) return false;
        return true;
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
