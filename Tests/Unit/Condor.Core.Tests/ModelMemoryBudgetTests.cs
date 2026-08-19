using Condor.Core.Evaluation;
using Condor.Core.Models;

namespace Condor.Core.Tests;

public class ModelMemoryBudgetTests
{
    [Fact]
    public void EstimatePeakGb_AplicaFactorDePicoAlPeso()
    {
        var peak = ModelMemoryBudget.EstimatePeakGb(4.0, 0.5);

        Assert.Equal(5.3, peak, 3);
    }

    [Fact]
    public void OperatingMargin_RespectaPisoYMaximo()
    {
        var min = ModelMemoryBudget.OperatingMarginGb(1);   // piso
        var max = ModelMemoryBudget.OperatingMarginGb(80);  // tope

        Assert.Equal(1.5, min, 3);
        Assert.Equal(3.0, max, 3);
    }

    [Fact]
    public void SafeBudget_NuncaSuperaLaRamLibreReal()
    {
        // Equipo con mucha RAM total pero poca libre: el presupuesto DEBE
        // quedar por debajo de la libre real, no del porcentaje de total.
        var budget = ModelMemoryBudget.SafeBudgetGb(32, 4.7);

        Assert.True(budget <= 4.7);
        Assert.True(budget < 4.7);
    }

    [Fact]
    public void SafeBudget_ConPresionTotalYLibreBaja_RespetaLaLibre()
    {
        var lowFree = ModelMemoryBudget.SafeBudgetGb(15.4, 4.7);
        var freeNoReserve = 4.7;

        Assert.True(lowFree < freeNoReserve);
        Assert.True(lowFree > 0);
    }

    [Fact]
    public void SafeBudget_NuncaEsNegativo()
    {
        Assert.True(ModelMemoryBudget.SafeBudgetGb(15.4, 0.1) >= 0);
        Assert.True(ModelMemoryBudget.SafeBudgetGb(16, 0) >= 0);
    }

    [Fact]
    public void FitsInRam_ModeloTresBTienePresupuestoSuficiente()
    {
        Assert.True(ModelMemoryBudget.FitsInRam(1.9, 0, 15.4, 4.7));
        Assert.True(ModelMemoryBudget.FitsInRam(1.8, 0, 15.4, 4.7));
    }

    [Fact]
    public void FitsInRam_ModeloSieteBNoEsViableConRamLibreBaja()
    {
        // regla raiz: presupuesto = libre (4.7) - margen (1.5) = 3.2; pico 7B ~5.45
        Assert.False(ModelMemoryBudget.FitsInRam(4.36, 0, 15.4, 4.7));
    }

    [Fact]
    public void FitsInRam_SuperaLaLibreReal_EsFalso()
    {
        // Aunque total alto, si libre es pequena la raiz no cabe.
        Assert.False(ModelMemoryBudget.FitsInRam(4.36, 0, 15.4, 2.5));
    }

    [Fact]
    public void FitsInRamStrict_CumpleAmbasCondiciones_SeleccionDefinitiva()
    {
        // 7B (pico ~5.23) sobre 16 GB totales con 7 libres: porcentaje Ajustado
        // pero presupuesto seguro (headroom 2.5) NO lo admite -> NO cabe.
        Assert.False(ModelMemoryBudget.FitsInRamStrict(4.36, 0, 16, 7));

        // 3B (pico ~2.16) con la misma RAM: Normal y dentro del presupuesto -> cabe.
        Assert.True(ModelMemoryBudget.FitsInRamStrict(1.8, 0, 16, 7));
    }

    [Fact]
    public void FitsInDisk_RespetaElMargenDeTrabajoYSeguridad()
    {
        var freeDiskGb = 200.0;

        Assert.True(ModelMemoryBudget.FitsInDisk(2, 4, freeDiskGb));
        Assert.False(ModelMemoryBudget.FitsInDisk(195, 4, freeDiskGb));
    }

    [Fact]
    public void HeadroomGb_RestaReservasYMargenSinSobreEstimar()
    {
        var headroom = ModelMemoryBudget.HeadroomGb(8.0, 1.5, 1.5, 2.0);

        Assert.Equal(3.0, headroom, 3);
        Assert.True(headroom <= 8.0);
    }

    [Fact]
    public void HeadroomGb_NuncaNegativo()
    {
        Assert.True(ModelMemoryBudget.HeadroomGb(0.5, 1.5, 1.5, 2.0) >= 0);
    }

    [Fact]
    public void ClassifyByRatio_Normal_CuandoHasta30PorCientoDeLaRamTotal()
    {
        // total esta = 100%; candidato = 25% -> Normal.
        Assert.Equal(ResourcePressure.Normal, ModelMemoryBudget.ClassifyByRatio(4.0, 16.0));
        Assert.Equal(ResourcePressure.Normal, ModelMemoryBudget.ClassifyByRatio(4.8, 16.0)); // 30% exacto
    }

