namespace Condor.Core.Models;

public class OllamaStatus
{
    public bool Installed { get; set; }
    public bool ServerRunning { get; set; }
    public string? ServerVersion { get; set; }
    public List<ModelInfo> Models { get; set; } = new();
    public string? Note { get; set; }
}
