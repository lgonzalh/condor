using System.Collections.Generic;
using System.Linq;
using Condor.Core.Evaluation;
using Condor.Core.Models;

namespace Condor.Infrastructure.Detection;

/// <summary>
/// Detecta procesos con alto consumo de RAM. SOLO LECTURA (diagnostico y
/// recomendacion): nunca cierra procesos ni limpia memoria.
/// </summary>
public class ProcessRamDetector
{
    public const double DefaultThresholdGb = 0.5;

    public IReadOnlyList<RamConsumer> DetectTopConsumers(int max = 5, double thresholdGb = DefaultThresholdGb)
    {
        var result = new List<RamConsumer>();
        try
        {
            var candidates = System.Diagnostics.Process.GetProcesses()
                .Where(p => !IsSafeToSkip(p))
                .Select(p =>
                {
                    try
                    {
                        return new { p.ProcessName, p.Id, WorkingGb = p.WorkingSet64 / ModelMemoryBudget.BytesPerGb };
                    }
                    catch
                    {
                        return null;
                    }
                })
                .Where(x => x is not null && x.WorkingGb >= thresholdGb)
                .OrderByDescending(x => x!.WorkingGb)
                .Take(max);

            foreach (var c in candidates)
            {
                if (c is null) continue;
                result.Add(new RamConsumer
                {
                    ProcessName = c.ProcessName,
                    ProcessId = c.Id,
                    WorkingSetGb = System.Math.Round(c.WorkingGb, 1)
                });
            }
        }
        catch
        {
            // Sin listado de procesos; se devuelve vacio sin fallar.
        }

        return result;
    }

    private static bool IsSafeToSkip(System.Diagnostics.Process p)
    {
        // Saltamos los procesos propiamente de Condor para autoreportarse como
        // consumidores y procesos no accesibles sin ruido ni sobrecarga.
        try
        {
            var name = p.ProcessName;
            return string.Equals(name, "Idle", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "System", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "condor", System.StringComparison.OrdinalIgnoreCase) ||
                   string.IsNullOrEmpty(name);
        }
        catch
        {
            return false;
        }
    }
}
