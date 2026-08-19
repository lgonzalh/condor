using Condor.Core.Evaluation;

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
    public void FitsInDisk_RespetaElMargenDeTrabajoYSeguridad()
    {
        var freeDiskGb = 200.0;

        Assert.True(ModelMemoryBudget.FitsInDisk(2, 4, freeDiskGb));
        Assert.False(ModelMemoryBudget.FitsInDisk(195, 4, freeDiskGb));
    }
}
