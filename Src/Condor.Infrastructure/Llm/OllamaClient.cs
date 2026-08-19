using System.Collections.Generic;
using System.Net.Http.Json;
using Condor.Core.Contracts;
using Condor.Core.Models;

namespace Condor.Infrastructure.Llm;

public class OllamaClient : ILlmClient
{
    private const string ApiBase = "http://127.0.0.1:11434";
    private const int DefaultTimeoutMilliseconds = 180000;

    private readonly HttpClient _httpClient;

    public OllamaClient()
        : this(new HttpClient { Timeout = TimeSpan.FromMilliseconds(DefaultTimeoutMilliseconds) })
    {
    }

    public OllamaClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<LlmResponse> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return new LlmResponse { Success = false, Error = "No se especifico un modelo" };
        }

        if (request.Messages is not { Count: > 0 } && string.IsNullOrWhiteSpace(request.Prompt))
        {
            return new LlmResponse { Success = false, Error = "No se especifico un mensaje" };
        }

        try
        {
            var content = BuildContent(request);
            var payload = new
            {
                model = request.Model,
                stream = false,
                messages = BuildMessages(request, content),
                options = request.MaxTokens is null
                    ? (object)new { temperature = request.Temperature }
                    : new { temperature = request.Temperature, num_predict = request.MaxTokens }
            };

            using var response = await _httpClient.PostAsJsonAsync(
                ApiBase + "/api/chat",
                payload,
                cancellationToken);

            if ((int)response.StatusCode == 404)
            {
                return new LlmResponse
                {
                    Success = false,
                    Error = "El modelo '" + request.Model + "' no existe en el servidor de Ollama. Usa 'condor analizar' para ver los modelos disponibles."
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                return new LlmResponse
                {
                    Success = false,
                    Error = "El servidor de Ollama respondio con error HTTP " + (int)response.StatusCode
                };
            }

            var body = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken);
            if (body?.Message?.Content is null)
            {
                return new LlmResponse { Success = false, Error = "El servidor de Ollama no devolvio contenido" };
            }

            return new LlmResponse
            {
                Success = true,
                Content = body.Message.Content,
                Model = body.Model
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new LlmResponse { Success = false, Error = "La inferencia fue cancelada" };
        }
        catch (TaskCanceledException)
        {
            return new LlmResponse { Success = false, Error = "La inferencia supero el tiempo maximo de espera" };
        }
        catch (HttpRequestException)
        {
            return new LlmResponse
            {
                Success = false,
                Error = "No fue posible comunicarse con el servidor de Ollama en 127.0.0.1:11434. Verifica que Ollama este instalado y en ejecucion."
            };
        }
        catch
        {
            return new LlmResponse { Success = false, Error = "Error inesperado durante la inferencia" };
        }
    }

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
