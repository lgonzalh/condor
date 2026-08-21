using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Condor.Infrastructure.Detection;

namespace Condor.Infrastructure.DependencyBootstrap;

/// <summary>
/// Inicia el server de Ollama (ollama serve) cuando esta instalado pero el
/// endpoint no responde. Devuelve true si Condor lo inicio y lo registra como
/// own (StartedByCondor); false si ya existia una instancia activa que Condor
/// debe reutilizar y NO cerrar.
///
/// Ownership: Condor nunca ejecuta taskkill ni cierra una instancia de Ollama
/// que ya existia antes de iniciarlo. Solo registra quien lo inicio; la
/// liberacion del modelo se hace via keep_alive=0 en la sesion, no cerrando
/// procesos de Ollama/llama-server.
/// </summary>
public sealed class OllamaServerLauncher : IOllamaServerLauncher
{
    public async Task<bool> StartServerAsync(CancellationToken cancellationToken = default)
    {
        var ollamaExe = ToolDetector.FindInPath("ollama");
        if (ollamaExe is null)
        {
            return false;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ollamaExe,
                Arguments = "serve",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = System.IO.Path.GetDirectoryName(ollamaExe) ?? ""
            };

            using var process = new Process { StartInfo = psi };
            var started = process.Start();
            if (!started)
            {
                return false;
            }

            // Condor inicio el server: registra ownership. No matamos el proceso;
            // Ollama es el gestor del runner. La liberacion del modelo sera por
            // keep_alive=0 en el shutdown.
            await Task.Yield();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
