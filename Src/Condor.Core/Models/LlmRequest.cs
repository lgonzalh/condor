using System.Collections.Generic;

namespace Condor.Core.Models;

public class LlmRequest
{
    public string Model { get; set; } = "";
    public string Prompt { get; set; } = "";
    public double Temperature { get; set; } = 0.7;
    public int? MaxTokens { get; set; }
    public List<string>? Images { get; set; }
    public List<LlmMessage>? Messages { get; set; }
    public List<LlmTool>? Tools { get; set; }
}
