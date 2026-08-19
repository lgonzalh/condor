using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Infrastructure.Agent;
using Condor.Infrastructure.State;

namespace Condor.Infrastructure.Tests;

/// <summary>
/// Regresion del escenario de RAM fluctuante confirmado: el modelo qwen2.5-coder:3b
/// esta instalado pero la RAM libre actual no permite cargarlo segun el presupuesto
/// seguro (FitsInRamStrict). Antes, AgentService la trataba igual que "no hay modelo
/// compatible" y descartaba la tarea.
/// Requisitos verificados: mensaje honesto (no "no hay compatible"), recuperacion
/// ACOTADA (sin bucle infinito) y tarea conservada (no se pierde).
/// </summary>
public class AgentServiceResourceBlockTests
{
    [Fact]
    public async Task RunAsync_RamBaja_ModeloInstalado_NoAfirmaQueNoExiste_y_ConservaTarea()
    {
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 2, installedModel: "qwen2.5-coder:3b");
        var service = new AgentService(store, assessment);

        var result = await service.RunAsync("analiza que contiene este proyecto", cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        // El modelo SI existe; el bloqueo es temporal por RAM: NUNCA afirmar ausencia.
        Assert.DoesNotContain("No hay un modelo compatible disponible", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RAM libre", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TEMPORAL", result.Reason, StringComparison.OrdinalIgnoreCase);
        // La tarea no se pierde.
        Assert.Equal("analiza que contiene este proyecto", result.Objective);
        Assert.Equal("analiza que contiene este proyecto", result.Checkpoint?.Task);
    }

    [Fact]
    public async Task RunAsync_RamBaja_RecuperacionAcotada_SinBucleInfinito()
    {
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 2, installedModel: "qwen2.5-coder:3b");
        var service = new AgentService(store, assessment);

        var result = await service.RunAsync("analiza", cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        // El gate re-evalua un numero LIMITADO de veces; nunca infinito.
        // Cada EnsureModelAsync = 1 live + 1 refresh = 2 llamadas al service.
        // Inicial (2) + MaxResourceRecoveryAttempts=3 (3*2=6) + refresh final (2) => tope conservador.
        Assert.True(assessment.ExecuteCount <= 12, "La recuperacion de recursos debe ser acotada.");
    }

    [Fact]
    public async Task RunAsync_RamBaja_UsuarioNiegaConfirmacion_SaleLimpioYConservaTarea()
    {
        // Respuesta NO a la pregunta opcional de RAM: Condor conserva la tarea,
        // termina de forma limpia y NO cierra aplicaciones por su cuenta.
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 2, installedModel: "qwen2.5-coder:3b");
        var confirmation = new StubConfirmation(response: false);
        var service = new AgentService(store, assessment, confirmation: confirmation);

