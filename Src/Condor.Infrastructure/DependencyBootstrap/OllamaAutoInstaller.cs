using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Condor.Infrastructure.DependencyBootstrap;

/// <summary>
/// Instalador AUTOMATICO de Ollama desde la fuente oficial de Windows. Descarga
/// OllamaSetup.exe y lo ejecuta. Si Windows requiere elevacion/UAC, el propio
/// instalador solicita la autorizacion del sistema operativo (no es una
/// confirmacion funcional de Condor). Espera la finalizacion y devuelve el
/// resultado. No muestra excepciones al usuario; el detalle queda en Diagnostic.
/// </summary>
public sealed class OllamaAutoInstaller : IOllamaInstaller
{
    /// <summary>Fuente oficial del instalador de Ollama para Windows.</summary>
    public const string OfficialDownloadUrl = "https://ollama.com/download/OllamaSetup.exe";

    private readonly TimeSpan _downloadTimeout;
    private readonly TimeSpan _installTimeout;
    private readonly Func<string, string, TimeSpan, CancellationToken, Task<bool>> _download;
    private readonly Func<string, string, TimeSpan, CancellationToken, Task<bool>> _runInstaller;

    public OllamaAutoInstaller(
        TimeSpan? downloadTimeout = null,
        TimeSpan? installTimeout = null,
        Func<string, string, TimeSpan, CancellationToken, Task<bool>>? downloader = null,
        Func<string, string, TimeSpan, CancellationToken, Task<bool>>? installer = null)
    {
        _downloadTimeout = downloadTimeout ?? TimeSpan.FromMinutes(10);
        _installTimeout = installTimeout ?? TimeSpan.FromMinutes(15);
        _download = downloader ?? DownloadCoreAsync;
        _runInstaller = installer ?? RunInstallerCoreAsync;
    }

    public string DiagnosticName => "OllamaSetup.exe (fuente oficial)";

    public async Task<bool> DownloadAndInstallAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var installerPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "condor-ollama-" + Guid.NewGuid().ToString("N") + ".exe");

            var downloaded = await _download(OfficialDownloadUrl, installerPath, _downloadTimeout, cancellationToken);
            if (!downloaded)
            {
                return false;
            }

            var installed = await _runInstaller(installerPath, "/VERYSILENT /NORESTART", _installTimeout, cancellationToken);
            return installed;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> DownloadCoreAsync(
        string url, string destination, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var http = new System.Net.Http.HttpClient { Timeout = timeout };
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(timeout);

        var bytes = await http.GetByteArrayAsync(url, linked.Token);
        await System.IO.File.WriteAllBytesAsync(destination, bytes, linked.Token);
        return System.IO.File.Exists(destination) && new System.IO.FileInfo(destination).Length > 0;
    }

    private static async Task<bool> RunInstallerCoreAsync(
        string installerPath, string arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = arguments,
            // UseShellExecute=true deja que Windows presente el UAC/elevacion si
            // corresponde. No es una confirmacion manual de Condor.
            UseShellExecute = true,
            CreateNoWindow = true,
            WorkingDirectory = System.IO.Path.GetTempPath()
        };

        using var process = new Process { StartInfo = psi };
        if (!process.Start())
        {
            return false;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(linked.Token);
            return process.ExitCode == 0;
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { /* ya termino */ }
            return false;
        }
        catch
        {
            return false;
        }
    }
}
