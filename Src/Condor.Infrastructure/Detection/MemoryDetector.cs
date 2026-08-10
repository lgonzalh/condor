using System.Text.Json;
using Condor.Core.Models;
using Condor.Infrastructure.Probing;

namespace Condor.Infrastructure.Detection;

public class MemoryDetector
{
    public async Task<MemoryInfo> DetectAsync(CancellationToken cancellationToken = default)
    {
        var memory = new MemoryInfo
        {
            Status = DetectionStatus.NotDetected,
            Reason = "RAM no detectable"
        };

        var json = await CimProbe.QueryAsync(
            "Get-CimInstance Win32_OperatingSystem | Select-Object TotalVisibleMemorySize,FreePhysicalMemory",
            cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(json))
        {
            memory.Reason = "No fue posible consultar la memoria del sistema";
            return memory;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var totalKb = ReadLong(root, "TotalVisibleMemorySize");
            var freeKb = ReadLong(root, "FreePhysicalMemory");

            if (totalKb > 0)
            {
                memory.TotalBytes = totalKb * 1024;
                memory.FreeBytes = freeKb * 1024;
                memory.Status = DetectionStatus.Detected;
                memory.Reason = null;
            }
        }
        catch
        {
            memory.Reason = "Respuesta CIM no interpretable para la memoria";
        }

        return memory;
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
