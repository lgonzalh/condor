using Condor.Cli.Presentation;
using Condor.Core.Models;
using Condor.Infrastructure.Llm;

namespace Condor.Infrastructure.Tests;

public class StubStartupProgressView : IStartupProgressView
{
    public int Started { get; private set; }
    public List<StartupProgress> Reports { get; } = new();
    public (bool Success, string? Line)? Stopped { get; private set; }

    public void Start() => Started++;
    public void Report(StartupProgress progress) => Reports.Add(progress);
    public void Stop(bool success, string? finalLine = null) => Stopped = (success, finalLine);
}

public class StartupProgressTests
{
    [Fact]
    public void Bridge_PropagaAlPresentadorQueHaRecibido()
    {
        var view = new StubStartupProgressView();
        var bridge = new StartupProgressObserverBridge(view);

        bridge.Report(StartupProgress.Of(StartupStage.ReviewingResources));
        bridge.Report(StartupProgress.Of(StartupStage.Ready, completed: true));

        Assert.Equal(2, view.Reports.Count);
        Assert.Equal(StartupStage.ReviewingResources, view.Reports[0].Stage);
        Assert.False(view.Reports[0].Completed);
        Assert.True(view.Reports[1].Completed);
        Assert.Equal(StartupStage.Ready, view.Reports[1].Stage);
    }

    [Fact]
    public void Bridge_PropagaDescargaConPorcentajeReal()
    {
        var view = new StubStartupProgressView();
        var bridge = new StartupProgressObserverBridge(view);

        bridge.Report(StartupProgress.Of(StartupStage.DownloadingModel, downloadPercent: 70.0));

        var report = Assert.Single(view.Reports);
        Assert.NotNull(report.DownloadPercent);
        Assert.Equal(70.0, report.DownloadPercent!.Value, 1);
    }

    [Fact]
    public void Progreso_NuncaInventaPorcentaje()
    {
        // Etapas sin descarga: NUNCA deben llevar porcentaje.
        Assert.Null(StartupProgress.Of(StartupStage.ReviewingResources).DownloadPercent);
        Assert.Null(StartupProgress.Of(StartupStage.SelectingModel).DownloadPercent);
        Assert.Null(StartupProgress.Of(StartupStage.Ready).DownloadPercent);
    }

    [Theory]
    [InlineData("{\"status\":\"downloading\",\"completed\":7,\"total\":10}", 70.0)]
    [InlineData("{\"status\":\"downloading\",\"completed\":0,\"total\":100}", 0.0)]
    [InlineData("{\"status\":\"downloading\",\"completed\":12345,\"total\":100000}", 12.345)]
    [InlineData("{\"status\":\"pulling manifest\"}", null)]
    [InlineData("{\"status\":\"success\"}", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("{\"status\":\"downloading\",\"completed\":10,\"total\":0}", null)]
    [InlineData("no es json", null)]
    public void ParseDownloadPercent_SoloUsaProgresoRealDeOllama(string? line, double? expected)
    {
        Assert.Equal(expected, OllamaModelOperator.ParseDownloadPercent(line));
    }

    [Fact]
    public void PresenterYObservador_EstanConectadosAlPuente()
    {
        // El presentador implementa la superficie que el puente consume, y es
        // independiente del presentador del agente (IAgentProgressView).
        using var presenter = new StartupProgressPresenter();
        IStartupProgressView view = presenter;
        var bridge = new StartupProgressObserverBridge(view);
        Assert.NotNull(bridge);
    }
}
