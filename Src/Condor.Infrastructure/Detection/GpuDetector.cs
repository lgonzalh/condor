using System.Text.Json;
using Condor.Core.Models;
using Condor.Infrastructure.Probing;

namespace Condor.Infrastructure.Detection;

public class GpuDetector
{
    public async Task<GpuDetectionResult> DetectAsync(CancellationToken cancellationToken = default)
    {
        var result = new GpuDetectionResult
        {
            Status = DetectionStatus.NotDetected,
            Reason = "GPU no detectable"
        };

        var json = await CimProbe.QueryAsync(
            "Get-CimInstance Win32_VideoController | Select-Object Name,AdapterRAM,DriverVersion",
            cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(json))
        {
            result.Reason = "No fue posible consultar Win32_VideoController";
            return result;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var gpus = new List<GpuInfo>();
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    var gpu = ParseGpu(item);
                    if (gpu is not null)
                    {
                        gpus.Add(gpu);
                    }
                }
            }
            else
            {
                var gpu = ParseGpu(root);
                if (gpu is not null)
                {
                    gpus.Add(gpu);
                }
            }

            if (gpus.Count > 0)
            {
                result.Gpus = gpus;
                result.Status = DetectionStatus.Detected;
                result.Reason = null;
            }
            else
            {
                result.Reason = "No se detectaron controladores de video";
            }
        }
        catch
        {
            result.Reason = "Respuesta CIM no interpretable para la GPU";
        }

        return result;
    }

    private static GpuInfo? ParseGpu(JsonElement element)
    {
        var name = OsDetector.ReadString(element, "Name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return new GpuInfo
        {
            Name = name,
            VramBytes = ReadLong(element, "AdapterRAM"),
            DriverVersion = OsDetector.ReadString(element, "DriverVersion") ?? ""
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

public class GpuDetectionResult
{
    public DetectionStatus Status { get; set; } = DetectionStatus.NotDetected;
    public string? Reason { get; set; }
    public List<GpuInfo> Gpus { get; set; } = new();
}
