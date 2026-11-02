using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Condor.Core.Catalog;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Infrastructure.Detection;
using Condor.Infrastructure.Llm;
using Condor.Infrastructure.Setup;
using Condor.Infrastructure.State;

namespace Condor.Infrastructure.Tests;

public class ModelAutoSetupServiceTests
{
    [Fact]
    public async Task EnsureModel_ModeloDeseadoInstalado_ReutilizaSinPull()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        await store.SaveAssessmentAsync(AssessmentConModelo("qwen2.5-coder:7b", ramFreeGb: 12));
        var service = CreateService(store, "qwen2.5-coder:7b");

        var result = await service.EnsureModelAsync(cancellationToken: CancellationToken.None);

        Assert.True(result.AlreadyInstalled);
        Assert.Equal("qwen2.5-coder:7b", result.InstalledName);
        Assert.Contains("reutiliza", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnsureModel_AlternativaMenosCapaz_SeleccionaElDeseado()
    {
        // La alternativa instalada no declara uso de herramientas; el perfil
        // generalista viable se recomienda, pero Ollama apagado impide descargar.
        var store = new LocalStateStore(DirectorioTemporal());
        var assessment = AssessmentConModelo("llama3.2:3b", ramFreeGb: 10);
        assessment.Tools!.Ollama!.ServerRunning = false;
        await store.SaveAssessmentAsync(assessment);
        var service = CreateService(store, "llama3.2:3b");

        var result = await service.EnsureModelAsync(cancellationToken: CancellationToken.None);

        Assert.NotNull(result.Desired);
        Assert.Equal("granite4.2:3b", result.Desired.PullName);
        Assert.NotEqual("llama3.2:3b", result.InstalledName);
    }

    [Fact]
    public async Task EnsureModel_SinAssessment_NoSelecciona()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        var service = CreateService(store);

        var result = await service.EnsureModelAsync(cancellationToken: CancellationToken.None);

        Assert.Null(result.Desired);
    }

    [Fact]
    public async Task EnsureModel_OllamaNoDisponible_DegradaSinPull()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        await store.SaveAssessmentAsync(AssessmentOllamaApagado());
        var service = CreateService(store, serverRunning: false);

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
        var service = CreateService(store);

        var result = await service.EnsureModelAsync(cancellationToken: CancellationToken.None);

        Assert.Null(result.Desired);
    }

    [Fact]
    public async Task EnsureModel_ReevaluaRAMAntesDeDescargar_YCancelaSiYaNoCabe()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        using var handler = new PullTrackingHandler();
        using var http = new HttpClient(handler);
        var assessments = new SequenceAssessmentService(
            AssessmentConModelo("sin-modelo", ramFreeGb: 8),
            AssessmentConModelo("sin-modelo", ramFreeGb: 8),
            AssessmentConModelo("sin-modelo", ramFreeGb: 1));
        var modelOperator = new OllamaModelOperator(http, new StubOllamaDetector(Array.Empty<string>()));
        var service = new ModelAutoSetupService(store, assessments, modelOperator: modelOperator);

        var result = await service.EnsureModelAsync(cancellationToken: CancellationToken.None);

        Assert.Null(result.Desired);
        Assert.False(handler.PullRequested);
        Assert.Equal(3, assessments.CallCount);
    }

    private static AssessmentResult AssessmentConModelo(string name, double ramFreeGb = 10)
    {
        return new AssessmentResult
        {
            Environment = new EnvironmentProfile
            {
                Memory = new MemoryInfo
                {
                    Status = DetectionStatus.Detected,
                    TotalBytes = 16L * 1024 * 1024 * 1024,
                    FreeBytes = (long)(ramFreeGb * 1024 * 1024 * 1024)
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
                        new()
                        {
                            Name = name,
                            SizeBytes = ModelCatalog.Default.FirstOrDefault(candidate =>
                                candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.SizeBytes ?? 1024L * 1024 * 1024,
                            Capabilities = name.Contains("coder", StringComparison.OrdinalIgnoreCase)
                                ? new List<string> { "completion", "tools" }
                                : new List<string> { "completion" }
                        }
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

    private static ModelAutoSetupService CreateService(LocalStateStore store, params string[] installedModels)
        => CreateService(store, serverRunning: true, installedModels);

    private static ModelAutoSetupService CreateService(
        LocalStateStore store,
        bool serverRunning,
        params string[] installedModels)
    {
        var detector = new StubOllamaDetector(installedModels, serverRunning);
        var http = new HttpClient(new PullTrackingHandler());
        return new ModelAutoSetupService(store, modelOperator: new OllamaModelOperator(http, detector));
    }

    private sealed class StubOllamaDetector(IEnumerable<string> installedModels, bool serverRunning = true) : OllamaDetector
    {
        public override Task<OllamaStatus> DetectAsync(CancellationToken cancellationToken = default)
        {
            var models = installedModels.Select(name => new ModelInfo { Name = name }).ToList();
            return Task.FromResult(new OllamaStatus
            {
                Installed = true,
                ServerRunning = serverRunning,
                Models = models
            });
        }
    }

    private sealed class SequenceAssessmentService(params AssessmentResult[] results) : IAssessmentService
    {
        private int _index;
        public int CallCount { get; private set; }

        public Task<AssessmentResult> ExecuteAsync(AssessmentRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(results[Math.Min(_index++, results.Length - 1)]);
        }
    }

    private sealed class PullTrackingHandler : HttpMessageHandler
    {
        public bool PullRequested { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath == "/api/pull")
            {
                PullRequested = true;
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("unexpected request", Encoding.UTF8, "text/plain")
            });
        }
    }
}
