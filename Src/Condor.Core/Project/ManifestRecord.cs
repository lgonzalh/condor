namespace Condor.Core.Project;

public sealed class ManifestRecord
{
    public string Kind { get; set; } = "";
    public string Path { get; set; } = "";
    public string? Name { get; set; }
    public string? Version { get; set; }
    public List<string> Dependencies { get; } = new();
    public bool ParseError { get; set; }
    public long SizeBytes { get; set; }
    public bool LimitManifestSize { get; set; }
    public bool DependenciesTruncated { get; set; }
    public string? Sdk { get; set; }
    public bool UseWpf { get; set; }
    public bool UseWindowsForms { get; set; }
}