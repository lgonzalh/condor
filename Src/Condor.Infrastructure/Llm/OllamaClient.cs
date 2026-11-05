using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Condor.Core.Contracts;
using Condor.Core.Models;

namespace Condor.Infrastructure.Llm;

/// <summary>
/// Cliente HTTP de Ollama (http://127.0.0.1:11434). Condor NO gestiona el
/// proceso del proveedor (llama-server.exe lo lanza Ollama internamente);
/// por eso se distinguen los fallos por su manifestacion (conexion, timeout,
/// respuesta) en lugar de por el proceso, y se expone un health check para
/// comprobar disponibilidad real antes de continuar.
/// </summary>
public class OllamaClient : ILlmClient, ILlmProviderDiagnostics
{
    public const string DefaultApiBase = "http://127.0.0.1:11434";
    public const int DefaultTimeoutMilliseconds = 180000;

    private readonly HttpClient _httpClient;
    private readonly string _apiBase;

    public OllamaClient()
        : this(DefaultApiBase, new HttpClient { Timeout = TimeSpan.FromMilliseconds(DefaultTimeoutMilliseconds) })
    {
    }

    public OllamaClient(string apiBase)
        : this(apiBase, new HttpClient { Timeout = TimeSpan.FromMilliseconds(DefaultTimeoutMilliseconds) })
    {
    }

    public OllamaClient(HttpClient httpClient)
        : this(DefaultApiBase, httpClient)
    {
    }

    public OllamaClient(string apiBase, HttpClient httpClient)
    {
        _apiBase = string.IsNullOrWhiteSpace(apiBase) ? DefaultApiBase : apiBase;
        _httpClient = httpClient;
    }

