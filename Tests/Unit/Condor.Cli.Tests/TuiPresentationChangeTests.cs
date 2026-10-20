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
        host.SetBusy(true);

        var frame = Ansi.StripSgr(host.SnapshotFullFrame());

        Assert.Contains("Iniciando ...", frame);
        Assert.DoesNotContain("_Iniciando", frame);
        Assert.Contains("©Condor 2026", frame);
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
}
