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
///
/// Las etiquetas provienen del origen unico de etiquetas (T-019): la CLI ya no
/// mantiene su propia copia de etiquetas, por lo que la salida redirigida y la
/// linea de estado usan el mismo texto honesto que la TUI.
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
                if (progress.Completed && _finalizedStages.Add(AgentProgressLabels.StageCompletedLabel(progress.Stage)))
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
                if (_finalizedStages.Add(AgentProgressLabels.StageCompletedLabel(progress.Stage)))
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
        var time = "  " + AgentProgressLabels.FormatElapsed(DateTime.Now - _startedAt);

        if (p.DownloadPercent is { } percent)
        {
            var bar = AgentProgressLabels.BuildBar(percent);
            return "  " + frame + " [" + AgentProgressLabels.StageTag(p.Stage) + "] " + AgentProgressLabels.StageLabel(p.Stage) + "... " + bar + " " + AgentProgressLabels.FormatPercent(percent) + time;
        }

        return "  " + frame + " [" + AgentProgressLabels.StageTag(p.Stage) + "] " + AgentProgressLabels.StageLabel(p.Stage) + "... " + (p.Message ?? "") + time;
    }

    private string CompactLine(StartupProgress p)
    {
        var time = "  " + AgentProgressLabels.FormatElapsed(DateTime.Now - _startedAt);
        if (p.DownloadPercent is { } percent)
        {
            return "  [" + AgentProgressLabels.StageTag(p.Stage) + "] " + AgentProgressLabels.StageLabel(p.Stage) + "... " + AgentProgressLabels.FormatPercent(percent) + time;
        }
        return "  [" + AgentProgressLabels.StageTag(p.Stage) + "] " + AgentProgressLabels.StageLabel(p.Stage) + "... " + (p.Message ?? "") + time;
    }

    private string CompletedLine(StartupProgress p)
    {
        var label = AgentProgressLabels.StageCompleted(p.Stage);
        var message = string.IsNullOrWhiteSpace(p.Message) ? "" : ": " + p.Message;
        return "  " + Check + " [" + AgentProgressLabels.StageTag(p.Stage) + "] " + label + message;
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
