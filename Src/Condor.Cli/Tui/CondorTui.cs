using System.Text;
using Condor.Cli.Commands;
using Condor.Cli.Presentation;
using Condor.Cli.Routing;
using Condor.Core.Catalog;
using Condor.Core.Contracts;
using Condor.Core.Evaluation;
using Condor.Core.Models;
using Condor.Infrastructure;
using Condor.Infrastructure.Agent;
using Condor.Infrastructure.DependencyBootstrap;
using Condor.Infrastructure.Documentation;
using Condor.Infrastructure.Llm;
using Condor.Infrastructure.Setup;

namespace Condor.Cli.Tui;

/// <summary>
/// Sesion interactiva de Condor sobre la TUI persistente. Sustituye el bucle
/// clasico de consola cuando hay terminal interactiva con soporte VT; en otro
/// caso Condor usa automaticamente la experiencia CLI establecida (E2E,
/// pipelines y terminales sin VT no cambian).
///
/// Flujo del mockup oficial: Condor Grande da la bienvenida mientras se prepara
/// el entorno real; al estar listo, la sesion pasa a Condor Ave con la identidad
/// institucional permanente, zona de Conversacion/Actividad, Estado/Progreso con
/// datos reales del sistema y entrada de intenciones.
/// </summary>
public static class CondorTui
{
    private const string PlanAgentId = "plan";
    private const string AskAgentId = "preguntar";
    private const string CodeAgentId = "codificar";
    private const string DebugAgentId = "depurar";
    private const string OrchestratorAgentId = "orquestador";

    /// <summary>La TUI requiere terminal interactiva con VT y tamano suficiente.</summary>
    public static bool CanRun()
    {
        return CanRun(out _, out _);
    }