    [Fact]
    public void ClassifyByRatio_Ajustado_CuandoEntre30Y35PorCiento()
    {
        // 32.5% de 16 GB -> Ajustado.
        Assert.Equal(ResourcePressure.Adjusted, ModelMemoryBudget.ClassifyByRatio(5.2, 16.0));
        Assert.Equal(ResourcePressure.Adjusted, ModelMemoryBudget.ClassifyByRatio(5.6, 16.0)); // 35% exacto
    }

    [Fact]
    public void ClassifyByRatio_Presion_CuandoEntre35Y40PorCiento()
    {
        // 37.5% de 16 GB -> Presion.
        Assert.Equal(ResourcePressure.Pressure, ModelMemoryBudget.ClassifyByRatio(6.0, 16.0));
        Assert.Equal(ResourcePressure.Pressure, ModelMemoryBudget.ClassifyByRatio(6.4, 16.0)); // 40% exacto
    }

    [Fact]
    public void ClassifyByRatio_Insuficiente_CuandoSuperaSLa40PorCiento()
    {
        // 43.75% de 16 GB -> Insuficiente (nunca se carga, sin reintentos).
        Assert.Equal(ResourcePressure.Insufficient, ModelMemoryBudget.ClassifyByRatio(7.0, 16.0));
    }

    [Fact]
    public void ClassifyCandidate_Insuficiente_CuandoNoCumpleElPresupuestoSeguro()
    {
        // El porcentaje de 5.2/16 = 32.5% (Ajustado) PERO el presupuesto seguro
        // (headroom) solo es 1.0 GB: la AMBAS condiciones fallan -> Insuficiente.
        var total = 16.0;
        var peak = ModelMemoryBudget.EstimatePeakGb(4.36, 0); // ~5.23 GB (7B Q4)
        var headroom = 1.0;

        Assert.Equal(ResourcePressure.Insufficient, ModelMemoryBudget.ClassifyCandidate(peak, total, headroom));
    }

    [Fact]
    public void ClassifyCandidate_Ajustado_PermiteSoloSiElMargenEsSuficiente()
    {
        // 5.23/16 = 32.7% (Ajustado) y headroom 5.5 >= pico: se permite.
        var peak = ModelMemoryBudget.EstimatePeakGb(4.36, 0);
        Assert.Equal(ResourcePressure.Adjusted, ModelMemoryBudget.ClassifyCandidate(peak, 16.0, 5.5));
    }

    [Fact]
    public void ClassifyCandidate_Insuficiente_SiElPorcentajeSuperaSLa40ConAmpliaRamLibre()
    {
        // Pico 7.0/16 = 43.75% > 40% -> Insuficiente aunque haya mucha RAM libre.
        Assert.Equal(ResourcePressure.Insufficient, ModelMemoryBudget.ClassifyCandidate(7.0, 16.0, 10.0));
    }

    [Fact]
    public void ClassifySystemState_CuandoSinCandidato_UsaElHeadroom()
    {
        Assert.Equal(ResourcePressure.Normal, ModelMemoryBudget.ClassifySystemState(3.0));
        Assert.Equal(ResourcePressure.Adjusted, ModelMemoryBudget.ClassifySystemState(1.0));
    }

    [Fact]
    public void Snapshot_NoCuentaLaCacheComoGarantia()
    {
        // 20 GB totales, 15 libres, pero 12 de esa "libre" es standby/cache.
        // El headroom debe usar la libre real, no la cache.
        var memory = new MemoryInfo
        {
            TotalBytes = (long)(20 * ModelMemoryBudget.BytesPerGb),
            FreeBytes = (long)(15 * ModelMemoryBudget.BytesPerGb),
            AvailableBytes = (long)(18 * ModelMemoryBudget.BytesPerGb),
            CacheBytes = (long)(12 * ModelMemoryBudget.BytesPerGb),
            Status = DetectionStatus.Detected
        };

        var snapshot = ModelMemoryBudget.Snapshot(memory, candidatePeakGb: 4.0);

        // El headroom se deriva de FreeBytes (15) menos reservas y margen, no de la cache.
        Assert.True(snapshot.HeadroomGb < 15.0);
        Assert.Equal(12.0, snapshot.CacheGb, 1);
        // Con headroom asi, no cabe un modelo que dependa de la cache.
        Assert.False(snapshot.HeadroomAllows(14.0));
    }

    [Fact]
    public void Snapshot_ClasificaInsuficiente_CuandoSinRamaDetectable()
    {
        var snapshot = ModelMemoryBudget.Snapshot(new MemoryInfo { Status = DetectionStatus.NotDetected }, candidatePeakGb: 4.0);

        Assert.Equal(ResourcePressure.Insufficient, snapshot.Pressure);
    }
}
