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
    private readonly Dictionary<string, string?> _undoStack = new(System.StringComparer.OrdinalIgnoreCase);

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
        if (action.Action == AgentAction.ActionPatch) return Patch(action, iteration);
        if (action.Action == AgentAction.ActionBuild) return await RunBuildAsync(action, iteration, ct);
        if (action.Action == AgentAction.ActionTest) return await RunTestAsync(action, iteration, ct);
        if (action.Action == AgentAction.ActionRestore) return await RunRestoreAsync(action, iteration, ct);
        if (action.Action == AgentAction.ActionGitStatus) return GitStatus(action, iteration);
        if (action.Action == AgentAction.ActionSearch) return Search(action, iteration);
        if (action.Action == AgentAction.ActionUndoFile) return UndoFile(action, iteration);

        return new AgentStep { Iteration = iteration, Action = action.Action, Success = false, ResultPreview = "ok", AtUtc = DateTime.UtcNow };
    }

    private AgentStep ListDir(AgentAction action, int iteration)
    {
        var dir = Path.GetFullPath(Resolve(action.Path ?? ""));
        if (!IsWithin(dir)) return F(iteration, action.Action, "Directorio fuera del proyecto.", action.Path);
        if (!Directory.Exists(dir)) return F(iteration, action.Action, "El directorio no existe: " + (action.Path ?? "") + SuggestPath(action.Path ?? ""), action.Path);

        var rel = RelativeOf(dir);
        var entries = Directory.EnumerateFileSystemEntries(dir)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Take(300)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("Directorio: " + (rel.Length == 0 ? "." : rel));
        var dirs = new List<string>();
        var files = new List<string>();
        foreach (var entry in entries)
        {
            var name = Path.GetFileName(entry);
            if (Directory.Exists(entry)) dirs.Add(name + "/");
            else files.Add(name);
        }

        foreach (var d in dirs) sb.AppendLine("  [d] " + d);
        foreach (var f in files) sb.AppendLine("  [f] " + f);

        sb.AppendLine("Nota: usa rutas relativas a la raiz del proyecto. Puedes listar subdirectorios con list_dir y su ruta.");
        if (entries.Count >= 300) sb.AppendLine("(listado limitado a 300 entradas; navega por subdirectorios)");

        return Ok(iteration, action.Action, action.Path ?? ".", sb.ToString());
    }

    private AgentStep ReadFile(AgentAction action, int iteration)
    {
        var full = Resolve(action.Path ?? "");
        if (!IsWithin(full)) return F(iteration, action.Action, "Ruta fuera del proyecto.", action.Path);
        if (!File.Exists(full)) return F(iteration, action.Action, "El archivo no existe: " + (action.Path ?? "") + SuggestPath(action.Path ?? ""), action.Path);

        var content = File.ReadAllText(full);
        if (content.Length > _maxContent) content = content.Substring(0, _maxContent) + "\n... [truncado]";
        var preview = "--- CONTENIDO DE " + RelativeOf(full) + " ---\n" + content + "\n--- FIN ---";
        return Ok(iteration, action.Action, action.Path, preview);
    }

    private AgentStep CreateFile(AgentAction action, int iteration)
    {
        var full = Resolve(action.Path ?? "");
        if (!IsWithin(full)) return F(iteration, action.Action, "Ruta fuera del proyecto.", action.Path);
        if (File.Exists(full)) return F(iteration, action.Action, "El archivo ya existe (usa patch o edit_file sobre " + RelativeOf(full) + ").", action.Path);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        _undoStack[full] = null; // antes no existia
        File.WriteAllText(full, action.Content ?? "");
        return Ok(iteration, action.Action, action.Path, "Archivo creado en " + RelativeOf(full));
    }

    private AgentStep EditFile(AgentAction action, int iteration)
    {
        var full = Resolve(action.Path ?? "");
        if (!IsWithin(full)) return F(iteration, action.Action, "Ruta fuera del proyecto.", action.Path);
        if (!File.Exists(full)) return F(iteration, action.Action, "El archivo no existe (usa create_file en " + RelativeOf(full) + ").", action.Path);
        if ((action.Content ?? "").Length > _maxContent) return F(iteration, action.Action, "Contenido demasiado largo.", action.Path);
        _undoStack[full] = File.ReadAllText(full);
        File.WriteAllText(full, action.Content ?? "");
        return Ok(iteration, action.Action, action.Path, "Archivo actualizado en " + RelativeOf(full) + ". Usa patch para reemplazos quirurgicos y conserva el resto del contenido intacto.");
    }

    private AgentStep Patch(AgentAction action, int iteration)
    {
        var full = Resolve(action.Path ?? "");
        if (!IsWithin(full)) return F(iteration, action.Action, "Ruta fuera del proyecto.", action.Path);
        if (!File.Exists(full)) return F(iteration, action.Action, "El archivo no existe: " + (action.Path ?? "") + SuggestPath(action.Path ?? ""), action.Path);

        var original = action.Original ?? action.Content ?? "";
        var replacement = action.Replacement ?? "";
        if (string.IsNullOrEmpty(original))
            return F(iteration, action.Action, "No se indico texto a localizar (patch.original).", action.Path);

        var text = File.ReadAllText(full);
        var normText = Normalize(text);
        var normOriginal = Normalize(original);
        var match = FindSnippet(normText, normOriginal);

        if (match is null)
        {
            return F(iteration, action.Action, "No se encontro el texto a reemplazar. Revisa que 'patch.original' coincida exactamente (incluyendo espacios). " + SuggestSnippet(text, original), action.Path);
        }

        var updated = normText.Remove(match.Value.Index, match.Value.Length).Insert(match.Value.Index, replacement);
        _undoStack[full] = text;
        File.WriteAllText(full, updated);

        var linesChanged = CountLines(normOriginal);
        return Ok(iteration, action.Action, action.Path, "Reemplazo aplicado (" + linesChanged + " lineas) en " + RelativeOf(full) + ". Revisa el resultado y compila con build.");
    }

    private static (int Index, int Length)? FindSnippet(string normText, string normOriginal)
    {
        var idx = normText.IndexOf(normOriginal, StringComparison.Ordinal);
        return idx < 0 ? null : (idx, normOriginal.Length);
    }

    private static string Normalize(string s)
    {
        return s.Replace("\r\n", "\n").Replace("\r", "\n");
    }

    private static int CountLines(string s)
    {
        if (s.Length == 0) return 0;
        return Normalize(s).Split('\n').Length;
    }

    private AgentStep UndoFile(AgentAction action, int iteration)
    {
        var full = Resolve(action.Path ?? "");
        if (!IsWithin(full)) return F(iteration, action.Action, "Ruta fuera del proyecto.", action.Path);
        if (!_undoStack.TryGetValue(full, out var previous))
            return F(iteration, action.Action, "No hay un estado anterior que deshacer para " + (action.Path ?? "") + ". Lee el archivo y corrige con patch/edit_file.", action.Path);

        if (previous is null)
        {
            var existed = File.Exists(full);
            if (existed) File.Delete(full);
            _undoStack.Remove(full);
            return Ok(iteration, action.Action, action.Path, (existed ? "Archivo eliminado (revierte create_file)" : "Sin cambios") + " en " + RelativeOf(full));
        }

        File.WriteAllText(full, previous);
        _undoStack.Remove(full);
        return Ok(iteration, action.Action, action.Path, "Revertido al estado anterior en " + RelativeOf(full) + ". Revisa con read_file y vuelve a compilar/build.");
    }

    private static string SuggestSnippet(string text, string original)
    {
        // Ayuda al modelo: muestra una ventana del contenido donde posiblemente este el fragmento.
        var norm = Normalize(text);
        var probe = NewestAnchor(norm);
        return probe is { } p ? "El archivo contiene cerca de: '" + Shorten(p, 80) + "'. Lee el archivo de nuevo y copia el texto exacto." : "Archivo vacio o sin coincidencia.";
    }

    private static string? NewestAnchor(string text)
    {
        var lines = text.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        if (lines.Count == 0) return null;
        return lines[Math.Min(3, lines.Count - 1)];
    }

    private string? SuggestPath(string rel)
    {
        // Recuperacion: sugiere rutas cercanas del proyecto (archivos y directorios).
        var trimmed = (rel ?? "").Trim('\\', '/', ' ').ToLowerInvariant();
        if (string.IsNullOrEmpty(trimmed)) return ".";
        var candidates = new List<string>();
        try
        {
            foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)
                .Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\") && !f.Contains("\\.git\\") && !f.Contains("\\node_modules\\")))
            {
                var fp = NormalizeSep(file).ToLowerInvariant();
                if (fp.Contains(trimmed) || trimmed.Contains(Path.GetFileName(file).ToLowerInvariant()))
                    candidates.Add(RelativeOf(file));
            }
        }
        catch { }

        if (candidates.Count == 0) return ". (no hay coincidencias)";
        return ". Candidatos coincidentes: " + string.Join(", ", candidates.Distinct(StringComparer.OrdinalIgnoreCase).Take(8));
    }

    private static string NormalizeSep(string path) => path.Replace('\\', '/');

    private async Task<AgentStep> RunBuildAsync(AgentAction action, int iteration, CancellationToken ct)
    {
        return await RunDotAsync("build", iteration, ct);
    }

    private async Task<AgentStep> RunTestAsync(AgentAction action, int iteration, CancellationToken ct)
    {
        return await RunDotAsync("test", iteration, ct);
    }

    private async Task<AgentStep> RunRestoreAsync(AgentAction action, int iteration, CancellationToken ct)
    {
        return await RunDotAsync("restore", iteration, ct);
    }

    private async Task<AgentStep> RunDotAsync(string kind, int iteration, CancellationToken ct)
    {
        var manifest = FindManifest();
        if (string.IsNullOrWhiteSpace(manifest)) return F(iteration, kind, "No hay un proyecto con sistema de build reconocido; se omite la compilacion.", null);

        var run = await _runner.RunAsync(_root, manifest, kind, _timeout, ct);
        if (run.ValidationReason is not null) return F(iteration, kind, run.ValidationReason, manifest);
        var ok = run.ExitCode == 0 && !run.TimedOut && !run.NotExecutable && !run.Incomplete;

        var output = (run.Output ?? "");

        return new AgentStep
        {
            Iteration = iteration,
            Action = kind,
            Path = manifest,
            Success = ok,
            ResultPreview = Truncate(output),
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
                .Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\") && !f.Contains("\\.git\\") && !f.Contains("\\node_modules\\")))
            {
                try
                {
                    var lines = File.ReadAllLines(file);
                    for (var i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].Contains(term, StringComparison.OrdinalIgnoreCase))
                        {
                            hits.Add(RelativeOf(file) + ":" + (i + 1));
                            if (hits.Count >= 50) break;
                        }
                    }
                }
                catch { }
                if (hits.Count >= 50) break;
            }
        }
        catch { }

        return Ok(iteration, action.Action, action.Path ?? term, hits.Count > 0 ? string.Join("\n", hits) : "Sin coincidencias en el proyecto.");
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
        try
        {
            string? f = Directory.EnumerateFiles(_root, "*.slnx", SearchOption.AllDirectories).FirstOrDefault()
                ?? Directory.EnumerateFiles(_root, "*.sln", SearchOption.AllDirectories).FirstOrDefault()
                ?? Directory.EnumerateFiles(_root, "*.csproj", SearchOption.AllDirectories)
                    .Where(p => !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) &&
                                !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                    .FirstOrDefault();
            return f is null ? null : Path.GetFileName(f);
        }
        catch { return null; }
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

    private string RelativeOf(string full)
    {
        var fullPath = Path.GetFullPath(full);
        var root = _root.TrimEnd(Path.DirectorySeparatorChar);
        var rel = fullPath.Equals(root, StringComparison.OrdinalIgnoreCase)
            ? ""
            : fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                ? fullPath.Substring(root.Length + 1)
                : fullPath;
        // Rutas mostradas al modelo siempre con "/" para consistencia.
        return rel.Replace('\\', '/');
    }

    private static AgentStep Ok(int iteration, string action, string? path, string result) =>
        new() { Iteration = iteration, Action = action, Path = path, Success = true, ResultPreview = Truncate(result), AtUtc = DateTime.UtcNow };

    private static AgentStep F(int iteration, string action, string reason, string? path) =>
        new() { Iteration = iteration, Action = action, Path = path, Success = false, ResultPreview = reason, AtUtc = DateTime.UtcNow };

    private static string Truncate(string s) => s is { Length: > 12000 } ? s.Substring(0, 12000) + "\n... [truncado]" : (s ?? "");

    private static string Shorten(string s, int max) => s is not null && s.Length > max ? s.Substring(0, max) + "..." : (s ?? "");
}
