namespace Condor.Core.Evaluation;

// Presupuesto de recursos para cargar un modelo LLM local.
// El presupuesto NUNCA puede superar la RAM libre real: se calcula como
//   ramFree - margenOperativo
// donde margenOperativo cubre el sistema operativo, el servidor Ollama,
// el runtime de Condor y un colchon anti-swapping. No se usa un porcentaje
// de la RAM total cuando la RAM libre es menor.
public static class ModelMemoryBudget
{
    public const double BytesPerGb = 1024.0 * 1024 * 1024;

    // Pico estimado de memoria de un modelo (peso + overhead de carga/KV).
    // Factor aplicado al peso en disco. Calibrado: 7B Q4 (4.36 GB) tiene pico ~5.2.
    public const double PeakFactor = 1.2;

    // Margen operativo: minimo de RAM que debe quedar para que Windows,
    // Ollama y Condor + build/test operen de forma estable (anti-swapping).
    public static double OperatingMarginGb(double ramTotalGb)
    {
        var ratio = ramTotalGb * 0.08;
        return System.Math.Min(3.0, System.Math.Max(1.5, ratio));
    }

    public static double EstimatePeakGb(double weightGb, double contextKbGb)
    {
        return (weightGb * PeakFactor) + contextKbGb;
    }

    // Presupuesto seguro de carga = RAM libre real - margen operativo.
    public static double SafeBudgetGb(double ramTotalGb, double ramFreeGb)
    {
        var margin = OperatingMarginGb(ramTotalGb);
        var budget = ramFreeGb - margin;
        return System.Math.Max(0, budget);
    }

    public static bool FitsInRam(double weightGb, double contextKbGb, double ramTotalGb, double ramFreeGb)
    {
        var safe = SafeBudgetGb(ramTotalGb, ramFreeGb);
        var peak = EstimatePeakGb(weightGb, contextKbGb);
        return peak <= safe;
    }

    // Disco: espacio libre - reserva de trabajo - margen anti-saturacion.
    public static bool FitsInDisk(double weightGb, double workReserveGb, double freeDiskGb)
    {
        var safetyReserve = freeDiskGb * 0.10;
        var margin = workReserveGb + safetyReserve;
        return weightGb + margin <= freeDiskGb;
    }
}
