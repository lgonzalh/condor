namespace Condor.Cli.Presentation;

/// <summary>
/// Zona de identidad de Condor en la interfaz interactiva. La identidad tiene
/// DOS elementos permanentes y diferenciados:
///
///   * Superior: "Condor" + eslogan. Sin ©.
///   * Inferior: "©Condor - &lt;modelo local&gt; - &lt;tiempo&gt;". El © solo aparece aqui.
///
/// La cabecera superior se redibuja en cada punto de espera de entrada para que
/// no desaparezca por el desplazamiento de la terminal; el pie inferior
/// acompaña esa misma redibujado para mantener la barra de identidad.
/// </summary>
public static class IdentityHeader
{
    private static readonly DateTime SessionStart = DateTime.Now;

    /// <summary>Barra superior: marca + eslogan, sin ©.</summary>
    public static void Render(string? realModel)
    {
        Terminal.WriteBlue("Condor");
        Terminal.WriteDim("Observa · Comprende · Planifica · Construye · Verifica");
        Terminal.WriteDim("------------------------------------------------------");
    }

    /// <summary>Barra inferior: ©Condor - &lt;modelo&gt; - &lt;tiempo&gt;. El © solo aqui.</summary>
    public static void RenderFooter(string? realModel)
    {
        var model = string.IsNullOrWhiteSpace(realModel) ? "modelo local" : realModel;
        Terminal.WriteBlue("©Condor - " + model + " - " + SessionElapsed());
    }

    private static string SessionElapsed()
    {
        var s = (DateTime.Now - SessionStart).TotalSeconds;
        return s < 1 ? "0 s" : s.ToString("0.0") + " s";
    }
}
