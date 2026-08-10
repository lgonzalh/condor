using System.Net.Http.Json;
using System.Text.Json;
using Condor.Core.Models;
using Condor.Infrastructure.Probing;

namespace Condor.Infrastructure.Detection;

public class OllamaDetector
{
    private const string ApiBase = "http://127.0.0.1:11434";

    public async Task<OllamaStatus> DetectAsync(CancellationToken cancellationToken = default)
    {
        var status = new OllamaStatus
        {
            Installed = ToolDetector.FindInPath("ollama") is not null
        };

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };

        if (await TryGetServerVersionAsync(http, status, cancellationToken))
        {
            await TryGetModelsAsync(http, status, cancellationToken);
        }
        else if (status.Installed)
        {
            status.Note = "El servidor de Ollama no responde en 127.0.0.1:11434";
            await TryGetModelsViaCliAsync(status, cancellationToken);
        }

        return status;
    }

    private static async Task<bool> TryGetServerVersionAsync(
        HttpClient http,
        OllamaStatus status,
        CancellationToken cancellationToken)
    {
        try
        {
            var version = await http.GetFromJsonAsync<OllamaVersionResponse>(
                ApiBase + "/api/version",
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(version?.Version))
            {
                status.ServerRunning = true;
                status.ServerVersion = version.Version;
                return true;
            }
        }
        catch
        {
            // Servidor no disponible.
        }

        return false;
    }

    private static async Task TryGetModelsAsync(
        HttpClient http,
        OllamaStatus status,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await http.GetFromJsonAsync<OllamaTagsResponse>(
                ApiBase + "/api/tags",
                cancellationToken);

            if (response?.Models is not null)
            {
                status.Models = response.Models
                    .Select(model => new ModelInfo
                    {
                        Name = model.Name ?? "",
                        SizeBytes = model.Size ?? 0,
                        Family = model.Details?.Family,
                        ParameterSize = model.Details?.ParameterSize,
                        Quantization = model.Details?.QuantizationLevel
                    })
                    .ToList();
            }
        }
        catch
        {
            status.Note = "El servidor respondio, pero no fue posible leer los modelos";
        }
    }

    private static async Task TryGetModelsViaCliAsync(OllamaStatus status, CancellationToken cancellationToken)
    {
        var output = await ProcessProbe.RunAsync(
            "ollama.exe",
            "list --format json",
            10000,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(output))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(output);
            var root = document.RootElement;

            var models = new List<ModelInfo>();
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    var name = OsDetector.ReadString(item, "name");
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        models.Add(new ModelInfo { Name = name });
                    }
                }
            }

            if (models.Count > 0)
            {
                status.Models = models;
                status.ServerRunning = true;
                status.Note = "Modelos detectados mediante el ejecutable local de Ollama";
            }
        }
        catch
        {
            // El formato del ejecutable pudo variar entre versiones.
        }
    }
}

internal class OllamaVersionResponse
{
    public string? Version { get; set; }
}

internal class OllamaTagsResponse
{
    public List<OllamaModelResponse>? Models { get; set; }
}

internal class OllamaModelResponse
{
    public string? Name { get; set; }
    public long? Size { get; set; }
    public OllamaModelDetailsResponse? Details { get; set; }
}

internal class OllamaModelDetailsResponse
{
    public string? Family { get; set; }
    public string? ParameterSize { get; set; }
    public string? QuantizationLevel { get; set; }
}
