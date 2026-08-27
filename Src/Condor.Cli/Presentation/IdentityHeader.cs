namespace Condor.Cli.Presentation;

/// <summary>
/// Zona de identidad de Condor en la interfaz interactiva. La identidad tiene
/// DOS elementos permanentes y diferenciados:
///
///   * Superior: "Condor" + eslogan + directorio de trabajo.
///   * Inferior: "©Condor - <modelo local> - <tiempo>". El © solo aparece aqui.
///
/// La cabecera superior se redibuja en cada punto de espera de entrada para que
/// no desaparezca por el desplazamiento de la terminal; el pie inferior
/// acompana esa misma redibujado para mantener la barra de identidad.
/// </summary>
public static class IdentityHeader
{
    private static readonly DateTime SessionStart = DateTime.Now;

    /// <summary>Barra superior: marca + eslogan + directorio, sin ©.</summary>
    public static void Render(string? realModel, string? workingDirectory = null)
    {
        Terminal.WriteWhite("Condor");
        Terminal.WriteDim("Observa · Comprende · Planifica · Construye · Verifica");
        var dir = !string.IsNullOrWhiteSpace(workingDirectory)
            ? workingDirectory
            : Environment.CurrentDirectory;
        Terminal.WriteDim("> " + dir);
        Terminal.WriteDim("------------------------------------------------------");
    }

    /// <summary>Barra inferior: ©Condor - <modelo> - <tiempo>. El © solo aqui.</summary>
    public static void RenderFooter(string? realModel)
    {
        var model = string.IsNullOrWhiteSpace(realModel) ? "modelo local" : realModel;
        Terminal.WriteWhite("©Condor - " + model + " - " + SessionElapsed());
    }

    private static string SessionElapsed()
    {
        var s = (DateTime.Now - SessionStart).TotalSeconds;
        return s < 1 ? "0 s" : s.ToString("0.0") + " s";
    }
}
