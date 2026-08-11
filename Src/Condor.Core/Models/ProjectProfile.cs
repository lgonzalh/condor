namespace Condor.Core.Models;

public class ProjectProfile
{
    public DetectionStatus Status { get; set; } = DetectionStatus.Detected;
    public string? Reason { get; set; }
    public string RootPath { get; set; } = "";
    public string RootName { get; set; } = "";
    public bool IsGitRepository { get; set; }
    public GitProjectState? Git { get; set; }
    public List<LanguageEvidence> Languages { get; set; } = new();
    public List<FrameworkEvidence> Frameworks { get; set; } = new();
    public List<ManifestInfo> Manifests { get; set; } = new();
    public List<DocumentationInfo> Documentation { get; set; } = new();
    public List<string> TopLevelDirectories { get; set; } = new();
    public List<string> TopLevelFiles { get; set; } = new();
    public List<ExtensionCount> ExtensionCounts { get; set; } = new();
    public int DirectoriesCount { get; set; }
    public int FilesCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public bool TotalSizeExceeded { get; set; }
    public List<string> LimitsApplied { get; set; } = new();
    public DateTime GeneratedAtUtc { get; set; }
}