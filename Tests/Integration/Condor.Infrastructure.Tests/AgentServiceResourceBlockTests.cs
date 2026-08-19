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
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 5, installedModel: "qwen2.5-coder:3b");
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
        var assessment = new StubAssessmentService(ramTotalGb: 16, ramFreeGb: 5, installedModel: "qwen2.5-coder:3b");
        var service = new AgentService(store, assessment);

        var result = await service.RunAsync("analiza", cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        // El gate re-evalua un numero LIMITADO de veces; nunca infinito.
        // Cada EnsureModelAsync = 1 live + 1 refresh = 2 llamadas al service.
        // Inicial (2) + MaxResourceRecoveryAttempts=3 (3*2=6) + refresh final (2) => tope conservador.
        Assert.True(assessment.ExecuteCount <= 12, "La recuperacion de recursos debe ser acotada.");
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
        public int ExecuteCount { get; private set; }

        public StubAssessmentService(double ramTotalGb, double ramFreeGb, string installedModel)
        {
            _ramTotalGb = ramTotalGb;
            _ramFreeGb = ramFreeGb;
            _installedModel = installedModel;
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
}
