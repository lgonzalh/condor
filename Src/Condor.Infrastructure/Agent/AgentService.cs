using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Condor.Core.Agent;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Infrastructure.Llm;
using Condor.Infrastructure.Setup;

namespace Condor.Infrastructure.Agent;

public sealed class AgentService : IAgentService
{
    private readonly IStateStore _stateStore;
    private readonly IAssessmentService? _assessmentService;
    private readonly OllamaClient _llm;
    private readonly AgentLimits _limits;

    public AgentService(IStateStore stateStore, IAssessmentService? assessmentService = null, AgentLimits? limits = null)
    {
        _stateStore = stateStore;
        _assessmentService = assessmentService;
        _llm = new OllamaClient();
        _limits = limits ?? AgentLimits.Default;
    }

    public async Task<AgentResult> RunAsync(string intention, CancellationToken cancellationToken)
    {
        var checkpoint = new AgentCheckpoint { Task = intention, GeneratedAtUtc = DateTime.UtcNow };
        var steps = new List<AgentStep>();
        var modifications = 0;

        var context = await _stateStore.LoadContextAsync(cancellationToken);
        if (context is null || context.Status == DetectionStatus.NotDetected || string.IsNullOrWhiteSpace(context.WorkingDirectory))
            return Fail("No hay contexto de proyecto. Ejecuta 'condor contexto' o 'condor analizar' primero.", "", intention, steps, checkpoint);

        var workingDir = context.WorkingDirectory;

        // Seleccion automatica del modelo (preparado).
        var modelSetup = new ModelAutoSetupService(_stateStore, _assessmentService);
        var selection = await modelSetup.EnsureModelAsync(cancellationToken: cancellationToken);
        if (selection.Desired is null)
            return Fail("No hay un modelo compatible disponible para la tarea.", "", intention, steps, checkpoint);

        var model = selection.InstalledName ?? selection.Desired.PullName;
        checkpoint.Model = model;
        checkpoint.Strategy = "structured-action";
        checkpoint.LastDecision = "comprender";

        var toolset = new AgentToolset(workingDir, maxContent: _limits.MaxContentLength);

        var manifest = FindManifest(workingDir);
        if (string.IsNullOrWhiteSpace(manifest))
            return Fail("No se encontro un proyecto .NET en el directorio de trabajo.", model, intention, steps, checkpoint);

        var messages = new List<LlmMessage>
        {
            new() { Role = "system", Content = BuildSystemPrompt(workingDir, manifest) },
            new() { Role = "user", Content = intention }
        };

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_limits.TimeoutMilliseconds);

