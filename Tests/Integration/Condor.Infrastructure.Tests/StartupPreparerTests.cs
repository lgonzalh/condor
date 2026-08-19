using Condor.Cli.Routing;
using Condor.Core.Contracts;
using Condor.Core.Models;
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
        var live = new StubAssessmentService(ConOllamaActivaYModelo());

        var preparer = new StartupPreparer(live, store);

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

        // El inventario real de Ollama (autoridad) tiene el modelo: se muestra listo.
        var preparer = new StartupPreparer(
            new StubAssessmentService(ConOllamaActivaYModelo()),
            store,
            modelAutoSetup: new StubModelAutoSetup(ModelSelectionEstancado()));

        var result = await preparer.RunAsync();

        Assert.True(result.Ready);
        Assert.Contains("qwen2.5-coder:3b", result.Model);
    }

    [Fact]
    public async Task RunAsync_OllamaVacioConEstadoPrevio_NoDeclaraModeloListo()
    {
        // REGRESION: el inventario de Ollama (/api/tags) esta VACIO, pero el estado
        // persistido (%LOCALAPPDATA%\Condor\state) dice que qwen2.5-coder:3b esta
        // instalado. Regla "estado persistido != estado real": Ollama es la autoridad.
        // Aunque el auto-setup devuelva AlreadyInstalled=true (simula un estancamiento
        // en el estado viejo), Cóndor NO debe declarar el modelo como listo.
        var storeDir = TempDir();
        var store = new LocalStateStore(storeDir);
        await store.SaveAssessmentAsync(ConOllamaActivaYModelo()); // estado viejo con 3b

        var oklahomaVacio = new StubAssessmentService(ConOllamaVacio());
        var autoSetupEstancado = new StubModelAutoSetup(ModelSelectionEstancado());

        var preparer = new StartupPreparer(
            oklahomaVacio,
            store,
            modelAutoSetup: autoSetupEstancado);

        var result = await preparer.RunAsync();

        Assert.Null(result.Model);
    }

    [Fact]
    public async Task RunAsync_OllamaVacioYDescargaFallida_NoListoYNecesitaIntervencion()
    {
        // REGRESION/REQUISITO: inventario de Ollama vacio y, pese a la
        // preparacion/descarga acotada, no se obtuvo un modelo utilizable.
        // Cóndor NO debe declarar "listo" ni arrancar sin capacidad operativa:
        // debe reportar el motivo e indicar que necesita intervencion.
        var storeDir = TempDir();
        var store = new LocalStateStore(storeDir);
        await store.SaveAssessmentAsync(ConOllamaVacio());

        var oklahomaReal = new StubAssessmentService(ConOllamaVacio()); // /api/tags sigue vacio
        var autoSetupFallido = new StubModelAutoSetup(ModelSelectionDescargaFallida());

        var preparer = new StartupPreparer(
            oklahomaReal,
            store,
            modelAutoSetup: autoSetupFallido);

        var result = await preparer.RunAsync();

        Assert.False(result.Ready);
        Assert.True(result.NeedsIntervention);
        Assert.Null(result.Model);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
        Assert.Contains("modelo", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_OllamaVacioSinModeloViable_PorRecursosNoListo()
    {
        // Inventario vacio y ningun modelo del catalogo cabe por recursos:
        // no se observa "Entorno listo" ni se deja esperando sin motivo.
        var storeDir = TempDir();
        var store = new LocalStateStore(storeDir);
        await store.SaveAssessmentAsync(ConOllamaVacio());

        var oklahomaReal = new StubAssessmentService(ConOllamaVacio());
        var autoSetupBloqueado = new StubModelAutoSetup(ModelSelectionBloqueadaPorRecursos());

        var preparer = new StartupPreparer(
            oklahomaReal,
            store,
            modelAutoSetup: autoSetupBloqueado);

        var result = await preparer.RunAsync();

        Assert.False(result.Ready);
        Assert.True(result.NeedsIntervention);
        Assert.Null(result.Model);
        Assert.Contains("recursos", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_ModelosInstaladosPeroRamaBaja_ArrancaSesionSinBloquear()
    {
        // PROMESA FUNDAMENTAL: hay modelos instalados en el inventario real de
        // Ollama pero la RAM libre actual no alcanza el presupuesto seguro.
        // Condor NO debe bloquear el inicio ("hay modelos pero no puedo usarlos");
        // debe arrancar la sesion, informar la RAM con honestidad y dejar que el
        // AgentService decida/recupere el modelo en cada tarea.
        var storeDir = TempDir();
        var store = new LocalStateStore(storeDir);
        await store.SaveAssessmentAsync(ConOllamaConModelosYRamaBaja());

        var oklahomaReal = new StubAssessmentService(ConOllamaConModelosYRamaBaja());
        var autoSetupBloqueado = new StubModelAutoSetup(ModelSelectionBloqueadaPorRecursos());

        var preparer = new StartupPreparer(
            oklahomaReal,
            store,
            modelAutoSetup: autoSetupBloqueado);

        var result = await preparer.RunAsync();

        Assert.True(result.Ready);            // la sesion arranca
        Assert.True(result.NeedsIntervention); // pero se advierte la RAM
        Assert.Null(result.Model);             // no se afirma un modelo listo que no lo esta
        Assert.True(result.Reason?.IndexOf("Hay modelos instalados", StringComparison.OrdinalIgnoreCase) >= 0);
        Assert.True(result.Reason?.IndexOf("liberar", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static AssessmentResult ConOllamaConModelosYRamaBaja()
    {
        // Inventario real de Ollama con modelos instalados, pero RAM libre baja.
        return new AssessmentResult
        {
            SchemaVersion = "1.0.0",
            Environment = new EnvironmentProfile
            {
                Memory = new MemoryInfo
                {
                    Status = DetectionStatus.Detected,
                    TotalBytes = 16L * 1024 * 1024 * 1024,
                    FreeBytes = (long)(4.0 * 1024 * 1024 * 1024)
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
                        new() { Name = "qwen2.5-coder:3b" },
                        new() { Name = "qwen2.5-coder:7b" }
                    }
                }
            }
        };
    }

    private static ModelSelectionResult ModelSelectionDescargaFallida()
    {
        return new ModelSelectionResult
        {
            Desired = new ModelCandidate { PullName = "qwen2.5-coder:3b" },
            AlreadyInstalled = false,
            Reason = "No fue posible obtener el modelo automaticamente.",
            Limitations = { "No fue posible obtener el modelo tras los reintentos limitados." }
        };
    }

    private static ModelSelectionResult ModelSelectionBloqueadaPorRecursos()
    {
        return new ModelSelectionResult
        {
            Desired = null,
            AlreadyInstalled = false,
            BlockedByResources = true,
            Resources = new ResourceSnapshot
            {
                FreeGb = 0.9,
                Pressure = ResourcePressure.Insufficient
            },
            Limitations =
            {
                "Ningun modelo del catalogo cumple ambas condiciones: el porcentaje de RAM total permitido y el presupuesto seguro."
            }
        };
    }

    private static AssessmentResult ConOllamaVacio()
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
                    Models = new List<ModelInfo>() // inventario real vacio
                }
            }
        };
    }

    private static ModelSelectionResult ModelSelectionEstancado()
    {
        return new ModelSelectionResult
        {
            Desired = new ModelCandidate { PullName = "qwen2.5-coder:3b" },
            AlreadyInstalled = true,
            InstalledName = "qwen2.5-coder:3b"
        };
    }

    private sealed class StubAssessmentService : IAssessmentService
    {
        private readonly AssessmentResult _result;
        public StubAssessmentService(AssessmentResult result) => _result = result;
        public Task<AssessmentResult> ExecuteAsync(AssessmentRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }

    private sealed class StubModelAutoSetup : IModelAutoSetupService
    {
        private readonly ModelSelectionResult _result;
        public StubModelAutoSetup(ModelSelectionResult result) => _result = result;
        public Task<ModelSelectionResult> EnsureModelAsync(string? purpose = null, CancellationToken ct = default, IStartupProgressObserver? progress = null)
            => Task.FromResult(_result);
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
