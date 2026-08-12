using Condor.Core.Models;
using Condor.Core.Project;

namespace Condor.Infrastructure.Project;

public sealed class ProjectDetector
{
    private readonly DiscoveryLimits limits;
    private readonly DirectoryScanner scanner;
    private readonly ManifestFileReader reader;
    private readonly ProjectClassifier classifier;
    private readonly GitRepositoryProbe gitProbe;

    public ProjectDetector(DiscoveryLimits? limits = null)
    {
        this.limits = limits ?? DiscoveryLimits.Default;
        scanner = new DirectoryScanner(this.limits);
        reader = new ManifestFileReader(this.limits);
        classifier = new ProjectClassifier();
        gitProbe = new GitRepositoryProbe(this.limits);
    }

    public async Task<ProjectProfile> DiscoverAsync(
        string workingDirectory,
        ToolInfo? gitTool,
        CancellationToken cancellationToken = default)
    {
        var generatedAt = DateTime.UtcNow;
        var rootPath = Path.GetFullPath(workingDirectory);
        var rootName = RootName(rootPath);

        if (!Directory.Exists(rootPath))
        {
            return new ProjectProfile
            {
                Status = DetectionStatus.NotDetected,
                Reason = "la ruta no existe o no es un directorio",
                RootPath = rootPath,
                RootName = rootName,
                GeneratedAtUtc = generatedAt
            };
        }

        if (!CanAccess(rootPath))
        {
            return new ProjectProfile
            {
                Status = DetectionStatus.Error,
                Reason = "sin acceso a la ruta de trabajo",
                RootPath = rootPath,
                RootName = rootName,
                GeneratedAtUtc = generatedAt
            };
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(limits.DiscoveryTimeoutMilliseconds);

        var scan = scanner.Scan(rootPath, timeoutCts.Token);

        var candidates = scan.Files
            .Where(file => SignalCatalog.ManifestKindOf(NameOf(file.RelativePath)) is not null)
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .ToList();

        if (candidates.Count > limits.MaxManifests)
        {
            AddOnce(scan.LimitsApplied, DiscoveryLimits.LimitManifests);
            scan.Degradations.Add("limite de manifiestos alcanzado");
        }

        var manifests = new List<ManifestRecord>();
        foreach (var candidate in candidates.Take(limits.MaxManifests))
        {
            if (timeoutCts.IsCancellationRequested)
            {
                AddOnce(scan.LimitsApplied, DiscoveryLimits.LimitTimeout);
                scan.Degradations.Add("tiempo maximo del descubrimiento alcanzado");
                break;
            }

            var record = reader.Read(rootPath, candidate);
            if (record is null)
            {
                AddOnce(scan.Degradations, "no se pudo leer el manifiesto '" + candidate.RelativePath + "'");
                continue;
            }

            if (record.LimitManifestSize)
            {
                AddOnce(scan.LimitsApplied, DiscoveryLimits.LimitManifestSize);
                AddOnce(scan.Degradations, "el manifiesto '" + candidate.RelativePath + "' supera el tamano maximo");
            }

            if (record.DependenciesTruncated)
            {
                AddOnce(scan.LimitsApplied, DiscoveryLimits.LimitDependencies);
                AddOnce(scan.Degradations, "dependencias limitadas en '" + candidate.RelativePath + "'");
            }

            manifests.Add(record);
        }

        var classification = classifier.Classify(scan, manifests);

        var isGitRepository = false;
        GitProjectState? git = null;
        if (!timeoutCts.IsCancellationRequested &&
            gitTool is not null &&
            gitTool.Status == DetectionStatus.Detected &&
            !string.IsNullOrWhiteSpace(gitTool.Path))
        {
            var outcome = await gitProbe.ProbeAsync(rootPath, gitTool.Path, timeoutCts.Token);
            if (outcome.CouldNotVerify)
            {
                AddOnce(scan.Degradations, "no fue posible consultar el estado de Git");
            }
            else if (outcome.State is not null)
            {
                isGitRepository = true;
                git = outcome.State;
                if (outcome.State.Status == DetectionStatus.Error && !string.IsNullOrWhiteSpace(outcome.State.Reason))
                {
                    AddOnce(scan.Degradations, outcome.State.Reason!);
                }
            }
        }

        var limitsApplied = scan.LimitsApplied.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
        var reason = scan.Degradations.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
        var degraded = limitsApplied.Count > 0 || reason.Count > 0;
        var status = degraded ? DetectionStatus.Limited : DetectionStatus.Detected;

        return ProjectProfileBuilder.Build(
            status,
            reason.Count > 0 ? string.Join("; ", reason) : null,
            rootPath,
            rootName,
            scan,
            manifests,
            classification,
            git,
            isGitRepository,
            limitsApplied,
            generatedAt);
    }

    private static bool CanAccess(string rootPath)
    {
        try
        {
            using var enumerator = Directory.EnumerateFileSystemEntries(rootPath).GetEnumerator();
            enumerator.MoveNext();
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static string RootName(string rootPath)
    {
        var name = new DirectoryInfo(rootPath).Name;
        return string.IsNullOrEmpty(name)
            ? rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : name;
    }

    private static string NameOf(string relativePath)
    {
        var index = relativePath.LastIndexOf('/');
        return index >= 0 ? relativePath.Substring(index + 1) : relativePath;
    }

    private static void AddOnce<T>(List<T> list, T value)
    {
        if (!list.Contains(value))
        {
            list.Add(value);
        }
    }
}