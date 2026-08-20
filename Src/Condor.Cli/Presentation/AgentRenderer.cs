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
    public static void RenderResult(AgentResult result)
    {
        Terminal.WriteLine(BuildResultText(result));
    }

    /// <summary>
    /// Construye el texto de presentacion humana (compacto, sin volcados de
    /// archivos). Separado de la E/S para poder verificarse en pruebas.
    /// </summary>
    public static string BuildResultText(AgentResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("CONDOR");

        // Cabecera breve.
        sb.AppendLine("  Estado  : " + (result.Success ? "ok" : "no completo"));
        if (!string.IsNullOrWhiteSpace(result.Objective)) sb.AppendLine("  Tarea   : " + result.Objective);

        // Bloque 0: INVENTARIO del entorno y de la decision de modelo.
        AppendInventory(sb, result.Inventory);

        // Bloque 1: PROGRESO / ACCIONES.
        var actions = result.Steps.Where(s => !IsObservation(s) && s.Action != AgentAction.ActionDone).ToList();
        if (actions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("[PROGRESO]");
            foreach (var step in actions)
            {
                var falla = !step.Success;
                var ruta = (step.Action == AgentAction.ActionBuild || step.Action == AgentAction.ActionTest ||
                           step.Action == AgentAction.ActionRestore)
                    ? ""
                    : (string.IsNullOrWhiteSpace(step.Path) ? "" : " " + step.Path);
                sb.AppendLine("  " + (falla ? "✕" : "✓") + " " + step.Action + ruta + (falla ? " (" + ShortLine(step.ResultPreview, 60) + ")" : ""));
            }
        }

        // Bloque 2: ANALISIS (archivos observados).
        var observed = result.Steps.Where(IsObservation).Select(s => s.Path)
            .Where(p => !string.IsNullOrWhiteSpace(p) && p != "." && p != "./")
            .Distinct().ToList();
        if (observed.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("[ANALISIS]");
            foreach (var f in observed)
                sb.AppendLine("  ✓ " + f);
        }

        // Bloque 2b: HALLAZGOS -> EVIDENCIA objetiva recogida durante la
        // observacion (archivos con contenido inspeccionado y datos puntuales de
        // los resultados de las herramientas). NO es la sinopsis del modelo: la
        // conclusion elaborada va en [RESULTADO]. Asi [HALLAZGOS] y [RESULTADO]
        // provienen del flujo correspondiente y no se repiten.
        var findings = ExtractFindings(result);
        if (findings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("[HALLAZGOS]");
            foreach (var f in findings)
                sb.AppendLine("  - " + f);
        }

        // Bloque 3: CAMBIOS.
        var changes = result.Steps.Where(IsChange).Where(s => s.Success).ToList();
        if (changes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("[CAMBIOS]");
            foreach (var c in changes)
            {
                var ruta = string.IsNullOrWhiteSpace(c.Path) ? "(archivo)" : c.Path;
                sb.AppendLine("  M " + ruta + "  " + MetricLine(c.ResultPreview));
            }
        }

        // Bloque 4: VERIFICACION.
        var verifications = result.Steps.Where(s => IsVerification(s.Action)).ToList();
        if (verifications.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("[VERIFICACION]");
            foreach (var v in verifications)
            {
                var ok = v.Success;
                sb.AppendLine("  " + (ok ? "✓" : "✕") + " " + Title(v.Action) + (ok ? "" : " (" + ShortLine(v.ResultPreview, 80) + ")"));
            }
        }

        // Bloque 5: RESULTADO.
        sb.AppendLine();
        sb.AppendLine("[RESULTADO]");
        var summary = result.Reason;
        sb.AppendLine("  " + (string.IsNullOrWhiteSpace(summary) ? "Condor completo." : summary.Trim()));

        sb.AppendLine();
        sb.AppendLine("  " + result.Steps.Count + " accion(es) de herramienta · " + (result.Checkpoint?.Iteration.ToString() ?? "-") + " iteracion(es) de decision");

        return sb.ToString();
    }

    private static bool IsObservation(AgentStep s)
        => s.Action is AgentAction.ActionListDir or AgentAction.ActionReadFile or AgentAction.ActionSearch;

    private static bool IsChange(AgentStep s)
        => s.Action is AgentAction.ActionPatch or AgentAction.ActionEditFile or AgentAction.ActionCreateFile or AgentAction.ActionUndoFile;

    private static bool IsVerification(string action)
        => action is AgentAction.ActionBuild or AgentAction.ActionTest or AgentAction.ActionRestore;

    /// <summary>Presenta el inventario del entorno y de la decision de modelo.</summary>
    private static void AppendInventory(StringBuilder sb, AgentInventory? inv)
    {
        if (inv is null) return;

        var lines = new List<string>();
        if (inv.RamTotalGb > 0)
            lines.Add("  RAM: " + inv.RamFreeGb.ToString("0.0") + " / " + inv.RamTotalGb.ToString("0.0") + " GB libres · presupuesto seguro " + inv.SafeBudgetGb.ToString("0.0") + " GB" + (string.IsNullOrWhiteSpace(inv.PressureLabel) ? "" : " · " + inv.PressureLabel));
        if (!string.IsNullOrWhiteSpace(inv.Cpu))
            lines.Add("  CPU: " + inv.Cpu);
        if (inv.FreeDiskGb > 0)
            lines.Add("  Disco libre: " + inv.FreeDiskGb.ToString("0.0") + " GB");
        if (inv.InstalledModels is { Count: > 0 })
            lines.Add("  Modelos instalados: " + string.Join(", ", inv.InstalledModels));
        if (!string.IsNullOrWhiteSpace(inv.SelectedModel))
            lines.Add("  Modelo: " + inv.SelectedModel + (string.IsNullOrWhiteSpace(inv.SelectionReason) ? "" : " — " + ShortLine(inv.SelectionReason, 90)));
        if (inv.ModelCapabilities is { Count: > 0 })
            lines.Add("  Capacidades: " + string.Join(", ", inv.ModelCapabilities));

        if (lines.Count == 0) return;
        sb.AppendLine();
        sb.AppendLine("[INVENTARIO]");
        foreach (var l in lines)
            sb.AppendLine(l);
    }

    /// <summary>
    /// EVIDENCIA objetiva recogida durante la observacion/analisis, a partir de
    /// los pasos reales de herramienta (list_dir, read_file, search). Devuelve
    /// enunciados puntuales y verificables, NUNCA la sintesis/razonamiento del
    /// modelo (que pertenece a [RESULTADO]).
    /// </summary>
    private static List<string> ExtractFindings(AgentResult result)
    {
        var items = new List<string>();

        foreach (var s in result.Steps.Where(s => s.Success && IsObservation(s)))
        {
            if (s.Action == AgentAction.ActionListDir && !string.IsNullOrWhiteSpace(s.Path) &&
                s.Path != "." && s.Path != "./")
            {
                items.Add("Existe el directorio '" + s.Path + "'.");
            }
            else if (s.Action == AgentAction.ActionReadFile && !string.IsNullOrWhiteSpace(s.Path) && s.Path != ".")
            {
                var info = DescribeReading(s.Path, s.ResultPreview);
                items.Add(info);
            }
            else if (s.Action == AgentAction.ActionSearch && !string.IsNullOrWhiteSpace(s.ResultPreview))
            {
                items.Add("Busqueda: " + ShortLine(s.ResultPreview, 120));
            }
        }

        return items.Distinct().ToList();
    }

    private static string DescribeReading(string path, string? preview)
    {
        var ext = System.IO.Path.GetExtension(path).TrimStart('.');
        var lang = TheLanguage(ext);
        if (!string.IsNullOrWhiteSpace(lang))
            return "Se inspecciono '" + path + "' (" + lang + ").";
        return "Se inspecciono '" + path + "'.";
    }

    private static string TheLanguage(string ext)
    {
        return ext.ToLowerInvariant() switch
        {
            "cs" => "codigo C#",
            "py" => "codigo Python",
            "ts" or "js" or "jsx" or "tsx" => "codigo JavaScript/TypeScript",
            "go" => "codigo Go",
            "rs" => "codigo Rust",
            "java" => "codigo Java",
            "csproj" or "fsproj" or "vbproj" => "manifiesto de proyecto .NET",
            "sln" or "slnx" => "solucion .NET",
            "json" => "JSON",
            "md" => "documento Markdown",
            "txt" => "texto",
            "html" or "htm" => "markup HTML",
            "css" or "scss" => "hoja de estilo",
            "xml" => "XML",
            "yaml" or "yml" => "YAML",
            _ => ""
        };
    }

    private static string MetricLine(string? preview)
    {
        // Solo una metrica compacta (nunca el contenido completo).
        if (string.IsNullOrWhiteSpace(preview)) return "0 cambios";
        var s = preview;
        var plus = CountOccurrences(s, '+');
        var minus = CountOccurrences(s, '-');
        return "+" + plus + " / -" + minus + " lineas estimadas";
    }

    private static int CountOccurrences(string s, char c)
    {
        var n = 0;
        foreach (var ch in s)
            if (ch == c) n++;
        return n;
    }

    private static string ShortLine(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var clean = s.Replace("\r\n", " ").Replace("\n", " ").Trim();
        if (clean.Length > max) clean = clean.Substring(0, max) + "…";
        return clean;
    }

    private static string Title(string action) => action switch
    {
        AgentAction.ActionBuild => "Build",
        AgentAction.ActionTest => "Tests",
        AgentAction.ActionRestore => "Restore",
        _ => action
    };
}
