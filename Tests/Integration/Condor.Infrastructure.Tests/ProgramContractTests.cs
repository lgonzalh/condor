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
    public async Task Main_SinArgumentos_ReflejaPreparacionDelEntorno()
    {
        // Sin argumentos y sin entrada interactiva, el codigo de salida refleja
        // si Condor quedo operativo: 0 cuando hay un modelo utilizable, 1 cuando
        // no (reportando el motivo sin entrar al prompt silenciosamente). Esto
        // es el contrato del flujo de arranque honesto: nunca decir "listo" sin
        // capacidad operativa.
        var state = new LocalStateStore();
        var prep = await new StartupPreparer(
            new AssessmentService(),
            state,
            modelAutoSetup: new ModelAutoSetupService(state, new AssessmentService())).RunAsync();

        var exitCode = await Program.Main(Array.Empty<string>());

        Assert.True(prep.Ready ? exitCode == 0 : exitCode == 1);
        Assert.False(prep.Ready && exitCode != 0);
        Assert.False(!prep.Ready && exitCode == 0);
    }
}
