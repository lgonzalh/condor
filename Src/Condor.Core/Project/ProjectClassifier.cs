using Condor.Core.Models;

namespace Condor.Core.Project;

public sealed class ClassificationResult
{
    public List<LanguageEvidence> Languages { get; } = new();
    public List<FrameworkEvidence> Frameworks { get; } = new();
    public List<ExtensionCount> ExtensionCounts { get; } = new();
    public List<DocumentationInfo> Documentation { get; } = new();
}

public sealed class ProjectClassifier
{
    public ClassificationResult Classify(ProjectScan scan, IReadOnlyList<ManifestRecord> manifests)
    {
        var result = new ClassificationResult();

        foreach (var pair in scan.ExtensionCounts.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            result.ExtensionCounts.Add(new ExtensionCount { Name = pair.Key, Count = pair.Value });
        }

        result.Documentation.AddRange(DocumentationOf(scan));
        result.Languages.AddRange(LanguagesOf(scan, manifests));
        result.Frameworks.AddRange(FrameworksOf(manifests));
        return result;
    }

    private static List<LanguageEvidence> LanguagesOf(ProjectScan scan, IReadOnlyList<ManifestRecord> manifests)
    {
        var languages = new List<LanguageEvidence>();

        foreach (var family in SignalCatalog.Families)
        {
            var foundKinds = family.ManifestKinds
                .Where(kind => manifests.Any(m => m.Kind == kind))
                .ToList();
            var foundMarkers = family.MarkerFiles
                .Where(marker => HasMarker(scan, marker))
                .ToList();
            var foundExtensions = family.Extensions
                .Where(extension => scan.ExtensionCounts.TryGetValue(SignalCatalog.ExtensionKey(extension), out var count) && count >= 3)
                .ToList();

            if (foundKinds.Count > 0 || foundMarkers.Count > 0)
            {
                var signals = new List<EvidenceSignal>();
                foreach (var kind in foundKinds)
                {
                    signals.Add(new EvidenceSignal { Kind = EvidenceKind.Manifest, Value = kind });
                }

                foreach (var marker in foundMarkers)
                {
                    signals.Add(new EvidenceSignal { Kind = EvidenceKind.Manifest, Value = marker });
                }

                foreach (var extension in foundExtensions)
                {
                    signals.Add(new EvidenceSignal
                    {
                        Kind = EvidenceKind.Extension,
                        Value = extension,
                        Count = scan.ExtensionCounts[SignalCatalog.ExtensionKey(extension)]
                    });
                }

                languages.Add(new LanguageEvidence { Name = family.Name, Primary = true, Signals = signals });
            }
            else if (foundExtensions.Count > 0)
            {
                var signals = foundExtensions
                    .Select(extension => new EvidenceSignal
                    {
                        Kind = EvidenceKind.Extension,
                        Value = extension,
                        Count = scan.ExtensionCounts[SignalCatalog.ExtensionKey(extension)]
                    })
                    .ToList();

                languages.Add(new LanguageEvidence { Name = family.Name, Primary = false, Signals = signals });
            }
        }

        return languages.OrderBy(l => l.Name, StringComparer.Ordinal).ToList();
    }

    private static bool HasMarker(ProjectScan scan, string marker)
    {
        return scan.Files.Any(f =>
            f.RelativePath.Equals(marker, StringComparison.OrdinalIgnoreCase) ||
            f.RelativePath.Equals("src/" + marker, StringComparison.OrdinalIgnoreCase));
    }

