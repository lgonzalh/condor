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
        _host.SetModelStatus("Ollama · comprobando el entorno local", working: true);
    }

    public void Report(StartupProgress progress)
    {
        var label = StageEstado(progress.Stage);

        if (progress.Completed)
        {
            var done = StageCompleted(progress.Stage);
            _host.SetModelStatus("Ollama · " + done, working: false, kind: ActivityKind.Success);
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
            _host.SetModelStatus("Modelo local · descargando · " + Math.Round(percent) + "%", working: true);
            return;
        }

        _host.SetEstado(label + (string.IsNullOrWhiteSpace(progress.Message) ? "" : " — " + progress.Message));
        var progreso = _host.IsWelcome && _lastCompleted is not null ? "✓ " + _lastCompleted + " · " : "";
        _host.SetProgreso(progreso + Elapsed());
        _host.SetModelStatus(StartupModelStatus(progress), working: IsModelStage(progress.Stage));
    }

    public void Stop(bool success, string? finalLine = null)
    {
        _host.SetBusy(false);
        _host.SetEstado(success ? "Entorno listo" : "Preparación detenida", success ? ActivityKind.Success : ActivityKind.Error);
        _host.SetProgreso(Elapsed());
        _host.SetModelStatus(success ? "Modelo local · listo" : "Modelo local · no disponible", working: false,
            kind: success ? ActivityKind.Success : ActivityKind.Error);
    }

    /// <summary>Estado concreto por etapa: siempre dice que se esta haciendo.</summary>
    internal static string StageEstado(StartupStage stage) => AgentProgressLabels.StageEstado(stage);

    internal static string StageCompleted(StartupStage stage) => AgentProgressLabels.StageCompleted(stage);

    private static bool IsModelStage(StartupStage stage)
        => stage is StartupStage.EvaluatingModels or StartupStage.SelectingModel or StartupStage.DownloadingModel or StartupStage.VerifyingModel;

    private static string StartupModelStatus(StartupProgress progress)
    {
        var label = StageEstado(progress.Stage);
        var subject = IsModelStage(progress.Stage) ? "Modelo local" : "Ollama";
        return subject + " · " + label + (string.IsNullOrWhiteSpace(progress.Message) ? "" : " — " + progress.Message);
    }

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
        _host.SetModelStatus("Modelo local · en espera", working: false);
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
        if (!string.IsNullOrWhiteSpace(progress.ModelActivity))
            _host.SetModelStatus(progress.ModelActivity, progress.ModelWorking ?? false, kind);
        var detalle = estado;
        if (!string.IsNullOrWhiteSpace(progress.Message))
            detalle += " — " + progress.Message.Trim();
        if (!string.IsNullOrWhiteSpace(progress.Action))
            _host.AddActivity(detalle + (string.IsNullOrWhiteSpace(progress.Path) ? "" : " · " + progress.Path),
                progress.Phase == AgentPhase.Building ? ActivityKind.CodeEdit : ActivityKind.Analysis);
    }

    public void Stop(bool success, string? finalLine = null)
    {
        _clock.Stop();
        _host.SetBusy(false);
        _host.SetEstado(success ? "Tarea completada" : "Tarea no completada",
            success ? ActivityKind.Success : ActivityKind.Error);
        _host.SetProgreso(Elapsed());
        _host.SetModelStatus(success ? "Modelo local · listo para otra tarea" : "Modelo local · interacción detenida",
            working: false, kind: success ? ActivityKind.Success : ActivityKind.Error);
    }

    internal static string PhaseEstado(AgentProgress p) => AgentProgressLabels.PhaseEstado(p);

    private string BuildProgreso(AgentProgress p)
    {
        if (string.IsNullOrWhiteSpace(p.Message))
            return AgentProgressLabels.BuildProgreso(p, _clock.Elapsed);

        var details = new System.Collections.Generic.List<string> { p.Message.Trim() };
        if (p.Iteration is { } iteration) details.Add("Iteración " + iteration);
        if (!string.IsNullOrWhiteSpace(p.ResourceState) && p.AvailableGb is { } free)
            details.Add("RAM libre " + free.ToString("0.0") + " GB");
        details.Add(AgentProgressLabels.FormatElapsed(_clock.Elapsed));
        return string.Join(" · ", details);
    }

    private string Elapsed() => AgentProgressLabels.FormatElapsed(_clock.Elapsed);
}
