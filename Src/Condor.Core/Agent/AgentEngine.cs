using System.Collections.Generic;
using System.Linq;
using Condor.Core.Evaluation;
using Condor.Core.Models;

namespace Condor.Core.Agent;

public readonly record struct ActionValidation(bool Valid, string? Reason);

public readonly record struct ProgressDecision(bool Done, bool Fail, string? Reason);

public enum ObservationSignal
{
    NewInformation,
    Redundant
}

public enum IntentFlavor
{
    Descriptive,
    Diagnostic,
    Build,
    Unknown
}

public static class AgentEngine
{
    private const int MaxPlanSteps = 12;

    private static readonly string[] CanonicalActions =
    {
        AgentAction.ActionListDir, AgentAction.ActionReadFile,
        AgentAction.ActionPatch, AgentAction.ActionEditFile,
        AgentAction.ActionCreateFile,
        AgentAction.ActionBuild, AgentAction.ActionTest,
        AgentAction.ActionRestore, AgentAction.ActionGitStatus, AgentAction.ActionGitDiff,
        AgentAction.ActionGitLog, AgentAction.ActionGitShow,
        AgentAction.ActionSearch, AgentAction.ActionUndoFile,
        AgentAction.ActionPlan, AgentAction.ActionClarify,
        AgentAction.ActionDelegate, AgentAction.ActionDone
    };
    private static readonly HashSet<string> AllowedActions = new(CanonicalActions, System.StringComparer.Ordinal);

    /// <summary>Nombres exactos que el modelo puede usar en acciones JSON.</summary>
    public static IReadOnlyList<string> SupportedActions => System.Array.AsReadOnly(CanonicalActions);

    public static ActionValidation ValidateAction(AgentAction action)
    {
        if (action is null || string.IsNullOrWhiteSpace(action.Action))
            return new ActionValidation(false, "Accion vacia o nula.");

        if (!AllowedActions.Contains(action.Action))
            return new ActionValidation(false, "Accion no permitida: " + action.Action);

        var validation = ValidateFileMutation(action) ?? ValidatePatch(action) ??
            ValidateUndoFile(action) ?? ValidateGitShow(action);
        if (validation is not null)
            return validation.Value;

        if (action.Action == AgentAction.ActionPlan)
            return ValidatePlan(action);

        validation = ValidateClarify(action) ?? ValidateDelegation(action);
        if (validation is not null)
            return validation.Value;

        return new ActionValidation(true, null);
    }

    private static ActionValidation? ValidateFileMutation(AgentAction action)
    {
        if (action.Action != AgentAction.ActionEditFile && action.Action != AgentAction.ActionCreateFile)
            return null;
        if (string.IsNullOrWhiteSpace(action.Path))
            return new ActionValidation(false, "Se requiere 'path' para la accion de archivo.");
        return string.IsNullOrWhiteSpace(action.Content)
            ? new ActionValidation(false, "Se requiere 'content' para la accion de archivo.")
            : null;
    }

    private static ActionValidation? ValidatePatch(AgentAction action)
    {
        if (action.Action != AgentAction.ActionPatch)
            return null;
        if (string.IsNullOrWhiteSpace(action.Path))
            return new ActionValidation(false, "Se requiere 'path' para la accion patch.");
        if (string.IsNullOrEmpty(action.Original) && string.IsNullOrWhiteSpace(action.Content))
            return new ActionValidation(false, "Se requiere 'original' (texto a localizar) para la accion patch.");
        return action.Replacement is null && action.Content is null
            ? new ActionValidation(false, "Se requiere 'replacement' (texto nuevo) para la accion patch.")
            : null;
    }

    private static ActionValidation? ValidateUndoFile(AgentAction action)
        => action.Action == AgentAction.ActionUndoFile && string.IsNullOrWhiteSpace(action.Path)
            ? new ActionValidation(false, "Se requiere 'path' para deshacer una edición.")
            : null;

    private static ActionValidation? ValidateGitShow(AgentAction action)
        => action.Action == AgentAction.ActionGitShow &&
           (string.IsNullOrWhiteSpace(action.Path) || action.Path.Length is < 7 or > 40 ||
            action.Path.Any(ch => !Uri.IsHexDigit(ch)))
            ? new ActionValidation(false, "git_show requiere en 'path' un hash de commit hexadecimal de 7 a 40 caracteres.")
            : null;

    private static ActionValidation? ValidateClarify(AgentAction action)
        => action.Action == AgentAction.ActionClarify &&
           string.IsNullOrWhiteSpace(action.Reason) && string.IsNullOrWhiteSpace(action.Content)
            ? new ActionValidation(false, "La aclaración requiere una pregunta concreta.")
            : null;

