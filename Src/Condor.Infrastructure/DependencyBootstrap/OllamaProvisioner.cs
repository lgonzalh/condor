using System;
using System.Threading;
using System.Threading.Tasks;
using Condor.Core.Contracts;
using Condor.Core.Models;

namespace Condor.Infrastructure.DependencyBootstrap;

/// <summary>
/// Aprovisionador de Ollama dentro del bootstrap de dependencias.
///
/// Flujo:
///   1. Detectar el estado real (OllamaHealthChecker): no-instalado / instalado
///      server-down / server-disponible.
///   2. NO instalado -> instalar AUTOMATICAMENTE (fuente oficial, UAC de Windows
///      si corresponde), sin confirmacion manual de Condor. Esperar finalizacion.
///   3. Instalado + server caido -> iniciar el server (ollama serve) y esperar
///      de forma controlada con timeout y reintentos, comprobando el endpoint.
///   4. Server disponible -> listo; registrar ownership (ya existia vs iniciado).
///
/// Cada etapa tiene timeout, estado visible (progress), cancelacion cooperativa
/// y error controlado. Tras los reintentos sin exito produce un resultado de
/// error (sin stack trace al usuario). Nunca cierra una instancia de Ollama
/// preexistente; solo libera el modelo via keep_alive=0 en la sesion.
/// </summary>
public sealed class OllamaProvisioner
{
    private readonly OllamaHealthChecker _health;
    private readonly IOllamaInstaller _installer;
    private readonly IOllamaServerLauncher _launcher;

    private readonly TimeSpan _serverWaitAttempt;
    private readonly int _serverMaxAttempts;
    private readonly TimeSpan _installVerifyWait;
    private readonly int _installVerifyAttempts;

    public OllamaProvisioner(
        OllamaHealthChecker? health = null,
        IOllamaInstaller? installer = null,
        IOllamaServerLauncher? launcher = null,
        TimeSpan? serverWaitAttempt = null,
        int? serverMaxAttempts = null,
        TimeSpan? installVerifyWait = null,
        int? installVerifyAttempts = null)
    {
        _health = health ?? new OllamaHealthChecker();
        _installer = installer ?? new OllamaAutoInstaller();
        _launcher = launcher ?? new OllamaServerLauncher();

        _serverWaitAttempt = serverWaitAttempt ?? TimeSpan.FromSeconds(2);
        _serverMaxAttempts = serverMaxAttempts ?? 20; // ~40 s maximo de espera al server
        _installVerifyWait = installVerifyWait ?? TimeSpan.FromSeconds(3);
        _installVerifyAttempts = installVerifyAttempts ?? 15; // ~45 s tras instalacion
    }

