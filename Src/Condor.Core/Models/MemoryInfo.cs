namespace Condor.Core.Models;

/// <summary>
/// Instantanea de la memoria del sistema con un desglose explicito y auditable.
/// La memoria en cache (standby reutilizable) NO se trata como RAM libre
/// garantizada: se informa por separado, pero el presupuesto seguro usa la RAM
/// realmente libre.
/// </summary>
public class MemoryInfo
{
    public long TotalBytes { get; set; }

    /// <summary>RAM libre fisica (no incluye la cache/standby). Metrica base del presupuesto.</summary>
    public long FreeBytes { get; set; }

    /// <summary>RAM disponible real (libre + standBy limpiable), informativa. NO es garantia de presupuesto.</summary>
    public long AvailableBytes { get; set; }

    /// <summary>Cache/standby reutilizable. SOLO informativo; nunca se cuenta como RAM libre garantizada.</summary>
    public long CacheBytes { get; set; }

    public DetectionStatus Status { get; set; } = DetectionStatus.NotDetected;
    public string? Reason { get; set; }

    public double TotalGb => Math.Round(TotalBytes / 1024.0 / 1024.0 / 1024.0, 1);
    public double FreeGb => Math.Round(FreeBytes / 1024.0 / 1024.0 / 1024.0, 1);
    public double AvailableGb => Math.Round(AvailableBytes / 1024.0 / 1024.0 / 1024.0, 1);
    public double CacheGb => Math.Round(CacheBytes / 1024.0 / 1024.0 / 1024.0, 1);
}
