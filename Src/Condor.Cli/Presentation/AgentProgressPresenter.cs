using Condor.Core.Contracts;
using Condor.Core.Models;

namespace Condor.Cli.Presentation;

/// <summary>
/// Presentador de progreso del agente en terminal. Muestra una barra de progreso
/// INDETERMINADO (spinner) y el estado real del trabajo (fase, accion, ruta,
/// iteracion y tiempo transcurrido) actualizado en la misma zona de la terminal,
/// sin porcentajes inventados. Degrada a lineas simples si la salida esta
/// redirigida (p. ej. pipelines o captura de E2E).
/// </summary>
public sealed class AgentProgressPresenter : IAgentProgressView, IDisposable
{
    private static readonly string[] Frames = { "◐", "◓", "◑", "◒" };

    private readonly object _gate = new();
    private DateTime _startedAt;
    private int _spin;

    private AgentProgress? _current;
    private System.Threading.Timer? _ticker;
    private bool _stopped;
    private bool _interactive;

    public AgentProgressPresenter()
    {
        _interactive = !Console.IsOutputRedirected;
    }

    public void Start(string intention)
    {
        lock (_gate)
        {
            if (_stopped) return;
            _startedAt = DateTime.Now;
            if (_interactive)
            {
                Console.WriteLine("Condor esta trabajando...");
            }
        }

        _ticker = new System.Threading.Timer(_ => Spin(), null, 250, 250);
    }

    public void Report(AgentProgress progress)
    {
        lock (_gate)
        {
            _current = progress;
            if (!_stopped)
            {
                if (_interactive)
                {
                    Draw(false);
                }
                else
                {
                    // Salida redirigida: una linea compacta por cambio de fase/accion.
                    Console.WriteLine(CompactLine(progress));
                }
            }
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
                // Limpia el bloque transitorio (la linea de cabecera + el bloque de estado).
                Clear();
            }

            Console.WriteLine();
            if (success)
                Terminal.WriteSuccess(finalLine ?? "Condor termino.");
            else
                Terminal.WriteWarning(finalLine ?? "Condor no pudo completar la tarea.");
        }
    }

    private void Spin()
    {
        lock (_gate)
        {
            if (_stopped || !_interactive) return;
            _spin++;
            Draw(false);
        }
    }

    private void Draw(bool spinner)
    {
        var lines = BuildLines();
        var height = lines.Count;

        // Sube "height" lineas y las reescribe en su sitio.
        if (height > 0 && _interactive)
        {
            Console.Write("\u001b[" + height + "A");
        }

        foreach (var line in lines)
        {
            Console.Write("\u001b[2K" + line + "\r\n");
        }
    }

    private void Clear()
    {
        var height = CountBlockHeight();
        if (height > 0)
        {
            Console.Write("\u001b[" + height + "A");
            for (var i = 0; i < height; i++)
            {
                Console.Write("\u001b[2K" + (i < height - 1 ? "\r\n" : ""));
            }
        }
    }

    private int CountBlockHeight()
    {
        // La cabecera "Condor esta trabajando..." (1) + el bloque de estado actual.
        return BuildLines().Count + 1;
    }

    private List<string> BuildLines()
    {
        var p = _current;
        var el = DateTime.Now - _startedAt;
        var elapsed = FormatElapsed(el);
        var frame = _interactive ? Frames[_spin % Frames.Length] : "·";

        // Indicador segun el estado del proveedor/modelo.
        var icon = p?.Flag switch
        {
            ProgressFlag.Recovering => "!",
            ProgressFlag.ProviderError => "X",
            _ => frame
        };

        var lines = new List<string>
        {
            StatusLine(icon, p, elapsed)
        };

        // Lineas suplementarias solo cuando aportan contexto extra, sin perder
        // nunca la linea de estado con el contador de tiempo.
        if (p is not null && p.Flag != ProgressFlag.Normal)
        {
            lines.Add($"  Estado: {ModelStateLabel(p.Flag)}");
            if (!string.IsNullOrWhiteSpace(p.Message))
            {
                lines.Add($"  Detalle: {p.Message}");
            }
        }
        else if (!string.IsNullOrWhiteSpace(p?.Message))
        {
            lines.Add($"  {p!.Message}");
        }
        if (p?.ResourceState is { } rs)
        {
            var budget = p.SafeBudgetGb is { } sb ? $" · Presupuesto seguro: {sb.ToString("0.0")} GB" : "";
            lines.Add($"  Recursos: {p.AvailableGb?.ToString("0.0") ?? "?"} GB disponibles{budget} · Estado: {rs}");
        }
        return lines;
    }

    private static string StatusLine(string icon, AgentProgress? p, string elapsed)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("  ").Append(icon).Append(' ').Append(PhaseLabel(p?.Phase ?? AgentPhase.Understanding));
        if (!string.IsNullOrWhiteSpace(p?.Action))
        {
            sb.Append(" · Acción: ").Append(p.Action);
            if (!string.IsNullOrWhiteSpace(p.Path)) sb.Append(' ').Append(p.Path);
        }
        if (p?.Iteration is { } iter)
        {
            sb.Append(" · Iteración: ").Append(iter);
        }
        sb.Append(" · Tiempo: ").Append(elapsed);
        return sb.ToString();
    }

    private static string FormatElapsed(TimeSpan el)
    {
        return el.TotalHours >= 1
            ? string.Format("{0:00}:{1:00}:{2:00}", (int)el.TotalHours, el.Minutes, el.Seconds)
            : string.Format("{0:00}:{1:00}", el.Minutes, el.Seconds);
    }

    private static string ModelStateLabel(ProgressFlag flag)
        => flag switch
        {
            ProgressFlag.Recovering => "recuperando proveedor",
            ProgressFlag.ProviderError => "proveedor no disponible / detenido",
            _ => "procesando"
        };

    private static string PhaseLabel(AgentPhase phase)
    {
        return phase switch
        {
            AgentPhase.Understanding => "Comprendiendo",
            AgentPhase.Observing => "Observando",
            AgentPhase.Analyzing => "Analizando",
            AgentPhase.Building => "Construyendo",
            AgentPhase.Verifying => "Verificando",
            AgentPhase.Finalizing => "Finalizando",
            _ => "Trabajando"
        };
    }

    private string CompactLine(AgentProgress p)
    {
        var icon = p.Flag switch
        {
            ProgressFlag.Recovering => "!",
            ProgressFlag.ProviderError => "X",
            _ => "·"
        };
        var line = StatusLine(icon, p, FormatElapsed(DateTime.Now - _startedAt));
        if (!string.IsNullOrWhiteSpace(p.Message))
        {
            line += " · " + p.Message;
        }
        return line;
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
