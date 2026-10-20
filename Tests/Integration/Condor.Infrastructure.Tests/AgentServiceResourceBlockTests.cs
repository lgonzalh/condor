using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Infrastructure.Agent;
using Condor.Infrastructure.Llm;
using Condor.Infrastructure.Setup;
using Condor.Infrastructure.State;

namespace Condor.Infrastructure.Tests;

public class AgentServiceResourceBlockTests
{
    [Fact]
    public async Task RunAsync_RamBaja_FallaRapido_ConPantallaModeloNoEjecutable()
    {
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 2, installedModels: new[] { "qwen2.5-coder:3b" });
        var service = new AgentService(store, assessment);

        var result = await service.RunAsync("analiza que contiene este proyecto", cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("MODELO NO EJECUTABLE", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RAM disponible", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RAM requerida estimada", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Motivo:", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Accion:", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No hay un modelo compatible disponible", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at Condor.", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("liberar memoria", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("analiza que contiene este proyecto", result.Objective);
        Assert.Equal("analiza que contiene este proyecto", result.Checkpoint?.Task);
    }

    [Fact]
    public async Task RunAsync_RamBaja_FalloInmediato_SinReevaluacionesEnBucle()
    {
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 2, installedModels: new[] { "qwen2.5-coder:3b" });
        var service = new AgentService(store, assessment);

        var result = await service.RunAsync("analiza", cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(assessment.ExecuteCount <= 4, "El bloqueo por RAM debe fallar rapido, sin bucle de recuperacion.");
    }

    [Fact]
    public async Task RunAsync_SinModelosNiPresupuesto_ReportaCompatibleNoDisponible()
    {
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 2, installedModels: Array.Empty<string>());
        var service = new AgentService(store, assessment);

        var result = await service.RunAsync("analiza", cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        var reason = result.Reason ?? "";
        Assert.True(
            reason.Contains("MODELO NO EJECUTABLE", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("MODELO COMPATIBLE NO DISPONIBLE", StringComparison.OrdinalIgnoreCase),
            "Debe informar el bloqueo con una pantalla clara.");
        Assert.Equal("analiza", result.Objective);
    }

    [Fact]
    public async Task RunAsync_ActiveModel0_5b_AgentTask_RamInsuficiente_FalloSuave_ActiveModelConservado()
    {
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 2, installedModels: new[] { "qwen2.5-coder:0.5b", "qwen2.5-coder:1.5b", "qwen2.5-coder:3b" });

        var session = new LocalModelSession();
        await session.EnsureAvailableAsync("qwen2.5-coder:0.5b", CancellationToken.None);

        var service = new AgentService(store, assessment, session: session);

        var result = await service.RunAsync("analiza que contiene este proyecto", cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Memoria insuficiente para continuar con esta tarea. Libera memoria y vuelve a intentarlo.", result.Reason);
        Assert.Equal("qwen2.5-coder:0.5b", result.Model);
        Assert.Equal("memoria_insuficiente", result.Checkpoint?.LastDecision);
        Assert.Equal("qwen2.5-coder:0.5b", session.ActiveModel);
    }

    [Fact]
    public async Task EnsureModelForRequirement_ActiveModel3b_AgentTask_ModeloSuficiente_Selecciona3b()
    {
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:0.5b", "qwen2.5-coder:1.5b", "qwen2.5-coder:3b" });

        var session = new LocalModelSession();
        await session.EnsureAvailableAsync("qwen2.5-coder:3b", CancellationToken.None);

        var modelSetup = new ModelAutoSetupService(store, assessment);
        var requirement = new TaskModelRequirement
        {
            IntentKind = TaskIntentKinds.Agent,
            RequiredCodingLevel = 3,
            RequiredMultiFileLevel = 2,
            RequiresToolUse = true,
            RequiresStructuredOutput = true,
            PreferSmallestSufficient = true,
            Label = "agente de ingenieria"
        };

        var selection = await modelSetup.EnsureModelForRequirementAsync(requirement, CancellationToken.None, currentModelName: session.ActiveModel);

        Assert.NotNull(selection.Desired);
        Assert.Equal("qwen2.5-coder:3b", selection.Desired.PullName);
        Assert.False(selection.BlockedByResources);
        Assert.Equal("qwen2.5-coder:3b", session.ActiveModel);
    }


    [Fact]
    public async Task RunAsync_ActiveModel0_5b_RamSuficiente_PromocionAlModeloSuficiente_3b()
    {
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 8, installedModels: new[] { "qwen2.5-coder:0.5b", "qwen2.5-coder:1.5b", "qwen2.5-coder:3b" });

        var session = new LocalModelSession();
        await session.EnsureAvailableAsync("qwen2.5-coder:0.5b", CancellationToken.None);

        var service = new AgentService(store, assessment, session: session);

        var result = await service.RunAsync("analiza que contiene este proyecto", cancellationToken: CancellationToken.None);

        Assert.Contains("qwen2.5-coder:3b", result.Model);
    }

    [Fact]
    public async Task RunAsync_ActiveModelNull_RamInsuficiente_FalloDuro_ModeloNoEjecutable()
    {
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 2, installedModels: new[] { "qwen2.5-coder:3b" });

        var session = new LocalModelSession();
        var service = new AgentService(store, assessment, session: session);

        var result = await service.RunAsync("analiza que contiene este proyecto", cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("MODELO NO EJECUTABLE", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RAM disponible", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RAM requerida estimada", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_FalloSuave_NoModificaActiveModel()
    {
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 2, installedModels: new[] { "qwen2.5-coder:0.5b", "qwen2.5-coder:1.5b", "qwen2.5-coder:3b" });

        var session = new LocalModelSession();
        await session.EnsureAvailableAsync("qwen2.5-coder:0.5b", CancellationToken.None);

        var service = new AgentService(store, assessment, session: session);

        var result = await service.RunAsync("analiza que contiene este proyecto", cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("qwen2.5-coder:0.5b", session.ActiveModel);
    }

    [Fact]
    public async Task RunDirectConsultAsync_ActiveModel0_5b_Suficiente_EjecutaConExito()
    {
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 2, installedModels: new[] { "qwen2.5-coder:0.5b", "qwen2.5-coder:1.5b", "qwen2.5-coder:3b" });

        // La prueba no depende de un Ollama real: el cliente LLM y el probe de
        // disponibilidad se sustituyen por stubs deterministas.
        using var http = new HttpClient(new StubOllamaHandler());
        using var session = new LocalModelSession(http);
        await session.EnsureAvailableAsync("qwen2.5-coder:0.5b", CancellationToken.None);

        var service = new AgentService(
            store,
            assessment,
            llm: new StubLlmClient(),
            provider: new StubLlmDiagnostics(),
            session: session);

        var result = await service.RunAsync("hola, que tal", cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("qwen2.5-coder:0.5b", result.Model);
    }

    private static string TempDir()
    {
        return Path.Combine(Path.GetTempPath(), "condor-agent-resource-" + Guid.NewGuid().ToString("N"));
    }

    private sealed class StubAssessmentService : IAssessmentService
    {
        private readonly double _ramTotalGb;
        private readonly double _ramFreeGb;
        private readonly string[] _installedModels;
        public int ExecuteCount { get; private set; }

        public StubAssessmentService(double ramTotalGb, double ramFreeGb, string[] installedModels)
        {
            _ramTotalGb = ramTotalGb;
            _ramFreeGb = ramFreeGb;
            _installedModels = installedModels;
        }

        public Task<AssessmentResult> ExecuteAsync(AssessmentRequest request, CancellationToken cancellationToken = default)
        {
            ExecuteCount++;
            return Task.FromResult(new AssessmentResult
            {
                Environment = new EnvironmentProfile
                {
                    Memory = new MemoryInfo
                    {
                        Status = DetectionStatus.Detected,
                        TotalBytes = (long)(_ramTotalGb * 1024d * 1024 * 1024),
                        FreeBytes = (long)(_ramFreeGb * 1024d * 1024 * 1024),
                        AvailableBytes = (long)(_ramFreeGb * 1024d * 1024 * 1024)
                    }
                },
                Tools = new ToolsProfile
                {
                    Ollama = new OllamaStatus
                    {
                        Installed = true,
                        ServerRunning = true,
                        Models = new List<ModelInfo>(
                            _installedModels.Select(name => new ModelInfo
                            {
                                Name = name,
                                SizeBytes = GetModelSizeBytes(name),
                                Capabilities = new List<string> { "completion", "tool-use", "structured-output", "coding" }
                            }))
                    }
                },
                Capabilities = new CapabilitiesSummary { ModelsCount = _installedModels.Length, OllamaReady = true }
            });
        }

        private static long GetModelSizeBytes(string name)
        {
            return name switch
            {
                "qwen2.5-coder:0.5b" => 397L * 1024 * 1024,
                "qwen2.5-coder:1.5b" => 986L * 1024 * 1024,
                "qwen2.5-coder:3b" => 1848L * 1024 * 1024,
                _ => 1848L * 1024 * 1024
            };
        }
    }

    private sealed class StubLlmClient : ILlmClient
    {
        public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new LlmResponse
            {
                Success = true,
                Content = "Hola. Estoy listo para ayudarte.",
                Model = request.Model,
                Outcome = LlmOutcome.Ok
            });
    }

    private sealed class StubLlmDiagnostics : ILlmProviderDiagnostics
    {
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public string ProviderName => "Stub";
    }

    private sealed class StubOllamaHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"version\":\"stub\"}", System.Text.Encoding.UTF8, "application/json")
            });
    }
}