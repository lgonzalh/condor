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
/// Regresion del escenario de RAM fluctuante: hay modelos instalados pero la RAM
/// libre actual no permite cargar ningun modelo segun el presupuesto seguro.
/// Contrato T-017: fallo RAPIDO y controlado (sin bucle de re-evaluacion), sin
/// preguntar al usuario que libere memoria como mecanismo normal, sin excepciones
/// tecnicas visibles, con pantalla honesta "MODELO NO EJECUTABLE" (modelo, RAM
/// requerida, RAM disponible, motivo, accion) y tarea conservada.
/// </summary>
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
        // Pantalla honesta MODELO NO EJECUTABLE con datos concretos.
        Assert.Contains("MODELO NO EJECUTABLE", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RAM disponible", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RAM requerida estimada", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Motivo:", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Accion:", result.Reason, StringComparison.OrdinalIgnoreCase);
        // Nunca afirmar ausencia de modelos cuando el bloqueo es por recursos.
        Assert.DoesNotContain("No hay un modelo compatible disponible", result.Reason, StringComparison.OrdinalIgnoreCase);
        // Sin stack traces ni terminos tecnicos de excepcion.
        Assert.DoesNotContain("Exception", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at Condor.", result.Reason, StringComparison.OrdinalIgnoreCase);
        // No se pide al usuario liberar memoria como mecanismo normal.
        Assert.DoesNotContain("liberar memoria", result.Reason, StringComparison.OrdinalIgnoreCase);
        // La tarea no se pierde.
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
        // Fallo rapido: una sola seleccion (live + refresh = 2 llamadas). Tope
        // conservador para permitir refrescos internos acotados; NUNCA un bucle.
        Assert.True(assessment.ExecuteCount <= 4, "El bloqueo por RAM debe fallar rapido, sin bucle de recuperacion.");
    }

    [Fact]
    public async Task RunAsync_SinModelosNiPresupuesto_ReportaCompatibleNoDisponible()
    {
        // Equipo sin modelos instalados y sin presupuesto: no debe intentar
        // descargar nada fuera de presupuesto ni inventar compatibilidad.
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
                                SizeBytes = 1848L * 1024 * 1024,
                                Capabilities = new List<string> { "completion" }
                            }))
                    }
                },
                Capabilities = new CapabilitiesSummary { ModelsCount = _installedModels.Length, OllamaReady = true }
            });
        }
    }
}
