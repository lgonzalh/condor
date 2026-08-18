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
        IReadOnlyList<ModelCandidate> catalog)
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

        List<ModelCandidate> ordered;

        try
        {
            ordered = OrderByCompatibility(catalog, memory, freeDiskBytes);
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
            result.Limitations.Add("No hay un modelo compatible en el catalogo para este equipo.");
            return result;
        }

        // Instalado: buscar el deseado primero, luego alternativa instalada.
        if (installed.Contains(desired.Name) || installed.Contains(desired.PullName))
        {
            result.Desired = desired;
            result.AlreadyInstalled = true;
            result.InstalledName = desired.PullName;
            result.Reason = "El modelo deseado ya existe en Ollama; se reutiliza sin descargar.";
            return result;
        }

        var installedViable = ordered
            .Where(c => installed.Contains(c.Name) || installed.Contains(c.PullName))
            .FirstOrDefault();

        if (installedViable is not null)
        {
            result.Desired = installedViable;
            result.AlreadyInstalled = true;
            result.InstalledName = installedViable.PullName;
            result.Alternatives.Add(desired.PullName);
            result.Reason = "Modelo alternativo ya instalado; se reutiliza (el deseado no esta disponible).";
            return result;
        }

        result.Desired = desired;
        result.AlreadyInstalled = false;
        result.Reason = "El modelo deseado no existe en Ollama; requerriria obtencion automatica.";
        result.Alternatives = ordered.Skip(1).Select(c => c.PullName).ToList();

        return result;
    }

    private static List<ModelCandidate> OrderByCompatibility(
        IReadOnlyList<ModelCandidate> catalog,
        MemoryInfo? memory,
        long freeDiskBytes)
    {
        var viable = new List<ModelCandidate>();

        foreach (var candidate in catalog)
        {
            if (candidate.SizeBytes <= 0)
            {
                continue;
            }

            if (memory is not null && memory.Status == DetectionStatus.Detected)
            {
                var totalGb = memory.TotalBytes / (double)ModelMemoryBudget.BytesPerGb;
                var freeGb = memory.FreeBytes / (double)ModelMemoryBudget.BytesPerGb;

                if (!ModelMemoryBudget.FitsInRam(candidate.SizeBytes, totalGb, freeGb))
                {
                    continue;
                }
            }

            if (freeDiskBytes > 0 &&
                !ModelMemoryBudget.FitsInDisk(candidate.SizeBytes, freeDiskBytes))
            {
                continue;
            }

            viable.Add(candidate);
        }

        // Preferencia: menor consumo en RAM, luego nombre alfabetico (determinista).
        return viable
            .OrderBy(c => c.SizeBytes)
            .ThenBy(c => c.Name, StringComparer.Ordinal)
            .ToList();
    }
}
