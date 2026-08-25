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
    internal static string StageEstado(StartupStage stage)
    {
        return stage switch
        {
            StartupStage.PreparingEnvironment => "Preparando el entorno local",
            StartupStage.ReviewingResources => "Revisando recursos del equipo",
            StartupStage.DetectingOllama => "Detectando Ollama en este equipo",
            StartupStage.BootstrappingDependencies => "Preparando dependencias locales",
            StartupStage.InstallingOllama => "Instalando Ollama",
            StartupStage.StartingOllamaServer => "Iniciando Ollama Server",
            StartupStage.VerifyingOllamaServer => "Verificando disponibilidad de Ollama Server",
            StartupStage.EvaluatingModels => "Evaluando modelos instalados",
            StartupStage.SelectingModel => "Seleccionando modelo adecuado para el equipo",
            StartupStage.DownloadingModel => "Descargando modelo",
            StartupStage.VerifyingModel => "Verificando modelo obtenido",
            StartupStage.Ready => "Entorno listo",
            _ => "Preparando el entorno"
        };
    }

    internal static string StageCompleted(StartupStage stage)
    {
        return stage switch
        {
            StartupStage.PreparingEnvironment => "Entorno preparado",
            StartupStage.ReviewingResources => "Recursos del equipo detectados",
            StartupStage.DetectingOllama => "Ollama detectado",
            StartupStage.BootstrappingDependencies => "Dependencias locales preparadas",
            StartupStage.InstallingOllama => "Ollama instalado",
            StartupStage.StartingOllamaServer => "Ollama Server iniciado",
            StartupStage.VerifyingOllamaServer => "Ollama Server verificado y disponible",
            StartupStage.EvaluatingModels => "Modelos instalados evaluados",
            StartupStage.SelectingModel => "Modelo seleccionado",
            StartupStage.DownloadingModel => "Modelo descargado",
            StartupStage.VerifyingModel => "Modelo verificado",
            StartupStage.Ready => "Entorno listo para trabajar",
            _ => "Etapa completada"
        };
    }

    private string Elapsed()
    {
        var e = _clock.Elapsed;
        return string.Format("{0:00}:{1:00}", (int)e.TotalMinutes, e.Seconds);
    }

    internal static string Bar(double percent)
    {
        const int width = 14;
        var filled = (int)Math.Floor(Math.Clamp(percent, 0, 100) / 100.0 * width);
        return new string('█', filled) + new string('░', width - filled);
    }
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

    internal static string PhaseEstado(AgentProgress p)
    {
        var subject = BuildSubject(p);

        if (p.Flag == ProgressFlag.ProviderError)
        {
            return "El proveedor local no esta disponible ahora";
        }

        if (p.Flag == ProgressFlag.Recovering)
        {
            return "Recuperando el proveedor local" + (subject.Length > 0 ? " — " + subject : "");
        }

        return p.Phase switch
        {
            AgentPhase.Understanding => "Comprendiendo la solicitud",
            AgentPhase.Observing => "Observando el proyecto" + subject,
            AgentPhase.Analyzing => "Analizando lo observado" + subject,
            AgentPhase.Building => "Aplicando cambios" + subject,
            AgentPhase.Verifying => "Verificando resultado de los cambios",
            AgentPhase.Finalizing => "Redactando la respuesta final",
            _ => "Trabajando" + subject
        };
    }

    /// <summary>Objeto concreto de la fase: accion y ruta reales cuando existen.</summary>
    private static string BuildSubject(AgentProgress p)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(p.Action))
        {
            parts.Add(p.Action!);
        }

        if (!string.IsNullOrWhiteSpace(p.Path) && p.Path is not ("." or "./"))
        {
            parts.Add(p.Path!);
        }

        return parts.Count == 0 ? "" : " (" + string.Join(" ", parts) + ")";
    }

    private string BuildProgreso(AgentProgress p)
    {
        var segments = new List<string>();
        if (p.Iteration is { } it)
        {
            segments.Add("Iteracion " + it);
        }

        if (!string.IsNullOrWhiteSpace(p.ResourceState) && p.AvailableGb is { } free)
        {
            var budget = p.SafeBudgetGb is { } safe
                ? ", presupuesto " + safe.ToString("0.0") + " GB"
                : "";
            segments.Add("RAM libre " + free.ToString("0.0") + " GB" + budget + " (" + p.ResourceState + ")");
        }

        if (!string.IsNullOrWhiteSpace(p.Message))
        {
            segments.Add(p.Message!);
        }

        segments.Add(Elapsed());
        return string.Join(" · ", segments);
    }

    private string Elapsed()
    {
        var e = _clock.Elapsed;
        if (e.TotalMinutes >= 60)
        {
            return string.Format("{0:00}:{1:00}:{2:00}", (int)e.TotalHours, e.Minutes, e.Seconds);
        }

        return string.Format("{0:00}:{1:00}", (int)e.TotalMinutes, e.Seconds);
    }
}
