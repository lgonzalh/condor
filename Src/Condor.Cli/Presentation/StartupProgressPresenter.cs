using System;
using System.Collections.Generic;
using Condor.Core.Models;

namespace Condor.Cli.Presentation;

/// <summary>
/// Presentador de la puesta en marcha de Condor sobre la pantalla centralizada
/// (TuiScreen). Muestra etapas reales con etiqueta de estado ([ENTORNO],
/// [MEMORIA], [OLLAMA], [MODELO]), operacion concreta y tiempo transcurrido.
/// Las etapas concluidas se archivan en la zona de actividad persistente; solo
/// la linea de estado se reescribe. Barra de progreso SOLO con porcentaje real
/// de descarga reportado por Ollama; nunca inventa porcentajes. Degrada a lineas
/// simples si la salida esta redirigida (p. ej. captura de E2E).
/// </summary>
public sealed class StartupProgressPresenter : IStartupProgressView, IDisposable
{
    private static readonly string[] SpinnerFrames = { "◐", "◓", "◑", "◒" };
    private const string Check = "✓";

    private readonly object _gate = new();
    private readonly TuiScreen _screen;
    private System.Threading.Timer? _ticker;
    private int _spin;
    private bool _stopped;
    private DateTime _startedAt;

    private bool _started;
    private readonly HashSet<string> _finalizedStages = new();
    private StartupProgress? _current;
    private (StartupStage Stage, int Percent)? _lastActiveEmission;

    public StartupProgressPresenter() : this(TuiScreen.Shared)
    {
    }

