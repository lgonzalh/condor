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
using Condor.Infrastructure.Detection;
using Condor.Infrastructure.Documentation;
using Condor.Infrastructure.Llm;
using Condor.Infrastructure.Planning;
using Condor.Infrastructure.Setup;
using Condor.Infrastructure.Vision;

namespace Condor.Cli.Tui;

/// <summary>
/// Sesion interactiva de Condor sobre la TUI persistente. Toda interaccion con
/// modelos locales permanece en esta pantalla; si la terminal no admite TUI,
/// Program informa el requisito y no deriva la solicitud a una consola clasica.
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
        int? tuiHeight = null,
        string? initialInput = null,
        bool sinAnimacion = false)
    {
        var workingDirectory = Environment.CurrentDirectory;
        using var host = tuiWidth.HasValue && tuiHeight.HasValue
            ? new TuiHost(tuiWidth.Value, tuiHeight.Value)
            : new TuiHost();
        using var ticker = new StatusTicker(
            host.SetTickerMessage,
            sinAnimacion: sinAnimacion,
            widthProvider: () => host.TerminalWidth);
        host.Enter();
        host.ShowWelcome();
        host.SetWorkspace(workingDirectory);
        host.Repaint(); // primera imagen inmediata: la TUI aparece sin esperar la preparacion

        var startupOutcome = await PrepareTuiEnvironmentAsync(
            assessmentService, stateStore, session, shutdownToken, host, ticker).ConfigureAwait(true);
        if (startupOutcome.ExitCode is int earlyExitCode)
        {
            return earlyExitCode;
        }

        var bootstrap = startupOutcome.Bootstrap!;
        var prep = startupOutcome.Prep!;

        var agentMode = await ConfigureTuiSessionAsync(
            bootstrap, prep, assessmentService, session, shutdownToken, host).ConfigureAwait(true);

        // ---- Bucle principal: teclado + repintado por regiones ------------------
        using var controlInput = new ConsoleControlInputScope();
        var input = new TuiInput(host);
        var terminalInput = new TuiConsoleInputReader();
        host.Repaint();  // transición limpia: ClearScreen + TUI 2 (Ave) antes de cualquier elemento de TUI 2
        if (string.IsNullOrWhiteSpace(initialInput))
        {
            input.Render();
        }
        else
        {
            input.SetText(initialInput);
        }

        Task<AgentResult>? running = null;
        CancellationTokenSource? runningCts = null;
        var runningWatch = System.Diagnostics.Stopwatch.StartNew();
        var exitCode = 0;
        string? lastHeaderModel = null;
        string? pendingClarificationTask = null;
        string? pendingClarificationQuestion = null;
        AgentSteeringChannel? activeSteering = null;
        Task<MemoryInfo>? lecturaRam = new MemoryDetector().DetectAsync(shutdownToken);
        var proximaLecturaRam = DateTime.UtcNow.AddSeconds(10);

        async Task ProcessFreeIntentionLocalAsync(string text, FreeIntentionRoute free)
        {
            var outcome = await ProcessFreeIntentionAsync(
                text,
                free,
                workingDirectory,
                agentMode.ActiveAgentId,
                pendingClarificationTask,
                pendingClarificationQuestion,
                exitCode,
                runningCts,
                running,
                activeSteering,
                runningWatch,
                assessmentService,
                stateStore,
                session,
                shutdownToken,
                host,
                ticker,
                input).ConfigureAwait(true);
            exitCode = outcome.ExitCode;
            pendingClarificationTask = outcome.PendingClarificationTask;
            pendingClarificationQuestion = outcome.PendingClarificationQuestion;
            runningCts = outcome.RunningCancellation;
            running = outcome.Running;
            activeSteering = outcome.ActiveSteering;
        }
        while (true)
        {
            if (host.HandleResizeIfNeeded())
            {
                input.Render();
            }
            host.Tick();

            var runtimeRefresh = await RefreshRuntimeStatusAsync(
                lecturaRam, proximaLecturaRam, lastHeaderModel, running is not null,
                session, shutdownToken, host).ConfigureAwait(true);
            lecturaRam = runtimeRefresh.MemoryRead;
            proximaLecturaRam = runtimeRefresh.NextMemoryRead;
            lastHeaderModel = runtimeRefresh.LastHeaderModel;

            host.Repaint();

            // Finalizacion de la tarea del agente (marshal al hilo de interfaz).
            if (running is { IsCompleted: true })
            {
                var completion = await CompleteRunningTaskAsync(
                    running, runningWatch, runningCts, pendingClarificationTask, pendingClarificationQuestion,
                    host, ticker, input).ConfigureAwait(true);
                running = completion.Running;
                runningCts = completion.RunningCancellation;
                activeSteering = completion.ActiveSteering;
                pendingClarificationTask = completion.PendingClarificationTask;
                pendingClarificationQuestion = completion.PendingClarificationQuestion;
            }

            if (terminalInput.TryRead(out var inputEvent))
            {
                var key = inputEvent.Key;

                if (inputEvent.Mouse is null &&
                    (key.Modifiers & ConsoleModifiers.Control) != 0 && key.Key == ConsoleKey.C)
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
                    HandleRunningTaskInput(
                        key,
                        inputEvent.Mouse,
                        input,
                        host,
                        runningCts,
                        activeSteering,
                        taskRunning => agentMode.OpenPicker(input, host, taskRunning),
                        taskRunning => agentMode.ApplySelection(input, host, taskRunning));
                    continue; // teclado reservado mientras Condor trabaja
                }

                var submitAction = inputEvent.Mouse is { } clickEvent
                    ? input.HandleMouse(clickEvent)
                    : input.Handle(key);
                var idleInputDisposition = await HandleIdleTaskInputAsync(
                    submitAction,
                    input,
                    host,
                    assessmentService,
                    session,
                    shutdownToken,
                    taskRunning => agentMode.OpenPicker(input, host, taskRunning),
                    taskRunning => agentMode.ApplySelection(input, host, taskRunning),
                    model => agentMode.Reconcile(model, host)).ConfigureAwait(true);
                if (idleInputDisposition == IdleInputDisposition.Continue)
                {
                    continue;
                }

                if (idleInputDisposition == IdleInputDisposition.Exit)
                {
                    break;
                }

                if (submitAction == InputAction.Submit)
                {
                    var text = input.Buffer.Trim();
                    input.Clear();
                    var inputOutcome = await HandleSpecialInputAsync(
                        text,
                        exitCode,
                        assessmentService,
                        session,
                        shutdownToken,
                        host,
                        input,
                        taskRunning => agentMode.OpenPicker(input, host, taskRunning),
                        model => agentMode.Reconcile(model, host))
                        .ConfigureAwait(true);
                    exitCode = inputOutcome.ExitCode;
                    if (inputOutcome.Disposition == SpecialInputDisposition.Exit)
                    {
                        break;
                    }

                    if (inputOutcome.Disposition == SpecialInputDisposition.Continue)
                    {
                        continue;
                    }

                    var route = IntentionRouter.Route(text);
                    switch (route)
                    {
                        case SlashRoute slash:
                            exitCode = await ProcessSlashRouteAsync(
                                text, slash, exitCode, workingDirectory, assessmentService, stateStore, session, shutdownToken, host, ticker, input)
                                .ConfigureAwait(true);
                            break;

                        case FreeIntentionRoute free when !string.IsNullOrWhiteSpace(free.Intention):
                            await ProcessFreeIntentionLocalAsync(text, free).ConfigureAwait(true);
                            break;
                    }
                }
            }
            else
            {
                Thread.Sleep(40);
            }
        }

        return await FinishTuiSessionAsync(exitCode, lecturaRam, runningCts, ticker, host).ConfigureAwait(true);
    }

    private static async Task<int> FinishTuiSessionAsync(
        int exitCode,
        Task<MemoryInfo>? memoryRead,
        CancellationTokenSource? runningCancellation,
        StatusTicker ticker,
        TuiHost host)
    {
        if (memoryRead is not null)
        {
            try
            {
                ActualizarRamLibre(host, await memoryRead.ConfigureAwait(true));
            }
            catch
            {
                host.SetRamLibrePorcentaje(null);
            }
        }

        runningCancellation?.Cancel();
        ticker.Complete(conservar: false);
        await ticker.CompletionTask.ConfigureAwait(true);
        host.Repaint();
        host.Dispose();
        PrintExitBanner();
        return exitCode;
    }

    private static async Task<TuiAgentModeState> ConfigureTuiSessionAsync(
        DependencyBootstrapResult bootstrap,
        StartupPrepResult prep,
        IAssessmentService assessmentService,
        LocalModelSession session,
        CancellationToken shutdownToken,
        TuiHost host)
    {
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
        var state = new TuiAgentModeState(activeAgentId, activeModelInfo, providerName);
        host.SetRuntimeMetadata(AgentModeLabel(state.ActiveAgentId), providerName, "Predeterminada");
        var workspace = bootstrap.Assessment?.WorkingDirectory;
        host.SetWorkspace(string.IsNullOrWhiteSpace(workspace) ? Environment.CurrentDirectory : workspace);
        host.SetEstado("Entorno listo", ActivityKind.Success);
        host.SetProgreso("—");
        return state;
    }

    private sealed class TuiAgentModeState(string activeAgentId, ModelInfo? activeModelInfo, string providerName)
    {
        public string ActiveAgentId { get; private set; } = activeAgentId;

        public void ApplySelection(TuiInput input, TuiHost host, bool taskRunning)
        {
            var selectedAgentId = input.SelectedAgentId;
            if (string.IsNullOrWhiteSpace(selectedAgentId))
            {
                return;
            }

            ActiveAgentId = selectedAgentId;
            var label = AgentModeLabel(ActiveAgentId);
            host.SetRuntimeMetadata(label, providerName, "Predeterminada");
            host.SetEstado(
                taskRunning ? "Modo " + label + " para la siguiente tarea" : "Modo activo: " + label,
                ActivityKind.Success);
        }

        public void Reconcile(ModelInfo? model, TuiHost host)
        {
            activeModelInfo = model;
            var resolvedAgentId = ResolveAgentModeForModel(ActiveAgentId, model);
            if (string.Equals(resolvedAgentId, ActiveAgentId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var previousLabel = AgentModeLabel(ActiveAgentId);
            ActiveAgentId = resolvedAgentId;
            var activeLabel = AgentModeLabel(ActiveAgentId);
            host.SetRuntimeMetadata(activeLabel, providerName, "Predeterminada");
            host.AddActivity(
                $"El modo {previousLabel} no está disponible con el modelo actual; se activó {activeLabel}.",
                ActivityKind.Warning);
        }

        public void OpenPicker(TuiInput input, TuiHost host, bool taskRunning)
        {
            input.BeginAgentPicker(BuildAgentOptions(activeModelInfo), ActiveAgentId);
            host.SetEstado(
                taskRunning ? "Elige el modo para la siguiente tarea" : "Elige el modo de trabajo",
                ActivityKind.System);
        }
    }

    private sealed record TuiStartupOutcome(
        DependencyBootstrapResult? Bootstrap,
        StartupPrepResult? Prep,
        int? ExitCode);

    private static async Task<TuiStartupOutcome> PrepareTuiEnvironmentAsync(
        IAssessmentService assessmentService,
        IStateStore stateStore,
        LocalModelSession session,
        CancellationToken shutdownToken,
        TuiHost host,
        StatusTicker ticker)
    {
        // Corre en segundo plano mientras el bucle de render mantiene visible la TUI.
        var startup = new TuiStartupView(host, ticker);
        var startupBridge = new StartupProgressObserverBridge(startup);
        startup.Start();

        var startupTask = System.Threading.Tasks.Task.Run(async () =>
        {
            host.SetEstado("Preparando dependencias locales");
            var boot = await new DependencyBootstrapper(assessmentService: assessmentService)
                .RunAsync(startupBridge, shutdownToken).ConfigureAwait(false);
            if (!boot.Ready)
            {
                return (Boot: boot, Prep: (StartupPrepResult?)null);
            }

            host.SetEstado("Seleccionando modelo adecuado para el equipo");
            var prep = await new StartupPreparer(
                assessmentService,
                stateStore,
                modelAutoSetup: new ModelAutoSetupService(
                    stateStore, assessmentService, httpClient: session.SharedHttpClient))
                .RunAsync(startupBridge, shutdownToken, cachedAssessment: boot.Assessment).ConfigureAwait(false);
            return (Boot: boot, Prep: (StartupPrepResult?)prep);
        });

        // Bucle de render mínimo mientras arranca: mantiene la pantalla viva.
        while (!startupTask.IsCompleted)
        {
            host.HandleResizeIfNeeded();
            host.Tick();
            host.Repaint();
            Thread.Sleep(40);
        }

        DependencyBootstrapResult bootstrap;
        StartupPrepResult? prep;
        try
        {
            (bootstrap, prep) = await startupTask.ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            ticker.Complete(conservar: false);
            await ticker.CompletionTask.ConfigureAwait(true);
            host.Repaint();
            host.Dispose();
            PrintExitBanner();
            return new TuiStartupOutcome(null, null, 130);
        }

        if (prep is null)
        {
            var exitCode = await FatalExitAsync(
                host,
                ticker,
                "No se pudo dejar el entorno listo automaticamente.",
                DescribeBootstrapFailure(bootstrap));
            return new TuiStartupOutcome(bootstrap, null, exitCode);
        }

        if (!prep.Ready)
        {
            startup.Stop(false);
            var exitCode = await FatalExitAsync(
                host,
                ticker,
                "Condor no puede iniciar: no hay un modelo utilizable ahora.",
                new[] { prep.Reason ?? "Se intento preparar un modelo compatible sin exito." });
            return new TuiStartupOutcome(bootstrap, prep, exitCode);
        }

        startup.Stop(true);
        if (!string.IsNullOrWhiteSpace(prep.Model))
        {
            await session.EnsureAvailableAsync(prep.Model, shutdownToken).ConfigureAwait(true);
        }

        return new TuiStartupOutcome(bootstrap, prep, null);
    }

    private sealed record RuntimeRefreshState(
        Task<MemoryInfo>? MemoryRead,
        DateTime NextMemoryRead,
        string? LastHeaderModel);

    private static async Task<RuntimeRefreshState> RefreshRuntimeStatusAsync(
        Task<MemoryInfo>? memoryRead,
        DateTime nextMemoryRead,
        string? lastHeaderModel,
        bool taskRunning,
        LocalModelSession session,
        CancellationToken shutdownToken,
        TuiHost host)
    {
        if (memoryRead is { IsCompleted: true })
        {
            try
            {
                ActualizarRamLibre(host, await memoryRead.ConfigureAwait(true));
            }
            catch
            {
                host.SetRamLibrePorcentaje(null);
            }

            memoryRead = null;
            nextMemoryRead = DateTime.UtcNow.AddSeconds(10);
        }

        if (memoryRead is null && DateTime.UtcNow >= nextMemoryRead)
        {
            memoryRead = new MemoryDetector().DetectAsync(shutdownToken);
        }

        // Actualizar modelo en cabecera si hay tarea activa y el modelo de sesion cambio.
        if (taskRunning)
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

        return new RuntimeRefreshState(memoryRead, nextMemoryRead, lastHeaderModel);
    }

    private enum IdleInputDisposition
    {
        Continue,
        Exit,
        Submit
    }

    private static async Task<IdleInputDisposition> HandleIdleTaskInputAsync(
        InputAction action,
        TuiInput input,
        TuiHost host,
        IAssessmentService assessmentService,
        LocalModelSession session,
        CancellationToken shutdownToken,
        Action<bool> openAgentPicker,
        Action<bool> applyAgentSelection,
        Action<ModelInfo?> reconcileAgentMode)
    {
        if (action == InputAction.OpenAgents)
        {
            openAgentPicker(false);
        }
        else if (action == InputAction.AgentSelected)
        {
            applyAgentSelection(false);
            host.Repaint();
        }
        else if (action == InputAction.OpenCommands)
        {
            input.BeginCommandPalette();
            host.SetEstado("Paleta de comandos", ActivityKind.System);
        }
        else if (action == InputAction.ModelSelected)
        {
            var selectedModel = input.SelectedModel;
            input.Clear();
            if (!string.IsNullOrWhiteSpace(selectedModel))
            {
                var switched = await SwitchModelAsync(selectedModel, assessmentService, session, host, shutdownToken)
                    .ConfigureAwait(true);
                if (switched)
                {
                    await ReconcileAgentModeAfterModelSwitchAsync(
                            assessmentService, session.ActiveModel, shutdownToken, reconcileAgentMode)
                        .ConfigureAwait(true);
                }
            }
        }
        else if (action == InputAction.Exit)
        {
            return IdleInputDisposition.Exit;
        }
        else if (action != InputAction.Submit)
        {
            return IdleInputDisposition.Continue;
        }

        return action == InputAction.Submit ? IdleInputDisposition.Submit : IdleInputDisposition.Continue;
    }

    private static void HandleRunningTaskInput(
        ConsoleKeyInfo key,
        TuiMouseEvent? mouse,
        TuiInput input,
        TuiHost host,
        CancellationTokenSource? runningCancellation,
        AgentSteeringChannel? activeSteering,
        Action<bool> openAgentPicker,
        Action<bool> applyAgentSelection)
    {
        var action = mouse is { } mouseEvent ? input.HandleMouse(mouseEvent) : input.Handle(key);
        if (action == InputAction.Interrupt)
        {
            // Interrupcion cooperativa de la tarea en curso (Esc + Esc).
            runningCancellation?.Cancel();
            return;
        }

        if (action == InputAction.OpenAgents)
        {
            openAgentPicker(true);
            host.Repaint();
            return;
        }

        if (action == InputAction.AgentSelected)
        {
            applyAgentSelection(true);
            host.Repaint();
            return;
        }

        if (action == InputAction.OpenCommands)
        {
            input.BeginCommandPalette(TuiInput.BusyCommandPaletteCommands);
            host.SetEstado("Comandos disponibles durante la tarea");
            host.Repaint();
            return;
        }

        if (action != InputAction.Submit)
        {
            return;
        }

        var command = input.Buffer.Trim();
        input.Clear();
        if (IsAgentPickerCommand(command))
        {
            openAgentPicker(true);
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
        else if (command.StartsWith("/", StringComparison.Ordinal))
        {
            host.AddActivity("Ese comando no se puede ejecutar mientras la tarea está activa.", ActivityKind.Warning);
            host.SetEstado("Comando reservado hasta que termine la tarea", ActivityKind.Warning);
        }
        else if (!string.IsNullOrWhiteSpace(command))
        {
            activeSteering?.Submit(command);
            host.AddActivity("Requisito enviado al agente: " + command, ActivityKind.User);
            host.SetEstado("El agente actualizará su plan", ActivityKind.System);
        }
        host.Repaint();
    }

    private sealed record RunningTaskCompletion(
        Task<AgentResult>? Running,
        CancellationTokenSource? RunningCancellation,
        AgentSteeringChannel? ActiveSteering,
        string? PendingClarificationTask,
        string? PendingClarificationQuestion);

    private static async Task<RunningTaskCompletion> CompleteRunningTaskAsync(
        Task<AgentResult> finished,
        System.Diagnostics.Stopwatch runningWatch,
        CancellationTokenSource? runningCancellation,
        string? pendingClarificationTask,
        string? pendingClarificationQuestion,
        TuiHost host,
        StatusTicker ticker,
        TuiInput input)
    {
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

        runningCancellation?.Dispose();

        if (result is not null)
        {
            ticker.Publish(result.AwaitingUserInput
                ? "Condor espera una aclaración"
                : result.Success
                ? "Respuesta del modelo recibida"
                : "Interacción con el modelo detenida");
            AddAgentResultToHistory(host, result);
            if (result.AwaitingUserInput)
            {
                pendingClarificationTask = result.Objective;
                pendingClarificationQuestion = result.Reason;
                host.AddActivity("Condor espera tu respuesta para continuar.", ActivityKind.Warning);
                host.SetEstado("Falta una aclaración", ActivityKind.Warning);
            }
            else
            {
                pendingClarificationTask = null;
                pendingClarificationQuestion = null;
                host.SetEstado(result.Success ? "Tarea completada" : "No se pudo completar la tarea",
                    result.Success ? ActivityKind.Success : ActivityKind.Error);
            }
        }
        else if (failure is not null)
        {
            ticker.Publish("No se pudo completar la interacción");
            host.AddActivity("No se pudo completar la tarea: " + failure, ActivityKind.Error);
            host.SetEstado("Error durante la tarea", ActivityKind.Error);
        }
        else
        {
            ticker.Publish("Interacción cancelada");
            host.AddActivity("Tarea cancelada por el usuario.", ActivityKind.Warning);
            host.SetEstado("Tarea cancelada", ActivityKind.Warning);
        }

        host.SetProgreso(FormatoTiempo(runningWatch.Elapsed));
        host.SetBusy(false);
        input.Render();
        return new RunningTaskCompletion(null, null, null, pendingClarificationTask, pendingClarificationQuestion);
    }

    private enum SpecialInputDisposition
    {
        Route,
        Continue,
        Exit
    }

    private sealed record SpecialInputOutcome(SpecialInputDisposition Disposition, int ExitCode);

    private static async Task<SpecialInputOutcome> HandleSpecialInputAsync(
        string text,
        int exitCode,
        IAssessmentService assessmentService,
        LocalModelSession session,
        CancellationToken shutdownToken,
        TuiHost host,
        TuiInput input,
        Action<bool> openAgentPicker,
        Action<ModelInfo?> reconcileAgentMode)
    {
        if (text.Length == 0)
        {
            return new SpecialInputOutcome(SpecialInputDisposition.Continue, exitCode);
        }

        if (IsExit(text))
        {
            return new SpecialInputOutcome(SpecialInputDisposition.Exit, exitCode);
        }

        // Comentario del usuario (-texto-): se registra tal cual y NUNCA
        // se interpreta como instruccion, comando, tarea o accion.
        if (EsComentarioUsuario(text))
        {
            host.AddActivity(text, ActivityKind.User);
            host.SetEstado("Comentario registrado", ActivityKind.System);
            host.SetProgreso("—");
            input.Render();
            return new SpecialInputOutcome(SpecialInputDisposition.Continue, exitCode);
        }

        if (IsAgentPickerCommand(text))
        {
            openAgentPicker(false);
            return new SpecialInputOutcome(SpecialInputDisposition.Continue, exitCode);
        }

        if (text.StartsWith("/comparar-modelos", StringComparison.OrdinalIgnoreCase))
        {
            host.AddActivity(text, ActivityKind.User);
            if (!TryParseModelComparisonCommand(text, out var firstModel, out var secondModel, out var comparisonTask))
            {
                host.AddActivity(
                    "Uso: /comparar-modelos <modelo1> <modelo2> -- <tarea de solo lectura>",
                    ActivityKind.Warning);
                host.SetEstado("Faltan modelos o tarea", ActivityKind.Warning);
            }
            else
            {
                exitCode = await CompareModelsInTuiAsync(
                    host, assessmentService, session, firstModel, secondModel, comparisonTask, shutdownToken)
                    .ConfigureAwait(true);
            }
            input.Render();
            return new SpecialInputOutcome(SpecialInputDisposition.Continue, exitCode);
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
                var loadedModels = await session.GetLoadedModelNamesAsync(shutdownToken).ConfigureAwait(true);
                input.BeginModelPicker(
                    models,
                    DescribeModelCapabilities,
                    model => loadedModels.Contains(model.Name) || ModelFitsCurrentMemory(model, inventory.Memory, out _),
                    session.ActiveModel);
                host.SetEstado("Selecciona un modelo local", ActivityKind.System);
            }
            return new SpecialInputOutcome(SpecialInputDisposition.Continue, exitCode);
        }

        if (TryReadModelArgument(text, out var modelArgument))
        {
            if (!string.IsNullOrWhiteSpace(modelArgument))
            {
                var switched = await SwitchModelAsync(
                        modelArgument, assessmentService, session, host, shutdownToken)
                    .ConfigureAwait(true);
                if (switched)
                {
                    await ReconcileAgentModeAfterModelSwitchAsync(
                            assessmentService, session.ActiveModel, shutdownToken, reconcileAgentMode)
                        .ConfigureAwait(true);
                }
            }
            return new SpecialInputOutcome(SpecialInputDisposition.Continue, exitCode);
        }

        return new SpecialInputOutcome(SpecialInputDisposition.Route, exitCode);
    }

    private static async Task<int> ProcessSlashRouteAsync(
        string text,
        SlashRoute slash,
        int exitCode,
        string workingDirectory,
        IAssessmentService assessmentService,
        IStateStore stateStore,
        LocalModelSession session,
        CancellationToken shutdownToken,
        TuiHost host,
        StatusTicker ticker,
        TuiInput input)
    {
        host.AddActivity(text, ActivityKind.User);
        if (slash.Kind == SlashCommandKind.Ayuda)
        {
            RenderHelpInTui(host);
            host.SetEstado("Listo", ActivityKind.Success);
            host.SetProgreso("—");
        }
        else if (slash.Kind == SlashCommandKind.Consultar)
        {
            exitCode = await ConsultInTuiAsync(
                host, session.Llm, session.ActiveModel, slash.Arguments, shutdownToken, ticker);
        }
        else if (slash.Kind == SlashCommandKind.Examinar)
        {
            exitCode = await ExamineInTuiAsync(
                host,
                stateStore,
                new VisionService(stateStore, session: session),
                session.ActiveModel,
                slash.Arguments,
                shutdownToken,
                ticker);
        }
        else if (slash.Kind == SlashCommandKind.Planear)
        {
            exitCode = await PlanInTuiAsync(
                host,
                new PlanService(stateStore, workingDirectory: workingDirectory),
                stateStore,
                slash.Arguments,
                shutdownToken,
                ticker);
        }
        else
        {
            // Los demas comandos "/" conservan su presentacion establecida:
            // la pantalla TUI se suspende y retoma intacta.
            host.SetEstado("Ejecutando " + FirstToken(text));
            await host.SuspendAsync(async () =>
                exitCode = await Program.HandleSlashAsync(slash, assessmentService, stateStore, session.Llm, session, workingDirectory)).ConfigureAwait(true);
            host.SetEstado("Listo", ActivityKind.Success);
            host.SetProgreso("—");
        }

        input.Render();
        return exitCode;
    }

    private sealed record FreeIntentionOutcome(
        int ExitCode,
        CancellationTokenSource? RunningCancellation,
        Task<AgentResult>? Running,
        AgentSteeringChannel? ActiveSteering,
        string? PendingClarificationTask,
        string? PendingClarificationQuestion);

    private static async Task<FreeIntentionOutcome> ProcessFreeIntentionAsync(
        string text,
        FreeIntentionRoute free,
        string workingDirectory,
        string activeAgentId,
        string? pendingClarificationTask,
        string? pendingClarificationQuestion,
        int exitCode,
        CancellationTokenSource? runningCts,
        Task<AgentResult>? running,
        AgentSteeringChannel? activeSteering,
        System.Diagnostics.Stopwatch runningWatch,
        IAssessmentService assessmentService,
        IStateStore stateStore,
        LocalModelSession session,
        CancellationToken shutdownToken,
        TuiHost host,
        StatusTicker ticker,
        TuiInput input)
    {
        host.AddActivity(text, ActivityKind.User);
        var requestedIntention = free.Intention;
        if (!string.IsNullOrWhiteSpace(pendingClarificationTask))
        {
            requestedIntention = "Solicitud original: " + pendingClarificationTask +
                "\nAclaración solicitada por Condor: " + (pendingClarificationQuestion ?? "") +
                "\nRespuesta del usuario: " + free.Intention +
                "\nContinúa la tarea usando esta respuesta, vuelve a observar el proyecto y actualiza el plan antes de nuevas escrituras.";
            pendingClarificationTask = null;
            pendingClarificationQuestion = null;
        }

        var modeRoute = RouteAgentMode(activeAgentId, requestedIntention, session.ActiveModel);
        if (modeRoute is SlashRoute modeSlash)
        {
            if (modeSlash.Kind == SlashCommandKind.Consultar)
            {
                exitCode = await ConsultInTuiAsync(
                    host, session.Llm, session.ActiveModel, modeSlash.Arguments, shutdownToken, ticker);
                input.Render();
            }
            else if (modeSlash.Kind == SlashCommandKind.Examinar)
            {
                exitCode = await ExamineInTuiAsync(
                    host,
                    stateStore,
                    new VisionService(stateStore, session: session),
                    session.ActiveModel,
                    modeSlash.Arguments,
                    shutdownToken,
                    ticker);
                input.Render();
            }
            else if (modeSlash.Kind == SlashCommandKind.Planear)
            {
                exitCode = await PlanInTuiAsync(
                    host,
                    new PlanService(stateStore, workingDirectory: Environment.CurrentDirectory),
                    stateStore,
                    modeSlash.Arguments,
                    shutdownToken,
                    ticker);
                input.Render();
            }
            else
            {
                host.SetBusy(true);
                ticker.Publish("Condor procesa la solicitud");
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
                            modeSlash, assessmentService, stateStore, session.Llm, session, workingDirectory)).ConfigureAwait(true);
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
            }

            return new FreeIntentionOutcome(
                exitCode, runningCts, running, activeSteering, pendingClarificationTask, pendingClarificationQuestion);
        }

        host.SetBusy(true);
        ticker.Publish("Condor comprende la solicitud");
        host.SetEstado("Preparando análisis de la tarea");
        host.SetProgreso("0 s");
        host.SetModelStatus(
            (string.IsNullOrWhiteSpace(session.ActiveModel) ? "Modelo local" : session.ActiveModel) + " · preparando análisis",
            working: true);
        runningCts = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
        var agentBridge = new AgentProgressObserverBridge(new TuiAgentProgressView(host, ticker));
        var agent = new DocumentingAgentService(
            new AgentService(
                stateStore,
                assessmentService,
                session: session,
                workingDirectoryProvider: () => workingDirectory,
                steeringChannel: activeSteering = new AgentSteeringChannel()),
            new DocumenterService(),
            workingDirectoryProvider: () => workingDirectory);
        runningWatch.Restart();
        var token = runningCts.Token;
        running = Task.Run(() => ExecuteAgentRouteAsync(agent, modeRoute, agentBridge, token));
        return new FreeIntentionOutcome(
            exitCode, runningCts, running, activeSteering, pendingClarificationTask, pendingClarificationQuestion);
    }

    internal static async Task<int> ConsultInTuiAsync(
        TuiHost host,
        ILlmClient llmClient,
        string? activeModel,
        string[] args,
        CancellationToken cancellationToken,
        StatusTicker? ticker = null)
    {
        var queryArgs = args.ToList();
        var modelOption = queryArgs.FindIndex(argument =>
            argument.Equals("--modelo", StringComparison.OrdinalIgnoreCase));
        if (modelOption >= 0)
        {
            var requestedModel = modelOption + 1 < queryArgs.Count
                ? queryArgs[modelOption + 1]
                : null;
            if (!string.Equals(requestedModel, activeModel, StringComparison.OrdinalIgnoreCase))
            {
                host.AddActivity(
                    "La consulta usa el modelo activo. Para cambiarlo, selecciona otro desde /modelo.",
                    ActivityKind.Warning);
                host.SetEstado("Selecciona el modelo dentro de la TUI", ActivityKind.Warning);
                return 1;
            }

            // El modo Preguntar incluye el modelo activo en la ruta; se valida
            // contra la seleccion actual y se retira antes de formar el prompt.
            queryArgs.RemoveAt(modelOption + 1);
            queryArgs.RemoveAt(modelOption);
        }

        var prompt = string.Join(" ", queryArgs).Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            host.AddActivity("Uso: /consultar <mensaje>", ActivityKind.Warning);
            host.SetEstado("Falta el mensaje para consultar", ActivityKind.Warning);
            return 1;
        }

        if (string.IsNullOrWhiteSpace(activeModel))
        {
            host.AddActivity("No hay un modelo local activo para esta consulta.", ActivityKind.Error);
            host.SetEstado("Selecciona un modelo local", ActivityKind.Error);
            return 1;
        }

        host.SetBusy(true);
        ticker?.Publish("Condor piensa con el modelo activo");
        host.SetEstado("Consultando al modelo activo", ActivityKind.System);
        host.SetModelStatus(activeModel + " · consultando", working: true);
        host.AddActivity("Condor consulta al modelo local...", ActivityKind.System);
        host.Repaint();
        var watch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var pendingResponse = llmClient.CompleteAsync(
                new LlmRequest { Model = activeModel, Prompt = prompt },
                cancellationToken);
            var response = await WaitWithTuiAsync(host, pendingResponse).ConfigureAwait(true);

            if (!response.Success)
            {
                host.AddActivity(response.Error ?? "El modelo local no devolvio una respuesta valida.", ActivityKind.Error);
                host.SetEstado("No se pudo completar la consulta", ActivityKind.Error);
                return 1;
            }

            var content = response.Content ?? "";
            if (string.IsNullOrWhiteSpace(content))
            {
                host.AddActivity("El modelo no devolvió contenido de texto para esta consulta.", ActivityKind.Error);
                host.SetEstado("El modelo no devolvió una respuesta", ActivityKind.Error);
                return 1;
            }

            foreach (var line in SplitLines(content))
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    host.AddActivity(line, ActivityKind.Response);
                }
            }

            ticker?.Publish("Respuesta del modelo recibida");
            host.SetEstado("Consulta completada", ActivityKind.Success);
            return 0;
        }
        catch (OperationCanceledException)
        {
            ticker?.Publish("Consulta cancelada");
            host.AddActivity("Consulta cancelada.", ActivityKind.Warning);
            host.SetEstado("Consulta cancelada", ActivityKind.Warning);
            return 130;
        }
        catch (Exception ex)
        {
            ticker?.Publish("No se pudo obtener la respuesta del modelo");
            host.AddActivity("No se pudo consultar el modelo: " + ShortReason(ex), ActivityKind.Error);
            host.SetEstado("Fallo en la consulta", ActivityKind.Error);
            return 1;
        }
        finally
        {
            watch.Stop();
            host.SetBusy(false);
            host.SetProgreso(FormatoTiempo(watch.Elapsed));
            host.SetModelStatus(activeModel + " · listo", working: false);
        }
    }

    internal static async Task<int> PlanInTuiAsync(
        TuiHost host,
        IPlanService planService,
        IStateStore stateStore,
        string[] args,
        CancellationToken cancellationToken,
        StatusTicker? ticker = null)
    {
        var request = string.Join(" ", args).Trim();
        if (string.IsNullOrWhiteSpace(request))
        {
            host.AddActivity("Uso: /planear <objetivo>", ActivityKind.Warning);
            host.SetEstado("Falta el objetivo del plan", ActivityKind.Warning);
            return 1;
        }

        host.SetBusy(true);
        ticker?.Publish("Condor piensa en el plan de trabajo");
        host.SetEstado("Creando plan con Condor", ActivityKind.System);
        host.SetProgreso("—");

        try
        {
            var plan = await WaitWithTuiAsync(host, planService.BuildPlanAsync(request, cancellationToken))
                .ConfigureAwait(true);
            await stateStore.SavePlanAsync(plan, cancellationToken).ConfigureAwait(true);
            ticker?.Publish("Respuesta del modelo recibida");
            return RenderPlanInTui(host, plan);
        }
        catch (OperationCanceledException)
        {
            ticker?.Publish("Creación del plan cancelada");
            host.AddActivity("Creación del plan cancelada.", ActivityKind.Warning);
            host.SetEstado("Plan cancelado", ActivityKind.Warning);
            return 130;
        }
        catch (Exception ex)
        {
            ticker?.Publish("No se pudo completar el plan");
            host.AddActivity("No se pudo crear el plan: " + ShortReason(ex), ActivityKind.Error);
            host.SetEstado("No se pudo crear el plan", ActivityKind.Error);
            return 1;
        }
        finally
        {
            host.SetBusy(false);
            host.SetProgreso("—");
        }
    }

    internal static int RenderPlanInTui(TuiHost host, WorkPlan plan)
    {
        var succeeded = plan.Status != DetectionStatus.NotDetected;
        var resultKind = succeeded ? ActivityKind.Response : ActivityKind.Error;
        host.AddActivity(succeeded ? "Condor generó el plan de trabajo." : "No hay plan disponible.", resultKind);
        host.AddActivity("PLAN", ActivityKind.System);
        host.AddActivity("Estado: " + (plan.Status switch
        {
            DetectionStatus.Detected => "detectado",
            DetectionStatus.NotDetected => "no detectado",
            DetectionStatus.Limited => "limitado",
            _ => "error"
        }), resultKind);
        host.AddActivity("Proyecto: " + (string.IsNullOrWhiteSpace(plan.RootName)
            ? "(sin proyecto)"
            : plan.RootName + " - " + plan.WorkingDirectory), ActivityKind.System);
        host.AddActivity("Intención: " + plan.Intention, ActivityKind.System);
        host.AddActivity("Objetivo: " + (string.IsNullOrWhiteSpace(plan.Objective) ? "(sin objetivo)" : plan.Objective), ActivityKind.System);
        host.AddActivity("Tareas: " + (plan.Tasks.Count > 0 ? plan.Tasks.Count.ToString() : "ninguna"), ActivityKind.System);

        foreach (var task in plan.Tasks)
        {
            var depends = task.DependsOn.Count > 0 ? " (después de " + string.Join(", ", task.DependsOn) + ")" : "";
            host.AddActivity("- [" + task.Priority + "] " + task.Id + " " + task.Title + depends, ActivityKind.Response);
            if (!string.IsNullOrWhiteSpace(task.Detail))
            {
                host.AddActivity("  detalle: " + task.Detail, ActivityKind.System);
            }
        }

        host.AddActivity("Recomendaciones: " + (plan.Evidence.Count > 0 ? plan.Evidence.Count.ToString() : "ninguna"), ActivityKind.System);
        host.AddActivity("Riesgos: " + (plan.RisksConsidered.Count > 0 ? plan.RisksConsidered.Count.ToString() : "ninguno"), ActivityKind.System);
        host.AddActivity("Límites aplicados: " + (plan.LimitsApplied.Count > 0
            ? string.Join(", ", plan.LimitsApplied)
            : "ninguno"), ActivityKind.System);

        if (plan.Status != DetectionStatus.Detected && !string.IsNullOrWhiteSpace(plan.Reason))
        {
            host.AddActivity("Plan: " + plan.Reason, succeeded ? ActivityKind.Warning : ActivityKind.Error);
        }

        host.SetEstado(succeeded ? "Plan creado" : "No hay plan disponible",
            succeeded ? ActivityKind.Success : ActivityKind.Error);
        return succeeded ? 0 : 1;
    }

    private static async Task<T> WaitWithTuiAsync<T>(TuiHost host, Task<T> operation)
    {
        while (!operation.IsCompleted)
        {
            host.HandleResizeIfNeeded();
            host.Tick();
            host.Repaint();
            await Task.WhenAny(operation, Task.Delay(40)).ConfigureAwait(true);
        }

        return await operation.ConfigureAwait(true);
    }

    private static async Task<int> ExamineInTuiAsync(
        TuiHost host,
        IStateStore stateStore,
        IVisionService visionService,
        string? activeModel,
        string[] args,
        CancellationToken cancellationToken,
        StatusTicker? ticker = null)
    {
        var imagePath = string.Join(" ", args.Where(argument =>
            !argument.Equals("--json", StringComparison.OrdinalIgnoreCase))).Trim();
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            host.AddActivity("Uso: /examinar <ruta de imagen>", ActivityKind.Warning);
            host.SetEstado("Falta la ruta de la imagen", ActivityKind.Warning);
            return 1;
        }

        host.SetBusy(true);
        ticker?.Publish("Condor examina la imagen con el modelo local");
        host.SetEstado("Examinando imagen con el modelo local", ActivityKind.System);
        host.SetModelStatus((activeModel ?? "Modelo local") + " · examinando imagen", working: true);
        host.AddActivity("Condor examina la imagen con el modelo local...", ActivityKind.System);
        host.Repaint();
        var watch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var result = await visionService.ExamineAsync(imagePath, cancellationToken).ConfigureAwait(true);
            await stateStore.SaveVisionAsync(result, cancellationToken).ConfigureAwait(true);
            ticker?.Publish("Respuesta del modelo recibida");
            return RenderVisionResultInTui(host, result, imagePath);
        }
        catch (OperationCanceledException)
        {
            ticker?.Publish("Examen de imagen cancelado");
            host.AddActivity("Examen de imagen cancelado.", ActivityKind.Warning);
            host.SetEstado("Examen cancelado", ActivityKind.Warning);
            return 130;
        }
        catch (Exception ex)
        {
            ticker?.Publish("No se pudo obtener la respuesta del modelo");
            host.AddActivity("No se pudo examinar la imagen: " + ShortReason(ex), ActivityKind.Error);
            host.SetEstado("Fallo al examinar la imagen", ActivityKind.Error);
            return 1;
        }
        finally
        {
            watch.Stop();
            host.SetBusy(false);
            host.SetProgreso(FormatoTiempo(watch.Elapsed));
            host.SetModelStatus((activeModel ?? "Modelo local") + " · listo", working: false);
        }
    }

    internal static int RenderVisionResultInTui(TuiHost host, VisionResult result, string fallbackImagePath)
    {
        var succeeded = result.Status == DetectionStatus.Detected;
        host.AddActivity(
            succeeded ? "Condor examinó la imagen." : "Condor no pudo examinar la imagen.",
            succeeded ? ActivityKind.Success : ActivityKind.Warning);
        host.AddActivity(
            "Imagen: " + (string.IsNullOrWhiteSpace(result.ImagePath) ? fallbackImagePath : result.ImagePath),
            ActivityKind.System);
        if (!string.IsNullOrWhiteSpace(result.ModelUsed))
        {
            host.AddActivity("Modelo: " + result.ModelUsed, ActivityKind.System);
        }

        foreach (var line in SplitLines(result.Description ?? ""))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                host.AddActivity(line, succeeded ? ActivityKind.Response : ActivityKind.Warning);
            }
        }

        if (!string.IsNullOrWhiteSpace(result.Reason))
        {
            host.AddActivity(result.Reason, ActivityKind.Warning);
        }

        foreach (var limit in result.LimitsApplied)
        {
            host.AddActivity("Limite aplicado: " + limit, ActivityKind.Warning);
        }

        host.SetEstado(succeeded ? "Examen completado" : "Examen limitado", succeeded ? ActivityKind.Success : ActivityKind.Warning);
        return succeeded ? 0 : 1;
    }

    internal const string ExitThanksLine = "Gracias por usar ©Condor 2026.";

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

        Console.WriteLine(Ansi.FgBlanco + ExitThanksLine + Ansi.Reset);
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

    internal static bool TryParseModelComparisonCommand(
        string text,
        out string firstModel,
        out string secondModel,
        out string task)
    {
        firstModel = "";
        secondModel = "";
        task = "";
        const string command = "/comparar-modelos";
        if (!text.StartsWith(command, StringComparison.OrdinalIgnoreCase)) return false;

        var arguments = text[command.Length..].TrimStart();
        var separator = arguments.IndexOf(" -- ", StringComparison.Ordinal);
        if (separator < 0) return false;
        var models = arguments[..separator].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        task = arguments[(separator + 4)..].Trim();
        if (models.Length != 2 || string.IsNullOrWhiteSpace(task)) return false;
        firstModel = models[0];
        secondModel = models[1];
        return true;
    }

    internal static bool TryParseComparisonModel(string value, out string providerId, out string model)
    {
        providerId = "ollama";
        model = value.Trim();
        var separator = model.IndexOf("::", StringComparison.Ordinal);
        if (separator < 0) return model.Length > 0;
        providerId = model[..separator].Trim().ToLowerInvariant();
        model = model[(separator + 2)..].Trim();
        return providerId.Length > 0 && model.Length > 0;
    }

    private static async Task<int> CompareModelsInTuiAsync(
        TuiHost host,
        IAssessmentService assessmentService,
        LocalModelSession session,
        string firstModel,
        string secondModel,
        string task,
        CancellationToken cancellationToken)
    {
        var previousModel = session.ActiveModel;
        try
        {
            using var providers = ModelComparisonProviderCatalog.CreateDefault(session);
            if (!TryParseComparisonModel(firstModel, out var firstProviderId, out var firstModelName) ||
                !TryParseComparisonModel(secondModel, out var secondProviderId, out var secondModelName) ||
                !providers.TryGet(firstProviderId, out var firstProvider) ||
                !providers.TryGet(secondProviderId, out var secondProvider))
            {
                host.AddActivity(
                    "Proveedor no configurado. Ollama está disponible por defecto; openai-compatible requiere " +
                    ModelComparisonProviderCatalog.OpenAiCompatibleBaseUrlVariable + ".",
                    ActivityKind.Warning);
                host.SetEstado("Proveedor de comparación no disponible", ActivityKind.Warning);
                return 1;
            }

            host.SetBusy(true);
            host.SetEstado("Comparando modelos");
            host.SetProgreso("Misma tarea · solicitudes secuenciales · sin ediciones");
            var service = new ModelComparisonService();
            var targets = new[]
            {
                new ModelComparisonTarget(firstProvider.Id, firstModelName, firstProvider.Client),
                new ModelComparisonTarget(secondProvider.Id, secondModelName, secondProvider.Client)
            };
            var report = await service.CompareAsync(
                task,
                targets,
                async (target, token) =>
                {
                    if (!providers.TryGet(target.Provider, out var provider))
                        return "Proveedor no registrado: " + target.Provider + ".";

                    if (provider.RequiresLocalModelSelection)
                    {
                        var inventory = await LoadInstalledModelsAsync(assessmentService, token).ConfigureAwait(true);
                        var candidate = inventory.Models.FirstOrDefault(model =>
                            model.Name.Equals(target.Model, StringComparison.OrdinalIgnoreCase));
                        if (candidate is null)
                        {
                            var unavailable = "El modelo " + target.Model + " no aparece en Ollama.";
                            host.AddActivity(unavailable, ActivityKind.Warning);
                            return unavailable;
                        }
                        // SwitchModelAsync libera primero el modelo activo y vuelve a
                        // medir la RAM. Un chequeo aquí contaría el modelo anterior
                        // como residente y rechazaría comparaciones secuenciales válidas.
                        var selected = await SwitchModelAsync(
                            target.Model, assessmentService, session, host, token).ConfigureAwait(true);
                        if (!selected)
                            return "Condor no pudo activar el modelo local " + target.Model +
                                   " después de la comprobación de recursos.";
                    }
                    else if (provider.Client is ILlmProviderDiagnostics diagnostics &&
                             !await diagnostics.IsAvailableAsync(token).ConfigureAwait(true))
                    {
                        return "El proveedor " + provider.Name + " no responde en su URL configurada.";
                    }

                    host.SetBusy(true);
                    host.SetEstado("Consultando " + provider.Name + " · " + target.Model);
                    host.SetProgreso("Mismo prompt · respuesta de solo lectura");
                    host.SetModelStatus(provider.Name + " · " + target.Model + " · comparando", working: true);
                    return null;
                },
                cancellationToken).ConfigureAwait(true);

            host.AddActivity(report.ReportPath, ActivityKind.Response);
            var succeeded = report.Results.Count(result => result.Success);
            host.SetEstado(succeeded == report.Results.Count ? "Comparación guardada" : "Comparación parcial guardada",
                succeeded == report.Results.Count ? ActivityKind.Success : ActivityKind.Warning);
            return succeeded > 0 ? 0 : 1;
        }
        catch (OperationCanceledException)
        {
            host.AddActivity("Comparación cancelada.", ActivityKind.Warning);
            host.SetEstado("Comparación cancelada", ActivityKind.Warning);
            return 1;
        }
        catch (Exception exception)
        {
            host.AddActivity("No se pudo completar la comparación: " + ShortReason(exception), ActivityKind.Error);
            host.SetEstado("Error al comparar modelos", ActivityKind.Error);
            return 1;
        }
        finally
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(previousModel))
                {
                    var restored = await SwitchModelAsync(
                        previousModel, assessmentService, session, host, CancellationToken.None).ConfigureAwait(true);
                    if (!restored)
                        host.AddActivity("El informe se guardó, pero Condor no pudo restaurar el modelo activo anterior.", ActivityKind.Warning);
                }
            }
            catch
            {
                // Restaurar el modelo anterior nunca debe ocultar el resultado del informe.
            }

            host.SetBusy(false);
            host.SetProgreso("—");
            var activeModel = session.ActiveModel;
            host.SetModelStatus(
                string.IsNullOrWhiteSpace(activeModel) ? "Modelo local · sin cambios" : activeModel + " · listo",
                working: false);
            host.Repaint();
        }
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
                Array.Empty<string>(), Disabled: false, DisabledReason: null, Shortcut: "Enter"),
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

    internal static async Task ReconcileAgentModeAfterModelSwitchAsync(
        IAssessmentService assessmentService,
        string? activeModel,
        CancellationToken cancellationToken,
        Action<ModelInfo?> reconcileAgentMode)
    {
        var assessment = await ReadAssessmentAsync(assessmentService, cancellationToken).ConfigureAwait(true);
        var model = string.IsNullOrWhiteSpace(activeModel)
            ? null
            : FindInstalledModel(assessment, activeModel);
        reconcileAgentMode(model);
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

    internal static Task<AgentResult> ExecuteAgentRouteAsync(
        IAgentService agent,
        IntentionRoute route,
        IAgentProgressObserver? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(route);
        if (route is not FreeIntentionRoute freeIntention)
        {
            throw new ArgumentException("La ruta seleccionada no corresponde a una tarea del agente con herramientas.", nameof(route));
        }

        return agent.RunAsync(freeIntention.Intention, progress, cancellationToken);
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
            var loadedModels = model is null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : await session.GetLoadedModelNamesAsync(cancellationToken).ConfigureAwait(true);
            var modelAlreadyLoaded = model is not null && loadedModels.Contains(model.Name);
            string? reason = null;
            if (model is null || (!modelAlreadyLoaded &&
                !ModelFitsCurrentMemory(model, freshAssessment?.Environment?.Memory, out reason)))
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
            host.SetEstado(modelAlreadyLoaded ? "Modelo reutilizado" : "Modelo seleccionado", ActivityKind.Success);
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

    private static async Task<int> FatalExitAsync(TuiHost host, StatusTicker ticker, string headline, string[] details)
    {
        ticker.Complete(conservar: false);
        await ticker.CompletionTask.ConfigureAwait(true);
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

    internal static void AddAgentResultToHistory(TuiHost host, AgentResult result)
    {
        var model = string.IsNullOrWhiteSpace(result.Model) ? "no identificado" : result.Model;
        host.AddActivity("Modelo usado: " + model, ActivityKind.System);

        foreach (var line in SplitLines(AgentRenderer.BuildResultText(result)))
        {
            if (IsInternalResultLine(line))
            {
                continue;
            }

            host.AddActivity(line, result.Success ? ResultActivityKind(line) : ActivityKind.Error);
        }
    }

    private static bool IsInternalResultLine(string line)
    {
        var text = line.TrimStart();
        return text.StartsWith("Tarea:", StringComparison.OrdinalIgnoreCase) ||
               text.StartsWith("Contexto del entorno:", StringComparison.OrdinalIgnoreCase) ||
               text.StartsWith("©Condor", StringComparison.OrdinalIgnoreCase);
    }

    private static void ActualizarRamLibre(TuiHost host, MemoryInfo memory)
    {
        if (memory.Status != DetectionStatus.Detected || memory.TotalBytes <= 0)
        {
            host.SetRamLibrePorcentaje(null);
            return;
        }

        var porcentaje = (int)Math.Round(
            memory.FreeBytes * 100d / memory.TotalBytes,
            MidpointRounding.AwayFromZero);
        host.SetRamLibrePorcentaje(porcentaje);
    }

    /// <summary>Motivo corto y legible de una excepcion: nunca stack traces.</summary>
    private static string ShortReason(Exception ex)
    {
        var message = ex.Message.Split('\n')[0].Trim();
        return message.Length == 0 ? ex.GetType().Name : message;
    }

    internal static string FormatoTiempo(TimeSpan e)
    {
        return e.TotalSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s";
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
        host.AddActivity("  /comparar-modelos [proveedor::]m1 [proveedor::]m2 -- <tarea>", ActivityKind.System);
        host.AddActivity("    Ollama es predeterminado; OpenAI-compatible usa URL configurada y solo la tarea.", ActivityKind.System);
        host.AddActivity("  /verificar-semantico         Compila y ejecuta las pruebas del proyecto.", ActivityKind.System);
        host.AddActivity("  /preparar                    Refresca la preparacion del entorno.", ActivityKind.System);
        host.AddActivity("  /metricas                    Consulta o registra métricas manuales de entrega.", ActivityKind.System);
        host.AddActivity("  /evaluar-revisor             Compara el revisor con casos conocidos.", ActivityKind.System);
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
