using Condor.Cli.Tui;
using Condor.Core.Models;
using System.Text.RegularExpressions;

namespace Condor.Cli.Tests;

public class TuiMouseInputTests
{
    [Fact]
    public void SgrParser_LeeClicYRuedaConCoordenadasDeTerminal()
    {
        Assert.True(TuiConsoleInputReader.TryParseSgrMouse("\u001b[<0;12;8M", out var click));
        Assert.Equal(new TuiMouseEvent(TuiMouseEventKind.LeftClick, 8, 12), click);

        Assert.True(TuiConsoleInputReader.TryParseSgrMouse("\u001b[<64;12;8M", out var up));
        Assert.Equal(new TuiMouseEvent(TuiMouseEventKind.Wheel, 8, 12, 1), up);

        Assert.True(TuiConsoleInputReader.TryParseSgrMouse("\u001b[<65;12;8M", out var down));
        Assert.Equal(new TuiMouseEvent(TuiMouseEventKind.Wheel, 8, 12, -1), down);
        Assert.False(TuiConsoleInputReader.TryParseSgrMouse("\u001b[<0;12;8m", out _));
    }

    [Fact]
    public void ClicEjecutaLaFilaEnSlashModeloModosYPaleta()
    {
        using var host = new TuiHost(110, 34);
        host.Enter();
        host.ShowSession("modelo-a");
        var input = new TuiInput(host);

        input.SetText("/");
        var slashPosition = PositionOf(host, host.SnapshotFullFrame(), "/agentes");
        Assert.True(host.TryGetSelectListItemIndexAt(slashPosition.Row, slashPosition.Column, out _),
            $"No se detecto fila clicable en {slashPosition.Row},{slashPosition.Column}.");
        Assert.Equal(InputAction.Submit, input.HandleMouse(Click(slashPosition)));
        Assert.Equal("/agentes", input.Buffer);

        input.SetText("tarea de prueba");
        input.BeginModelPicker(new[]
        {
            new ModelInfo { Name = "modelo-a" },
            new ModelInfo { Name = "modelo-b" }
        }, activeModel: "modelo-a");
        var modelPosition = PositionOf(host, host.SnapshotFullFrame(), "modelo-b");
        Assert.Equal(InputAction.ModelSelected, input.HandleMouse(Click(modelPosition)));
        Assert.Equal("modelo-b", input.SelectedModel);

        input.SetText("tarea de prueba");
        input.BeginAgentPicker(new[]
        {
            new SelectItem("codificar", "Codificar", "", Array.Empty<string>(), false, null, null),
            new SelectItem("plan", "Plan", "", Array.Empty<string>(), false, null, null)
        }, activeAgentId: "codificar");
        var modePosition = PositionOf(host, host.SnapshotFullFrame(), "Plan");
        Assert.Equal(InputAction.AgentSelected, input.HandleMouse(Click(modePosition)));
        Assert.Equal("plan", input.SelectedAgentId);

        input.BeginCommandPalette(new[] { "/analizar", "/avanzar" });
        var commandPosition = PositionOf(host, host.SnapshotFullFrame(), "/avanzar");
        Assert.Equal(InputAction.Submit, input.HandleMouse(Click(commandPosition)));
        Assert.Equal("/avanzar", input.Buffer);
    }

    [Fact]
    public void RuedaRecorreOpcionesYClicEnDeshabilitadoNoConfirma()
    {
        using var host = new TuiHost(110, 34);
        host.Enter();
        host.ShowSession("modelo-b");
        var input = new TuiInput(host);
        input.BeginModelPicker(new[]
        {
            new ModelInfo { Name = "modelo-a" },
            new ModelInfo { Name = "modelo-b" },
            new ModelInfo { Name = "modelo-c" }
        }, isSelectable: model => model.Name != "modelo-a", activeModel: "modelo-b");

        var disabledPosition = PositionOf(host, host.SnapshotFullFrame(), "modelo-a");
        Assert.True(host.TryGetSelectListItemIndexAt(disabledPosition.Row, disabledPosition.Column, out _),
            $"No se detecto fila deshabilitada en {disabledPosition.Row},{disabledPosition.Column}.");
        Assert.Equal(InputAction.None, input.HandleMouse(Click(disabledPosition)));
        Assert.Equal("modelo-b", input.SelectedModel);

        var enabledPosition = PositionOf(host, host.SnapshotFullFrame(), "modelo-b");
        Assert.True(host.TryGetSelectListItemIndexAt(enabledPosition.Row, enabledPosition.Column, out _),
            $"No se detecto fila para la rueda en {enabledPosition.Row},{enabledPosition.Column}.");
        var wheel = new TuiMouseEvent(TuiMouseEventKind.Wheel, enabledPosition.Row, enabledPosition.Column, -1);
        Assert.Equal(InputAction.None, input.HandleMouse(wheel));
        Assert.Equal("modelo-c", input.SelectedModel);
    }

    [Fact]
    public void ClicEnLaEntradaPosicionaElCursorYPermiteEditar()
    {
        using var host = new TuiHost(110, 34);
        host.Enter();
        host.ShowSession("modelo-a");
        var input = new TuiInput(host);
        input.SetText("Condor");

        Assert.Equal(InputAction.None,
            input.HandleMouse(new TuiMouseEvent(TuiMouseEventKind.LeftClick, host.InputRow + 1, 8)));
        input.Handle(new ConsoleKeyInfo('\b', ConsoleKey.Backspace, false, false, false));

        Assert.Equal("Cndor", input.Buffer);
    }

    private static TuiMouseEvent Click((int Row, int Column) position) =>
        new(TuiMouseEventKind.LeftClick, position.Row, position.Column);

    private static (int Row, int Column) PositionOf(TuiHost host, string frame, string text)
    {
        var index = frame.IndexOf(text, StringComparison.Ordinal);
        while (index >= 0)
        {
            var positions = Regex.Matches(frame[..index], "\\x1b\\[(\\d+);(\\d+)H");
            if (positions.Count > 0)
            {
                var last = positions[positions.Count - 1];
                var position = (int.Parse(last.Groups[1].Value), int.Parse(last.Groups[2].Value));
                if (host.TryGetSelectListItemIndexAt(position.Item1, position.Item2, out _))
                {
                    return position;
                }
            }

            index = frame.IndexOf(text, index + text.Length, StringComparison.Ordinal);
        }

        throw new Xunit.Sdk.XunitException($"No se encontro '{text}' en una fila del menu TUI.");
    }
}
