using System;
using System.Collections.Generic;
using System.IO;
using Condor.Core.Models;

namespace Condor.Infrastructure.Building;

public sealed class ProjectFileWriter
{
    public void Apply(IReadOnlyList<BuildAction> actions, string workingDirectory)
    {
        foreach (var action in actions)
        {
            ApplyAction(action, workingDirectory);
        }
    }

    private static void ApplyAction(BuildAction action, string workingDirectory)
    {
        if (!TryResolvePath(workingDirectory, action.RelativePath, out var fullPath))
        {
            action.Status = BuildAction.StatusOmitted;
            action.StatusReason = "Ruta fuera del proyecto objetivo.";
            return;
        }

        try
        {
            switch (action.Kind)
            {
                case BuildActionKind.Crear:
                    ApplyCreate(action, fullPath);
                    break;
                case BuildActionKind.Actualizar:
                    ApplyUpdate(action, fullPath);
                    break;
                default:
                    action.Status = BuildAction.StatusOmitted;
                    action.StatusReason = "Operacion no soportada.";
                    break;
            }
        }
        catch
        {
            action.Status = BuildAction.StatusFailed;
            action.StatusReason = "No fue posible escribir el archivo.";
        }
    }

    private static void ApplyCreate(BuildAction action, string fullPath)
    {
        if (File.Exists(fullPath))
        {
            action.Status = BuildAction.StatusOmitted;
            action.StatusReason = "El archivo ya existe; la operacion Crear no sobrescribe.";
            return;
        }

        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, action.Content);
        action.Status = BuildAction.StatusApplied;
    }

    private static void ApplyUpdate(BuildAction action, string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            action.Status = BuildAction.StatusOmitted;
            action.StatusReason = "El archivo no existe; la operacion Actualizar no lo crea.";
            return;
        }

        File.WriteAllText(fullPath, action.Content);
        action.Status = BuildAction.StatusApplied;
    }

    private static bool TryResolvePath(string workingDirectory, string relativePath, out string fullPath)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(workingDirectory) || string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var baseDirectory = Path.GetFullPath(workingDirectory).TrimEnd(Path.DirectorySeparatorChar);
        var combined = Path.Combine(baseDirectory, relativePath);
        var candidate = Path.GetFullPath(combined);

        if (!candidate.Equals(baseDirectory, StringComparison.OrdinalIgnoreCase) &&
            !candidate.StartsWith(baseDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        fullPath = candidate;
        return true;
    }
}
