using Condor.Core.Models;

namespace Condor.Core.Project;

public static class ProjectProfileBuilder
{
    public static ProjectProfile Build(
        DetectionStatus status,
        string? reason,
        string rootPath,
        string rootName,
        ProjectScan scan,
        IReadOnlyList<ManifestRecord> manifests,
        ClassificationResult classification,
        GitProjectState? git,
        bool isGitRepository,
        IReadOnlyList<string> limitsApplied,
        DateTime generatedAtUtc)
    {
        var topLevelDirectories = scan.Directories
            .Where(d => !d.RelativePath.Contains('/'))
            .Select(d => d.RelativePath)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var topLevelFiles = scan.Files
            .Where(f => !f.RelativePath.Contains('/'))
            .Select(f => f.RelativePath)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var manifestInfos = manifests
            .OrderBy(m => m.Path, StringComparer.Ordinal)
            .Select(m => new ManifestInfo
            {
                Kind = m.Kind,
                Path = m.Path,
                Name = m.Name,
                Version = m.Version,
                Dependencies = m.Dependencies.ToList(),
                ParseError = m.ParseError,
                SizeBytes = m.SizeBytes
            })
            .ToList();

        return new ProjectProfile
        {
            Status = status,
            Reason = reason,
            RootPath = rootPath,
            RootName = rootName,
            IsGitRepository = isGitRepository,
            Git = git,
            Languages = classification.Languages,
            Frameworks = classification.Frameworks,
            Manifests = manifestInfos,
            Documentation = classification.Documentation,
            TopLevelDirectories = topLevelDirectories,
            TopLevelFiles = topLevelFiles,
            ExtensionCounts = classification.ExtensionCounts,
            DirectoriesCount = scan.Directories.Count,
            FilesCount = scan.Files.Count,
            TotalSizeBytes = scan.TotalSizeBytes,
            TotalSizeExceeded = scan.TotalSizeExceeded,
            LimitsApplied = limitsApplied.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList(),
            GeneratedAtUtc = generatedAtUtc
        };
    }
}