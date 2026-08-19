using Condor.Core.Contracts;
using Condor.Core.Models;

namespace Condor.Cli.Presentation;

/// <summary>
/// Puente que traduce el protocolo de observador del agente (IAgentProgressObserver)
/// hacia la superficie de presentacion visual (IAgentProgressView). Desacopla la
/// emision de eventos (AgentService) de la UI y es trivialmente testeable.
/// </summary>
public sealed class AgentProgressObserverBridge : IAgentProgressObserver
{
    private readonly IAgentProgressView _view;

    public AgentProgressObserverBridge(IAgentProgressView view)
    {
        _view = view;
    }

    public void Report(AgentProgress progress)
    {
        _view.Report(progress);
    }
}
