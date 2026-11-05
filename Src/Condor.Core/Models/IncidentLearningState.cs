namespace Condor.Core.Models;

public sealed class IncidentLearningState
{
    public int SchemaVersion { get; set; } = 1;
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public Dictionary<string, IncidentLearningEntry> Entries { get; set; } = new(StringComparer.Ordinal);
}

public sealed class IncidentLearningEntry
{
    public int Occurrences { get; set; }
    public DateTimeOffset FirstObservedAtUtc { get; set; }
    public DateTimeOffset LastObservedAtUtc { get; set; }
}
