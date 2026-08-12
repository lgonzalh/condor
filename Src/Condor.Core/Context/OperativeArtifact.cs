using Condor.Core.Models;

namespace Condor.Core.Context;

public sealed class OperativeArtifact
{
    public OperativeArtifactKind Kind { get; init; }
    public string RelativePath { get; init; } = "";
    public string Content { get; init; } = "";
    public long SizeBytes { get; init; }
    public DetectionStatus Status { get; init; } = DetectionStatus.Detected;
    public string? Reason { get; init; }
}