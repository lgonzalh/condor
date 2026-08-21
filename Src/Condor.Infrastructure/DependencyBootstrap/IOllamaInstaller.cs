using System.Threading;
using System.Threading.Tasks;

namespace Condor.Infrastructure.DependencyBootstrap;

/// <summary>
/// Instalador de Ollama desde la fuente oficial. Descarga e instala de forma
/// AUTOMATICA (sin confirmacion interactiva de Condor). Si Windows requiere
/// elevacion/UAC, el propio instalador de Windows solicita la autorizacion del
/// sistema operativo; eso NO es una confirmacion funcional de Condor.
/// </summary>
public interface IOllamaInstaller
{
    /// <summary>Diagnostico/telemetria de la instalacion (detalle tecnico, no UI).</summary>
    string DiagnosticName { get; }

    /// <summary>
    /// Descarga e instala Ollama de la fuente oficial. Devuelve true al terminar
    /// correctamente. Tolerante a timeout/cancelacion.
    /// </summary>
    Task<bool> DownloadAndInstallAsync(CancellationToken cancellationToken = default);
}
