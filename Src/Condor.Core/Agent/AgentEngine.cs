using System.Collections.Generic;
using System.Linq;
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
    private static readonly HashSet<string> AllowedActions = new()
    {
        AgentAction.ActionListDir, AgentAction.ActionReadFile,
        AgentAction.ActionPatch, AgentAction.ActionEditFile,
        AgentAction.ActionCreateFile,
        AgentAction.ActionBuild, AgentAction.ActionTest,
        AgentAction.ActionRestore, AgentAction.ActionGitStatus,
        AgentAction.ActionSearch, AgentAction.ActionUndoFile, AgentAction.ActionDone
    };

    public static ActionValidation ValidateAction(AgentAction action)
    {
        if (action is null || string.IsNullOrWhiteSpace(action.Action))
            return new ActionValidation(false, "Accion vacia o nula.");

        if (!AllowedActions.Contains(action.Action))
            return new ActionValidation(false, "Accion no permitida: " + action.Action);

        if (action.Action == AgentAction.ActionEditFile || action.Action == AgentAction.ActionCreateFile)
        {
            if (string.IsNullOrWhiteSpace(action.Path))
                return new ActionValidation(false, "Se requiere 'path' para la accion de archivo.");
            if (string.IsNullOrWhiteSpace(action.Content))
                return new ActionValidation(false, "Se requiere 'content' para la accion de archivo.");
        }

        if (action.Action == AgentAction.ActionPatch)
        {
            if (string.IsNullOrWhiteSpace(action.Path))
                return new ActionValidation(false, "Se requiere 'path' para la accion patch.");
            if (string.IsNullOrEmpty(action.Original) && string.IsNullOrWhiteSpace(action.Content))
                return new ActionValidation(false, "Se requiere 'original' (texto a localizar) para la accion patch.");
            if (action.Replacement is null && action.Content is null)
                return new ActionValidation(false, "Se requiere 'replacement' (texto nuevo) para la accion patch.");
        }

        return new ActionValidation(true, null);
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
        => action is AgentAction.ActionListDir or AgentAction.ActionReadFile or AgentAction.ActionSearch;

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

        bool hasBuild = t.Contains("crea") || t.Contains("agrega") || t.Contains("implementa")
            || t.Contains("constru") || t.Contains("escribe");
        bool hasError = t.Contains("error") || t.Contains("falla") || t.Contains("fallo")
            || t.Contains("bug") || t.Contains("no funciona") || t.Contains("corrige")
            || t.Contains("arregla") || t.Contains("roto") || t.Contains("revisa si");
        bool hasDescribe = t.Contains("que tenemos") || t.Contains("describe") || t.Contains("cuentame")
            || t.Contains("que es") || t.Contains("que contiene") || t.Contains("resumen");

        if (hasError && !hasBuild) return IntentFlavor.Diagnostic;
        if (hasBuild && !hasError) return IntentFlavor.Build;
        if (hasDescribe && !hasError && !hasBuild) return IntentFlavor.Descriptive;
        if (hasError && hasDescribe) return IntentFlavor.Diagnostic;
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
                return (false, "La solicitud apunta a un archivo concreto; usa read_file sobre el archivo señalado para localizar el error (o un search de 'error' si no encuentras el archivo).");
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

    public static ProgressDecision CheckProgress(int iteration, IReadOnlyList<AgentStep> steps, AgentLimits limits)
    {
        if (iteration >= limits.MaxIterations)
            return new ProgressDecision(false, true, "Se alcanzo el limite de iteraciones.");

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
        var p = path.Replace('\\', '/').TrimEnd('/');
        var idx = p.LastIndexOf('.');
        var ext = idx >= 0 ? p.Substring(idx) : "";
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
