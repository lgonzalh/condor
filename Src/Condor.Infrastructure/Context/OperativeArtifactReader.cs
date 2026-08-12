using Condor.Core.Context;
using Condor.Core.Models;

namespace Condor.Infrastructure.Context;

public sealed class OperativeArtifactReader
{
    private const string OperativeDirectoryName = "operacion";

    private readonly ContextLimits _limits;

    public OperativeArtifactReader(ContextLimits? limits = null)
    {
        _limits = limits ?? ContextLimits.Default;
    }

    public async Task<IReadOnlyList<OperativeArtifact>> ReadAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        var artifacts = new List<OperativeArtifact>();

        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            return artifacts;
        }

        foreach (var kind in OperativeArtifactCatalog.Order)
        {
            artifacts.Add(await ReadArtifactAsync(kind, workingDirectory, cancellationToken));
        }

        return artifacts
            .Where(artifact => artifact.Status != DetectionStatus.NotDetected)
            .ToList();
    }

    private async Task<OperativeArtifact> ReadArtifactAsync(
        OperativeArtifactKind kind,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var fileName = OperativeArtifactCatalog.FileName(kind);
        var filePath = Path.Combine(
            workingDirectory,
            OperativeDirectoryName,
            fileName);

        if (!File.Exists(filePath))
        {
            return new OperativeArtifact
            {
                Kind = kind,
                RelativePath = OperativeDirectoryName + "/" + fileName,
                Status = DetectionStatus.NotDetected
            };
        }

        long sizeBytes;
        try
        {
            sizeBytes = new FileInfo(filePath).Length;
        }
        catch
        {
            return Skipped(kind, fileName, "acceso denegado");
        }

        if (sizeBytes > _limits.MaxArtifactBytes)
        {
            return Skipped(kind, fileName, "supera el limite de tamano");
        }

        try
        {
            var content = await File.ReadAllTextAsync(filePath, cancellationToken);

            return new OperativeArtifact
            {
                Kind = kind,
                RelativePath = OperativeDirectoryName + "/" + fileName,
                Content = content,
                SizeBytes = sizeBytes,
                Status = DetectionStatus.Detected
            };
        }
        catch
        {
            return Skipped(kind, fileName, "acceso denegado");
        }
    }

    private static OperativeArtifact Skipped(
        OperativeArtifactKind kind,
        string fileName,
        string reason)
    {
        return new OperativeArtifact
        {
            Kind = kind,
            RelativePath = OperativeDirectoryName + "/" + fileName,
            Status = DetectionStatus.Limited,
            Reason = reason
        };
    }
}