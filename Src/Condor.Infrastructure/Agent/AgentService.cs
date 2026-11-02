using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Condor.Core.Agent;
using Condor.Core.Contracts;
using Condor.Core.Evaluation;
using Condor.Core.Models;
using Condor.Infrastructure.Detection;
using Condor.Infrastructure.Llm;
using Condor.Infrastructure.Setup;

namespace Condor.Infrastructure.Agent;

public sealed class AgentService : IAgentService
{
    private readonly IStateStore _stateStore;
    private readonly IAssessmentService? _assessmentService;
    private readonly ILlmClient _llm;
    private readonly ILlmProviderDiagnostics _provider;
    private readonly AgentLimits _limits;
    private readonly LocalModelSession? _session;
    private readonly Func<string> _workingDirectoryProvider;
    private readonly ModelAutoSetupService? _modelAutoSetup;

    public AgentService(
        IStateStore stateStore,
        IAssessmentService? assessmentService = null,
        AgentLimits? limits = null,
        ILlmClient? llm = null,
        ILlmProviderDiagnostics? provider = null,
        LocalModelSession? session = null,
        Func<string>? workingDirectoryProvider = null,
        ModelAutoSetupService? modelAutoSetup = null)
    {
        _stateStore = stateStore;
        _assessmentService = assessmentService;
        _session = session;
        _workingDirectoryProvider = workingDirectoryProvider ?? (() => Environment.CurrentDirectory);
        _modelAutoSetup = modelAutoSetup;
        if (session is not null)
        {
            // Sesion compartida: un unico proveedor/HttpClient para toda la ejecucion.
            _llm = llm ?? session.Llm;
            _provider = provider ?? session.Diagnostics;
        }
        else
        {
            _llm = llm ?? new OllamaClient();
            _provider = provider ?? (_llm as ILlmProviderDiagnostics) ?? new OllamaClient();
        }
        _limits = limits ?? AgentLimits.Default;
    }

    /// <summary>
    /// Crea el auto-setup de modelos reutilizando el HttpClient de la sesion
    /// compartida cuando existe, para no duplicar conectores por tarea.
    /// </summary>
    private ModelAutoSetupService CreateModelSetup()
        => _modelAutoSetup ?? new(_stateStore, _assessmentService, httpClient: _session?.SharedHttpClient);

    public async Task<AgentResult> RunAsync(string intention, IAgentProgressObserver? progress = null, CancellationToken cancellationToken = default)
    {
        var checkpoint = new AgentCheckpoint { Task = intention, GeneratedAtUtc = DateTime.UtcNow };
        var steps = new List<AgentStep>();
        var modifications = 0;
        var effectiveMaxIterations = _limits.MaxIterations;
        var planBUsed = false;
        string? planBInitialFailure = null;
        var awaitingPlanBAction = false;

        var workingDir = _workingDirectoryProvider();
        if (string.IsNullOrWhiteSpace(workingDir) || !Directory.Exists(workingDir))
        {
            return Fail("No hay un directorio de trabajo util. Ejecuta desde el proyecto.", "", intention, steps, checkpoint);
        }

        // Clasificar la tarea -> requisito de modelo (que capacidades y eficiencia).
        var requirement = TaskIntentClassifier.Classify(intention);

        // Una consulta conversacional no es una tarea de ingenieria: no debe
        // forzar al modelo minimo a producir una accion JSON que no necesita.
        // Esto permite que una entrada simple como \"hola\" use el modelo local
        // mas pequeno y adecuado, y evita confundir una respuesta de texto
        // valida con un fallo del protocolo de herramientas.
        if (requirement.IntentKind == TaskIntentKinds.Consult)
        {
            return await RunDirectConsultAsync(intention, requirement, progress, cancellationToken);
        }

// Seleccion automatica del modelo (harness dinamico por tarea + presupuesto).
        var modelSetup = CreateModelSetup();
        var lastBudgetGb = _session?.LastSelectionBudgetGb ?? 0;
        var lastFreeRamGb = _session?.LastSelectionFreeRamGb ?? 0;
        var selection = await modelSetup.EnsureModelForRequirementAsync(
            requirement,
            cancellationToken: cancellationToken,
            currentModelName: _session?.ActiveModel,
            lastSelectionBudgetGb: lastBudgetGb,
            lastSelectionFreeRamGb: lastFreeRamGb);

        // Actualizar el presupuesto y RAM de la ultima seleccion en la sesion
        if (_session is not null && selection.Budget is not null)
        {
            _session.LastSelectionBudgetGb = selection.Budget.BudgetGb;
            _session.LastSelectionFreeRamGb = selection.Resources?.FreeGb ?? 0;
        }

        // SelectForTask siempre marca BlockedByResources=true cuando Desired es null
        // (viable vacio o presupuesto insuficiente); este caso cubre todos los
        // caminos de fallo de seleccion y expone el motivo detallado.
        if (selection.Desired is null)
        {
            // CASO NUEVO: hay modelo activo en sesión, pero la tarea requiere
            // un upgrade (modelo superior) que no cabe en el presupuesto actual.
            // No bloqueamos Condor: informamos al usuario y mantenemos la sesión viva.
            var currentActiveModel = _session?.ActiveModel;
            var needsUpgradeButNoMemory = currentActiveModel is not null &&
                selection.BlockedByResources;

            if (needsUpgradeButNoMemory)
            {
                progress?.Report(AgentProgress.Of(
                    AgentPhase.Verifying,
                    message: "Memoria insuficiente: ninguna alternativa compatible cabe en el presupuesto seguro.",
                    resourceState: selection.Resources?.PressureLabel,
                    availableGb: selection.Resources?.FreeGb,
                    safeBudgetGb: selection.Budget?.BudgetGb,
                    flag: ProgressFlag.Normal));

                checkpoint.LastDecision = "memoria_insuficiente";
                checkpoint.LastError = "Memoria insuficiente para el upgrade requerido.";

                return new AgentResult
                {
                    Success = false,
                    Reason = "Memoria insuficiente para continuar con esta tarea. Libera memoria y vuelve a intentarlo.",
                    Model = currentActiveModel ?? string.Empty,
                    Objective = intention,
                    Steps = steps,
                    Checkpoint = checkpoint,
                    Inventory = await BuildInventoryAsync(selection, cancellationToken)
                };
            }

            // Sin modelo utilizable en absoluto: fallo controlado honesto.
            progress?.Report(AgentProgress.Of(
                AgentPhase.Analyzing,
                message: "No hay un modelo compatible que pueda cargarse con seguridad.",
                resourceState: selection.Resources?.PressureLabel,
                availableGb: selection.Resources?.FreeGb,
                safeBudgetGb: selection.Budget?.BudgetGb));

            return Fail(BuildModelNotExecutableReason(selection), "", intention, steps, checkpoint);
        }

        // Invariante: en este punto Desired es obligatoriamente no-nulo.
        if (selection.Desired is null)
            return Fail("No se pudo resolver un modelo utilizable para la tarea.", "", intention, steps, checkpoint);

        var model = selection.InstalledName ?? selection.Desired.PullName;
        checkpoint.Model = model;
        checkpoint.Strategy = "structured-action";
        checkpoint.LastDecision = "comprender";

        // Registrar la sesion activa del proveedor para la reutilizacion.
        // Deduplicacion: si la sesion ya esta activa para el MISMO modelo, se
        // reutiliza; nunca se inicializa un recurso nuevo por solicitud.
        if (_session is not null)
        {
            await _session.EnsureAvailableAsync(model, cancellationToken);
        }

        // Inventario del entorno y de la decision de modelo (recursos, CPU, disco,
        // modelos, modelo seleccionado, motivo y capacidades) para orientar y
        // presentar en el analisis. Opcional y tolerante a errores.
        var inventory = await BuildInventoryAsync(selection, cancellationToken);

        var toolset = new AgentToolset(workingDir, maxContent: _limits.MaxContentLength);
        var harness = new AgentHarness(workingDir, _limits, steps, intention);
        var originalSnapshot = RepoSnapshot.Capture(workingDir);

        // Observar el directorio real primero. El manifest .NET es OPCIONAL:
        // solo informa de la capacidad de build/test disponible; NO es un
        // requisito de entrada. Para intenciones abiertas (analisis, ""de que
        // va"") el agente razona sobre lo que realmente existe en el directorio.
        var snapshot = await ListRootSnapshotAsync(toolset, workingDir, cancellationToken);
        var manifest = FindManifest(workingDir);

        // Verificar disponibilidad de la sesion y ajustar el prompt al modelo
        // seleccionado (adaptacion por capacidades; no un prompt unico estatico).
        var activeModel = selection.Desired;
        var systemPrompt = ModelPromptBuilder.BuildSystemPrompt(workingDir, manifest, activeModel);

        var messages = new List<LlmMessage>
        {
            new() { Role = "system", Content = systemPrompt },
            new() { Role = "user", Content = "Contexto inicial del repositorio (estructura de la raiz):\n" + snapshot + "\n\nTarea: " + intention + "\n\nCONTEXTO ESTRICTO: si el modelo soporta salida estructurada, emite SIEMPRE una accion JSON (ver system). Para una solicitud de comprension (\"que tenemos aqui\"), observa con list_dir/read_file y cuando tengas suficiente evidencia responde usando done, colocando tu sintesis en 'reason'. No respondas fuera de lo indicado en system." }
        };

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_limits.TimeoutMilliseconds);

        string? lastDoneReason = null;

