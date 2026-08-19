using Condor.Core.Models;

namespace Condor.Core.Contracts;

/// <summary>
/// Observador opcional de la preparacion del entorno al arrancar Condor.
/// Independiente del progreso de tareas del agente (IAgentProgressObserver):
/// este canal refleja EXCLUSIVAMENTE la puesta en marcha (recursos, Ollama,
/// modelos, descarga, verificacion) hasta que la sesion esta lista. La UI se
/// implementa en la capa CLI sin acoplar la logica de preparacion a la pantalla.
/// </summary>
public interface IStartupProgressObserver
{
    void Report(StartupProgress progress);
}
