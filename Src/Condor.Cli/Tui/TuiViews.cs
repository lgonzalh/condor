using Condor.Cli.Presentation;
using Condor.Core.Models;

namespace Condor.Cli.Tui;

/// <summary>
/// Superficie de preparacion del entorno sobre la TUI. Traduce cada etapa REAL
/// (IStartupProgressView) a una linea de Estado concreta y honesta: nunca un
/// "Verificando..." ambiguo, siempre QUE se esta verificando. Las etapas
/// concluidas se archivan en Conversacion / Actividad; el porcentaje de
/// descarga solo aparece cuando Ollama reporta avance real.
/// </summary>
public sealed class TuiStartupView : IStartupProgressView
{
    private readonly TuiHost _host;
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private readonly HashSet<StartupStage> _archived = new();
    private string? _lastCompleted;

    public TuiStartupView(TuiHost host)
    {
        _host = host;
    }

    public void Start()
    {
        _host.SetBusy(true);
        _host.SetEstado("Preparando el entorno local");
        _host.SetProgreso("Iniciando Condor…");
    }

    public void Report(StartupProgress progress)
    {
        var label = StageEstado(progress.Stage);

        if (progress.Completed)
        {
            var done = StageCompleted(progress.Stage);
            if (_host.IsWelcome)
            {
                // En la bienvenida no existe aun zona de conversacion: la ultima
                // etapa concluida se refleja en el progreso, sin inventar actividad.
                _lastCompleted = done;
                _host.SetProgreso((_lastCompleted is null ? "" : "✓ " + _lastCompleted + " · ") + Elapsed());
            }
            else if (_archived.Add(progress.Stage))
            {
                _host.AddActivity(done, ActivityKind.System);
            }

            return;
        }

        if (progress.DownloadPercent is { } percent)
        {
            _host.SetEstado(label);
            var tail = _host.IsWelcome && _lastCompleted is not null ? "✓ " + _lastCompleted + " · " : "";
            _host.SetProgreso(tail + Bar(percent) + " " + Math.Round(percent) + "% · " + Elapsed());
            return;
        }

        _host.SetEstado(label + (string.IsNullOrWhiteSpace(progress.Message) ? "" : " — " + progress.Message));
        var progreso = _host.IsWelcome && _lastCompleted is not null ? "✓ " + _lastCompleted + " · " : "";
        _host.SetProgreso(progreso + Elapsed());
    }

    public void Stop(bool success, string? finalLine = null)
    {
        _host.SetBusy(false);
        if (success)
        {
            _host.SetEstado("Entorno listo", ActivityKind.Success);
            _host.SetProgreso(Elapsed());
        }
    }

    /// <summary>Estado concreto por etapa: siempre dice que se esta haciendo.</summary>
    internal static string StageEstado(StartupStage stage) => AgentProgressLabels.StageEstado(stage);

    internal static string StageCompleted(StartupStage stage) => AgentProgressLabels.StageCompleted(stage);

    private string Elapsed() => AgentProgressLabels.FormatElapsed(_clock.Elapsed);

    internal static string Bar(double percent) => AgentProgressLabels.BuildBar(percent);
}

/// <summary>
/// Superficie de progreso del agente sobre la TUI. Cada evento refleja lo que
/// el agente realmente hace (fase, accion concreta, archivo afectado,
/// iteracion); "Verificando" se presenta siempre con su objeto real
/// ("Verificando resultado"), sin actividad inventada.
/// </summary>
public sealed class TuiAgentProgressView : IAgentProgressView
{
    private readonly TuiHost _host;
    private readonly System.Diagnostics.Stopwatch _clock = new();

    public TuiAgentProgressView(TuiHost host)
    {
        _host = host;
    }

    public void Start(string intention)
    {
        _clock.Restart();
        _host.SetBusy(true);
        _host.AddActivity(intention, ActivityKind.User);
        _host.SetEstado("Comprendiendo la solicitud", ActivityKind.System);
        _host.SetProgreso(Elapsed());
    }

    public void Report(AgentProgress progress)
    {
        var estado = PhaseEstado(progress);
        var kind = progress.Flag switch
        {
            ProgressFlag.ProviderError => ActivityKind.Error,
            ProgressFlag.Recovering => ActivityKind.Warning,
            _ => ActivityKind.System
        };

        _host.SetEstado(estado, kind);
        _host.SetProgreso(BuildProgreso(progress));
    }

    public void Stop(bool success, string? finalLine = null)
    {
        _clock.Stop();
        _host.SetBusy(false);
        _host.SetEstado(success ? "Tarea completada" : "Tarea no completada",
            success ? ActivityKind.Success : ActivityKind.Error);
        _host.SetProgreso(Elapsed());
    }

    internal static string PhaseEstado(AgentProgress p) => AgentProgressLabels.PhaseEstado(p);

    private string BuildProgreso(AgentProgress p) => AgentProgressLabels.BuildProgreso(p, _clock.Elapsed);

    private string Elapsed() => AgentProgressLabels.FormatElapsed(_clock.Elapsed);
}