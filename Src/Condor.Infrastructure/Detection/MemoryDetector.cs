using System.Runtime.InteropServices;
using System.Text.Json;
using Condor.Core.Models;
using Condor.Infrastructure.Probing;

namespace Condor.Infrastructure.Detection;

public class MemoryDetector
{
    public async Task<MemoryInfo> DetectAsync(CancellationToken cancellationToken = default)
    {
        // La RAM LIBRE pura (base segura del presupuesto) se lee de CIM.
        var cim = await DetectViaCimAsync(cancellationToken);
        if (cim.Status != DetectionStatus.Detected)
        {
            return cim;
        }

        // La RAM disponible se lee de GlobalMemoryStatusEx. Como esa fuente ya
        // incluye el standby limpiable, el disponible NUNCA puede ser menor que la
        // RAM libre pura (de CIM). Si las dos fuentes divergen en el instante de
        // muestreo, se protege el invariante available >= free y la cache/standby
        // se deriva como disponible - libre (solo informativa; jamas RAM garantizada).
        var (availableBytes, _) = ReadAvailableAndCache();
        var available = availableBytes ?? cim.FreeBytes;
        if (available < cim.FreeBytes)
        {
            available = cim.FreeBytes;
        }

        cim.AvailableBytes = available;
        cim.CacheBytes = available - cim.FreeBytes;
        return cim;
    }

    private static (long? Available, long? Cache) ReadAvailableAndCache()
    {
        try
        {
            var status = new MemoryStatusEx();
            if (!GlobalMemoryStatusEx(status))
            {
                return (null, null);
            }

            var availableBytes = (long)status.ullAvailPhys;
            return (availableBytes, null); // cache calculada por el llamador con la libre pura.
        }
        catch
        {
            return (null, null);
        }
    }

    private static async Task<MemoryInfo> DetectViaCimAsync(CancellationToken cancellationToken)
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
                var totalBytes = totalKb * 1024;
                var freeBytes = freeKb * 1024;

                memory.TotalBytes = totalBytes;
                memory.FreeBytes = freeBytes;
                memory.AvailableBytes = freeBytes; // tope informativo; cache=0 por defecto
                memory.CacheBytes = 0;
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MemoryStatusEx
    {
        public uint dwLength = 64;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx lpBuffer);
}
