using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Condor.Infrastructure.Agent;

/// <summary>
/// Toma una instantanea del estado textual de los archivos fuente del proyecto
/// para verificar integridad al final (p. ej. evitar que el agente "arregle" una
/// tarea modificando las pruebas en lugar del codigo de produccion).
/// </summary>
public sealed class RepoSnapshot
{
    private readonly Dictionary<string, string?> _files;

    private RepoSnapshot(Dictionary<string, string?> files)
    {
        _files = files;
    }

    public static RepoSnapshot Capture(string root)
    {
        var files = new Dictionary<string, string?>(System.StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Where(f => IsSourceFile(f) && !f.Contains("\\bin\\") && !f.Contains("\\obj\\") && !f.Contains("\\.git\\") && !f.Contains("\\node_modules\\")))
            {
                try
                {
                    files[Path.GetFullPath(file)] = File.ReadAllText(file);
                }
                catch
                {
                    // archivo ilegible o en uso; se omite.
                }
            }
        }
        catch
        {
            // sin instantanea; se falla siempre de manera honesta en la comprobacion
        }

        return new RepoSnapshot(files);
    }

    /// <summary>Devuelve las rutas de archivos de prueba (fuente) que cambiaron respecto a la instantanea.</summary>
    public IReadOnlyList<string> ChangedTestFiles(string root)
    {
        var changed = new List<string>();
        try
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Where(f => IsTestSource(f) && !f.Contains("\\bin\\") && !f.Contains("\\obj\\") && !f.Contains("\\.git\\") && !f.Contains("\\node_modules\\")))
            {
                var full = Path.GetFullPath(file);
                var original = _files.TryGetValue(full, out var prior) ? prior : null;
                string? current = null;
                try { current = File.ReadAllText(full); } catch { current = null; }

                if (!Equals(original, current))
                {
                    changed.Add(RelativeOf(root, full));
                }
            }
        }
        catch
        {
        }

        return changed;
    }

    public static bool IsSourceFile(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".vb", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".fs", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".ts", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".js", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".py", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".go", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".rs", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".java", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".csproj", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".fsproj", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".vcxproj", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTestSource(string path)
    {
        // Archivo de prueba o dentro de un proyecto/nombre de prueba de .NET.
        return path.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith("Test.cs", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("\\Tests\\", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("\\Test\\", StringComparison.OrdinalIgnoreCase) ||
               (Path.GetFileName(path)?.Contains("Tests", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static string RelativeOf(string root, string full)
    {
        var r = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        return full.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? full.Substring(r.Length + 1).Replace('\\', '/')
            : full;
    }
}
