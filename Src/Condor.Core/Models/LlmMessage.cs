using System.Collections.Generic;

namespace Condor.Core.Models;

public class LlmMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public string? ToolCallId { get; set; }
    public List<ToolCall>? ToolCalls { get; set; }
    public string? Name { get; set; }
}
