using Condor.Core.Contracts;
using Condor.Core.Models;

namespace Condor.Cli.Presentation;

/// <summary>
/// Puente que traduce el protocolo de observador de la preparacion del entorno
/// (IStartupProgressObserver) hacia la superficie visual (StartupProgressPresenter).
/// Desacopla la emision de etapas (StartupPreparer/Program) de la representacion
/// en terminal. Independiente del puente del agente (AgentProgressObserverBridge).
/// </summary>
public sealed class StartupProgressObserverBridge : IStartupProgressObserver
{
    private readonly IStartupProgressView _view;

    public StartupProgressObserverBridge(IStartupProgressView view)
    {
        _view = view;
    }

    public void Report(StartupProgress progress)
    {
        _view.Report(progress);
    }
}
