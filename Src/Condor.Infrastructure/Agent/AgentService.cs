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
    private readonly IUserConfirmation? _confirmation;
    private readonly LocalModelSession? _session;
    private readonly BudgetReevaluator? _reevaluator;

    public AgentService(
        IStateStore stateStore,
        IAssessmentService? assessmentService = null,
        AgentLimits? limits = null,
        ILlmClient? llm = null,
        ILlmProviderDiagnostics? provider = null,
        IUserConfirmation? confirmation = null,
        LocalModelSession? session = null,
        BudgetReevaluator? reevaluator = null)
    {
        _stateStore = stateStore;
        _assessmentService = assessmentService;
        _session = session;
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
        _confirmation = confirmation;
        _reevaluator = reevaluator ?? new BudgetReevaluator(BudgetPolicy.Default);
    }

    /// <summary>
    /// Crea el auto-setup de modelos reutilizando el HttpClient de la sesion
    /// compartida cuando existe, para no duplicar conectores por tarea.
    /// </summary>
    private ModelAutoSetupService CreateModelSetup()
        => new(_stateStore, _assessmentService, httpClient: _session?.SharedHttpClient);

    /// <summary>
    /// Intentos acotados de recuperacion cuando el modelo instalado no cabe por
    /// RAM libre en el presupuesto seguro. Se evita un reintento infinito:
    /// tras este numero de re-evaluaciones, Condor comunica el bloqueo de forma
    /// honesta y conserva la tarea.
    /// </summary>
    private const int MaxResourceRecoveryAttempts = 3;

    public async Task<AgentResult> RunAsync(string intention, IAgentProgressObserver? progress = null, CancellationToken cancellationToken = default)
    {
        var checkpoint = new AgentCheckpoint { Task = intention, GeneratedAtUtc = DateTime.UtcNow };
        var steps = new List<AgentStep>();
        var modifications = 0;

        var workingDir = Environment.CurrentDirectory;
        if (string.IsNullOrWhiteSpace(workingDir) || !Directory.Exists(workingDir))
        {
            return Fail("No hay un directorio de trabajo util. Ejecuta desde el proyecto.", "", intention, steps, checkpoint);
        }

        // Clasificar la tarea -> requisito de modelo (que capacidades y eficiencia).
        var requirement = TaskIntentClassifier.Classify(intention);

        // Seleccion automatica del modelo (harness dinamico por tarea + presupuesto).
        var modelSetup = CreateModelSetup();
        var selection = await modelSetup.EnsureModelForRequirementAsync(requirement, cancellationToken: cancellationToken);

        if (selection.Desired is null && !selection.BlockedByResources)
            return Fail("No hay un modelo compatible disponible para la tarea.", "", intention, steps, checkpoint);

        if (selection.Desired is null)
        {
            // El modelo EXISTE (instalado/conocido) pero la RAM libre actual no
            // permite cargarlo segun el presupuesto seguro. Se espera un numero
            // limitado de veces (delay corto + re-evaluacion viva) para dar
            // oportunidad a que la RAM se libere; NUNCA en bucle infinito.
            progress?.Report(AgentProgress.Of(
                AgentPhase.Verifying,
                message: "Modelo instalado, pero la RAM libre no permite cargarlo por ahora; comprobando de nuevo...",
                resourceState: selection.Resources?.PressureLabel,
                availableGb: selection.Resources?.FreeGb,
                safeBudgetGb: selection.Budget?.BudgetGb,
                flag: ProgressFlag.Recovering));

            var recovered = false;
            for (var attempt = 0; attempt < MaxResourceRecoveryAttempts && !recovered; attempt++)
            {
                await ResourceRecoveryDelayAsync(cancellationToken);

                selection = await modelSetup.EnsureModelForRequirementAsync(requirement, cancellationToken);
                recovered = selection.Desired is not null;
            }

            if (!recovered)
            {
                var blocked = selection.Resources;
                var reason = BuildResourceBlockedReason(blocked);

                // Intervencion OPCIONAL de RAM: si hay un confirmador interactivo
                // (consola) y el usuario confirma liberar memoria, se re-evalua UNA
                // vez mas de forma acotada y, si ahora existe un modelo viable, se
                // continua automaticamente. Cóndor NUNCA cierra aplicaciones por su
                // cuenta; si el usuario no confirma, se conserva la tarea y se sale
                // de forma limpia. Sin confirmador, el comportamiento sigue siendo
                // la salida limpia honesta actual.
                var confirmed = _confirmation is not null &&
                    await _confirmation.AskToReleaseRamAsync(
                        "La RAM disponible actualmente no permite ejecutar un modelo seguro. " +
                        "¿Quieres liberar memoria y que Cóndor vuelva a intentarlo? [S/N]",
                        cancellationToken);

                if (confirmed)
                {
                    progress?.Report(AgentProgress.Of(
                        AgentPhase.Verifying,
                        message: "Usuario confirmo liberar memoria; reevaluando RAM y seleccionando modelo...",
                        flag: ProgressFlag.Recovering));

                    selection = await modelSetup.EnsureModelForRequirementAsync(requirement, cancellationToken);
                    if (selection.Desired is not null)
                    {
                        progress?.Report(AgentProgress.Of(
                            AgentPhase.Verifying,
                            message: "RAM liberada; modelo " + (selection.InstalledName ?? selection.Desired.PullName) + " disponible.",
                            flag: ProgressFlag.Recovering));
                    }
                }

                if (selection.Desired is null)
                {
                    progress?.Report(AgentProgress.Of(
                        AgentPhase.Verifying,
                        message: reason,
                        resourceState: blocked?.PressureLabel,
                        availableGb: blocked?.FreeGb,
                        safeBudgetGb: selection.Budget?.BudgetGb,
                        flag: ProgressFlag.ProviderError));
                    // Promesa: la tarea no se pierde. La intencion queda conservada en
                    // Objective + Checkpoint; el usuario puede reintentar cuando haya
                    // recursos. Sin reintentos automaticos ilimitados.
                    return Fail(reason, "", intention, steps, checkpoint);
                }
            }
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
        var harness = new AgentHarness(workingDir, _limits, steps);
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
            var invalidOutputs = 0;
            var redundantObservations = 0;
            var resourcesWarned = false;

            var initialResources = EvaluateResources();
            progress?.Report(AgentProgress.Of(AgentPhase.Understanding,
                message: "Comprendiendo la solicitud",
                resourceState: initialResources?.PressureLabel,
                availableGb: initialResources?.FreeGb,
                safeBudgetGb: initialResources?.SafeBudgetGb));

            for (var iteration = 0; iteration < _limits.MaxIterations; iteration++)
            {
                checkpoint.Iteration = iteration + 1;

                // Punto seguro de reevaluacion del presupuesto (no se interrumpe
                // una inferencia en curso). Si la RAM cambio y existe un 1+/1- mas
                // adecuado, Condor cambia de modelo en este punto, de forma acotada.
                model = await MaybeReevaluateBudgetAtSafePointAsync(
                    model, selection.Desired, selection.NextCandidate, requirement, progress, cancellationToken);

                // 1. Modelo produce una decision estructurada. Se distingue el
                //    fallo del proveedor (crash/caida/timeout) del protocolo del
                //    modelo (respuesta no-JSON), para no consumir iteraciones
                //    como si el modelo siguiera disponible cuando no lo esta.
                var call = await RequestActionAsync(model, messages, timeoutCts.Token);

                if (call.ProviderFailure is not null)
                {
                    checkpoint.NextAction = "revisar";
                    progress?.Report(AgentProgress.Of(
                        AgentPhase.Verifying,
                        message: "Modelo local · " + LlmOutcomeLabel(call.ProviderFailure.Value) + " · " + call.Error,
                        flag: ProgressFlag.ProviderError));

                    var recovered = await TryRecoverProviderAsync(model, messages, progress, timeoutCts.Token);
                    if (recovered is not null)
                    {
                        call = recovered.Value;
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

                        if (call.Action is null && call.ProviderFailure is null)
                        {
                            // El proveedor volvio pero la salida no fue JSON: protocolo, no proveedor.
                            invalidOutputs++;
                            messages.Add(new LlmMessage { Role = "user", Content = "El modelo respondio pero no como JSON; reintenta con una accion estructurada valida." });
                            continue;
                        }
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
                    invalidOutputs++;
                    if (invalidOutputs >= _limits.MaxInvalidOutputs)
                    {
                        checkpoint.NextAction = "revisar";
                        // Para una solicitud informativa de comprension, Condor
                        // observa por si mismo (list_dir real + lee archivos
                        // representativos) y entrega una descripcion fundamentada
                        // en EVIDENCIA REAL, sin inventar exito ni codigo.
                        if (IsInformationalRequest(intention))
                        {
                            checkpoint.LastDecision = "describir";
                            progress?.Report(AgentProgress.Of(AgentPhase.Finalizing, message: "Preparando respuesta"));
                            var grounded = await GroundInformationalAsync(toolset, workingDir, steps, timeoutCts.Token);
                            return grounded is null
                                ? Fail(checkpoint.LastError ?? "No fue posible describir el directorio.", model, intention, steps, checkpoint)
                                : new AgentResult { Success = true, Reason = grounded, Model = model, Objective = intention, Steps = steps, Checkpoint = checkpoint, Inventory = inventory };
                        }

                        checkpoint.LastError = "El modelo no produjo acciones estructuradas validas tras varios intentos.";
                        return Fail(checkpoint.LastError, model, intention, steps, checkpoint);
                    }

                    // Guiado situacional (no lista rigida): segun cuanto se ha
                    // observado, se sugiere el siguiente paso concreto en formato
                    // JSON para que el modelo entre al protocolo estructurado.
                    string hint;
                    if (steps.Count == 0)
                    {
                        hint = "Todavia no has observado el directorio. Emite exactamente: {\"action\":\"list_dir\",\"path\":\"\",\"reason\":\"observar el proyecto\"}";
                    }
                    else if (!steps.Any(s => s.Success && s.Action == AgentAction.ActionReadFile))
                    {
                        hint = "Ya listaste pero aun no lees ningun archivo. Emite read_file sobre uno de los archivos que viste en list_dir.";
                    }
                    else
                    {
                        hint = "Ya observas contenido. Si tienes suficiente evidencia, responde con done usando 'reason' para tu sintesis; si no, continua leyendo/comprendiendo.";
                    }

                    messages.Add(new LlmMessage { Role = "user", Content = "Tu texto no fue un JSON valido. No respondas con prosa: emite un JSON de accion. " + hint + ". Forma valida: {\"action\":\"<accion>\",\"path\":\"<ruta>\",\"original\":\"\",\"replacement\":\"\",\"content\":\"\",\"reason\":\"<justificacion o sintesis>\"}." });
                    continue;
                }

                invalidOutputs = 0;

                // A partir de aqui hay una accion util; el caso nulo ya hizo continue.
                var action = call.Action!;

                // 2. Valida la accion.
                var validation = AgentEngine.ValidateAction(action);
                if (!validation.Valid)
                {
                    messages.Add(new LlmMessage { Role = "user", Content = "Accion invalida: " + (validation.Reason ?? "sin motivo") + " Devuelve nuevamente un JSON con una accion permitida." });
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

                // 4b. Reevaluacion de recursos en cada accion (presupuesto dinamico):
                //     si la presion empeora (Presion/Insuficiente), se advierte una vez
                //     (sin saturar), y se reduce la carga propia al no lanzar build/test
                //     pesado en ese instante si estamos en Presion.
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
                            if (modifications == 0)
                            {
                                checkpoint.NextAction = "entregar";
                                checkpoint.LastDecision = "describir";
                                var summary = BuildObservedSummary(steps, workingDir);
                                return new AgentResult { Success = true, Reason = summary, Model = model, Objective = intention, Steps = steps, Checkpoint = checkpoint };
                            }

                            checkpoint.LastError = "Observaciones redundantes sin informacion nueva y sin un cambio de codigo verificado.";
                            checkpoint.NextAction = "detener";
                            return Fail(checkpoint.LastError, model, intention, steps, checkpoint);
                        }

                        messages.Add(new LlmMessage { Role = "user", Content = "Ya observaste esto exactamente antes y el resultado no cambio; repetir la misma observacion no aporta informacion nueva. Si la solicitud es de comprension/analisis, sintetiza lo visto y responde con done. Si la tarea requiere un cambio de codigo, edita con patch/edit_file y verifica con build/test." });
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
                    checkpoint.HarnessState = result.Done ? "exito" : result.Reason;
                    checkpoint.LastError = result.Done ? null : result.Reason;

                    if (result.Done)
                    {
                        var integrity = VerifyTestIntegrity(workingDir, originalSnapshot);
                        if (integrity is not null)
                        {
                            checkpoint.NextAction = "detener";
                            checkpoint.LastDecision = "revisar";
                            return Fail(integrity, model, intention, steps, checkpoint);
                        }

                        checkpoint.NextAction = "entregar";
                        checkpoint.LastDecision = "verificar";
                        progress?.Report(AgentProgress.Of(AgentPhase.Finalizing, message: "Finalizando"));
                        return new AgentResult { Success = true, Reason = "El harness confirmo build y pruebas tras la correccion.", Model = model, Objective = intention, Steps = steps, Checkpoint = checkpoint };
                    }

                    // Devuelve al modelo la evidencia real del harness (build/test) para que decida.
                    messages.Add(new LlmMessage { Role = "user", Content = buildErrorPreamble(result) + result.Detail });
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
                        checkpoint.LastDecision = "describir";
                        progress?.Report(AgentProgress.Of(AgentPhase.Finalizing, message: "Preparando respuesta"));
                        var summary = action.Reason ?? action.Content ?? "";
                        return new AgentResult {
                            Success = true,
                            Reason = string.IsNullOrWhiteSpace(summary)
                                ? "Condor observo el directorio y describio lo encontrado."
                                : summary,
                            Inventory = inventory,
                            Model = model, Objective = intention, Steps = steps, Checkpoint = checkpoint
                        };
                    }

                    progress?.Report(AgentProgress.Of(AgentPhase.Verifying, action: "build/test", iteration: iteration + 1));
                    var r = await harness.VerifyAsync(timeoutCts.Token);
                    checkpoint.HarnessState = r.Done ? "exito" : r.Reason;
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
                        checkpoint.LastDecision = "verificar";
                        progress?.Report(AgentProgress.Of(AgentPhase.Finalizing, message: "Finalizando"));
                        return new AgentResult { Success = true, Reason = "El harness confirmo build y pruebas.", Model = model, Objective = intention, Steps = steps, Checkpoint = checkpoint };
                    }

                    messages.Add(new LlmMessage { Role = "user", Content = "El harness no confirmo: " + r.Reason + "\n" + r.Detail });
                    continue;
                }

                // 7. Progress check.
                var progressCheck = AgentEngine.CheckProgress(iteration + 1, steps, _limits);
                if (progressCheck.Fail)
                {
                    checkpoint.LastError = progressCheck.Reason;

                    // Si la intencion es de comprension/diagnostico y hay evidencia,
                    // se entrega la respuesta observada en vez de un crptico fallo
                    // por limite de iteraciones.
                    var delivered = TryDeliverInformational(checkpoint, steps, workingDir, model, intention, progress, lastDoneReason, inventory);
                    if (delivered is not null)
                    {
                        return delivered;
                    }

                    checkpoint.NextAction = "detener";
                    return Fail(progressCheck.Reason ?? "Limite sin progreso.", model, intention, steps, checkpoint);
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
        return TryDeliverInformational(checkpoint, steps, workingDir, model, intention, progress, lastDoneReason, inventory)
            ?? Fail("El agente no pudo completar la tarea dentro de los limites.", model, intention, steps, checkpoint);
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
    {
        if (string.IsNullOrWhiteSpace(intention)) return true;
        var t = intention.ToLowerInvariant();
        return t.Contains("revisa") ||
               t.Contains("cuentame") ||
               t.Contains("describe") ||
               t.Contains("explime") ||
               t.Contains("explica") ||
               t.Contains("que es") ||
               t.Contains("que contiene") ||
               t.Contains("analiza") ||
               t.Contains("resumen") ||
               t.Contains("contenido") ||
               t.Contains("what is") ||
               t.Contains("tell me");
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
            case ".cs": case ".vb": case ".fs": case ".ts": case ".js": case ".mjs": case ".cjs":
            case ".py": case ".go": case ".rs": case ".java": case ".kt": case ".swift": case ".rb": case ".php":
            case ".html": case ".htm": case ".css": case ".scss": case ".sass": case ".less":
            case ".csproj": case ".fsproj": case ".vbproj": case ".sln": case ".slnx": case ".csx": case ".json":
            case ".yaml": case ".yml": case ".xml": case ".ini": case ".cfg": case ".toml":
            case ".md": case ".markdown": case ".txt":
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

    private static string buildErrorPreamble(HarnessVerifyResult r)
        => "El harness reporto un error real tras la correccion: " + r.Reason + ".\nPuedes leer de nuevo el archivo con read_file o revertir la ultima edicion con undo_file, luego corregir con patch y volver a intentar.\nEvidencia real de build/test:\n";

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
                flag: ProgressFlag.Recovering));

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

    private static async Task ResourceRecoveryDelayAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(700, ct);
        }
        catch (OperationCanceledException)
        {
            // Cancelacion cooperativa: el gate abandona la espera.
        }
    }

    private static string BuildResourceBlockedReason(Condor.Core.Models.ResourceSnapshot? resources)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("El modelo instalado no se pudo cargar ahora: la RAM libre (" +
                  (resources?.FreeGb.ToString("0.0") ?? "-") + " GB) no alcanza el presupuesto seguro (" +
                  (resources?.SafeBudgetGb.ToString("0.0") ?? "-") + " GB) para el modelo minimo compatible.");
        sb.Append(" Es un bloqueo TEMPORAL por recursos, no la ausencia de un modelo: libera memoria");
        sb.Append(" (por ejemplo cerrando procesos de alto consumo) y reintenta la misma tarea.");
        if (resources is not null && resources.TopConsumers.Count > 0)
        {
            var top = string.Join(", ", resources.TopConsumers.Select(c => c.ProcessName + " ~" + c.WorkingSetGb.ToString("0.0") + " GB"));
            sb.Append(" Consumidores actuales: " + top + ".");
        }

        sb.Append(" Estado: " + (resources?.PressureLabel ?? "sin datos") + ".");
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
    /// Reevaluacion dinamica del presupuesto en un punto seguro (entre inferencias).
    /// Si la RAM cambio de forma significativa y existe un candidato mas adecuado
    /// (1+ al subir RAM, o una alternativa viable al bajar), Condor cambia el modelo
    /// actual de forma ACOTADA (el reevaluador limita los cambios para evitar bucles)
    /// y reregistra la sesion (libera el anterior, asegura el nuevo). Nunca interrumpe
    /// una inferencia en curso. Devuelve el nombre de modelo a usar a continuacion.
    /// </summary>
    private async Task<string> MaybeReevaluateBudgetAtSafePointAsync(
        string model,
        ModelCandidate? node,
        ModelCandidate? next,
        TaskModelRequirement requirement,
        IAgentProgressObserver? progress,
        CancellationToken ct)
    {
        if (_reevaluator is null)
        {
            return model;
        }

        Condor.Core.Models.MemoryInfo? memory;
        try
        {
            memory = await new MemoryDetector().DetectAsync(ct);
        }
        catch
        {
            return model;
        }

        // Reevaluamos solo cuando el intervalo haya transcurrido desde la ultima
        // evaluacion para no reintentar en cada iteracion del loop (politica).
        var elapsed = DateTime.UtcNow - _lastBudgetCheckUtc;
        if (_lastBudgetCheckUtc != DateTime.MinValue && elapsed < _reevaluator.ReevaluationInterval)
        {
            return model;
        }
        _lastBudgetCheckUtc = DateTime.UtcNow;

        var decision = _reevaluator.Decide(
            memory, node ?? FindNodeByName(model), next, requirement, _budgetChanges);

        switch (decision.Transition)
        {
            case BudgetTransition.UpgradeToNext:
            case BudgetTransition.Downgrade:
                if (string.IsNullOrWhiteSpace(decision.SuggestedModel))
                {
                    return model;
                }

                _budgetChanges++;
                var newModel = decision.SuggestedModel!;
                progress?.Report(AgentProgress.Of(
                    AgentPhase.Verifying,
                    message: "Presupuesto reevaluado (" + (decision.Budget?.BudgetGb.ToString("0.0") ?? "?") +
                              " GB): " + (decision.Transition == BudgetTransition.UpgradeToNext ? "subiendo a 1+ " : "degradando a ") +
                              newModel + " en punto seguro.",
                    flag: ProgressFlag.Recovering));

                if (_session is not null)
                {
                    // Liberar el modelo anterior como parte de la transicion (Ollama
                    // keep_alive=0) y registrar el nuevo como sesion activa.
                    await _session.ReleaseAsync(ct);
                    await _session.EnsureAvailableAsync(newModel, ct);
                }

                return newModel;

            default:
                return model;
        }
    }

    private ModelCandidate? FindNodeByName(string model)
    {
        foreach (var c in Condor.Core.Catalog.ModelCatalog.Default)
        {
            if (c.PullName.Equals(model, StringComparison.OrdinalIgnoreCase) ||
                c.Name.Equals(model, StringComparison.OrdinalIgnoreCase))
            {
                return c;
            }
        }
        return null;
    }

    private DateTime _lastBudgetCheckUtc = DateTime.MinValue;
    private int _budgetChanges = 0;

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
                SafeBudgetGb = resources?.SafeBudgetGb ?? (budget?.BudgetGb ?? 0),
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
        builder.Append(" Condor reducirá temporalmente su carga.");

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
