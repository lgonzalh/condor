using System;
using System.Collections.Generic;

namespace Condor.Core.Models;

public class CycleResult
{
    public string SchemaVersion { get; set; } = "1.0.0";
    public DetectionStatus Status { get; set; } = DetectionStatus.Detected;
    public string? Reason { get; set; }
    public string RootName { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public string Intention { get; set; } = "";
    public string Objective { get; set; } = "";
    public int Iterations { get; set; }
    public int Stages { get; set; }
    public int Applied { get; set; }
    public int Verified { get; set; }
    public CycleCheckpoint Checkpoint { get; set; } = new();
    public List<string> LimitsApplied { get; set; } = new();
    public DateTime GeneratedAtUtc { get; set; }
}
