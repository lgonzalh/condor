using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Core.Serialization;
using Condor.Infrastructure.State;
using Condor.Infrastructure.Vision;

namespace Condor.Infrastructure.Tests;

public class VisionServiceTests
{
    [Fact]
    public async Task ExamineAsync_ConCapacidadYModelo_DevuelveDescripcion()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        await store.SaveAssessmentAsync(AssessmentConVision());
        var image = CrearImagen(100);
        var service = new VisionService(store, new StubLlm("descripcion de prueba"));

        var result = await service.ExamineAsync(image, CancellationToken.None);

        Assert.Equal(DetectionStatus.Detected, result.Status);
        Assert.Equal("descripcion de prueba", result.Description);
        Assert.Equal("llm3.2-vision", result.ModelUsed);
        Assert.Equal(100, result.ImageBytes);
    }

    [Fact]
    public async Task ExamineAsync_SinCapacidad_Degrada()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        await store.SaveAssessmentAsync(new AssessmentResult
        {
            Capabilities = new CapabilitiesSummary { VisionCapable = false }
        });
        var image = CrearImagen(50);
        var service = new VisionService(store, new StubLlm("x"));

        var result = await service.ExamineAsync(image, CancellationToken.None);

        Assert.Equal(DetectionStatus.Limited, result.Status);
        Assert.Contains("vision", result.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExamineAsync_SinModeloVision_Degrada()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        await store.SaveAssessmentAsync(AssessmentConVisionSinModelo());
        var image = CrearImagen(50);
        var service = new VisionService(store, new StubLlm("x"));

        var result = await service.ExamineAsync(image, CancellationToken.None);

        Assert.Equal(DetectionStatus.Limited, result.Status);
        Assert.Contains("vision", result.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExamineAsync_ImagenInexistente_Degrada()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        await store.SaveAssessmentAsync(AssessmentConVision());
        var service = new VisionService(store, new StubLlm("x"));

        var result = await service.ExamineAsync(Path.Combine(DirectorioTemporal(), "no-existe.png"), CancellationToken.None);

        Assert.Equal(DetectionStatus.Limited, result.Status);
        Assert.Contains("no existe", result.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExamineAsync_ImagenSinAssessment_Degrada()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        var image = CrearImagen(50);
        var service = new VisionService(store, new StubLlm("x"));

        var result = await service.ExamineAsync(image, CancellationToken.None);

        Assert.Equal(DetectionStatus.Limited, result.Status);
    }

    [Fact]
    public async Task ExamineAsync_DescripcionSuperaLimite_TruncaDeterminista()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        await store.SaveAssessmentAsync(AssessmentConVision());
        var image = CrearImagen(100);
        var longText = new string('x', 5000);
        var service = new VisionService(store, new StubLlm(longText));

        var result = await service.ExamineAsync(image, CancellationToken.None);

        Assert.Equal(DetectionStatus.Detected, result.Status);
        Assert.True(result.Description.Length <= VisionLimits.Default.MaxDescriptionLength);
        Assert.Equal(VisionLimits.Default.MaxDescriptionLength, result.Description.Length);
    }

    [Fact]
    public async Task Determinismo_ParteNoLlm_ProduceElMismoResultadoEstructural()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        await store.SaveAssessmentAsync(AssessmentConVision());
        var image = CrearImagen(80);
        var service = new VisionService(store, new StubLlm("descripcion fija"));

        var first = await service.ExamineAsync(image, CancellationToken.None);
        var second = await service.ExamineAsync(image, CancellationToken.None);

        first.GeneratedAtUtc = DateTime.MinValue;
        second.GeneratedAtUtc = DateTime.MinValue;

        Assert.Equal(VisionJson.Serialize(first), VisionJson.Serialize(second));
    }

    [Fact]
    public async Task ExamineAsync_Timeout_DevuelveLimited()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        await store.SaveAssessmentAsync(AssessmentConVision());
        var image = CrearImagen(50);
        var limits = new VisionLimits { VisionTimeoutMilliseconds = 50 };
        var service = new VisionService(store, new SlowLlm(TimeSpan.FromMilliseconds(1000)), limits);

        var result = await service.ExamineAsync(image, CancellationToken.None);

        Assert.Equal(DetectionStatus.Limited, result.Status);
        Assert.Contains("tiempo", result.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    private static AssessmentResult AssessmentConVision()
    {
        return new AssessmentResult
        {
            Capabilities = new CapabilitiesSummary { VisionCapable = true },
            Tools = new ToolsProfile
            {
                Ollama = new OllamaStatus
                {
                    Models = new List<ModelInfo>
                    {
                        new() { Name = "llm3.2-vision", Capabilities = new List<string> { "vision" } }
                    }
                }
            }
        };
    }

    private static AssessmentResult AssessmentConVisionSinModelo()
    {
        return new AssessmentResult
        {
            Capabilities = new CapabilitiesSummary { VisionCapable = true },
            Tools = new ToolsProfile
            {
                Ollama = new OllamaStatus
                {
                    Models = new List<ModelInfo>
                    {
                        new() { Name = "texto", Capabilities = new List<string> { "completion" } }
                    }
                }
            }
        };
    }

    private static string CrearImagen(int bytes)
    {
        var dir = DirectorioTemporal();
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "imagen-" + Guid.NewGuid().ToString("N") + ".png");
        File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    private static string DirectorioTemporal()
    {
        return Path.Combine(Path.GetTempPath(), "condor-vision-" + Guid.NewGuid().ToString("N"));
    }

    private sealed class StubLlm : ILlmClient
    {
        private readonly string _content;

        public StubLlm(string content) => _content = content;

        public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(new LlmResponse { Success = true, Content = _content, Model = request.Model });
        }
    }

    private sealed class SlowLlm : ILlmClient
    {
        private readonly TimeSpan _delay;

        public SlowLlm(TimeSpan delay) => _delay = delay;

        public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
        {
            await Task.Delay(_delay, CancellationToken.None);
            return new LlmResponse { Success = true, Content = "lento", Model = request.Model };
        }
    }
}
