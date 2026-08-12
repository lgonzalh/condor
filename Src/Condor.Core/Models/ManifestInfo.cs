namespace Condor.Core.Models;

public class ManifestInfo
{
    public string Kind { get; set; } = "";
    public string Path { get; set; } = "";
    public string? Name { get; set; }
    public string? Version { get; set; }
    public List<string> Dependencies { get; set; } = new();
    public bool ParseError { get; set; }
    public long SizeBytes { get; set; }
}