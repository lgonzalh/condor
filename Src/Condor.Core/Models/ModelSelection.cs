using System.Collections.Generic;

namespace Condor.Core.Models;

public class ModelCandidate
{
    public string Name { get; set; } = "";
    public string PullName { get; set; } = "";
    public long SizeBytes { get; set; }
    public string? Family { get; set; }
    public string? ParameterSize { get; set; }
    public string? Quantization { get; set; }
    public List<string> Capabilities { get; set; } = new();
}

public class ModelSelectionResult
{
    public ModelCandidate? Desired { get; set; }
    public bool AlreadyInstalled { get; set; }
    public string? InstalledName { get; set; }
    public string? Reason { get; set; }
    public List<string> Alternatives { get; set; } = new();
    public List<string> Limitations { get; set; } = new();
}
