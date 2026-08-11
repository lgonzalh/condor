using Condor.Core.Evaluation;

namespace Condor.Core.Tests;

public class ModelMemoryBudgetTests
{
    [Fact]
    public void EstimatePeakBytes_AplicaFactorDeSeguridad()
    {
        var peak = ModelMemoryBudget.EstimatePeakBytes(4_687_090_790);

        Assert.Equal(5_624_508_948, peak);
    }

    [Fact]
    public void AvailableBudgetGb_UsaElMayorEntreLibresYTopeDeTotal()
    {
        var budget = ModelMemoryBudget.AvailableBudgetGb(15.4, 7.1);

        Assert.Equal(6.93, budget, 3);
    }

    [Fact]
    public void AvailableBudgetGb_NuncaPermiteMasDelTopeDeTotal()
    {
        var budget = ModelMemoryBudget.AvailableBudgetGb(8, 0.5);

        Assert.Equal(3.6, budget, 3);
    }

    [Fact]
    public void AvailableBudgetGb_NuncaEsNegativo()
    {
        Assert.True(ModelMemoryBudget.AvailableBudgetGb(0.5, 0.1) >= 0);
        Assert.True(ModelMemoryBudget.AvailableBudgetGb(16, 0) >= 0);
    }

    [Fact]
    public void FitsInRam_ModeloAjustadoAlPresupuesto_DevuelveVerdadero()
    {
        var size = (long)(3.56 * ModelMemoryBudget.BytesPerGb);

        Assert.True(ModelMemoryBudget.FitsInRam(size, 15.4, 7.1));
    }

    [Fact]
    public void FitsInRam_ModeloQueSuperaElPresupuesto_DevuelveFalso()
    {
        var size = (long)(8 * ModelMemoryBudget.BytesPerGb);

        Assert.False(ModelMemoryBudget.FitsInRam(size, 15.4, 7.1));
    }

    [Fact]
    public void FitsInRam_ModeloSieteBConCuantizacionQ4_EsViable()
    {
        var size = (long)(4.36 * ModelMemoryBudget.BytesPerGb);

        Assert.True(ModelMemoryBudget.FitsInRam(size, 15.4, 7.1));
    }

    [Fact]
    public void FitsInDisk_RespetaElMargenDeSeguridad()
    {
        var freeBytes = (long)(200 * ModelMemoryBudget.BytesPerGb);

        Assert.True(ModelMemoryBudget.FitsInDisk((long)(90 * ModelMemoryBudget.BytesPerGb), freeBytes));
        Assert.False(ModelMemoryBudget.FitsInDisk((long)(110 * ModelMemoryBudget.BytesPerGb), freeBytes));
    }
}
