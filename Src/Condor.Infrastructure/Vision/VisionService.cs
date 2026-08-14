using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Core.Vision;

namespace Condor.Infrastructure.Vision;

public sealed class VisionService : IVisionService
{
    private const string ReasonTimeout = "La operacion de vision supero el tiempo maximo de espera.";

    private readonly IStateStore _stateStore;
    private readonly ILlmClient _llmClient;
    private readonly ImageFileReader _imageReader;
    private readonly VisionLimits _limits;

    public VisionService(
        IStateStore stateStore,
        ILlmClient? llmClient = null,
        VisionLimits? limits = null)
    {
        _stateStore = stateStore;
        _llmClient = llmClient ?? new Condor.Infrastructure.Llm.OllamaClient();
        _imageReader = new ImageFileReader();
        _limits = limits ?? VisionLimits.Default;
    }

    public async Task<VisionResult> ExamineAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        var timeout = TimeSpan.FromMilliseconds(_limits.VisionTimeoutMilliseconds);

        var assessment = await _stateStore
            .LoadAssessmentAsync(cancellationToken)
            .WaitAsync(timeout, cancellationToken);

        var gate = VisionGate.Evaluate(assessment);

        if (!gate.Available)
        {
            return Degraded(gate.Reason ?? "La capacidad de vision no esta disponible.", imagePath);
        }

        var image = _imageReader.Read(imagePath, _limits.MaxImageBytes);

        if (!image.Success)
        {
            return Degraded(image.Reason ?? "La imagen no pudo leerse.", imagePath);
        }

        byte[] imageBytes = image.Bytes!;

        try
        {
            var request = new LlmRequest
            {
                Model = gate.SelectedModel!,
                Prompt = "Describe brevemente el contenido de esta imagen.",
                Images = new List<string> { Convert.ToBase64String(imageBytes) },
                MaxTokens = 512
            };

            var response = await _llmClient
                .CompleteAsync(request, cancellationToken)
                .WaitAsync(timeout, cancellationToken);

            if (!response.Success)
            {
                return Degraded(response.Error ?? "No fue posible obtener una descripcion de la imagen.", imagePath);
            }

            return new VisionResult
            {
                SchemaVersion = "1.0.0",
                Status = DetectionStatus.Detected,
                ImagePath = image.Path,
                ImageBytes = image.SizeBytes,
                ModelUsed = gate.SelectedModel!,
                Description = Truncate(response.Content ?? "", _limits.MaxDescriptionLength),
                GeneratedAtUtc = DateTime.UtcNow
            };
        }
        catch (TimeoutException)
        {
            return new VisionResult
            {
                SchemaVersion = "1.0.0",
                Status = DetectionStatus.Limited,
                Reason = ReasonTimeout,
                ImagePath = image.Path,
                LimitsApplied = new List<string> { VisionLimits.LimitTimeout },
                GeneratedAtUtc = DateTime.UtcNow
            };
        }
    }

    private static VisionResult Degraded(string reason, string imagePath)
    {
        return new VisionResult
        {
            SchemaVersion = "1.0.0",
            Status = DetectionStatus.Limited,
            Reason = reason,
            ImagePath = imagePath,
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value.Substring(0, maxLength).TrimEnd();
    }
}
