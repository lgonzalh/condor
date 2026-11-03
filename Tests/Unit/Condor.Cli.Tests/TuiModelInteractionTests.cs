using Condor.Cli.Tui;
using Condor.Core.Contracts;
using Condor.Core.Models;

namespace Condor.Cli.Tests;

public class TuiModelInteractionTests
{
    [Fact]
    public async Task ConsultInTuiAsync_MuestraLaRespuestaDentroDeLaPantallaYUsaElModeloActivo()
    {
        using var host = new TuiHost(110, 34);
        host.Enter();
        host.ShowSession("phi4-mini:3.8b");
        var llm = new RecordingLlmClient(new LlmResponse
        {
            Success = true,
            Model = "phi4-mini:3.8b",
            Content = "Respuesta visible en Condor.\nSegunda linea."
        });

        var exitCode = await CondorTui.ConsultInTuiAsync(
            host,
            llm,
            "phi4-mini:3.8b",
            new[] { "hola" },
            CancellationToken.None);

        host.Repaint();
        var frame = host.SnapshotFullFrame();

        Assert.Equal(0, exitCode);
        Assert.NotNull(llm.LastRequest);
        Assert.Equal("phi4-mini:3.8b", llm.LastRequest!.Model);
        Assert.Equal("hola", llm.LastRequest.Prompt);
        Assert.Contains("Respuesta visible en Condor.", frame);
        Assert.Contains("Segunda linea.", frame);
        Assert.Contains("Consulta completada", frame);
    }

    [Fact]
    public async Task ConsultInTuiAsync_AceptaElModeloActivoAgregadoPorModoPreguntar()
    {
        using var host = new TuiHost(110, 34);
        host.Enter();
        host.ShowSession("phi4-mini:3.8b");
        var llm = new RecordingLlmClient(new LlmResponse
        {
            Success = true,
            Model = "phi4-mini:3.8b",
            Content = "OK-TUI-LOCAL"
        });

        var exitCode = await CondorTui.ConsultInTuiAsync(
            host,
            llm,
            "phi4-mini:3.8b",
            new[] { "hola", "--modelo", "phi4-mini:3.8b" },
            CancellationToken.None);

        var frame = host.SnapshotFullFrame();

        Assert.Equal(0, exitCode);
        Assert.Equal("hola", llm.LastRequest?.Prompt);
        Assert.Equal("phi4-mini:3.8b", llm.LastRequest?.Model);
        Assert.Contains("OK-TUI-LOCAL", frame);
    }

    [Fact]
    public async Task ConsultInTuiAsync_RechazaUnModeloDistintoAlSeleccionadoEnLaTui()
    {
        using var host = new TuiHost(110, 34);
        host.Enter();
        host.ShowSession("phi4-mini:3.8b");
        var llm = new RecordingLlmClient(new LlmResponse { Success = true, Content = "no debe usarse" });

        var exitCode = await CondorTui.ConsultInTuiAsync(
            host,
            llm,
            "phi4-mini:3.8b",
            new[] { "hola", "--modelo", "otro-modelo" },
            CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Null(llm.LastRequest);
        Assert.Contains("Selecciona el modelo dentro de la TUI", host.SnapshotFullFrame());
    }

    [Fact]
    public void RenderVisionResultInTui_MuestraLaRespuestaYModeloEnLaPantalla()
    {
        using var host = new TuiHost(110, 34);
        host.Enter();
        host.ShowSession("llava-local");

        var exitCode = CondorTui.RenderVisionResultInTui(
            host,
            new VisionResult
            {
                Status = DetectionStatus.Detected,
                ImagePath = "captura.png",
                ModelUsed = "llava-local",
                Description = "Un ave junto a un árbol.\nLa escena es exterior."
            },
            "imagen.png");

        host.Repaint();
        var frame = host.SnapshotFullFrame();

        Assert.Equal(0, exitCode);
        Assert.Contains("captura.png", frame);
        Assert.Contains("llava-local", frame);
        Assert.Contains("Un ave junto a un árbol.", frame);
        Assert.Contains("La escena es exterior.", frame);
    }

    private sealed class RecordingLlmClient(LlmResponse response) : ILlmClient
    {
        public LlmRequest? LastRequest { get; private set; }

        public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(response);
        }
    }
}
