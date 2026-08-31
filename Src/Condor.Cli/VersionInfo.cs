namespace Condor.Cli;

/// <summary>
/// UNICO ORIGEN DE VERDAD de la version de Condor.
/// La version publica permanece 1.0; el build interno es experimental (α.XX)
/// y NO altera la version publica. Este archivo es la unica fuente para ambos.
/// </summary>
public static class VersionInfo
{
    /// <summary>Version publica de Condor.</summary>
    public const string PublicVersion = "1.0";

    /// <summary>Build interno experimental (α.01, α.02, ...).</summary>
    public const string InternalBuild = "α.03";

    /// <summary>Forma visible: "v1.0 · build interno α.01".</summary>
    public const string DisplayName = "v" + PublicVersion + " · build interno " + InternalBuild;

    /// <summary>Version larga (retrocompatible con usos previos).</summary>
    public const string Version = "1.0.0";

    public const string Product = "Condor";
    public const string Tagline = "Observa · Comprende · Planifica · Construye · Verifica";
}
