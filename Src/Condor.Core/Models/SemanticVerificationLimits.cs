namespace Condor.Core.Models;

public sealed class SemanticVerificationLimits
{
    public const string LimitTimeout = "semantic-timeout";
    public const string LimitOutput = "semantic-output";
    public const string LimitChecks = "semantic-checks";

    public int ProcessTimeoutMilliseconds { get; init; } = 60_000;
    public int MaxOutputLength { get; init; } = 8_000;
    public int MaxChecks { get; init; } = 2;

    public static SemanticVerificationLimits Default { get; } = new();
}
