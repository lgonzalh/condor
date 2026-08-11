namespace Condor.Core.Evaluation;

public static class ModelMemoryBudget
{
    // Estimacion conservadora inicial (pesos + cache de contexto).
    // NO es consumo real medido de inferencia; aislada para calibrar en el futuro.
    // Calibracion con el equipo real de referencia: 7B Q4 (4,36 GB, pico 5,2 GB)
    // es viable con ~6 GB libres; un 8B Q4 (4,87 GB, pico 5,8 GB) queda al limite.
    public const double PeakEstimateFactor = 1.2;
    public const double ReserveGb = 0.75;
    public const double MaxTotalRatio = 0.45;
    public const double DiskSafetyRatio = 0.5;
    public const double BytesPerGb = 1024 * 1024 * 1024;

    public static double EstimatePeakBytes(long sizeBytes) => sizeBytes * PeakEstimateFactor;

    public static double AvailableBudgetGb(double ramTotalGb, double ramFreeGb)
    {
        var freeBudget = ramFreeGb - ReserveGb;
        var totalCap = ramTotalGb * MaxTotalRatio;
        return Math.Max(0, Math.Max(freeBudget, totalCap));
    }

    public static bool FitsInRam(long sizeBytes, double ramTotalGb, double ramFreeGb)
    {
        return EstimatePeakBytes(sizeBytes) / BytesPerGb <= AvailableBudgetGb(ramTotalGb, ramFreeGb);
    }

    public static bool FitsInDisk(long sizeBytes, long freeDiskBytes)
    {
        return sizeBytes <= freeDiskBytes * DiskSafetyRatio;
    }
}