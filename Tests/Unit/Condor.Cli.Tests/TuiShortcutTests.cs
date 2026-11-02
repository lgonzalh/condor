using Condor.Cli.Tui;
using Condor.Core.Models;

namespace Condor.Cli.Tests;

public class TuiShortcutTests
{
    [Fact]
    public void TabAbreAgentesAunqueLaEntradaTengaTexto()
    {
        using var host = new TuiHost(forceInteractive: true);
        var input = new TuiInput(host);
        input.SetText("crear una página");

        var action = input.Handle(new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false));

        Assert.Equal(InputAction.OpenAgents, action);
        Assert.Equal("crear una página", input.Buffer);
    }

    [Fact]
    public void ControlP_AbrePaletaYPermiteEjecutarUnComando()
    {
        using var host = new TuiHost(forceInteractive: true);
        var input = new TuiInput(host);

        Assert.Equal(InputAction.OpenCommands,
            input.Handle(new ConsoleKeyInfo('\0', ConsoleKey.P, false, false, true)));
        input.BeginCommandPalette();
        var first = input.Buffer;

        input.Handle(new ConsoleKeyInfo((char)0, ConsoleKey.DownArrow, false, false, false));

        Assert.NotEqual(first, input.Buffer);
        Assert.Equal(InputAction.Submit,
            input.Handle(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false)));
    }

    [Fact]
    public void TabDesdeLaPaletaCierraLaPaletaYAbreAgentes()
    {
        using var host = new TuiHost(forceInteractive: true);
        var input = new TuiInput(host);
        input.BeginCommandPalette();

        var action = input.Handle(new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false));

        Assert.Equal(InputAction.OpenAgents, action);
        Assert.Equal("", input.Buffer);
        Assert.Equal(InputAction.None,
            input.Handle(new ConsoleKeyInfo((char)0, ConsoleKey.DownArrow, false, false, false)));
    }

    [Fact]
    public void SelectorDeModelos_PermiteCambiarYConfirmarModelo()
    {
        using var host = new TuiHost(forceInteractive: true);
        var input = new TuiInput(host);
        input.BeginModelPicker(new[]
        {
            new ModelInfo { Name = "modelo-a" },
            new ModelInfo { Name = "modelo-b" }
        });

        var initial = input.SelectedModel;
        input.Handle(new ConsoleKeyInfo((char)0, ConsoleKey.DownArrow, false, false, false));

        Assert.NotEqual(initial, input.SelectedModel);
        Assert.Equal(InputAction.ModelSelected,
            input.Handle(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false)));
        Assert.Equal("modelo-b", input.SelectedModel);
    }

    [Fact]
    public void CapacidadesSeMuestranConEtiquetasBreves()
    {
        var labels = CondorTui.DescribeModelCapabilities(new ModelInfo
        {
            Name = "qwen2.5-coder:3b",
            SizeBytes = 1_900_000_000,
            Capabilities = new List<string> { "completion", "tools", "thinking", "vision" }
        });

        Assert.Contains("Agente", labels);
        Assert.Contains("Herramientas", labels);
        Assert.Contains("Código", labels);
        Assert.Contains("Visión", labels);
        Assert.DoesNotContain("Rápido", labels);
    }

    [Fact]
    public void VersionVisible_IdentificaBuildGamma()
    {
        Assert.Equal("v1.0.0-gamma (Build Gamma)", Condor.Cli.VersionInfo.DisplayName);
    }
}
