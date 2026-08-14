using System;
using System.Collections.Generic;

namespace Condor.Core.Models;

public class BuildResult
{
    public string SchemaVersion { get; set; } = "1.0.0";
    public DetectionStatus Status { get; set; } = DetectionStatus.Detected;
    public string? Reason { get; set; }
    public string RootName { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public string Intention { get; set; } = "indefinida";
    public string Objective { get; set; } = "";
    public List<BuildAction> Actions { get; set; } = new();
    public int Applied { get; set; }
    public int Omitted { get; set; }
    public int Failed { get; set; }
    public List<string> LimitsApplied { get; set; } = new();
    public DateTime GeneratedAtUtc { get; set; }
}
