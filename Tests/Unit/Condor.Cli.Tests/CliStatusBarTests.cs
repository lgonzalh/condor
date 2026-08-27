using Condor.Cli;
using Condor.Cli.Presentation;

namespace Condor.Cli.Tests;

/// <summary>
/// Pruebas del nucleo testable de la barra de estado fija (T-020 P2/P3): el
/// texto puro BuildFooterText. La E/S (RenderFooter -> Terminal) no se prueba aqui
/// (Terminal es Console-estatico). BuildFooterText es puro y determinista.
/// </summary>
public class CliStatusBarTests
{
    [Fact]
    public void FooterText_contiene_identidad_permanente_y_version()
    {
        var text = CliStatusBar.BuildFooterText(@"C:\repo", "llama3", "Entorno listo");

        Assert.StartsWith("©Condor", text);
        Assert.Contains("©Condor", text);
        Assert.Contains("· > C:\\repo ·", text);
        Assert.Contains("* llama3", text);
        Assert.Contains("Entorno listo", text);
        Assert.Contains(Condor.Cli.VersionInfo.Version, text);
    }

    [Fact]
    public void FooterText_modelo_vacio_muestra_modelo_local()
    {
        var text = CliStatusBar.BuildFooterText(@"C:\repo", "", "Listo");

        Assert.Contains("* modelo local", text);
    }

    [Fact]
    public void FooterText_fallo_agrega_marcador_de_alerta_al_final()
    {
        var text = CliStatusBar.BuildFooterText(@"C:\repo", "llama3", "Error", failed: true);

        Assert.Contains("⚠", text);
        Assert.EndsWith(" · ⚠", text.Trim());
    }

    [Fact]
    public void FooterText_NO_falla_sin_alerta()
    {
        var text = CliStatusBar.BuildFooterText(@"C:\repo", "llama3", "Entorno listo");

        Assert.DoesNotContain("⚠", text);
    }

    [Fact]
    public void FooterText_copyright_solo_en_el_pie_exactamente_una_vez()
    {
        var text = CliStatusBar.BuildFooterText(@"C:\repo", "llama3", "Entorno listo");

        var count = text.Length - text.Replace("©", "").Length;
        Assert.Equal(1, count);
    }

    [Fact]
    public void FooterText_estado_vacio_muestra_entorno_listo()
    {
        var text = CliStatusBar.BuildFooterText(@"C:\repo", "llama3", "");

        Assert.Contains("Entorno listo", text);
    }
}
