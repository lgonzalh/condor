using Condor.Core.Models;

namespace Condor.Core.Contracts;

/// <summary>
/// Observador opcional del progreso del agente. El agente emite eventos puros
/// (fase, accion, ruta, iteracion); la presentacion visual se implementa en la
/// capa CLI sin acoplar la logica del agente a la UI.
/// </summary>
public interface IAgentProgressObserver
{
    void Report(AgentProgress progress);
}
