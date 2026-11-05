using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Condor.Core.Contracts;
using Condor.Core.Models;

namespace Condor.Infrastructure.Agent;

/// <summary>
/// Ejecuta subtareas concurrentes en worktrees desechables. Cada hijo recibe la
/// misma base rastreada y los cambios locales del padre, pero nunca comparte su
/// directorio de trabajo ni integra sus cambios automáticamente.
/// </summary>
public sealed class AgentDelegationService : IAgentDelegationService
{
    private const int MaxConcurrentSubagents = 2;
    private const int MaxUntrackedFiles = 500;
    private const long MaxUntrackedBytes = 64L * 1024 * 1024;
    private const int MaxBaseDiffCharacters = 32_000_000;
    private const int MaxDiffCharactersPerSubagent = 10_000;

    private readonly Func<string, AgentSubtask, IAgentProgressObserver?, CancellationToken, Task<AgentResult>> _runChild;

    public AgentDelegationService(
        Func<string, AgentSubtask, IAgentProgressObserver?, CancellationToken, Task<AgentResult>> runChild)
    {
        _runChild = runChild ?? throw new ArgumentNullException(nameof(runChild));
    }

    public async Task<AgentStep> ExecuteAsync(
        AgentAction action,
        string workingDirectory,
        int iteration,
        IAgentProgressObserver? progress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecuteCoreAsync(action, workingDirectory, iteration, progress, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var reason = "Condor no pudo preparar la delegación aislada: " + exception.GetType().Name + ": " + exception.Message;
            progress?.Report(AgentProgress.Of(
                AgentPhase.Verifying,
                action: "subagentes",
                iteration: iteration,
                message: reason,
                flag: ProgressFlag.Recovering));
            return new AgentStep
            {
                Iteration = iteration,
                Action = AgentAction.ActionDelegate,
                Success = false,
                ResultPreview = Truncate(reason, 2000),
                AtUtc = DateTime.UtcNow
            };
        }
    }

    private async Task<AgentStep> ExecuteCoreAsync(
        AgentAction action,
        string workingDirectory,
        int iteration,
        IAgentProgressObserver? progress,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        var root = await ResolveRepositoryRootAsync(workingDirectory, cancellationToken).ConfigureAwait(false);
        var baseDiff = await RunGitAsync(root, new[] { "diff", "--binary", "HEAD", "--" }, cancellationToken).ConfigureAwait(false);
        if (baseDiff.Length > MaxBaseDiffCharacters)
            throw new InvalidOperationException("Los cambios locales exceden el límite para preparar worktrees aislados de subagentes.");
        var untracked = await ReadUntrackedFilesAsync(root, cancellationToken).ConfigureAwait(false);
        ValidateUntrackedSnapshot(root, untracked);

        var results = new SubagentResult[action.Subtasks!.Count];
        using var parallelism = new SemaphoreSlim(MaxConcurrentSubagents, MaxConcurrentSubagents);
        var workers = action.Subtasks.Select((subtask, index) => RunSubtaskAsync(
            root, baseDiff, untracked, subtask, index, results, parallelism, progress, cancellationToken));
        await Task.WhenAll(workers).ConfigureAwait(false);

        var successful = results.Count(result => result.Success);
        var summary = new System.Text.StringBuilder();
        summary.AppendLine($"Delegación: {successful}/{results.Length} subagentes completados correctamente.");
        summary.AppendLine("Los cambios permanecen separados en worktrees temporales; Condor no los integró automáticamente.");

        foreach (var result in results)
        {
            summary.AppendLine();
            summary.AppendLine($"[{result.Id}] perfil={result.ProfileId}; estado={(result.Success ? "completado" : "fallido")}; duración={result.Elapsed.TotalSeconds:F1}s; límites={result.MaxDurationSeconds}s/{result.MaxIterations} iteraciones/{result.MaxOutputTokens} tokens de salida");
            if (!string.IsNullOrWhiteSpace(result.Reason))
                summary.AppendLine("Resultado: " + result.Reason);
            foreach (var finding in result.ReviewFindings)
            {
                summary.AppendLine($"Hallazgo estructurado [{finding.Severity}] {finding.Path}:{finding.Line} ({finding.Status}): {finding.Explanation}");
                summary.AppendLine("Evidencia literal: " + finding.Evidence);
                if (!string.IsNullOrWhiteSpace(finding.Reproduction))
                    summary.AppendLine("Reproducción: " + finding.Reproduction);
                if (!string.IsNullOrWhiteSpace(finding.ClosureReason))
                    summary.AppendLine("Cierre: " + finding.ClosureReason);
            }
            summary.AppendLine("Archivos modificados: " + (result.Files.Count == 0 ? "ninguno" : string.Join(", ", result.Files)));
            if (!string.IsNullOrWhiteSpace(result.Diff))
                summary.AppendLine("Diff del worktree (puede estar truncado):\n" + result.Diff);
        }

        progress?.Report(AgentProgress.Of(
            AgentPhase.Analyzing,
            action: "subagentes",
            iteration: iteration,
            message: $"Delegación terminada: {successful}/{results.Length} agentes; cambios aislados, pendientes de integración."));

        return new AgentStep
        {
            Iteration = iteration,
            Action = AgentAction.ActionDelegate,
            Success = successful == results.Length,
            ResultPreview = Truncate(summary.ToString(), 42_000),
            AtUtc = DateTime.UtcNow
        };
    }

