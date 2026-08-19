using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        var workingDir = Environment.CurrentDirectory;
        if (string.IsNullOrWhiteSpace(workingDir) || !Directory.Exists(workingDir))
        {
            return Fail("No hay un directorio de trabajo util. Ejecuta desde el proyecto.", "", intention, steps, checkpoint);
        }

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
        var harness = new AgentHarness(workingDir, _limits, steps);
        var originalSnapshot = RepoSnapshot.Capture(workingDir);

        var manifest = FindManifest(workingDir);
        if (string.IsNullOrWhiteSpace(manifest))
            return Fail("No se encontro un proyecto .NET en el directorio de trabajo.", model, intention, steps, checkpoint);

        var snapshot = await ListRootSnapshotAsync(toolset, workingDir, cancellationToken);

        var messages = new List<LlmMessage>
        {
            new() { Role = "system", Content = BuildSystemPrompt(workingDir, manifest) },
            new() { Role = "user", Content = "Contexto inicial del repositorio (estructura de la raiz):\n" + snapshot + "\n\nTarea: " + intention }
        };

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_limits.TimeoutMilliseconds);

        try
        {
            var invalidOutputs = 0;

            for (var iteration = 0; iteration < _limits.MaxIterations; iteration++)
            {
                checkpoint.Iteration = iteration + 1;

                // 1. Modelo produce una decision estructurada.
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

                    messages.Add(new LlmMessage { Role = "user", Content = "Tu respuesta no fue un JSON valido. Devuelve UNICAMENTE un JSON con la forma {\"action\":\"...\",\"path\":\"...\",\"original\":\"...\",\"replacement\":\"...\",\"content\":\"...\"}." });
                    continue;
                }

                invalidOutputs = 0;

                // 2. Valida la accion.
                var validation = AgentEngine.ValidateAction(action);
                if (!validation.Valid)
                {
                    messages.Add(new LlmMessage { Role = "user", Content = "Accion invalida: " + (validation.Reason ?? "sin motivo") + " Devuelve nuevamente un JSON con una accion permitida." });
                    continue;
                }

                // 3. Pre-condicion: si es build/test y no hay restauracion previa, intenta restore primero.
                //    La restauracion bajo demanda ocurre en AgentHarness cuando build/test fallan por
                //    ausencia de restauracion (NETSDK1004 / project.assets.json).

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

                // 5. Harness externo tras un cambio de codigo.
                if (IsModification(action.Action) && step.Success)
                {
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
                        return new AgentResult { Success = true, Reason = "El harness confirmo build y pruebas tras la correccion.", Model = model, Objective = intention, Steps = steps, Checkpoint = checkpoint };
                    }

                    // Devuelve al modelo la evidencia real del harness (build/test) para que decida.
                    messages.Add(new LlmMessage { Role = "user", Content = buildErrorPreamble(result) + result.Detail });
                }

                // 6. Si el modelo dice 'done', verificar harness y decidir.
                if (action.Action == AgentAction.ActionDone)
                {
                    if (modifications == 0)
                    {
                        messages.Add(new LlmMessage { Role = "user", Content = "No hubo modificaciones validadas. Modifica el codigo con 'patch'/'edit_file' o usa 'build'/'test' para verificar." });
                        continue;
                    }

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
                        return new AgentResult { Success = true, Reason = "El harness confirmo build y pruebas.", Model = model, Objective = intention, Steps = steps, Checkpoint = checkpoint };
                    }

                    messages.Add(new LlmMessage { Role = "user", Content = "El harness no confirmo: " + r.Reason + "\n" + r.Detail });
                    continue;
                }

                // 7. Progress check.
                var progress = AgentEngine.CheckProgress(iteration + 1, steps, _limits);
                if (progress.Fail)
                {
                    checkpoint.LastError = progress.Reason;
                    checkpoint.NextAction = "detener";
                    return Fail(progress.Reason ?? "Limite sin progreso.", model, intention, steps, checkpoint);
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

    private static bool IsModification(string action)
        => action is AgentAction.ActionEditFile or AgentAction.ActionCreateFile or AgentAction.ActionPatch;

    private static string buildErrorPreamble(HarnessVerifyResult r)
        => "El harness reporto un error real tras la correccion: " + r.Reason + ".\nPuedes leer de nuevo el archivo con read_file o revertir la ultima edicion con undo_file, luego corregir con patch y volver a intentar.\nEvidencia real de build/test:\n";

    private static async Task<string> ListRootSnapshotAsync(AgentToolset toolset, string workingDir, CancellationToken ct)
    {
        var step = await toolset.ExecuteAsync(new AgentAction { Action = AgentAction.ActionListDir, Path = "" }, 0, ct);
        return step.ResultPreview ?? "";
    }

    private async Task<AgentAction?> RequestActionAsync(string model, List<LlmMessage> messages, CancellationToken ct)
    {
        var resp = await _llm.CompleteAsync(new LlmRequest { Model = model, Messages = messages }, ct);
        if (!resp.Success) return null;

        return AgentActionParser.Parse(resp.Content);
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

    private static string BuildSystemPrompt(string workingDir, string manifest)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Eres el agente de ingenieria local de Condor. Resuelves la tarea sobre el proyecto en " + workingDir + " (manifiesto: " + manifest + ").");
        sb.AppendLine();
        sb.AppendLine("Devuelve UNICAMENTE un JSON valido por paso, sin texto extra, con esta forma:");
        sb.AppendLine("{\"action\": \"<accion>\", \"path\": \"<ruta relativa>\", \"original\": \"<texto exacto a localizar>\", \"replacement\": \"<texto nuevo>\", \"content\": \"<contenido o vacio>\", \"reason\": \"<breve explicacion>\"}");
        sb.AppendLine();
        sb.AppendLine("Acciones permitidas:");
        sb.AppendLine("  list_dir  \"path\"            -> listar el contenido de un directorio (usa rutas relativas a la raiz del proyecto).");
        sb.AppendLine("  read_file \"path\"            -> leer el contenido exacto de un archivo.");
        sb.AppendLine("  patch     \"path\"            -> reemplazo quirurgico: 'original' es el texto EXACTO que ya existe (copialo del read_file) y 'replacement' es el texto nuevo. Es la forma preferida de editar: no reescribes el archivo entero, conservas el resto intacto.");
        sb.AppendLine("  edit_file \"path\"            -> sobrescribe TODO el archivo con 'content'. Usalo solo si no puedes anclar un patch.");
        sb.AppendLine("  create_file \"path\"          -> crea un archivo nuevo con 'content'.");
        sb.AppendLine("  build                        -> compila el proyecto.");
        sb.AppendLine("  test                         -> ejecuta las pruebas.");
        sb.AppendLine("  restore                      -> restaura paquetes de NuGet si build/test fallan por restauracion.");
        sb.AppendLine("  git_status                   -> estado del repositorio.");
        sb.AppendLine("  search \"content\"            -> busca texto en el proyecto.");
        sb.AppendLine("  undo_file \"path\"            -> revierte la ultima edicion/patch/create sobre el archivo (recuperacion tras errores).");
        sb.AppendLine("  done                         -> termina cuando creas que la tarea esta resuelta.");
        sb.AppendLine();
        sb.AppendLine("FLUJO: primero usa list_dir y read_file para conocer la estructura real y leer el contenido exacto de los archivos. Para corregir, usa patch con 'original' copiado literalmente del archivo. No uses rutas inventadas: usa siempre rutas relativas reales que hayas visto en list_dir/read_file. Las rutas no existen si list_dir no las mostro.");
        sb.AppendLine("Si con patch/edit_file dejas el archivo roto y el build falla, puedes revertir el cambio con undo_file (igual ruta) y volver a intentar. Tambien puedes leer de nuevo el archivo con read_file para ver su estado actual despues de un fallo.");
        sb.AppendLine();
        sb.AppendLine("IMPORTANTE - HONESTIDAD Y VERIFICACION:");
        sb.AppendLine("  - NUNCA inventes ni simules exito. El harness ejecutara realmente build y test de forma externa.");
        sb.AppendLine("  - NO modifiques archivos de prueba (Tests.cs, *Tests*) para que las pruebas parezcan pasar. La tarea se resuelve corrigiendo el CODIGO DE PRODUCCION, no las pruebas. Si alteras pruebas, Condor lo detectara y no confirmara exito.");
        sb.AppendLine("  - Si lees un error real del harness, decide el siguiente paso basandote en esa evidencia (lee el archivo señalado, corrige con patch, y vuelve a build/test).");
        sb.AppendLine("  - No declares 'done' hasta que el harness haya confirmado build y test con exito.");
        sb.AppendLine("  - Si una ruta no existe, Condor te mostrara candidatos coincidentes; eligelos y sigue. No te inventes rutas.");
        sb.AppendLine("  - Cuando modifiques archivos, comprueba que el resultado compila y las pruebas pasan antes de terminar.");
        return sb.ToString();
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
