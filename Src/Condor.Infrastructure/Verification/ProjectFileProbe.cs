using System;
using System.Collections.Generic;
using System.IO;
using Condor.Core.Models;

namespace Condor.Infrastructure.Verification;

public sealed class ProjectFileProbe
{
    public Dictionary<string, ProbedFile?> Read(
        IReadOnlyList<BuildAction> actions,
        string workingDirectory,
        int maxContentLength)
    {
        var result = new Dictionary<string, ProbedFile?>(StringComparer.Ordinal);

        foreach (var action in actions)
        {
            if (string.IsNullOrWhiteSpace(action.RelativePath))
            {
                continue;
            }

            if (!TryResolvePath(workingDirectory, action.RelativePath, out var fullPath))
            {
                continue;
            }

            if (!File.Exists(fullPath))
            {
                result[action.RelativePath] = null;
                continue;
            }

            string content;

            try
            {
                content = File.ReadAllText(fullPath);
            }
            catch
            {
                result[action.RelativePath] = null;
                continue;
            }

            if (content.Length > maxContentLength)
            {
                content = content.Substring(0, maxContentLength);
            }

            result[action.RelativePath] = new ProbedFile { Content = content };
        }

        return result;
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
