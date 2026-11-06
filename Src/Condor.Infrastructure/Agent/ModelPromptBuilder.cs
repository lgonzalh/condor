namespace Condor.Infrastructure.Agent;

using Condor.Core.Evaluation;
using Condor.Core.Models;

/// <summary>
/// Adapta el prompt del sistema al modelo SELECCIONADO. El "prompt magico" no es
/// estatico ni identico para todos los modelos: se ajusta a las capacidades
/// conocidas (tool-use, salida estructurada, nivel de codigo, contexto) para
/// maximizar la fiabilidad del modelo concreto bajo el presupuesto actual.
///
/// Regla: no se inventa capacidad; SOLO se usa lo que el catalogo/inventario
/// conoce del modelo. Si el modelo es pequeno y no soporta tool-use, se reduce
/// el conjunto de acciones y se refuerza el formato JSON. Si soporta salida
/// estructurada, se pide JSON estricto; en otro caso se permite una respuesta
/// mas guiada.
/// </summary>
public static class ModelPromptBuilder
{
    private const string BaseIdentity =
        "Eres el agente de ingenieria local de Condor. Resuelves la tarea sobre el directorio {0}.";

    /// <summary>
    /// Adapta el prompt base del agente a las capacidades del modelo.
    /// </summary>
    public static string BuildSystemPrompt(string workingDir, string? manifest, ModelCandidate? model)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(string.Format(BaseIdentity, workingDir ?? "(directorio actual)"));

        if (!string.IsNullOrWhiteSpace(manifest))
            sb.AppendLine("Se detecto un proyecto con manifest ('" + manifest + "'). Si la tarea requiere compilar/probar y el ecosistema lo permite, usa build/test; para modificaciones edita los archivos reales.");
        else
            sb.AppendLine("No se asume ningun ecosistema: observa el contenido real con list_dir y read_file para descubrir que existe. Solo usa build/test si el ecosistema lo permite y la tarea lo requiere; una solicitud de comprension/analisis no requiere compilar: describe e interpreta lo que realmente encuentres.");
        sb.AppendLine();

        var structured = model?.StructuredOutput ?? true;
        var toolUse = model?.ToolUse ?? true;
        var multiFile = (model?.MultiFileLevel ?? 0) >= 2;

        if (structured)
        {
            sb.AppendLine("Devuelve UNICAMENTE un JSON valido por paso, sin texto extra, con esta forma:");
            sb.AppendLine("{\"action\": \"<accion>\", \"path\": \"<ruta relativa>\", \"original\": \"<texto exacto>\", \"replacement\": \"<texto nuevo>\", \"content\": \"<contenido o vacio>\", \"reason\": \"<breve explicacion>\"}");
        }
        else
        {
            sb.AppendLine("Responde SIEMPRE con texto breve y claro, sin JSON ni etiquetas tecnicas. Indica el siguiente paso o tu analisis en prosa directa.");
        }
        sb.AppendLine();

        sb.AppendLine("Acciones permitidas:");
        sb.AppendLine("  list_dir  \"path\"  -> listar el contenido de un directorio (rutas relativas).");
        sb.AppendLine("  read_file \"path\"  -> leer el contenido exacto de un archivo.");
        if (toolUse)
        {
            sb.AppendLine("  create_file \"path\" + \"content\" -> crear un archivo nuevo; no sobrescribe uno existente.");
            sb.AppendLine("  edit_file \"path\" + \"content\" -> reemplazar el contenido de un archivo existente.");
            sb.AppendLine("  patch \"path\" + \"original\" + \"replacement\" -> reemplazo quirurgico en un archivo existente.");
            sb.AppendLine("  build -> compilar el proyecto cuando su ecosistema lo permita y la tarea lo requiera.");
            sb.AppendLine("  test -> ejecutar las pruebas del proyecto cuando existan y la tarea lo requiera.");
            sb.AppendLine("  restore -> restaurar dependencias cuando el build o test lo requieran.");
            sb.AppendLine("  git_status -> estado del repositorio.");
            sb.AppendLine("  search \"content\" -> buscar texto en el proyecto.");
            sb.AppendLine("  undo_file \"path\" -> revertir la ultima edicion sobre un archivo.");
            sb.AppendLine("Usa exactamente un nombre de accion de esta lista por JSON; nunca combines acciones con '/' ni inventes variantes.");
        }
        else
        {
            sb.AppendLine("  (este modelo NO ejecuta herramientas externas; usa la observacion directa con list_dir/read_file y razona en texto.)");
        }
        sb.AppendLine("  done -> termina cuando creas que la tarea esta resuelta.");
        sb.AppendLine();

        sb.AppendLine("FLUJO: respeta las rutas exactas escritas por el usuario y resuelvelas desde la raiz del proyecto; no anadas prefijos de carpeta. Si una tarea pide actualizar una ruta concreta, empieza con read_file sobre esa ruta. Si no se nombro una ruta, lista primero la raiz con list_dir y luego observa los archivos necesarios. No inventes rutas distintas a las solicitadas o a las que viste en list_dir/read_file.");
        sb.AppendLine();
        sb.AppendLine("MINIMALISMO TECNICO: elige la solucion mas pequena que cumpla los criterios de la tarea; no agregues componentes, abstracciones ni cambios fuera de alcance sin una necesidad justificada.");
        sb.AppendLine();
        sb.AppendLine("ALTERNATIVAS: si hay varias soluciones viables con diferencias reales de alcance, riesgo, costo o mantenimiento, genera como maximo tres alternativas basadas en el codigo observado; compara sus diferencias, ventajas y limitaciones y recomienda la que mejor cumple el objetivo antes de editar. Pregunta al usuario solo si la decision depende de una preferencia que no haya acordado.");
        sb.AppendLine();

        if (multiFile)
        {
            sb.AppendLine("La tarea abarca un proyecto multi-archivo: relaciona los archivos entre si y el proyecto en su conjunto, no aislado.");
        }

        if (model is not null)
        {
            sb.AppendLine("Modelo local en uso: " + model.PullName + " · capacidades: " +
                          string.Join(", ", model.Capabilities.Count == 0 ? new[] { "completion" } : model.Capabilities) + ".");
        }

        sb.AppendLine();
        sb.AppendLine("SOLICITUDES DE COMPRENSION/ANALISIS (no de codigo): la respuesta esperada es un ANALISIS UTIL, no una enumeracion. Lee el contenido relevante con read_file y, cuando tengas suficiente evidencia, haz done colocando en 'reason' un analisis elaborado de que hace y como se relaciona. No repitas observaciones.");
        sb.AppendLine();
        sb.AppendLine("HONESTIDAD: NUNCA inventes ni simules exito. No declares 'done' hasta que la tarea este resuelta con evidencia real. Si una ruta no existe, Condor te mostrara candidatos coincidentes; elige entre ellos. No te inventes rutas ni capacidades.");

        return sb.ToString();
    }
}