    /// <summary>
    /// Verifica compatibilidad y devuelve las dimensiones reales de la terminal
    /// para que RunAsync() no relea P/Invoke redundantes.
    /// </summary>
    public static bool CanRun(out int width, out int height)
    {
        width = 0;
        height = 0;
        if (Console.IsOutputRedirected || Console.IsInputRedirected)
        {
            return false;
        }

        if (!Ansi.TryEnableVirtualTerminal())
        {
            return false;
        }

        try
        {
            width = Console.WindowWidth;
            height = Console.WindowHeight;
            return width >= TuiHost.MinWidth &&
                   height >= TuiHost.MinHeight;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<int> RunAsync(
        IAssessmentService assessmentService,
        IStateStore stateStore,
        LocalModelSession session,
        CancellationToken shutdownToken,
        int? tuiWidth = null,
        int? tuiHeight = null)
    {
        using var host = tuiWidth.HasValue && tuiHeight.HasValue
            ? new TuiHost(tuiWidth.Value, tuiHeight.Value)
            : new TuiHost();
        host.Enter();
        host.ShowWelcome();
        host.SetWorkspace(Environment.CurrentDirectory);
        host.Repaint(); // primera imagen inmediata: la TUI aparece sin esperar la preparacion

        // ---- Preparacion real del entorno (bootstrap Ollama + modelo) ----------
        // Corre en segundo plano MIENTRAS el bucle de render repinta la pantalla:
        // el progreso es real (TuiStartupView publica estados reales) y el usuario
        // ve la TUI de inmediato, sin bloqueo previo.
        var startup = new TuiStartupView(host);
        var startupBridge = new StartupProgressObserverBridge(startup);
        startup.Start();

        var arranque = System.Threading.Tasks.Task.Run(async () =>
        {
            host.SetEstado("Preparando dependencias locales");
            var boot = await new DependencyBootstrapper(assessmentService: assessmentService).RunAsync(startupBridge, shutdownToken).ConfigureAwait(false);
            if (!boot.Ready)
            {
                return (Boot: boot, Prep: (StartupPrepResult?)null);
            }

            host.SetEstado("Seleccionando modelo adecuado para el equipo");
            var prep = await new StartupPreparer(
                assessmentService,
                stateStore,
                modelAutoSetup: new ModelAutoSetupService(
                    stateStore, assessmentService, httpClient: session.SharedHttpClient)).RunAsync(startupBridge, shutdownToken, cachedAssessment: boot.Assessment).ConfigureAwait(false);

            return (Boot: boot, Prep: (StartupPrepResult?)prep);
        });

        // Bucle de render minimo mientras arranca: mantiene la pantalla viva.
        while (!arranque.IsCompleted)
        {
            host.HandleResizeIfNeeded();
            host.Tick();
            host.Repaint();
            Thread.Sleep(40);
        }

        DependencyBootstrapResult bootstrap;
        StartupPrepResult? prepNullable;
        try
        {
            (bootstrap, prepNullable) = await arranque.ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            host.Dispose();
            PrintExitBanner();
            return 130;
        }

        if (prepNullable is null)
        {
            return await FatalExitAsync(
                host,
                "No se pudo dejar el entorno listo automaticamente.",
                DescribeBootstrapFailure(bootstrap));
        }

        var prep = prepNullable;
        if (!prep.Ready)
        {
            startup.Stop(false);
            return await FatalExitAsync(
                host,
                "Condor no puede iniciar: no hay un modelo utilizable ahora.",
                new[] { prep.Reason ?? "Se intento preparar un modelo compatible sin exito." });
        }

        startup.Stop(true);

        // Conservar en la sesion el modelo elegido durante el arranque. Las
        // tareas posteriores pueden promocionarlo solo al siguiente nivel,
        // nunca saltar directamente desde 0.5B a 3B/7B.
        if (!string.IsNullOrWhiteSpace(prep.Model))
        {
            await session.EnsureAvailableAsync(prep.Model, shutdownToken).ConfigureAwait(true);
        }

        // ---- Sesion de trabajo: Condor Grande -> Condor Ave --------------------
        host.ShowSession(prep.Model);
        host.SetModel(prep.Model);
        var providerName = session.ProviderName;
        if (!providerName.Contains("(Local)", StringComparison.OrdinalIgnoreCase))
        {
            providerName += " (Local)";
        }
        var activeModelName = session.ActiveModel ?? prep.Model;
        ModelInfo? activeModelInfo = string.IsNullOrWhiteSpace(activeModelName)
            ? null
            : FindInstalledModel(bootstrap.Assessment, activeModelName);
        if (activeModelInfo is null || (activeModelInfo.Capabilities?.Count ?? 0) == 0)
        {
            var refreshedAssessment = await ReadAssessmentAsync(assessmentService, shutdownToken).ConfigureAwait(true);
            activeModelInfo = FindInstalledModel(refreshedAssessment, activeModelName ?? "") ?? activeModelInfo;
        }
        var activeAgentId = ResolveAgentModeForModel(CodeAgentId, activeModelInfo);
        host.SetRuntimeMetadata(AgentModeLabel(activeAgentId), providerName, "Predeterminada");
        var workspace = bootstrap.Assessment?.WorkingDirectory;
        host.SetWorkspace(string.IsNullOrWhiteSpace(workspace) ? Environment.CurrentDirectory : workspace);
        host.SetEstado("Entorno listo", ActivityKind.Success);
        host.SetProgreso("—");

        // ---- Bucle principal: teclado + repintado por regiones ------------------
        using var controlInput = new ConsoleControlInputScope();
        var input = new TuiInput(host);
        host.Repaint();  // transición limpia: ClearScreen + TUI 2 (Ave) antes de cualquier elemento de TUI 2
        input.Render();

        void ApplyAgentSelection(bool taskRunning)
        {
            var selectedAgentId = input.SelectedAgentId;
            if (string.IsNullOrWhiteSpace(selectedAgentId))
            {
                return;
            }

            activeAgentId = selectedAgentId;
            var label = AgentModeLabel(activeAgentId);
            host.SetRuntimeMetadata(label, providerName, "Predeterminada");
            host.AddActivity("Modo seleccionado: " + label, ActivityKind.System);
            host.SetEstado(
                taskRunning ? "Modo " + label + " para la siguiente tarea" : "Modo activo: " + label,
                ActivityKind.Success);
        }

        void ReconcileAgentMode(ModelInfo? model)
        {
            activeModelInfo = model;
            var resolvedAgentId = ResolveAgentModeForModel(activeAgentId, model);
            if (string.Equals(resolvedAgentId, activeAgentId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var previousLabel = AgentModeLabel(activeAgentId);
            activeAgentId = resolvedAgentId;
            var activeLabel = AgentModeLabel(activeAgentId);
            host.SetRuntimeMetadata(activeLabel, providerName, "Predeterminada");
            host.AddActivity(
                $"El modo {previousLabel} no está disponible con el modelo actual; se activó {activeLabel}.",
                ActivityKind.Warning);
        }

        void OpenAgentPicker(bool taskRunning)
        {
            input.BeginAgentPicker(BuildAgentOptions(activeModelInfo), activeAgentId);
            host.SetEstado(
                taskRunning ? "Elige el modo para la siguiente tarea" : "Elige el modo de trabajo",
                ActivityKind.System);
        }

        Task<AgentResult>? running = null;
        CancellationTokenSource? runningCts = null;
        var runningWatch = System.Diagnostics.Stopwatch.StartNew();
        var exitCode = 0;
        string? lastHeaderModel = null;

        while (true)
        {
            if (host.HandleResizeIfNeeded())
            {
                input.Render();
            }
            host.Tick();

            // Actualizar modelo en cabecera si hay tarea activa y el modelo de sesion cambio.
            if (running is not null)
            {
                var activeModel = session.ActiveModel;
                if (!string.IsNullOrWhiteSpace(activeModel) && activeModel != lastHeaderModel)
                {
                    host.SetModel(activeModel);
                    lastHeaderModel = activeModel;
                }
            }
            else if (lastHeaderModel is not null)
            {
                // Conservar el modelo activo real, incluso si el usuario lo
                // cambio desde el selector despues del arranque.
                var activeModel = session.ActiveModel;
                if (!string.IsNullOrWhiteSpace(activeModel) && activeModel != lastHeaderModel)
                {
                    host.SetModel(activeModel);
                }
                lastHeaderModel = null;
            }

            host.Repaint();

            // Finalizacion de la tarea del agente (marshal al hilo de interfaz).
            if (running is { IsCompleted: true })
            {
                var finished = running;
                running = null;
                runningWatch.Stop();

                AgentResult? result = null;
                string? failure = null;
                try
                {
                    result = await finished.ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                    failure = null;
                }
                catch (Exception ex)
                {
                    failure = ShortReason(ex);
                }

                runningCts?.Dispose();
                runningCts = null;

                if (result is not null)
                {
                    foreach (var line in SplitLines(AgentRenderer.BuildResultText(result, runningWatch.Elapsed)))
                    {
                        host.AddActivity(line, result.Success ? ResultActivityKind(line) : ActivityKind.Error);
                    }

                    host.SetEstado(result.Success ? "Tarea completada" : "No se pudo completar la tarea",
                        result.Success ? ActivityKind.Success : ActivityKind.Error);
                }
                else if (failure is not null)
                {
                    host.AddActivity("No se pudo completar la tarea: " + failure, ActivityKind.Error);
                    host.SetEstado("Error durante la tarea", ActivityKind.Error);
                }
                else
                {
                    host.AddActivity("Tarea cancelada por el usuario.", ActivityKind.Warning);
                    host.SetEstado("Tarea cancelada", ActivityKind.Warning);
                }

                host.SetProgreso(FormatoTiempo(runningWatch.Elapsed));
                host.SetBusy(false);
                input.Render();
            }

            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);

                if ((key.Modifiers & ConsoleModifiers.Control) != 0 && key.Key == ConsoleKey.C)
                {
                    // Ctrl+C solo sale de la sesion cuando no hay tarea en curso
                    if (running is null)
                    {
                        break;
                    }
                    // Con tarea en curso, Ctrl+C no hace nada (se usa Esc+Esc)
                    continue;
                }

                if (running is not null)
                {
                    var interruptAction = input.Handle(key);
                    if (interruptAction == InputAction.Interrupt)
                    {
                        // Interrupcion cooperativa de la tarea en curso (Esc + Esc).
                        runningCts?.Cancel();
                        continue;
                    }
                    if (interruptAction == InputAction.OpenAgents)
                    {
                        OpenAgentPicker(taskRunning: true);
                        host.Repaint();
                        continue;
                    }
                    if (interruptAction == InputAction.AgentSelected)
                    {
                        ApplyAgentSelection(taskRunning: true);
                        host.Repaint();
                        continue;
                    }
                    if (interruptAction == InputAction.OpenCommands)
                    {
                        input.BeginCommandPalette(TuiInput.BusyCommandPaletteCommands);
                        host.SetEstado("Comandos disponibles durante la tarea");
                        host.Repaint();
                        continue;
                    }
                    if (interruptAction == InputAction.Submit)
                    {
                        var command = input.Buffer.Trim();
                        input.Clear();
                        if (IsAgentPickerCommand(command))
                        {
                            OpenAgentPicker(taskRunning: true);
                        }
                        else if (IsModelListCommand(command))
                        {
                            host.AddActivity("El cambio de modelo estará disponible al terminar la tarea en curso.", ActivityKind.Warning);
                            host.SetEstado("Modelo local protegido durante la ejecución", ActivityKind.Warning);
                        }
                        else if (command.Equals("/ayuda", StringComparison.OrdinalIgnoreCase))
                        {
                            RenderHelpInTui(host);
                            host.SetEstado("Ayuda · tarea en curso", ActivityKind.System);
                        }
                        host.Repaint();
                        continue;
                    }
                    continue; // teclado reservado mientras Condor trabaja
                }

                var submitAction = input.Handle(key);
                if (submitAction == InputAction.OpenAgents)
                {
                    OpenAgentPicker(taskRunning: false);
                    continue;
                }

                if (submitAction == InputAction.AgentSelected)
                {
                    ApplyAgentSelection(taskRunning: false);
                    host.Repaint();
                    continue;
                }

                if (submitAction == InputAction.OpenCommands)
                {
                    input.BeginCommandPalette();
                    host.SetEstado("Paleta de comandos", ActivityKind.System);
                    continue;
                }

                if (submitAction == InputAction.ModelSelected)
                {
                    var selectedModel = input.SelectedModel;
                    input.Clear();
                    if (!string.IsNullOrWhiteSpace(selectedModel))
                    {
                        var switched = await SwitchModelAsync(selectedModel, assessmentService, session, host, shutdownToken)
                            .ConfigureAwait(true);
                        if (switched)
                        {
                            var assessment = await ReadAssessmentAsync(assessmentService, shutdownToken).ConfigureAwait(true);
                            var model = string.IsNullOrWhiteSpace(session.ActiveModel)
                                ? null
                                : FindInstalledModel(assessment, session.ActiveModel);
                            ReconcileAgentMode(model);
                        }
                    }
                    continue;
                }

                if (submitAction == InputAction.Exit)
                {
                    break;
                }

                if (submitAction == InputAction.Submit)
                {
                    var text = input.Buffer.Trim();
                    input.Clear();
                    if (text.Length == 0)
                    {
                        continue;
                    }

                    if (IsExit(text))
                    {
                        break;
                    }

                    // Comentario del usuario (-texto-): se registra tal cual y NUNCA
                    // se interpreta como instruccion, comando, tarea o accion.
                    if (EsComentarioUsuario(text))
                    {
                        host.AddActivity(text, ActivityKind.User);
                        host.SetEstado("Comentario registrado", ActivityKind.System);
                        host.SetProgreso("—");
                        input.Render();
                        continue;
                    }

                    if (IsAgentPickerCommand(text))
                    {
                        OpenAgentPicker(taskRunning: false);
                        continue;
                    }

                    if (IsModelListCommand(text))
                    {
                        var inventory = await LoadInstalledModelsAsync(assessmentService, shutdownToken).ConfigureAwait(true);
                        var models = inventory.Models;
                        if (models.Count == 0)
                        {
                            host.SetEstado("Sin modelos locales", ActivityKind.Warning);
                        }
                        else
                        {
                            input.BeginModelPicker(
                                models,
                                DescribeModelCapabilities,
                                model => ModelFitsCurrentMemory(model, inventory.Memory, out _),
                                session.ActiveModel);
                            host.SetEstado("Selecciona un modelo local", ActivityKind.System);
                        }
                        continue;
                    }

                    if (TryReadModelArgument(text, out var modelArgument))
                    {
                        if (!string.IsNullOrWhiteSpace(modelArgument))
                        {
                            await SwitchModelAsync(modelArgument, assessmentService, session, host, shutdownToken)
                                .ConfigureAwait(true);
                        }
                        continue;
                    }

                    var route = IntentionRouter.Route(text);
                    switch (route)
                    {
                        case SlashRoute slash when slash.Kind == SlashCommandKind.Ayuda:
                            // /ayuda dentro de la TUI: mostrar ayuda en la zona de actividad sin suspender la sesion.
                            host.AddActivity(text, ActivityKind.User);
                            RenderHelpInTui(host);
                            host.SetEstado("Listo", ActivityKind.Success);
                            host.SetProgreso("—");
                            input.Render();
                            break;

                        case SlashRoute slash:
                            // Los demas comandos "/" conservan su presentacion establecida:
                            // la pantalla TUI se suspende y retoma intacta.
                            host.AddActivity(text, ActivityKind.User);
                            host.SetEstado("Ejecutando " + FirstToken(text));
                            await host.SuspendAsync(async () =>
                                exitCode = await Program.HandleSlashAsync(slash, assessmentService, stateStore, session.Llm, session)).ConfigureAwait(true);
                            host.SetEstado("Listo", ActivityKind.Success);
                            host.SetProgreso("—");
                            input.Render();
                            break;

                        case FreeIntentionRoute free when !string.IsNullOrWhiteSpace(free.Intention):
                            host.AddActivity(text, ActivityKind.User);
                            var modeRoute = RouteAgentMode(activeAgentId, free.Intention, session.ActiveModel);
                            if (modeRoute is SlashRoute modeSlash)
                            {
                                host.SetBusy(true);
                                host.SetEstado(modeSlash.Kind switch
                                {
                                    SlashCommandKind.Planear => "Creando plan con Condor",
                                    SlashCommandKind.Consultar => "Consultando al modelo activo",
                                    SlashCommandKind.Avanzar => "Orquestando el ciclo de Condor",
                                    _ => "Ejecutando el modo seleccionado"
                                }, ActivityKind.System);
                                host.SetProgreso("—");
                                try
                                {
                                    await host.SuspendAsync(async () =>
                                        exitCode = await Program.HandleSlashAsync(
                                            modeSlash, assessmentService, stateStore, session.Llm, session)).ConfigureAwait(true);
                                }
                                finally
                                {
                                    host.SetBusy(false);
                                    host.SetProgreso("—");
                                }
                                host.SetEstado(modeSlash.Kind switch
                                {
                                    SlashCommandKind.Planear => exitCode == 0 ? "Plan creado" : "No se pudo crear el plan",
                                    SlashCommandKind.Consultar => exitCode == 0 ? "Consulta completada" : "No se pudo completar la consulta",
                                    SlashCommandKind.Avanzar => exitCode == 0 ? "Ciclo de Condor completado" : "No se pudo completar el ciclo",
                                    _ => exitCode == 0 ? "Modo completado" : "No se pudo completar el modo"
                                }, exitCode == 0 ? ActivityKind.Success : ActivityKind.Error);
                                input.Render();
                                break;
                            }

                            var agentIntention = modeRoute is FreeIntentionRoute routedFree
                                ? routedFree.Intention
                                : free.Intention;
                            host.SetBusy(true);
                            host.SetEstado("Preparando análisis de la tarea");
                            host.SetProgreso("0 s");
                            host.SetModelStatus(
                                (string.IsNullOrWhiteSpace(session.ActiveModel) ? "Modelo local" : session.ActiveModel) + " · preparando análisis",
                                working: true);
                            runningCts = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
                            var agentBridge = new AgentProgressObserverBridge(new TuiAgentProgressView(host));
                            var agent = new DocumentingAgentService(
                                new AgentService(stateStore, assessmentService, session: session),
                                new DocumenterService());
                            runningWatch.Restart();
                            var token = runningCts.Token;
                            running = Task.Run(() => agent.RunAsync(agentIntention, agentBridge, token));
                            break;
                    }
                }
            }
            else
            {
                Thread.Sleep(40);
            }
        }

        runningCts?.Cancel();
        host.Dispose();
        PrintExitBanner();
        return exitCode;
    }

    private static void PrintExitBanner()
    {
        try
        {
            Console.OutputEncoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }
        catch
        {
            // Conserva la salida aunque el host de consola no permita cambiar la codificacion.
        }

        Console.WriteLine();
        foreach (var row in CondorArt.ExitWordmark)
        {
            Console.WriteLine(row);
        }

        Console.WriteLine(Ansi.FgBlanco + "Gracias por usar Condor." + Ansi.Reset);
        Console.WriteLine(Ansi.FgGris + "Hasta pronto." + Ansi.Reset);
    }

    private static bool IsAgentPickerCommand(string text)
        => text.Equals("/agentes", StringComparison.OrdinalIgnoreCase) ||
           text.Equals("/agents", StringComparison.OrdinalIgnoreCase);

    private static bool IsModelListCommand(string text)
        => text.Equals("/modelo", StringComparison.OrdinalIgnoreCase) ||
           text.Equals("/model", StringComparison.OrdinalIgnoreCase) ||
           text.Equals("/models", StringComparison.OrdinalIgnoreCase) ||
           text.Equals("/modelos", StringComparison.OrdinalIgnoreCase);

    private static bool TryReadModelArgument(string text, out string modelName)
    {
        foreach (var command in new[] { "/modelo ", "/model ", "/models ", "/modelos " })
        {
            if (text.StartsWith(command, StringComparison.OrdinalIgnoreCase))
            {
                modelName = text[command.Length..].Trim();
                return true;
            }
        }

        modelName = "";
        return false;
    }

    internal static IReadOnlyList<SelectItem> BuildAgentOptions(ModelInfo? model)
    {
        var hasCompletion = HasCapability(model, "completion");
        var hasTools = HasCapability(model, "tools", "tool-use");
        var canCode = hasCompletion && hasTools && IsCodingModel(model);
        var hasOrchestrator = HasCapability(model, "orchestrator");
        var unknownCapabilities = model is null || (model.Capabilities?.Count ?? 0) == 0;

        string CompletionReason() => unknownCapabilities
            ? "capacidades del modelo no verificadas"
            : "el modelo no declara capacidad de respuesta";

        string CodingReason() => !hasCompletion
            ? CompletionReason()
            : !hasTools
                ? "el modelo no declara uso de herramientas"
                : "el perfil del modelo no identifica capacidad de código";

        return new[]
        {
            new SelectItem(
                CodeAgentId, "Codificar", "Implementa la tarea con herramientas de Condor.",
                Array.Empty<string>(), Disabled: !canCode, DisabledReason: canCode ? null : CodingReason(), Shortcut: "Enter"),
            new SelectItem(
                AskAgentId, "Preguntar", "Consulta al modelo activo sin ejecutar cambios.",
                Array.Empty<string>(), Disabled: !hasCompletion, DisabledReason: hasCompletion ? null : CompletionReason(), Shortcut: "Enter"),
            new SelectItem(
                DebugAgentId, "Depurar", "Investiga el fallo y valida una corrección con herramientas.",
                Array.Empty<string>(), Disabled: !canCode, DisabledReason: canCode ? null : CodingReason(), Shortcut: "Enter"),
            new SelectItem(
                OrchestratorAgentId, "Orquestador", "Ejecuta el ciclo de plan, construcción y verificación de Condor.",
                Array.Empty<string>(), Disabled: !hasOrchestrator,
                DisabledReason: hasOrchestrator ? null : unknownCapabilities
                    ? "capacidades del modelo no verificadas"
                    : "el modelo no declara la opción Orquestador",
                Shortcut: "Enter"),
            new SelectItem(
                PlanAgentId, "Plan", "Condor prepara y guarda un plan sin modificar el proyecto.",
                Array.Empty<string>(), Disabled: false, DisabledReason: null, Shortcut: "Enter")
        };
    }

    internal static string ResolveAgentModeForModel(string? preferredAgentId, ModelInfo? model)
    {
        var options = BuildAgentOptions(model);
        if (!string.IsNullOrWhiteSpace(preferredAgentId) && options.Any(option =>
                option.Id.Equals(preferredAgentId, StringComparison.OrdinalIgnoreCase) && !option.Disabled))
        {
            return options.First(option => option.Id.Equals(preferredAgentId, StringComparison.OrdinalIgnoreCase)).Id;
        }

        return options.FirstOrDefault(option => !option.Disabled)?.Id ?? PlanAgentId;
    }

    internal static string AgentModeLabel(string agentId) => agentId.ToLowerInvariant() switch
    {
        "codificar" => "Codificar",
        "preguntar" => "Preguntar",
        "depurar" => "Depurar",
        "orquestador" => "Orquestador",
        "plan" => "Plan",
        _ => "Modo de trabajo"
    };

    internal static IntentionRoute RouteAgentMode(string agentId, string intention, string? activeModel = null)
    {
        if (string.Equals(agentId, PlanAgentId, StringComparison.OrdinalIgnoreCase))
        {
            return new SlashRoute(SlashCommandKind.Planear, new[] { intention });
        }

        if (string.Equals(agentId, AskAgentId, StringComparison.OrdinalIgnoreCase))
        {
            var arguments = new List<string> { intention };
            if (!string.IsNullOrWhiteSpace(activeModel))
            {
                arguments.Add("--modelo");
                arguments.Add(activeModel);
            }

            return new SlashRoute(SlashCommandKind.Consultar, arguments.ToArray());
        }

        if (string.Equals(agentId, OrchestratorAgentId, StringComparison.OrdinalIgnoreCase))
        {
            return new SlashRoute(SlashCommandKind.Avanzar, new[] { intention });
        }

        if (string.Equals(agentId, DebugAgentId, StringComparison.OrdinalIgnoreCase))
        {
            return new FreeIntentionRoute(
                "Depura e investiga la falla descrita; aplica una corrección y valida el resultado. Solicitud: " + intention);
        }

        return new FreeIntentionRoute(intention);
    }

    private static bool HasCapability(ModelInfo? model, params string[] expected)
        => model?.Capabilities?.Any(value => expected.Contains(value, StringComparer.OrdinalIgnoreCase)) == true;

    private static bool IsCodingModel(ModelInfo? model)
    {
        if (model is null)
        {
            return false;
        }

        var identity = (model.Name + " " + model.Family).ToLowerInvariant();
        if (identity.Contains("coder", StringComparison.Ordinal) || identity.Contains("code", StringComparison.Ordinal))
        {
            return true;
        }

        var catalogProfile = ModelCatalog.Default.FirstOrDefault(candidate =>
            candidate.Name.Equals(model.Name, StringComparison.OrdinalIgnoreCase) ||
            candidate.PullName.Equals(model.Name, StringComparison.OrdinalIgnoreCase));
        return (catalogProfile?.CodingLevel ?? 0) > 0;
    }

    private static async Task<(IReadOnlyList<ModelInfo> Models, MemoryInfo? Memory)> LoadInstalledModelsAsync(
        IAssessmentService assessmentService,
        CancellationToken cancellationToken)
    {
        try
        {
            var assessment = await assessmentService.ExecuteAsync(
                new AssessmentRequest { WorkingDirectory = Environment.CurrentDirectory },
                cancellationToken).ConfigureAwait(true);
            var models = (assessment.Tools?.Ollama?.Models ?? new List<ModelInfo>())
                .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return (models, assessment.Environment?.Memory);
        }
        catch
        {
            return (Array.Empty<ModelInfo>(), null);
        }
    }

    private static async Task<bool> SwitchModelAsync(
        string modelName,
        IAssessmentService assessmentService,
        LocalModelSession session,
        TuiHost host,
        CancellationToken cancellationToken)
    {
        host.SetBusy(true);
        host.SetEstado("Evaluando el cambio de modelo");
        host.SetProgreso("inventario local y RAM disponibles");
        host.SetModelStatus("Cargando " + modelName, working: true);
        host.Repaint();

        try
        {
            var firstAssessment = await ReadAssessmentAsync(assessmentService, cancellationToken).ConfigureAwait(true);
            var model = FindInstalledModel(firstAssessment, modelName);
            if (model is null)
            {
                host.AddActivity("El modelo ya no aparece en Ollama; se canceló el cambio.", ActivityKind.Warning);
                host.SetEstado("Modelo no disponible", ActivityKind.Warning);
                return false;
            }

            var previousModel = session.ActiveModel;
            if (previousModel?.Equals(model.Name, StringComparison.OrdinalIgnoreCase) == true)
            {
                host.AddActivity("El modelo " + model.Name + " ya está activo.", ActivityKind.System);
                host.SetEstado("Modelo ya activo", ActivityKind.Success);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(previousModel))
            {
                await session.ReleaseAsync(cancellationToken).ConfigureAwait(true);
            }

            // La liberación del modelo anterior puede cambiar la RAM libre. Se
            // vuelve a medir justo antes de cambiar el modelo activo.
            var freshAssessment = await ReadAssessmentAsync(assessmentService, cancellationToken).ConfigureAwait(true);
            model = FindInstalledModel(freshAssessment, model.Name);
            string? reason = null;
            if (model is null || !ModelFitsCurrentMemory(model, freshAssessment?.Environment?.Memory, out reason))
            {
                if (!string.IsNullOrWhiteSpace(previousModel))
                {
                    await session.EnsureAvailableAsync(previousModel, cancellationToken).ConfigureAwait(true);
                }
                host.AddActivity("No se cambió el modelo: " + (reason ?? "el inventario local cambió durante la selección."), ActivityKind.Warning);
                host.SetEstado("Cambio de modelo cancelado", ActivityKind.Warning);
                return false;
            }

            if (!await session.EnsureAvailableAsync(model.Name, cancellationToken).ConfigureAwait(true))
            {
                if (!string.IsNullOrWhiteSpace(previousModel))
                {
                    await session.EnsureAvailableAsync(previousModel, cancellationToken).ConfigureAwait(true);
                }
                host.AddActivity("Ollama no responde; se conserva el modelo anterior.", ActivityKind.Warning);
                host.SetEstado("Ollama no disponible", ActivityKind.Warning);
                return false;
            }

            var budget = BudgetPolicy.Default.Assess(freshAssessment?.Environment?.Memory);
            session.LastSelectionBudgetGb = budget.BudgetGb;
            session.LastSelectionFreeRamGb = freshAssessment?.Environment?.Memory is { } memory
                ? memory.FreeBytes / ModelMemoryBudget.BytesPerGb
                : 0;
            host.SetModel(model.Name);
            host.AddActivity("Modelo seleccionado: " + model.Name + " · " + DescribeModelCapabilities(model), ActivityKind.Success);
            host.SetEstado("Modelo seleccionado", ActivityKind.Success);
            return true;
        }
        finally
        {
            host.SetBusy(false);
            host.SetProgreso("—");
            var activeModel = session.ActiveModel;
            host.SetModelStatus(
                string.IsNullOrWhiteSpace(activeModel) ? "Modelo local · sin cambios" : activeModel + " · listo",
                working: false);
            host.Repaint();
        }
    }

    private static async Task<AssessmentResult?> ReadAssessmentAsync(
        IAssessmentService assessmentService,
        CancellationToken cancellationToken)
    {
        try
        {
            return await assessmentService.ExecuteAsync(
                new AssessmentRequest { WorkingDirectory = Environment.CurrentDirectory },
                cancellationToken).ConfigureAwait(true);
        }
        catch
        {
            return null;
        }
    }

    private static ModelInfo? FindInstalledModel(AssessmentResult? assessment, string name)
        => assessment?.Tools?.Ollama?.Models.FirstOrDefault(model =>
            model.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static bool ModelFitsCurrentMemory(ModelInfo model, MemoryInfo? memory, out string? reason)
    {
        reason = null;
        if (memory is null || memory.Status != DetectionStatus.Detected || memory.TotalBytes <= 0 || model.SizeBytes <= 0)
        {
            reason = "RAM o tamaño real del modelo no verificable.";
            return false;
        }

        var weightGb = model.SizeBytes / ModelMemoryBudget.BytesPerGb;
        const double contextEstimateGb = 8192 * 0.125 / ModelMemoryBudget.BytesPerGb;
        var peakGb = ModelMemoryBudget.EstimatePeakGb(weightGb, contextEstimateGb);
        var totalGb = memory.TotalBytes / ModelMemoryBudget.BytesPerGb;
        var budget = BudgetPolicy.Default.Assess(memory);
        if (ModelMemoryBudget.ClassifyByRatio(peakGb, totalGb) == ResourcePressure.Insufficient ||
            !budget.IsBudgeted || !budget.Admits(peakGb))
        {
            reason = "la RAM libre real no deja el margen operativo requerido para cargarlo.";
            return false;
        }

        return true;
    }

    internal static string DescribeModelCapabilities(ModelInfo model)
    {
        var labels = new List<string>();
        var hasCompletion = HasCapability(model, "completion");
        var hasTools = HasCapability(model, "tools", "tool-use");
        var canCode = hasCompletion && hasTools && IsCodingModel(model);

        if (hasCompletion) labels.Add("Preguntar");
        if (hasTools) labels.Add("Herramientas");
        if (canCode)
        {
            labels.Add("Codificar");
            labels.Add("Depurar");
        }
        if (HasCapability(model, "orchestrator")) labels.Add("Orquestador");
        if (HasCapability(model, "vision")) labels.Add("Visión");
        if (HasCapability(model, "audio")) labels.Add("Audio");
        if (labels.Count == 0) labels.Add("Capacidades no declaradas");
        return string.Join(" · ", labels);
    }

    private static async Task<int> FatalExitAsync(TuiHost host, string headline, string[] details)
    {
        host.SetEstado(headline, ActivityKind.Error);
        host.SetProgreso("—");
        foreach (var detail in details.Where(d => !string.IsNullOrWhiteSpace(d)))
        {
            host.AddActivity(detail!, ActivityKind.Error);
        }

        host.AddActivity("Cierra esta ventana o reintenta con 'condor /preparar' cuando el entorno permita continuar.", ActivityKind.System);
        host.Repaint();

        using var controlInput = new ConsoleControlInputScope();
        try
        {
            Console.ReadKey(intercept: true);
        }
        catch
        {
            await Task.Delay(1500);
        }
        host.Dispose();
        PrintExitBanner();
        return 1;
    }

    private static string[] DescribeBootstrapFailure(DependencyBootstrapResult bootstrap)
    {
        var lines = new List<string>();
        if (bootstrap.Ollama is not null)
        {
            lines.Add("Ollama instalado: " + (bootstrap.Ollama.Health == OllamaHealth.NotInstalled ? "no" : "si") +
                      " · Ollama Server: " + (bootstrap.Ollama.Health == OllamaHealth.ServerAvailable ? "disponible" : "no disponible"));
        }

        if (!string.IsNullOrWhiteSpace(bootstrap.Reason))
        {
            lines.Add("Motivo: " + bootstrap.Reason);
        }

        return lines.ToArray();
    }

    private static bool IsExit(string text)
    {
        return text.Equals("/salir", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("salir", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("/exit", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("/quit", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("quit", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Comentario del usuario: texto entre guiones, p. ej. "-asi de esta manera-".
    /// Se distingue de instrucciones y comandos; nunca dispara ejecucion.
    /// </summary>
    internal static bool EsComentarioUsuario(string text)
    {
        var s = (text ?? "").Trim();
        if (s.Length < 3 || !s.StartsWith('-') || !s.EndsWith('-'))
        {
            return false;
        }

        // El interior debe contener al menos un caracter que no sea guion.
        return s[1..^1].Trim('-').Trim().Length > 0;
    }

    private static string FirstToken(string text)
    {
        var i = text.IndexOf(' ');
        return i < 0 ? text : text[..i];
    }

    internal static IEnumerable<string> SplitLines(string text)
    {
        return text.Replace("\r\n", "\n").Split('\n');
    }

    private static ActivityKind ResultActivityKind(string line)
    {
        var text = line.Trim();
        if (text.StartsWith("He modificado", StringComparison.OrdinalIgnoreCase))
            return ActivityKind.CodeEdit;
        if (text.EndsWith("?", StringComparison.Ordinal))
            return ActivityKind.Question;
        return ActivityKind.Response;
    }

    /// <summary>Motivo corto y legible de una excepcion: nunca stack traces.</summary>
    private static string ShortReason(Exception ex)
    {
        var message = ex.Message.Split('\n')[0].Trim();
        return message.Length == 0 ? ex.GetType().Name : message;
    }

    private static string FormatoTiempo(TimeSpan e)
    {
        return e.TotalHours >= 1
            ? string.Format("{0:00}:{1:00}:{2:00}", (int)e.TotalHours, e.Minutes, e.Seconds)
            : string.Format("{0:00}:{1:00}", e.Minutes, e.Seconds);
    }

    /// <summary>Renderiza la ayuda integrada dentro de la TUI (zona de actividad).</summary>
    private static void RenderHelpInTui(TuiHost host)
    {
        host.AddActivity("", ActivityKind.System);
        host.AddActivity("C O N D O R", ActivityKind.System);
        host.AddActivity("Observa · Comprende · Planifica · Construye · Verifica", ActivityKind.System);
        host.AddActivity(VersionInfo.DisplayName, ActivityKind.System);
        host.AddActivity("", ActivityKind.System);
        host.AddActivity("Condor es un agente de ingenieria. Escribe con palabras la intencion", ActivityKind.System);
        host.AddActivity("y Condor comprende, analiza, selecciona estrategia y modelo, actua con", ActivityKind.System);
        host.AddActivity("herramientas reales, verifica y entrega el resultado.", ActivityKind.System);
        host.AddActivity("", ActivityKind.System);
        host.AddActivity("Uso:", ActivityKind.System);
        host.AddActivity("  condor                       Prepara el entorno y abre la sesion interactiva.", ActivityKind.System);
        host.AddActivity("  condor <tu intencion>        Ejecuta la intencion con el motor agente.", ActivityKind.System);
        host.AddActivity("", ActivityKind.System);
        host.AddActivity("Comandos de control (con /):", ActivityKind.System);
        host.AddActivity("  /analizar                    Analiza el proyecto o directorio actual.", ActivityKind.System);
        host.AddActivity("  /contexto                    Reconstruye el contexto del proyecto.", ActivityKind.System);
        host.AddActivity("  /planear \"<solicitud>\"       Genera un plan de trabajo.", ActivityKind.System);
        host.AddActivity("  /construir                   Aplica los cambios del plan.", ActivityKind.System);
        host.AddActivity("  /verificar                   Comprueba los cambios aplicados.", ActivityKind.System);
        host.AddActivity("  /avanzar \"<solicitud>\"        Ejecuta el ciclo de ingenieria parcial.", ActivityKind.System);
        host.AddActivity("  /examinar \"<imagen>\"         Analiza una imagen localmente.", ActivityKind.System);
        host.AddActivity("  /recomendar \"<tipo>\"         Recomienda un modelo para el equipo.", ActivityKind.System);
        host.AddActivity("  /consultar \"<mensaje>\"       Consulta al modelo local.", ActivityKind.System);
        host.AddActivity("  /verificar-semantico         Compila y ejecuta las pruebas del proyecto.", ActivityKind.System);
        host.AddActivity("  /preparar                    Refresca la preparacion del entorno.", ActivityKind.System);
        host.AddActivity("  /ayuda                       Muestra esta ayuda.", ActivityKind.System);
        host.AddActivity("  /version                     Muestra la version.", ActivityKind.System);
        host.AddActivity("  /salir                       Termina la sesion interactiva.", ActivityKind.System);
        host.AddActivity("", ActivityKind.System);
        host.AddActivity("Contracciones:", ActivityKind.System);
        host.AddActivity("  -v, --version                Muestra la version.", ActivityKind.System);
        host.AddActivity("  -h, --help                   Muestra esta ayuda.", ActivityKind.System);
        host.AddActivity("", ActivityKind.System);
        host.AddActivity("No necesitas conocer modelos, herramientas, fases internas ni rutas.", ActivityKind.System);
        host.AddActivity("Escribe lo que necesitas y Condor se encarga del resto.", ActivityKind.System);
    }

    internal static bool IsExitAccessible(string text) => IsExit(text);
    internal static void RenderHelpInTuiAccessible(TuiHost host) => RenderHelpInTui(host);

    private sealed class ConsoleControlInputScope : IDisposable
    {
        private readonly bool _previousValue = Console.TreatControlCAsInput;

        public ConsoleControlInputScope() => Console.TreatControlCAsInput = true;

        public void Dispose() => Console.TreatControlCAsInput = _previousValue;
    }
}
