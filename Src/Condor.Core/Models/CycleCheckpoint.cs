using System;

namespace Condor.Core.Models;

public class CycleCheckpoint
{
    public string SchemaVersion { get; set; } = "1.0.0";
    public string CycleId { get; set; } = "";
    public int Iteration { get; set; }
    public CycleStage Stage { get; set; }
    public string? StageResult { get; set; }
    public string? StatusCycle { get; set; }
    public string RecoveryState { get; set; } = "";
    public string NextAction { get; set; } = "";
    public DateTime GeneratedAtUtc { get; set; }
}
