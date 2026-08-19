using Condor.Cli.Routing;

namespace Condor.Infrastructure.Tests;

public class IntentionRouterTests
{
    // ------------------------------------------------------------------ Slash

    [Theory]
    [InlineData("/analizar")]
    [InlineData("/contexto")]
    [InlineData("/planear")]
    [InlineData("/construir")]
    [InlineData("/verificar")]
    [InlineData("/examinar")]
    [InlineData("/recomendar")]
    [InlineData("/ayuda")]
    [InlineData("/consultar")]
    [InlineData("/verificar-semantico")]
    [InlineData("/preparar")]
    [InlineData("/avanzar")]
    public void Route_SlashConocido_EsSlashRoute(string input)
    {
        var route = IntentionRouter.Route(input);

        Assert.IsType<SlashRoute>(route);
    }

    [Fact]
    public void Route_Analizar_MapeaAlComandoCorrecto()
    {
        Assert.IsType<SlashRoute>(IntentionRouter.Route("/analizar"));
        Assert.Equal(
            SlashCommandKind.Analizar,
            (IntentionRouter.Route("/analizar") as SlashRoute)!.Kind);
    }

    // ------------------------------------------------------ Intencion libre

    [Theory]
    [InlineData("revisa este directorio y ayudame a solucionar el error")]
    [InlineData("continua el desarrollo de esta aplicacion")]
    [InlineData("crea una pagina web sencilla para este proyecto")]
    [InlineData("revisa por que no compila")]
    [InlineData("analizar")] // sin "/" no es comando; es intencion natural
    [InlineData("hacer algo nuevo")]
    public void Route_TextoNaturalSinSlash_EsFreeIntention(string input)
    {
        var route = IntentionRouter.Route(input);

        var free = Assert.IsType<FreeIntentionRoute>(route);
        Assert.Equal(input, free.Intention);
    }

    [Fact]
    public void Route_TextoNaturalConservaLaIntencionCompleta()
    {
        var route = IntentionRouter.Route("revisa este directorio y ayudame a solucionar el error");

        var free = Assert.IsType<FreeIntentionRoute>(route);
        Assert.Contains("revisa este directorio", free.Intention);
        Assert.Contains("solucionar el error", free.Intention);
    }

    [Fact]
    public void Route_NoSeRespondeComandoDesconocido()
    {
        // Cualquier frase que no inicia con "/" es una intencion valida y jamas
        // producira un resultado de "desconocido".
        var route = IntentionRouter.Route("esto no es parte de una lista rigida");

        Assert.IsType<FreeIntentionRoute>(route);
    }

    [Fact]
    public void Route_FraseQueNoIniciaConSlash_NuncaEsDesconocida()
    {
        // Un token que no corresponde a ningun comando de control tampoco se
        // descarta como desconocido: se interpreta como intencion natural.
        var route = IntentionRouter.Route("haz x para el proyecto");

        Assert.IsType<FreeIntentionRoute>(route);
    }

    [Fact]
    public void Route_SlashConArgumentos_DescomponeArgumentos()
    {
        var route = IntentionRouter.Route("/planear \"corregir el error de compilacion\"");

        var slash = Assert.IsType<SlashRoute>(route);
        Assert.Equal(SlashCommandKind.Planear, slash.Kind);
        Assert.Equal(new[] { "corregir el error de compilacion" }, slash.Arguments);
    }

    [Fact]
    public void Route_Vacio_EsIntencionLibreVacia()
    {
        var route = IntentionRouter.Route("   ");

        var free = Assert.IsType<FreeIntentionRoute>(route);
        Assert.Equal("", free.Intention);
    }
}
