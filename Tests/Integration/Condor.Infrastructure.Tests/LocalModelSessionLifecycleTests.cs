using System.Net;
using System.Text;
using System.Threading;
using Condor.Core.Models;
using Condor.Infrastructure.Llm;

namespace Condor.Infrastructure.Tests;

/// <summary>
/// Pruebas del ciclo de vida de la sesion local del proveedor (Prompt 2).
/// Cubren: inicializacion unica, reutilizacion entre solicitudes consecutivas,
/// fallo de proveedor, retry sin duplicar instancia, timeout, cancelacion,
/// cierre normal/anormal y liberacion (keep_alive=0). Todo a nivel de sesion y
/// de cliente HTTP, sin procesos reales (Condor nunca gestiona llama-server).
/// </summary>
public class LocalModelSessionLifecycleTests
{
    [Fact]
    public async Task UnaUnicaInicializacion_RegistraUnaSolaSesionActiva()
    {
        var handler = new CountingHandler();
        using var session = new LocalModelSession(new HttpClient(handler));

        var ok1 = await session.EnsureAvailableAsync("qwen", CancellationToken.None);
        var ok2 = await session.EnsureAvailableAsync("qwen", CancellationToken.None);
        var ok3 = await session.EnsureAvailableAsync("qwen", CancellationToken.None);

        Assert.True(ok1);
        Assert.True(ok2);
        Assert.True(ok3);
        Assert.Equal("qwen", session.ActiveModel);

        // La disponibilidad se comprueba una vez por Ensure llamada, pero la
        // sesion NO se re-inicializa: solo una llamada a /api/version por check.
        Assert.Equal(3, handler.VersionChecks);
        Assert.Equal(0, handler.GenerateCalls); // ninguna inferencia ni liberacion ficticia
    }

    [Fact]
    public async Task DosSolicitudesConsecutivas_CompartenLaMismaSesion()
    {
        var handler = new CountingHandler();
        using var session = new LocalModelSession(new HttpClient(handler));

        await session.EnsureAvailableAsync("qwen", CancellationToken.None);
        var resp1 = await session.Llm.CompleteAsync(
            new LlmRequest { Model = "qwen", Prompt = "hola" }, CancellationToken.None);
        var resp2 = await session.Llm.CompleteAsync(
            new LlmRequest { Model = "qwen", Prompt = "adios" }, CancellationToken.None);

        Assert.True(resp1.Success);
        Assert.True(resp2.Success);

        // Misma instancia de HttpClient (misma sesion): dos inferencias, cero
        // inicializaciones nuevas ni liberaciones prematuras.
        Assert.Equal(2, handler.ChatCalls);
        Assert.Equal(0, handler.GenerateCalls);
    }

    [Fact]
    public async Task ModelosDistintos_CambianLaSesionActivaSinDuplicar()
    {
        var handler = new CountingHandler();
        using var session = new LocalModelSession(new HttpClient(handler));

        await session.EnsureAvailableAsync("qwen", CancellationToken.None);
        Assert.Equal("qwen", session.ActiveModel);

        // Un modelo distinto re-registra la sesion (la ejecucion elige un unico
        // modelo activo), pero no libera el anterior ni duplica conectores.
        var ok = await session.EnsureAvailableAsync("llama3", CancellationToken.None);
        Assert.True(ok);
        Assert.Equal("llama3", session.ActiveModel);
        Assert.Equal(0, handler.GenerateCalls);
        Assert.Equal(0, handler.ChatCalls);
    }

    [Fact]
    public async Task FalloDelProveedor_NoCreaCascadaDeInstancias()
    {
        var handler = new FailingHandler();
        using var session = new LocalModelSession(new HttpClient(handler));

        var ok = await session.EnsureAvailableAsync("qwen", CancellationToken.None);

        // El proveedor no esta disponible: se informa de forma honesta, sin
        // insistir en crear/inicializar nada nuevo en bucle.
        Assert.False(ok);
        Assert.Equal("qwen", session.ActiveModel);
        Assert.Equal(1, handler.VersionChecks);
    }

