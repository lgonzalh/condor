using Condor.Cli.Tui;
using Condor.Core.Models;
using System.Text.RegularExpressions;

namespace Condor.Cli.Tests;

public class SelectListTests
{
    private static readonly TuiTheme Theme = new(
        Ansi.FgDorado,
        Ansi.FgBlanco,
        Ansi.FgGris,
        Ansi.FgGris,
        Ansi.Esc + "48;5;235m",
        Ansi.Esc + "48;5;179m",
        Ansi.Esc + "38;5;16m");

    private static SelectItem Item(
        string id,
        string label,
        string description = "",
        bool disabled = false,
        string? disabledReason = null,
        string? shortcut = null,
        bool isCurrent = false) =>
        new SelectItem(id, label, description, Array.Empty<string>(), disabled, disabledReason, shortcut)
        {
            IsCurrent = isCurrent
        };

    [Fact]
    public void FiltraPorSubsecuenciaEnNombreYDescripcionSinDistinguirAcentos()
    {
        var state = SelectList.Create(new[]
        {
            Item("models", "/models", "Cambiar modelo"),
            Item("build", "/construir", "Generar Código")
        });

        var models = state.SetQuery("mdl");
        Assert.Equal("models", models.Selected?.Id);

        var code = state.SetQuery("codigo");
        Assert.Equal("build", code.Selected?.Id);
    }

    [Fact]
    public void FuzzyMatcherPuntuaPrefijoInicioDePalabraYConsecutividad()
    {
        var prefix = FuzzyMatcher.Score("cmd", "command");
        var word = FuzzyMatcher.Score("cmd", "run command");
        var consecutive = FuzzyMatcher.Score("ab", "abc");
        var separated = FuzzyMatcher.Score("ab", "a-b");

        Assert.NotNull(prefix);
        Assert.NotNull(word);
        Assert.NotNull(consecutive);
        Assert.NotNull(separated);
        Assert.True(prefix < word);
        Assert.True(consecutive < separated);
        Assert.Null(FuzzyMatcher.Score("xyz", "command"));
    }

    [Fact]
    public void CambiarQueryConservaLaSeleccionSiSigueCoincidiendo()
    {
        var state = SelectList.Create(new[]
        {
            new SelectItem("first", "Beta primero", "", Array.Empty<string>(), false, null, null),
            new SelectItem("second", "Beta segundo", "", Array.Empty<string>(), false, null, null)
        }, selectedId: "second");

        state = state.SetQuery("beta");

        Assert.Equal("second", state.Selected?.Id);
    }

    [Fact]
    public void NavegacionEnvuelveOmiteDeshabilitadosYMantieneLaSeleccionVisible()
    {
        var items = Enumerable.Range(0, 30)
            .Select(index => Item(
                $"item-{index}",
                $"Elemento {index}",
                disabled: index == 1,
                disabledReason: index == 1 ? "presupuesto no disponible" : null))
            .ToArray();

        Assert.Equal(8, SelectList.Create(items).VisibleRows);
        var state = SelectList.Create(items, visibleRows: 8, selectedId: "item-0");
        state = state.MoveNext();
        Assert.Equal("item-2", state.Selected?.Id);
        Assert.Equal(0, state.ScrollOffset);

        state = SelectList.Create(items, visibleRows: 8, selectedId: "item-29");
        Assert.Equal("item-29", state.Selected?.Id);
        Assert.Equal(22, state.ScrollOffset);

        state = state.MoveNext();
        Assert.Equal("item-0", state.Selected?.Id);
        Assert.Equal(0, state.ScrollOffset);

        state = state.MovePrev();
        Assert.Equal("item-29", state.Selected?.Id);
        Assert.Equal(22, state.ScrollOffset);
    }

    [Fact]
    public void OrdenaPorPuntajeYConservaElOrdenOriginalEnEmpates()
    {
        var items = new[]
        {
            new SelectItem("separated", "a-b", "", Array.Empty<string>(), false, null, null),
            new SelectItem("first", "ab", "", Array.Empty<string>(), false, null, null),
            new SelectItem("second", "ab", "", Array.Empty<string>(), false, null, null)
        };

        var filtered = SelectList.Filter(items, "ab");

        Assert.Equal(new[] { 1, 2, 0 }, filtered);
    }

    [Fact]
    public void RenderizadorMantieneElAnchoCompletoYMuestraBarraConTreintaElementos()
    {
        var items = Enumerable.Range(0, 30)
            .Select(index => Item($"item-{index}", $"Elemento {index}", "Agente · Herramientas"))
            .ToArray();
        var state = SelectList.Create(items, visibleRows: 9, selectedId: "item-15");

        var lines = SelectListRenderer.Render(state, 52, new SelectListRenderOptions(), Theme);

        Assert.Equal(9, lines.Count);
        Assert.All(lines, line => Assert.Equal(52, Ansi.VisibleWidth(line)));
        Assert.Equal(1, lines.Count(line => line.Contains(Theme.SelectionBackground, StringComparison.Ordinal)));
        Assert.All(lines, line => Assert.Contains(
            Ansi.StripSgr(line).Substring(Ansi.StripSgr(line).Length - 1),
            new[] { "│", "█" }));
    }

