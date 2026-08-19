using Condor.Core.Evaluation;
using Condor.Core.Models;

namespace Condor.Core.Evaluation;

public class ModelRecommender
{
    private const string PurposeDevelopment = "development";
    private const string PurposeGeneral = "general";
    private const string PurposeVision = "vision";

    private static readonly Dictionary<string, double> WeightCompat = new()
    {
        [PurposeDevelopment] = 0.35,
        [PurposeGeneral] = 0.35,
        [PurposeVision] = 0.35
    };

    private static readonly Dictionary<string, double> WeightDev = new()
    {
        [PurposeDevelopment] = 0.30,
        [PurposeGeneral] = 0.00,
        [PurposeVision] = 0.00
    };

    private static readonly Dictionary<string, double> WeightMemory = new()
    {
        [PurposeDevelopment] = 0.20,
        [PurposeGeneral] = 0.25,
        [PurposeVision] = 0.25
    };

    private static readonly Dictionary<string, double> WeightFunctional = new()
    {
        [PurposeDevelopment] = 0.10,
        [PurposeGeneral] = 0.25,
        [PurposeVision] = 0.25
    };

    private static readonly Dictionary<string, double> WeightStability = new()
    {
        [PurposeDevelopment] = 0.05,
        [PurposeGeneral] = 0.15,
        [PurposeVision] = 0.15
    };

    public ModelRecommendationResult Recommend(AssessmentResult? assessment, string purpose)
    {
        var result = new ModelRecommendationResult
        {
            Purpose = purpose,
            GeneratedAtUtc = DateTime.UtcNow,
            Inputs = BuildInputs(assessment)
        };

        if (assessment is null)
        {
            result.Limitations.Add("No hay Assessment disponible. Ejecuta 'condor analizar' primero.");
            return result;
        }

        var status = assessment.Tools?.Ollama;
        if (!(status?.ServerRunning ?? false))
        {
            result.Limitations.Add("Ollama no esta disponible (servidor inactivo o no instalado).");
            return result;
        }

        var models = status.Models ?? new List<ModelInfo>();
        result.Inputs.ModelsCount = models.Count;

        if (models.Count == 0)
        {
            result.Limitations.Add("No hay modelos disponibles en el inventario local.");
            return result;
        }

        var memory = assessment.Environment?.Memory;
        if (memory?.Status != DetectionStatus.Detected)
        {
            result.Limitations.Add("La memoria RAM no pudo detectarse; la viabilidad es incierta.");
        }

        var visionOnly = string.Equals(purpose, PurposeVision, StringComparison.OrdinalIgnoreCase);
        if (visionOnly && !models.Any(m => ModelRoleClassifier.HasVision(m)))
        {
            result.Limitations.Add("Ningun modelo disponible tiene capacidad de vision.");
            return result;
        }

        var candidates = EvaluateCandidates(models, memory);
        var viable = candidates.Where(c => c.Viable).ToList();

        if (viable.Count == 0)
        {
            result.Limitations.Add("Ningun modelo disponible es viable en este equipo.");
            foreach (var candidate in candidates)
            {
                result.Excluded.Add(ToEntry(candidate, 0));
            }
            return result;
        }

        var minSize = viable.Where(c => c.Model.SizeBytes > 0)
                            .Select(c => (double)c.Model.SizeBytes)
                            .DefaultIfEmpty(0)
                            .Min();

        var scored = new List<ScoredCandidate>();
        foreach (var candidate in viable)
        {
            var memScore = MemoryScore(candidate.Model, minSize);
            var total = WeightCompat[purpose] * 100
                      + WeightDev[purpose] * (candidate.DevScore * 100)
                      + WeightMemory[purpose] * memScore
                      + WeightFunctional[purpose] * candidate.FunctionalScore
                      + WeightStability[purpose] * candidate.StabilityScore;
            scored.Add(new ScoredCandidate(candidate, Math.Round(total, 1), memScore));
        }

        scored = scored.OrderByDescending(s => s.TotalScore)
                       .ThenBy(s => s.Candidate.Model.Name, StringComparer.Ordinal)
                       .ToList();

        foreach (var candidate in candidates.Where(c => !c.Viable))
        {
            result.Excluded.Add(ToEntry(candidate, 0));
        }

        var top = scored[0];
        result.Recommended = BuildEntry(top);
        result.HasRecommendation = true;

        foreach (var entry in scored.Skip(1))
        {
            result.Alternatives.Add(BuildEntry(entry));
        }

        if (viable.Count == 1)
        {
            result.Limitations.Add("Solo hay un modelo viable disponible; no existen alternativas.");
        }

        foreach (var limitation in viable.SelectMany(c => c.Limitations).Distinct())
        {
            result.Limitations.Add(limitation);
        }

        return result;
    }

