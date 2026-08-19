using Condor.Cli.Presentation;
using Condor.Core.Models;
using Condor.Infrastructure.Agent;

namespace Condor.Infrastructure.Tests;

public class StubProgressView : IAgentProgressView
{
    public string? Started { get; private set; }
    public List<AgentProgress> Reports { get; } = new();
    public (bool Success, string? Line)? Stopped { get; private set; }

    public void Start(string intention) => Started = intention;
    public void Report(AgentProgress progress) => Reports.Add(progress);
    public void Stop(bool success, string? finalLine) => Stopped = (success, finalLine);
}

public class AgentProgressTests
{
    [Theory]
    [InlineData(AgentAction.ActionListDir, AgentPhase.Observing)]
    [InlineData(AgentAction.ActionReadFile, AgentPhase.Observing)]
    [InlineData(AgentAction.ActionSearch, AgentPhase.Observing)]
    [InlineData(AgentAction.ActionPatch, AgentPhase.Building)]
    [InlineData(AgentAction.ActionEditFile, AgentPhase.Building)]
    [InlineData(AgentAction.ActionCreateFile, AgentPhase.Building)]
    [InlineData(AgentAction.ActionBuild, AgentPhase.Verifying)]
    [InlineData(AgentAction.ActionTest, AgentPhase.Verifying)]
    [InlineData(AgentAction.ActionRestore, AgentPhase.Verifying)]
    [InlineData(AgentAction.ActionDone, AgentPhase.Finalizing)]
    public void PhaseForAction_MapeaAccionAFase(string action, AgentPhase expected)
    {
        Assert.Equal(expected, AgentService.PhaseForAction(action));
    }

    [Fact]
    public void Bridge_ReportaAlPresentadorQueHaRecibido()
    {
        var view = new StubProgressView();
        var bridge = new AgentProgressObserverBridge(view);
        var e = AgentProgress.Of(AgentPhase.Verifying, action: "build", iteration: 3);

        bridge.Report(e);

        Assert.Single(view.Reports);
        Assert.Equal(AgentPhase.Verifying, view.Reports[0].Phase);
        Assert.Equal("build", view.Reports[0].Action);
        Assert.Equal(3, view.Reports[0].Iteration);
    }

    [Fact]
    public void PresenterYObsObserver_EstanConectadosAlPuente()
    {
        // Verifica que el presentador implementa la superficie que el puente consume.
        IAgentProgressView view = new AgentProgressPresenter();
        var bridge = new AgentProgressObserverBridge(view);
        Assert.NotNull(bridge);
    }

    [Fact]
    public void Bridge_PropagaEstadoDeRecuperacionYError()
    {
        var view = new StubProgressView();
        var bridge = new AgentProgressObserverBridge(view);

        bridge.Report(AgentProgress.Of(AgentPhase.Verifying, message: "recuperando el modelo", flag: ProgressFlag.Recovering));
        bridge.Report(AgentProgress.Of(AgentPhase.Verifying, message: "proveedor detenido", flag: ProgressFlag.ProviderError));

        Assert.Equal(2, view.Reports.Count);
        Assert.Equal(ProgressFlag.Recovering, view.Reports[0].Flag);
        Assert.Equal(ProgressFlag.ProviderError, view.Reports[1].Flag);
    }

    [Fact]
    public void Bridge_PropagaEstadoDeRecursos()
    {
        var view = new StubProgressView();
        var bridge = new AgentProgressObserverBridge(view);

        bridge.Report(AgentProgress.Of(AgentPhase.Observing, resourceState: "Normal", availableGb: 6.1));

        Assert.Single(view.Reports);
        Assert.Equal("Normal", view.Reports[0].ResourceState);
        Assert.Equal(6.1, view.Reports[0].AvailableGb);
    }

    [Fact]
    public void Bridge_PropagaAlertaDePresion()
    {
        var view = new StubProgressView();
        var bridge = new AgentProgressObserverBridge(view);

        bridge.Report(AgentProgress.Of(AgentPhase.Verifying, message: "Presion de memoria: 2.0 GB libres · estado Presion. Condor reducirá temporalmente su carga.", flag: ProgressFlag.Recovering));

        Assert.Single(view.Reports);
        Assert.Equal(ProgressFlag.Recovering, view.Reports[0].Flag);
        Assert.Contains("Presion", view.Reports[0].Message);
    }
}
