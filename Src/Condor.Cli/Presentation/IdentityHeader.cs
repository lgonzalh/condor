using System;

namespace Condor.Cli.Presentation;

/// <summary>
/// Zona de identidad de Condor en la interfaz interactiva. La identidad tiene
/// DOS elementos permanentes y diferenciados:
///
///   * Superior: "Condor" + eslogan + directorio de trabajo (Render). Sin ©.
///   * Inferior: barra de identidad fija (©Condor · > dir · * modelo · estado · versión),
///     implementada por CliStatusBar y redibujada en cada punto de espera de
///     entrada y al final de cada respuesta. El © SOLO aparece en el pie.
///
/// La cabecera superior se redibuja en cada punto de espera de entrada para que
/// no desaparezca por el desplazamiento; el pie (CliStatusBar) acompaña esa misma
/// redibujado para mantener la barra de identidad.
/// </summary>
public static class IdentityHeader
{
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

    /// <summary>Barra inferior fija: ©Condor · > dir · * modelo · estado · versión. El © SOLO aqui.</summary>
    public static void RenderFooter(string? realModel, string? workingDirectory = null, string estado = "Entorno listo", bool failed = false)
    {
        CliStatusBar.RenderFooter(realModel, workingDirectory, estado, failed);
    }
}