    private static ModelRecommendationInputSnapshot BuildInputs(AssessmentResult? assessment)
    {
        var memory = assessment?.Environment?.Memory;
        var storage = assessment?.Environment?.StorageList?.FirstOrDefault();
        return new ModelRecommendationInputSnapshot
        {
            RamTotalGb = memory is null ? 0 : memory.TotalBytes / ModelMemoryBudget.BytesPerGb,
            RamFreeGb = memory is null ? 0 : memory.FreeBytes / ModelMemoryBudget.BytesPerGb,
            StorageFreeGb = storage is null ? 0 : storage.FreeBytes / ModelMemoryBudget.BytesPerGb,
            GpuDetected = assessment?.Capabilities?.GpuDetected ?? false,
            OllamaReady = assessment?.Capabilities?.OllamaReady ?? false,
            ModelsCount = assessment?.Capabilities?.ModelsCount ?? 0
        };
    }

    private static List<CandidateEvaluation> EvaluateCandidates(List<ModelInfo> models, MemoryInfo? memory)
    {
        var candidates = new List<CandidateEvaluation>();
        foreach (var model in models.OrderBy(m => m.Name, StringComparer.Ordinal))
        {
            candidates.Add(EvaluateCompatibility(model, memory));
        }
        return candidates;
    }

    private static CandidateEvaluation EvaluateCompatibility(ModelInfo model, MemoryInfo? memory)
    {
        var limitations = new List<string>();
        var reasons = new List<string>();

        var devScore = ModelRoleClassifier.DevelopmentScore(model);
        var functionalScore = FunctionalScore(model);
        var stabilityScore = StabilityScore(model, limitations);

        if (model.SizeBytes > 0)
        {
            if (memory is null)
            {
                reasons.Add("RAM desconocida; no se puede verificar la viabilidad.");
            }
            else
            {
                var totalGb = memory.TotalBytes / ModelMemoryBudget.BytesPerGb;
                var freeGb = memory.FreeBytes / ModelMemoryBudget.BytesPerGb;
                var weightGb = model.SizeBytes / (double)ModelMemoryBudget.BytesPerGb;
                var fits = ModelMemoryBudget.FitsInRam(weightGb, 0, totalGb, freeGb);

                if (fits)
                {
                    reasons.Add("Consumo estimado de RAM compatible con el equipo.");
                }
                else
                {
                    reasons.Add("Consumo estimado de RAM supera el presupuesto del equipo.");
                    return new CandidateEvaluation(model, viable: false, devScore, functionalScore, stabilityScore, reasons, limitations);
                }
            }
        }

        reasons.Add(DevReason(devScore, model));
        reasons.Add(MemoryReason(model));

        if (!string.IsNullOrWhiteSpace(model.Family))
        {
            reasons.Add($"Familia '{model.Family}' con buenas capacidades generales.");
        }
        else
        {
            reasons.Add("Familia desconocida; la capacidad se estimo con incertidumbre.");
        }

        return new CandidateEvaluation(model, viable: true, devScore, functionalScore, stabilityScore, reasons, limitations);
    }

