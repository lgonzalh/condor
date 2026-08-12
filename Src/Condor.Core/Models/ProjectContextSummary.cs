namespace Condor.Core.Models;

public class ProjectContextSummary
{
    public List<string> Languages { get; set; } = new();
    public List<string> Frameworks { get; set; } = new();
    public int ManifestCount { get; set; }
    public int DocumentationCount { get; set; }
    public bool IsGitRepository { get; set; }
    public string? GitBranch { get; set; }
    public bool GitIsDirty { get; set; }
    public List<string> LastCommits { get; set; } = new();
    public bool HasOperativeArtifacts { get; set; }
}