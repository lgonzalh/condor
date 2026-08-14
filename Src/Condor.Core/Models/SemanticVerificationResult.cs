using System;
using System.Collections.Generic;

namespace Condor.Core.Models;

public class SemanticVerificationResult
{
    public string SchemaVersion { get; set; } = "1.0.0";
    public DetectionStatus Status { get; set; } = DetectionStatus.Detected;
    public string? Reason { get; set; }
    public string RootName { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public List<SemanticCheck> Checks { get; set; } = new();
    public List<string> LimitsApplied { get; set; } = new();
    public DateTime GeneratedAtUtc { get; set; }
}
