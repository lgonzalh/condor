using Condor.Cli.Tui;
using Condor.Cli.Routing;
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
        var first = input.SelectedCommand;

        input.Handle(new ConsoleKeyInfo((char)0, ConsoleKey.DownArrow, false, false, false));

        Assert.NotEqual(first, input.SelectedCommand);
        Assert.Equal(InputAction.Submit,
            input.Handle(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false)));
        Assert.Equal("/analizar", input.Buffer);
    }

    [Fact]
    public void CtrlP_MuestraComandosFiltrablesYModelsEjecutaElSelectorLocal()
    {
        using var host = new TuiHost(forceInteractive: true);
        host.Enter();
        host.ShowSession("granite4.2:3b");
        var input = new TuiInput(host);
        input.BeginCommandPalette();

        var initialFrame = Ansi.StripSgr(host.SnapshotFullFrame());
        Assert.Contains("Comandos", initialFrame);
        Assert.Contains("/agentes", initialFrame);

        foreach (var character in "models")
        {
            var key = character switch
            {
                'm' => ConsoleKey.M,
                'o' => ConsoleKey.O,
                'd' => ConsoleKey.D,
                'e' => ConsoleKey.E,
                'l' => ConsoleKey.L,
                's' => ConsoleKey.S,
                _ => ConsoleKey.NoName
            };
            input.Handle(new ConsoleKeyInfo(character, key, false, false, false));
        }

        Assert.Equal("/models", input.SelectedCommand);
        Assert.Equal(InputAction.Submit,
            input.Handle(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false)));
        Assert.Equal("/models", input.Buffer);
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
        host.Enter();
        host.ShowSession("granite4.2:3b");
        var input = new TuiInput(host);
        input.BeginModelPicker(new[]
        {
            new ModelInfo { Name = "modelo-a" },
            new ModelInfo { Name = "modelo-b" }
        }, _ => "Código · Herramientas");

        Assert.Contains("modelo-a    Código · Herramientas",
            Ansi.StripSgr(host.SnapshotFullFrame()));

        var initial = input.SelectedModel;
        input.Handle(new ConsoleKeyInfo((char)0, ConsoleKey.DownArrow, false, false, false));

        Assert.NotEqual(initial, input.SelectedModel);
        Assert.Equal(InputAction.ModelSelected,
            input.Handle(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false)));
        Assert.Equal("modelo-b", input.SelectedModel);
        Assert.DoesNotContain("Modelos locales", Ansi.StripSgr(host.SnapshotFullFrame()));
    }

    [Fact]
    public void SelectorDeModelos_DeshabilitaYOmiteLosQueNoCabenEnElPresupuesto()
    {
        using var host = new TuiHost(forceInteractive: true);
        host.Enter();
        host.ShowSession("modelo-b");
        var input = new TuiInput(host);
        input.BeginModelPicker(
            new[]
            {
                new ModelInfo { Name = "modelo-a" },
                new ModelInfo { Name = "modelo-b" },
                new ModelInfo { Name = "modelo-c" }
            },
            _ => "Código",
            model => model.Name == "modelo-b");

        var rawFrame = host.SnapshotFullFrame();
        var frame = Ansi.StripSgr(rawFrame);
        Assert.Contains("modelo-a    Código · presupuesto no disponible", frame);
        Assert.Contains("modelo-c    Código · presupuesto no disponible", frame);
        if (Ansi.ColorEnabled)
        {
            Assert.Contains(Ansi.Dim + Ansi.FgGris, rawFrame);
        }
        Assert.Equal("modelo-b", input.SelectedModel);

        input.Handle(new ConsoleKeyInfo((char)0, ConsoleKey.DownArrow, false, false, false));
        input.Handle(new ConsoleKeyInfo((char)0, ConsoleKey.UpArrow, false, false, false));
        Assert.Equal("modelo-b", input.SelectedModel);
        Assert.Equal(InputAction.ModelSelected,
            input.Handle(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false)));
        Assert.Equal("modelo-b", input.SelectedModel);
    }

    [Fact]
    public void SelectorDeModelos_SinPresupuestoNoPermiteConfirmar()
    {
        using var host = new TuiHost(forceInteractive: true);
        host.Enter();
        host.ShowSession("granite4.2:3b");
        var input = new TuiInput(host);
        input.BeginModelPicker(
            new[] { new ModelInfo { Name = "modelo-a" } },
            isSelectable: _ => false);

        Assert.Null(input.SelectedModel);
        Assert.Equal(InputAction.None,
            input.Handle(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false)));
        Assert.Null(input.SelectedModel);
        Assert.Contains("presupuesto no disponible",
            Ansi.StripSgr(host.SnapshotFullFrame()));
    }

    [Fact]
    public void SelectorAgentes_PermiteElegirPlanYConservaLaTarea()
    {
        using var host = new TuiHost(forceInteractive: true);
        host.Enter();
        host.ShowSession("granite4.2:3b");
        var input = new TuiInput(host);
        input.SetText("actualiza el sitio de prueba");
        input.BeginAgentPicker(new[]
        {
            new SelectItem("plan", "Plan", "Crea y guarda un plan", Array.Empty<string>(), false, null, "Enter"),
            new SelectItem("codigo", "Código", "Ejecuta tareas con herramientas", Array.Empty<string>(), false, null, "Enter")
        }, activeAgentId: "codigo");

        var frame = Ansi.StripSgr(host.SnapshotFullFrame());
        Assert.Contains("Agentes", frame);
        Assert.Contains("Plan", frame);
        Assert.Contains("Código", frame);
        Assert.Equal("codigo", input.SelectedAgentId);

        input.Handle(new ConsoleKeyInfo((char)0, ConsoleKey.DownArrow, false, false, false));

        Assert.Equal("plan", input.SelectedAgentId);
        Assert.Equal(InputAction.AgentSelected,
            input.Handle(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false)));
        Assert.Equal("plan", input.SelectedAgentId);
        Assert.Equal("actualiza el sitio de prueba", input.Buffer);
    }

    [Fact]
    public void SelectorAgentes_EscapeCierraSinCambiarElModoNiPerderLaTarea()
    {
        using var host = new TuiHost(forceInteractive: true);
        host.Enter();
        var input = new TuiInput(host);
        input.SetText("revisa el proyecto");
        input.BeginAgentPicker(new[]
        {
            new SelectItem("plan", "Plan", "Crea y guarda un plan", Array.Empty<string>(), false, null, null),
            new SelectItem("codigo", "Código", "Ejecuta tareas con herramientas", Array.Empty<string>(), false, null, null)
        }, activeAgentId: "codigo");

        Assert.Equal(InputAction.None,
            input.Handle(new ConsoleKeyInfo((char)0, ConsoleKey.Escape, false, false, false)));

        Assert.Equal("codigo", input.SelectedAgentId);
        Assert.Equal("revisa el proyecto", input.Buffer);
        Assert.DoesNotContain("Agentes", Ansi.StripSgr(host.SnapshotFullFrame()));
    }

    [Fact]
    public void PaletaDesdeAgentes_ConservaElTextoDeLaTareaAlCerrar()
    {
        using var host = new TuiHost(forceInteractive: true);
        var input = new TuiInput(host);
        input.SetText("construye el sitio");
        input.BeginAgentPicker(new[]
        {
            new SelectItem("plan", "Plan", "Planifica", Array.Empty<string>(), false, null, null),
            new SelectItem("codigo", "Código", "Construye", Array.Empty<string>(), false, null, null)
        }, activeAgentId: "codigo");

        Assert.Equal(InputAction.OpenCommands,
            input.Handle(new ConsoleKeyInfo('\0', ConsoleKey.P, false, false, true)));
        input.BeginCommandPalette();
        input.Handle(new ConsoleKeyInfo((char)0, ConsoleKey.Escape, false, false, false));

        Assert.Equal("construye el sitio", input.Buffer);
    }

    [Fact]
    public void ModoPlan_EnviaLaSolicitudAlPlanificadorExistente()
    {
        var route = Assert.IsType<SlashRoute>(CondorTui.RouteAgentMode("plan", "preparar una pagina"));

        Assert.Equal(SlashCommandKind.Planear, route.Kind);
        Assert.Equal("preparar una pagina", string.Join(" ", route.Arguments));
    }

    [Fact]
    public void ModoCodigo_ConservaLaRutaDelAgenteConHerramientas()
    {
        var route = Assert.IsType<FreeIntentionRoute>(CondorTui.RouteAgentMode("codigo", "crear una pagina"));

        Assert.Equal("crear una pagina", route.Intention);
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
