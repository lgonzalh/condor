using Condor.Core.Contracts;
using Condor.Core.Models;

namespace Condor.Cli.Presentation;

/// <summary>
/// Presentador de progreso del agente sobre la pantalla centralizada (TuiScreen).
/// Una sola linea de estado reescrita en su sitio con el estado real del trabajo
/// (etiqueta operacional [SOLICITUD]/[AGENTE]/[VERIFICACION]/[RESPUESTA],
/// operacion concreta, mensaje y tiempo transcurrido). Sin porcentajes
/// inventados y sin redibujados de bloque que peleaban por el cursor. Degrada a
/// lineas compactas deduplicadas si la salida esta redirigida (pipelines/E2E).
/// </summary>
public sealed class AgentProgressPresenter : IAgentProgressView, IDisposable
{
    private static readonly string[] Frames = { "◐", "◓", "◑", "◒" };

    private readonly object _gate = new();
    private readonly TuiScreen _screen;
    private DateTime _startedAt;
    private int _spin;
    private System.Threading.Timer? _ticker;
    private bool _stopped;
    private bool _started;

    private AgentProgress? _current;

    public AgentProgressPresenter() : this(TuiScreen.Shared)
    {
    }

    public AgentProgressPresenter(TuiScreen screen)
    {
        _screen = screen;
    }

    public void Start(string intention)
    {
        lock (_gate)
        {
            if (_stopped || _started) return;
            _started = true;
            _startedAt = DateTime.Now;
        }

        if (_screen.Interactive)
        {
            _ticker = new System.Threading.Timer(_ => Spin(), null, 250, 250);
        }
    }

    public void Report(AgentProgress progress)
    {
        lock (_gate)
        {
            _current = progress;
            if (_stopped) return;

            if (_screen.Interactive)
            {
                _screen.SetStatus(StatusLine(Icon(progress, interactive: true), progress, Elapsed()));
            }
            else
            {
                // Salida redirigida: una linea compacta SOLO cuando cambia el
                // contenido real (fase/accion/ruta/iteracion/banderas), no cada tick.
                var line = StatusLine(Icon(progress, interactive: false), progress, Elapsed());
                if (line != _lastRedirectedLine)
                {
                    _lastRedirectedLine = line;
                    Console.WriteLine(line);
                }
            }
        }
    }

    private string? _lastRedirectedLine;

    public void Stop(bool success, string? finalLine = null)
    {
        lock (_gate)
        {
            if (_stopped) return;
            _stopped = true;
            _ticker?.Dispose();
            _ticker = null;

            // Libera la linea de estado; el resultado lo renderiza AgentRenderer.
            // El historial archivado permanece visible en la zona persistente.
            _screen.EndStatus();
            Console.WriteLine();
        }
    }

    private void Spin()
    {
        lock (_gate)
        {
            if (_stopped || !_screen.Interactive) return;
            _spin++;
            if (_current is not null)
            {
                _screen.SetStatus(StatusLine(Frames[_spin % Frames.Length].ToString(), _current, Elapsed()));
            }
        }
    }

    private string Elapsed() => FormatElapsed(DateTime.Now - _startedAt);

    private static string Icon(AgentProgress p, bool interactive)
    {
        return p.Flag switch
        {
            ProgressFlag.Recovering => "!",
            ProgressFlag.ProviderError => "X",
            _ => interactive ? Frames[0] : "·"
        };
    }

    private static string StatusLine(string icon, AgentProgress? p, string elapsed)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("  ").Append(icon).Append(" [" + PhaseTag(p?.Phase ?? AgentPhase.Understanding) + "] ")
          .Append(PhaseLabel(p?.Phase ?? AgentPhase.Understanding));
        // Estado concreto: si hay mensaje, SIEMPRE se muestra (nunca una fase
        // generica sin detalle). Es lo que hace honesta la espera o el bloqueo.
        if (!string.IsNullOrWhiteSpace(p?.Message))
        {
            sb.Append(" - ").Append(p.Message);
        }
        if (!string.IsNullOrWhiteSpace(p?.Action) && !string.IsNullOrWhiteSpace(p.Path))
        {
            sb.Append(' ').Append(p.Path);
        }
        sb.Append(' ').Append(elapsed);
        return sb.ToString();
    }

    private static string FormatElapsed(TimeSpan el)
    {
        return el.TotalHours >= 1
            ? string.Format("{0:00}:{1:00}:{2:00}", (int)el.TotalHours, el.Minutes, el.Seconds)
            : string.Format("{0:00}:{1:00}", el.Minutes, el.Seconds);
    }

    /// <summary>Estado operacional real por fase del agente.</summary>
    private static string PhaseTag(AgentPhase phase)
    {
        return phase switch
        {
            AgentPhase.Understanding => "SOLICITUD",
            AgentPhase.Observing => "AGENTE",
            AgentPhase.Analyzing => "AGENTE",
            AgentPhase.Building => "AGENTE",
            AgentPhase.Verifying => "VERIFICACION",
            AgentPhase.Finalizing => "RESPUESTA",
            _ => "AGENTE"
        };
    }

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
