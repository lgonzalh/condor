namespace Condor.Core.Models;

public class EvidenceSignal
{
    public EvidenceKind Kind { get; set; }
    public string Value { get; set; } = "";
    public int? Count { get; set; }
}