    [Fact]
    public void ListaVaciaRenderizaUnaFilaDeSinResultados()
    {
        var state = SelectList.Create(Array.Empty<SelectItem>(), "inexistente");

        var lines = SelectListRenderer.Render(state, 32, new SelectListRenderOptions(), Theme);

        Assert.Single(lines);
        Assert.Contains("Sin resultados", Ansi.StripSgr(lines[0]));
        Assert.Contains(Theme.Muted, lines[0], StringComparison.Ordinal);
        Assert.Equal(32, Ansi.VisibleWidth(lines[0]));
    }

    [Fact]
    public void ModalDeComandosAlineaAtajosYRespetaElAnchoVisibleUnicode()
    {
        var state = SelectList.Create(new[]
        {
            Item("/models", "/models", "Cambiar modelo", shortcut: "Enter")
        });

        var lines = SelectListRenderer.Render(
            state,
            48,
            new SelectListRenderOptions(Title: "Comandos", IncludeSearchField: true, ShowShortcuts: true),
            Theme);

        Assert.Equal(3, lines.Count);
        Assert.All(lines, line => Assert.Equal(48, Ansi.VisibleWidth(line)));
        Assert.EndsWith("Enter", Ansi.StripSgr(lines[^1]));
        Assert.Equal(6, Ansi.VisibleWidth("A界🙂e\u0301"));
        Assert.Equal("A界", Ansi.ClipVisible("A界🙂Z", 4));
    }

    [Fact]
    public void RendererProduceSnapshotDeDosColumnasEnTextoPlano()
    {
        var state = SelectList.Create(new[]
        {
            new SelectItem("alpha", "alpha", "Descriptor", Array.Empty<string>(), false, null, null)
        }, selectedId: "alpha");
        var theme = TuiTheme.Create(colorEnabled: false, supportsTrueColor: false);

        var line = SelectListRenderer.Render(state, 28, theme).Single();

        Assert.Equal("alpha    Descriptor" + new string(' ', 9), Ansi.StripSgr(line));
        Assert.Equal(28, Ansi.VisibleWidth(line));
    }

    [Fact]
    public void RendererProduceSnapshotCrudoConAcentoYColoresConfigurables()
    {
        var state = SelectList.Create(new[]
        {
            new SelectItem("alpha", "alpha", "Descriptor", Array.Empty<string>(), false, null, null)
        }, selectedId: "alpha");
        var theme = TuiTheme.Create(colorEnabled: true, supportsTrueColor: false);
        var trueColorTheme = TuiTheme.Create(colorEnabled: true, supportsTrueColor: true);

        var line = SelectListRenderer.Render(state, 28, theme).Single();

        Assert.StartsWith(theme.FondoSeleccion + theme.TextoSeleccion, line);
        Assert.EndsWith(Ansi.Reset, line);
        Assert.Equal(28, Ansi.VisibleWidth(line));
        Assert.StartsWith(Ansi.Esc + "38;5;", theme.Acento);
        Assert.StartsWith(Ansi.Esc + "38;2;", trueColorTheme.Acento);
        Assert.Equal("", TuiTheme.Create(colorEnabled: false, supportsTrueColor: false).Acento);
    }

    [Fact]
    public void SlashAbreMenuFiltraYTabCompletaLaSeleccion()
    {
        using var host = new TuiHost(forceInteractive: true);
        var input = new TuiInput(host);
        input.SetText("/");

        input.Handle(new ConsoleKeyInfo('m', ConsoleKey.M, false, false, false));
        input.Handle(new ConsoleKeyInfo('o', ConsoleKey.O, false, false, false));
        input.Handle(new ConsoleKeyInfo('d', ConsoleKey.D, false, false, false));
        input.Handle(new ConsoleKeyInfo('e', ConsoleKey.E, false, false, false));
        input.Handle(new ConsoleKeyInfo('l', ConsoleKey.L, false, false, false));
        input.Handle(new ConsoleKeyInfo('o', ConsoleKey.O, false, false, false));
        input.Handle(new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false));