    public StartupProgressPresenter(TuiScreen screen)
    {
        _screen = screen;
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_stopped || _started) return;
            _started = true;
            _startedAt = DateTime.Now;
            // Desde el primer instante hay una etapa en curso visible: la terminal
            // nunca parece congelada entre el banner y la primera etapa real.
            if (_current is null)
            {
                _current = StartupProgress.Of(StartupStage.PreparingEnvironment);
            }
        }

        if (_screen.Interactive)
        {
            _ticker = new System.Threading.Timer(_ => Spin(), null, 200, 200);
        }
        else
        {
            Report(_current!);
        }
    }

    public void Report(StartupProgress progress)
    {
        lock (_gate)
        {
            if (_stopped) return;

            if (!_screen.Interactive)
            {
                // Salida redirigida (captura de E2E / pipelines): lineas sobrias,
                // cada etapa terminada una sola vez y la etapa en curso solo cuando
                // cambia (etapa o ~1% de descarga) para no inundar la salida.
                if (progress.Completed && _finalizedStages.Add(StageCompletedLabel(progress.Stage)))
                {
                    Console.WriteLine(CompletedLine(progress));
                }

                var key = progress.DownloadPercent is { } pct
                    ? (progress.Stage, (int)Math.Floor(pct))
                    : (progress.Stage, -1);
                if (!progress.Completed && _lastActiveEmission != key)
                {
                    _lastActiveEmission = key;
                    Console.WriteLine(CompactLine(progress));
                }
                _current = progress;
                return;
            }

            // Interactivo: las etapas concluidas se archivan UNA vez en la zona de
            // actividad persistente; la linea de estado muestra la etapa en curso.
            if (progress.Completed)
            {
                if (_finalizedStages.Add(StageCompletedLabel(progress.Stage)))
                {
                    _screen.ArchiveLine(CompletedLine(progress));
                }
                _current = StartupProgress.Of(progress.Stage);
                _screen.SetStatus(ActiveLine(_current));
                return;
            }

            _current = progress;
            _screen.SetStatus(ActiveLine(_current));
        }
    }

    public void Stop(bool success, string? finalLine = null)
    {
        lock (_gate)
        {
            if (_stopped) return;
            _stopped = true;
            _ticker?.Dispose();
            _ticker = null;

            _screen.EndStatus();
            Console.WriteLine();
        }
    }

    private void Spin()
    {
        lock (_gate)
        {
            if (_stopped) return;
            _spin++;
            if (_current is not null)
            {
                _screen.SetStatus(ActiveLine(_current));
            }
        }
    }

    private string ActiveLine(StartupProgress p)
    {
        var frame = _screen.Interactive ? SpinnerFrames[_spin % SpinnerFrames.Length] : "_";
        var time = "  " + FormatElapsed(DateTime.Now - _startedAt);

        if (p.DownloadPercent is { } percent)
        {
            var bar = BuildBar(percent);
            return "  " + frame + " [" + StageTag(p.Stage) + "] " + StageLabel(p.Stage) + "... " + bar + " " + FormatPercent(percent) + time;
        }

        return "  " + frame + " [" + StageTag(p.Stage) + "] " + StageLabel(p.Stage) + "... " + (p.Message ?? "") + time;
    }

    private string CompactLine(StartupProgress p)
    {
        var time = "  " + FormatElapsed(DateTime.Now - _startedAt);
        if (p.DownloadPercent is { } percent)
        {
            return "  [" + StageTag(p.Stage) + "] " + StageLabel(p.Stage) + "... " + FormatPercent(percent) + time;
        }
        return "  [" + StageTag(p.Stage) + "] " + StageLabel(p.Stage) + "... " + (p.Message ?? "") + time;
    }

    private string CompletedLine(StartupProgress p)
    {
        var label = StageCompletedLabel(p.Stage);
        var message = string.IsNullOrWhiteSpace(p.Message) ? "" : ": " + p.Message;
        return "  " + Check + " [" + StageTag(p.Stage) + "] " + label + message;
    }

    private static string BuildBar(double percent)
    {
        const int width = 10;
        var filled = (int)Math.Floor(percent / 100.0 * width);
        if (filled < 0) filled = 0;
        if (filled > width) filled = width;
        return new string('█', filled) + new string('░', width - filled);
    }

    private static string FormatPercent(double percent)
    {
        return Math.Round(percent) + "%";
    }

    private static string FormatElapsed(TimeSpan el)
    {
        return el.TotalHours >= 1
            ? string.Format("{0:00}:{1:00}:{2:00}", (int)el.TotalHours, el.Minutes, el.Seconds)
            : string.Format("{0:00}:{1:00}", el.Minutes, el.Seconds);
    }

    /// <summary>Estado operacional real por etapa (nunca un generico sin detalle).</summary>
    private static string StageTag(StartupStage stage)
    {
        return stage switch
        {
            StartupStage.PreparingEnvironment => "ENTORNO",
            StartupStage.ReviewingResources => "MEMORIA",
            StartupStage.DetectingOllama => "OLLAMA",
            StartupStage.BootstrappingDependencies => "ENTORNO",
            StartupStage.InstallingOllama => "OLLAMA",
            StartupStage.StartingOllamaServer => "OLLAMA",
            StartupStage.VerifyingOllamaServer => "OLLAMA",
            StartupStage.EvaluatingModels => "MODELO",
            StartupStage.SelectingModel => "MODELO",
            StartupStage.DownloadingModel => "MODELO",
            StartupStage.VerifyingModel => "VERIFICACION",
            StartupStage.Ready => "DECISION",
            _ => "ENTORNO"
        };
    }

    private static string StageLabel(StartupStage stage)
    {
        return stage switch
        {
            StartupStage.PreparingEnvironment => "Preparando entorno",
            StartupStage.ReviewingResources => "Revisando recursos",
            StartupStage.DetectingOllama => "Detectando Ollama",
            StartupStage.BootstrappingDependencies => "Preparando dependencias",
            StartupStage.InstallingOllama => "Instalando Ollama",
            StartupStage.StartingOllamaServer => "Iniciando Ollama Server",
            StartupStage.VerifyingOllamaServer => "Verificando Ollama Server",
            StartupStage.EvaluatingModels => "Evaluando modelos",
            StartupStage.SelectingModel => "Seleccionando modelo",
            StartupStage.DownloadingModel => "Descargando modelo",
            StartupStage.VerifyingModel => "Verificando modelo",
            StartupStage.Ready => "Entorno listo",
            _ => "Preparando"
        };
    }

    private static string StageCompletedLabel(StartupStage stage)
    {
        return stage switch
        {
            StartupStage.PreparingEnvironment => "Entorno preparado",
            StartupStage.ReviewingResources => "Recursos detectados",
            StartupStage.DetectingOllama => "Ollama disponible",
            StartupStage.BootstrappingDependencies => "Dependencias preparadas",
            StartupStage.InstallingOllama => "Ollama instalado",
            StartupStage.StartingOllamaServer => "Ollama Server iniciado",
            StartupStage.VerifyingOllamaServer => "Ollama Server disponible",
            StartupStage.EvaluatingModels => "Modelos evaluados",
            StartupStage.SelectingModel => "Modelo seleccionado",
            StartupStage.DownloadingModel => "Modelo descargado",
            StartupStage.VerifyingModel => "Modelo verificado",
            StartupStage.Ready => "Entorno listo",
            _ => "Entorno listo"
        };
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (!_stopped)
            {
                _stopped = true;
                _ticker?.Dispose();
                _ticker = null;
            }
        }
    }
}
