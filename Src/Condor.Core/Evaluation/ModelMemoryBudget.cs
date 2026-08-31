namespace Condor.Core.Evaluation;

using Condor.Core.Models;

// Presupuesto de recursos para cargar un modelo LLM local, con desglose
// explicito y conservador. La RAM en cache (standby) NUNCA se trata como RAM
// libre garantizada: el presupuesto seguro se basa en la RAM libre real menos
// las reservas del sistema, de Condor y un margen anti-swapping.
public static class ModelMemoryBudget
{
    public const double BytesPerGb = 1024.0 * 1024 * 1024;

    // Pico estimado de memoria de un modelo (peso + overhead de carga/KV).
    // Factor calibrado: 7B Q4 (4.36 GB) tiene pico ~5.2.
    public const double PeakFactor = 1.2;

    /// <summary>Reserva minima del sistema (SO), en GB.</summary>
    public const double SystemReserveGb = 1.5;

    /// <summary>Reserva de Condor + runtime + build/test, en GB.</summary>
    public const double CondorReserveGb = 1.5;

    // Margen operativo minimo para estabilidad (anti-swapping).
    public static double OperatingMarginGb(double ramTotalGb)
    {
        var ratio = ramTotalGb * 0.08;
        return System.Math.Min(3.0, System.Math.Max(1.5, ratio));
    }

    public static double EstimatePeakGb(double weightGb, double contextKbGb)
    {
        return (weightGb * PeakFactor) + contextKbGb;
    }

    /// <summary>Headroom (RAM libre real - reservas y margen). Base del presupuesto seguro.</summary>
    public static double HeadroomGb(double ramFreeGb, double systemReserveGb, double condorReserveGb, double safetyMarginGb)
    {
        var available = ramFreeGb - systemReserveGb - condorReserveGb - safetyMarginGb;
        return System.Math.Max(0, available); // solo evita negativos; no sobreestima.
    }

    /// <summary>Presupuesto seguro de carga = headroom. NUNCA supera la RAM libre real.</summary>
    public static double SafeBudgetGb(double ramTotalGb, double ramFreeGb)
    {
        var margin = OperatingMarginGb(ramTotalGb);
        return System.Math.Max(0, ramFreeGb - margin);
    }

    public static bool FitsInRam(double weightGb, double contextKbGb, double ramTotalGb, double ramFreeGb)
    {
        var safe = SafeBudgetGb(ramTotalGb, ramFreeGb);
        var peak = EstimatePeakGb(weightGb, contextKbGb);
        return peak < safe;
    }

    /// <summary>
    /// Cumple las DOS condiciones de carga definitivas: porcentaje de RAM total
    /// permitido (RAM total = 100%) Y presupuesto seguro (RAM libre real menos
    /// reservaSO, reservaCondor y margen). Cache no cuenta como RAM garantizada.
    /// ESTRICTO: peak < headroom (no <=).
    /// </summary>
    public static bool FitsInRamStrict(double weightGb, double contextKbGb, double ramTotalGb, double ramFreeGb)
    {
        var peak = EstimatePeakGb(weightGb, contextKbGb);
        var headroom = HeadroomGb(ramFreeGb, SystemReserveGb, CondorReserveGb, OperatingMarginGb(ramTotalGb));

        // Condicion 1: porcentaje de RAM total permitido
        var byRatio = ClassifyByRatio(peak, ramTotalGb);
        if (byRatio == ResourcePressure.Insufficient)
            return false;

        // Condicion 2: presupuesto seguro ESTRICTO (peak < headroom)
        return peak < headroom;
    }

    // Disco: espacio libre - reserva de trabajo - margen anti-saturacion.
    public static bool FitsInDisk(double weightGb, double workReserveGb, double freeDiskGb)
    {
        var safetyReserve = freeDiskGb * 0.10;
        var margin = workReserveGb + safetyReserve;
        return weightGb + margin <= freeDiskGb;
    }

    // Umbrales de clasificacion sobre la RAM TOTAL (usada como 100%).
    // El coste del modelo (pico estimado) como porcentaje de la RAM total decide
    // el estado: Normal <=30%, Ajustado <=35%, Presion <=40%, Insuficiente >40%.
    public const double NormalMaxRatio = 0.30;
    public const double AdjustedMaxRatio = 0.35;
    public const double PressureMaxRatio = 0.40;

