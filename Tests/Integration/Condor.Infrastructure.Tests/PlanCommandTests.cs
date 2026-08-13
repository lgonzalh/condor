using Condor.Cli.Commands;
using Condor.Core.Models;
using Condor.Infrastructure.Planning;
using Condor.Infrastructure.State;

namespace Condor.Infrastructure.Tests;

public class PlanCommandTests
{
    [Fact]
    public async Task ExecuteAsync_ConContexto_DevuelveExitCodeCero()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        await store.SaveContextAsync(ContextoConProyecto());

        var exitCode = await PlanCommand.ExecuteAsync(
            new PlanService(store),
            store,
            new[] { "modificar", "algo", "--json" });

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_PersistePlanJson()
    {
        var storeDirectory = DirectorioTemporal();
        var store = new LocalStateStore(storeDirectory);
        await store.SaveContextAsync(ContextoConProyecto());

        var exitCode = await PlanCommand.ExecuteAsync(
            new PlanService(store),
            store,
            new[] { "continuar", "el", "proyecto", "--json" });

        var persisted = await new LocalStateStore(storeDirectory).LoadPlanAsync();

        Assert.Equal(0, exitCode);
        Assert.NotNull(persisted);
        Assert.Equal("1.0.0", persisted.SchemaVersion);
    }

    [Fact]
    public async Task ExecuteAsync_SinContexto_DevuelveExitCodeUno()
    {
        var store = new LocalStateStore(DirectorioTemporal());

        var exitCode = await PlanCommand.ExecuteAsync(
            new PlanService(store),
            store,
            new[] { "crear", "algo", "--json" });

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_SinContexto_EscribeMensajeInstructivoEnEspanolSinTildes()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            var exitCode = await PlanCommand.ExecuteAsync(
                new PlanService(store),
                store,
                new[] { "crear", "algo" });

            var output = writer.ToString();

            Assert.Equal(1, exitCode);
            Assert.Contains("No hay plan disponible.", output);
            Assert.Contains("condor contexto", output);
            Assert.DoesNotContain(output, texto => "áéíóúñÁÉÍÓÚ".Contains(texto, StringComparison.Ordinal));
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private static ProjectContext ContextoConProyecto()
    {
        return new ProjectContext
        {
            SchemaVersion = "1.0.0",
            Status = DetectionStatus.Detected,
            WorkingDirectory = "C:\\proyecto",
            RootName = "condor",
            Summary = new ProjectContextSummary { IsGitRepository = false },
            Risks = new List<ContextRisk>(),
            RelevantDependencies = new List<RelevantDependency>(),
            Recommendations = new List<PlannerRecommendation>(),
            LimitsApplied = new List<string>(),
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static string DirectorioTemporal()
    {
        return Path.Combine(Path.GetTempPath(), "condor-plancli-" + Guid.NewGuid().ToString("N"));
    }
}
