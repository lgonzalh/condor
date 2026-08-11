using System.Text;
using Condor.Core.Models;
using Condor.Infrastructure.State;

namespace Condor.Infrastructure.Tests;

public class LocalStateStoreTests
{
    [Fact]
    public async Task LoadAssessmentAsync_CuandoNoExisteEstado_DevuelveNull()
    {
        var store = new LocalStateStore(DirectorioTemporal());

        var result = await store.LoadAssessmentAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAssessmentAsync_CuandoElJsonEstaCorrupto_DevuelveNull()
    {
        var directory = DirectorioTemporal();
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "assessment.json"),
            "esto no es un json valido {{{",
            Encoding.UTF8);
        var store = new LocalStateStore(directory);

        var result = await store.LoadAssessmentAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAssessmentAsync_CuandoElJsonEsParcial_DevuelveResultadoSeguroSinModelos()
    {
        var directory = DirectorioTemporal();
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "assessment.json"),
            "{\"schemaVersion\":\"1.0.0\"}",
            Encoding.UTF8);
        var store = new LocalStateStore(directory);

        var result = await store.LoadAssessmentAsync();

        Assert.NotNull(result);
        Assert.Equal("1.0.0", result.SchemaVersion);
        Assert.Empty(result.Tools.Ollama.Models);
    }

    [Fact]
    public async Task SaveYLoad_ConservanElAssessment()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        var assessment = new AssessmentResult
        {
            SchemaVersion = "1.0.0",
            GeneratedAtUtc = DateTime.UtcNow,
            WorkingDirectory = "C:\\proyecto",
            Tools = new ToolsProfile
            {
                Ollama = new OllamaStatus
                {
                    Installed = true,
                    ServerRunning = true,
                    Models = new List<ModelInfo> { new ModelInfo { Name = "qwen3:8b" } }
                }
            }
        };

        await store.SaveAssessmentAsync(assessment);
        var loaded = await store.LoadAssessmentAsync();

        Assert.NotNull(loaded);
        Assert.Equal("1.0.0", loaded.SchemaVersion);
        Assert.Equal("C:\\proyecto", loaded.WorkingDirectory);
        Assert.Equal("qwen3:8b", loaded.Tools.Ollama.Models[0].Name);
        Assert.True(loaded.Tools.Ollama.Installed);
    }

    private static string DirectorioTemporal()
    {
        return Path.Combine(Path.GetTempPath(), "condor-tests-" + Guid.NewGuid().ToString("N"));
    }
}
