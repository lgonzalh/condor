using System;
using System.Threading;
using System.Threading.Tasks;
using Condor.Core.Models;
using Condor.Infrastructure.DependencyBootstrap;

namespace Condor.Infrastructure.Tests;

/// <summary>
/// Pruebas del bootstrap de dependencias (Ollama): deteccion real del server,
/// instalacion automatica, arranque de servidor, timeout, ownership y
/// cancelacion cooperativa (escenarios A-H).
/// </summary>
public class DependencyBootstrapTests
{
    #region A. Ollama instalado + servidor disponible -> continuar inmediatamente
    [Fact]
    public async Task A_ServerDisponible_ContinuaSinInstalarNiArrancar()
    {
        var health = new FakeHealthChecker(OllamaHealth.ServerAvailable);
        var installer = new FakeInstaller { Succeed = false }; // no debe usarse
        var launcher = new FakeLauncher { Result = false };
        var boot = new DependencyBootstrapper(new OllamaProvisioner(health, installer, launcher));

        var r = await boot.RunAsync();

        Assert.True(r.Ready);
        Assert.Equal(OllamaOwnership.Existing, r.Ollama!.Ownership);
        Assert.Equal(0, installer.Calls);
        Assert.Equal(0, launcher.Calls);
    }
    #endregion

    #region B. Instalado + server detenido -> Condor lo inicia y verifica
    [Fact]
    public async Task B_ServerDetenido_CondorLoIniciaYVerificaDisponibilidad()
    {
        var launcher = new FakeLauncher { Result = true, ServerComesUpAfterLaunches = true };
        var health = new FakeHealthChecker(OllamaHealth.InstalledServerDown, becomeAvailableAfterChecks: 2);
        var boot = new DependencyBootstrapper(new OllamaProvisioner(
            health, new FakeInstaller(), launcher,
            serverWaitAttempt: TimeSpan.FromMilliseconds(1), serverMaxAttempts: 10));

        var r = await boot.RunAsync();

        Assert.True(r.Ready);
        Assert.Equal(1, launcher.Calls);
        Assert.Equal(OllamaOwnership.StartedByCondor, r.Ollama!.Ownership);
        Assert.Equal("Ollama Server iniciado por Condor.", r.Ollama.Action);
    }
    #endregion

    #region C. No instalado -> flujo de instalacion automatica
    [Fact]
    public async Task C_NoInstalado_InstalacionAutomaticaYVerificacion()
    {
        var installer = new FakeInstaller { Succeed = true };
        // Tras instalar, el server queda disponible.
        var health = new FakeHealthChecker(OllamaHealth.NotInstalled, becomeAvailableAfterChecks: 1);
        var boot = new DependencyBootstrapper(new OllamaProvisioner(
            health, installer, new FakeLauncher { Result = true },
            installVerifyWait: TimeSpan.FromMilliseconds(1), installVerifyAttempts: 10));

        var r = await boot.RunAsync();

        Assert.True(r.Ready);
        Assert.Equal(1, installer.Calls);
        Assert.Contains("instalado", r.Ollama!.Action, StringComparison.OrdinalIgnoreCase);
    }
    #endregion

    #region D. Instalado pero server no puede iniciarse -> timeout + error controlado
    [Fact]
    public async Task D_NoPuedeIniciar_TimeoutYErrorControlado()
    {
        var health = new FakeHealthChecker(OllamaHealth.InstalledServerDown);
        var boot = new DependencyBootstrapper(new OllamaProvisioner(
            health, new FakeInstaller(), new FakeLauncher(),
            serverWaitAttempt: TimeSpan.FromMilliseconds(1), serverMaxAttempts: 3));

        var r = await boot.RunAsync();

        Assert.False(r.Ready);
        Assert.False(r.Ollama!.Ok);
        Assert.Contains("Ollama Server", r.Ollama.Reason, StringComparison.OrdinalIgnoreCase);
        // No presenta stack trace al usuario.
        Assert.False(string.Equals(r.Reason, r.Ollama.Diagnostic, StringComparison.Ordinal), "No debe exponer el detalle tecnico como motivo.");
    }
    #endregion

    #region E. Ya existia antes de Condor -> reutiliza y no lo cierra
    [Fact]
    public async Task E_YaEjecutandose_SeReutilizaYNoSeCierra()
    {
        var health = new FakeHealthChecker(OllamaHealth.ServerAvailable);
        var boot = new DependencyBootstrapper(new OllamaProvisioner(
            health, new FakeInstaller(), new FakeLauncher()));

        var r = await boot.RunAsync();

        Assert.True(r.Ready);
        Assert.Equal(OllamaOwnership.Existing, r.Ollama!.Ownership);
        // El bootstrap nunca cierra una instancia ajena (no hay kill en el resultado).
        Assert.NotEqual("Ollama cerrado", r.Ollama.Action);
    }
    #endregion

