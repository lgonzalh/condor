using Condor.Cli.Tui;

namespace Condor.Cli.Tests;

public sealed class TuiPresentationChangeTests
{
    [Fact]
    public void Bienvenida_AnimacionYTextoQuedanEnLaMismaLinea()
    {
        using var host = new TuiHost(forceInteractive: true);
        host.Enter();
        host.ShowWelcome();
        host.SetEstado("Preparando dependencias");
        host.SetProgreso("Verificando RAM y modelos locales");
        host.SetModelStatus("Ollama · Verificando RAM y modelos locales", working: true);
        host.SetBusy(true);

        var frame = host.SnapshotFullFrame();

        var rowStart = frame.IndexOf(Ansi.At(18, 3), StringComparison.Ordinal);
        var nextRow = frame.IndexOf(Ansi.At(19, 3), rowStart + 1, StringComparison.Ordinal);
        Assert.True(rowStart >= 0 && nextRow > rowStart);
        var statusRow = Ansi.StripSgr(frame[rowStart..nextRow]);
        Assert.Contains("Condor · Preparando dependencias", statusRow);
        Assert.Contains("Ollama · Verificando RAM", statusRow);
        Assert.DoesNotContain("Iniciando ...", frame);
        Assert.Contains("©Condor 2026", frame);
    }

    [Fact]
    public void Trabajo_CombinaCondorYModeloEnUnSoloEstadoConAtajos()
    {
        using var host = new TuiHost(forceInteractive: true);
        host.Enter();
        host.ShowSession("qwen2.5-coder:3b");
        host.SetEstado("Construyendo index.html");
        host.SetProgreso("Iteración 1");
        host.SetModelStatus("qwen2.5-coder:3b · creando archivo", working: true);
        host.SetBusy(true);

        var frame = Ansi.StripSgr(host.SnapshotFullFrame());

        Assert.Contains("Construyendo index.html", frame);
        Assert.Contains("RAM Condor", frame);
        Assert.DoesNotContain("qwen2.5-coder:3b · creando archivo", frame);
        Assert.DoesNotContain("Iteración 1", frame);
        Assert.Contains("tab agentes  ctrl+p comandos", frame);
    }

    [Fact]
    public void Trabajo_ContieneCopyrightYNoUsaEtiquetasDeSeccion()
    {
        using var host = new TuiHost(forceInteractive: true);
        host.Enter();
        host.ShowSession("qwen2.5-coder:0.5b");
        host.AddActivity("analisis", ActivityKind.Analysis);
        host.AddActivity("edicion", ActivityKind.CodeEdit);
        host.AddActivity("respuesta", ActivityKind.Response);
        host.AddActivity("pregunta", ActivityKind.Question);

        var frame = Ansi.StripSgr(host.SnapshotFullFrame());

        Assert.Contains("©Condor 2026", frame);
        Assert.DoesNotContain("a) analisis", frame);
        Assert.DoesNotContain("b) edicion", frame);
        Assert.DoesNotContain("c) respuesta", frame);
        Assert.DoesNotContain("d) pregunta", frame);
        Assert.Contains("analisis", frame);
        Assert.Contains("edicion", frame);
        Assert.Contains("respuesta", frame);
        Assert.Contains("pregunta", frame);
    }

    [Fact]
    public void Salida_MuestraWordmarkUnicodeCondor()
    {
        Assert.Equal(6, CondorArt.ExitWordmark.Length);
        Assert.All(CondorArt.ExitWordmark, row => Assert.Equal(62, Ansi.VisibleWidth(row)));
        Assert.All(CondorArt.ExitWordmark.Take(2), row => Assert.Equal(2, row.Count(pixel => pixel == '█')));
        Assert.All(CondorArt.ExitWordmark.Skip(2), row => Assert.Contains("█", row));
    }
}
