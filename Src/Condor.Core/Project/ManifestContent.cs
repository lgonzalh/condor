namespace Condor.Core.Project;

public sealed class ManifestContent
{
    public string? Name { get; set; }
    public string? Version { get; set; }
    public List<string> Dependencies { get; } = new();
    public bool ParseError { get; set; }
    public bool DependenciesTruncated { get; set; }
    public string? Sdk { get; set; }
    public bool UseWpf { get; set; }
    public bool UseWindowsForms { get; set; }
    public string? TsTarget { get; set; }

    public void CapAndSortDependencies()
    {
        var unique = Dependencies.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
        DependenciesTruncated = unique.Count > DiscoveryLimits.Default.MaxDependencies;
        Dependencies.Clear();
        Dependencies.AddRange(unique.Take(DiscoveryLimits.Default.MaxDependencies));
    }
}