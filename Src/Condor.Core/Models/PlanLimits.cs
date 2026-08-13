namespace Condor.Core.Models
{
    public sealed class PlanLimits
    {
        public const string LimitTasks = "plan-tasks";
        public const string LimitObjective = "plan-objective";
        public const string LimitDetail = "plan-detail";
        public const string LimitEvidence = "plan-evidence";
        public const string LimitTimeout = "plan-timeout";

        public int MaxTasks { get; init; } = 12;
        public int MaxObjectiveLength { get; init; } = 240;
        public int MaxTaskDetailLength { get; init; } = 320;
        public int MaxEvidenceItems { get; init; } = 30;
        public int PlanTimeoutMilliseconds { get; init; } = 15_000;

        public static PlanLimits Default { get; } = new();
    }
}
