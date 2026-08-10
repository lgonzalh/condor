using System.Text.Json;
using Condor.Core.Models;
using Condor.Infrastructure.Probing;

namespace Condor.Infrastructure.Detection;

public class CpuDetector
{
    public async Task<ProcessorInfo> DetectAsync(CancellationToken cancellationToken = default)
    {
        var cpu = new ProcessorInfo
        {
            Status = DetectionStatus.NotDetected,
            Reason = "CPU no detectable"
        };

        var json = await CimProbe.QueryAsync(
            "Get-CimInstance Win32_Processor | Select-Object -First 1 Name,NumberOfCores,NumberOfLogicalProcessors,MaxClockSpeed,Architecture",
            cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(json))
        {
            cpu.Reason = "No fue posible consultar Win32_Processor";
            return cpu;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var name = OsDetector.ReadString(root, "Name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                cpu.Name = name;
            }

            cpu.Cores = ReadInt(root, "NumberOfCores");
            cpu.LogicalProcessors = ReadInt(root, "NumberOfLogicalProcessors");
            cpu.MaxClockMhz = ReadDouble(root, "MaxClockSpeed");

            if (cpu.Cores > 0 || cpu.LogicalProcessors > 0 || !string.IsNullOrWhiteSpace(cpu.Name))
            {
                cpu.Status = DetectionStatus.Detected;
                cpu.Reason = null;
            }
        }
        catch
        {
            cpu.Reason = "Respuesta CIM no interpretable para la CPU";
        }

        return cpu;
    }

    private static int ReadInt(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value))
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return 0;
    }

    private static double ReadDouble(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value))
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return 0;
    }
}
