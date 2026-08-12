namespace Condor.Core.Project;

public sealed record ScannedFile(string RelativePath, long SizeBytes);

public sealed record ScannedDirectory(string RelativePath, bool IsReparsePoint);

public sealed class ProjectScan
{
    public List<ScannedFile> Files { get; } = new();
    public List<ScannedDirectory> Directories { get; } = new();
    public long TotalSizeBytes { get; set; }
    public bool TotalSizeExceeded { get; set; }
    public bool Stopped { get; set; }
    public List<string> Degradations { get; } = new();
    public List<string> LimitsApplied { get; } = new();
    public Dictionary<string, int> ExtensionCounts { get; } = new(StringComparer.Ordinal);
}