using System.Collections.Generic;

namespace Condor.Core.Agent;

public static class AgentContentFileTypes
{
    private static readonly HashSet<string> SourceExtensions = new(System.StringComparer.Ordinal)
    {
        ".cs", ".vb", ".fs", ".ts", ".js", ".mjs", ".cjs", ".py", ".go", ".rs",
        ".java", ".kt", ".swift", ".rb", ".php", ".html", ".htm", ".css", ".scss",
        ".sass", ".less", ".csx"
    };

    private static readonly HashSet<string> ReadableExtensions = new(System.StringComparer.Ordinal)
    {
        ".cs", ".vb", ".fs", ".ts", ".js", ".mjs", ".cjs", ".py", ".go", ".rs",
        ".java", ".kt", ".swift", ".rb", ".php", ".html", ".htm", ".css", ".scss", ".sass",
        ".less", ".csproj", ".fsproj", ".vbproj", ".sln", ".slnx", ".csx", ".json", ".yaml", ".yml",
        ".xml", ".ini", ".cfg", ".toml", ".md", ".markdown", ".txt"
    };

    public static bool IsReadableExtension(string? extension)
        => !string.IsNullOrWhiteSpace(extension) && ReadableExtensions.Contains(extension.ToLowerInvariant());

    public static bool IsSourceExtension(string? extension)
        => !string.IsNullOrWhiteSpace(extension) && SourceExtensions.Contains(extension.ToLowerInvariant());
}