    private static double FunctionalScore(ModelInfo model)
    {
        var family = (model.Family ?? "").ToLowerInvariant();
        var score = family switch
        {
            "qwen3" => 90,
            "qwen2" => 80,
            "llama" => 60,
            "" => 40,
            _ => 50
        };

        if (ModelRoleClassifier.HasCapability(model, "tools")) score += 5;
        if (ModelRoleClassifier.HasCapability(model, "insert")) score += 5;
        if (ModelRoleClassifier.HasCapability(model, "vision")) score += 5;

        return Math.Min(score, 100);
    }

    private static double StabilityScore(ModelInfo model, List<string> limitations)
    {
        var missing = new List<string>();
        if (model.SizeBytes <= 0) missing.Add("tamano");
        if (string.IsNullOrWhiteSpace(model.ParameterSize)) missing.Add("parametros");
        if (string.IsNullOrWhiteSpace(model.Quantization)) missing.Add("cuantizacion");
        if ((model.Capabilities ?? new List<string>()).Count == 0) missing.Add("capacidades");

        if (missing.Count == 0) return 100;

        limitations.Add($"Datos incompletos del modelo '{model.Name}': {string.Join(", ", missing)}.");
        return 60;
    }

    private static double MemoryScore(ModelInfo model, double minSize)
    {
        if (model.SizeBytes <= 0 || minSize <= 0) return 0;
        return 100 * Math.Min(1, minSize / model.SizeBytes);
    }

    private static string DevReason(double devScore, ModelInfo model)
    {
        var name = (model.Name ?? "").ToLowerInvariant();
        if (devScore >= 0.9) return "Modelo orientado a codigo y uso de herramientas.";
        if (devScore >= 0.6) return "Modelo orientado a codigo.";
        if (devScore >= 0.3) return "Modelo con capacidades de herramientas.";
        if (name.Contains("r1") || (model.Family ?? "").ToLowerInvariant().Contains("deepseek")) return "Modelo con razonamiento destacado.";
        return "Capacidad general para desarrollo.";
    }

    private static string MemoryReason(ModelInfo model)
    {
        var gb = model.SizeBytes / ModelMemoryBudget.BytesPerGb;
        return $"Consumo estimado {gb:0.0} GB; {model.SizeBytes / (1024 * 1024):0} MB en disco.";
    }

    private static ModelRecommendationEntry ToEntry(CandidateEvaluation candidate, double score)
    {
        return new ModelRecommendationEntry
        {
            Model = candidate.Model,
            Score = score,
            Reasons = candidate.Reasons
        };
    }

    private static ModelRecommendationEntry BuildEntry(ScoredCandidate scored)
    {
        var reasons = new List<string>(scored.Candidate.Reasons);
        if (scored.MemoryScore >= 99.5)
        {
            reasons.Add("Menor consumo estimado entre los candidatos viables.");
        }
        return new ModelRecommendationEntry
        {
            Model = scored.Candidate.Model,
            Score = scored.TotalScore,
            Reasons = reasons
        };
    }

    private sealed class CandidateEvaluation
    {
        public ModelInfo Model { get; }
        public bool Viable { get; }
        public double DevScore { get; }
        public double FunctionalScore { get; }
        public double StabilityScore { get; }
        public List<string> Reasons { get; }
        public List<string> Limitations { get; }

        public CandidateEvaluation(
            ModelInfo model,
            bool viable,
            double devScore,
            double functionalScore,
            double stabilityScore,
            List<string> reasons,
            List<string> limitations)
        {
            Model = model;
            Viable = viable;
            DevScore = devScore;
            FunctionalScore = functionalScore;
            StabilityScore = stabilityScore;
            Reasons = reasons;
            Limitations = limitations;
        }
    }

    private sealed class ScoredCandidate
    {
        public CandidateEvaluation Candidate { get; }
        public double TotalScore { get; }
        public double MemoryScore { get; }

        public ScoredCandidate(CandidateEvaluation candidate, double totalScore, double memoryScore)
        {
            Candidate = candidate;
            TotalScore = totalScore;
            MemoryScore = memoryScore;
        }
    }
}
