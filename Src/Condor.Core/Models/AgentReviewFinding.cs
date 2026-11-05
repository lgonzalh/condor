namespace Condor.Core.Models;

/// <summary>Hallazgo verificable emitido por un perfil de revisión.</summary>
public sealed class AgentReviewFinding
{
    public string Path { get; set; } = "";
    public int Line { get; set; }
    public string Severity { get; set; } = "medium";
    public string Explanation { get; set; } = "";
    public string Evidence { get; set; } = "";
    public string Reproduction { get; set; } = "";
    public string Origin { get; set; } = "uncertain";
    public string Status { get; set; } = "open";
    public string? ClosureReason { get; set; }
}