        try
        {
            var redundantObservations = 0;
            var resourcesWarned = false;

            async Task<AgentResult?> TryPlanBHtmlLinkRepairAsync(string? failureReason, int currentIteration)
            {
                var repair = await harness.TryRepairMissingRequiredHtmlLinkAsync(
                    failureReason,
                    timeoutCts.Token,
                    allowRequestedRootHtmlFile: true);
                if (repair is null) return null;
                var repairedPath = repair.Value.RelativePath;
                var repairAction = repair.Value.Action;

                planBUsed = true;
                planBInitialFailure ??= failureReason;
                awaitingPlanBAction = false;
                modifications++;
                var repairReason = repairAction == AgentAction.ActionCreateFile
                    ? "Plan B creó el archivo HTML raíz solicitado con el enlace a documentación; el harness verificará el resultado."
                    : "Plan B insertó el enlace HTML solicitado y el harness verificará el resultado.";
                var repairStep = new AgentStep
                {
                    Iteration = currentIteration,
                    Action = repairAction,
                    Path = repairedPath,
                    Success = true,
                    ResultPreview = repairReason,
                    AtUtc = DateTime.UtcNow
                };
                steps.Add(repairStep);
                checkpoint.LastDecision = "plan_b_html_link_repaired";
                checkpoint.LastAction = AgentAction.ActionEditFile + " " + repairedPath;
                checkpoint.LastResult = repairReason;
                progress?.Report(AgentProgress.Of(
                    AgentPhase.Verifying,
                    message: repairReason,
                    action: repairAction,
                    path: repairedPath,
                    iteration: currentIteration));

                var verification = await harness.VerifyAsync(timeoutCts.Token);
                checkpoint.HarnessState = !verification.ChecksApplicable
                    ? "no_aplicable"
                    : verification.Done ? "exito" : verification.Reason;
                checkpoint.LastError = verification.Done ? null : verification.Reason;
                messages.Add(new LlmMessage
                {
                    Role = "user",
                    Content = repairReason + " Resultado del harness: " +
                              (verification.Done ? (verification.Detail ?? "verificado") : (verification.Reason ?? "fallo sin detalle"))
                });

                if (!verification.Done) return null;

                var integrity = VerifyTestIntegrity(workingDir, originalSnapshot);
                if (integrity is not null)
                {
                    checkpoint.LastDecision = "revisar";
                    checkpoint.LastError = integrity;
                    return Fail(integrity, model, intention, steps, checkpoint);
                }

                checkpoint.NextAction = "entregar";
                checkpoint.LastDecision = "plan_b_corregido";
                var completionReason = HarnessCompletionReason(verification, planBUsed);
                progress?.Report(AgentProgress.Of(AgentPhase.Finalizing, message: completionReason));
                return new AgentResult
                {
                    Success = true,
                    Reason = completionReason,
                    Model = model,
                    Objective = intention,
                    Steps = steps,
                    Checkpoint = checkpoint
                };
            }

            var initialResources = EvaluateResources();
            progress?.Report(AgentProgress.Of(AgentPhase.Understanding,
                message: "Comprendiendo la solicitud",
                resourceState: initialResources?.PressureLabel,
                availableGb: initialResources?.FreeGb,
                safeBudgetGb: initialResources?.SafeBudgetGb));

            for (var iteration = 0; iteration < effectiveMaxIterations; iteration++)
            {
                checkpoint.Iteration = iteration + 1;
                if (awaitingPlanBAction)
                {
                    var maxPlanBIterations = Math.Max(1, _limits.MaxPlanBIterations);
                    if (checkpoint.PlanBIterationsUsed >= maxPlanBIterations)
                    {
                        var limitFailure = StartPlanB(
                            null,
                            null,
                            "Plan B alcanzo el limite de " + maxPlanBIterations + " iteraciones sin completar una accion correctiva.",
                            ref planBUsed,
                            ref planBInitialFailure,
                            ref awaitingPlanBAction,
                            ref effectiveMaxIterations,
                            iteration + 1,
                            messages,
                            progress,
                            checkpoint);
                        return Fail(limitFailure ?? "Plan B no pudo completar una accion correctiva.", model, intention, steps, checkpoint);
                    }
                    checkpoint.PlanBIterationsUsed++;
                }

                // Modelo bloqueado para toda la tarea: no se reevalua ni cambia durante la ejecucion.
                // El modelo seleccionado al inicio (selection.Desired) permanece fijo.

                // 1. Modelo produce una decision estructurada. Se distingue el
                //    fallo del proveedor (crash/caida/timeout) del protocolo del
                //    modelo (respuesta no-JSON), para no consumir iteraciones
                //    como si el modelo siguiera disponible cuando no lo esta.
                progress?.Report(AgentProgress.Of(
                    AgentPhase.Analyzing,
                    iteration: iteration + 1,
                    message: "Esperando propuesta",
                    modelActivity: model + " · generando propuesta",
                    modelWorking: true));
                var call = await RequestActionAsync(model, messages, timeoutCts.Token);
                var modelActivity = call.Action is { } proposedAction
                    ? model + " · propuso " + proposedAction.Action + (string.IsNullOrWhiteSpace(proposedAction.Path) ? "" : " " + proposedAction.Path)
                    : model + " · no produjo una acción válida";
                progress?.Report(AgentProgress.Of(
                    AgentPhase.Analyzing,
                    iteration: iteration + 1,
                    message: "Respuesta estructurada recibida",
                    modelActivity: modelActivity,
                    modelWorking: false));

                // Una respuesta no estructurada es un fallo de protocolo, no una
                // caida del proveedor. Se corrige en Plan B sin esperar al limite.
                if (call.ProviderFailure == LlmOutcome.InvalidResponse)
                    call = new AgentModelCall(null, null, call.Error);

                if (call.ProviderFailure is not null)
                {
                    checkpoint.NextAction = "revisar";
                    progress?.Report(AgentProgress.Of(
                        AgentPhase.Verifying,
                        message: "Modelo local · " + LlmOutcomeLabel(call.ProviderFailure.Value) + " · " + call.Error,
                        flag: ProgressFlag.ProviderError,
                        modelActivity: model + " · " + LlmOutcomeLabel(call.ProviderFailure.Value),
                        modelWorking: false));

                    var recovered = await TryRecoverProviderAsync(model, messages, progress, timeoutCts.Token);
                    if (recovered is not null)
                    {
                        call = recovered.Value;
                        if (call.ProviderFailure == LlmOutcome.InvalidResponse)
                            call = new AgentModelCall(null, null, call.Error);

                        // Si tras la recuperacion el proveedor sigue fallando (p. ej.
                        // HTTP 500 persistente o timeout), NO se degrada a "respuesta
                        // del modelo invalida": es un fallo del proveedor y se detiene
                        // sin gastar mas iteraciones.
                        if (call.ProviderFailure is not null && call.Action is null)
                        {
                            checkpoint.LastDecision = "detener";
                            checkpoint.LastError = BuildProviderFailureReason(call.ProviderFailure.Value, call.Error);
                            progress?.Report(AgentProgress.Of(
                                AgentPhase.Finalizing,
                                message: "Modelo local no disponible tras reintentos · Condor detuvo la tarea",
                                flag: ProgressFlag.ProviderError));
                            return Fail(checkpoint.LastError, model, intention, steps, checkpoint);
                        }

                        progress?.Report(AgentProgress.Of(
                            AgentPhase.Analyzing,
                            iteration: iteration + 1,
                            message: "Respuesta recuperada del proveedor",
                            modelActivity: call.Action is { } recoveredAction
                                ? model + " · propuso " + recoveredAction.Action + (string.IsNullOrWhiteSpace(recoveredAction.Path) ? "" : " " + recoveredAction.Path)
                                : model + " · respuesta sin acción válida",
                            modelWorking: false));

                    }
                    else
                    {
                        checkpoint.LastDecision = "detener";
                        checkpoint.LastError = BuildProviderFailureReason(call.ProviderFailure.Value, call.Error);
                        progress?.Report(AgentProgress.Of(
                            AgentPhase.Finalizing,
                            message: "Modelo local no disponible · Condor detuvo la tarea",
                            flag: ProgressFlag.ProviderError));
                        return Fail(checkpoint.LastError, model, intention, steps, checkpoint);
                    }
                }

                if (call.Action is null)
                {
                    var protocolFailure = call.Error ?? "El modelo no produjo una accion JSON valida.";
                    steps.Add(new AgentStep
                    {
                        Iteration = iteration + 1,
                        Action = "respuesta_estructurada",
                        Success = false,
                        ResultPreview = protocolFailure,
                        AtUtc = DateTime.UtcNow
                    });

                    if (IsInformationalRequest(intention))
                    {
                        checkpoint.LastDecision = "plan_b_describir";
                        checkpoint.LastError = protocolFailure;
                        checkpoint.NextAction = "observar_y_describir";
                        progress?.Report(AgentProgress.Of(
                            AgentPhase.Analyzing,
                            action: "Plan B",
                            iteration: iteration + 1,
                            message: "Plan B: la respuesta no fue estructurada; Condor observara el proyecto y resumira evidencia real."));
                        var grounded = await GroundInformationalAsync(toolset, workingDir, steps, timeoutCts.Token);
                        return grounded is null
                            ? Fail("Plan B no pudo obtener evidencia suficiente para describir el proyecto.", model, intention, steps, checkpoint)
                            : new AgentResult { Success = true, Reason = "Plan B entrego una descripcion basada en archivos observados por Condor. " + grounded, Model = model, Objective = intention, Steps = steps, Checkpoint = checkpoint, Inventory = inventory };
                    }

                    var planBFailure = StartPlanB(
                        "respuesta_estructurada",
                        null,
                        protocolFailure,
                        ref planBUsed,
                        ref planBInitialFailure,
                        ref awaitingPlanBAction,
                        ref effectiveMaxIterations,
                        iteration + 1,
                        messages,
                        progress,
                        checkpoint);
                    if (planBFailure is not null)
                    {
                        return Fail(planBFailure, model, intention, steps, checkpoint);
                    }
                    var htmlRecovery = await TryPlanBHtmlLinkRepairAsync(protocolFailure, iteration + 1);
                    if (htmlRecovery is not null) return htmlRecovery;
                    continue;
                }

                // A partir de aqui hay una accion util; el caso nulo ya hizo continue.
                var action = call.Action!;
                var requestedActionName = action.Action;
                var requestedActionPath = action.Path;
                var normalizedRequestedRootHtmlPath = TryNormalizeRequestedRootHtmlPath(action, intention, workingDir);
                var normalizedCombinedFileAction = awaitingPlanBAction &&
                    TryNormalizeCombinedFileAction(action, workingDir);

                // 2. Valida la accion.
                var validation = AgentEngine.ValidateAction(action);
                if (!validation.Valid)
                {
                    var validationFailure = validation.Reason ?? "La accion no cumple el contrato.";
                    steps.Add(new AgentStep
                    {
                        Iteration = iteration + 1,
                        Action = action.Action,
                        Path = action.Path,
                        Success = false,
                        ResultPreview = validationFailure,
                        AtUtc = DateTime.UtcNow
                    });
                    var planBFailure = StartPlanB(
                        action.Action,
                        action.Path,
                        validationFailure,
                        ref planBUsed,
                        ref planBInitialFailure,
                        ref awaitingPlanBAction,
                        ref effectiveMaxIterations,
                        iteration + 1,
                        messages,
                        progress,
                        checkpoint);
                    if (planBFailure is not null)
                        return Fail(planBFailure, model, intention, steps, checkpoint);
                    var htmlRecovery = await TryPlanBHtmlLinkRepairAsync(validationFailure, iteration + 1);
                    if (htmlRecovery is not null) return htmlRecovery;
                    continue;
                }

                if (action.Action == AgentAction.ActionDone && awaitingPlanBAction)
                {
                    var agentRefusal = action.Reason ?? action.Content;
                    var earlyDoneFailure = "El agente intento terminar sin ejecutar una accion de recuperacion tras el fallo." +
                        (string.IsNullOrWhiteSpace(agentRefusal) ? "" : " Motivo del agente: " + agentRefusal);
                    steps.Add(new AgentStep
                    {
                        Iteration = iteration + 1,
                        Action = action.Action,
                        Success = false,
                        ResultPreview = earlyDoneFailure,
                        AtUtc = DateTime.UtcNow
                    });
                    var planBFailure = StartPlanB(
                        action.Action,
                        action.Path,
                        earlyDoneFailure,
                        ref planBUsed,
                        ref planBInitialFailure,
                        ref awaitingPlanBAction,
                        ref effectiveMaxIterations,
                        iteration + 1,
                        messages,
                        progress,
                        checkpoint);
                    if (planBFailure is not null)
                        return Fail(planBFailure, model, intention, steps, checkpoint);
                    var htmlRecovery = await TryPlanBHtmlLinkRepairAsync(earlyDoneFailure, iteration + 1);
                    if (htmlRecovery is not null) return htmlRecovery;
                    continue;
                }

                // 3. Notifica la accion que esta por ejecutar.
                progress?.Report(AgentProgress.Of(
                    PhaseForAction(action.Action),
                    action: action.Action,
                    path: action.Path,
                    iteration: iteration + 1));

                // 4. Ejecuta la herramienta real.
                var step = await toolset.ExecuteAsync(action, iteration + 1, timeoutCts.Token);
                if (normalizedCombinedFileAction || normalizedRequestedRootHtmlPath)
                {
                    var normalization = normalizedRequestedRootHtmlPath
                        ? "Condor corrigio la ruta '" + (requestedActionPath ?? "") + "' a la ruta raiz exacta '" + action.Path + "' solicitada. "
                        : "Condor interpreto la accion compuesta '" + requestedActionName + "' como '" + action.Action + "'. ";
                    step.ResultPreview = normalization + (step.ResultPreview ?? "");
                }
                else if (!step.Success && awaitingPlanBAction)
                {
                    var completeHtmlFallback = TryCreateCompleteHtmlEditFallback(action, workingDir, step.ResultPreview);
                    if (completeHtmlFallback is not null)
                    {
                        steps.Add(step);
                        var fallbackStep = await toolset.ExecuteAsync(completeHtmlFallback, iteration + 1, timeoutCts.Token);
                        fallbackStep.ResultPreview = "Plan B detecto que el original de patch no coincidia; como replacement contenia un documento HTML completo y la ruta era un archivo HTML de la raiz, aplico edit_file. " +
                                                     (fallbackStep.ResultPreview ?? "");
                        action = completeHtmlFallback;
                        step = fallbackStep;
                    }
                }
                steps.Add(step);
                checkpoint.LastAction = action.Action + " " + (action.Path ?? "");
                checkpoint.LastResult = Truncate(step.ResultPreview ?? "");

                if ((action.Action is AgentAction.ActionEditFile or AgentAction.ActionCreateFile or AgentAction.ActionPatch) && step.Success)
                    modifications++;

                messages.Add(new LlmMessage
                {
                    Role = "user",
                    Content = "Resultado de " + action.Action + "(" + (action.Path ?? "") + "):\n" + (step.Success ? (step.ResultPreview ?? "ok") : ("ERROR: " + (step.ResultPreview ?? "sin detalle")))
                });

                if (!step.Success)
                {
                    var planBFailure = StartPlanB(
                        action.Action,
                        action.Path,
                        step.ResultPreview ?? "La herramienta no pudo completar la accion.",
                        ref planBUsed,
                        ref planBInitialFailure,
                        ref awaitingPlanBAction,
                        ref effectiveMaxIterations,
                        iteration + 1,
                        messages,
                        progress,
                        checkpoint);
                    if (planBFailure is not null)
                        return Fail(planBFailure, model, intention, steps, checkpoint);
                    var htmlRecovery = await TryPlanBHtmlLinkRepairAsync(
                        step.ResultPreview ?? "La herramienta fallo al ejecutar la accion.",
                        iteration + 1);
                    if (htmlRecovery is not null) return htmlRecovery;
                }
                else if (awaitingPlanBAction && IsPlanBCorrectionAction(action.Action))
                {
                    awaitingPlanBAction = false;
                }

                // 4b. Monitoreo de recursos en cada accion (advertencia solo):
                //     el MODELO esta bloqueado para toda la tarea. Si la presion de
                //     RAM empeora, se advierte una vez (sin saturar) y se pospone
                //     build/test pesado mientras el sistema esta en Presion. Nunca
                //     se cambia el modelo durante la ejecucion.
                EvaluateResourcesAndWarn(checkpoint, progress, ref resourcesWarned, iteration + 1);

                // 5. Redundancia de observacion: si el modelo repite la MISMA
                //    observacion (accion+ruta) y obtiene el MISMO resultado (no
                //    aporta informacion nueva), se orienta para avanzar. Si
                //    persiste sin informacion nueva, se entrega o falla de forma
                //    honesta segun el tipo de tarea.
                if (step.Success && (action.Action is AgentAction.ActionListDir or AgentAction.ActionReadFile or AgentAction.ActionSearch))
                {
                    var observationSignal = AgentEngine.AssessObservation(step, steps.Take(steps.Count - 1).ToList());
                    if (observationSignal == ObservationSignal.Redundant)
                    {
                        redundantObservations++;
                        if (redundantObservations > _limits.MaxRedundantObservations)
                        {
                            // Sin informacion nueva y repitiendo: entregar (si es
                            // informativo/analisis) o fallar honestamente (si se
                            // esperaba un cambio de codigo).
                            if (modifications == 0 && IsInformationalRequest(intention))
                            {
                                checkpoint.NextAction = "entregar";
                                checkpoint.LastDecision = planBUsed ? "plan_b_corregido" : "describir";
                                var summary = BuildObservedSummary(steps, workingDir);
                                return new AgentResult
                                {
                                    Success = true,
                                    Reason = (planBUsed ? "Plan B recupero la tarea tras el fallo inicial. " : "") + summary,
                                    Model = model,
                                    Objective = intention,
                                    Steps = steps,
                                    Checkpoint = checkpoint
                                };
                            }

                            checkpoint.LastError = "Observaciones redundantes sin informacion nueva y sin un cambio de codigo verificado.";
                            var planBFailure = StartPlanB(
                                action.Action,
                                action.Path,
                                checkpoint.LastError,
                                ref planBUsed,
                                ref planBInitialFailure,
                                ref awaitingPlanBAction,
                                ref effectiveMaxIterations,
                                iteration + 1,
                                messages,
                                progress,
                                checkpoint);
                            if (planBFailure is not null)
                                return Fail(planBFailure, model, intention, steps, checkpoint);
                            var htmlRecovery = await TryPlanBHtmlLinkRepairAsync(checkpoint.LastError, iteration + 1);
                            if (htmlRecovery is not null) return htmlRecovery;
                            continue;
                        }

                        messages.Add(new LlmMessage
                        {
                            Role = "user",
                            Content = "Ya observaste esto exactamente antes y el resultado no cambio; repetir la misma observacion no aporta informacion nueva. Si la solicitud es de comprension/analisis, sintetiza lo visto y responde con done. Si la tarea requiere un cambio, usa exactamente create_file para crear, edit_file para reemplazar un archivo existente o patch con original/replacement para un cambio quirurgico. No combines nombres con '/'; verifica con build o test cuando aplique."
                        });
                    }
                    else
                    {
                        redundantObservations = 0;
                    }
                }

                // 5b. Harness externo tras un cambio de codigo.
                if (IsModification(action.Action) && step.Success)
                {
                    progress?.Report(AgentProgress.Of(AgentPhase.Verifying, action: "build/test", iteration: iteration + 1));
                    var result = await harness.VerifyAsync(timeoutCts.Token);
                    var htmlRepair = planBUsed
                        ? await harness.TryRepairMissingRequiredHtmlLinkAsync(result.Reason, timeoutCts.Token)
                        : null;
                    if (htmlRepair is not null)
                    {
                        var repairedHtmlPath = htmlRepair.Value.RelativePath;
                        var repairAction = htmlRepair.Value.Action;
                        planBUsed = true;
                        planBInitialFailure ??= result.Reason;
                        awaitingPlanBAction = false;
                        checkpoint.LastDecision = "plan_b_html_link_repaired";
                        checkpoint.LastAction = repairAction + " " + repairedHtmlPath;
                        checkpoint.LastResult = repairAction == AgentAction.ActionCreateFile
                            ? "Plan B creó el archivo HTML solicitado; el harness repetirá la verificación."
                            : "Plan B insertó el enlace HTML solicitado; el harness repetirá la verificación.";
                        steps.Add(new AgentStep
                        {
                            Action = repairAction,
                            Path = repairedHtmlPath,
                            Success = true,
                            ResultPreview = checkpoint.LastResult
                        });
                        progress?.Report(AgentProgress.Of(
                            AgentPhase.Verifying,
                            message: checkpoint.LastResult,
                            action: repairAction,
                            path: repairedHtmlPath,
                            iteration: iteration + 1));
                        result = await harness.VerifyAsync(timeoutCts.Token);
                    }

                    checkpoint.HarnessState = !result.ChecksApplicable
                        ? "no_aplicable"
                        : result.Done ? "exito" : result.Reason;
                    checkpoint.LastError = result.Done ? null : result.Reason;

                    if (result.Done)
                    {
                        var integrity = VerifyTestIntegrity(workingDir, originalSnapshot);
                        if (integrity is not null)
                        {
                            checkpoint.LastError = integrity;
                            var integrityPlanBFailure = StartPlanB(
                                "integridad_harness",
                                null,
                                integrity,
                                ref planBUsed,
                                ref planBInitialFailure,
                                ref awaitingPlanBAction,
                                ref effectiveMaxIterations,
                                iteration + 1,
                                messages,
                                progress,
                                checkpoint);
                            if (integrityPlanBFailure is not null)
                            {
                                return Fail(integrityPlanBFailure, model, intention, steps, checkpoint);
                            }
                            continue;
                        }

                        checkpoint.NextAction = "entregar";
                        checkpoint.LastDecision = planBUsed
                            ? "plan_b_corregido"
                            : result.ChecksApplicable ? "verificar" : "verificacion_no_aplicable";
                        var completionReason = HarnessCompletionReason(result, planBUsed);
                        progress?.Report(AgentProgress.Of(AgentPhase.Finalizing, message: completionReason));
                        return new AgentResult { Success = true, Reason = completionReason, Model = model, Objective = intention, Steps = steps, Checkpoint = checkpoint };
                    }

                    var failedHarnessStep = steps.LastOrDefault(s => !s.Success &&
                        (s.Action.Equals(AgentAction.ActionBuild, StringComparison.OrdinalIgnoreCase) ||
                         s.Action.Equals(AgentAction.ActionTest, StringComparison.OrdinalIgnoreCase)))
                        ?? new AgentStep { Action = "harness", Success = false, ResultPreview = result.Detail };
                    var planBFailure = StartPlanB(
                        failedHarnessStep.Action,
                        failedHarnessStep.Path,
                        (result.Reason ?? "Build/test fallo.") + " " + result.Detail,
                        ref planBUsed,
                        ref planBInitialFailure,
                        ref awaitingPlanBAction,
                        ref effectiveMaxIterations,
                        iteration + 1,
                        messages,
                        progress,
                        checkpoint);
                    if (planBFailure is not null)
                        return Fail(planBFailure, model, intention, steps, checkpoint);
                    var htmlRecovery = await TryPlanBHtmlLinkRepairAsync(
                        (result.Reason ?? "Build/test fallo.") + " " + result.Detail,
                        iteration + 1);
                    if (htmlRecovery is not null) return htmlRecovery;
                }

                // 6. Si el modelo dice 'done', decidir la entrega:
                //    - Con modificaciones: exigir harness .NET (autoridad externa).
                //    - Sin modificaciones pero con exploracion real: tarea
                //      informativa/analisis, se entrega la descripcion observada
                //      (sin inventar exito de harness).
                //    - Sin nada hecho: pedir que explore/actue.
                if (action.Action == AgentAction.ActionDone)
                {
                    if (modifications == 0)
                    {
                        // Evaluador general (no lista rigida) de evidencia suficiente
                        // para 'done' segun el matiz de la intencion (describir,
                        // diagnosticar, construir). Evita tanto el 'done' prematuro
                        // como el ciclo forzado de read_file cuando ya hay base.
                        var (sufficient, hint) = AgentEngine.HasSufficientEvidenceForDone(intention, steps);
                        if (!sufficient)
                        {
                            // Conservamos la sintesis del 'done' rechazado para, si
                            // agotamos iteraciones, poder entregarla honestamente
                            // (no un crptico "limite de iteraciones") cuando la
                            // intencion es de comprension/diagnostico.
                            lastDoneReason = action.Reason ?? action.Content ?? lastDoneReason;
                            messages.Add(new LlmMessage { Role = "user", Content = hint ?? "No tienes evidencia suficiente aun; observa o actua, y luego concluye." });
                            continue;
                        }

                        checkpoint.NextAction = "entregar";
                        checkpoint.LastDecision = planBUsed ? "plan_b_corregido" : "describir";
                        progress?.Report(AgentProgress.Of(AgentPhase.Finalizing, message: "Preparando respuesta"));
                        var summary = action.Reason ?? action.Content ?? "";
                        return new AgentResult
                        {
                            Success = true,
                            Reason = (planBUsed ? "Plan B recupero la tarea tras el fallo inicial. " : "") + (string.IsNullOrWhiteSpace(summary)
                                ? "Condor observo el directorio y describio lo encontrado."
                                : summary),
                            Inventory = inventory,
                            Model = model,
                            Objective = intention,
                            Steps = steps,
                            Checkpoint = checkpoint
                        };
                    }

                    progress?.Report(AgentProgress.Of(AgentPhase.Verifying, action: "build/test", iteration: iteration + 1));
                    var r = await harness.VerifyAsync(timeoutCts.Token);
                    var htmlRepair = planBUsed
                        ? await harness.TryRepairMissingRequiredHtmlLinkAsync(r.Reason, timeoutCts.Token)
                        : null;
                    if (htmlRepair is not null)
                    {
                        var repairedHtmlPath = htmlRepair.Value.RelativePath;
                        var repairAction = htmlRepair.Value.Action;
                        planBUsed = true;
                        planBInitialFailure ??= r.Reason;
                        awaitingPlanBAction = false;
                        checkpoint.LastDecision = "plan_b_html_link_repaired";
                        checkpoint.LastAction = repairAction + " " + repairedHtmlPath;
                        checkpoint.LastResult = repairAction == AgentAction.ActionCreateFile
                            ? "Plan B creó el archivo HTML solicitado; el harness repetirá la verificación."
                            : "Plan B insertó el enlace HTML solicitado; el harness repetirá la verificación.";
                        steps.Add(new AgentStep
                        {
                            Action = repairAction,
                            Path = repairedHtmlPath,
                            Success = true,
                            ResultPreview = checkpoint.LastResult
                        });
                        progress?.Report(AgentProgress.Of(
                            AgentPhase.Verifying,
                            message: checkpoint.LastResult,
                            action: repairAction,
                            path: repairedHtmlPath,
                            iteration: iteration + 1));
                        r = await harness.VerifyAsync(timeoutCts.Token);
                    }

                    checkpoint.HarnessState = !r.ChecksApplicable
                        ? "no_aplicable"
                        : r.Done ? "exito" : r.Reason;
                    checkpoint.LastError = r.Done ? null : r.Reason;

                    if (r.Done)
                    {
                        var integrity = VerifyTestIntegrity(workingDir, originalSnapshot);
                        if (integrity is not null)
                        {
                            checkpoint.NextAction = "detener";
                            checkpoint.LastDecision = "revisar";
                            return Fail(integrity, model, intention, steps, checkpoint);
                        }

                        checkpoint.NextAction = "entregar";
                        checkpoint.LastDecision = planBUsed
                            ? "plan_b_corregido"
                            : r.ChecksApplicable ? "verificar" : "verificacion_no_aplicable";
                        var completionReason = HarnessCompletionReason(r, planBUsed);
                        progress?.Report(AgentProgress.Of(AgentPhase.Finalizing, message: completionReason));
                        return new AgentResult { Success = true, Reason = completionReason, Model = model, Objective = intention, Steps = steps, Checkpoint = checkpoint };
                    }

                    var failedHarnessStep = steps.LastOrDefault(s => !s.Success &&
                        (s.Action.Equals(AgentAction.ActionBuild, StringComparison.OrdinalIgnoreCase) ||
                         s.Action.Equals(AgentAction.ActionTest, StringComparison.OrdinalIgnoreCase)))
                        ?? new AgentStep { Action = "harness", Success = false, ResultPreview = r.Detail };
                    var planBFailure = StartPlanB(
                        failedHarnessStep.Action,
                        failedHarnessStep.Path,
                        (r.Reason ?? "Build/test fallo.") + " " + r.Detail,
                        ref planBUsed,
                        ref planBInitialFailure,
                        ref awaitingPlanBAction,
                        ref effectiveMaxIterations,
                        iteration + 1,
                        messages,
                        progress,
                        checkpoint);
                    if (planBFailure is not null)
                        return Fail(planBFailure, model, intention, steps, checkpoint);
                    var htmlRecovery = await TryPlanBHtmlLinkRepairAsync(
                        (r.Reason ?? "Build/test fallo.") + " " + r.Detail,
                        iteration + 1);
                    if (htmlRecovery is not null) return htmlRecovery;
                    continue;
                }

                // 7. Progress check.
                var progressCheck = AgentEngine.CheckProgress(iteration + 1, steps, _limits, effectiveMaxIterations);
                if (progressCheck.Fail)
                {
                    var progressReason = progressCheck.Reason ?? "Limite sin progreso.";
                    if (planBUsed && progressReason.Contains("limite de iteraciones", StringComparison.OrdinalIgnoreCase))
                        progressReason = "Plan B no completo la tarea dentro del presupuesto de iteraciones.";
                    checkpoint.LastError = progressReason;

                    // Si la intencion es de comprension/diagnostico y hay evidencia,
                    // se entrega la respuesta observada en vez de un crptico fallo
                    // por limite de iteraciones.
                    var delivered = TryDeliverInformational(checkpoint, steps, workingDir, model, intention, progress, lastDoneReason, inventory);
                    if (delivered is not null)
                    {
                        return ApplyPlanBCompletion(delivered, planBUsed);
                    }

                    var lastStep = steps.LastOrDefault();
                    var planBFailure = StartPlanB(
                        null,
                        lastStep?.Path,
                        progressReason + (lastStep is null ? "" : " Ultima accion: " + lastStep.Action + ". Resultado: " + (lastStep.ResultPreview ?? "sin detalle")),
                        ref planBUsed,
                        ref planBInitialFailure,
                        ref awaitingPlanBAction,
                        ref effectiveMaxIterations,
                        iteration + 1,
                        messages,
                        progress,
                        checkpoint);
                    if (planBFailure is not null)
                        return Fail(planBFailure, model, intention, steps, checkpoint);
                    var htmlRecovery = await TryPlanBHtmlLinkRepairAsync(progressReason, iteration + 1);
                    if (htmlRecovery is not null) return htmlRecovery;
                    continue;
                }

                checkpoint.NextAction = "siguiente";
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            return Fail("Tiempo total del agente agotado.", model, intention, steps, checkpoint);
        }
        catch (Exception ex)
        {
            return Fail("Error interno del agente: " + ex.Message, model, intention, steps, checkpoint);
        }

        // Agotadas/progreso-limitado: si la intencion es de comprension/diagnostico y el
        // agente recolecto evidencia, se entrega una respuesta fundamentada (no un
        // crptico "limite de iteraciones"). Para una intencion de CONSTRUIR, sin
        // cambio verificado no hay respuesta que entregar.
        var finalDelivery = TryDeliverInformational(checkpoint, steps, workingDir, model, intention, progress, lastDoneReason, inventory);
        if (finalDelivery is not null)
            return ApplyPlanBCompletion(finalDelivery, planBUsed);
        return Fail(
            planBUsed ? "Plan B no pudo completar la tarea dentro de los limites." : "El agente no pudo completar la tarea dentro de los limites.",
            model,
            intention,
            steps,
            checkpoint);
    }