    private async Task RunSubtaskAsync(
        string root,
        string baseDiff,
        IReadOnlyList<string> untracked,
        AgentSubtask subtask,
        int index,
        SubagentResult[] results,
        SemaphoreSlim parallelism,
        IAgentProgressObserver? progress,
        CancellationToken cancellationToken)
    {
        await parallelism.WaitAsync(cancellationToken).ConfigureAwait(false);
        var timer = Stopwatch.StartNew();
        string? worktree = null;
        SubagentResult result;
        try
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "condor-subagents");
            Directory.CreateDirectory(tempRoot);
            worktree = Path.Combine(tempRoot, Guid.NewGuid().ToString("N") + "-" + subtask.Id);
            progress?.Report(AgentProgress.Of(
                AgentPhase.Analyzing,
                action: "subagente",
                path: subtask.Id,
                iteration: index + 1,
                message: $"Subagente {subtask.Id} ({subtask.ProfileId}): creando worktree aislado."));

            await RunGitAsync(root, new[] { "worktree", "add", "--detach", worktree, "HEAD" }, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(baseDiff))
                await RunGitWithInputAsync(worktree, new[] { "apply", "--binary", "-" }, baseDiff, cancellationToken).ConfigureAwait(false);
            CopyUntrackedFiles(root, worktree, untracked);

            using var taskTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            taskTimeout.CancelAfter(TimeSpan.FromSeconds(subtask.MaxDurationSeconds));
            var childObserver = new PrefixProgressObserver(progress, subtask.Id);
            progress?.Report(AgentProgress.Of(
                AgentPhase.Understanding,
                action: "subagente",
                path: subtask.Id,
                iteration: index + 1,
                message: $"Subagente {subtask.Id}: perfil {subtask.ProfileId}; límites {subtask.MaxDurationSeconds}s, {subtask.MaxIterations} iteraciones y {subtask.MaxOutputTokens} tokens de salida."));

