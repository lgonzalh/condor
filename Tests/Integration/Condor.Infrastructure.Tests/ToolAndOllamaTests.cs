using Condor.Core.Models;
using Condor.Infrastructure.Detection;

namespace Condor.Infrastructure.Tests;

public class ToolDetectorTests
{
    [Fact]
    public void DetectAll_EnEquipoDeDesarrollo_DetectaGit()
    {
        var detector = new ToolDetector();

        var tools = detector.DetectAll();

        Assert.Contains(tools, tool => tool.Name == "git" && tool.Status == DetectionStatus.Detected);
    }

    [Fact]
    public void DetectAll_DevuelveResultadoParaTodasLasHerramientasConocidas()
    {
        var detector = new ToolDetector();

        var tools = detector.DetectAll();

        Assert.Equal(13, tools.Count);
        Assert.All(tools, tool => Assert.True(
            tool.Status == DetectionStatus.Detected ||
            tool.Status == DetectionStatus.NotDetected));
    }
}

public class GitDetectorTests
{
    [Fact]
    public async Task DetectAsync_EnEquipoConGit_DevuelveVersion()
    {
        var detector = new GitDetector();

        var result = await detector.DetectAsync();

        Assert.Equal(DetectionStatus.Detected, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Version));
    }
}

public class OllamaDetectorTests
{
    [Fact]
    public async Task DetectAsync_NoLanzaExcepcion_YConservaEstadoCoherente()
    {
        var detector = new OllamaDetector();

        var result = await detector.DetectAsync();

        Assert.False(string.IsNullOrWhiteSpace(result.Installed.ToString()));
        if (result.ServerRunning)
        {
            Assert.False(string.IsNullOrWhiteSpace(result.ServerVersion));
        }
        else
        {
            Assert.False(string.IsNullOrWhiteSpace(result.Note));
        }
    }
}
