using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Condor.Core.Models;
using Condor.Infrastructure.Llm;

namespace Condor.Infrastructure.Tests;

public sealed class OpenAiCompatibleClientTests
{
    [Fact]
    public async Task CompleteAsync_SendsCompatibleChatRequestAndParsesTextResponse()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"model\":\"servidor/modelo\",\"choices\":[{\"message\":{\"content\":\"respuesta comparable\"}}]}",
                Encoding.UTF8,
                "application/json")
        });
        using var http = new HttpClient(handler);
        using var client = new OpenAiCompatibleClient("http://127.0.0.1:1234/v1", "clave-prueba", http);

        var response = await client.CompleteAsync(new LlmRequest
        {
            Model = "servidor/modelo",
            Temperature = 0.2,
            MaxTokens = 256,
            Messages = new List<LlmMessage>
            {
                new() { Role = "system", Content = "Instrucción común." },
                new() { Role = "user", Content = "Tarea común." }
            }
        });

        Assert.True(response.Success, response.Error);
        Assert.Equal("servidor/modelo", response.Model);
        Assert.Equal("respuesta comparable", response.Content);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("/v1/chat/completions", handler.RequestUri?.AbsolutePath);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "clave-prueba"), handler.Authorization);

        using var payload = JsonDocument.Parse(handler.Body!);
        Assert.Equal("servidor/modelo", payload.RootElement.GetProperty("model").GetString());
        Assert.Equal(0.2, payload.RootElement.GetProperty("temperature").GetDouble());
        Assert.Equal(256, payload.RootElement.GetProperty("max_tokens").GetInt32());
        Assert.Equal("Tarea común.", payload.RootElement.GetProperty("messages")[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task CompleteAsync_RequireJson_SendsJsonObjectResponseFormat()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"model\":\"servidor/modelo\",\"choices\":[{\"message\":{\"content\":\"{\\\"action\\\":\\\"done\\\"}\"}}]}",
                Encoding.UTF8,
                "application/json")
        });
        using var http = new HttpClient(handler);
        using var client = new OpenAiCompatibleClient("http://127.0.0.1:1234/v1", "clave-prueba", http);

        var response = await client.CompleteAsync(new LlmRequest
        {
            Model = "servidor/modelo",
            Prompt = "Devuelve una acción JSON.",
            RequireJson = true
        });

        Assert.True(response.Success, response.Error);
        using var payload = JsonDocument.Parse(handler.Body!);
        Assert.Equal(
            "json_object",
            payload.RootElement.GetProperty("response_format").GetProperty("type").GetString());
    }

    [Fact]
    public async Task IsAvailableAsync_UsesModelsEndpointWithoutLeakingCredentials()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"data\":[]}", Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler);
        using var client = new OpenAiCompatibleClient("http://127.0.0.1:1234/v1", "clave-no-registrada", http);

        Assert.True(await client.IsAvailableAsync());
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal("/v1/models", handler.RequestUri?.AbsolutePath);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "clave-no-registrada"), handler.Authorization);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public AuthenticationHeaderValue? Authorization { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }
}
