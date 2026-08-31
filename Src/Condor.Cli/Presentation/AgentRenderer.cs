using System.Text;
using Condor.Core.Models;

namespace Condor.Cli.Presentation;

/// <summary>
/// Presentacion humana de la salida del agente: compacta, orientada a acciones
/// y resultado. El contenido completo de los archivos pertenece al contexto
/// interno del agente y NO se imprime en la terminal; aqui solo se muestra el
/// nombre de archivo, el estado y una metrica compacta.
/// </summary>
public static class AgentRenderer
{
    public static void RenderResult(AgentResult result, TimeSpan? elapsed = null)
    {
        // Identidad superior (Condor, blanco, sin ©) + eslogan.
        Terminal.WriteWhite("Condor");
        Terminal.WriteDim("Observa · Comprende · Planifica · Construye · Verifica");

        if (!string.IsNullOrWhiteSpace(result.Objective))
        {
            Terminal.WriteLine();
            Terminal.WriteLine("Tarea: " + result.Objective);
        }

        // Contexto/decisiones de Condor (cian).
        AppendInventoryColor(result.Inventory);
        var observed = result.Steps.Where(IsObservation).Select(s => s.Path)
            .Where(p => !string.IsNullOrWhiteSpace(p) && p != "." && p != "./")
            .Distinct().ToList();
        if (observed.Count > 0)
        {
            Terminal.WriteLine();
            Terminal.WriteCyan("He revisado " + string.Join(", ", observed) + ".");
        }

        // Analisis producido por el modelo (gris) o error real (rojo).
        Terminal.WriteLine();
        if (!result.Success)
        {
            Terminal.WriteError(string.IsNullOrWhiteSpace(result.Reason) ? "No pude completar esta tarea." : result.Reason!.Trim());
        }
        else
        {
            Terminal.WriteDim(string.IsNullOrWhiteSpace(result.Reason) ? "Listo." : result.Reason!.Trim());
        }

        var changes = result.Steps.Where(IsChange).Where(s => s.Success).ToList();
        if (changes.Count > 0)
        {
            var paths = changes.Select(c => string.IsNullOrWhiteSpace(c.Path) ? "(archivo)" : c.Path).Distinct();
            Terminal.WriteLine();
            Terminal.WriteCyan("He modificado " + string.Join(", ", paths) + ".");
        }

        // Pie de respuesta (firma permanente).
        Terminal.WriteLine();
        Terminal.WriteWhite(SignatureLine(result, elapsed));
    }

    /// <summary>
    /// Construye el texto de respuesta humana como UNA CONVERSACION NATURAL: no se
    /// exponen las etapas tecnicas internas como etiquetas obligatorias; se relata
    /// de forma natural lo observado y el analisis. La firmita final documenta el
    /// modelo usado y el tiempo. Separada de la E/S para poder verificarse en pruebas.
    /// </summary>
    public static string BuildResultText(AgentResult result, TimeSpan? elapsed = null)
    {
        var sb = new StringBuilder();

        // Identidad superior de Condor (sin ©) + eslogan.
        sb.AppendLine("Condor");
        sb.AppendLine("Observa · Comprende · Planifica · Construye · Verifica");

        // Tarea que se respondio.
        if (!string.IsNullOrWhiteSpace(result.Objective))
            sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(result.Objective))
            sb.AppendLine("Tarea: " + result.Objective);

        // Inventario breve y natural (opcional).
        AppendInventory(sb, result.Inventory);

        // Archivos observados, en prosa.
        var observed = result.Steps.Where(IsObservation).Select(s => s.Path)
            .Where(p => !string.IsNullOrWhiteSpace(p) && p != "." && p != "./")
            .Distinct().ToList();
        if (observed.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("He revisado " + string.Join(", ", observed) + ".");
        }

        // El corazon de la respuesta: el analisis elaborado.
        sb.AppendLine();
        var summary = string.IsNullOrWhiteSpace(result.Reason) ? "Listo. No tengo mas que anadir por ahora." : result.Reason!.Trim();
        sb.AppendLine(summary);

        // Cambios, si los hubo (en pocas palabras).
        var changes = result.Steps.Where(IsChange).Where(s => s.Success).ToList();
        if (changes.Count > 0)
        {
            var paths = changes.Select(c => string.IsNullOrWhiteSpace(c.Path) ? "(archivo)" : c.Path).Distinct();
            sb.AppendLine();
            sb.AppendLine("He modificado " + string.Join(", ", paths) + ".");
        }

