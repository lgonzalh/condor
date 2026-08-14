using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json;
using Condor.Core.Models;
using Condor.Infrastructure.Llm;

namespace Condor.Infrastructure.Tests;

public class LlmMultimodalCompatibilityTests
{
    [Fact]
    public async Task SinImagenes_EnviaContenidoTextual()
    {
        string? body = null;
        var handler = new CapturingHandler(r =>
        {
            body = r.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"model\":\"m\",\"message\":{\"content\":\"ok\"}}", Encoding.UTF8, "application/json")
            };
        });
        var client = new OllamaClient(new HttpClient(handler));

        await client.CompleteAsync(new LlmRequest { Model = "m", Prompt = "hola" });

        using var doc = JsonDocument.Parse(body!);
        var message = doc.RootElement.GetProperty("messages")[0];
        Assert.Equal(JsonValueKind.String, message.GetProperty("content").ValueKind);
        Assert.Equal("hola", message.GetProperty("content").GetString());
    }

    [Fact]
    public async Task ConImagenes_EnviaContenidoMultimodal()
    {
        string? body = null;
        var handler = new CapturingHandler(r =>
        {
            body = r.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"model\":\"m\",\"message\":{\"content\":\"ok\"}}", Encoding.UTF8, "application/json")
            };
        });
        var client = new OllamaClient(new HttpClient(handler));

        await client.CompleteAsync(new LlmRequest
        {
            Model = "m",
            Prompt = "describe",
            Images = new List<string> { "YmFzZTY0" }
        });

        using var doc = JsonDocument.Parse(body!);
        var message = doc.RootElement.GetProperty("messages")[0];
        Assert.Equal(JsonValueKind.Array, message.GetProperty("content").ValueKind);
        var parts = message.GetProperty("content").EnumerateArray().ToList();
        Assert.Equal(2, parts.Count);
        Assert.Equal("text", parts[0].GetProperty("type").GetString());
        Assert.Equal("image", parts[1].GetProperty("type").GetString());
        Assert.Equal("YmFzZTY0", parts[1].GetProperty("image").GetString());
    }

    [Fact]
    public async Task ConImagenesVacia_ConservaComportamientoTextual()
    {
        string? body = null;
        var handler = new CapturingHandler(r =>
        {
            body = r.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"model\":\"m\",\"message\":{\"content\":\"ok\"}}", Encoding.UTF8, "application/json")
            };
        });
        var client = new OllamaClient(new HttpClient(handler));

        await client.CompleteAsync(new LlmRequest { Model = "m", Prompt = "hola", Images = new List<string>() });

        using var doc = JsonDocument.Parse(body!);
        var message = doc.RootElement.GetProperty("messages")[0];
        Assert.Equal(JsonValueKind.String, message.GetProperty("content").ValueKind);
        Assert.Equal("hola", message.GetProperty("content").GetString());
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }
}
