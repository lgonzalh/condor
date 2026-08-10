using Condor.Core.Models;
using Condor.Infrastructure;
using Condor.Infrastructure.Detection;

namespace Condor.Infrastructure.Tests;

public class OsDetectorTests
{
    [Fact]
    public async Task DetectAsync_EnWindows_DevuelveEstadoDetectado()
    {
        var detector = new OsDetector();

        var result = await detector.DetectAsync();

        Assert.Equal(DetectionStatus.Detected, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Name));
    }
}

public class CpuDetectorTests
{
    [Fact]
    public async Task DetectAsync_EnEquipoReal_DevuelveNucleosPositivos()
    {
        var detector = new CpuDetector();

        var result = await detector.DetectAsync();

        Assert.Equal(DetectionStatus.Detected, result.Status);
        Assert.True(result.Cores > 0);
        Assert.True(result.LogicalProcessors > 0);
    }
}

public class MemoryDetectorTests
{
    [Fact]
    public async Task DetectAsync_EnEquipoReal_DevuelveMemoriaTotalPositiva()
    {
        var detector = new MemoryDetector();

        var result = await detector.DetectAsync();

        Assert.Equal(DetectionStatus.Detected, result.Status);
        Assert.True(result.TotalBytes > 0);
    }
}

public class GpuDetectorTests
{
    [Fact]
    public async Task DetectAsync_NoLanzaExcepcion_YDegradaSinControladores()
    {
        var detector = new GpuDetector();

        var result = await detector.DetectAsync();

        Assert.NotEqual(DetectionStatus.Error, result.Status);
        if (result.Status == DetectionStatus.Detected)
        {
            Assert.NotEmpty(result.Gpus);
        }
    }
}

public class StorageDetectorTests
{
    [Fact]
    public async Task DetectAsync_EnEquipoReal_DetectaAlMenosUnDisco()
    {
        var detector = new StorageDetector();

        var result = await detector.DetectAsync();

        Assert.Equal(DetectionStatus.Detected, result.Status);
        Assert.NotEmpty(result.Disks);
        Assert.True(result.Disks.All(disk => disk.TotalBytes > 0));
    }
}
