using Condor.Core.Models;

namespace Condor.Infrastructure.Tests;

public class AssessmentServiceTests
{
    [Fact]
    public async Task ExecuteAsync_GeneraResultadoConSchemaVersion()
    {
        var service = new AssessmentService();
        var request = new AssessmentRequest { WorkingDirectory = Environment.CurrentDirectory };

        var result = await service.ExecuteAsync(request);

        Assert.Equal("1.0.0", result.SchemaVersion);
        Assert.NotEqual(default, result.GeneratedAtUtc);
        Assert.False(string.IsNullOrWhiteSpace(result.WorkingDirectory));
        Assert.NotNull(result.Capabilities);
    }

    [Fact]
    public async Task ExecuteAsync_DetectaSistemaOperativoEnWindows()
    {
        var service = new AssessmentService();
        var request = new AssessmentRequest { WorkingDirectory = Environment.CurrentDirectory };

        var result = await service.ExecuteAsync(request);

        Assert.Equal(DetectionStatus.Detected, result.Environment.Os.Status);
    }

    [Fact]
    public async Task ExecuteAsync_EvaluacionDeCapacidadesCoherenteConHerramientas()
    {
        var service = new AssessmentService();
        var request = new AssessmentRequest { WorkingDirectory = Environment.CurrentDirectory };

        var result = await service.ExecuteAsync(request);

        Assert.Equal(result.Tools.Ollama.Installed, result.Capabilities.LocalLlm);
        Assert.Equal(result.Tools.Ollama.Models.Count, result.Capabilities.ModelsCount);
    }
}
