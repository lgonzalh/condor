using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Condor.Cli.Commands;
using Condor.Core.Models;
using Condor.Infrastructure.State;
using Condor.Infrastructure.Verification;

namespace Condor.Infrastructure.Tests;

public class VerifyCommandTests
{
    [Fact]
    public async Task ExecuteAsync_ConBuild_DevuelveExitCodeCero()
    {
        var storeDirectory = DirectorioTemporal();
        var store = new LocalStateStore(storeDirectory);
        await store.SaveBuildAsync(BuildConAccion(DirectorioProyecto(storeDirectory)));
        await store.SaveContextAsync(Contexto(DirectorioProyecto(storeDirectory)));

        var exitCode = await VerifyCommand.ExecuteAsync(
            new VerificationService(store),
            store,
            new[] { "--json" });

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_PersisteVerificationJson()
    {
        var storeDirectory = DirectorioTemporal();
        var store = new LocalStateStore(storeDirectory);
        await store.SaveBuildAsync(BuildConAccion(DirectorioProyecto(storeDirectory)));
        await store.SaveContextAsync(Contexto(DirectorioProyecto(storeDirectory)));

        var exitCode = await VerifyCommand.ExecuteAsync(
            new VerificationService(store),
            store,
            new[] { "--json" });

        var persisted = await new LocalStateStore(storeDirectory).LoadVerificationAsync();

        Assert.Equal(0, exitCode);
        Assert.NotNull(persisted);
        Assert.Equal("1.0.0", persisted.SchemaVersion);
    }

    [Fact]
    public async Task ExecuteAsync_SinBuild_DevuelveExitCodeUno()
    {
        var store = new LocalStateStore(DirectorioTemporal());

        var exitCode = await VerifyCommand.ExecuteAsync(
            new VerificationService(store),
            store,
            new[] { "--json" });

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_SinBuild_EscribeMensajeInstructivoEnEspanolSinTildes()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            var exitCode = await VerifyCommand.ExecuteAsync(
                new VerificationService(store),
                store,
                new[] { "primero" });

            var output = writer.ToString();

            Assert.Equal(1, exitCode);
            Assert.Contains("No hay cambios para verificar.", output);
            Assert.Contains("condor construir", output);
            Assert.DoesNotContain(output, texto => "áéíóúñÁÉÍÓÚ".Contains(texto, StringComparison.Ordinal));
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private static BuildResult BuildConAccion(string workingDirectory)
    {
        return new BuildResult
        {
            SchemaVersion = "1.0.0",
            Status = DetectionStatus.Detected,
            WorkingDirectory = workingDirectory,
            RootName = "condor",
            Objective = "Verificar cambios",
            Actions = new List<BuildAction>
            {
                new()
                {
                    Id = "B0",
                    Kind = BuildActionKind.Crear,
                    RelativePath = "config/app.json",
                    Content = "configuracion",
                    Status = BuildAction.StatusOmitted,
                    StatusReason = "Accion omitida en el build"
                }
            },
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static ProjectContext Contexto(string workingDirectory)
    {
        return new ProjectContext
        {
            SchemaVersion = "1.0.0",
            Status = DetectionStatus.Detected,
            WorkingDirectory = workingDirectory,
            RootName = "condor",
            Risks = new List<ContextRisk>(),
            RelevantDependencies = new List<RelevantDependency>(),
            Recommendations = new List<PlannerRecommendation>(),
            LimitsApplied = new List<string>(),
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static string DirectorioProyecto(string storeDirectory)
    {
        return Path.Combine(storeDirectory, "proyecto");
    }

    private static string DirectorioTemporal()
    {
        return Path.Combine(Path.GetTempPath(), "condor-verifycli-" + Guid.NewGuid().ToString("N"));
    }
}
