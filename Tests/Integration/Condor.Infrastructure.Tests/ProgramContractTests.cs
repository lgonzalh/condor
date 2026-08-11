using Condor.Cli;

namespace Condor.Infrastructure.Tests;

public class ProgramContractTests
{
    [Theory]
    [InlineData("ayuda")]
    [InlineData("--help")]
    [InlineData("-h")]
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

    [Theory]
    [InlineData("assess")]
    [InlineData("ask")]
    [InlineData("recommend")]
    [InlineData("help")]
    public async Task Main_ComandosInglesesPrevios_DejanDeSerContratoValido(string command)
    {
        var exitCode = await Program.Main(new[] { command });

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task Main_SinArgumentos_DevuelveExito()
    {
        var exitCode = await Program.Main(Array.Empty<string>());

        Assert.Equal(0, exitCode);
    }
}
