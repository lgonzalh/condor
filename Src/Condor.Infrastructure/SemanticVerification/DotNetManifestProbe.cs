using System;
using System.Collections.Generic;
using System.IO;

namespace Condor.Infrastructure.SemanticVerification;

public sealed class DotNetManifestProbe
{
    public List<string> Find(string workingDirectory)
    {
        var result = new List<string>();

        if (string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory))
        {
            return result;
        }

        try
        {
            foreach (var pattern in new[] { "*.sln", "*.csproj" })
            {
                foreach (var file in Directory.EnumerateFiles(workingDirectory, pattern, SearchOption.TopDirectoryOnly))
                {
                    result.Add(Path.GetFullPath(file));
                }
            }
        }
        catch
        {
            return result;
        }

        return result;
    }
}
