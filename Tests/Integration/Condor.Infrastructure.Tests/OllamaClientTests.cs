using System.Net;
using System.Text;
using Condor.Core.Models;
using Condor.Infrastructure.Llm;

namespace Condor.Infrastructure.Tests;

public class OllamaClientTests
{
    [Fact]
    public async Task CompleteAsync_SinModelo_DevuelveErrorClaro()
    {
        var client = new OllamaClient();

        var response = await client.CompleteAsync(new LlmRequest { Model = "", Prompt = "hola" });

        Assert.False(response.Success);
        Assert.Contains("No se especifico un modelo", response.Error);
    }

    [Fact]
    public async Task CompleteAsync_SinMensaje_DevuelveErrorClaro()
    {
        var client = new OllamaClient();

        var response = await client.CompleteAsync(new LlmRequest { Model = "modelo", Prompt = "  " });

        Assert.False(response.Success);
        Assert.Contains("No se especifico un mensaje", response.Error);
    }

    [Fact]
    public async Task CompleteAsync_ModeloInexistente_DevuelveErrorDeModelo()
    {
        var client = new OllamaClient();

        var response = await client.CompleteAsync(new LlmRequest { Model = "modelo-inexistente-condor", Prompt = "hola" });

        Assert.False(response.Success);
        Assert.Contains("no existe", response.Error);
    }
}

public class OllamaClientTestsConSimulacion
{
    [Fact]
    public async Task CompleteAsync_CuandoNoHayComunicacion_DevuelveErrorDeConexion()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("conexion rechazada"));
        var client = new OllamaClient(new HttpClient(handler));

        var response = await client.CompleteAsync(new LlmRequest { Model = "modelo", Prompt = "hola" });

        Assert.False(response.Success);
        Assert.Contains("No fue posible comunicarse", response.Error);
    }

    [Fact]
    public async Task CompleteAsync_CuandoElServidorResponde500_DevuelveErrorConEstado()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = new OllamaClient(new HttpClient(handler));

        var response = await client.CompleteAsync(new LlmRequest { Model = "modelo", Prompt = "hola" });

        Assert.False(response.Success);
        Assert.Contains("500", response.Error);
    }

    [Fact]
    public async Task CompleteAsync_CuandoElServidorResponde404_DevuelveErrorDeModelo()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = new OllamaClient(new HttpClient(handler));

        var response = await client.CompleteAsync(new LlmRequest { Model = "modelo", Prompt = "hola" });

        Assert.False(response.Success);
        Assert.Contains("no existe", response.Error);
    }

    [Fact]
    public async Task CompleteAsync_ConContenidoValido_ParseaLaRespuesta()
    {
        var json = "{\"model\":\"modelo\",\"message\":{\"role\":\"assistant\",\"content\":\"respuesta local\"}}";
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var client = new OllamaClient(new HttpClient(handler));

        var response = await client.CompleteAsync(new LlmRequest { Model = "modelo", Prompt = "hola" });

        Assert.True(response.Success);
        Assert.Equal("respuesta local", response.Content);
        Assert.Equal("modelo", response.Model);
    }

    [Fact]
    public async Task CompleteAsync_EnviaPeticionAlEndpointLocal()
    {
        Uri? captured = null;
        var handler = new FakeHttpMessageHandler(request =>
        {
            captured = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"model\":\"modelo\",\"message\":{\"content\":\"ok\"}}", Encoding.UTF8, "application/json")
            };
        });
        var client = new OllamaClient(new HttpClient(handler));

        await client.CompleteAsync(new LlmRequest { Model = "modelo", Prompt = "hola" });

        Assert.NotNull(captured);
        Assert.StartsWith("http://127.0.0.1:11434", captured.ToString());
    }
}

internal class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
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