        // Firma permanente del ADN de Condor.
        sb.AppendLine();
        sb.AppendLine(SignatureLine(result, elapsed));

        return sb.ToString();
    }

    private static string SignatureLine(AgentResult result, TimeSpan? elapsed)
    {
        var model = string.IsNullOrWhiteSpace(result.Model) ? "modelo local" : result.Model;
        var time = FormatElapsed(elapsed);
        // Barra inferior de identidad: el © aparece SOLO aqui, nunca arriba.
        return "©Condor - " + model + " - " + time;
    }

    private static string FormatElapsed(TimeSpan? elapsed)
    {
        if (elapsed is not { } e) return "-";
        if (e.TotalMilliseconds < 1000)
            return Math.Max(1, (int)e.TotalMilliseconds) + " ms";
        return e.TotalSeconds.ToString("0.0") + " s";
    }

    private static bool IsObservation(AgentStep s)
        => s.Action is AgentAction.ActionListDir or AgentAction.ActionReadFile or AgentAction.ActionSearch;

    private static bool IsChange(AgentStep s)
        => s.Action is AgentAction.ActionPatch or AgentAction.ActionEditFile or AgentAction.ActionCreateFile or AgentAction.ActionUndoFile;

    /// <summary>Presenta el inventario del entorno y de la decision de modelo, en prosa breve (versión texto).</summary>
    private static void AppendInventory(StringBuilder sb, AgentInventory? inv)
    {
        if (inv is null) return;

        var parts = new List<string>();
        if (inv.RamTotalGb > 0)
            parts.Add("RAM " + inv.RamFreeGb.ToString("0.0") + "/" + inv.RamTotalGb.ToString("0.0") + " GB libres (presupuesto " + inv.SafeBudgetGb.ToString("0.0") + " GB" + (string.IsNullOrWhiteSpace(inv.PressureLabel) ? ")" : ", " + inv.PressureLabel + ")"));
        if (!string.IsNullOrWhiteSpace(inv.Cpu))
            parts.Add(inv.Cpu);
        if (inv.FreeDiskGb > 0)
            parts.Add(inv.FreeDiskGb.ToString("0.0") + " GB libres de disco");
        if (inv.InstalledModels is { Count: > 0 })
            parts.Add("modelos: " + string.Join(", ", inv.InstalledModels));
        if (!string.IsNullOrWhiteSpace(inv.SelectedModel))
            parts.Add("uso " + inv.SelectedModel);
        if (inv.ModelCapabilities is { Count: > 0 })
            parts.Add("capaz de " + string.Join(", ", inv.ModelCapabilities));

        if (parts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Contexto del entorno: " + string.Join(" · ", parts) + ".");
        }
    }

    /// <summary>Presenta el inventario del entorno en color de Condor (cian).</summary>
    private static void AppendInventoryColor(AgentInventory? inv)
    {
        if (inv is null) return;

        var parts = new List<string>();
        if (inv.RamTotalGb > 0)
            parts.Add("RAM " + inv.RamFreeGb.ToString("0.0") + "/" + inv.RamTotalGb.ToString("0.0") + " GB libres (presupuesto " + inv.SafeBudgetGb.ToString("0.0") + " GB" + (string.IsNullOrWhiteSpace(inv.PressureLabel) ? ")" : ", " + inv.PressureLabel + ")"));
        if (!string.IsNullOrWhiteSpace(inv.Cpu))
            parts.Add(inv.Cpu);
        if (inv.FreeDiskGb > 0)
            parts.Add(inv.FreeDiskGb.ToString("0.0") + " GB libres de disco");
        if (inv.InstalledModels is { Count: > 0 })
            parts.Add("modelos: " + string.Join(", ", inv.InstalledModels));
        if (!string.IsNullOrWhiteSpace(inv.SelectedModel))
            parts.Add("uso " + inv.SelectedModel);
        if (inv.ModelCapabilities is { Count: > 0 })
            parts.Add("capaz de " + string.Join(", ", inv.ModelCapabilities));

        if (parts.Count > 0)
        {
            Terminal.WriteLine();
            Terminal.WriteCyan("Contexto del entorno: " + string.Join(" · ", parts) + ".");
        }
    }
}