        var result = await service.RunAsync("analiza", cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(1, confirmation.AskCount);        // se pregunto una vez
        Assert.Contains("RAM libre", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("analiza", result.Objective);      // no se pierde la tarea
        Assert.Equal("analiza", result.Checkpoint?.Task);
    }

    [Fact]
    public async Task RunAsync_RamBaja_UsuarioConfirmaPeroRamaSigueBaja_SaleSinBucle()
    {
        // Respuesta SI, pero la RAM sigue insuficiente tras la re-evaluacion:
        // Condor re-evalua UNA vez mas (contabilizable) y luego sale limpio,
        // sin bucles de reintento ilimitados y sin perder la tarea.
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 2, installedModel: "qwen2.5-coder:3b");
        var confirmation = new StubConfirmation(response: true);
        var service = new AgentService(store, assessment, confirmation: confirmation);

        var result = await service.RunAsync("analiza", cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(1, confirmation.AskCount);
        Assert.Contains("RAM libre", result.Reason, StringComparison.OrdinalIgnoreCase);
        // Reintentos acotados: la confirmacion aÃ±ade una re-evaluacion por encima
        // de la recuperacion automatica, pero queda limitada (sin bucle infinito).
        Assert.True(assessment.ExecuteCount <= 14, "La reevaluacion tras confirmacion debe ser acotada.");
        Assert.Equal("analiza", result.Objective);
    }

    [Fact]
    public async Task RunAsync_RamBaja_UsuarioConfirmaYRamaSeLibera_Reevalua()
    {
        // Respuesta SI y la RAM se libera (estado compartido subido al confirmar):
        // Condor re-evalua y continÃºa en lugar de rendirse con el error de RAM.
        // CÃ³ndor NUNCA cierra apps; simula que el usuario si libero memoria.
        var store = new LocalStateStore(Path.Combine(TempDir(), "state-" + Guid.NewGuid().ToString("N")));
        var ram = new RamState { FreeGb = 2.0 };
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 2, installedModel: "qwen2.5-coder:3b", ram: ram);
        var confirmation = new StubConfirmation(response: true, ram: ram, releasedGb: 9.0);
        var service = new AgentService(store, assessment, confirmation: confirmation);

        var result = await service.RunAsync("analiza", cancellationToken: CancellationToken.None);

        Assert.Equal(1, confirmation.AskCount);          // se pregunto
        Assert.Equal(9.0, ram.FreeGb);                    // el usuario libero RAM
        Assert.True(assessment.ExecuteCount > 6, "Debe re-evaluarse tras la confirmacion");
        Assert.DoesNotContain("no se pudo cargar", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    private static string TempDir()
    {
        return Path.Combine(Path.GetTempPath(), "condor-agent-resource-" + Guid.NewGuid().ToString("N"));
    }

    private sealed class StubAssessmentService : IAssessmentService
    {
        private readonly double _ramTotalGb;
        private readonly double _ramFreeGb;
        private readonly string _installedModel;
        public RamState? Ram { get; }
        public int ExecuteCount { get; private set; }

        public StubAssessmentService(double ramTotalGb, double ramFreeGb, string installedModel, RamState? ram = null)
        {
            _ramTotalGb = ramTotalGb;
            _ramFreeGb = ramFreeGb;
            _installedModel = installedModel;
            Ram = ram;
        }

        private double CurrentFreeGb => Ram?.FreeGb ?? _ramFreeGb;

        public Task<AssessmentResult> ExecuteAsync(AssessmentRequest request, CancellationToken cancellationToken = default)
        {
            ExecuteCount++;
            var ramFree = CurrentFreeGb;
            return Task.FromResult(new AssessmentResult
            {
                Environment = new EnvironmentProfile
                {
                    Memory = new MemoryInfo
                    {
                        Status = DetectionStatus.Detected,
                        TotalBytes = (long)(_ramTotalGb * 1024d * 1024 * 1024),
                        FreeBytes = (long)(ramFree * 1024d * 1024 * 1024),
                        AvailableBytes = (long)(ramFree * 1024d * 1024 * 1024)
                    }
                },
                Tools = new ToolsProfile
                {
                    Ollama = new OllamaStatus
                    {
                        Installed = true,
                        ServerRunning = true,
                        Models = new List<ModelInfo>
                        {
                            new() { Name = _installedModel, SizeBytes = 1848L * 1024 * 1024, Capabilities = new List<string> { "completion" } }
                        }
                    }
                },
                Capabilities = new CapabilitiesSummary { ModelsCount = 1, OllamaReady = true }
            });
        }
    }

    private sealed class RamState
    {
        public double FreeGb { get; set; }
    }

    /// <summary>
    /// Confirmador configurable. Cuando confirma y SeLiberaRamaAlConfirmar es true,
    /// libera RAM (sube el valor del estado compartido) para simular que el usuario
    /// cerro aplicaciones: la re-evaluacion posterior vera RAM suficiente.
    /// </summary>
    private sealed class StubConfirmation : IUserConfirmation
    {
        private readonly bool _response;
        private readonly RamState? _ram;
        private readonly double _releasedGb;
        public int AskCount { get; private set; }

        public StubConfirmation(bool response, RamState? ram = null, double releasedGb = 0)
        {
            _response = response;
            _ram = ram;
            _releasedGb = releasedGb;
        }

        public Task<bool> AskToReleaseRamAsync(string prompt, CancellationToken cancellationToken = default)
        {
            AskCount++;
            if (_response && _ram is not null && _releasedGb > 0)
            {
                _ram.FreeGb = _releasedGb; // el usuario "libero" RAM
            }

            return Task.FromResult(_response);
        }
    }
}
