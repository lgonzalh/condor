namespace Condor.Core.Models;

public sealed class AgentPlanItem
{
    public string? Task { get; set; }
    public string? ExpectedResult { get; set; }
    public string? VerificationStep { get; set; }
}
