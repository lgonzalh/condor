using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Condor.Core.Models;
using Condor.Core.Serialization;
using Condor.Infrastructure.Setup;
using Condor.Infrastructure.State;

namespace Condor.Infrastructure.Tests;

public class SetupServiceTests
{
    [Fact]
    public async Task PrepareAsync_ConAssessmentYEstadoLocalUsable_Detected()
    {
        var storeDirectory = DirectorioTemporal();
        var stateDir = Path.Combine(storeDirectory, "estado");
        Directory.CreateDirectory(stateDir);
        var store = new LocalStateStore(storeDirectory);
        await store.SaveAssessmentAsync(AssessmentListo());
        var service = new SetupService(store, stateDirectory: stateDir);

        var result = await service.PrepareAsync(refreshAssessment: false, CancellationToken.None);

        Assert.Equal(DetectionStatus.Detected, result.Status);
        Assert.True(result.StateUsable);
    }

    [Fact]
    public async Task PrepareAsync_EstadoLocalAusente_LimitedSinCrear()
    {
        var storeDirectory = DirectorioTemporal();
        var stateDir = Path.Combine(storeDirectory, "no-existe-estado");
        var store = new LocalStateStore(storeDirectory);
        await store.SaveAssessmentAsync(AssessmentListo());
        var service = new SetupService(store, stateDirectory: stateDir);

        var result = await service.PrepareAsync(refreshAssessment: false, CancellationToken.None);
        var dirLeft = Directory.Exists(stateDir);

        Assert.Equal(DetectionStatus.Limited, result.Status);
        Assert.False(result.StateUsable);
        Assert.False(dirLeft, "T-012 no debe crear el directorio de estado automaticamente");
    }

    [Fact]
    public async Task PrepareAsync_SinAssessment_UsoAssessmentServiceSiSeIndica()
    {
        var storeDirectory = DirectorioTemporal();
        var stateDir = Path.Combine(storeDirectory, "estado");
        Directory.CreateDirectory(stateDir);
        var store = new LocalStateStore(storeDirectory);
        var service = new SetupService(store, new FakeAssessment(AssessmentListo()), stateDirectory: stateDir);

        var result = await service.PrepareAsync(refreshAssessment: true, CancellationToken.None);

        Assert.Equal(DetectionStatus.Detected, result.Status);
    }

    [Fact]
    public async Task Determinismo_DosEjecucionesProducenElMismoResultado()
    {
        var storeDirectory = DirectorioTemporal();
        var stateDir = Path.Combine(storeDirectory, "estado");
        Directory.CreateDirectory(stateDir);
        var store = new LocalStateStore(storeDirectory);
        await store.SaveAssessmentAsync(AssessmentListo());
        var service = new SetupService(store, stateDirectory: stateDir);

        var first = await service.PrepareAsync(refreshAssessment: false, CancellationToken.None);
        var second = await service.PrepareAsync(refreshAssessment: false, CancellationToken.None);

        first.GeneratedAtUtc = DateTime.MinValue;
        second.GeneratedAtUtc = DateTime.MinValue;

        Assert.Equal(SetupJson.Serialize(first), SetupJson.Serialize(second));
    }

    private static AssessmentResult AssessmentListo()
    {
        return new AssessmentResult
        {
            Capabilities = new CapabilitiesSummary { LocalLlm = true, GpuDetected = true, ModelsCount = 1 },
            Tools = new ToolsProfile
            {
                Ollama = new OllamaStatus
                {
                    Installed = true,
                    ServerRunning = true,
                    Models = new List<ModelInfo> { new() { Name = "a", Capabilities = new List<string> { "completion" } } }
                },
                DetectedTools = new List<ToolInfo>
                {
                    new() { Name = "dotnet", Status = DetectionStatus.Detected }
                }
            }
        };
    }

    private static string DirectorioTemporal()
    {
        return Path.Combine(Path.GetTempPath(), "condor-setup-" + Guid.NewGuid().ToString("N"));
    }

    private sealed class FakeAssessment : Condor.Core.Contracts.IAssessmentService
    {
        private readonly AssessmentResult _result;

        public FakeAssessment(AssessmentResult result) => _result = result;

        public Task<AssessmentResult> ExecuteAsync(AssessmentRequest request, CancellationToken ct = default)
            => Task.FromResult(_result);
    }
}
