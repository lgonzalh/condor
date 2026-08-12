using System;
using System.Collections.Generic;

namespace Condor.Core.Models
{
    public class ProjectContext
    {
        public string SchemaVersion { get; set; } = "1.0.0";
        public DetectionStatus Status { get; set; } = DetectionStatus.Detected;
        public string? Reason { get; set; }
        public string WorkingDirectory { get; set; } = "";
        public string RootName { get; set; } = "";
        public ProjectContextSummary Summary { get; set; } = new();
        public ContinuationPoint? ContinuationPoint { get; set; }
        public List<ContextRisk> Risks { get; set; } = new();
        public List<RelevantDependency> RelevantDependencies { get; set; } = new();
        public List<PlannerRecommendation> Recommendations { get; set; } = new();
        public List<string> LimitsApplied { get; set; } = new();
        public DateTime GeneratedAtUtc { get; set; }
    }
}
