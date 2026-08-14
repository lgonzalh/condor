namespace Condor.Core.Models;

public sealed class VisionLimits
{
    public const string LimitImageSize = "vision-image-size";
    public const string LimitDescription = "vision-description";
    public const string LimitTimeout = "vision-timeout";

    public long MaxImageBytes { get; init; } = 10 * 1024 * 1024;
    public int MaxDescriptionLength { get; init; } = 4000;
    public int VisionTimeoutMilliseconds { get; init; } = 60_000;

    public static VisionLimits Default { get; } = new();
}
