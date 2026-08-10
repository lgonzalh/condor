using System.Text.Json;
using Condor.Core.Models;
using Condor.Infrastructure.Probing;

namespace Condor.Infrastructure.Detection;

public class StorageDetector
{
    public async Task<StorageDetectionResult> DetectAsync(CancellationToken cancellationToken = default)
    {
        var result = new StorageDetectionResult
        {
            Status = DetectionStatus.NotDetected,
            Reason = "Almacenamiento no detectable"
        };

        var json = await CimProbe.QueryAsync(
            "Get-CimInstance Win32_LogicalDisk -Filter \"DriveType=3\" | Select-Object DeviceID,VolumeName,FileSystem,Size,FreeSpace",
            cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(json))
        {
            result.Reason = "No fue posible consultar Win32_LogicalDisk";
            return result;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var disks = new List<StorageInfo>();
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    var disk = ParseDisk(item);
                    if (disk is not null)
                    {
                        disks.Add(disk);
                    }
                }
            }
            else
            {
                var disk = ParseDisk(root);
                if (disk is not null)
                {
                    disks.Add(disk);
                }
            }

            if (disks.Count > 0)
            {
                result.Disks = disks;
                result.Status = DetectionStatus.Detected;
                result.Reason = null;
            }
        }
        catch
        {
            result.Reason = "Respuesta CIM no interpretable para el almacenamiento";
        }

        return result;
    }

    private static StorageInfo? ParseDisk(JsonElement element)
    {
        var deviceId = OsDetector.ReadString(element, "DeviceID");
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        return new StorageInfo
        {
            Drive = deviceId,
            VolumeName = OsDetector.ReadString(element, "VolumeName") ?? "",
            FileSystem = OsDetector.ReadString(element, "FileSystem") ?? "",
            TotalBytes = ReadLong(element, "Size"),
            FreeBytes = ReadLong(element, "FreeSpace")
        };
    }

    private static long ReadLong(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value))
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return 0;
    }
}

public class StorageDetectionResult
{
    public DetectionStatus Status { get; set; } = DetectionStatus.NotDetected;
    public string? Reason { get; set; }
    public List<StorageInfo> Disks { get; set; } = new();
}
