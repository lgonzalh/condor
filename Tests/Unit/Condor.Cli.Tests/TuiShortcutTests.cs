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
    public void CtrlP_MuestraComandosFiltrablesYModeloEjecutaElSelectorLocal()
    {
        using var host = new TuiHost(forceInteractive: true);
        host.Enter();
        host.ShowSession("granite4.2:3b");
        var input = new TuiInput(host);
        input.BeginCommandPalette();

        var initialFrame = Ansi.StripSgr(host.SnapshotFullFrame());
        Assert.Contains("Comandos", initialFrame);
        Assert.Contains("/agentes", initialFrame);

        foreach (var character in "modelo")
        {
            var key = character switch
            {
                'm' => ConsoleKey.M,
                'o' => ConsoleKey.O,
                'd' => ConsoleKey.D,
                'e' => ConsoleKey.E,
                'l' => ConsoleKey.L,
                _ => ConsoleKey.NoName
            };
            input.Handle(new ConsoleKeyInfo(character, key, false, false, false));
        }

        Assert.Equal("/modelo", input.SelectedCommand);
        Assert.Equal(InputAction.Submit,
            input.Handle(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false)));
        Assert.Equal("/modelo", input.Buffer);
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
        }, _ => "Codificar · Herramientas");

        Assert.Contains("modelo-a    Codificar · Herramientas",
            Ansi.StripSgr(host.SnapshotFullFrame()));

        var initial = input.SelectedModel;
        input.Handle(new ConsoleKeyInfo((char)0, ConsoleKey.DownArrow, false, false, false));

        Assert.NotEqual(initial, input.SelectedModel);
        Assert.Equal(InputAction.ModelSelected,
            input.Handle(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false)));
        Assert.Equal("modelo-b", input.SelectedModel);
        Assert.DoesNotContain("Modelo local", Ansi.StripSgr(host.SnapshotFullFrame()));
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
            _ => "Codificar",
            model => model.Name == "modelo-b");

        var rawFrame = host.SnapshotFullFrame();
        var frame = Ansi.StripSgr(rawFrame);
        Assert.Contains("modelo-a    Codificar · presupuesto no disponible", frame);
        Assert.Contains("modelo-c    Codificar · presupuesto no disponible", frame);
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
            new SelectItem("codificar", "Codificar", "Ejecuta tareas con herramientas", Array.Empty<string>(), false, null, "Enter")
        }, activeAgentId: "codificar");

        var frame = Ansi.StripSgr(host.SnapshotFullFrame());
        Assert.Contains("Modos de trabajo", frame);
        Assert.Contains("Plan", frame);
        Assert.Contains("Codificar", frame);
        Assert.Equal("codificar", input.SelectedAgentId);

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
            new SelectItem("codificar", "Codificar", "Ejecuta tareas con herramientas", Array.Empty<string>(), false, null, null)
        }, activeAgentId: "codificar");

        Assert.Equal(InputAction.None,
            input.Handle(new ConsoleKeyInfo((char)0, ConsoleKey.Escape, false, false, false)));

        Assert.Equal("codificar", input.SelectedAgentId);
        Assert.Equal("revisa el proyecto", input.Buffer);
        Assert.DoesNotContain("Modos de trabajo", Ansi.StripSgr(host.SnapshotFullFrame()));
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
            new SelectItem("codificar", "Codificar", "Construye", Array.Empty<string>(), false, null, null)
        }, activeAgentId: "codificar");

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
        var route = Assert.IsType<FreeIntentionRoute>(CondorTui.RouteAgentMode("codificar", "crear una pagina"));

        Assert.Equal("crear una pagina", route.Intention);
    }

    [Fact]
    public void ModoPreguntar_ConsultaConElModeloActivo()
    {
        var route = Assert.IsType<SlashRoute>(CondorTui.RouteAgentMode("preguntar", "explica el proyecto", "granite4.2:3b"));

        Assert.Equal(SlashCommandKind.Consultar, route.Kind);
        Assert.Equal(new[] { "explica el proyecto", "--modelo", "granite4.2:3b" }, route.Arguments);
    }

    [Fact]
    public void ModoDepurar_EnrutaLaSolicitudConObjetivoDeDiagnosticoYValidacion()
    {
        var route = Assert.IsType<FreeIntentionRoute>(CondorTui.RouteAgentMode("depurar", "el test falla al guardar"));

        Assert.Contains("Depura e investiga la falla descrita", route.Intention);
        Assert.Contains("el test falla al guardar", route.Intention);
        Assert.Contains("valida el resultado", route.Intention);
    }

    [Fact]
    public void ModoOrquestador_EnviaLaSolicitudAlCicloDeCondor()
    {
        var route = Assert.IsType<SlashRoute>(CondorTui.RouteAgentMode("orquestador", "crea una pagina"));

        Assert.Equal(SlashCommandKind.Avanzar, route.Kind);
        Assert.Equal("crea una pagina", string.Join(" ", route.Arguments));
    }

    [Fact]
    public void ModosSeHabilitanSegunCapacidadesDeclaradasYFuncionesDeCondor()
    {
        var model = new ModelInfo
        {
            Name = "qwen2.5-coder:3b",
            Capabilities = new List<string> { "completion", "tools" }
        };

        var options = CondorTui.BuildAgentOptions(model).ToDictionary(option => option.Id);

        Assert.False(options["codificar"].Disabled);
        Assert.False(options["preguntar"].Disabled);
        Assert.False(options["depurar"].Disabled);
        Assert.True(options["orquestador"].Disabled);
        Assert.False(options["plan"].Disabled);
        Assert.Equal("preguntar", CondorTui.ResolveAgentModeForModel("codificar", new ModelInfo
        {
            Name = "granite4.2:3b",
            Capabilities = new List<string> { "completion" }
        }));

        model.Capabilities.Add("orchestrator");
        options = CondorTui.BuildAgentOptions(model).ToDictionary(option => option.Id);
        Assert.False(options["orquestador"].Disabled);
    }

    [Fact]
    public void ModeloSinCapacidadesDeclaradas_IniciaEnPreguntarYPermiteSeleccionarlo()
    {
        var model = new ModelInfo { Name = "granite4.2:3b" };

        var options = CondorTui.BuildAgentOptions(model).ToDictionary(option => option.Id);

        Assert.False(options["preguntar"].Disabled);
        Assert.Equal("preguntar", CondorTui.ResolveAgentModeForModel("codificar", model));
        Assert.Equal("preguntar", CondorTui.ResolveAgentModeForModel("codificar", null));
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

        Assert.Contains("Preguntar", labels);
        Assert.Contains("Herramientas", labels);
        Assert.Contains("Codificar", labels);
        Assert.Contains("Depurar", labels);
        Assert.Contains("Visión", labels);
        Assert.DoesNotContain("Orquestador", labels);
        Assert.DoesNotContain("Plan", labels);
        Assert.DoesNotContain("Rápido", labels);
    }

    [Fact]
    public void VersionVisible_IdentificaBuildGamma()
    {
        Assert.Equal("v1.0.0-gamma (Build Gamma)", Condor.Cli.VersionInfo.DisplayName);
    }
}