    private static ActionValidation? ValidateDelegation(AgentAction action)
    {
        if (action.Action != AgentAction.ActionDelegate)
            return null;
        if (action.Subtasks is not { Count: > 0 and <= 4 })
            return new ActionValidation(false, "La delegación requiere entre 1 y 4 subtareas independientes.");

        var ids = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var subtask in action.Subtasks)
        {
            if (subtask is null || string.IsNullOrWhiteSpace(subtask.Id) || subtask.Id.Length > 40 ||
                subtask.Id.Any(ch => !char.IsLetterOrDigit(ch) && ch is not '-' and not '_'))
                return new ActionValidation(false, "Cada subtarea requiere un id corto con letras, números, guion o guion bajo.");
            if (!ids.Add(subtask.Id))
                return new ActionValidation(false, "Los ids de las subtareas deben ser únicos.");
            if (string.IsNullOrWhiteSpace(subtask.Task) || subtask.Task.Length > 6000)
                return new ActionValidation(false, "Cada subtarea requiere una instrucción de hasta 6000 caracteres.");
            if (subtask.ProfileId is not ("engineer" or "reviewer" or "security-reviewer" or "test-engineer"))
                return new ActionValidation(false, "La subtarea usa un perfil desconocido.");
            if (subtask.MaxDurationSeconds is < 15 or > 1800)
                return new ActionValidation(false, "La duración de cada agente debe estar entre 15 y 1800 segundos.");
            if (subtask.MaxIterations is < 1 or > 20)
                return new ActionValidation(false, "Las iteraciones de cada agente deben estar entre 1 y 20.");
            if (subtask.MaxOutputTokens is < 128 or > 65_536)
                return new ActionValidation(false, "El presupuesto de salida de cada agente debe estar entre 128 y 65536 tokens.");
        }