    [Fact]
    public async Task Retry_SinDuplicarInstancia_ReusaLaMismaSesion()
    {
        var handler = new CountingHandler();
        using var session = new LocalModelSession(new HttpClient(handler));

        var retries = 0;
        var completed = false;
        while (retries < 3 && !completed)
        {
            var available = await session.EnsureAvailableAsync("qwen", CancellationToken.None);
            if (available)
            {
                var resp = await session.Llm.CompleteAsync(
                    new LlmRequest { Model = "qwen", Prompt = "consulta" }, CancellationToken.None);
                completed = resp.Success;
            }
            retries++;
        }

        Assert.True(completed);
        Assert.Equal("qwen", session.ActiveModel);

        // El retry no inicializa un recurso nuevo por intento: una sola sesion.
        Assert.Equal(1, handler.ChatCalls);
    }

    [Fact]
    public async Task Timeout_ClasificaCorrectamente_SinCrearNuevaInstancia()
    {
        var handler = new TimeoutHandler();
        using var session = new LocalModelSession(new HttpClient(handler));

        var response = await session.Llm.CompleteAsync(
            new LlmRequest { Model = "qwen", Prompt = "lento" }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(LlmOutcome.Timeout, response.Outcome);
        Assert.Equal(0, handler.GenerateCalls);
    }

    [Fact]
    public async Task Cancelacion_CooperaConElToken_SinDejarRecursos()
    {
        var handler = new CountingHandler();
        using var cts = new CancellationTokenSource();
        using var session = new LocalModelSession(new HttpClient(handler));

        cts.Cancel();
        // Con un token ya cancelado, la sesion no debe quedar marcada como
        // lista: Ensure devuelve falso y no se dispara ninguna liberacion.
        var ok = await session.EnsureAvailableAsync("qwen", cts.Token);

        Assert.False(ok);
        Assert.Equal(0, handler.ChatCalls);
        Assert.Equal(0, handler.GenerateCalls);
    }

    [Fact]
    public async Task CierreNormal_InvocaLiberacion_PorApiDeOllama()
    {
        var handler = new CountingHandler();
        using var session = new LocalModelSession(new HttpClient(handler));

        await session.EnsureAvailableAsync("qwen", CancellationToken.None);
        await session.EnsureAvailableAsync("llama3", CancellationToken.None);

        await session.ReleaseAsync(CancellationToken.None);

        // Libera el modelo activo (llama3) una sola vez via keep_alive=0.
        Assert.Equal(1, handler.GenerateCalls);
        Assert.Equal("llama3", handler.LastGenerateModel);
    }

    [Fact]
    public async Task CierreAnormal_EnFinally_IgualLiberaLaSesion()
    {
        var handler = new CountingHandler();
        var session = new LocalModelSession(new HttpClient(handler));
        await session.EnsureAvailableAsync("qwen", CancellationToken.None);

        var asserted = false;
        try
        {
            try
            {
                throw new System.IO.InvalidDataException("fallo simulado");
            }
            finally
            {
                // Equivale al finally de Program.Main: el shutdown libera aun en error.
                await session.ReleaseAsync(CancellationToken.None);
                Assert.Equal(1, handler.GenerateCalls);
                asserted = true;
            }
        }
        catch (System.IO.InvalidDataException)
        {
            // El fallo original se propaga; la liberacion ya se comprobo en finally.
        }
        finally
        {
            session.Dispose();
        }

        Assert.True(asserted);
    }

    [Fact]
    public async Task Liberacion_EsIdempotente_UnaSolaVez()
    {
        var handler = new CountingHandler();
        using var session = new LocalModelSession(new HttpClient(handler));

        await session.EnsureAvailableAsync("qwen", CancellationToken.None);
        await session.ReleaseAsync(CancellationToken.None);
        await session.ReleaseAsync(CancellationToken.None);
        await session.ReleaseAsync(CancellationToken.None);

        Assert.Equal(1, handler.GenerateCalls);
    }

    [Fact]
    public async Task ProveedorYaExistente_SeReutiliza_SinReinicializar()
    {
        var handler = new CountingHandler();
        using var session = new LocalModelSession(new HttpClient(handler));

        await session.EnsureAvailableAsync("qwen", CancellationToken.None);
        var resp1 = await session.Llm.CompleteAsync(new LlmRequest { Model = "qwen", Prompt = "a" }, CancellationToken.None);
        var resp2 = await session.Llm.CompleteAsync(new LlmRequest { Model = "qwen", Prompt = "b" }, CancellationToken.None);

        Assert.True(resp1.Success);
        Assert.True(resp2.Success);
        Assert.Equal(2, handler.ChatCalls);
        Assert.Equal(0, handler.GenerateCalls); // no se libera entre solicitudes
    }

    [Fact]
    public async Task ProveedorNoCompatible_NoFuerzaInicializacionNueva()
    {
        var handler = new CountingHandler();
        using var session = new LocalModelSession(new HttpClient(handler));

        // Sesion activa para qwen; una solicitud para un modelo distinto no
        // debe disparar liberaciones prematuras ni crear conectores extra.
        await session.EnsureAvailableAsync("qwen", CancellationToken.None);
        var resp = await session.Llm.CompleteAsync(
            new LlmRequest { Model = "otro-modelo", Prompt = "x" }, CancellationToken.None);

        // La sesion activa se conserva hasta que se libere con ReleaseAsync.
        Assert.True(resp.Success);
        Assert.Equal(0, handler.GenerateCalls);
        Assert.Equal("qwen", session.ActiveModel);
    }

    [Fact]
    public async Task SinModelo_ParaLiberar_NoHaceLlamadas()
    {
        var handler = new CountingHandler();
        using var session = new LocalModelSession(new HttpClient(handler));

        await session.ReleaseAsync(CancellationToken.None);

        Assert.Equal(0, handler.GenerateCalls);
    }

    [Fact]
    public async Task CancelacionDuranteInferencia_AbortaDeFormaCooperativa_SinLiberarLaSesion()
    {
        // Cubre el comportamiento de Ctrl+C a mitad de una inferencia: el token
        // cancela la operacion pendiente (la sesion NO se libera por el propio
        // cierre de consola; la liberacion queda para ReleaseAsync del shutdown).
        var handler = new CancelableHandler();
        using var cts = new CancellationTokenSource();
        using var session = new LocalModelSession(new HttpClient(handler));

        await session.EnsureAvailableAsync("qwen", CancellationToken.None);

        var inference = session.Llm.CompleteAsync(
            new LlmRequest { Model = "qwen", Prompt = "lenta" }, cts.Token);
        cts.CancelAfter(100);

        var response = await inference;

        Assert.False(response.Success);
        Assert.Equal(LlmOutcome.Timeout, response.Outcome);
        Assert.Equal(0, handler.GenerateCalls); // la cancelacion no libera el modelo
        Assert.Equal("qwen", session.ActiveModel);
    }
}

/// <summary>Handler que simula una inferencia que se cancela de forma cooperativa.</summary>
internal sealed class CancelableHandler : HttpMessageHandler
{
    public int GenerateCalls { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri?.AbsolutePath == "/api/generate")
        {
            GenerateCalls++;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        if (request.RequestUri?.AbsolutePath == "/api/chat")
        {
            // Inferencia lenta que respeta la cancelacion del token.
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        return new HttpResponseMessage(HttpStatusCode.OK);
    }
}

/// <summary>Handler que simula un servidor Ollama disponible y con respuestas validas.</summary>
internal sealed class CountingHandler : HttpMessageHandler
{
    public int VersionChecks { get; private set; }
    public int ChatCalls { get; private set; }
    public int GenerateCalls { get; private set; }
    public string? LastGenerateModel { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? "";

        if (path == "/api/version")
        {
            VersionChecks++;
            return Ok("{\"version\":\"0.1.0\"}");
        }

        if (path == "/api/chat")
        {
            ChatCalls++;
            return Ok("{\"model\":\"qwen\",\"message\":{\"content\":\"respuesta\"}}");
        }

        if (path == "/api/generate")
        {
            GenerateCalls++;
            var body = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            LastGenerateModel = ExtractModel(body);
            return Ok("{\"model\":\"" + (LastGenerateModel ?? "") + "\",\"done\":true}");
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private static Task<HttpResponseMessage> Ok(string json) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

    private static string? ExtractModel(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("model", out var m) && m.ValueKind == System.Text.Json.JsonValueKind.String)
                return m.GetString();
        }
        catch { /* ignorar */ }
        return null;
    }
}

/// <summary>Handler que simula servidor no disponible (conexion rechazada).</summary>
internal sealed class FailingHandler : HttpMessageHandler
{
    public int VersionChecks { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri?.AbsolutePath == "/api/version")
        {
            VersionChecks++;
        }
        throw new HttpRequestException("conexion rechazada");
    }
}

/// <summary>Handler que simula un proveedor que no responde a tiempo (timeout real).</summary>
internal sealed class TimeoutHandler : HttpMessageHandler
{
    public int GenerateCalls { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri?.AbsolutePath == "/api/generate")
        {
            GenerateCalls++;
        }
        throw new TaskCanceledException("el tiempo de espera de la operacion agoto");
    }
}
