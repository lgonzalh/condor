using Condor.Core.Models;
using Condor.Infrastructure.Detection;

namespace Condor.Infrastructure.Tests;

public class ResourceDetectionTests
{
    [Fact]
    public async Task MemoryDetector_DetectaDesgloseDeMemoria()
    {
        var detector = new MemoryDetector();
        var memory = await detector.DetectAsync();

        Assert.Equal(DetectionStatus.Detected, memory.Status);
        Assert.True(memory.TotalBytes > 0);
        Assert.True(memory.FreeBytes >= 0);
        // La cache es informativa; puede ser 0 si no se pudo derivar, pero nunca
        // se trata como RAM libre (el presupuesto usa FreeBytes).
        Assert.True(memory.CacheBytes >= 0);
        Assert.True(memory.AvailableBytes >= memory.FreeBytes || memory.AvailableBytes == 0);
    }

    [Fact]
    public void ProcessRamDetector_ListaProcesosDeAltoConsumoSinCerrarlos()
    {
        var detector = new ProcessRamDetector();
        var consumers = detector.DetectTopConsumers(max: 5);

        // No debe devolver null y si hay procesos de alto consumo, los reporta.
        Assert.NotNull(consumers);
        foreach (var c in consumers)
        {
            Assert.False(string.IsNullOrWhiteSpace(c.ProcessName));
            Assert.True(c.WorkingSetGb >= 0);
        }
    }
}
