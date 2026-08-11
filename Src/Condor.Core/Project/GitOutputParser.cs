using Condor.Core.Models;

namespace Condor.Core.Project;

public static class GitOutputParser
{
    public static bool IsInsideWorkTree(string? output)
    {
        return string.Equals(output?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
    }

    public static string? ParseBranch(string? output)
    {
        var value = output?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    public static bool IsDirty(string? statusPorcelain)
    {
        return !string.IsNullOrWhiteSpace(statusPorcelain);
    }

    public static List<GitCommitSummary> ParseLog(string? output, int maxCommits, int hashLength, int subjectLength)
    {
        var commits = new List<GitCommitSummary>();
        if (output is null)
        {
            return commits;
        }

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var separator = line.IndexOf('|');
            var hash = separator >= 0 ? line.Substring(0, separator) : line;
            var subject = separator >= 0 ? line.Substring(separator + 1) : "";

            if (hash.Length > hashLength)
            {
                hash = hash.Substring(0, hashLength);
            }

            if (subject.Length > subjectLength)
            {
                subject = subject.Substring(0, subjectLength);
            }

            commits.Add(new GitCommitSummary { Hash = hash, Subject = subject });
            if (commits.Count >= maxCommits)
            {
                break;
            }
        }

        return commits;
    }
}