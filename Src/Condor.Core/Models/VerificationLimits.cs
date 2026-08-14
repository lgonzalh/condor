namespace Condor.Core.Models;

public sealed class VerificationLimits
{
    public const string LimitChecks = "verification-checks";
    public const string LimitContent = "verification-content";
    public const string LimitTimeout = "verification-timeout";

    public int MaxChecks { get; init; } = 24;
    public int MaxContentLength { get; init; } = 64_000;
    public int VerifyTimeoutMilliseconds { get; init; } = 15_000;

    public static VerificationLimits Default { get; } = new();
}
