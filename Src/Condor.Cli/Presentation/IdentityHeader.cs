namespace Condor.Cli.Presentation;

/// <summary>
/// Zona persistente de identidad de Condor en la interfaz interactiva. Siempre
/// muestra el modelo local REAL que Condor esta utilizando en ese momento
/// (nunca uno sugerido, anterior o supuesto), junto al eslogan y un separador.
/// Se invoca para redibujar esta zona en cada punto de espera de entrada, de modo
/// que la identidad nunca desaparezca por el desplazamiento de la terminal y no
/// se superponga con el resto de la salida.
/// </summary>
public static class IdentityHeader
{
    public static void Render(string? realModel)
    {
        var model = string.IsNullOrWhiteSpace(realModel) ? "modelo local" : realModel;
        Terminal.WriteBlue("©Condor - " + model);
        Terminal.WriteDim("Observa · Comprende · Planifica · Construye · Verifica");
        Terminal.WriteDim("------------------------------------------------------");
    }
}