    public string ProviderName => "Ollama";

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var probe = _httpClient.GetAsync(_apiBase + "/api/version", cancellationToken);
            var response = await probe;
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlySet<string>> GetLoadedModelNamesAsync(CancellationToken cancellationToken = default)
    {
        var loadedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));

        try
        {
            using var response = await _httpClient.GetAsync(
                _apiBase + "/api/ps",
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return loadedModels;

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var document = await System.Text.Json.JsonDocument.ParseAsync(
                stream,
                cancellationToken: timeout.Token).ConfigureAwait(false);
            if (!document.RootElement.TryGetProperty("models", out var models) ||
                models.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                return loadedModels;
            }

            foreach (var model in models.EnumerateArray())
            {
                if (!model.TryGetProperty("name", out var nameElement) ||
                    nameElement.ValueKind != System.Text.Json.JsonValueKind.String ||
                    !model.TryGetProperty("context_length", out var contextLengthElement) ||
                    contextLengthElement.ValueKind != System.Text.Json.JsonValueKind.Number ||
                    !contextLengthElement.TryGetInt32(out var contextLength) || contextLength <= 0)
                {
                    continue;
                }

                var name = nameElement.GetString();
                if (!string.IsNullOrWhiteSpace(name))
                    loadedModels.Add(name);
            }

            return loadedModels;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return loadedModels;
        }
    }

    /// <summary>
    /// Descarga el modelo de la RAM del proveedor mediante el mecanismo oficial
    /// de Ollama (POST /api/generate con keep_alive=0). Condor NO gestiona el
    /// proceso llama-server.exe; este metodo solo pide a Ollama que libere el
    /// modelo, devolviendo la memoria retenida. Es idempotente y tolerante a
    /// errores: un fallo de liberacion no debe impedir el cierre de Condor.
    /// </summary>
    public async Task ReleaseModelAsync(string model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return;
        }

        try
        {
            // Un generate de una sola respuesta con keep_alive=0 hace que Ollama
            // descargue el modelo al terminar. Si el modelo no esta cargado o el
            // servidor no esta disponible, se ignora con elegancia.
            using var response = await _httpClient.PostAsJsonAsync(
                _apiBase + "/api/generate",
                new { model, prompt = string.Empty, keep_alive = 0, stream = false },
                cancellationToken);
        }
        catch
        {
            // La liberacion nunca debe impedir el cierre de Condor.
        }
    }

    public async Task<LlmResponse> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return Failed(LlmOutcome.InvalidResponse, "No se especifico un modelo");
        }

        if (request.Messages is not { Count: > 0 } && string.IsNullOrWhiteSpace(request.Prompt))
        {
            return Failed(LlmOutcome.InvalidResponse, "No se especifico un mensaje");
        }

        try
        {
            var content = BuildContent(request);
            var payload = new Dictionary<string, object?>
            {
                ["model"] = request.Model,
                ["stream"] = false,
                ["messages"] = BuildMessages(request, content),
                ["options"] = request.MaxTokens is null
                    ? (object)new { temperature = request.Temperature }
                    : new { temperature = request.Temperature, num_predict = request.MaxTokens }
            };
            if (request.RequireJson)
            {
                payload["format"] = "json";
            }

            using var response = await _httpClient.PostAsJsonAsync(
                _apiBase + "/api/chat",
                payload,
                cancellationToken);

            if ((int)response.StatusCode == 404)
            {
                return Failed(LlmOutcome.InvalidResponse, "El modelo '" + request.Model + "' no existe en el servidor de Ollama.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return Failed(LlmOutcome.InvalidResponse, "El servidor de Ollama respondio con error HTTP " + (int)response.StatusCode);
            }

            var body = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken);
            var responseContent = body?.Message?.Content;
            if (string.IsNullOrWhiteSpace(responseContent))
            {
                return Failed(LlmOutcome.InvalidResponse, "El servidor de Ollama no devolvio contenido de texto para este modelo.");
            }

            return new LlmResponse
            {
                Success = true,
                Content = responseContent,
                Model = body?.Model,
                Outcome = LlmOutcome.Ok
            };
        }
        catch (OperationCanceledException)
        {
            return Failed(LlmOutcome.Timeout, "La inferencia supero el tiempo maximo de espera o fue cancelada");
        }
        catch (HttpRequestException ex)
        {
            // Una conexion fallida NO es evidencia suficiente de que el proceso del
            // proveedor "termino": puede ser servidor no iniciado, firewall, DNS o
            // un RST. Se clasifica como ServidorNoDisponible con causa honesta.
            // Si la excepcion real es de timeout, se clasifica como Timeout.
            var isTimeout = ex.InnerException is TaskCanceledException or OperationCanceledException;
            return isTimeout
                ? Failed(LlmOutcome.Timeout, "El proveedor local no respondio a tiempo en " + _apiBase)
                : Failed(LlmOutcome.ServerUnavailable, "El proveedor local no esta disponible en " + _apiBase + "; no se puede determinar si el servidor o el proceso del modelo termino.");
        }
        catch (Exception)
        {
            return Failed(LlmOutcome.ServerUnavailable, "Error inesperado durante la inferencia");
        }
    }

    private static LlmResponse Failed(LlmOutcome outcome, string reason)
        => new()
        {
            Success = false,
            Error = reason,
            Outcome = outcome,
            ProcessExitCode = null,
            FailedAtUtc = DateTime.UtcNow
        };

    private static List<object> BuildMessages(LlmRequest request, object fallbackContent)
    {
        if (request.Messages is not { Count: > 0 })
        {
            return new List<object> { new { role = "user", content = fallbackContent } };
        }

        var list = new List<object>();
        foreach (var m in request.Messages)
        {
            list.Add(new { role = m.Role, content = m.Content });
        }

        return list;
    }

    private static object BuildContent(LlmRequest request)
    {
        var hasImages = request.Images is { Count: > 0 };

        if (!hasImages)
        {
            return request.Prompt;
        }

        var parts = new List<object>
        {
            new { type = "text", text = request.Prompt }
        };

        foreach (var image in request.Images!)
        {
            parts.Add(new { type = "image", image });
        }

        return parts;
    }
}

internal class OllamaChatResponse
{
    public string? Model { get; set; }
    public OllamaChatMessageResponse? Message { get; set; }
}

internal class OllamaChatMessageResponse
{
    public string? Content { get; set; }
}
