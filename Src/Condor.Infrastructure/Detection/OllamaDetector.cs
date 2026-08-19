using System.Net.Http.Json;
using System.Text.Json;
using Condor.Core.Models;
using Condor.Infrastructure.Probing;

namespace Condor.Infrastructure.Detection;

public class OllamaDetector
{
    private const string ApiBase = "http://127.0.0.1:11434";

    public virtual async Task<OllamaStatus> DetectAsync(CancellationToken cancellationToken = default)
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
            var json = await http.GetStringAsync(ApiBase + "/api/tags", cancellationToken);
            var models = OllamaTagsParser.Parse(json);
            if (models.Count > 0)
            {
                status.Models = models;
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

        var models = OllamaTagsParser.Parse(output);
        if (models.Count > 0)
        {
            status.Models = models;
            status.ServerRunning = true;
            status.Note = "Modelos detectados mediante el ejecutable local de Ollama";
        }
    }
}

internal static class OllamaTagsParser
{
    public static List<ModelInfo> Parse(string json)
    {
        var models = new List<ModelInfo>();

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var array = root.ValueKind == JsonValueKind.Array
                ? root
                : root.TryGetProperty("models", out var modelsProperty) ? modelsProperty : default;

            if (array.ValueKind != JsonValueKind.Array)
            {
                return models;
            }

            foreach (var item in array.EnumerateArray())
            {
                var name = ReadString(item, "name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var details = ReadObject(item, "details");
                models.Add(new ModelInfo
                {
                    Name = name,
                    SizeBytes = ReadLong(item, "size"),
                    Family = details is null ? null : ReadString(details.Value, "family"),
                    ParameterSize = details is null ? null : ReadString(details.Value, "parameter_size"),
                    Quantization = details is null ? null : ReadString(details.Value, "quantization_level"),
                    ContextLength = details is null ? null : ReadLongNullable(details.Value, "context_length"),
                    Capabilities = ReadStringList(item, "capabilities")
                });
            }
        }
        catch
        {
            // El formato de la respuesta pudo variar entre versiones.
        }

        return models;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String) return null;
        return value.GetString();
    }

    private static long ReadLong(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object) return 0;
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Number) return 0;
        return value.TryGetInt64(out var parsed) ? parsed : 0;
    }

    private static long? ReadLongNullable(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Number) return null;
        return value.TryGetInt64(out var parsed) ? parsed : null;
    }

    private static JsonElement? ReadObject(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Object) return null;
        return value;
    }

    private static List<string> ReadStringList(JsonElement element, string propertyName)
    {
        var list = new List<string>();
        if (element.ValueKind != JsonValueKind.Object) return list;
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array) return list;

        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var text = item.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    list.Add(text);
                }
            }
        }

        return list;
    }
}

internal class OllamaVersionResponse
{
    public string? Version { get; set; }
}