        try
        {
            var invalidOutputs = 0;
            var harnessVerified = false;

            for (var iteration = 0; iteration < _limits.MaxIterations; iteration++)
            {
                checkpoint.Iteration = iteration + 1;

                // 1. Modelo produce una decision estructurada. Si el JSON es invalido, re-intentar (no fallar).
                var action = await RequestActionAsync(model, messages, timeoutCts.Token);
                if (action is null)
                {
                    invalidOutputs++;
                    if (invalidOutputs >= 3)
                    {
                        checkpoint.LastError = "El modelo no produjo acciones validas en intentos repetidos.";
                        checkpoint.NextAction = "revisar";
                        return Fail(checkpoint.LastError, model, intention, steps, checkpoint);
                    }

                    messages.Add(new LlmMessage { Role = "user", Content = "Tu respuesta no fue un JSON valido. Devuelve UNICAMENTE un JSON con la forma {\"action\":\"...\",\"path\":\"...\",\"content\":\"...\"}." });
                    continue;
                }

                invalidOutputs = 0;

                // 2. Valida la accion (schema, permitida, contenido).
                var validation = AgentEngine.ValidateAction(action);
                if (!validation.Valid)
                {
                    messages.Add(new LlmMessage { Role = "user", Content = "Accion invalida: " + (validation.Reason ?? "sin motivo") + " Devuelve nuevamente un JSON con accion permitida." });
                    continue;
                }

                // 3. Ejecuta la herramienta real y registra resultado.
                var step = await toolset.ExecuteAsync(action, iteration + 1, timeoutCts.Token);
                steps.Add(step);
                checkpoint.LastAction = action.Action + " " + (action.Path ?? "");
                checkpoint.LastResult = Truncate(step.ResultPreview ?? "");

                if ((action.Action == AgentAction.ActionEditFile || action.Action == AgentAction.ActionCreateFile) && step.Success)
                    modifications++;

                messages.Add(new LlmMessage
                {
                    Role = "user",
                    Content = "Resultado: '" + (action.Action ?? "") + "' " + (action.Path ?? "") + " -> " + (step.Success ? (step.ResultPreview ?? "ok") : ("ERROR: " + (step.ResultPreview ?? "sin detalle")))
                });

                // 4. Harness externo tras cualquier modificacion de archivo (no dependo del modelo para 'done').
                if ((action.Action is AgentAction.ActionEditFile or AgentAction.ActionCreateFile) && step.Success)
                {
                    var (hDone, hReason) = await VerifyHarnessAsync(workingDir, manifest, steps, timeoutCts.Token);
                    harnessVerified = true;
                    checkpoint.HarnessState = hDone ? "exito" : hReason;
                    checkpoint.LastError = hDone ? null : hReason;

                    if (hDone)
                    {
                        checkpoint.NextAction = "entregar";
                        checkpoint.LastDecision = "verificar";
                        return new AgentResult { Success = true, Reason = "El harness confirmo build y pruebas tras la correccion.", Model = model, Objective = intention, Steps = steps, Checkpoint = checkpoint };
                    }

                    messages.Add(new LlmMessage { Role = "user", Content = "El harness aun no confirma: " + hReason + " Corrige el codigo con las herramientas (edit_file) y vuelve a intentar." });
                }

                // 5. Si el modelo dice 'done', verificar harness y decidir exito o pedir correccion.
                if (action.Action == AgentAction.ActionDone)
                {
                    if (modifications == 0 && !harnessVerified)
                    {
                        messages.Add(new LlmMessage { Role = "user", Content = "No hubo modificaciones validadas. Modifica el codigo con 'edit_file' o usa 'build'/'test' para verificar." });
                        continue;
                    }

                    var (d2, r2) = await VerifyHarnessAsync(workingDir, manifest, steps, timeoutCts.Token);
                    checkpoint.HarnessState = d2 ? "exito" : r2;
                    checkpoint.LastError = d2 ? null : r2;

                    if (d2)
                    {
                        checkpoint.NextAction = "entregar";
                        checkpoint.LastDecision = "verificar";
                        return new AgentResult { Success = true, Reason = "El harness confirmo build y pruebas.", Model = model, Objective = intention, Steps = steps, Checkpoint = checkpoint };
                    }

                    messages.Add(new LlmMessage { Role = "user", Content = "El harness no confirmo: " + r2 + " Corrige con los herramientas." });
                    continue;
                }

                // 6. Progress check (loop improductivo / limites).
                var progress = AgentEngine.CheckProgress(iteration + 1, steps, _limits);
                if (progress.Fail)
                {
                    checkpoint.LastError = progress.Reason;
                    checkpoint.NextAction = "detener";
                    return Fail(progress.Reason ?? "Limited sin progreso.", model, intention, steps, checkpoint);
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

        return Fail("El agente no pudo completar la tarea dentro de los limites.", model, intention, steps, checkpoint);
    }

    private async Task<AgentAction?> RequestActionAsync(string model, List<LlmMessage> messages, CancellationToken ct)
    {
        var resp = await _llm.CompleteAsync(new LlmRequest { Model = model, Messages = messages }, ct);
        if (!resp.Success) return null;

        return AgentActionParser.Parse(resp.Content);
    }

    private static async Task<(bool, string?)> VerifyHarnessAsync(string workingDir, string manifest, List<AgentStep> steps, CancellationToken ct)
    {
        var toolset = new AgentToolset(workingDir);
        var build = await toolset.ExecuteAsync(new AgentAction { Action = AgentAction.ActionBuild }, 0, ct);
        if (!build.Success)
            return (false, "Build fallo: " + (build.ResultPreview ?? "sin detalle"));

        var tests = await toolset.ExecuteAsync(new AgentAction { Action = AgentAction.ActionTest }, 0, ct);
        if (!tests.Success)
            return (false, "Pruebas fallaron: " + (tests.ResultPreview ?? "sin detalle"));

        // Registrar los pasos de harness.
        steps.Add(build);
        steps.Add(tests);

        return (true, null);
    }

    private static string BuildSystemPrompt(string workingDir, string manifest)
    {
        var allowed = string.Join(", ", new[]
        {
            AgentAction.ActionListDir, AgentAction.ActionReadFile,
            AgentAction.ActionEditFile, AgentAction.ActionCreateFile,
            AgentAction.ActionBuild, AgentAction.ActionTest,
            AgentAction.ActionGitStatus, AgentAction.ActionSearch, AgentAction.ActionDone
        });

        return "Eres el agente de ingenieria local de Condor. Debes resolver la tarea sobre el proyecto en " + workingDir +
               " (manifiesto: " + manifest + "). Para cada paso devuelve UNICAMENTE un JSON valido con esta forma y sin texto extra: " +
               "{\"action\": \"<accion>\", \"path\": \"<ruta relativa o vacia>\", \"content\": \"<contenido o vacio para build/test>\", \"reason\": \"<breve explicacion>\"}. " +
               "Acciones permitidas: " + allowed + ". " +
               "Usa read_file para inspeccionar, edit_file para corregir, build para compilar, test para las pruebas, " +
               "y al final done cuando creas que la tarea esta resuelta y quieras que el harness verifique. " +
               "Solo cuando Condor confirme externamente el exito, consideralo terminado. Las rutas son relativas al proyecto.";
    }

    private static string? FindManifest(string workingDirectory)
    {
        try
        {
            string? f = Directory.EnumerateFiles(workingDirectory, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault()
                ?? Directory.EnumerateFiles(workingDirectory, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
            return f is null ? null : Path.GetFileName(f);
        }
        catch { return null; }
    }

    private static AgentResult Fail(string reason, string model, string objective, List<AgentStep> steps, AgentCheckpoint checkpoint)
        => new() { Success = false, Reason = reason, Model = model, Objective = objective, Steps = steps, Checkpoint = checkpoint };

    private static string Truncate(string s) => s is { Length: > 1000 } ? s.Substring(0, 1000) + " …" : (s ?? "");
}
