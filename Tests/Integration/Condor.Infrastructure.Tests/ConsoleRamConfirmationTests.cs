using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Condor.Cli.Presentation;

namespace Condor.Infrastructure.Tests;

/// <summary>
/// Valida el confirmador interactivo de RAM (ConsoleRamConfirmation): lee la
/// respuesta [S/N] de la consola y devuelve la decision correcta sin cerrar
/// aplicaciones por su cuenta ni entrar en bucles infinitos.
/// </summary>
public class ConsoleRamConfirmationTests
{
    [Fact]
    public async Task RespuestaSi_ConfirmaLiberarMensaje()
    {
        // Captura temporal de entrada/salida para aislar la lectura de consola.
        var originalIn = Console.In;
        var originalOut = Console.Out;
        using (var sr = new StringReader("S"))
        using (var sw = new StringWriter())
        {
            Console.SetIn(sr);
            Console.SetOut(sw);
            var confirm = new ConsoleRamConfirmation();
            var yes = await confirm.AskToReleaseRamAsync("RAM insuficiente. ¿Quieres liberar memoria? [S/N]", CancellationToken.None);
            Assert.True(yes);
            Assert.Contains("RAM insuficiente", sw.ToString());
            Assert.Contains("[S/N]", sw.ToString());
        }
        Console.SetIn(originalIn);
        Console.SetOut(originalOut);
    }

    [Fact]
    public async Task RespuestaNo_NoConfirma()
    {
        var originalIn = Console.In;
        var originalOut = Console.Out;
        using (var sr = new StringReader("N"))
        using (var sw = new StringWriter())
        {
            Console.SetIn(sr);
            Console.SetOut(sw);
            var confirm = new ConsoleRamConfirmation();
            var yes = await confirm.AskToReleaseRamAsync("prompt", CancellationToken.None);
            Assert.False(yes);
        }
        Console.SetIn(originalIn);
        Console.SetOut(originalOut);
    }

    [Fact]
    public async Task RespuestaSiEnMinuscula_Confirma()
    {
        var originalIn = Console.In;
        var originalOut = Console.Out;
        using (var sr = new StringReader("s"))
        using (var sw = new StringWriter())
        {
            Console.SetIn(sr);
            Console.SetOut(sw);
            var confirm = new ConsoleRamConfirmation();
            var yes = await confirm.AskToReleaseRamAsync("prompt", CancellationToken.None);
            Assert.True(yes);
        }
        Console.SetIn(originalIn);
        Console.SetOut(originalOut);
    }

    [Fact]
    public async Task SinRespuestaValida_TrasReintentosAcotados_Niega()
    {
        // Respuestas invalidas una y otra vez: nunca un bucle infinito; tras un
        // numero acotado se trata como negativa (salida limpia sin perder la tarea).
        var originalIn = Console.In;
        var originalOut = Console.Out;
        using (var sr = new StringReader("x\r\n?\r\nx"))
        using (var sw = new StringWriter())
        {
            Console.SetIn(sr);
            Console.SetOut(sw);
            var confirm = new ConsoleRamConfirmation();
            var yes = await confirm.AskToReleaseRamAsync("prompt", CancellationToken.None);
            Assert.False(yes);
        }
        Console.SetIn(originalIn);
        Console.SetOut(originalOut);
    }
}