        Assert.Equal("/modelo ", input.Buffer);
        Assert.Equal(InputAction.Submit,
            input.Handle(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false)));
    }

    [Fact]
    public void SlashYModelosSeAnclanEncimaDeLaEntradaYCtrlPApareceCentrado()
    {
        using var host = new TuiHost(120, 40);
        host.Enter();
        host.ShowSession("modelo-a");
        var input = new TuiInput(host);

        input.SetText("/");
        var slashPosition = PositionOf(host.SnapshotFullFrame(), "/agentes");
        Assert.Equal(1, slashPosition.Column);
        Assert.InRange(slashPosition.Row, 7, 37);

        input.BeginModelPicker(
            new[] { new ModelInfo { Name = "modelo-a" }, new ModelInfo { Name = "modelo-b" } },
            activeModel: "modelo-a");
        Assert.Equal((35, 1), PositionOf(host.SnapshotFullFrame(), "Modelo local"));

        input.BeginCommandPalette(new[] { "/models" });
        Assert.Equal((21, 16), PositionOf(host.SnapshotFullFrame(), "Comandos"));
    }

    [Fact]
    public void SlashSeCierraConEscapeOBorrarElPrefijo()
    {
        using var host = new TuiHost(forceInteractive: true);
        var input = new TuiInput(host);

        input.SetText("/");
        Assert.Equal(InputAction.None,
            input.Handle(new ConsoleKeyInfo((char)0, ConsoleKey.Escape, false, false, false)));
        Assert.Equal("/", input.Buffer);
        Assert.Equal(InputAction.OpenAgents,
            input.Handle(new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false)));

        input.SetText("/");
        input.Handle(new ConsoleKeyInfo('\b', ConsoleKey.Backspace, false, false, false));
        Assert.Equal("", input.Buffer);
        Assert.Equal(InputAction.OpenAgents,
            input.Handle(new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false)));
    }

    [Fact]
    public void SlashNoAbreElMenuCuandoLaEntradaYaTieneUnEspacio()
    {
        using var host = new TuiHost(110, 34);
        host.Enter();
        host.ShowSession("modelo-a");
        var input = new TuiInput(host);

        input.SetText("/models ");

        Assert.DoesNotContain("/agentes", Ansi.StripSgr(host.SnapshotFullFrame()));
        Assert.Equal("/models ", input.Buffer);
    }

    [Fact]
    public void PaletaFiltraPorSubsecuenciaYConservaAtajosEnLasFilas()
    {
        using var host = new TuiHost(110, 34);
        host.Enter();
        host.ShowSession("modelo-a");
        var input = new TuiInput(host);
        input.BeginCommandPalette(new[] { "/analizar", "/verificar", "/recomendar" });

        var initialFrame = Ansi.StripSgr(host.SnapshotFullFrame());
        Assert.Contains("Enter", initialFrame);

        input.Handle(new ConsoleKeyInfo('v', ConsoleKey.V, false, false, false));
        input.Handle(new ConsoleKeyInfo('f', ConsoleKey.F, false, false, false));
        input.Handle(new ConsoleKeyInfo('r', ConsoleKey.R, false, false, false));

        Assert.Equal("/verificar", input.SelectedCommand);
        Assert.Equal(InputAction.Submit,
            input.Handle(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false)));
        Assert.Equal("/verificar", input.Buffer);
    }

    [Fact]
    public void RedimensionarConPaletaAbiertaRecalculaAnchoYActualizaLasFilas()
    {
        using var host = new TuiHost(110, 34);
        host.Enter();
        host.ShowSession("modelo-a");
        var input = new TuiInput(host);
        var commands = Enumerable.Range(0, 30).Select(index => $"/cmd-{index:00}").ToArray();
        input.BeginCommandPalette(commands);

        var initialFrame = host.SnapshotFullFrame();
        Assert.Equal(11, PositionOf(initialFrame, "Comandos").Column);

        foreach (var character in "cmd23")
        {
            input.Handle(new ConsoleKeyInfo(character, ConsoleKey.NoName, false, false, false));
        }

        Assert.Equal("/cmd-23", input.SelectedCommand);
        Assert.True(host.ResizeTo(80, 24));
        input.Render();
        host.Repaint();

        var resizedFrame = host.SnapshotFullFrame();
        var plainFrame = Ansi.StripSgr(resizedFrame);
        Assert.Equal(1, PositionOf(resizedFrame, "Comandos").Column);
        Assert.Contains("Buscar: cmd23", plainFrame);
        Assert.Contains("/cmd-23", plainFrame);
        Assert.DoesNotContain("/cmd-00", plainFrame);
    }

    [Fact]
    public void ModeloActualSeDistingueEnNegritaSinIcono()
    {
        var state = SelectList.Create(new[]
        {
            Item("current", "modelo-local", isCurrent: true)
        }, selectedId: "current");

        var line = SelectListRenderer.Render(state, 40, new SelectListRenderOptions(), Theme).Single();

        Assert.Contains(Ansi.Bold, line);
        Assert.DoesNotContain("▶", Ansi.StripSgr(line));
        Assert.DoesNotContain("▸", Ansi.StripSgr(line));
    }

    private static (int Row, int Column) PositionOf(string frame, string text)
    {
        var index = frame.IndexOf(text, StringComparison.Ordinal);
        Assert.True(index >= 0, $"No se encontro el texto '{text}' en el fotograma.");
        var matches = Regex.Matches(frame[..index], "\\x1b\\[(\\d+);(\\d+)H");
        Assert.NotEmpty(matches);
        var last = matches[matches.Count - 1];
        return (int.Parse(last.Groups[1].Value), int.Parse(last.Groups[2].Value));
    }
}