    /// <summary>
    /// Estado por porcentaje: coste del candidato (peak) / RAM total como 100%.
    /// Este veredicto es INTRINSECO a la RAM fisica; no depende de cuanta esté libre.
    /// </summary>
    public static ResourcePressure ClassifyByRatio(double candidatePeakGb, double totalGb)
    {
        if (totalGb <= 0)
            return ResourcePressure.Insufficient;

        var ratio = candidatePeakGb / totalGb;
        if (ratio <= NormalMaxRatio) return ResourcePressure.Normal;
        if (ratio <= AdjustedMaxRatio) return ResourcePressure.Adjusted;
        if (ratio <= PressureMaxRatio) return ResourcePressure.Pressure;
        return ResourcePressure.Insufficient;
    }

    /// <summary>True si el candidato cabe dentro del presupuesto seguro (headroom) - ESTRICTO.</summary>
    public static bool FitsSafeBudget(double candidatePeakGb, double headroomGb)
        => headroomGb > candidatePeakGb;

    /// <summary>
    /// Veredicto final de presion. El candidato DEBE cumplir AMBAS condiciones:
    /// porcentaje de RAM total permitido Y presupuesto seguro. La cache NO cuenta
    /// como RAM garantizada. Si no cumple el presupuesto seguro, se trata como
    /// Insuficiente (no se carga ni se reintenta en bucle), aunque su porcentaje
    /// estuviera dentro del rango permitido.
    /// </summary>
    public static ResourcePressure ClassifyCandidate(double candidatePeakGb, double totalGb, double headroomGb)
    {
        var byRatio = ClassifyByRatio(candidatePeakGb, totalGb);
        if (byRatio == ResourcePressure.Insufficient)
            return ResourcePressure.Insufficient;

        return FitsSafeBudget(candidatePeakGb, headroomGb) ? byRatio : ResourcePressure.Insufficient;
    }

    /// <summary>
    /// Estado del sistema cuando NO hay candidato (p. ej. evaluacion de recursos
    /// durante la ejecucion del agente). Se clasifica por el headroom disponible.
    /// </summary>
    public static ResourcePressure ClassifySystemState(double headroomGb)
        => headroomGb > 2.0 ? ResourcePressure.Normal : ResourcePressure.Adjusted;

    /// <summary>Evalua candidato y arma la instantanea de recursos con desglose.</summary>
    public static ResourceSnapshot Snapshot(
        MemoryInfo? memory,
        double? candidatePeakGb,
        System.Collections.Generic.IReadOnlyList<RamConsumer>? consumers = null)
    {
        if (memory is null || memory.Status != DetectionStatus.Detected || memory.TotalBytes <= 0)
        {
            return new ResourceSnapshot
            {
                TotalGb = 0,
                FreeGb = 0,
                AvailableGb = 0,
                CacheGb = 0,
                SystemReserveGb = SystemReserveGb,
                CondorReserveGb = CondorReserveGb,
                SafetyMarginGb = OperatingMarginGb(0),
                HeadroomGb = 0,
                SafeBudgetGb = 0,
                Pressure = ResourcePressure.Insufficient,
                TopConsumers = consumers ?? System.Array.Empty<RamConsumer>(),
                CandidatePeakGb = candidatePeakGb,
                CandidatePercentage = candidatePeakGb is { } pk && pk > 0 ? 100.0 : null
            };
        }

        var totalGb = memory.TotalBytes / BytesPerGb;
        var freeGb = memory.FreeBytes / BytesPerGb;
        var availableGb = memory.AvailableBytes / BytesPerGb;
        var cacheGb = memory.CacheBytes / BytesPerGb;
        var margin = OperatingMarginGb(totalGb);

        var headroom = HeadroomGb(freeGb, SystemReserveGb, CondorReserveGb, margin);

        var pressure = candidatePeakGb is { } peak
            ? ClassifyCandidate(peak, totalGb, headroom)
            : ClassifySystemState(headroom);

        var percentage = candidatePeakGb is { } pk2 ? (pk2 / totalGb) * 100.0 : (double?)null;

        return new ResourceSnapshot
        {
            TotalGb = System.Math.Round(totalGb, 1),
            FreeGb = System.Math.Round(freeGb, 1),
            AvailableGb = System.Math.Round(availableGb, 1),
            CacheGb = System.Math.Round(cacheGb, 1),
            SystemReserveGb = SystemReserveGb,
            CondorReserveGb = CondorReserveGb,
            SafetyMarginGb = System.Math.Round(margin, 1),
            HeadroomGb = System.Math.Round(headroom, 1),
            SafeBudgetGb = System.Math.Round(headroom, 1),
            Pressure = pressure,
            TopConsumers = consumers ?? System.Array.Empty<RamConsumer>(),
            CandidatePeakGb = candidatePeakGb,
            CandidatePercentage = percentage is { } pct ? System.Math.Round(pct, 1) : null
        };
    }
}
