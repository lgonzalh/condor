using System;
using System.IO;

namespace Condor.Infrastructure.Setup;

public sealed class StateDirectoryProbe
{
    public StateProbeResult Probe(string stateDirectory)
    {
        if (string.IsNullOrWhiteSpace(stateDirectory))
        {
            return StateProbeResult.Fail("No se pudo determinar el directorio de estado local.");
        }

        if (!Directory.Exists(stateDirectory))
        {
            return StateProbeResult.Fail("El directorio de estado local no existe; Condor lo creara al primer analisis.");
        }

        try
        {
            var files = Directory.GetFiles(stateDirectory);
            return new StateProbeResult(true, stateDirectory, files.Length, null);
        }
        catch (UnauthorizedAccessException)
        {
            return StateProbeResult.Fail("El directorio de estado local no es legible por falta de permisos.");
        }
        catch
        {
            return StateProbeResult.Fail("El directorio de estado local existe pero no puede inspeccionarse.");
        }
    }
}

public readonly record struct StateProbeResult(
    bool Exists,
    string Directory,
    int FileCount,
    string? Reason)
{
    public static StateProbeResult Fail(string reason) => new(false, string.Empty, 0, reason);
}
