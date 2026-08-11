namespace Condor.Core.Models;

public class LlmRequest
{
    public string Model { get; set; } = "";
    public string Prompt { get; set; } = "";
    public double Temperature { get; set; } = 0.7;
    public int? MaxTokens { get; set; }
}
