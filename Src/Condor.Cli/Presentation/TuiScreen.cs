using System;

namespace Condor.Cli.Presentation;

/// <summary>
/// Pantalla centralizada de la TUI operacional de Condor. Autoridad UNICA para
/// dibujar durante las esperas: una sola linea de estado que se reescribe en su
/// sitio y una zona de actividad persistente (las lineas concluidas se archivan
/// y quedan en el historico del scroll). Sustituye los redibujados independientes
/// de cada presentador, que peleaban por el cursor y producian salidas rotas.
///
/// Mecanica sin calculos de altura: cuando se archiva una linea, la propia
/// linea de estado se convierte en esa linea de historial y el estado vuelve a
/// escribirse justo debajo. El historial fluye hacia arriba de forma natural.
/// Degrada a lineas simples si la salida esta redirigida (pipelines/E2E).
/// </summary>
public sealed class TuiScreen
{
    private const string ClearLine = "\u001b[2K";

    private readonly object _gate = new();
    private bool _statusReserved;
    private string _currentStatus = "";

    public TuiScreen()
    {
        Interactive = !Console.IsOutputRedirected;
    }

    /// <summary>Instancia compartida por proceso: un solo dueño del cursor.</summary>
    public static TuiScreen Shared { get; } = new();

    public bool Interactive { get; }

    /// <summary>
    /// Reescribe la linea de estado en su sitio. Es la unica zona que parpadea:
    /// todo lo demas es historial persistente o resultado final.
    /// </summary>
    public void SetStatus(string line)
    {
        lock (_gate)
        {
            if (!Interactive)
            {
                return;
            }

            Console.Write("\r" + ClearLine + line);
            _currentStatus = line;
            _statusReserved = true;
        }
    }

    /// <summary>
    /// Archiva una linea en la zona de actividad persistente. Si habia una linea
    /// de estado reservada, esta pasa a ser historial y el estado se reescribe
    /// debajo; el scroll conserva ambas. En salida redirigida imprime directo.
    /// </summary>
    public void ArchiveLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        lock (_gate)
        {
            if (!Interactive)
            {
                Console.WriteLine(line);
                return;
            }

            if (_statusReserved)
            {
                // La linea de estado se convierte en historial; el estado baja una posicion.
                Console.Write("\r" + ClearLine + line + "\r\n");
                Console.Write(_currentStatus);
            }
            else
            {
                Console.WriteLine(line);
            }
        }
    }

    /// <summary>
    /// Libera la linea de estado (fin de espera). El cursor queda al inicio de
    /// una linea limpia, lista para el resultado final. Sin borrados de bloque.
    /// </summary>
    public void EndStatus()
    {
        lock (_gate)
        {
            if (!Interactive)
            {
                return;
            }

            if (_statusReserved)
            {
                Console.Write("\r" + ClearLine);
                _statusReserved = false;
            }
            _currentStatus = "";
        }
    }
}
