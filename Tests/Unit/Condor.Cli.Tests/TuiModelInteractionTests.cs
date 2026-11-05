using Condor.Cli.Tui;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Infrastructure.State;

namespace Condor.Cli.Tests;

public class TuiModelInteractionTests
{
    [Fact]
    public async Task CambioDeModelo_ReconciliaCapacidadesDelModeloActivo()
    {
        var model = new ModelInfo
        {
            Name = "qwen2.5-coder:3b",
            Family = "qwen2",
            Capabilities = ["completion", "tools"]
        };
        var assessment = new FixedAssessmentService(new AssessmentResult
        {
            Tools = new ToolsProfile
            {
                Ollama = new OllamaStatus { Models = [model] }
            }
        });
        ModelInfo? reconciledModel = null;

        await CondorTui.ReconcileAgentModeAfterModelSwitchAsync(
            assessment,
            model.Name,
            CancellationToken.None,
            activeModel => reconciledModel = activeModel);

        Assert.Same(model, reconciledModel);
        Assert.Contains(CondorTui.BuildAgentOptions(reconciledModel), option =>
            option.Id == "codificar" && !option.Disabled);
    }

    [Fact]
    public void HistorialDeTarea_MuestraElModeloQueGeneroLaRespuesta()
    {
        using var host = new TuiHost(110, 34);
        host.Enter();
        host.ShowSession("granite4.2:3b");

        CondorTui.AddAgentResultToHistory(host, new AgentResult
        {
            Success = true,
            Model = "granite4.2:3b",
            Reason = "Respuesta generada por el modelo."
        });

        var frame = Ansi.StripSgr(host.SnapshotFullFrame());

        Assert.Contains("Modelo usado: granite4.2:3b", frame);
        Assert.Contains("Respuesta generada por el modelo.", frame);
    }

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
        Assert.DoesNotContain("Consulta completada", frame);
        Assert.Contains("RAM Condor", frame);
        Assert.Matches(@"[0-9]+\.[0-9]s", frame);
    }

    [Fact]
    public void FormatoTiempo_MuestraSegundosConDecimal()
    {
        Assert.Equal("27.0s", CondorTui.FormatoTiempo(TimeSpan.FromSeconds(27)));
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
    public async Task ConsultInTuiAsync_ModeloSinContenidoMuestraElErrorDentroDeCondor()
    {
        using var host = new TuiHost(110, 34);
        host.Enter();
        host.ShowSession("granite4.2:3b");
        var llm = new RecordingLlmClient(new LlmResponse
        {
            Success = true,
            Model = "granite4.2:3b",
            Content = "  "
        });

        var exitCode = await CondorTui.ConsultInTuiAsync(
            host,
            llm,
            "granite4.2:3b",
            new[] { "¿puedes ayudarme?" },
            CancellationToken.None);

        var frame = host.SnapshotFullFrame();

        Assert.Equal(1, exitCode);
        Assert.Contains("El modelo no devolvió una respuesta", frame);
        Assert.DoesNotContain("Consulta completada", frame);
    }

    [Fact]
    public async Task PlanInTuiAsync_MuestraYGuardaElResultadoDentroDeCondor()
    {
        var stateRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "condor-tui-" + Guid.NewGuid().ToString("N")));
        var store = new LocalStateStore(stateRoot);
        var plan = new WorkPlan
        {
            Status = DetectionStatus.Detected,
            Intention = "crear",
            Objective = "Crear una aplicación mensual",
            Tasks = new List<PlanTask>
            {
                new() { Id = "T0", Priority = "alta", Title = "Preparar el proyecto", Detail = "Crear la estructura inicial." }
            }
        };
        var service = new FixedPlanService(plan);

        using var host = new TuiHost(110, 34);
        host.Enter();
        host.ShowSession("granite4.2:3b");

        try
        {
            var exitCode = await CondorTui.PlanInTuiAsync(
                host,
                service,
                store,
                new[] { "crear", "una", "aplicación" },
                CancellationToken.None);

            host.Repaint();
            var frame = host.SnapshotFullFrame();
            var savedPlan = await store.LoadPlanAsync();

            Assert.Equal(0, exitCode);
            Assert.Equal("crear una aplicación", service.LastRequest);
            Assert.NotNull(savedPlan);
            Assert.Contains("Condor generó el plan de trabajo.", frame);
            Assert.Contains("Crear una aplicación mensual", frame);
            Assert.Contains("Preparar el proyecto", frame);
        }
        finally
        {
            var tempRoot = Path.GetFullPath(Path.GetTempPath());
            if (stateRoot.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(stateRoot))
            {
                Directory.Delete(stateRoot, recursive: true);
            }
        }
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

    private sealed class FixedAssessmentService(AssessmentResult assessment) : IAssessmentService
    {
        public Task<AssessmentResult> ExecuteAsync(
            AssessmentRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(assessment);
    }

    private sealed class FixedPlanService(WorkPlan plan) : IPlanService
    {
        public string? LastRequest { get; private set; }

        public Task<WorkPlan> BuildPlanAsync(string userRequest, CancellationToken cancellationToken = default)
        {
            LastRequest = userRequest;
            return Task.FromResult(plan);
        }
    }
}
