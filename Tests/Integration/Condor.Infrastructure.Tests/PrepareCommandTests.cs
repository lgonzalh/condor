using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Condor.Cli.Commands;
using Condor.Core.Models;
using Condor.Infrastructure.Setup;
using Condor.Infrastructure.State;

namespace Condor.Infrastructure.Tests;

public class PrepareCommandTests
{
    [Fact]
    public async Task ExecuteAsync_ConTodoListo_DevuelveExitCodeCero()
    {
        var storeDirectory = DirectorioTemporal();
        var stateDir = Path.Combine(storeDirectory, "estado");
        Directory.CreateDirectory(stateDir);
        var store = new LocalStateStore(storeDirectory);
        await store.SaveAssessmentAsync(AssessmentListo());
        var service = new SetupService(store, stateDirectory: stateDir);

        var exitCode = await PrepareCommand.ExecuteAsync(service, null, new[] { "--json" }, CancellationToken.None);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_SinDotnet_DevuelveExitCodeNoCero()
    {
        var storeDirectory = DirectorioTemporal();
        var stateDir = Path.Combine(storeDirectory, "estado");
        Directory.CreateDirectory(stateDir);
        var store = new LocalStateStore(storeDirectory);
        await store.SaveAssessmentAsync(AssessmentSinDotnet());
        var service = new SetupService(store, stateDirectory: stateDir);

        var exitCode = await PrepareCommand.ExecuteAsync(service, null, new[] { "--json" }, CancellationToken.None);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_ConTodoListo_EscribeSalidaEnEspanolSinTildes()
    {
        var storeDirectory = DirectorioTemporal();
        var stateDir = Path.Combine(storeDirectory, "estado");
        Directory.CreateDirectory(stateDir);
        var store = new LocalStateStore(storeDirectory);
        await store.SaveAssessmentAsync(AssessmentListo());
        var service = new SetupService(store, stateDirectory: stateDir);
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            await PrepareCommand.ExecuteAsync(service, null, new[] { "preparar" }, CancellationToken.None);
            var output = writer.ToString();
            Assert.Contains("PREPARAR", output);
            Assert.DoesNotContain(output, t => "áéíóúñÁÉÍÓÚ".Contains(t, StringComparison.Ordinal));
        }
        finally
        {
            Console.SetOut(originalOut);
        }
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

    private static AssessmentResult AssessmentSinDotnet()
    {
        var a = AssessmentListo();
        a.Tools.DetectedTools.Clear();
        return a;
    }

    private static string DirectorioTemporal()
    {
        return Path.Combine(Path.GetTempPath(), "condor-prepcli-" + Guid.NewGuid().ToString("N"));
    }
}
