using Condor.Core.Models;
using System;

namespace Condor.Core.Context
{
    public sealed class ContextLimits
    {
        public const string LimitArtifactSize = "artifact-size";
        public const string LimitArtifacts = "max-artifacts";
        public const string LimitLines = "max-lines";
        public const string LimitPendingTasks = "pending-tasks";
        public const string LimitRecommendations = "recommendations";
        public const string LimitTimeout = "timeout-context";

        public int MaxArtifactBytes { get; init; } = 64 * 1024;
        public int MaxArtifacts { get; init; } = 5;
        public int MaxScannedLinesPerArtifact { get; init; } = 400;
        public int MaxPendingTasks { get; init; } = 10;
        public int MaxRecommendations { get; init; } = 8;
        public int ContextTimeoutMilliseconds { get; init; } = 15_000;

        public static ContextLimits Default { get; } = new();
    }
}
