namespace Condor.Core.Models;

public class ModelInfo
{
    public string Name { get; set; } = "";
    public long SizeBytes { get; set; }
    public string? Family { get; set; }
    public string? ParameterSize { get; set; }
    public string? Quantization { get; set; }
    public long? ContextLength { get; set; }
    public List<string> Capabilities { get; set; } = new();
}
