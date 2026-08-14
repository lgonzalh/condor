namespace Condor.Core.Models;

public class BuildAction
{
    public const string StatusApplied = "aplicada";
    public const string StatusOmitted = "omitida";
    public const string StatusFailed = "fallida";

    public string Id { get; set; } = "";
    public BuildActionKind Kind { get; set; }
    public string RelativePath { get; set; } = "";
    public string Content { get; set; } = "";
    public string Evidence { get; set; } = "";
    public string? Status { get; set; }
    public string? StatusReason { get; set; }
}
