using System.Collections.Concurrent;
using System.Diagnostics;
using Condor.Core.Contracts;
using Condor.Core.Models;
using Condor.Infrastructure.Agent;
using Xunit;

namespace Condor.Infrastructure.Tests;

public sealed class AgentDelegationServiceTests
{
    [Fact]
    public async Task ExecuteAsync_DelegacionesParalelas_ConservanSnapshotYSeparanCambios()
    {
        var root = Path.Combine(Path.GetTempPath(), "condor-delegation-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "base\n");
            await File.WriteAllTextAsync(Path.Combine(root, "WorkerA.md"), "pendiente A\n");
            await File.WriteAllTextAsync(Path.Combine(root, "WorkerB.md"), "pendiente B\n");
            await GitAsync(root, "init", "--quiet");
            await GitAsync(root, "config", "user.name", "Condor Tests");
            await GitAsync(root, "config", "user.email", "condor-tests@example.invalid");
            await GitAsync(root, "add", "README.md", "WorkerA.md", "WorkerB.md");
            await GitAsync(root, "commit", "--quiet", "-m", "base de delegacion");
            await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "cambio local\n");
            await File.WriteAllTextAsync(Path.Combine(root, "nota-local.txt"), "archivo no rastreado\n");

            var active = 0;
            var maximumActive = 0;
            var bothStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var observed = new ConcurrentBag<(string WorkingDirectory, AgentSubtask Subtask)>();
            var service = new AgentDelegationService(async (workingDirectory, subtask, _, cancellationToken) =>
            {
                observed.Add((workingDirectory, subtask));
                var nowActive = Interlocked.Increment(ref active);
                UpdateMaximum(ref maximumActive, nowActive);
                if (nowActive == 2)
                    bothStarted.TrySetResult(true);

                try
                {
                    await bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                    Assert.NotEqual(root, workingDirectory);
                    Assert.Contains("cambio local", await File.ReadAllTextAsync(Path.Combine(workingDirectory, "README.md"), cancellationToken));
                    Assert.True(File.Exists(Path.Combine(workingDirectory, "nota-local.txt")));

                    var outputName = subtask.Id == "a" ? "WorkerA.md" : "WorkerB.md";
                    await File.WriteAllTextAsync(Path.Combine(workingDirectory, outputName), "resultado de " + subtask.Id + "\n", cancellationToken);
                    await Task.Delay(40, cancellationToken);
                    return new AgentResult { Success = true, Reason = "resultado " + subtask.Id };
                }
                finally
                {
                    Interlocked.Decrement(ref active);
                }
            });
            var action = new AgentAction
            {
                Action = AgentAction.ActionDelegate,
                Subtasks = new List<AgentSubtask>
                {
                    new() { Id = "a", Task = "revisa el archivo A", ProfileId = "reviewer", MaxDurationSeconds = 30, MaxIterations = 3, MaxOutputTokens = 1024 },
                    new() { Id = "b", Task = "verifica el archivo B", ProfileId = "test-engineer", MaxDurationSeconds = 30, MaxIterations = 4, MaxOutputTokens = 2048 }
                }
            };

            var result = await service.ExecuteAsync(action, root, 7, progress: null);

            Assert.True(result.Success, result.ResultPreview);
            Assert.Equal(2, maximumActive);
            Assert.Collection(observed, _ => { }, _ => { });
            Assert.Equal(2, observed.Select(item => item.WorkingDirectory).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Contains(observed, item => item.Subtask.Id == "a" && item.Subtask.Task.StartsWith("@reviewer ", StringComparison.Ordinal));
            Assert.Contains(observed, item => item.Subtask.Id == "b" && item.Subtask.Task.StartsWith("perfil: pruebas ", StringComparison.Ordinal));
            Assert.Contains("30s/3 iteraciones/1024 tokens", result.ResultPreview, StringComparison.Ordinal);
            Assert.Contains("30s/4 iteraciones/2048 tokens", result.ResultPreview, StringComparison.Ordinal);
            Assert.Contains("WorkerA.md", result.ResultPreview, StringComparison.Ordinal);
            Assert.Contains("WorkerB.md", result.ResultPreview, StringComparison.Ordinal);
            Assert.Contains("cambios permanecen separados", result.ResultPreview, StringComparison.Ordinal);

            Assert.Equal("cambio local\n", await File.ReadAllTextAsync(Path.Combine(root, "README.md")));
            Assert.Equal("pendiente A\n", await File.ReadAllTextAsync(Path.Combine(root, "WorkerA.md")));
            Assert.Equal("pendiente B\n", await File.ReadAllTextAsync(Path.Combine(root, "WorkerB.md")));
            Assert.True(File.Exists(Path.Combine(root, "nota-local.txt")));
            var worktrees = await GitAsync(root, "worktree", "list", "--porcelain");
            Assert.Single(worktrees.Split("worktree ", StringSplitOptions.RemoveEmptyEntries));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
            }
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_RevisorDelegado_EntregaHallazgosEstructuradosAlAgentePrincipal()
    {
        var root = Path.Combine(Path.GetTempPath(), "condor-review-delegation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "base\n");
            await GitAsync(root, "init", "--quiet");
            await GitAsync(root, "config", "user.name", "Condor Tests");
            await GitAsync(root, "config", "user.email", "condor-tests@example.invalid");
            await GitAsync(root, "add", "README.md");
            await GitAsync(root, "commit", "--quiet", "-m", "base de revisión");

            var service = new AgentDelegationService((_, _, _, _) => Task.FromResult(new AgentResult
            {
                Success = true,
                Reason = "Revisión terminada.",
                ReviewFindings = new List<AgentReviewFinding>
                {
                    new()
                    {
                        Path = "README.md",
                        Line = 1,
                        Severity = "high",
                        Explanation = "El documento omite la condición de seguridad requerida.",
                        Evidence = "base",
                        Reproduction = "Leer la primera línea.",
                        Status = "open"
                    }
                }
            }));
            var result = await service.ExecuteAsync(new AgentAction
            {
                Action = AgentAction.ActionDelegate,
                Subtasks = new List<AgentSubtask>
                {
                    new()
                    {
                        Id = "review",
                        Task = "Revisa README.md",
                        ProfileId = "reviewer",
                        MaxDurationSeconds = 30,
                        MaxIterations = 2,
                        MaxOutputTokens = 512
                    }
                }
            }, root, 1, progress: null);

            Assert.True(result.Success, result.ResultPreview);
            Assert.Contains("Hallazgo estructurado [high] README.md:1 (open)", result.ResultPreview, StringComparison.Ordinal);
            Assert.Contains("Evidencia literal: base", result.ResultPreview, StringComparison.Ordinal);
            Assert.Contains("Reproducción: Leer la primera línea.", result.ResultPreview, StringComparison.Ordinal);
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
            }
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_SubagenteAgotaTiempo_CancelaTrabajoYConservaLaCarpetaPrincipal()
    {
        var root = Path.Combine(Path.GetTempPath(), "condor-delegation-timeout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var parentPath = Path.Combine(root, "README.md");
        var childCancelled = false;

        try
        {
            await File.WriteAllTextAsync(parentPath, "original\n");
            await GitAsync(root, "init", "--quiet");
            await GitAsync(root, "config", "user.name", "Condor Tests");
            await GitAsync(root, "config", "user.email", "condor-tests@example.invalid");
            await GitAsync(root, "add", "README.md");
            await GitAsync(root, "commit", "--quiet", "-m", "base de timeout");

            var service = new AgentDelegationService(async (_, _, _, cancellationToken) =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    return new AgentResult { Success = true, Reason = "la tarea debió exceder el límite" };
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    childCancelled = true;
                    throw;
                }
            });
            var action = new AgentAction
            {
                Action = AgentAction.ActionDelegate,
                Subtasks = new List<AgentSubtask>
                {
                    new() { Id = "timeout", Task = "trabajo acotado", ProfileId = "engineer", MaxDurationSeconds = 1, MaxIterations = 2, MaxOutputTokens = 512 }
                }
            };

            var result = await service.ExecuteAsync(action, root, 3, progress: null);

            Assert.False(result.Success);
            Assert.True(childCancelled);
            Assert.Contains("Se alcanzó el límite de tiempo del subagente", result.ResultPreview, StringComparison.Ordinal);
            Assert.Equal("original\n", await File.ReadAllTextAsync(parentPath));
            var worktrees = await GitAsync(root, "worktree", "list", "--porcelain");
            Assert.Single(worktrees.Split("worktree ", StringSplitOptions.RemoveEmptyEntries));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
            }
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<string> GitAsync(string workingDirectory, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("No se pudo iniciar git.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await outputTask;
        var error = await errorTask;
        if (process.ExitCode != 0)
            throw new InvalidOperationException("git " + string.Join(" ", arguments) + " falló: " + error);
        return output;
    }

    private static void UpdateMaximum(ref int maximum, int candidate)
    {
        int current;
        while (candidate > (current = Volatile.Read(ref maximum)) &&
               Interlocked.CompareExchange(ref maximum, candidate, current) != current)
        {
        }
    }
}
