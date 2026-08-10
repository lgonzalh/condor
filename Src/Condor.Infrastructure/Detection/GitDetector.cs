using System.Text.RegularExpressions;
using Condor.Core.Models;
using Condor.Infrastructure.Probing;

namespace Condor.Infrastructure.Detection;

public class GitDetector
{
    public async Task<ToolInfo> DetectAsync(CancellationToken cancellationToken = default)
    {
        var path = ToolDetector.FindInPath("git");
        if (path is null)
        {
            return new ToolInfo
            {
                Name = "git",
                Status = DetectionStatus.NotDetected,
                Reason = "git no esta en el PATH"
            };
        }

        var tool = new ToolInfo
        {
            Name = "git",
            Path = path,
            Status = DetectionStatus.Detected
        };

        var output = await ProcessProbe.RunAsync(
            path,
            "--version",
            10000,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(output))
        {
            var match = Regex.Match(output, @"git version\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                tool.Version = match.Groups[1].Value;
            }
        }

        return tool;
    }
}
