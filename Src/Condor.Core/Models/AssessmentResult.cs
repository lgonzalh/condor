namespace Condor.Core.Models;

public class AssessmentResult
{
    public string SchemaVersion { get; set; } = "1.0.0";
    public DateTime GeneratedAtUtc { get; set; }
    public string WorkingDirectory { get; set; } = "";
    public EnvironmentProfile Environment { get; set; } = new();
    public ToolsProfile Tools { get; set; } = new();
    public CapabilitiesSummary Capabilities { get; set; } = new();
    public ProjectProfile? Project { get; set; }
}