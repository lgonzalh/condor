using Condor.Cli;
using Condor.Cli.Routing;
using Condor.Infrastructure.Setup;
using Condor.Infrastructure.State;

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
    public async Task Main_SinTuiCompatible_NoIniciaLaConsolaClasica()
    {
        var exitCode = await Program.Main(Array.Empty<string>());

        Assert.Equal(1, exitCode);
    }

    [Theory]
    [InlineData("hola")]
    [InlineData("/consultar hola")]
    [InlineData("/examinar imagen.png")]
    public async Task Main_InteraccionConModeloSinTui_NoLaEjecutaEnConsola(string request)
    {
        var exitCode = await Program.Main(new[] { request });

        Assert.Equal(1, exitCode);
    }
}
