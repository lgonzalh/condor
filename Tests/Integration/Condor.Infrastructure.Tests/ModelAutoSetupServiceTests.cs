using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Condor.Core.Models;
using Condor.Infrastructure.Setup;
using Condor.Infrastructure.State;

namespace Condor.Infrastructure.Tests;

public class ModelAutoSetupServiceTests
{
    [Fact]
    public async Task EnsureModel_ModeloDeseadoInstalado_ReutilizaSinPull()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        await store.SaveAssessmentAsync(AssessmentConModelo("qwen2.5-coder:7b"));
        var service = new ModelAutoSetupService(store);

        var result = await service.EnsureModelAsync(cancellationToken: CancellationToken.None);

        Assert.True(result.AlreadyInstalled);
        Assert.Equal("qwen2.5-coder:7b", result.InstalledName);
        Assert.Contains("reutiliza", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnsureModel_AlternativaMenosCapaz_SeleccionaElDeseado()
    {
        // La alternativa instalada (llama3.2:3b, general) es MENOS capaz en
        // ingenieria que el deseado viable (qwen2.5-coder:7b): la seleccion
        // apunta al deseado de mayor capacidad, no a la alternativa menor.
        var store = new LocalStateStore(DirectorioTemporal());
        await store.SaveAssessmentAsync(AssessmentConModelo("llama3.2:3b"));
        var service = new ModelAutoSetupService(store);

        var result = await service.EnsureModelAsync(cancellationToken: CancellationToken.None);

        Assert.NotNull(result.Desired);
        Assert.Equal("qwen2.5-coder:7b", result.Desired.PullName);
        Assert.NotEqual("llama3.2:3b", result.InstalledName);
    }

    [Fact]
    public async Task EnsureModel_SinAssessment_NoSelecciona()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        var service = new ModelAutoSetupService(store);

        var result = await service.EnsureModelAsync(cancellationToken: CancellationToken.None);

        Assert.Null(result.Desired);
    }

    [Fact]
    public async Task EnsureModel_OllamaNoDisponible_DegradaSinPull()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        await store.SaveAssessmentAsync(AssessmentOllamaApagado());
        var service = new ModelAutoSetupService(store);

        var result = await service.EnsureModelAsync(cancellationToken: CancellationToken.None);

        Assert.NotNull(result.Desired);
        Assert.False(result.AlreadyInstalled);
        Assert.Contains(result.Limitations, l => l.Contains("Ollama"));
    }

    [Fact]
    public async Task EnsureModel_RamaInsuficiente_NoSelecciona()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        await store.SaveAssessmentAsync(AssessmentRamaBaja());
        var service = new ModelAutoSetupService(store);

        var result = await service.EnsureModelAsync(cancellationToken: CancellationToken.None);

        Assert.Null(result.Desired);
    }

    private static AssessmentResult AssessmentConModelo(string name)
    {
        return new AssessmentResult
        {
            Environment = new EnvironmentProfile
            {
                Memory = new MemoryInfo
                {
                    Status = DetectionStatus.Detected,
                    TotalBytes = 16L * 1024 * 1024 * 1024,
                    FreeBytes = 8L * 1024 * 1024 * 1024
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
                        new() { Name = name, SizeBytes = 1024L * 1024 * 1024, Capabilities = new List<string> { "completion" } }
                    }
                }
            },
            Capabilities = new CapabilitiesSummary { ModelsCount = 1, OllamaReady = true }
        };
    }

    private static AssessmentResult AssessmentOllamaApagado()
    {
        return new AssessmentResult
        {
            Environment = new EnvironmentProfile
            {
                Memory = new MemoryInfo
                {
                    Status = DetectionStatus.Detected,
                    TotalBytes = 16L * 1024 * 1024 * 1024,
                    FreeBytes = 8L * 1024 * 1024 * 1024
                }
            },
            Tools = new ToolsProfile
            {
                Ollama = new OllamaStatus { Installed = true, ServerRunning = false, Models = new List<ModelInfo>() }
            },
            Capabilities = new CapabilitiesSummary { ModelsCount = 0, OllamaReady = false }
        };
    }

    private static AssessmentResult AssessmentRamaBaja()
    {
        return new AssessmentResult
        {
            Environment = new EnvironmentProfile
            {
                Memory = new MemoryInfo
                {
                    Status = DetectionStatus.Detected,
                    TotalBytes = 2L * 1024 * 1024 * 1024,
                    FreeBytes = 900L * 1024 * 1024
                }
            },
            Tools = new ToolsProfile
            {
                Ollama = new OllamaStatus { Installed = true, ServerRunning = true, Models = new List<ModelInfo>() }
            },
            Capabilities = new CapabilitiesSummary { ModelsCount = 0, OllamaReady = true }
        };
    }

    private static string DirectorioTemporal()
    {
        return Path.Combine(Path.GetTempPath(), "condor-modelauto-" + Guid.NewGuid().ToString("N"));
    }
}
