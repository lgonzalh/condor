using System.Text;
using System.Text.Json;
using Condor.Core.Models;

namespace Condor.Infrastructure.Documentation;

internal sealed record ReviewOutcomeSummary(
    int Executions,
    int ExecutionsLast30Days,
    int Useful,
    int Discarded,
    int Open,
    int Unclassified,
    int UsefulLast30Days,
    int DiscardedLast30Days,
    int OpenLast30Days,
    int UnclassifiedLast30Days)
{
    public double? UsefulRate => Useful + Discarded == 0 ? null : (double)Useful / (Useful + Discarded);
    public double? UsefulRateLast30Days => UsefulLast30Days + DiscardedLast30Days == 0
        ? null
        : (double)UsefulLast30Days / (UsefulLast30Days + DiscardedLast30Days);
}

internal static class ReviewOutcomeJournal
{
    private const int CurrentSchemaVersion = 1;
    private const int MaximumRecordBytes = 4096;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static async Task<ReviewOutcomeSummary?> RecordAndSummarizeAsync(
        string workingDirectory,
        IReadOnlyList<AgentReviewFinding> findings,
        CancellationToken cancellationToken = default)
    {
        var directory = ResolveDirectory(workingDirectory, create: true);
        if (directory is null) return null;

        var now = DateTime.UtcNow;
        var record = new ReviewOutcomeRecord
        {
            RecordedAtUtc = now,
            Total = findings.Count,
            Useful = findings.Count(finding => finding.Status == "resolved"),
            Discarded = findings.Count(finding => finding.Status == "accepted"),
            Open = findings.Count(finding => finding.Status == "open")
        };
        record.Unclassified = Math.Max(0, record.Total - record.Useful - record.Discarded - record.Open);

        var json = JsonSerializer.Serialize(record);
        if (Encoding.UTF8.GetByteCount(json) > MaximumRecordBytes) return null;
        var fileName = now.ToString("yyyyMMdd'T'HHmmssfffffff'Z'", System.Globalization.CultureInfo.InvariantCulture)
            + "-" + Guid.NewGuid().ToString("N") + ".json";
        var path = Path.Combine(directory, fileName);
        var temporaryPath = path + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, json, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, path);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }

        return await SummarizeAsync(directory, now, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ReviewOutcomeSummary> SummarizeAsync(
        string directory,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var records = new List<ReviewOutcomeRecord>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DocumentationFileSafety.IsReparsePoint(path)) continue;
            try
            {
                var info = new FileInfo(path);
                if (info.Length is <= 0 or > MaximumRecordBytes) continue;
                var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                var record = JsonSerializer.Deserialize<ReviewOutcomeRecord>(json, JsonOptions);
                if (record is null || record.SchemaVersion != CurrentSchemaVersion || record.Total < 0 ||
                    record.Useful < 0 || record.Discarded < 0 || record.Open < 0 || record.Unclassified < 0 ||
                    record.Useful + record.Discarded + record.Open + record.Unclassified != record.Total)
                {
                    continue;
                }
                records.Add(record);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Ignore a corrupt or unreadable local record; the rest remain usable.
            }
        }

        var recent = records.Where(record => record.RecordedAtUtc >= now.AddDays(-30)).ToList();
        return new ReviewOutcomeSummary(
            records.Count,
            recent.Count,
            records.Sum(record => record.Useful),
            records.Sum(record => record.Discarded),
            records.Sum(record => record.Open),
            records.Sum(record => record.Unclassified),
            recent.Sum(record => record.Useful),
            recent.Sum(record => record.Discarded),
            recent.Sum(record => record.Open),
            recent.Sum(record => record.Unclassified));
    }

    private static string? ResolveDirectory(string workingDirectory, bool create)
    {
        try
        {
            var root = Path.GetFullPath(workingDirectory);
            if (!Directory.Exists(root)) return null;

            var stateDirectory = Path.Combine(root, ".condor");
            if (File.Exists(stateDirectory)) return null;
            if (!Directory.Exists(stateDirectory) && create) Directory.CreateDirectory(stateDirectory);
            if (!Directory.Exists(stateDirectory) || DocumentationFileSafety.IsReparsePoint(stateDirectory)) return null;

            var directory = Path.Combine(stateDirectory, "review-outcomes");
            if (File.Exists(directory)) return null;
            if (!Directory.Exists(directory) && create) Directory.CreateDirectory(directory);
            if (!Directory.Exists(directory) || DocumentationFileSafety.IsReparsePoint(directory)) return null;
            return directory;
        }
        catch
        {
            return null;
        }
    }

    private sealed class ReviewOutcomeRecord
    {
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public DateTime RecordedAtUtc { get; set; }
        public int Total { get; set; }
        public int Useful { get; set; }
        public int Discarded { get; set; }
        public int Open { get; set; }
        public int Unclassified { get; set; }
    }
}
