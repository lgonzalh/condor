using Condor.Core.Models;

namespace Condor.Infrastructure.Detection;

public class ToolDetector
{
    private static readonly string[] KnownTools =
    {
        "git", "python", "py", "node", "npm", "dotnet", "java",
        "docker", "gh", "go", "cargo", "powershell", "pwsh"
    };

    public List<ToolInfo> DetectAll()
    {
        var tools = new List<ToolInfo>();
        foreach (var tool in KnownTools)
        {
            var path = FindInPath(tool);
            tools.Add(path is null
                ? new ToolInfo { Name = tool, Status = DetectionStatus.NotDetected }
                : new ToolInfo { Name = tool, Path = path, Status = DetectionStatus.Detected });
        }

        return tools;
    }

    public static string? FindInPath(string tool)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? "";
        var directories = pathValue.Split(
            Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var directory in directories)
        {
            foreach (var extension in new[] { ".exe", ".cmd", ".bat" })
            {
                var candidate = Path.Combine(directory, tool + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