    private static List<FrameworkEvidence> FrameworksOf(IReadOnlyList<ManifestRecord> manifests)
    {
        var frameworks = new List<FrameworkEvidence>();

        foreach (var manifest in manifests)
        {
            switch (manifest.Kind)
            {
                case "csproj":
                    if (!string.IsNullOrWhiteSpace(manifest.Sdk) && manifest.Sdk.Contains("Web", StringComparison.OrdinalIgnoreCase))
                    {
                        Add(frameworks, "ASP.NET Core", "Sdk " + manifest.Sdk, manifest.Path);
                    }
                    else
                    {
                        var aspNet = manifest.Dependencies.FirstOrDefault(d => d.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase));
                        if (aspNet is not null)
                        {
                            Add(frameworks, "ASP.NET Core", "dependencia " + aspNet, manifest.Path);
                        }
                    }

                    if (manifest.UseWpf)
                    {
                        Add(frameworks, "WPF", "UseWPF", manifest.Path);
                    }

                    if (manifest.UseWindowsForms)
                    {
                        Add(frameworks, "WinForms", "UseWindowsForms", manifest.Path);
                    }

                    break;

                case "package.json":
                    if (ContainsName(manifest, "react"))
                    {
                        Add(frameworks, "React", "dependencia react", manifest.Path);
                    }

                    if (ContainsName(manifest, "vue"))
                    {
                        Add(frameworks, "Vue", "dependencia vue", manifest.Path);
                    }

                    if (ContainsName(manifest, "@angular/core"))
                    {
                        Add(frameworks, "Angular", "dependencia @angular/core", manifest.Path);
                    }

                    if (ContainsName(manifest, "express"))
                    {
                        Add(frameworks, "Express", "dependencia express", manifest.Path);
                    }

                    break;

                case "requirements.txt":
                case "pyproject.toml":
                    if (ContainsName(manifest, "Django"))
                    {
                        Add(frameworks, "Django", "dependencia Django", manifest.Path);
                    }

                    if (ContainsName(manifest, "Flask"))
                    {
                        Add(frameworks, "Flask", "dependencia Flask", manifest.Path);
                    }

                    break;

                case "pom.xml":
                    if (manifest.Dependencies.Any(d => d.StartsWith("spring-boot-", StringComparison.OrdinalIgnoreCase)))
                    {
                        Add(frameworks, "Spring Boot", "dependencia spring-boot-*", manifest.Path);
                    }

                    break;
            }
        }

        return frameworks.OrderBy(f => f.Name, StringComparer.Ordinal).ToList();
    }

    private static bool ContainsName(ManifestRecord manifest, string name)
    {
        return manifest.Dependencies.Contains(name, StringComparer.OrdinalIgnoreCase);
    }

    private static void Add(List<FrameworkEvidence> frameworks, string name, string signal, string manifestPath)
    {
        frameworks.Add(new FrameworkEvidence { Name = name, Signal = signal, Manifest = manifestPath });
    }

    private static List<DocumentationInfo> DocumentationOf(ProjectScan scan)
    {
        var documentation = new List<DocumentationInfo>();

        foreach (var file in scan.Files)
        {
            var name = NameOf(file.RelativePath);
            var directory = DirectoryOf(file.RelativePath);

            if (SignalCatalog.ReadmeNames.Contains(name, StringComparer.OrdinalIgnoreCase) &&
                (directory.Length == 0 || directory.Equals("docs", StringComparison.OrdinalIgnoreCase)))
            {
                documentation.Add(new DocumentationInfo { Kind = "README", Path = file.RelativePath, SizeBytes = file.SizeBytes });
            }
            else if (SignalCatalog.LicenseNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                documentation.Add(new DocumentationInfo { Kind = "LICENSE", Path = file.RelativePath, SizeBytes = file.SizeBytes });
            }
            else if (SignalCatalog.ChangelogNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                documentation.Add(new DocumentationInfo { Kind = "CHANGELOG", Path = file.RelativePath, SizeBytes = file.SizeBytes });
            }
        }

        if (scan.Directories.Any(d => d.RelativePath.Equals("docs", StringComparison.OrdinalIgnoreCase)))
        {
            documentation.Add(new DocumentationInfo { Kind = "docs", Path = "docs", SizeBytes = 0 });
        }

        return documentation.OrderBy(d => d.Path, StringComparer.Ordinal).ToList();
    }

    private static string NameOf(string relativePath)
    {
        var index = relativePath.LastIndexOf('/');
        return index >= 0 ? relativePath.Substring(index + 1) : relativePath;
    }

    private static string DirectoryOf(string relativePath)
    {
        var index = relativePath.LastIndexOf('/');
        return index > 0 ? relativePath.Substring(0, index) : "";
    }
}