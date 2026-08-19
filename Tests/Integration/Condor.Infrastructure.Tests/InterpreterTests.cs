using Condor.Cli.Routing;

namespace Condor.Infrastructure.Tests;

public class InterpreterTests
{
    [Fact]
    public async Task RunAsync_IntencionLibre_LlegaAlMotorAgente()
    {
        var linea = new Queue<string>(new[]
        {
            "revisa por que no compila este proyecto",
            "/salir"
        });

        var recibido = new List<object>();
        var interpreter = new Interpreter(
            line => { recibido.Add(line); return Task.FromResult(0); },
            line => { recibido.Add(line); return Task.FromResult(0); },
            () => linea.Count > 0 ? linea.Dequeue() : null);

        var exit = await interpreter.RunAsync();

        Assert.Equal(0, exit);
        var free = Assert.IsType<FreeIntentionRoute>(recibido.Single());
        Assert.Contains("no compila", free.Intention);
    }

    [Fact]
    public async Task RunAsync_SlashCommand_LlegaAlManejadorDeSlash()
    {
        var linea = new Queue<string>(new[]
        {
            "/analizar",
            "/salir"
        });

        var recibido = new List<object>();
        var interpreter = new Interpreter(
            line => { recibido.Add(line); return Task.FromResult(0); },
            line => { recibido.Add(line); return Task.FromResult(0); },
            () => linea.Count > 0 ? linea.Dequeue() : null);

        await interpreter.RunAsync();

        var slash = Assert.IsType<SlashRoute>(recibido.Single());
        Assert.Equal(SlashCommandKind.Analizar, slash.Kind);
    }

    [Fact]
    public async Task RunAsync_MezclaComandosEIntenciones_SeparaCorrectamente()
    {
        var linea = new Queue<string>(new[]
        {
            "/contexto",
            "continuar el desarrollo de la aplicacion",
            "/ayuda",
            "/salir"
        });

        var recibido = new List<object>();
        var interpreter = new Interpreter(
            line => { recibido.Add(line); return Task.FromResult(0); },
            line => { recibido.Add(line); return Task.FromResult(0); },
            () => linea.Count > 0 ? linea.Dequeue() : null);

        await interpreter.RunAsync();

        Assert.IsType<SlashRoute>(recibido[0]);
        Assert.IsType<FreeIntentionRoute>(recibido[1]);
        Assert.IsType<SlashRoute>(recibido[2]);
        Assert.Equal(SlashCommandKind.Contexto, ((SlashRoute)recibido[0]).Kind);
        Assert.Equal(SlashCommandKind.Ayuda, ((SlashRoute)recibido[2]).Kind);
    }

    [Fact]
    public async Task RunAsync_LineasVaciasYComentarios_SeIgnoran()
    {
        var linea = new Queue<string>(new[]
        {
            "",
            "# un comentario",
            "/salir"
        });

        var recibido = 0;
        var interpreter = new Interpreter(
            line => { recibido++; return Task.FromResult(0); },
            line => { recibido++; return Task.FromResult(0); },
            () => linea.Count > 0 ? linea.Dequeue() : null);

        await interpreter.RunAsync();

        Assert.Equal(0, recibido);
    }

    [Fact]
    public async Task RunAsync_FinDeEntrada_TerminaConExito()
    {
        var interpreter = new Interpreter(
            _ => Task.FromResult(0),
            _ => Task.FromResult(0),
            () => null);

        var exit = await interpreter.RunAsync();

        Assert.Equal(0, exit);
    }
}
