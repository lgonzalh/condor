using Condor.Core.Models;

namespace Condor.Cli.Presentation;

/// <summary>
/// Superficie de presentacion del progreso de la preparacion del entorno al
/// arrancar Condor (independiente de la del agente, IAgentProgressView). Permite
/// probar el enrutado de eventos (bridge) sin acoplar a la consola y desacopla la
/// emision de etapas (StartupPreparer/Program) de la representacion visual.
/// </summary>
public interface IStartupProgressView
{
    /// <summary>Inicia la animacion y muestra el banner de arranque.</summary>
    void Start();

    /// <summary>Refleja una etapa (o un avance real de descarga) de la preparacion.</summary>
    void Report(StartupProgress progress);

    /// <summary>Detiene la animacion y cierra la preparacion.</summary>
    void Stop(bool success, string? finalLine = null);
}
