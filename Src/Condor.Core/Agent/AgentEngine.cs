using System.Collections.Generic;
using System.Linq;
using Condor.Core.Models;

namespace Condor.Core.Agent;

public readonly record struct ActionValidation(bool Valid, string? Reason);

public readonly record struct ProgressDecision(bool Done, bool Fail, string? Reason);

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

    private static string First(string? s) => string.IsNullOrWhiteSpace(s) ? "sin detalle" : s;
}
