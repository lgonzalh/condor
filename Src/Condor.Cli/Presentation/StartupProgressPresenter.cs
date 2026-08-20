using System;
using System.Collections.Generic;
using Condor.Core.Models;

namespace Condor.Cli.Presentation;

/// <summary>
/// Anime la puesta en marcha de Condor (independiente del progreso de tareas del
/// agente). Muestra el banner, la lista de etapas reales de preparacion y, en la
/// etapa en curso, una animacion indeterminada (spinner) o una barra de progreso
/// SOLO cuando existe un porcentaje real de descarga reportado por Ollama.
/// Nunca inventa porcentajes. Degrada a lineas simples si la salida esta
/// redirigida (p. ej. captura de E2E).
/// </summary>
public sealed class StartupProgressPresenter : IStartupProgressView, IDisposable
{
    private static readonly string[] SpinnerFrames = { "◐", "◓", "◑", "◒" };
    private const string Check = "✓";

    private readonly object _gate = new();
    private System.Threading.Timer? _ticker;
    private int _spin;
    private bool _stopped;
    private bool _interactive;
    private DateTime _startedAt;

    private bool _bannerShown;
    private readonly List<string> _completedLines = new();
    private StartupProgress? _current;
    private int _emittedCompletedLines;
    private (StartupStage Stage, int Percent)? _lastActiveEmission;

    public StartupProgressPresenter()
    {
        _interactive = !Console.IsOutputRedirected;
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_stopped || _bannerShown) return;
            _bannerShown = true;
            _startedAt = DateTime.Now;
            RenderHeader();
            // Desde el primer instante hay una etapa en curso: el spinner debe ser
            // visible mientras se analiza y prepara el entorno. Sin esto, entre el
            // banner y la primera etapa reportada la terminal parece congelada.
            if (_current is null)
            {
                _current = StartupProgress.Of(StartupStage.PreparingEnvironment);
            }
        }

        _ticker = new System.Threading.Timer(_ => Spin(), null, 200, 200);
    }

    public void Report(StartupProgress progress)
    {
        lock (_gate)
        {
            if (_stopped) return;

            if (progress.Completed)
            {
                // Etapa concluida: cierra la actual como correcta y la archiva.
                if (_current is not null)
                {
                    _completedLines.Add(CompletedLine(_current));
                }
                else
                {
                    // Etapa concluida sin estado previo: marcar directa.
                    _completedLines.Add(CompletedLine(progress));
                }

                // Se mantiene una etapa en curso (la misma, en modo "procesando")
                // para que el spinner nunca desaparezca mientras Condor sigue
                // trabajando. Se sustituye cuando llegue la siguiente etapa o al
                // detenerse con Stop; nunca deja la terminal visualmente congelada.
                _current = StartupProgress.Of(progress.Stage);
            }
            else
            {
                // Etapa en curso (nueva o actualizada). Si cambia de etapa una
                // que no concluyo, se descarta la anterior (solo se marcan con
                // ✓ las concluidas) para no mostrar lineas incompletas/falsas.
                _current = progress;
            }

            Redraw();
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

            if (_interactive)
            {
                // Quita solo la linea de la etapa en curso (spinner/barra), pero
                // conserva el banner y las etapas terminadas (✓) como evidencia
                // de que Condor siguio avanzando de forma visible.
                ClearActiveLine();
            }

            Console.WriteLine();
            if (success)
                Terminal.WriteSuccess(finalLine ?? "Condor esta listo.");
            else
                Terminal.WriteWarning(finalLine ?? "Condor no pudo preparar el entorno.");
        }
    }

    private void Spin()
    {
        lock (_gate)
        {
            if (_stopped || !_interactive) return;
            _spin++;
            Redraw();
        }
    }

    private void RenderHeader()
    {
        Console.WriteLine();
        Terminal.WriteBlue("©Condor");
        Terminal.WriteDim("Observa · Comprende · Planifica · Construye · Verifica");
        Console.WriteLine();
        Console.WriteLine();
    }

    private void Redraw()
    {
        if (!_interactive)
        {
            // Salida redirigida (p. ej. captura de E2E): no se puede recargar la
            // zona de la terminal, asi que se emiten lineas SOBRIAS: cada etapa
            // terminada una sola vez y la etapa en curso de forma compacta y solo
            // cuando cambia (etapa o ~1% de descarga) para no inundar la salida.
            if (_emittedCompletedLines < _completedLines.Count)
            {
                for (; _emittedCompletedLines < _completedLines.Count; _emittedCompletedLines++)
                {
                    Console.WriteLine(_completedLines[_emittedCompletedLines]);
                }
            }

            if (_current is not null)
            {
                var key = _current.DownloadPercent is { } pct
                    ? (_current.Stage, (int)Math.Floor(pct))
                    : (_current.Stage, -1);
                if (_lastActiveEmission != key)
                {
                    _lastActiveEmission = key;
                    Console.WriteLine(CompactLine(_current));
                }
            }
            return;
        }

        var lines = BuildLines();
        var height = lines.Count;
        if (height <= 0) return;

        Console.Write("\u001b[" + height + "A");
        foreach (var line in lines)
        {
            Console.Write("\u001b[2K" + line + "\r\n");
        }
    }

    private void ClearActiveLine()
    {
        if (!_interactive) return;
        // La linea activa (si hay alguna etapa en curso) es la ultima del bloque.
        if (_current is null) return;

        Console.Write("\u001b[1A");
        Console.Write("\u001b[2K");
    }

    private List<string> BuildLines()
    {
        var lines = new List<string>();
        foreach (var line in _completedLines)
        {
            if (!string.IsNullOrWhiteSpace(line)) lines.Add(line);
        }

        if (_current is not null)
        {
            lines.Add(ActiveLine(_current));
        }

        return lines;
    }

    private string ActiveLine(StartupProgress p)
    {
        var frame = _interactive ? SpinnerFrames[_spin % SpinnerFrames.Length] : "_";
        var prefix = "  " + frame + " ";
        var time = " · Tiempo: " + FormatElapsed(DateTime.Now - _startedAt);

        if (p.DownloadPercent is { } percent)
        {
            var bar = BuildBar(percent);
            return prefix + StageLabel(p.Stage) + "... " + bar + " " + FormatPercent(percent) + time;
        }

        return prefix + StageLabel(p.Stage) + "... " + (p.Message ?? "") + time;
    }

    private string CompletedLine(StartupProgress p)
    {
        var label = StageCompletedLabel(p.Stage);
        var message = string.IsNullOrWhiteSpace(p.Message) ? "" : ": " + p.Message;
        return "  " + Check + " " + label + message;
    }

    private string CompactLine(StartupProgress p)
    {
        var time = " · Tiempo: " + FormatElapsed(DateTime.Now - _startedAt);
        if (p.DownloadPercent is { } percent)
        {
            return "  " + StageLabel(p.Stage) + "... " + FormatPercent(percent) + time;
        }
        return "  " + StageLabel(p.Stage) + "... " + (p.Message ?? "") + time;
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

    private static string StageLabel(StartupStage stage)
    {
        return stage switch
        {
            StartupStage.PreparingEnvironment => "Preparando entorno",
            StartupStage.ReviewingResources => "Revisando recursos",
            StartupStage.DetectingOllama => "Detectando Ollama",
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
