using System.Runtime.InteropServices;
using System.Text.Json;
using Condor.Core.Models;
using Condor.Infrastructure.Probing;

namespace Condor.Infrastructure.Detection;

public class OsDetector
{
    public async Task<OperatingSystemInfo> DetectAsync(CancellationToken cancellationToken = default)
    {
        var info = new OperatingSystemInfo
        {
            Status = DetectionStatus.Detected,
            Name = RuntimeInformation.OSDescription,
            Version = Environment.OSVersion.Version.ToString(),
            Architecture = RuntimeInformation.OSArchitecture.ToString()
        };

        var json = await CimProbe.QueryAsync(
            "Get-CimInstance Win32_OperatingSystem | Select-Object Caption,Version,BuildNumber,OSArchitecture",
            cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(json))
        {
            info.Reason = "No fue posible consultar Win32_OperatingSystem";
            return info;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var caption = ReadString(root, "Caption");
            if (!string.IsNullOrWhiteSpace(caption))
            {
                info.Name = caption;
            }

            var version = ReadString(root, "Version");
            if (!string.IsNullOrWhiteSpace(version))
            {
                info.Version = version;
            }

            var build = ReadString(root, "BuildNumber");
            if (!string.IsNullOrWhiteSpace(build))
            {
                info.Build = build;
            }

            var architecture = ReadString(root, "OSArchitecture");
            if (!string.IsNullOrWhiteSpace(architecture))
            {
                info.Architecture = architecture;
            }
        }
        catch
        {
            info.Reason = "Respuesta CIM no interpretable para el sistema operativo";
        }

        return info;
    }

    internal static string? ReadString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }
}