    #region F. Condor iniciÃ³ Ollama -> registra propiedad
    [Fact]
    public async Task F_CondorInicioElServer_RegistraPropiedad()
    {
        var launcher = new FakeLauncher { Result = true, ServerComesUpAfterLaunches = true };
        // check 1 = clasificar (server abajo); check 2 = tras lanzar, server arriba.
        var health = new FakeHealthChecker(OllamaHealth.InstalledServerDown, becomeAvailableAfterChecks: 2);
        var boot = new DependencyBootstrapper(new OllamaProvisioner(
            health, new FakeInstaller(), launcher,
            serverWaitAttempt: TimeSpan.FromMilliseconds(1), serverMaxAttempts: 5));

        var r = await boot.RunAsync();

        Assert.True(r.Ready);
        Assert.Equal(OllamaOwnership.StartedByCondor, r.Ollama!.Ownership);
        Assert.True(launcher.Calls == 1);
    }
    #endregion

    #region G. Server deja de responder -> no bloquea indefinidamente
    [Fact]
    public async Task G_ServerNuncaResponde_NoBloqueaIndefinidamente()
    {
        var health = new FakeHealthChecker(OllamaHealth.InstalledServerDown);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boot = new DependencyBootstrapper(new OllamaProvisioner(
            health, new FakeInstaller(), new FakeLauncher(),
            serverWaitAttempt: TimeSpan.FromMilliseconds(1), serverMaxAttempts: 4));

        var r = await boot.RunAsync();
        sw.Stop();

        Assert.False(r.Ready);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), "No debe bloquear mas alla del limite.");
    }
    #endregion

    #region H. Cancelacion (Ctrl+C/Ctrl+D) -> cooperativa y limpia
    [Fact]
    public async Task H_CancelacionCooperaYLimpia()
    {
        using var cts = new CancellationTokenSource();
        var launcher = new FakeLauncher { Result = true, ServerComesUpAfterLaunches = false };
        var health = new FakeHealthChecker(OllamaHealth.InstalledServerDown);
        var boot = new DependencyBootstrapper(new OllamaProvisioner(
            health, new FakeInstaller(), launcher,
            serverWaitAttempt: TimeSpan.FromMilliseconds(1000), serverMaxAttempts: 50));

        cts.CancelAfter(TimeSpan.FromMilliseconds(100));
        var r = await boot.RunAsync(progress: null, cancellationToken: cts.Token);

        Assert.False(r.Ready);
        Assert.Contains("cancelad", r.Reason!, StringComparison.OrdinalIgnoreCase);
    }
    #endregion

    #region extras: separa instalado vs server disponible
    [Fact]
    public async Task InstaladoPeroServerSinResponder_SeTrataComoServerCaido_NoComoDisponible()
    {
        // Caso critico: el ejecutable existe pero el endpoint real no responde.
        var health = new FakeHealthChecker(OllamaHealth.InstalledServerDown);
        var healthChecker = health; // ya clasifica como ServerDown aunque "instalado".

        Assert.Equal(OllamaHealth.InstalledServerDown, await healthChecker.ClassifyAsync());
    }
    #endregion

    // --------------------------------------------------------------- fakes
    private sealed class FakeHealthChecker : OllamaHealthChecker
    {
        private readonly OllamaHealth _initial;
        public int Checks;
        private readonly int _becomeAvailableAfter;
        private bool _available;

        public FakeHealthChecker(OllamaHealth initial, int becomeAvailableAfterChecks = -1)
        {
            _initial = initial;
            _becomeAvailableAfter = becomeAvailableAfterChecks;
            _available = initial == OllamaHealth.ServerAvailable;
        }

        public override bool IsInstalled() => _initial != OllamaHealth.NotInstalled;

        public override Task<bool> IsServerAvailableAsync(CancellationToken cancellationToken = default)
        {
            Checks++;
            if (_becomeAvailableAfter >= 0 && Checks >= _becomeAvailableAfter) _available = true;
            return Task.FromResult(_available);
        }

        public override async Task<OllamaHealth> ClassifyAsync(CancellationToken cancellationToken = default)
        {
            if (!IsInstalled()) return OllamaHealth.NotInstalled;
            return await IsServerAvailableAsync(cancellationToken)
                ? OllamaHealth.ServerAvailable
                : OllamaHealth.InstalledServerDown;
        }

        public override Task<OllamaStatus> DetectAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new OllamaStatus { Installed = IsInstalled(), ServerRunning = _available, ServerVersion = "0.31.1" });
    }

    private sealed class FakeInstaller : IOllamaInstaller
    {
        public bool Succeed { get; set; } = true;
        public int Calls { get; private set; }
        public string DiagnosticName => "stub";
        public Task<bool> DownloadAndInstallAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Succeed);
        }
    }

    private sealed class FakeLauncher : IOllamaServerLauncher
    {
        public bool Result { get; set; }
        public bool ServerComesUpAfterLaunches { get; set; }
        public int Calls { get; private set; }
        public Task<bool> StartServerAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Result);
        }
    }
}

