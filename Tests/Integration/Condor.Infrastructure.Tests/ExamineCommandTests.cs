using System;
using System.Threading.Tasks;
using Condor.Cli.Commands;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Infrastructure.State;
using Condor.Infrastructure.Vision;

namespace Condor.Infrastructure.Tests;

public class ExamineCommandTests
{
    [Fact]
    public async Task ExecuteAsync_ConExito_DevuelveExitCodeCero()
    {
        var storeDirectory = DirectorioTemporal();
        var store = new LocalStateStore(storeDirectory);
        await store.SaveAssessmentAsync(AssessmentConVision());
        var image = CrearImagen(50);
        var service = new VisionService(store, new StubLlm("descripcion"));

        var exitCode = await ExamineCommand.ExecuteAsync(
            service,
            store,
            new[] { image, "--json" },
            CancellationToken.None);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_SinCapacidad_DevuelveExitCodeNoCero()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        await store.SaveAssessmentAsync(new AssessmentResult
        {
            Capabilities = new CapabilitiesSummary { VisionCapable = false }
        });
        var image = CrearImagen(50);
        var service = new VisionService(store, new StubLlm("x"));

        var exitCode = await ExamineCommand.ExecuteAsync(
            service,
            store,
            new[] { image, "--json" },
            CancellationToken.None);

        Assert.Equal(1, exitCode);
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
                    Models = new System.Collections.Generic.List<ModelInfo>
                    {
                        new() { Name = "llm3.2-vision", Capabilities = new System.Collections.Generic.List<string> { "vision" } }
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
        return Path.Combine(Path.GetTempPath(), "condor-examcli-" + Guid.NewGuid().ToString("N"));
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
}
