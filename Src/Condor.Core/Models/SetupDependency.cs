namespace Condor.Core.Models;

public class SetupDependency
{
    public string Name { get; set; } = "";
    public bool IsRequired { get; set; }
    public bool Present { get; set; }
    public string? Reason { get; set; }
    public string Guidance { get; set; } = "";
}
