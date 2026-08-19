using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Condor.Core.Models;
using Condor.Infrastructure.Llm;
using Condor.Infrastructure.SemanticVerification;

namespace Condor.Infrastructure.Agent;

public sealed class AgentToolset
{
    private readonly string _root;
    private readonly ProcessRunner _runner;
    private readonly int _timeout;
    private int _maxContent;

    public AgentToolset(string root, int timeout = 120_000, int maxContent = 200_000)
    {
        _root = Path.GetFullPath(root);
        _runner = new ProcessRunner();
        _timeout = timeout;
        _maxContent = maxContent;
    }

    public async Task<AgentStep> ExecuteAsync(AgentAction action, int iteration, CancellationToken ct)
    {
        if (action.Action == AgentAction.ActionListDir) return ListDir(action, iteration);
        if (action.Action == AgentAction.ActionReadFile) return ReadFile(action, iteration);
        if (action.Action == AgentAction.ActionCreateFile) return CreateFile(action, iteration);
        if (action.Action == AgentAction.ActionEditFile) return EditFile(action, iteration);
        if (action.Action == AgentAction.ActionBuild) return await RunBuildAsync(action, iteration, ct);
        if (action.Action == AgentAction.ActionTest) return await RunTestAsync(action, iteration, ct);
        if (action.Action == AgentAction.ActionGitStatus) return GitStatus(action, iteration);
        if (action.Action == AgentAction.ActionSearch) return Search(action, iteration);

        return new AgentStep { Iteration = iteration, Action = action.Action, Success = false, ResultPreview = "ok", AtUtc = DateTime.UtcNow };
    }

    private AgentStep ListDir(AgentAction action, int iteration)
    {
        var dir = Path.GetFullPath(Resolve(action.Path ?? "."));
        if (!IsWithin(dir)) return F(iteration, action.Action, "Directorio fuera del proyecto.", action.Path);
        if (!Directory.Exists(dir)) return F(iteration, action.Action, "Directorio no existe.", action.Path);
        var entries = Directory.EnumerateFileSystemEntries(dir).Take(200)
            .Select(Path.GetFileName)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
        return Ok(iteration, action.Action, action.Path, string.Join("\n", entries));
    }

    private AgentStep ReadFile(AgentAction action, int iteration)
    {
        var full = Resolve(action.Path ?? "");
        if (!IsWithin(full)) return F(iteration, action.Action, "Ruta fuera del proyecto.", action.Path);
        if (!File.Exists(full)) return F(iteration, action.Action, "El archivo no existe.", action.Path);
        var content = File.ReadAllText(full);
        if (content.Length > _maxContent) content = content.Substring(0, _maxContent) + "\n... [truncado]";
        return Ok(iteration, action.Action, action.Path, content);
    }

    private AgentStep CreateFile(AgentAction action, int iteration)
    {
        var full = Resolve(action.Path ?? "");
        if (!IsWithin(full)) return F(iteration, action.Action, "Ruta fuera del proyecto.", action.Path);
        if (File.Exists(full)) return F(iteration, action.Action, "El archivo ya existe (usa edit_file).", action.Path);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(full, action.Content ?? "");
        return Ok(iteration, action.Action, action.Path, "Archivo creado.");
    }

    private AgentStep EditFile(AgentAction action, int iteration)
    {
        var full = Resolve(action.Path ?? "");
        if (!IsWithin(full)) return F(iteration, action.Action, "Ruta fuera del proyecto.", action.Path);
        if (!File.Exists(full)) return F(iteration, action.Action, "El archivo no existe (usa create_file).", action.Path);
        if ((action.Content ?? "").Length > _maxContent) return F(iteration, action.Action, "Contenido demasiado largo.", action.Path);
        File.WriteAllText(full, action.Content ?? "");
        return Ok(iteration, action.Action, action.Path, "Archivo actualizado.");
    }

