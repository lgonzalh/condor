using System;
using System.Collections.Generic;

namespace Condor.Core.Models;

public class SetupResult
{
    public string SchemaVersion { get; set; } = "1.0.0";
    public DetectionStatus Status { get; set; } = DetectionStatus.Detected;
    public string? Reason { get; set; }
    public string Platform { get; set; } = "windows";
    public int RequiredPresent { get; set; }
    public int RequiredTotal { get; set; }
    public int OptionalPresent { get; set; }
    public int OptionalTotal { get; set; }
    public List<SetupDependency> Dependencies { get; set; } = new();
    public string StateDirectory { get; set; } = "";
    public bool StateUsable { get; set; }
    public string? StateReason { get; set; }
    public List<string> LimitsApplied { get; set; } = new();
    public DateTime GeneratedAtUtc { get; set; }
}
