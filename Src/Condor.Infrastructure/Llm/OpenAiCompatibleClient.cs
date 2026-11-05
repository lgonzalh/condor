using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Condor.Core.Contracts;
using Condor.Core.Models;

namespace Condor.Infrastructure.Llm;

/// <summary>Cliente opcional para servidores que exponen la API OpenAI Chat Completions.</summary>
public sealed class OpenAiCompatibleClient : ILlmClient, ILlmProviderDiagnostics, IDisposable
{
    private const int DefaultTimeoutMilliseconds = 180_000;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly Uri _baseUri;
    private readonly string? _apiKey;

    public OpenAiCompatibleClient(string baseUrl, string? apiKey = null, HttpClient? httpClient = null)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("La URL del proveedor debe usar HTTP o HTTPS.", nameof(baseUrl));

        _baseUri = new Uri(parsed.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMilliseconds(DefaultTimeoutMilliseconds) };
        _ownsHttpClient = httpClient is null;
    }

    public string ProviderName => "OpenAI-compatible";

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, "models");
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Model))
            return Failed(LlmOutcome.InvalidResponse, "Indica el nombre del modelo OpenAI-compatible.");

        var messages = request.Messages is { Count: > 0 }
            ? request.Messages.Select(message => new { role = message.Role, content = message.Content }).ToArray()
            : new[] { new { role = "user", content = request.Prompt ?? "" } };
        var payload = new Dictionary<string, object?>
        {
            ["model"] = request.Model,
            ["messages"] = messages,
            ["temperature"] = request.Temperature
        };
        if (request.MaxTokens.HasValue) payload["max_tokens"] = request.MaxTokens.Value;
        if (request.RequireJson) payload["response_format"] = new { type = "json_object" };

        try
        {
            using var httpRequest = CreateRequest(HttpMethod.Post, "chat/completions");
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var outcome = (int)response.StatusCode >= 500
                    ? LlmOutcome.ServerUnavailable
                    : LlmOutcome.InvalidResponse;
                return Failed(outcome, "El proveedor OpenAI-compatible devolvió HTTP " + (int)response.StatusCode + ".");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var content = ReadTextContent(document.RootElement);
            if (string.IsNullOrWhiteSpace(content))
                return Failed(LlmOutcome.InvalidResponse, "El proveedor OpenAI-compatible no devolvió contenido de texto.");

            return new LlmResponse
            {
                Success = true,
                Model = ReadString(document.RootElement, "model") ?? request.Model,
                Content = content,
                Outcome = LlmOutcome.Ok
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Failed(LlmOutcome.Timeout, "El proveedor OpenAI-compatible excedió el tiempo de espera.");
        }
        catch (HttpRequestException)
        {
            return Failed(LlmOutcome.ServerUnavailable, "No se pudo conectar con el proveedor OpenAI-compatible.");
        }
        catch (JsonException)
        {
            return Failed(LlmOutcome.InvalidResponse, "El proveedor OpenAI-compatible devolvió JSON no válido.");
        }
        catch
        {
            return Failed(LlmOutcome.ServerUnavailable, "Falló la solicitud al proveedor OpenAI-compatible.");
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient) _httpClient.Dispose();
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath)
    {
        var request = new HttpRequestMessage(method, new Uri(_baseUri, relativePath));
        if (_apiKey is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        return request;
    }

    private static string? ReadTextContent(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
            return null;
        var choice = choices[0];
        if (!choice.TryGetProperty("message", out var message) || !message.TryGetProperty("content", out var content))
            return null;
        if (content.ValueKind == JsonValueKind.String) return content.GetString();
        if (content.ValueKind != JsonValueKind.Array) return null;

        var text = new StringBuilder();
        foreach (var part in content.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.String)
                text.Append(part.GetString());
            else if (part.ValueKind == JsonValueKind.Object && part.TryGetProperty("text", out var textPart) &&
                     textPart.ValueKind == JsonValueKind.String)
                text.Append(textPart.GetString());
        }
        return text.ToString();
    }

    private static string? ReadString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static LlmResponse Failed(LlmOutcome outcome, string error)
        => new() { Success = false, Error = error, Outcome = outcome, FailedAtUtc = DateTime.UtcNow };
}
