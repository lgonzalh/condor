using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Condor.Core.Models;

namespace Condor.Infrastructure.Agent;

public readonly record struct HarnessVerifyResult(bool Done, string? Reason, string Detail);

/// <summary>
/// Harness externo del agente: ejecuta build y test de forma real e independiente
/// del modelo y devuelve la evidencia obtenida. No adivina ni simula exito.
/// </summary>
public sealed class AgentHarness
{
    private readonly string _workingDir;
    private readonly AgentLimits _limits;
    private readonly List<AgentStep> _steps;
    private bool _restored;

    public AgentHarness(string workingDir, AgentLimits limits, List<AgentStep> steps)
    {
        _workingDir = workingDir;
        _limits = limits;
        _steps = steps;
    }

    public async Task<HarnessVerifyResult> VerifyAsync(CancellationToken ct)
    {
        var toolset = new AgentToolset(_workingDir, maxContent: _limits.MaxContentLength);

        var build = await toolset.ExecuteAsync(new AgentAction { Action = AgentAction.ActionBuild }, 0, ct);
        _steps.Add(build);

        if (!build.Success)
        {
            var (restored, build2) = await MaybeRestoreAndReRunAsync(toolset, build, ct);
            if (!build2.Success)
                return new(false, TailReason("Build fallo: " + (build2.ResultPreview ?? "sin detalle")), build2.ResultPreview ?? "");
        }

        var tests = await toolset.ExecuteAsync(new AgentAction { Action = AgentAction.ActionTest }, 0, ct);
        _steps.Add(tests);
        if (!tests.Success)
        {
            var (restored2, tests2) = await MaybeRestoreAndReRunAsync(toolset, tests, ct);
            if (!tests2.Success)
                return new(false, TailReason("Pruebas fallaron: " + (tests2.ResultPreview ?? "sin detalle")), tests2.ResultPreview ?? "");
        }

        return new(true, "El harness confirmo build y pruebas externamente.", "build ok; test ok");
    }

    private async Task<(bool, AgentStep)> MaybeRestoreAndReRunAsync(AgentToolset toolset, AgentStep failed, CancellationToken ct)
    {
        if (_restored || !LooksLikeRestoreFailure(failed.ResultPreview))
            return (false, failed);

        var restore = await toolset.ExecuteAsync(new AgentAction { Action = AgentAction.ActionRestore }, 0, ct);
        _steps.Add(restore);
        _restored = true;

        var rerun = await toolset.ExecuteAsync(new AgentAction { Action = failed.Action == AgentAction.ActionTest ? AgentAction.ActionTest : AgentAction.ActionBuild }, 0, ct);
        _steps.Add(rerun);
        return (true, rerun);
    }

    private static bool LooksLikeRestoreFailure(string? output)
    {
        return output is not null &&
               (output.Contains("NETSDK1004", StringComparison.OrdinalIgnoreCase) ||
                output.Contains("project.assets.json", StringComparison.OrdinalIgnoreCase) ||
                output.Contains("NU1004", StringComparison.OrdinalIgnoreCase) ||
                output.Contains("no se encontro el archivo de recursos", StringComparison.OrdinalIgnoreCase) ||
                output.Contains("restauracion de paquetes", StringComparison.OrdinalIgnoreCase));
    }

    private static string TailReason(string s)
    {
        // Resumen breve del motivo, tomado del final del mensaje real del harness.
        return s.Length > 300 ? s.Substring(0, 300) + "..." : s;
    }
}