    private async Task<AgentStep> RunBuildAsync(AgentAction action, int iteration, CancellationToken ct)
    {
        return await RunDotAsync("build", iteration, ct);
    }

    private async Task<AgentStep> RunTestAsync(AgentAction action, int iteration, CancellationToken ct)
    {
        return await RunDotAsync("test", iteration, ct);
    }

    private async Task<AgentStep> RunDotAsync(string kind, int iteration, CancellationToken ct)
    {
        var manifest = FindManifest();
        if (string.IsNullOrWhiteSpace(manifest)) return F(iteration, kind, "No se encontro manifiesto .NET.", null);
        var run = await _runner.RunAsync(_root, manifest, kind, _timeout, ct);
        if (run.ValidationReason is not null) return F(iteration, kind, run.ValidationReason, manifest);
        var ok = run.ExitCode == 0 && !run.TimedOut && !run.NotExecutable && !run.Incomplete;
        return new AgentStep
        {
            Iteration = iteration,
            Action = kind,
            Path = manifest,
            Success = ok,
            ResultPreview = Truncate(run.Output),
            AtUtc = DateTime.UtcNow
        };
    }

    private AgentStep Search(AgentAction action, int iteration)
    {
        var term = action.Content ?? "";
        if (string.IsNullOrWhiteSpace(term)) return F(iteration, action.Action, "Se requiere 'content' (texto a buscar).", action.Path);
        var hits = new List<string>();
        try
        {
            foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)
                .Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\") && f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
            {
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Contains(term, StringComparison.OrdinalIgnoreCase))
                    {
                        hits.Add(Path.GetRelativePath(_root, file) + ":" + (i + 1));
                        if (hits.Count >= 50) break;
                    }
                }

                if (hits.Count >= 50) break;
            }
        }
        catch { }

        return Ok(iteration, action.Action, action.Path ?? term, hits.Count > 0 ? string.Join("\n", hits) : "Sin coincidencias.");
    }

    private AgentStep GitStatus(AgentAction action, int iteration)
    {
        var branch = TryGit("rev-parse --abbrev-ref HEAD");
        var status = TryGit("status --porcelain");
        return Ok(iteration, action.Action, null, "Rama: " + branch + "\n" + (string.IsNullOrWhiteSpace(status) ? "limpio" : status));
    }

    private string TryGit(string args)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git", WorkingDirectory = _root,
                RedirectStandardOutput = true, RedirectStandardError = true,
                UseShellExecute = false, CreateNoWindow = true
            };
            foreach (var part in args.Split(' ')) psi.ArgumentList.Add(part);
            using var p = System.Diagnostics.Process.Start(psi);
            if (p is null) return "";
            p.WaitForExit(10000);
            return (p.StandardOutput.ReadToEnd() ?? "").Trim();
        }
        catch { return ""; }
    }

    private string? FindManifest()
    {
        string? f = Directory.EnumerateFiles(_root, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault()
            ?? Directory.EnumerateFiles(_root, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
        return f is null ? null : Path.GetFileName(f);
    }

    private string Resolve(string rel)
    {
        return Path.IsPathRooted(rel)
            ? Path.GetFullPath(rel)
            : Path.GetFullPath(Path.Combine(_root, rel ?? ""));
    }

    private bool IsWithin(string full)
    {
        return full.Equals(_root, StringComparison.OrdinalIgnoreCase) ||
               full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static AgentStep Ok(int iteration, string action, string? path, string result) =>
        new() { Iteration = iteration, Action = action, Path = path, Success = true, ResultPreview = Truncate(result), AtUtc = DateTime.UtcNow };

    private static AgentStep F(int iteration, string action, string reason, string? path) =>
        new() { Iteration = iteration, Action = action, Path = path, Success = false, ResultPreview = reason, AtUtc = DateTime.UtcNow };

    private static string Truncate(string s) => s is { Length: > 5000 } ? s.Substring(0, 5000) + "\n... [truncado]" : (s ?? "");
}
