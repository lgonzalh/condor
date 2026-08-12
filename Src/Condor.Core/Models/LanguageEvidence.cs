namespace Condor.Core.Models;

public class LanguageEvidence
{
    public string Name { get; set; } = "";
    public bool Primary { get; set; }
    public List<EvidenceSignal> Signals { get; set; } = new();
}