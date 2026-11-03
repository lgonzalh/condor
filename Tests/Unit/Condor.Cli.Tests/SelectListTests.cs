using Condor.Cli.Tui;
using Condor.Core.Models;
using System.Text.RegularExpressions;

namespace Condor.Cli.Tests;

public class SelectListTests
{
    private static readonly SelectListTheme Theme = new(
        Ansi.FgDorado,
        Ansi.FgBlanco,
        Ansi.FgGris,
        Ansi.FgGris,
        Ansi.Esc + "48;5;235m",
        Ansi.Esc + "48;5;179m",
        Ansi.Esc + "38;5;16m");

    [Fact]
    public void FiltraPorSubsecuenciaEnNombreYDescripcionSinDistinguirAcentos()
    {
        var state = SelectList.Create(new[]
        {
            new SelectListItem("models", "/models", "Cambiar modelo"),
            new SelectListItem("build", "/construir", "Generar Código")
        });

        var models = SelectList.SetQuery(state, "mdl");
        Assert.Equal("models", models.SelectedItem?.Id);

        var code = SelectList.SetQuery(state, "codigo");
        Assert.Equal("build", code.SelectedItem?.Id);
    }

    [Fact]
    public void NavegacionEnvuelveOmiteDeshabilitadosYMantieneLaSeleccionVisible()
    {
        var items = Enumerable.Range(0, 30)
            .Select(index => new SelectListItem(
                $"item-{index}",
                $"Elemento {index}",
                Disabled: index == 1,
                DisabledReason: index == 1 ? "presupuesto no disponible" : null))
            .ToArray();

        var state = SelectList.Create(items, visibleRows: 8, selectedId: "item-0");
        state = SelectList.Move(state, 1);
        Assert.Equal("item-2", state.SelectedItem?.Id);
        Assert.Equal(0, state.ScrollOffset);

        state = SelectList.MoveTo(state, 29);
        Assert.Equal("item-29", state.SelectedItem?.Id);
        Assert.Equal(22, state.ScrollOffset);

        state = SelectList.Move(state, 1);
        Assert.Equal("item-0", state.SelectedItem?.Id);
        Assert.Equal(0, state.ScrollOffset);
    }

    [Fact]
    public void RenderizadorMantieneElAnchoCompletoYMuestraBarraConTreintaElementos()
    {
        var items = Enumerable.Range(0, 30)
            .Select(index => new SelectListItem($"item-{index}", $"Elemento {index}", "Agente · Herramientas"))
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
        var state = SelectList.Create(Array.Empty<SelectListItem>(), "inexistente");

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
            new SelectListItem("/models", "/models", "Cambiar modelo", Shortcut: "Enter")
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
        input.Handle(new ConsoleKeyInfo('s', ConsoleKey.S, false, false, false));
        input.Handle(new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false));

        Assert.Equal("/models ", input.Buffer);
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
        Assert.Equal((34, 1), PositionOf(host.SnapshotFullFrame(), "Modelos locales"));

        input.BeginCommandPalette(new[] { "/models" });
        Assert.Equal((20, 16), PositionOf(host.SnapshotFullFrame(), "Comandos"));
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
    public void ModeloActualSeDistingueEnNegritaSinIcono()
    {
        var state = SelectList.Create(new[]
        {
            new SelectListItem("current", "modelo-local", IsCurrent: true)
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
