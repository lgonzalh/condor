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