        return null;
    }

    private static ActionValidation ValidatePlan(AgentAction action)
    {
        if (string.IsNullOrWhiteSpace(action.Objective))
            return new ActionValidation(false, "El plan requiere un objetivo explícito.");
        if (action.ScopePaths is not { Count: > 0 } || action.ScopePaths.Any(path => !IsSafeScopePath(path)))
            return new ActionValidation(false, "El plan requiere el campo scopePaths con rutas relativas seguras desde la raíz del proyecto.");
        if (action.ExcludedPaths?.Any(path => !IsSafeScopePath(path)) == true)
            return new ActionValidation(false, "excludedPaths contiene una ruta insegura.");
        if (action.PlanSteps is not { Count: > 0 } || action.PlanSteps.Any(string.IsNullOrWhiteSpace))
            return new ActionValidation(false, "El plan requiere tareas ordenadas.");
        if (action.PlanSteps.Count > MaxPlanSteps)
            return new ActionValidation(false, "El plan excede 12 tareas; divídelo en un conjunto más pequeño y verificable.");
        if (action.PlanSteps.Distinct(System.StringComparer.OrdinalIgnoreCase).Count() != action.PlanSteps.Count)
            return new ActionValidation(false, "Las tareas del plan deben ser únicas y estar ordenadas.");
        if (action.ExpectedResults is not { Count: > 0 } || action.ExpectedResults.Any(string.IsNullOrWhiteSpace))
            return new ActionValidation(false, "El plan requiere un resultado esperado para cada tarea.");
        if (action.ExpectedResults.Count != action.PlanSteps.Count)
            return new ActionValidation(false, "Cada tarea del plan requiere exactamente un resultado esperado, en el mismo orden.");
        if (action.VerificationSteps is not { Count: > 0 } || action.VerificationSteps.Any(string.IsNullOrWhiteSpace))
            return new ActionValidation(false, "El plan requiere comprobaciones de salida.");
        if (action.VerificationSteps.Count != action.PlanSteps.Count)
            return new ActionValidation(false, "Cada tarea del plan requiere exactamente una comprobación propia, en el mismo orden.");
        if (action.VerificationSteps.Distinct(System.StringComparer.OrdinalIgnoreCase).Count() != action.VerificationSteps.Count)
            return new ActionValidation(false, "Cada tarea del plan requiere una comprobación distinta y verificable.");

        return new ActionValidation(true, null);
    }

    public static bool IsPathWithinScope(string? path, IReadOnlyList<string>? scopePaths, IReadOnlyList<string>? excludedPaths = null)
    {
        if (!IsSafeScopePath(path) || scopePaths is not { Count: > 0 }) return false;
        if (excludedPaths?.Any(scope => MatchesScope(path!, scope)) == true) return false;
        return scopePaths.Any(scope => MatchesScope(path!, scope));
    }

    private static bool IsSafeScopePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var normalized = path.Replace('\\', '/').Trim();
        if (normalized.StartsWith('/') || normalized.Contains(':')) return false;
        if (normalized.Contains('*') && !normalized.EndsWith("/**", System.StringComparison.Ordinal)) return false;
        if (normalized.Split('/').Any(segment => segment == ".." || segment.Length == 0)) return false;
        return normalized == "." || !normalized.Split('/').Any(segment => segment == ".");
    }

    private static bool MatchesScope(string path, string scope)
    {
        var candidate = path.Replace('\\', '/').TrimStart('/');
        if (candidate.StartsWith("./", System.StringComparison.Ordinal)) candidate = candidate.Substring(2);
        var declared = scope.Replace('\\', '/').Trim();
        if (declared == ".") return true;
        if (declared.EndsWith("/**", System.StringComparison.Ordinal))
        {
            var directory = declared.Substring(0, declared.Length - 3).TrimEnd('/');
            return candidate.Equals(directory, System.StringComparison.OrdinalIgnoreCase) ||
                   candidate.StartsWith(directory + "/", System.StringComparison.OrdinalIgnoreCase);
        }
        return candidate.Equals(declared, System.StringComparison.OrdinalIgnoreCase);
    }

    public static ProgressDecision EvaluateHarness(bool buildOk, bool testsOk, string? buildError, string? testsError)
    {
        if (!buildOk)
            return new ProgressDecision(false, false, "Build fallo: " + First(buildError));
        if (!testsOk)
            return new ProgressDecision(false, false, "Pruebas fallaron: " + First(testsError));
        return new ProgressDecision(true, false, "Harness confirmo build y pruebas externamente.");
    }

    /// <summary>
    /// Clasifica una observacion recien ejecutada: es Redundant si el modelo
    /// repite la MISMA observacion (accion + ruta) y obtiene el MISMO resultado
    /// que una previa (no aporta informacion nueva). Es NewInformation en caso
    /// contrario. General: nunca penaliza rutas nuevas ni resultados que cambian.
    /// </summary>
    public static ObservationSignal AssessObservation(AgentStep step, IReadOnlyList<AgentStep> priorSteps)
    {
        if (!IsObservation(step.Action) || !step.Success)
            return ObservationSignal.NewInformation;

        foreach (var prior in priorSteps)
        {
            if (prior.Action == step.Action &&
                SamePath(prior.Path, step.Path) &&
                Equals(prior.ResultPreview, step.ResultPreview))
            {
                return ObservationSignal.Redundant;
            }
        }

        return ObservationSignal.NewInformation;
    }

    private static bool IsObservation(string action)
        => action is AgentAction.ActionListDir or AgentAction.ActionReadFile or AgentAction.ActionSearch or AgentAction.ActionGitDiff or AgentAction.ActionGitLog or AgentAction.ActionGitShow;

    private static bool SamePath(string? a, string? b)
        => string.Equals(a, b, System.StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Clasifica la intencion libre en un matiz general (Descriptiva, diagnostica,
    /// construccion o desconocida) para decidir que evidencia es suficiente. No es
    /// una regla rigida de archivos: es una heuristica sobre el texto y el estado.
    /// </summary>
    public static IntentFlavor ClassifyIntent(string intention)
    {
        if (string.IsNullOrWhiteSpace(intention)) return IntentFlavor.Unknown;
        var t = intention.ToLowerInvariant();

        bool hasBuild = TaskIntentClassifier.RequiresModification(t);
        bool hasError = t.Contains("error") || t.Contains("falla") || t.Contains("fallo")
            || t.Contains("bug") || t.Contains("no funciona") || t.Contains("corrige")
            || t.Contains("arregla") || t.Contains("roto") || t.Contains("revisa si");
        bool hasDescribe = t.Contains("que tenemos") || t.Contains("describe") || t.Contains("cuentame")
            || t.Contains("que es") || t.Contains("que contiene") || t.Contains("resumen");

        // Cuando la solicitud pide corregir un error, conserva el matiz
        // diagnóstico aunque también incluya una acción de modificación.
        // La validación de finalización acepta la corrección verificada como
        // evidencia; clasificarla como Unknown permitiría cerrar tras solo leer.
        if (hasError) return IntentFlavor.Diagnostic;
        if (hasBuild) return IntentFlavor.Build;
        if (hasDescribe) return IntentFlavor.Descriptive;
        return IntentFlavor.Unknown;
    }

    /// <summary>
    /// Decide, de forma general y no rigida, si el agente tiene evidencia suficiente
    /// para responder con 'done' dadas la intencion y las observaciones reales.
    /// </summary>
    public static (bool Sufficient, string? Hint) HasSufficientEvidenceForDone(string intention, IReadOnlyList<AgentStep> steps)
    {
        var flavor = ClassifyIntent(intention);
        var readFiles = steps.Where(s => s.Success && s.Action == AgentAction.ActionReadFile).ToList();
        bool readAny = readFiles.Count > 0;
        bool explored = steps.Any(s => s.Success && (s.Action == AgentAction.ActionListDir || s.Action == AgentAction.ActionReadFile));

        if (!explored)
            return (false, "No observaste el directorio todavia; usa list_dir y read_file para conocer que existe antes de responder.");

        if (flavor == IntentFlavor.Descriptive)
        {
            // Para describir basta con la estructura y al menos un archivo leido.
            if (readAny) return (true, null);
            return (false, "Aun no leiste ningun archivo; para describir que contiene realmente, usa read_file sobre un archivo representativo del directorio, luego haz done.");
        }

        if (flavor == IntentFlavor.Diagnostic)
        {
            // Diagnosticar un error no es leer UN archivo representativo: es
            // comprender el alcance del proyecto. En una solicitud ABIERTA de
            // deteccion de errores (p. ej. "hay un error, revisa este directorio"),
            // el error puede estar en la logica, en un archivo de estilo/estructura,
            // o en una INCOHERENCIA ENTRE ARCHIVOS (una referencia a un archivo que
            // no existe, un recurso declarado pero ausente, un enlace roto). Por
            // eso una sola lectura no basta: se exige evidencia transversal
            // (relacionar varios archivos o una busqueda de 'error'). No es una
            // lista rigida de archivos: es exigir alcance, no una ruta concreta.
            var changed = steps.Any(s => s.Success && s.Action is AgentAction.ActionPatch or AgentAction.ActionEditFile or AgentAction.ActionCreateFile);
            if (changed) return (true, null);

            if (TaskIntentClassifier.RequiresModification(intention))
            {
                return (false, "La solicitud exige modificar archivos; la lectura o el diagnóstico no completan el cambio pedido. Ejecuta el cambio autorizado y verifícalo antes de concluir.");
            }

            var searchedErrors = steps.Any(s => s.Success && s.Action == AgentAction.ActionSearch);

            var readContent = readFiles
                .Where(s => IsReadableContent(s.Path))
                .Select(s => NormalizePath(s.Path))
                .Distinct()
                .ToList();
            var distinctRead = readContent.Count;

            // Solicitud enfocada a un archivo/modulo concreto: basta con haber leido
            // su contenido (el objetivo esta acotado por el propio usuario). Es una
            // heuristica general (detecta la referencia textual a un objetivo),
            // no una regla de nombres ni de ecosistema.
            if (IntentNamesSpecificTarget(intention))
            {
                if (distinctRead >= 1) return (true, null);
                return (false, "La solicitud apunta a un archivo concreto; usa read_file sobre el archivo senalado para localizar el error (o un search de 'error' si no encuentras el archivo).");
            }

            // Solicitud ABIERTA de deteccion de errores: exige alcance transversal
            // sobre CONTENIDO REAL. Un search solo no basta: sin haber leido
            // ningun archivo no se comprende el error ni su relacion con el resto.
            // Se acepta (a) dos archivos distintos leidos (relacion entre ellos),
            // o (b) al menos un archivo leido mas una busqueda de errores.
            if (distinctRead >= 2 || (distinctRead >= 1 && searchedErrors)) return (true, null);

            var openHint = readAny
                ? "Para una revision abierta de errores, leer o buscar no basta: necesitas leer CONTENIDO real y relacionarlo. Lee los archivos RELACIONADOS (el que enlaza o referencia y los recursos que usa: css/script/import/recursos) y verifica la coherencia entre ellos (por ejemplo, que existan los archivos que se referencian y que cada recurso declarado este presente)."
                : "Para diagnosticar, lee contenido real: observa que archivos existen (list_dir) y luego lee el archivo de entrada o referencia (pagina/principal) y los recursos que declara, para detectar incoherencias entre archivos y errores reales; un search sin leer no basta.";
            return (false, openHint);
        }

        if (flavor == IntentFlavor.Build)
        {
            // Para construir/crear hace falta una modificacion verificada (harness).
            var modified = steps.Any(s => s.Success && s.Action is AgentAction.ActionPatch or AgentAction.ActionEditFile or AgentAction.ActionCreateFile);
            if (!modified) return (false, "Pediste construir/crear, pero aun no modificaste ningun archivo; escribe el codigo con create_file/edit_file o aplica cambios y verifica con build/test.");
            return (true, null);
        }

        // Intencion desconocida: confiar en la exploracion minimamente fundamentada.
        return (readAny ? (true, null) : (false, "Aun no leiste ningun archivo; aporta al menos una lectura real antes de concluir, o especifica que necesitas construir/arreglar."));
    }

    public static ProgressDecision CheckProgress(
        int iteration,
        IReadOnlyList<AgentStep> steps,
        AgentLimits limits,
        int? maxIterationsOverride = null)
    {
        var repeatedFailureLimit = Math.Max(2, limits.MaxRepeatedFailedAction);
        var last = steps.LastOrDefault();
        if (last is { Success: false })
        {
            var repeatedFailures = steps.Reverse()
                .TakeWhile(step =>
                    !step.Success &&
                    string.Equals(step.Action, last.Action, StringComparison.Ordinal) &&
                    string.Equals(step.Path, last.Path, StringComparison.OrdinalIgnoreCase))
                .Take(repeatedFailureLimit)
                .Count();

            if (repeatedFailures >= repeatedFailureLimit)
                return new ProgressDecision(false, true, "Plan B no corrigio la misma accion fallida; Condor detuvo la repeticion.");
        }

        var recent = steps.TakeLast(limits.MaxRepeatedAction).ToList();
        if (recent.Count >= limits.MaxRepeatedAction)
        {
            var allSame = recent.All(s =>
                s.Action == recent[0].Action &&
                s.Path == recent[0].Path &&
                string.IsNullOrEmpty(s.ResultPreview));
            if (allSame)
                return new ProgressDecision(false, true, "Loop improductivo (misma accion sin progreso).");
        }

        if (iteration >= (maxIterationsOverride ?? limits.MaxIterations))
            return new ProgressDecision(false, true, "Se alcanzo el limite de iteraciones.");

        return new ProgressDecision(false, false, null);
    }

    public static bool WithinModifications(int modifications, AgentLimits limits)
        => modifications < limits.MaxModifications;

    /// <summary>
    /// Extensiones de contenido que un agente puede inspeccionar para diagnosticar
    /// (codigo, marcado, estilo, manifiestos y documentos). Amplia e independiente
    /// del ecosistema; no implica una regla de "leer X", solo reconoce que un
    /// archivo puede contener evidencia de error o de relacion con otros.
    /// </summary>
    private static bool IsReadableContent(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var normalized = path.Replace('\\', '/').TrimEnd('/');
        var extensionStart = normalized.LastIndexOf('.');
        var extension = extensionStart >= 0 ? normalized.Substring(extensionStart) : "";
        return AgentContentFileTypes.IsReadableExtension(extension);
    }

    private static string NormalizePath(string? p)
        => (p ?? "").Replace('\\', '/').TrimEnd('/').ToLowerInvariant();

    /// <summary>
    /// Indica si la intencion nombra un OBJETIVO concreto de diagnosticar (un
    /// archivo con extension, una ruta, o un modulo/clase con identidad de codigo
    /// introducido por una preposicion como "en/de/del/sobre") en lugar de una
    /// revision abierta del conjunto. Heuristica general: no depende del
    /// ecosistema ni de un nombre de archivo especifico.
    /// </summary>
    private static bool IntentNamesSpecificTarget(string intention)
    {
        if (string.IsNullOrWhiteSpace(intention)) return false;
        var t = intention.Trim();

        // Ruta o archivo con extension (p. ej. "Calc.cs", "src/app.js", "estilos.css").
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"[\w\-]+[\\/]") ||
            System.Text.RegularExpressions.Regex.IsMatch(t, @"\.[a-zA-Z0-9]{1,8}(?![\w])"))
            return true;

        // Identificador de codigo (PascalCase/camelCase) introducido por una
        // preposicion que suele encabezar un objetivo ("en Calc", "de Modulo"):
        // solo se marca como objetivo si viene tras "en/de/del/sobre/archivo",
        // nunca un nombre propio al inicio de la frase.
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"\b(en|de|del|sobre|archivo|el archivo)\s+[A-Z][A-Za-z0-9_]{1,}\b"))
            return true;

        return false;
    }

    private static string First(string? s) => string.IsNullOrWhiteSpace(s) ? "sin detalle" : s;
}
