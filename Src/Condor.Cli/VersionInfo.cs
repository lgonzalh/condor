namespace Condor.Cli;

/// <summary>
/// UNICO ORIGEN DE VERDAD de la version de Condor.
/// El build visible se identifica como v1.0.0-gamma (Build Gamma).
/// Este archivo es la unica fuente para las cadenas de version.
/// </summary>
public static class VersionInfo
{
    /// <summary>Version publica de Condor.</summary>
    public const string PublicVersion = "1.0";

    /// <summary>Build interno actual.</summary>
    public const string InternalBuild = "Gamma";

    /// <summary>Forma visible solicitada para Build Gamma.</summary>
    public const string DisplayName = "v1.0.0-gamma (Build " + InternalBuild + ")";

    /// <summary>Version larga (retrocompatible con usos previos).</summary>
    public const string Version = "1.0.0";

    public const string Product = "Condor";
    public const string Tagline = "Observa · Comprende · Planifica · Construye · Verifica";
}