            var childTask = ApplyProfile(subtask);
            var child = await _runChild(worktree, childTask, childObserver, taskTimeout.Token).ConfigureAwait(false);
            var files = await ReadChangedFilesAsync(root, worktree, cancellationToken).ConfigureAwait(false);
            var diff = await CreateDelegatedDiffAsync(root, worktree, files, cancellationToken).ConfigureAwait(false);
            result = new SubagentResult(subtask.Id, subtask.ProfileId, subtask.MaxDurationSeconds, subtask.MaxIterations,
                subtask.MaxOutputTokens, child.Success, child.Reason, timer.Elapsed, files,
                Truncate(diff, MaxDiffCharactersPerSubagent), child.ReviewFindings ?? new List<AgentReviewFinding>());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var partial = await CollectPartialChangesAsync(root, worktree).ConfigureAwait(false);
            result = new SubagentResult(subtask.Id, subtask.ProfileId, subtask.MaxDurationSeconds, subtask.MaxIterations,
                subtask.MaxOutputTokens, false, "Se alcanzó el límite de tiempo del subagente.", timer.Elapsed,
                partial.Files, partial.Diff, new List<AgentReviewFinding>());
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            var partial = await CollectPartialChangesAsync(root, worktree).ConfigureAwait(false);
            result = new SubagentResult(subtask.Id, subtask.ProfileId, subtask.MaxDurationSeconds, subtask.MaxIterations,
                subtask.MaxOutputTokens, false, exception.GetType().Name + ": " + exception.Message, timer.Elapsed,
                partial.Files, partial.Diff, new List<AgentReviewFinding>());
        }
        finally
        {
            if (worktree is not null)
            {
                try
                {
                    await RemoveWorktreeSafelyAsync(root, worktree).ConfigureAwait(false);
                }
                catch
                {
                    // La limpieza no debe ocultar el resultado del subagente.
                }
            }

            parallelism.Release();
        }

        timer.Stop();
        results[index] = result;
        progress?.Report(AgentProgress.Of(
            AgentPhase.Verifying,
            action: "subagente",
            path: subtask.Id,
            iteration: index + 1,
            message: $"Subagente {subtask.Id}: {(result.Success ? "completado" : "fallido")}; {timer.Elapsed.TotalSeconds:F1}s; archivos: {(result.Files.Count == 0 ? "ninguno" : string.Join(", ", result.Files))}."));
    }

    private static AgentSubtask ApplyProfile(AgentSubtask subtask)
    {
        var marker = subtask.ProfileId switch
        {
            "reviewer" => "@reviewer ",
            "security-reviewer" => "@security-reviewer ",
            "test-engineer" => "perfil: pruebas ",
            _ => string.Empty
        };
        return new AgentSubtask
        {
            Id = subtask.Id,
            ProfileId = subtask.ProfileId,
            Task = marker + subtask.Task,
            MaxDurationSeconds = subtask.MaxDurationSeconds,
            MaxIterations = subtask.MaxIterations,
            MaxOutputTokens = subtask.MaxOutputTokens
        };
    }

    private static async Task<string> ResolveRepositoryRootAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        var root = await RunGitAsync(workingDirectory, new[] { "rev-parse", "--show-toplevel" }, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("No se pudo ubicar la raíz del repositorio Git para aislar los subagentes.");
        return Path.GetFullPath(root.Trim());
    }

    private static async Task<List<string>> ReadUntrackedFilesAsync(string root, CancellationToken cancellationToken)
    {
        var output = await RunGitAsync(root, new[] { "ls-files", "--others", "--exclude-standard", "-z" }, cancellationToken).ConfigureAwait(false);
        return output.Split('\0', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    private static void ValidateUntrackedSnapshot(string root, IReadOnlyList<string> files)
    {
        if (files.Count > MaxUntrackedFiles)
            throw new InvalidOperationException($"El worktree aislado superaría el límite de {MaxUntrackedFiles} archivos nuevos.");

        long total = 0;
        foreach (var relative in files)
        {
            var source = SafeCombine(root, relative);
            if (!File.Exists(source))
                continue;
            var info = new FileInfo(source);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("No se copian enlaces simbólicos no rastreados a worktrees de subagentes: " + relative);
            total += info.Length;
            if (total > MaxUntrackedBytes)
                throw new InvalidOperationException($"Los archivos nuevos superan el límite de {MaxUntrackedBytes / (1024 * 1024)} MB para la copia aislada.");
        }
    }

    private static void CopyUntrackedFiles(string sourceRoot, string targetRoot, IReadOnlyList<string> files)
    {
        foreach (var relative in files)
        {
            var source = SafeCombine(sourceRoot, relative);
            if (!File.Exists(source))
                continue;
            var target = SafeCombine(targetRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: true);
        }
    }

    private static async Task<List<string>> ReadChangedFilesAsync(string root, string worktree, CancellationToken cancellationToken)
    {
        var tracked = await RunGitAsync(worktree, new[] { "diff", "--name-only", "-z", "HEAD", "--" }, cancellationToken).ConfigureAwait(false);
        var untracked = await ReadUntrackedFilesAsync(worktree, cancellationToken).ConfigureAwait(false);
        var candidates = tracked.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Concat(untracked)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Take(100);
        var changed = new List<string>();
        foreach (var relative in candidates)
        {
            var parentFile = SafeCombine(root, relative);
            var childFile = SafeCombine(worktree, relative);
            var parentExists = File.Exists(parentFile);
            var childExists = File.Exists(childFile);
            if (parentExists != childExists || (parentExists && !await FilesEqualAsync(parentFile, childFile, cancellationToken).ConfigureAwait(false)))
                changed.Add(relative);
        }
        return changed;
    }

    private static async Task RemoveWorktreeSafelyAsync(string root, string worktree)
    {
        var tempRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "condor-subagents")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullWorktree = Path.GetFullPath(worktree);
        if (!fullWorktree.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Se rechazó limpiar un worktree fuera de la carpeta temporal de subagentes.");

        using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await RunGitAsync(root, new[] { "worktree", "remove", "--force", fullWorktree }, cleanupTimeout.Token).ConfigureAwait(false);
        }
        catch
        {
            try
            {
                if (Directory.Exists(fullWorktree))
                    Directory.Delete(fullWorktree, recursive: true);
            }
            catch { }
            try
            {
                using var pruneTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await RunGitAsync(root, new[] { "worktree", "prune" }, pruneTimeout.Token).ConfigureAwait(false);
            }
            catch { }
        }
    }

    private static async Task<(List<string> Files, string Diff)> CollectPartialChangesAsync(string root, string? worktree)
    {
        if (worktree is null || !Directory.Exists(worktree))
            return (new List<string>(), "");
        try
        {
            var files = await ReadChangedFilesAsync(root, worktree, CancellationToken.None).ConfigureAwait(false);
            var diff = await CreateDelegatedDiffAsync(root, worktree, files, CancellationToken.None).ConfigureAwait(false);
            return (files, Truncate(diff, MaxDiffCharactersPerSubagent));
        }
        catch
        {
            return (new List<string>(), "");
        }
    }

    private static async Task<bool> FilesEqualAsync(string left, string right, CancellationToken cancellationToken)
    {
        var leftInfo = new FileInfo(left);
        var rightInfo = new FileInfo(right);
        if (leftInfo.Length != rightInfo.Length)
            return false;
        await using var leftStream = new FileStream(left, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        await using var rightStream = new FileStream(right, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var leftHash = await System.Security.Cryptography.SHA256.HashDataAsync(leftStream, cancellationToken).ConfigureAwait(false);
        var rightHash = await System.Security.Cryptography.SHA256.HashDataAsync(rightStream, cancellationToken).ConfigureAwait(false);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(leftHash, rightHash);
    }

    private static async Task<string> CreateDelegatedDiffAsync(
        string root,
        string worktree,
        IReadOnlyList<string> files,
        CancellationToken cancellationToken)
    {
        if (files.Count == 0)
            return "";
        var emptyFile = Path.Combine(Path.GetTempPath(), "condor-empty-" + Guid.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(emptyFile, "", cancellationToken).ConfigureAwait(false);
        try
        {
            var output = new System.Text.StringBuilder();
            foreach (var relative in files)
            {
                var parentFile = SafeCombine(root, relative);
                var childFile = SafeCombine(worktree, relative);
                var left = File.Exists(parentFile) ? parentFile : emptyFile;
                var right = File.Exists(childFile) ? childFile : emptyFile;
                var diff = await RunGitAllowDiffAsync(root, new[] { "diff", "--no-index", "--binary", "--", left, right }, cancellationToken).ConfigureAwait(false);
                output.AppendLine(diff.Replace(left, "a/" + relative, StringComparison.OrdinalIgnoreCase)
                    .Replace(right, "b/" + relative, StringComparison.OrdinalIgnoreCase));
                if (output.Length >= MaxDiffCharactersPerSubagent)
                    break;
            }
            return output.ToString();
        }
        finally
        {
            try { File.Delete(emptyFile); } catch { }
        }
    }

    private static string SafeCombine(string root, string relative)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(root, relative));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Ruta no válida en el snapshot Git: " + relative);
        return fullPath;
    }

    private static Task<string> RunGitAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        => RunGitCoreAsync(workingDirectory, arguments, null, cancellationToken);

    private static Task<string> RunGitWithInputAsync(string workingDirectory, IReadOnlyList<string> arguments, string input, CancellationToken cancellationToken)
        => RunGitCoreAsync(workingDirectory, arguments, input, cancellationToken);

    private static Task<string> RunGitAllowDiffAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        => RunGitCoreAsync(workingDirectory, arguments, null, cancellationToken, allowDiffExitCode: true);

    private static async Task<string> RunGitCoreAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        string? input,
        CancellationToken cancellationToken,
        bool allowDiffExitCode = false)
    {
        var start = GitProcessStartInfoFactory.Create(workingDirectory, redirectStandardInput: input is not null);
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = start };
        if (!process.Start())
            throw new InvalidOperationException("No se pudo iniciar Git para preparar un worktree de subagente.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        if (input is not null)
        {
            await process.StandardInput.WriteAsync(input.AsMemory(), cancellationToken).ConfigureAwait(false);
            process.StandardInput.Close();
        }

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw;
        }

        var output = await stdoutTask.ConfigureAwait(false);
        var error = await stderrTask.ConfigureAwait(false);
        if (process.ExitCode != 0 && !(allowDiffExitCode && process.ExitCode == 1))
            throw new InvalidOperationException("Git falló (" + string.Join(" ", arguments) + "): " + Truncate(error.Trim(), 1200));
        return output;
    }

    private static string Truncate(string value, int maximum)
        => value.Length <= maximum ? value : value.Substring(0, maximum) + "\n… [truncado]";

    private sealed record SubagentResult(
        string Id,
        string ProfileId,
        int MaxDurationSeconds,
        int MaxIterations,
        int MaxOutputTokens,
        bool Success,
        string? Reason,
        TimeSpan Elapsed,
        List<string> Files,
        string Diff,
        List<AgentReviewFinding> ReviewFindings);

    private sealed class PrefixProgressObserver : IAgentProgressObserver
    {
        private readonly IAgentProgressObserver? _inner;
        private readonly string _agentId;

        public PrefixProgressObserver(IAgentProgressObserver? inner, string agentId)
        {
            _inner = inner;
            _agentId = agentId;
        }

        public void Report(AgentProgress progress)
        {
            if (_inner is null)
                return;
            _inner.Report(new AgentProgress
            {
                Phase = progress.Phase,
                Action = progress.Action,
                Path = progress.Path,
                Iteration = progress.Iteration,
                Message = $"Subagente {_agentId}: {progress.Message}",
                Flag = progress.Flag,
                ResourceState = progress.ResourceState,
                AvailableGb = progress.AvailableGb,
                SafeBudgetGb = progress.SafeBudgetGb,
                ModelActivity = progress.ModelActivity,
                ModelWorking = progress.ModelWorking
            });
        }
    }
}
