using Condor.Cli.Routing;
using Condor.Core.Models;
using Condor.Infrastructure.Setup;
using Condor.Infrastructure.State;

namespace Condor.Infrastructure.Tests;

public class StartupPreparerTests
{
    [Fact]
    public async Task RunAsync_CuandoExisteAssessment_NoReEjecutaDeteccion()
    {
        var storeDir = TempDir();
        var store = new LocalStateStore(storeDir);
        await store.SaveAssessmentAsync(ConOllamaDetenido());

        var preparer = new StartupPreparer(new AssessmentService(), store);

        var result = await preparer.RunAsync();

        Assert.True(result.Ready);
        Assert.False(result.NeedsIntervention);
    }

    [Fact]
    public async Task RunAsync_SinOllama_DejaListoYNoIntentaObtenerModelo()
    {
        var storeDir = TempDir();
        var store = new LocalStateStore(storeDir);
        await store.SaveAssessmentAsync(ConOllamaDetenido());

        var preparer = new StartupPreparer(new AssessmentService(), store);

        var result = await preparer.RunAsync();

        Assert.True(result.Ready);
        Assert.Null(result.Model);
    }

    [Fact]
    public async Task RunAsync_SinAssessment_GeneraUnoAutomaticamente()
    {
        var storeDir = TempDir();
        var store = new LocalStateStore(storeDir);

        var preparer = new StartupPreparer(new AssessmentService(), store);

        var result = await preparer.RunAsync();

        Assert.True(result.Ready);
        var persisted = await store.LoadAssessmentAsync();
        Assert.NotNull(persisted);
        Assert.Equal("1.0.0", persisted.SchemaVersion);
    }

    [Fact]
    public async Task RunAsync_ShowsModelCuandoHayOllamaYModelos()
    {
        var storeDir = TempDir();
        var store = new LocalStateStore(storeDir);
        await store.SaveAssessmentAsync(ConOllamaActivaYModelo());

        var preparer = new StartupPreparer(
            new AssessmentService(),
            store,
            modelAutoSetup: new ModelAutoSetupService(store));

        var result = await preparer.RunAsync();

        Assert.True(result.Ready);
        Assert.Contains("qwen2.5-coder:3b", result.Model);
    }

    private static AssessmentResult ConOllamaActivaYModelo()
    {
        return new AssessmentResult
        {
            SchemaVersion = "1.0.0",
            Tools = new ToolsProfile
            {
                Ollama = new OllamaStatus
                {
                    Installed = true,
                    ServerRunning = true,
                    Models = new List<ModelInfo> { new() { Name = "qwen2.5-coder:3b" } }
                }
            }
        };
    }

    private static AssessmentResult ConOllamaDetenido()
    {
        return new AssessmentResult
        {
            SchemaVersion = "1.0.0",
            Tools = new ToolsProfile
            {
                Ollama = new OllamaStatus { Installed = true, ServerRunning = false }
            }
        };
    }

    private static string TempDir()
    {
        return Path.Combine(Path.GetTempPath(), "condor-startup-" + Guid.NewGuid().ToString("N"));
    }
}
