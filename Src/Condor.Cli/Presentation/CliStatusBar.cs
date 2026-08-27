using System;

namespace Condor.Cli.Presentation;

/// <summary>
/// Barra de estado fija de la CLI (T-020 P2).
///
/// Linea de identidad inferior que acompana al usuario entre operaciones:
///   ©Condor · > <directorio> · * <modelo> · <estado> · <versión>
///
/// - El © de la marca aparece SOLO aqui (pie), nunca en la cabecera superior.
/// - Es estatica durante la ejecucion del agente: no se redibuja ni anima mientras
///   el spinner del AgentProgressPresenter ocupa su propia linea, evitando
///   colisiones por el cursor.
/// - Se redibuja en cada punto de espera de entrada (onBeforePrompt) y al final de
///   cada respuesta (ExecuteAsync), por lo que no se pierde por desplazamiento.
/// - BuildFooterText es puro y determinista; RenderFooter separa la E/S (Terminal).
/// </summary>
public static class CliStatusBar
{
    /// <summary>Texto puro y determinista de la barra (testeable; sin colores).</summary>
    public static string BuildFooterText(string? workingDirectory, string? model, string estado, bool failed = false)
    {
        var dir = !string.IsNullOrWhiteSpace(workingDirectory)
            ? workingDirectory
            : Environment.CurrentDirectory;
        var modelo = string.IsNullOrWhiteSpace(model) ? "modelo local" : model;
        var estadoReal = string.IsNullOrWhiteSpace(estado) ? "Entorno listo" : estado;
        var version = Condor.Cli.VersionInfo.Version;
        return "©Condor · > " + dir + " · * " + modelo + " · " + estadoReal + " · " + version + (failed ? " · ⚠" : "");
    }

    /// <summary>Renderiza la barra de estado inferior (© solo en el pie).</summary>
    public static void RenderFooter(string? model, string? workingDirectory = null, string estado = "Entorno listo", bool failed = false)
    {
        Terminal.WriteWhite(BuildFooterText(workingDirectory, model, estado, failed));
    }
}