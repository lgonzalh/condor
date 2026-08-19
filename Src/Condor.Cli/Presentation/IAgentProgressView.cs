using Condor.Core.Models;

namespace Condor.Cli.Presentation;

/// <summary>
/// Superficie de presentacion del progreso. Permite probar el enrutado de
/// eventos (bridge) sin acoplar a la consola y desacopla la emision del agente
/// de la representacion visual.
/// </summary>
public interface IAgentProgressView
{
    void Start(string intention);
    void Report(AgentProgress progress);
    void Stop(bool success, string? finalLine);
}
