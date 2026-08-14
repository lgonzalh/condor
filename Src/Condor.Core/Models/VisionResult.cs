using System;
using System.Collections.Generic;

namespace Condor.Core.Models;

public class VisionResult
{
    public string SchemaVersion { get; set; } = "1.0.0";
    public DetectionStatus Status { get; set; } = DetectionStatus.Detected;
    public string? Reason { get; set; }
    public string ImagePath { get; set; } = "";
    public long ImageBytes { get; set; }
    public string ModelUsed { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> LimitsApplied { get; set; } = new();
    public DateTime GeneratedAtUtc { get; set; }
}
