namespace Condor.Core.Models;

public class StorageInfo
{
    public string Drive { get; set; } = "";
    public string VolumeName { get; set; } = "";
    public string FileSystem { get; set; } = "";
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
}