    /// <summary>
    /// Ejecuta el bootstrap completo de Ollama. Devuelve el resultado con estado,
    /// ownership y accion tomada. Nunca lanza excepciones al llamador. Tolera
    /// cancelacion cooperativa.
    /// </summary>
    public async Task<OllamaProvisioningResult> ProvisionAsync(
        IStartupProgressObserver? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Step(progress, StartupStage.DetectingOllama, "Detectando Ollama...");
            var health = await _health.ClassifyAsync(cancellationToken);

            switch (health)
            {
                case OllamaHealth.ServerAvailable:
                    return await ResultAvailableAsync(progress);

                case OllamaHealth.NotInstalled:
                    return await ProvisionInstallAsync(progress, cancellationToken);

                case OllamaHealth.InstalledServerDown:
                default:
                    return await ProvisionStartServerAsync(progress, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            return new OllamaProvisioningResult
            {
                Ok = false,
                Health = OllamaHealth.InstalledServerDown,
                Reason = "La preparacion fue cancelada.",
                Action = "Cancelada por el usuario."
            };
        }
        catch (Exception ex)
        {
            return new OllamaProvisioningResult
            {
                Ok = false,
                Reason = "No fue posible preparar Ollama automaticamente.",
                Action = "Error controlado.",
                Diagnostic = ex.Message
            };
        }
    }

    private async Task<OllamaProvisioningResult> ProvisionInstallAsync(
        IStartupProgressObserver? progress,
        CancellationToken ct)
    {
        Step(progress, StartupStage.InstallingOllama, "Ollama no esta instalado. Lo necesito para trabajar. Un momento...");
        Step(progress, StartupStage.InstallingOllama, "Descargando e instalando Ollama...");

        var installed = await _installer.DownloadAndInstallAsync(ct);
        if (!installed)
        {
            return new OllamaProvisioningResult
            {
                Ok = false,
                Health = OllamaHealth.NotInstalled,
                Reason = "No fue posible instalar Ollama automaticamente (" + _installer.DiagnosticName + ").",
                Action = "Error de instalacion.",
                Diagnostic = "DownloadAndInstall devolvio false o no termino."
            };
        }

        Step(progress, StartupStage.InstallingOllama, "Verificando instalacion...");
        // Verificar que quedo instalado y que el server responde (reintentos acotados).
        var serverUp = false;
        for (var i = 0; i < _installVerifyAttempts && !serverUp; i++)
        {
            if (ct.IsCancellationRequested) break;
            await Task.Delay(_installVerifyWait, ct).ConfigureAwait(false);
            serverUp = await _health.IsServerAvailableAsync(ct);
        }

        if (!serverUp)
        {
            // Instalado pero server no responde: intentar iniciarlo.
            return await ProvisionStartServerAsync(progress, ct, justInstalled: true);
        }

        Step(progress, StartupStage.VerifyingOllamaServer, "Ollama Server disponible.", isCompleted: true);
        return new OllamaProvisioningResult
        {
            Ok = true,
            Health = OllamaHealth.ServerAvailable,
            Ownership = OllamaOwnership.StartedByCondor,
            Action = "Ollama instalado y server disponible.",
            Reason = "Ollama instalado correctamente."
        };
    }

    private async Task<OllamaProvisioningResult> ProvisionStartServerAsync(
        IStartupProgressObserver? progress,
        CancellationToken ct,
        bool justInstalled = false)
    {
        Step(progress, StartupStage.StartingOllamaServer,
            justInstalled ? "Iniciando el server de Ollama..." : "Ollama no esta levantando el server. Lo hago por ti...");

        var startedByCondor = await _launcher.StartServerAsync(ct);

        Step(progress, StartupStage.VerifyingOllamaServer, "Comprobando disponibilidad del server...");
        var available = false;
        for (var attempt = 0; attempt < _serverMaxAttempts; attempt++)
        {
            if (ct.IsCancellationRequested)
            {
                return CancelledResult();
            }

            if (await _health.IsServerAvailableAsync(ct))
            {
                available = true;
                break;
            }

            await Task.Delay(_serverWaitAttempt, ct).ConfigureAwait(false);
        }

        if (!available)
        {
            return new OllamaProvisioningResult
            {
                Ok = false,
                Health = OllamaHealth.InstalledServerDown,
                Ownership = startedByCondor ? OllamaOwnership.StartedByCondor : OllamaOwnership.Existing,
                Reason = "No pude iniciar Ollama Server automaticamente.",
                Action = "Ollama instalado: OK · Ollama Server: ERROR",
                Diagnostic = "El server no respondio tras " + _serverMaxAttempts + " intentos."
            };
        }

        Step(progress, StartupStage.VerifyingOllamaServer, "Ollama Server disponible.", isCompleted: true);

        string version;
        try { version = (await _health.DetectAsync(ct)).ServerVersion ?? ""; }
        catch { version = ""; }

        return new OllamaProvisioningResult
        {
            Ok = true,
            Health = OllamaHealth.ServerAvailable,
            Ownership = startedByCondor ? OllamaOwnership.StartedByCondor : OllamaOwnership.Existing,
            ServerVersion = version,
            Action = startedByCondor ? "Ollama Server iniciado por Condor." : "Ollama Server ya estaba disponible; se reutiliza.",
            Reason = "Ollama Server disponible."
        };
    }

    private async Task<OllamaProvisioningResult> ResultAvailableAsync(IStartupProgressObserver? progress)
    {
        Step(progress, StartupStage.VerifyingOllamaServer, "Ollama Server disponible.", isCompleted: true);
        await Task.CompletedTask;
        return new OllamaProvisioningResult
        {
            Ok = true,
            Health = OllamaHealth.ServerAvailable,
            Ownership = OllamaOwnership.Existing,
            Action = "Ollama ya estaba disponible; se reutiliza.",
            Reason = "Ollama Server disponible."
        };
    }

    private static OllamaProvisioningResult CancelledResult() => new()
    {
        Ok = false,
        Health = OllamaHealth.InstalledServerDown,
        Reason = "La preparacion fue cancelada.",
        Action = "Cancelada."
    };

    private static void Step(IStartupProgressObserver? progress, StartupStage stage, string? message, bool isCompleted = false)
    {
        progress?.Report(StartupProgress.Of(stage, message, completed: isCompleted));
    }
}
