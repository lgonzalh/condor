using Condor.Cli.Commands;
using Condor.Core.Models;
using Condor.Infrastructure.Context;
using Condor.Infrastructure.State;

namespace Condor.Infrastructure.Tests;

public class ContextCommandTests
{
    [Fact]
    public async Task ExecuteAsync_SinAssessment_DevuelveExitCodeUnoYNoLanza()
    {
        var store = new LocalStateStore(DirectorioTemporal());

        var exitCode = await ContextCommand.ExecuteAsync(
            new ContextService(store),
            store,
            new[] { "--json" });

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_ConAssessment_DevuelveExitCodeCero()
    {
        var projectDirectory = DirectorioTemporal();
        Directory.CreateDirectory(projectDirectory);
        var storeDirectory = DirectorioTemporal();
        var store = new LocalStateStore(storeDirectory);
        await store.SaveAssessmentAsync(AssessmentConProyecto(projectDirectory));

        var exitCode = await ContextCommand.ExecuteAsync(
            new ContextService(store),
            store,
            new[] { "--json" });

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task ExecuteAsync_SinAssessment_EscribeMensajeInstructivoEnEspanolSinTildes()
    {
        var store = new LocalStateStore(DirectorioTemporal());
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            var exitCode = await ContextCommand.ExecuteAsync(
                new ContextService(store),
                store,
                Array.Empty<string>());

            var output = writer.ToString();

            Assert.Equal(1, exitCode);
            Assert.Contains("No hay contexto operativo disponible.", output);
            Assert.Contains("condor analizar", output);
            Assert.DoesNotContain(output, texto => "áéíóúñÁÉÍÓÚ".Contains(texto, StringComparison.Ordinal));
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private static AssessmentResult AssessmentConProyecto(string workingDirectory)
    {
        return new AssessmentResult
        {
            SchemaVersion = "1.0.0",
            WorkingDirectory = workingDirectory,
            Project = new ProjectProfile
            {
                Status = DetectionStatus.Detected,
                RootPath = workingDirectory,
                RootName = "condor",
                IsGitRepository = false
            }
        };
    }

    private static string DirectorioTemporal()
    {
        return Path.Combine(Path.GetTempPath(), "condor-cli-" + Guid.NewGuid().ToString("N"));
    }
}
