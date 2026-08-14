namespace Condor.Core.Models;

public class VerificationCheck
{
    public const string StatusPassed = "pasada";
    public const string StatusFailed = "fallida";
    public const string StatusInformative = "informativa";

    public string Id { get; set; } = "";
    public string BuildActionId { get; set; } = "";
    public BuildActionKind Kind { get; set; }
    public string RelativePath { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Reason { get; set; }
    public string Evidence { get; set; } = "";
}
