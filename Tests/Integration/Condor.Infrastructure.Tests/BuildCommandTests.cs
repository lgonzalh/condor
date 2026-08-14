using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Condor.Cli.Commands;
using Condor.Core.Models;
using Condor.Infrastructure.Building;
using Condor.Infrastructure.State;

namespace Condor.Infrastructure.Tests;

public class BuildCommandTests
{
    [Fact]
    public async Task ExecuteAsync_ConPlan_DevuelveExitCodeCero()
    {
        var storeDirectory = DirectorioTemporal();
        var store = new LocalStateStore(storeDirectory);
        await store.SavePlanAsync(PlanConTarea(DirectorioProyecto(storeDirectory)));

        var exitCode = await BuildCommand.ExecuteAsync(
            new BuildService(store),
            store,
            new[] { "--json" });

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_PersisteBuildJson()
    {
        var storeDirectory = DirectorioTemporal();
        var store = new LocalStateStore(storeDirectory);
        await store.SavePlanAsync(PlanConTarea(DirectorioProyecto(storeDirectory)));

        var exitCode = await BuildCommand.ExecuteAsync(
            new BuildService(store),
            store,
            new[] { "--json" });

        var persisted = await new LocalStateStore(storeDirectory).LoadBuildAsync();

        Assert.Equal(0, exitCode);
        Assert.NotNull(persisted);
        Assert.Equal("1.0.0", persisted.SchemaVersion);
    }

    [Fact]
    public async Task ExecuteAsync_SinPlan_DevuelveExitCodeUno()
    {
        var store = new LocalStateStore(DirectorioTemporal());

        var exitCode = await BuildCommand.ExecuteAsync(
            new BuildService(store),
            store,
            new[] { "--json" });

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_SinPlan_EscribeMensajeInstructivoEnEspanolSinTildes()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            var exitCode = await BuildCommand.ExecuteAsync(
                new BuildService(store),
                store,
                Array.Empty<string>());

            var output = writer.ToString();

            Assert.Equal(1, exitCode);
            Assert.Contains("No hay cambios para aplicar.", output);
            Assert.Contains("condor planear", output);
            Assert.DoesNotContain(output, texto => "áéíóúñÁÉÍÓÚ".Contains(texto, StringComparison.Ordinal));
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private static WorkPlan PlanConTarea(string workingDirectory)
    {
        return new WorkPlan
        {
            SchemaVersion = "1.0.0",
            Status = DetectionStatus.Detected,
            WorkingDirectory = workingDirectory,
            RootName = "condor",
            Intention = "modificar",
            Objective = "Aplicar cambios",
            Tasks = new List<PlanTask>
            {
                new()
                {
                    Id = "T0",
                    Title = "crear configuracion",
                    Detail = "Tarea con [ruta:config/app.json].",
                    Evidence = "origen-plan"
                }
            },
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static string DirectorioProyecto(string storeDirectory)
    {
        return Path.Combine(storeDirectory, "proyecto");
    }

    private static string DirectorioTemporal()
    {
        return Path.Combine(Path.GetTempPath(), "condor-buildcli-" + Guid.NewGuid().ToString("N"));
    }
}
