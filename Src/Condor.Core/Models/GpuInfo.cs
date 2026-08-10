namespace Condor.Core.Models;

public class GpuInfo
{
    public string Name { get; set; } = "";
    public long VramBytes { get; set; }
    public string DriverVersion { get; set; } = "";
}
