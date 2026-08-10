namespace Condor.Core.Models;

public class ToolsProfile
{
    public ToolInfo Git { get; set; } = new();
    public OllamaStatus Ollama { get; set; } = new();
    public List<ToolInfo> DetectedTools { get; set; } = new();
}
