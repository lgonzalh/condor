using System;
using System.Collections.Generic;

namespace Condor.Core.Models
{
    public class WorkPlan
    {
        public string SchemaVersion { get; set; } = "1.0.0";
        public DetectionStatus Status { get; set; } = DetectionStatus.Detected;
        public string? Reason { get; set; }
        public string RootName { get; set; } = "";
        public string WorkingDirectory { get; set; } = "";
        public string Intention { get; set; } = "indefinida";
        public string Objective { get; set; } = "";
        public List<PlanTask> Tasks { get; set; } = new();
        public List<string> Evidence { get; set; } = new();
        public List<string> RisksConsidered { get; set; } = new();
        public List<string> LimitsApplied { get; set; } = new();
        public DateTime GeneratedAtUtc { get; set; }
    }
}
