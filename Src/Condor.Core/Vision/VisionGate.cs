using System;
using System.Collections.Generic;
using System.Linq;
using Condor.Core.Evaluation;
using Condor.Core.Models;

namespace Condor.Core.Vision;

public readonly record struct VisionGateResult(
    bool Available,
    string? SelectedModel,
    string? Reason);

public static class VisionGate
{
    private const string ReasonNoCapability =
        "No se detecto capacidad de vision en el entorno. Ejecuta 'condor analizar' para verificar el hardware y la GPU.";

    private const string ReasonNoVisionModel =
        "No hay un modelo local con capacidad de vision disponible. Usa 'condor recomendar --proposito vision' para identificar modelos compatibles.";

    public static VisionGateResult Evaluate(AssessmentResult? assessment)
    {
        if (assessment is null || assessment.Capabilities is null)
        {
            return new VisionGateResult(false, null, ReasonNoCapability);
        }

        if (!assessment.Capabilities.VisionCapable)
        {
            return new VisionGateResult(false, null, ReasonNoCapability);
        }

        var models = assessment.Tools?.Ollama?.Models ?? new List<ModelInfo>();
        var visionModels = models
            .Where(ModelRoleClassifier.HasVision)
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (visionModels.Count == 0)
        {
            return new VisionGateResult(false, null, ReasonNoVisionModel);
        }

        return new VisionGateResult(true, visionModels[0].Name, null);
    }
}
