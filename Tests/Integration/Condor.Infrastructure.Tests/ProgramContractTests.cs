using Condor.Cli;
using Condor.Cli.Routing;

namespace Condor.Infrastructure.Tests;

public class ProgramContractTests
{
    [Theory]
    [InlineData("ayuda")]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("/ayuda")]
    [InlineData("/help")]
    public async Task Main_ComandosDeAyuda_DevuelvenExito(string command)
    {
        var exitCode = await Program.Main(new[] { command });

        Assert.Equal(0, exitCode);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("--version")]
    [InlineData("-v")]
    public async Task Main_ComandosDeVersion_DevuelvenExito(string command)
    {
        var exitCode = await Program.Main(new[] { command });

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Main_SinArgumentos_DevuelveExito()
    {
        // Sin argumentos y sin entrada interactiva deja el entorno preparado sin
        // presentar una lista de comandos como contrato de capacidad.
        var exitCode = await Program.Main(Array.Empty<string>());

        Assert.Equal(0, exitCode);
    }
}
