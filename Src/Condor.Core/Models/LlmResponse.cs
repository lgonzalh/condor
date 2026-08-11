namespace Condor.Core.Models;

public class LlmResponse
{
    public bool Success { get; set; }
    public string? Content { get; set; }
    public string? Model { get; set; }
    public string? Error { get; set; }
}
