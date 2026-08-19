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
        Assert.Contains("no esta disponible", response.Error, System.StringComparison.OrdinalIgnoreCase);
        Assert.Equal(LlmOutcome.ServerUnavailable, response.Outcome);
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

    [Fact]
    public async Task CompleteAsync_Timeout_ClasificaComoTiempoReal()
    {
        // El proveedor tarda mas del limite: TaskCanceledException por timeout real.
        var handler = new FakeHttpMessageHandler(_ =>
        {
            throw new TaskCanceledException("el tiempo de espera de la operacion agoto");
        });
        var client = new OllamaClient(new HttpClient(handler));

        var response = await client.CompleteAsync(new LlmRequest { Model = "modelo", Prompt = "hola" });

        Assert.False(response.Success);
        Assert.Contains("tiempo", response.Error, System.StringComparison.OrdinalIgnoreCase);
        Assert.Equal(LlmOutcome.Timeout, response.Outcome);
    }

    [Fact]
    public async Task IsAvailableAsync_CuandoElServidorResponde_EsCierto()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = new OllamaClient(new HttpClient(handler));

        var available = await client.IsAvailableAsync();

        Assert.True(available);
    }

    [Fact]
    public async Task IsAvailableAsync_CuandoNoResponde_EsFalso()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("sin servidor"));
        var client = new OllamaClient(new HttpClient(handler));

        var available = await client.IsAvailableAsync();

        Assert.False(available);
    }

    [Fact]
    public async Task CompleteAsync_ConexionFallida_NoAsumeProcesoTerminado()
    {
        // Una conexion fallida no es evidencia de "proceso terminado"; se
        // clasifica como ServidorNoDisponible con causa honesta.
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("connection refused"));
        var client = new OllamaClient(new HttpClient(handler));

        var response = await client.CompleteAsync(new LlmRequest { Model = "modelo", Prompt = "hola" });

        Assert.False(response.Success);
        Assert.Equal(LlmOutcome.ServerUnavailable, response.Outcome);
        Assert.Contains("no esta disponible", response.Error, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompleteAsync_HttpRequestConTimeoutInterno_ClasificaTimeOut()
    {
        var inner = new TaskCanceledException("timeout");
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("se agoto el tiempo", inner));
        var client = new OllamaClient(new HttpClient(handler));

        var response = await client.CompleteAsync(new LlmRequest { Model = "modelo", Prompt = "hola" });

        Assert.False(response.Success);
        Assert.Equal(LlmOutcome.Timeout, response.Outcome);
    }

    [Fact]
    public async Task PullAsync_ModoStreaming_ReportaPorcentajeRealDeOllama()
    {
        // NDJSON de progreso como el que emite Ollama en stream=true.
        const string ndjson =
            "{\"status\":\"pulling manifest\"}\n" +
            "{\"status\":\"downloading\",\"digest\":\"a\",\"total\":10,\"completed\":3}\n" +
            "{\"status\":\"downloading\",\"digest\":\"a\",\"total\":10,\"completed\":5}\n" +
            "{\"status\":\"downloading\",\"digest\":\"a\",\"total\":10,\"completed\":7}\n" +
            "{\"status\":\"downloading\",\"digest\":\"a\",\"total\":10,\"completed\":10}\n" +
            "{\"status\":\"success\"}\n";

        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(ndjson)))
        });
        var operator_ = new OllamaModelOperator(
            new HttpClient(handler),
            new OllamaDetectorAceptandoTodo());

        var percents = new List<double?>();
        var ok = await operator_.PullAsync(
            "modelo-test",
            300000,
            percent => percents.Add(percent),
            CancellationToken.None);

        Assert.True(ok);
        Assert.NotEmpty(percents);
        Assert.True(percents.Any(p => p is { } v && Math.Abs(v - 30.0) < 0.001), "debe reportar 30%");
        Assert.True(percents.Any(p => p is { } v && Math.Abs(v - 50.0) < 0.001), "debe reportar 50%");
        Assert.True(percents.Any(p => p is { } v && Math.Abs(v - 70.0) < 0.001), "debe reportar 70%");
        Assert.True(percents.Any(p => p is { } v && Math.Abs(v - 100.0) < 0.001), "debe reportar 100%");
        // "pulling manifest"/"success" no llevan porcentaje (no hay descarga real):
        // el operador las reporta como null (honesto: sin progreso numerico).
        Assert.True(percents.Any(p => p is null), "las etapas sin descarga deben reportarse sin porcentaje");
    }
}

internal sealed class OllamaDetectorAceptandoTodo : Condor.Infrastructure.Detection.OllamaDetector
{
    public override Task<Condor.Core.Models.OllamaStatus> DetectAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new Condor.Core.Models.OllamaStatus
        {
            Installed = true,
            ServerRunning = true,
            Models = new List<Condor.Core.Models.ModelInfo> { new() { Name = "modelo-test" } }
        });
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