    private async Task<AgentResult> RunDirectConsultAsync(
        string intention,
        TaskModelRequirement requirement,
        IAgentProgressObserver? progress,
        CancellationToken cancellationToken)
    {
        var checkpoint = new AgentCheckpoint
        {
            Task = intention,
            GeneratedAtUtc = DateTime.UtcNow,
            Strategy = "direct-consult",
            LastDecision = "comprender"
        };
        var steps = new List<AgentStep>();

var modelSetup = CreateModelSetup();
        var lastBudgetGb = _session?.LastSelectionBudgetGb ?? 0;
        var lastFreeRamGb = _session?.LastSelectionFreeRamGb ?? 0;
        var selection = await modelSetup.EnsureModelForRequirementAsync(
            requirement,
            cancellationToken: cancellationToken,
            currentModelName: _session?.ActiveModel,
            lastSelectionBudgetGb: lastBudgetGb,
            lastSelectionFreeRamGb: lastFreeRamGb);

        // Actualizar el presupuesto y RAM de la ultima seleccion en la sesion
        if (_session is not null && selection.Budget is not null)
        {
            _session.LastSelectionBudgetGb = selection.Budget.BudgetGb;
            _session.LastSelectionFreeRamGb = selection.Resources?.FreeGb ?? 0;
        }

        if (selection.Desired is null)
        {
            // CASO NUEVO: hay modelo activo en sesión, pero la tarea requiere
            // un upgrade (modelo superior) que no cabe en el presupuesto actual.
            // No bloqueamos Condor: informamos al usuario y mantenemos la sesión viva.
            var currentActiveModel = _session?.ActiveModel;
            var needsUpgradeButNoMemory = currentActiveModel is not null &&
                selection.BlockedByResources;

            if (needsUpgradeButNoMemory)
            {
                progress?.Report(AgentProgress.Of(
                    AgentPhase.Verifying,
                    message: "Memoria insuficiente para continuar con esta tarea.",
                    resourceState: selection.Resources?.PressureLabel,
                    availableGb: selection.Resources?.FreeGb,
                    safeBudgetGb: selection.Budget?.BudgetGb,
                    flag: ProgressFlag.Normal));

                checkpoint.LastDecision = "memoria_insuficiente";
                checkpoint.LastError = "Memoria insuficiente para el upgrade requerido.";

                return new AgentResult
                {
                    Success = false,
                    Reason = "Memoria insuficiente para continuar con esta tarea. Libera memoria y vuelve a intentarlo.",
                    Model = currentActiveModel ?? string.Empty,
                    Objective = intention,
                    Steps = steps,
                    Checkpoint = checkpoint,
                    Inventory = await BuildInventoryAsync(selection, cancellationToken)
                };
            }

            // Sin modelo utilizable en absoluto: fallo controlado honesto.
            progress?.Report(AgentProgress.Of(
                AgentPhase.Verifying,
                message: "Modelo no ejecutable por presupuesto de memoria.",
                resourceState: selection.Resources?.PressureLabel,
                availableGb: selection.Resources?.FreeGb,
                safeBudgetGb: selection.Budget?.BudgetGb,
                flag: ProgressFlag.ProviderError));

            return Fail(
                BuildModelNotExecutableReason(selection),
                "",
                intention,
                steps,
                checkpoint);
        }

        var model = selection.InstalledName ?? selection.Desired.PullName;
        checkpoint.Model = model;

        if (_session is not null)
        {
            var available = await _session.EnsureAvailableAsync(model, cancellationToken);
            if (!available)
            {
                checkpoint.LastDecision = "detener";
                checkpoint.LastError = "El proveedor local no esta disponible.";
                progress?.Report(AgentProgress.Of(
                    AgentPhase.Verifying,
                    message: checkpoint.LastError,
                    flag: ProgressFlag.ProviderError));
                return Fail(checkpoint.LastError, model, intention, steps, checkpoint);
            }
        }

        var inventory = await BuildInventoryAsync(selection, cancellationToken);
        progress?.Report(AgentProgress.Of(
            AgentPhase.Understanding,
            message: "Consultando el modelo local",
            resourceState: selection.Resources?.PressureLabel,
            availableGb: selection.Resources?.FreeGb,
            safeBudgetGb: selection.Budget?.BudgetGb,
            modelActivity: model + " · redactando respuesta",
            modelWorking: true));

        LlmResponse response;
        try
        {
            response = await _llm.CompleteAsync(
                new LlmRequest
                {
                    Model = model,
                    Prompt = "Responde en español, de forma breve y natural, a esta consulta del usuario: " + intention,
                    Temperature = 0.2,
                    MaxTokens = 256
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Fail("Tiempo total de la consulta agotado.", model, intention, steps, checkpoint);
        }

        progress?.Report(AgentProgress.Of(
            AgentPhase.Understanding,
            message: "Condor recibió la respuesta del modelo",
            modelActivity: model + " · respuesta recibida",
            modelWorking: false));

        if (!response.Success || string.IsNullOrWhiteSpace(response.Content))
        {
            checkpoint.LastDecision = "detener";
            checkpoint.LastError = BuildProviderFailureReason(
                response.Outcome,
                response.Error ?? "El modelo no devolvio una respuesta util.");
            progress?.Report(AgentProgress.Of(
                AgentPhase.Verifying,
                message: "Modelo local · " + checkpoint.LastError,
                flag: ProgressFlag.ProviderError));
            return Fail(checkpoint.LastError, model, intention, steps, checkpoint);
        }

        checkpoint.Iteration = 1;
        checkpoint.LastDecision = "entregar";
        checkpoint.NextAction = "entregar";
        progress?.Report(AgentProgress.Of(
            AgentPhase.Finalizing,
            message: "Preparando respuesta"));

        return new AgentResult
        {
            Success = true,
            Reason = response.Content.Trim(),
            Model = model,
            Objective = intention,
            Steps = steps,
            Checkpoint = checkpoint,
            Inventory = inventory
        };
    }

    private static AgentResult? TryDeliverInformational(
        AgentCheckpoint checkpoint,
        IReadOnlyList<AgentStep> steps,
        string workingDir,
        string model,
        string intention,
        IAgentProgressObserver? progress,
        string? lastDoneReason,
        AgentInventory? inventory)
    {
        var flavor = AgentEngine.ClassifyIntent(intention);
        if (flavor == IntentFlavor.Build)
        {
            return null;
        }

        bool hasRealEvidence = steps.Any(s =>
            s.Success &&
            (s.Action == AgentAction.ActionListDir || s.Action == AgentAction.ActionReadFile || s.Action == AgentAction.ActionSearch));
        if (!hasRealEvidence)
        {
            return null;
        }

        checkpoint.NextAction = "entregar";
        checkpoint.LastDecision = "describir";
        progress?.Report(AgentProgress.Of(AgentPhase.Finalizing, message: "Preparando respuesta"));

        var baseSummary = DescribeObserved(workingDir, steps);
        var summary = string.IsNullOrWhiteSpace(lastDoneReason)
            ? baseSummary
            : baseSummary + "\n" + lastDoneReason.Trim();

        return new AgentResult
        {
            Success = true,
            Reason = summary,
            Model = model,
            Objective = intention,
            Steps = steps.ToList(),
            Checkpoint = checkpoint,
            Inventory = inventory
        };
    }

    private static bool IsInformationalRequest(string intention)
        => TaskIntentClassifier.IsInformational(intention);

    private static bool IsPlanBCorrectionAction(string action)
        => action is AgentAction.ActionPatch or
            AgentAction.ActionEditFile or
            AgentAction.ActionCreateFile or
            AgentAction.ActionUndoFile or
            AgentAction.ActionBuild or
            AgentAction.ActionTest;

    private static bool TryNormalizeCombinedFileAction(AgentAction action, string workingDir)
    {
        if (!string.Equals(action.Action, "patch/edit_file", StringComparison.OrdinalIgnoreCase))
            return false;

        var relativePath = action.Path?.Trim();
        if (string.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathRooted(relativePath) ||
            !string.Equals(Path.GetFileName(relativePath), relativePath, StringComparison.Ordinal) ||
            relativePath is "." or "..")
        {
            return false;
        }

        var target = Path.Combine(workingDir, relativePath);
        var exists = File.Exists(target);
        if (exists && !string.IsNullOrEmpty(action.Original) && action.Replacement is not null)
        {
            action.Action = AgentAction.ActionPatch;
            return true;
        }

        if (string.IsNullOrWhiteSpace(action.Content))
            return false;

        action.Action = exists ? AgentAction.ActionEditFile : AgentAction.ActionCreateFile;
        return true;
    }

    private static bool TryNormalizeRequestedRootHtmlPath(AgentAction action, string intention, string workingDir)
    {
        if (action.Action is not (AgentAction.ActionReadFile or AgentAction.ActionEditFile or
            AgentAction.ActionCreateFile or AgentAction.ActionPatch or "patch/edit_file"))
        {
            return false;
        }

        var match = System.Text.RegularExpressions.Regex.Match(
            intention ?? "",
            @"\bruta\s+exacta\s+(?<file>[A-Za-z0-9_.-]+\.html)\s*\(\s*ra[ií]z\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success) return false;

        var requestedFile = match.Groups["file"].Value;
        if (!string.Equals(Path.GetFileName(requestedFile), requestedFile, StringComparison.Ordinal))
            return false;

        var actionPath = NormalizeRel(action.Path?.Trim());
        while (actionPath.StartsWith("./", StringComparison.Ordinal)) actionPath = actionPath[2..];
        if (!string.Equals(actionPath, "docs/" + requestedFile, StringComparison.OrdinalIgnoreCase))
            return false;

        var root = Path.GetFullPath(workingDir);
        var target = Path.GetFullPath(Path.Combine(root, requestedFile));
        var rootPrefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        if (!target.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        action.Path = requestedFile;
        return true;
    }

    private static AgentAction? TryCreateCompleteHtmlEditFallback(
        AgentAction action,
        string workingDir,
        string? failureDetail)
    {
        if (!string.Equals(action.Action, AgentAction.ActionPatch, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(failureDetail) ||
            !failureDetail.Contains("No se encontro el texto a reemplazar", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var relativePath = action.Path?.Trim();
        if (string.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathRooted(relativePath) ||
            !string.Equals(Path.GetFileName(relativePath), relativePath, StringComparison.Ordinal) ||
            !string.Equals(Path.GetExtension(relativePath), ".html", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var document = action.Replacement ?? action.Content;
        if (string.IsNullOrWhiteSpace(document) ||
            !document.Contains("<!doctype html", StringComparison.OrdinalIgnoreCase) ||
            !document.Contains("<html", StringComparison.OrdinalIgnoreCase) ||
            !document.Contains("</html>", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var target = Path.Combine(workingDir, relativePath);
        try
        {
            if (!File.Exists(target) || (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)
                return null;
        }
        catch
        {
            return null;
        }

        return new AgentAction
        {
            Action = AgentAction.ActionEditFile,
            Path = relativePath,
            Content = document,
            Reason = "Aplicar replacement como documento HTML completo despues de que patch.original no coincidio."
        };
    }

    private static async Task<string?> GroundInformationalAsync(AgentToolset toolset, string workingDir, List<AgentStep> steps, CancellationToken ct)
    {
        try
        {
            AgentStep list = await toolset.ExecuteAsync(new AgentAction { Action = AgentAction.ActionListDir, Path = "" }, 0, ct);
            steps.Add(list);
            if (!list.Success) return null;

            var files = UnreadContentFiles(workingDir, steps).Take(2).ToList();
            foreach (var f in files)
            {
                var read = await toolset.ExecuteAsync(new AgentAction { Action = AgentAction.ActionReadFile, Path = f }, 0, ct);
                if (read.Success) steps.Add(read);
            }

            return DescribeObserved(workingDir, steps);
        }
        catch
        {
            return null;
        }
    }

    private static string DescribeObserved(string workingDir, IReadOnlyList<AgentStep> steps)
    {
        var dirs = steps.Where(s => s.Success && s.Action == AgentAction.ActionListDir)
            .Select(s => s.Path ?? ".")
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .ToList();
        var files = steps.Where(s => s.Success && s.Action == AgentAction.ActionReadFile)
            .Select(s => s.Path ?? "")
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .Where(p => p.Length > 0)
            .ToList();

        var sb = new System.Text.StringBuilder();
        sb.Append("Condor observo el directorio " + workingDir + ".");
        if (files.Count > 0)
            sb.Append(" Archivos revisados: " + string.Join(", ", files) + ".");
        else
            sb.Append(" No se leyo contenido de archivos.");

        if (dirs.Count > 0)
            sb.Append(" Estructura observada: " + string.Join(", ", dirs) + ".");
        return sb.ToString();
    }

    private static List<string> UnreadContentFiles(string workingDir, IReadOnlyList<AgentStep> steps)
    {
        var readPaths = steps
            .Where(s => s.Success && s.Action == AgentAction.ActionReadFile)
            .Select(s => NormalizeRel(s.Path))
            .ToHashSet(System.StringComparer.OrdinalIgnoreCase);

        var candidates = new List<string>();
        try
        {
            foreach (var file in Directory.EnumerateFiles(workingDir, "*", SearchOption.AllDirectories))
            {
                var name = Path.GetFileName(file);
                if (name.StartsWith(".", StringComparison.Ordinal)) continue;
                if (file.Contains("\\bin\\") || file.Contains("\\obj\\") || file.Contains("\\.git\\") ||
                    file.Contains("\\node_modules\\") || file.Contains("\\.vs\\") || file.Contains("\\.artifacts\\"))
                    continue;

                var ext = Path.GetExtension(file);
                if (!IsContentExtension(ext)) continue;

                var rel = Path.GetRelativePath(workingDir, file).Replace('\\', '/');
                if (readPaths.Contains(NormalizeRel(rel))) continue;

                candidates.Add(rel);
                if (candidates.Count >= 12) break;
            }
        }
        catch
        {
            // Si falla la enumeracion del sistema de archivos, no se bloquea.
        }

        return candidates;
    }

    // Extensiones de contenido/codigo que un agente debe poder inspeccionar.
    // Amplia e independiente del ecosistema (no esta sesgada a .NET): incluye
    // codigo, marcado, estilo, documentos y manifiestos comunes. No implica una
    // regla de "leer X": solo indica evidencia de contenido pendiente.
    private static bool IsContentExtension(string ext)
    {
        switch (ext.ToLowerInvariant())
        {
            case ".cs":
            case ".vb":
            case ".fs":
            case ".ts":
            case ".js":
            case ".mjs":
            case ".cjs":
            case ".py":
            case ".go":
            case ".rs":
            case ".java":
            case ".kt":
            case ".swift":
            case ".rb":
            case ".php":
            case ".html":
            case ".htm":
            case ".css":
            case ".scss":
            case ".sass":
            case ".less":
            case ".csproj":
            case ".fsproj":
            case ".vbproj":
            case ".sln":
            case ".slnx":
            case ".csx":
            case ".json":
            case ".yaml":
            case ".yml":
            case ".xml":
            case ".ini":
            case ".cfg":
            case ".toml":
            case ".md":
            case ".markdown":
            case ".txt":
                return true;
            default:
                return false;
        }
    }

    private static string NormalizeRel(string? p) => (p ?? "").Replace('\\', '/');

    private static string BuildObservedSummary(IReadOnlyList<AgentStep> steps, string workingDir)
    {
        var dirs = steps.Where(s => s.Success && s.Action == AgentAction.ActionListDir)
            .Select(s => s.Path ?? ".")
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .ToList();
        var files = steps.Where(s => s.Success && s.Action == AgentAction.ActionReadFile)
            .Select(s => s.Path ?? "")
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .Where(p => p.Length > 0)
            .ToList();

        var sb = new System.Text.StringBuilder();
        sb.Append("Condor observo el directorio " + workingDir);

        if (files.Count > 0)
            sb.Append(" y reviso " + files.Count + " archivo(s): " + string.Join(", ", files) + ".");
        else
            sb.Append(". No llego a leer archivos.");

        if (dirs.Count > 0)
            sb.Append(" Estructura explorada: " + string.Join(", ", dirs) + ".");

        sb.Append(" Describe lo encontrado segun la evidencia observada.");
        return sb.ToString();
    }

    private static bool IsModification(string action)
        => action is AgentAction.ActionEditFile or AgentAction.ActionCreateFile or AgentAction.ActionPatch;

    /// <summary>Mapa (puro) de accion a fase de progreso, para observabilidad.</summary>
    public static AgentPhase PhaseForAction(string action)
    {
        switch (action)
        {
            case AgentAction.ActionListDir:
            case AgentAction.ActionReadFile:
            case AgentAction.ActionSearch:
                return AgentPhase.Observing;

            case AgentAction.ActionPatch:
            case AgentAction.ActionEditFile:
            case AgentAction.ActionCreateFile:
            case AgentAction.ActionUndoFile:
                return AgentPhase.Building;

            case AgentAction.ActionBuild:
            case AgentAction.ActionTest:
            case AgentAction.ActionRestore:
                return AgentPhase.Verifying;

            case AgentAction.ActionDone:
                return AgentPhase.Finalizing;

            default:
                return AgentPhase.Analyzing;
        }
    }

    private string? StartPlanB(
        string? failedAction,
        string? failedPath,
        string failureDetail,
        ref bool planBUsed,
        ref string? planBInitialFailure,
        ref bool awaitingPlanBAction,
        ref int effectiveMaxIterations,
        int failedIteration,
        List<LlmMessage> messages,
        IAgentProgressObserver? progress,
        AgentCheckpoint checkpoint)
    {
        var detail = Truncate(string.IsNullOrWhiteSpace(failureDetail) ? "sin detalle" : failureDetail);
        var location = string.IsNullOrWhiteSpace(failedPath) ? "" : " en '" + failedPath + "'";
        var redundantObservation = detail.Contains("observaciones redundantes", StringComparison.OrdinalIgnoreCase);
        var failureContext = string.IsNullOrWhiteSpace(failedAction)
            ? "Bloqueo detectado: " + detail
            : redundantObservation
                ? "Observacion repetida sin informacion nueva: " + failedAction + location + ". Resultado real: " + detail
                : "Accion fallida: " + failedAction + location + ". Resultado real: " + detail;
        var failureLabel = redundantObservation
            ? "observacion redundante (" + failedAction + location + ")"
            : string.IsNullOrWhiteSpace(failedAction) ? "falta de progreso" : failedAction + location;
        var redundantObservationGuidance = redundantObservation
            ? " Esta observacion ya produjo el mismo resultado; no la repitas. Si la tarea requiere un cambio, usa el contenido ya leido para corregir el archivo nombrado; leelo solo si aun no lo has leido."
            : "";

        if (planBUsed)
        {
            var maxPlanBIterations = Math.Max(1, _limits.MaxPlanBIterations);
            if (awaitingPlanBAction && checkpoint.PlanBIterationsUsed < maxPlanBIterations)
            {
                var allowedActions = string.Join(", ", AgentEngine.SupportedActions);
                var remainingIterations = maxPlanBIterations - checkpoint.PlanBIterationsUsed;
                effectiveMaxIterations = Math.Max(effectiveMaxIterations, failedIteration + remainingIterations);
                checkpoint.LastDecision = "plan_b_reintento";
                checkpoint.LastError = detail;
                checkpoint.NextAction = "analizar_y_corregir";
                messages.Add(new LlmMessage
                {
                    Role = "user",
                    Content = "La correccion de Plan B no paso la validacion. Resultado real: " + failureContext + redundantObservationGuidance +
                              "\nReanaliza y emite una nueva accion JSON usando exactamente uno de estos nombres permitidos: " + allowedActions +
                              ". Respeta la ruta exacta de la solicitud o una observada y resuelvela desde la raiz; no anadas prefijos no solicitados. Para crear un archivo nuevo usa create_file; para reemplazar el contenido de uno existente usa edit_file; usa patch solo con original y replacement. No inventes acciones combinadas ni repitas la accion rechazada. Quedan " + remainingIterations + " iteracion(es) de Plan B. Si no puedes corregirlo de forma segura, detente con el motivo en reason."
                });
                progress?.Report(AgentProgress.Of(
                    AgentPhase.Analyzing,
                    action: "Plan B",
                    iteration: failedIteration,
                    message: "La correccion de Plan B no paso; Condor analiza el error y prepara otra accion valida (" +
                             checkpoint.PlanBIterationsUsed + "/" + maxPlanBIterations + ")."));
                return null;
            }

            awaitingPlanBAction = false;
            checkpoint.LastDecision = "plan_b_fallido";
            checkpoint.LastError = detail;
            checkpoint.NextAction = "detener";
            var message = "Plan B no pudo corregir el fallo. Condor detuvo la tarea para evitar repetir intentos.";
            progress?.Report(AgentProgress.Of(
                AgentPhase.Finalizing,
                action: "Plan B",
                iteration: failedIteration,
                message: message));
            return "Plan B no pudo corregir el fallo inicial. Fallo inicial: " +
                   (planBInitialFailure ?? "sin detalle") + " Recuperacion fallida: " + detail;
        }

        planBUsed = true;
        planBInitialFailure = detail;
        awaitingPlanBAction = true;
        checkpoint.PlanBIterationsUsed = 0;
        effectiveMaxIterations = Math.Max(
            effectiveMaxIterations,
            failedIteration + Math.Max(1, _limits.MaxPlanBIterations));
        checkpoint.LastDecision = "plan_b";
        checkpoint.LastError = detail;
        checkpoint.NextAction = "analizar_y_corregir";

        messages.Add(new LlmMessage
        {
            Role = "user",
            Content = "PLAN B ACTIVADO tras el fallo de la iteracion " + failedIteration +
                      ". " + failureContext +
                      "\nAnaliza ese resultado y corrige la causa antes de continuar. No repitas la misma accion con los mismos datos si ya fallo; observa la ruta o adapta la accion usando la evidencia disponible. " + redundantObservationGuidance +
                      "Emite exactamente una accion JSON con un solo nombre tomado de esta lista: " + string.Join(", ", AgentEngine.SupportedActions) +
                      ". Usa la ruta exacta de la solicitud o una observada, siempre relativa a la raiz; no anadas prefijos ni subdirectorios no solicitados. Para archivo nuevo usa create_file; para existente usa edit_file o patch con original/replacement. Nunca combines nombres con '/'. " +
                      "Si la correccion tiene exito, continua la tarea y verifica el resultado. No declares done antes de ejecutar una accion de recuperacion exitosa; si no existe una correccion segura, detente con el motivo en reason."
        });

        progress?.Report(AgentProgress.Of(
            AgentPhase.Analyzing,
            action: "Plan B",
            iteration: failedIteration,
                message: "Plan B activado tras detectar " + failureLabel + "; Condor analiza el resultado y prepara una correccion."));
        return null;
    }

    private static AgentResult ApplyPlanBCompletion(AgentResult result, bool planBUsed)
    {
        if (planBUsed)
        {
            result.Reason = "Plan B recupero la tarea tras el fallo inicial. " + result.Reason;
            if (result.Checkpoint is not null)
                result.Checkpoint.LastDecision = "plan_b_corregido";
        }

        return result;
    }

    private static string HarnessCompletionReason(HarnessVerifyResult result, bool planBUsed)
    {
        var recovery = planBUsed ? "Plan B recupero la tarea tras el fallo inicial. " : "";
        if (!result.ChecksApplicable)
            return recovery + "Tarea completada; build/test no aplican a este proyecto. " +
                   (result.Reason ?? result.Detail);

        return recovery + "El harness confirmo build y pruebas.";
    }

    private static async Task<string> ListRootSnapshotAsync(AgentToolset toolset, string workingDir, CancellationToken ct)
    {
        var step = await toolset.ExecuteAsync(new AgentAction { Action = AgentAction.ActionListDir, Path = "" }, 0, ct);
        return step.ResultPreview ?? "";
    }

    private async Task<AgentModelCall> RequestActionAsync(string model, List<LlmMessage> messages, CancellationToken ct)
    {
        var resp = await _llm.CompleteAsync(new LlmRequest { Model = model, Messages = messages }, ct);

        // Fallo a nivel del proveedor (proceso terminado, server no disponible,
        // timeout real). Se devuelve el estado para que el ciclo decida recuperar
        // o detenerse sin gastar mas iteraciones como si el modelo siguiera activo.
        if (!resp.Success && resp.Outcome is not LlmOutcome.Ok)
        {
            return new AgentModelCall(null, resp.Outcome, resp.Error);
        }

        if (!resp.Success)
        {
            return new AgentModelCall(null, null, resp.Error);
        }

        var parsed = AgentActionParser.Parse(resp.Content);
        if (parsed is null)
        {
            return new AgentModelCall(null, LlmOutcome.InvalidResponse, "El modelo devolvio un JSON que no es una accion valida");
        }

        return new AgentModelCall(parsed, null, null);
    }

    private readonly record struct AgentModelCall(AgentAction? Action, LlmOutcome? ProviderFailure, string? Error);

    /// <summary>Recuperacion limitada del proveedor: comprueba health y reintenta UNA solicitud si el servidor volvio. Devuelve null si no se recupero.</summary>
    private async Task<AgentModelCall?> TryRecoverProviderAsync(string model, List<LlmMessage> messages, IAgentProgressObserver? progress, CancellationToken ct)
    {
        const int maxProbes = 3;

        for (var attempt = 0; attempt < maxProbes; attempt++)
        {
            progress?.Report(AgentProgress.Of(
                AgentPhase.Verifying,
                message: "Modelo local: intentando recuperacion (" + (attempt + 1) + "/" + maxProbes + ")",
                flag: ProgressFlag.Recovering,
                modelActivity: model + " · reintentando conexión (" + (attempt + 1) + "/" + maxProbes + ")",
                modelWorking: true));

            bool available;
            try
            {
                available = await _provider.IsAvailableAsync(ct);
            }
            catch
            {
                available = false;
            }

            if (available)
            {
                // El proveedor volvio: reintentar la solicitud actual una sola vez mas.
                progress?.Report(AgentProgress.Of(
                    AgentPhase.Verifying,
                    message: "Modelo local recuperado; reintentando la solicitud",
                    flag: ProgressFlag.Recovering));
                var retried = await RequestActionAsync(model, messages, ct);
                return retried;
            }

            // Pequena pausa antes de volver a comprobar.
            try
            {
                await Task.Delay(700, ct);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        return null;
    }

    private static string LlmOutcomeLabel(LlmOutcome outcome)
    {
        return outcome switch
        {
            LlmOutcome.Thinking => "modelo pensando (respuesta lenta)",
            LlmOutcome.ServerUnavailable => "servidor temporalmente no disponible",
            LlmOutcome.ProcessEnded => "proceso del modelo terminado inesperadamente",
            LlmOutcome.Timeout => "timeout real de la solicitud",
            LlmOutcome.InvalidResponse => "respuesta invalida del modelo",
            _ => outcome.ToString()
        };
    }

    private static string BuildProviderFailureReason(LlmOutcome outcome, string? detail)
    {
        var cause = LlmOutcomeLabel(outcome);
        var detailText = string.IsNullOrWhiteSpace(detail) ? "" : " (" + detail + ")";
        return "El proveedor del modelo fallo: " + cause + detailText + ". Condor detuvo la tarea para no consumir mas iteraciones. Revisa el diagnostico del servidor local.";
    }

    /// <summary>
    /// Construye el mensaje claro de "MODELO NO EJECUTABLE" por presupuesto de
    /// memoria. Incluye modelo (si se conoce), RAM requerida estimada, RAM
    /// disponible y el motivo exacto. Es la salida controlada ante RAM insuficiente
    /// (no se pide al usuario liberar memoria como mecanismo normal, no hay loop).
    /// </summary>
    private static string BuildModelNotExecutableReason(Condor.Core.Models.ModelSelectionResult selection)
    {
        var budget = selection.Budget;
        var resources = selection.Resources;
        // Referencia del modelo: el minimo suficiente para la tarea que no cabe
        // (bloqueo por recursos) o el deseado si existe.
        var reference = selection.Desired ?? selection.MinimumViable;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("MODELO NO EJECUTABLE");
        var model = reference is not null ? reference.PullName : "ninguno (sin modelo compatible)";
        sb.AppendLine("Modelo: " + model);
        sb.AppendLine("RAM requerida estimada: " +
                      (reference is null ? "-" : ModelEfficiencyEvaluator.PeakGb(reference).ToString("0.0")) + " GB");
        sb.AppendLine("RAM disponible: " + (resources?.FreeGb.ToString("0.0") ?? "-") + " GB");
        sb.AppendLine("Motivo: presupuesto de memoria insuficiente.");
        if (budget is not null && budget.IsBudgeted)
        {
            sb.AppendLine("Presupuesto permitido: " + budget.BudgetGb.ToString("0.0") + " GB · reserva operativa: " + budget.ReserveGb.ToString("0.0") + " GB.");
        }
        else if (resources is not null)
        {
            sb.AppendLine("Estado de recursos: " + (resources.PressureLabel ?? "sin datos") + ".");
        }

        if (resources is not null && resources.TopConsumers.Count > 0)
        {
            var top = string.Join(", ", resources.TopConsumers.Select(c => c.ProcessName + " ~" + c.WorkingSetGb.ToString("0.0") + " GB"));
            sb.AppendLine("Consumidores actuales de RAM (solo informativo): " + top + ".");
        }

        sb.AppendLine("Accion: se ha detenido de forma controlada; no se descarga ni carga un modelo que no pueda ejecutarse de forma segura.");
        return sb.ToString();
    }

    private static string? VerifyTestIntegrity(string workingDir, RepoSnapshot snapshot)
    {
        var changed = snapshot.ChangedTestFiles(workingDir);
        if (changed.Count == 0)
        {
            return null;
        }

        return "El agente modifico archivos de prueba (" + string.Join(", ", changed) +
               "). Condor no puede confirmar exito contra las pruebas originales; revierte los cambios de prueba (undo_file) y corrige el codigo de produccion en lugar de las pruebas.";
    }

    private static ResourceSnapshot? EvaluateResources()
    {
        try
        {
            var memory = new MemoryDetector().DetectAsync(CancellationToken.None).GetAwaiter().GetResult();
            var consumers = new ProcessRamDetector().DetectTopConsumers();
            return ModelMemoryBudget.Snapshot(memory, candidatePeakGb: null, consumers);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Recopila el inventario objetivo del entorno y de la decision de modelo
    /// (recursos, CPU, almacenamiento, modelos instalados, modelo seleccionado,
    /// motivo y capacidades verificadas del catalogo). Solo usa datos reales
    /// detectados o del catalogo; nunca inventa capacidades.
    /// </summary>
    private async Task<AgentInventory?> BuildInventoryAsync(
        Condor.Core.Models.ModelSelectionResult selection,
        CancellationToken ct)
    {
        try
        {
            var resources = selection.Resources;
            var desired = selection.Desired;
            var budget = selection.Budget;
            var inventory = new AgentInventory
            {
                RamTotalGb = resources?.TotalGb ?? (budget?.RamTotalGb ?? 0),
                RamFreeGb = resources?.FreeGb ?? (budget?.RamFreeGb ?? 0),
                SafeBudgetGb = budget?.BudgetGb ?? (resources?.SafeBudgetGb ?? 0),
                EstimatedModelRamGb = desired is null ? 0 : System.Math.Round(ModelEfficiencyEvaluator.PeakGb(desired), 1),
                PressureLabel = resources?.PressureLabel,
                ReserveGb = budget?.ReserveGb ?? 0,
                OperationalReserveGb = budget?.OperationalReserveGb ?? 0,
                BudgetGb = budget?.BudgetGb ?? 0,
                NodeInCurrent = selection.NodeInCurrent?.PullName,
                NextCandidate = selection.NextCandidate?.PullName
            };

            try
            {
                var cpu = await new CpuDetector().DetectAsync(ct);
                if (cpu.Status == DetectionStatus.Detected)
                {
                    var parts = new List<string>();
                    if (!string.IsNullOrWhiteSpace(cpu.Name)) parts.Add(cpu.Name);
                    if (cpu.Cores > 0) parts.Add(cpu.Cores + " nucleos");
                    if (cpu.LogicalProcessors > 0) parts.Add(cpu.LogicalProcessors + " hebras");
                    inventory.Cpu = parts.Count > 0 ? string.Join(" · ", parts) : null;
                }
            }
            catch { /* CPU opcional */ }

            try
            {
                if (_assessmentService is not null)
                {
                    var live = await _assessmentService.ExecuteAsync(new AssessmentRequest(), ct);
                    inventory.InstalledModels = live.Tools?.Ollama?.Models?.Select(m => m.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
                }
            }
            catch { /* modelos opcional */ }

            var storage = await new StorageDetector().DetectAsync(ct);
            var disks = storage.Disks;
            inventory.FreeDiskGb = disks is { Count: > 0 }
                ? System.Math.Round(disks.Max(d => d.FreeBytes) / (double)ModelMemoryBudget.BytesPerGb, 1)
                : 0;

            inventory.SelectedModel = selection.InstalledName ?? desired?.PullName;
            inventory.SelectionReason = selection.Reason;
            inventory.ModelCapabilities = desired?.Capabilities?.Any() == true ? desired.Capabilities : null;
            return inventory;
        }
        catch
        {
            return null;
        }
    }

    private static void EvaluateResourcesAndWarn(AgentCheckpoint checkpoint, IAgentProgressObserver? progress, ref bool warned, int iteration)
    {
        var snapshot = EvaluateResources();
        if (snapshot is null)
        {
            return;
        }

        checkpoint.ResourcesPressure = snapshot.PressureLabel;
        checkpoint.HeadroomGb = snapshot.HeadroomGb;

        if (snapshot.Pressure is ResourcePressure.Normal or ResourcePressure.Adjusted || warned)
        {
            return;
        }

        var builder = new System.Text.StringBuilder("Presion de memoria: " + snapshot.FreeGb + " GB libres · estado " + snapshot.PressureLabel + ".");
        builder.Append(" Condor reducira temporalmente su carga.");

        if (snapshot.TopConsumers.Count > 0)
        {
            builder.Append(" Consumidores relevantes: ");
            for (var i = 0; i < snapshot.TopConsumers.Count; i++)
            {
                var c = snapshot.TopConsumers[i];
                builder.Append(c.ProcessName + " ~" + c.WorkingSetGb + " GB" + (i < snapshot.TopConsumers.Count - 1 ? ", " : "."));
            }

            builder.Append(" Cerrar aplicaciones no necesarias podria liberar memoria (Condor no cierra procesos).");
        }

        warned = true;
        progress?.Report(AgentProgress.Of(
            AgentPhase.Verifying,
            message: builder.ToString(),
            iteration: iteration,
            flag: ProgressFlag.Recovering));
    }

    private static string? FindManifest(string workingDirectory)
    {
        try
        {
            string? f = Directory.EnumerateFiles(workingDirectory, "*.slnx", SearchOption.AllDirectories).FirstOrDefault()
                ?? Directory.EnumerateFiles(workingDirectory, "*.sln", SearchOption.AllDirectories).FirstOrDefault()
                ?? Directory.EnumerateFiles(workingDirectory, "*.csproj", SearchOption.AllDirectories)
                    .Where(p => !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) &&
                                !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                    .FirstOrDefault();
            return f is null ? null : Path.GetFileName(f);
        }
        catch { return null; }
    }

    private static AgentResult Fail(string reason, string model, string objective, List<AgentStep> steps, AgentCheckpoint checkpoint)
        => new() { Success = false, Reason = reason, Model = model, Objective = objective, Steps = steps, Checkpoint = checkpoint };

    private static string Truncate(string s) => s is { Length: > 2000 } ? s.Substring(0, 2000) + " …" : (s ?? "");
}

