namespace Condor.Infrastructure.Agent;

using Condor.Core.Evaluation;
using Condor.Core.Models;
using Condor.Infrastructure.Documentation;

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
    public static string BuildSystemPrompt(
        string workingDir,
        string? manifest,
        ModelCandidate? model,
        Condor.Core.Evaluation.TaskRiskLevel riskLevel = Condor.Core.Evaluation.TaskRiskLevel.Medium,
        Condor.Core.Models.AgentProfile? profile = null,
        bool delegationEnabled = true)
    {
        profile ??= Condor.Core.Evaluation.AgentProfileCatalog.Engineer;
        var allowedActions = delegationEnabled
            ? profile.AllowedActions
            : profile.AllowedActions.Where(action => action != Condor.Core.Models.AgentAction.ActionDelegate).ToHashSet(StringComparer.Ordinal);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(string.Format(BaseIdentity, workingDir ?? "(directorio actual)"));
        AppendProfileInstructions(sb, workingDir, manifest, profile, allowedActions);
        sb.AppendLine("EXPLICACIÓN DEL CÓDIGO: cuando te pidan explicar cómo funciona una parte, examina los archivos actuales y sigue sus dependencias y llamadas; separa hechos observados de inferencias e indica rutas, símbolos y líneas que sostienen la explicación.");
        sb.AppendLine();

        var structured = model?.StructuredOutput ?? true;
        var toolUse = model?.ToolUse ?? true;
        var multiFile = (model?.MultiFileLevel ?? 0) >= 2;

        if (structured)
        {
            sb.AppendLine("Devuelve UNICAMENTE un JSON valido por paso, sin texto extra, con esta forma:");
            sb.AppendLine("{\"action\": \"<accion>\", \"path\": \"<ruta relativa>\", \"original\": \"<texto exacto>\", \"replacement\": \"<texto nuevo>\", \"content\": \"<contenido o vacio>\", \"reason\": \"<breve explicacion>\", \"objective\": \"<objetivo del plan>\", \"scopePaths\": [\"ruta/relativa/**\"], \"excludedPaths\": [], \"planItems\": [{\"task\":\"tarea pequeña\",\"expectedResult\":\"resultado observable\",\"verificationStep\":\"comprobacion propia\"}], \"riskLevel\": \"<low|medium|high|critical>\", \"subtasks\": [{\"id\":\"inspeccion\",\"task\":\"tarea independiente\",\"profileId\":\"reviewer\",\"maxDurationSeconds\":300,\"maxIterations\":8,\"maxOutputTokens\":4096}]}");
            if (profile.Id is "reviewer" or "security-reviewer")
                sb.AppendLine("Al usar done, añade reviewFindings: [{\"path\":\"src/archivo.cs\",\"line\":12,\"severity\":\"high\",\"explanation\":\"defecto\",\"evidence\":\"texto literal de la línea\",\"reproduction\":\"pasos\",\"origin\":\"current-change|pre-existing|uncertain\",\"status\":\"open|resolved|accepted\",\"closureReason\":\"justificación si aplica\"}]. Usa [] si no hallaste defectos.");
        }
        else
        {
            sb.AppendLine("Responde SIEMPRE con texto breve y claro, sin JSON ni etiquetas tecnicas. Indica el siguiente paso o tu analisis en prosa directa.");
        }
        sb.AppendLine();

        AppendActionGuidance(sb, allowedActions, toolUse, delegationEnabled);

        sb.AppendLine("FLUJO: respeta las rutas exactas escritas por el usuario y resuelvelas desde la raiz del proyecto; no anadas prefijos de carpeta. Si una tarea pide cambiar una ruta concreta, comprueba primero si existe: para un archivo existente usa read_file sobre esa ruta; para un archivo nuevo, lista con list_dir su directorio padre existente; para un directorio, usa list_dir sobre esa ruta. Si no se nombro una ruta, lista primero la raiz con list_dir y luego observa los archivos necesarios. No inventes rutas distintas a las solicitadas o a las que viste en list_dir/read_file.");
        sb.AppendLine("AVANCE TRAS OBSERVAR: una vez que leiste los archivos existentes y listaste los directorios padre de los archivos nuevos del alcance, no repitas list_dir ni read_file con las mismas rutas. Devuelve de inmediato el plan requerido para una tarea de cambio, o la respuesta final para una solicitud de analisis. Si la informacion observada no basta, indica que dato concreto falta en lugar de repetir una observacion.");
        sb.AppendLine("RECUPERACION DE BUILD Y PRUEBAS: ante un fallo, usa el primer error real del resultado. Lee el archivo y la linea que indique el compilador o runner; corrige ese defecto dentro del alcance autorizado y vuelve a ejecutar la verificacion fallida. No edites un archivo distinto para responder a un error localizado ni repitas build/test sin una accion correctiva.");
        sb.AppendLine("PLAN Y ALCANCE OBLIGATORIOS: antes de la primera accion create_file, edit_file o patch, explora los archivos relacionados y devuelve una accion plan. Incluye objetivo, rutas relativas incluidas, rutas excluidas y hasta 12 objetos planItems. Cada objeto agrupa task (tarea), expectedResult (resultado observable) y verificationStep (comprobación propia); no distribuyas esos datos en listas paralelas. Ejecuta una tarea a la vez y comprueba su resultado antes de empezar la siguiente. Usa el campo JSON scopePaths con rutas de archivo concretas o directorios terminados en /**. Respeta literalmente las rutas indicadas por el usuario también en scopePaths; no elimines ni sustituyas segmentos. Para crear un archivo nuevo, incluye su ruta exacta y explora su directorio padre. Condor bloqueara cualquier escritura que quede fuera del alcance declarado; si aparece trabajo nuevo, actualiza el plan antes de escribirlo.");
        sb.AppendLine("EJEMPLO PARA PLAN DE RIESGO BAJO: {\"action\":\"plan\",\"objective\":\"Agregar la prueba solicitada\",\"scopePaths\":[\"Tests/CalculatorTests.cs\"],\"excludedPaths\":[\"src/Calculator.cs\"],\"planItems\":[{\"task\":\"Crear la prueba de regresión\",\"expectedResult\":\"El archivo de prueba contiene el caso solicitado\",\"verificationStep\":\"Ejecutar el caso con dotnet test\"}],\"riskLevel\":\"low\"}. Usa las rutas observadas en el proyecto, no copies estas rutas. Para riesgo alto o crítico agrega objetos planItems distintos hasta cumplir el mínimo de comprobaciones indicado arriba.");
        sb.AppendLine("RIESGO: Condor clasifico esta solicitud como " + riskLevel.ToString().ToLowerInvariant() + ". Trata ese nivel como minimo, explica el impacto y adapta las comprobaciones a la reversibilidad y consecuencias del cambio. Para riesgo alto declara al menos 2 pasos y 2 comprobaciones distintas; para riesgo crítico, al menos 3 de cada una y rutas concretas sin raíz ni comodines.");
        if (riskLevel == Condor.Core.Evaluation.TaskRiskLevel.Critical)
            sb.AppendLine("APROBACIÓN HUMANA: antes de ejecutar cualquier escritura, Condor solicitará una confirmación vinculada al contenido exacto de la acción. No intentes eludir este control ni describas la aprobación como otorgada antes de recibirla.");
        sb.AppendLine("ACLARACIONES: si falta un dato que puede cambiar el alcance, seguridad o resultado esperado, devuelve {\"action\":\"clarify\",\"reason\":\"pregunta concreta\"} y espera la respuesta del usuario. No rellenes ese dato por suposicion.");
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
        sb.AppendLine("REVISION DEL CAMBIO: después de editar, usa git_diff para revisar el contenido resultante y confirmar que el alcance y la intención coinciden antes de declarar done.");
        sb.AppendLine("REVISION DE COMMITS: cuando la solicitud incluya revisar commits, usa git_log para enumerar los commits del alcance y git_show para inspeccionar cada hash; informa por commit si el asunto explica el propósito y si los archivos/cambios forman un alcance coherente. Señala asuntos vagos y cambios mezclados.");

        return sb.ToString();
    }

    private static void AppendProfileInstructions(
        System.Text.StringBuilder sb,
        string? workingDir,
        string? manifest,
        Condor.Core.Models.AgentProfile profile,
        IReadOnlySet<string> allowedActions)
    {
        sb.AppendLine("Perfil activo: " + profile.Name + " (" + profile.Id + "). Objetivo: " + profile.Objective);
        sb.AppendLine("Instrucciones del perfil: " + profile.Instructions);
        sb.AppendLine("Herramientas autorizadas para este perfil: " + string.Join(", ", allowedActions) + ".");
        if (profile.ReadOnly)
            sb.AppendLine("Este perfil es de solo lectura. No solicites ni ejecutes acciones que creen, editen, parchen o reviertan archivos.");
        if (profile.Id is "reviewer" or "security-reviewer")
            sb.AppendLine("REVISIÓN: en la acción done incluye reviewFindings como lista, aunque esté vacía. Cada hallazgo requiere path, line, severity (critical/high/medium/low), explanation, evidencia literal de esa línea, reproduction, origin (current-change/pre-existing/uncertain) y status (open/resolved/accepted). Compara con HEAD para separar problemas preexistentes; usa uncertain cuando la base no permita atribución. Los hallazgos críticos abiertos impiden cerrar la revisión.");

        if (!string.IsNullOrWhiteSpace(manifest))
            sb.AppendLine("Se detecto un proyecto con manifest ('" + manifest + "'). Si la tarea requiere compilar/probar y el ecosistema lo permite, usa build/test; para modificaciones edita los archivos reales.");
        else
            sb.AppendLine("No se asume ningun ecosistema: observa el contenido real con list_dir y read_file para descubrir que existe. Solo usa build/test si el ecosistema lo permite y la tarea lo requiere; una solicitud de comprension/analisis no requiere compilar: describe e interpreta lo que realmente encuentres.");
        if (profile.Id is "engineer" or "test-engineer")
            sb.AppendLine("PRUEBAS: si cambias comportamiento de código, identifica la unidad afectada y revisa sus pruebas existentes. Crea o ajusta pruebas unitarias para los criterios de aceptación y regresiones relevantes; si el cambio cruza componentes, persistencia, procesos o proveedores, cubre también sus interacciones con pruebas de integración aisladas. Si cambia un recorrido público, prueba de extremo a extremo desde la entrada real del usuario hasta el resultado. Si cambia la interfaz, compara capturas o estados renderizados con el diseño esperado en tamaños relevantes y registra diferencias. Si cambia la TUI de Condor, ejecuta las pruebas TuiVisualSnapshotTests y revisa las filas reportadas; actualiza los baselines solo para un cambio visual intencional mediante operacion/scripts/Update-TuiVisualSnapshots.ps1 y vuelve a ejecutar la comparación. Ejecuta las pruebas enfocadas con la acción test y deja que la verificación final del proyecto confirme el conjunto aplicable. Relaciona los casos ejecutados con el comportamiento cubierto, comunica fallos y brechas, y no declares cobertura sin resultados observados. Si el entorno no permite pruebas automatizadas o capturas, describe la limitación y no afirmes que la validación se completó.");
        if (profile.Id == "engineer")
            sb.AppendLine("ENTREGA Y RECUPERACIÓN: las publicaciones y despliegues son manuales. Si la solicitud requiere activar una funcionalidad, propone un flag desactivado por defecto, un entorno controlado y una habilitación gradual con criterios de avance y reversión. En cambios de riesgo alto o crítico, declara en el plan cómo desactivar o revertir el cambio; no ejecutes la publicación ni la reversión sin autorización humana.");
        if (profile.Id == "engineer")
            sb.AppendLine("OBJETIVO Y DISEÑO: para un cambio de producto, expresa el resultado observable que busca el usuario y una señal verificable de éxito. Antes de una decisión arquitectónica importante, presenta alternativas acotadas con diferencias, ventajas y límites. Prefiere la solución más pequeña que satisfaga los criterios; justifica cada paquete o servicio nuevo y elimina lo que no sea necesario.");
        if (profile.Id == "engineer")
            sb.AppendLine("DOCUMENTACIÓN DEL PROYECTO: lee las reglas del repositorio y del módulo afectado. Si cambian el propósito, las convenciones o los límites, actualiza la documentación correspondiente para que refleje el código actual.");
        if (profile.Id is "engineer" or "test-engineer")
        {
            var learnedInstructions = IncidentLearningService.LoadActiveInstructions(workingDir ?? Environment.CurrentDirectory);
            if (learnedInstructions.Count > 0)
            {
                sb.AppendLine("CONTROLES ACTIVADOS POR INCIDENTES REPETIDOS EN ESTE PROYECTO:");
                foreach (var instruction in learnedInstructions)
                    sb.AppendLine("- " + instruction);
            }
        }
        if (profile.Id == "reviewer")
            sb.AppendLine("REVISIÓN POR MÓDULO: antes de revisar, lee las reglas del repositorio y del módulo afectado, respeta las rutas excluidas y prioriza fallos importantes para esa área. Cada observación conserva ruta, línea y evidencia reproducible.");
    }

    private static void AppendActionGuidance(
        System.Text.StringBuilder sb,
        IReadOnlySet<string> allowedActions,
        bool toolUse,
        bool delegationEnabled)
    {
        bool Allows(string action) => allowedActions.Contains(action);
        sb.AppendLine("Acciones permitidas:");
        if (Allows(Condor.Core.Models.AgentAction.ActionListDir))
            sb.AppendLine("  list_dir  \"path\"  -> listar el contenido de un directorio (rutas relativas).");
        if (Allows(Condor.Core.Models.AgentAction.ActionReadFile))
            sb.AppendLine("  read_file \"path\"  -> leer el contenido exacto de un archivo.");
        if (toolUse)
        {
            AppendToolActionDetails(sb, Allows, delegationEnabled);
            sb.AppendLine("Usa exactamente un nombre de accion de esta lista por JSON; nunca combines acciones con '/' ni inventes variantes.");
        }
        else
        {
            sb.AppendLine("  (este modelo NO ejecuta herramientas externas; usa la observacion directa con list_dir/read_file y razona en texto.)");
        }
        if (Allows(Condor.Core.Models.AgentAction.ActionDone))
            sb.AppendLine("  done -> termina cuando creas que la tarea esta resuelta.");
        sb.AppendLine();
    }

    private static void AppendToolActionDetails(
        System.Text.StringBuilder sb,
        Func<string, bool> allows,
        bool delegationEnabled)
    {
        if (allows(Condor.Core.Models.AgentAction.ActionCreateFile)) sb.AppendLine("  create_file \"path\" + \"content\" -> crear un archivo nuevo; no sobrescribe uno existente.");
        if (allows(Condor.Core.Models.AgentAction.ActionEditFile)) sb.AppendLine("  edit_file \"path\" + \"content\" -> reemplazar el contenido de un archivo existente.");
        if (allows(Condor.Core.Models.AgentAction.ActionCreateFile) || allows(Condor.Core.Models.AgentAction.ActionEditFile))
            sb.AppendLine("CONTENIDO DE ARCHIVOS: en create_file y edit_file, content lleva el contenido fuente completo y valido del archivo; nunca copies la ruta o el nombre como contenido.");
        if (allows(Condor.Core.Models.AgentAction.ActionPatch)) sb.AppendLine("  patch \"path\" + \"original\" + \"replacement\" -> reemplazo quirurgico en un archivo existente.");
        if (allows(Condor.Core.Models.AgentAction.ActionBuild)) sb.AppendLine("  build -> compilar el proyecto cuando su ecosistema lo permita y la tarea lo requiera.");
        if (allows(Condor.Core.Models.AgentAction.ActionTest)) sb.AppendLine("  test -> ejecutar las pruebas del proyecto cuando existan y la tarea lo requiera.");
        if (allows(Condor.Core.Models.AgentAction.ActionRestore)) sb.AppendLine("  restore -> restaurar dependencias cuando el build o test lo requieran.");
        if (allows(Condor.Core.Models.AgentAction.ActionGitStatus)) sb.AppendLine("  git_status -> estado del repositorio.");
        if (allows(Condor.Core.Models.AgentAction.ActionGitDiff)) sb.AppendLine("  git_diff -> revisar los cambios hechos por esta tarea antes de declarar done.");
        if (allows(Condor.Core.Models.AgentAction.ActionGitLog)) sb.AppendLine("  git_log -> últimos diez commits con hash, asunto y archivos/cambios resumidos.");
        if (allows(Condor.Core.Models.AgentAction.ActionGitShow)) sb.AppendLine("  git_show con path=<hash hexadecimal> -> resumen del propósito declarado y alcance por archivos de un commit.");
        if (allows(Condor.Core.Models.AgentAction.ActionSearch)) sb.AppendLine("  search \"content\" -> buscar texto en el proyecto.");
        if (allows(Condor.Core.Models.AgentAction.ActionUndoFile)) sb.AppendLine("  undo_file \"path\" -> revertir la ultima edicion sobre un archivo.");
        if (allows(Condor.Core.Models.AgentAction.ActionPlan)) sb.AppendLine("  plan -> declarar objective, scopePaths, excludedPaths, planItems (cada uno con task, expectedResult y verificationStep) y riskLevel antes de escribir.");
        if (allows(Condor.Core.Models.AgentAction.ActionClarify)) sb.AppendLine("  clarify con reason -> hacer una pregunta concreta y detenerse a esperar respuesta.");
        if (delegationEnabled && allows(Condor.Core.Models.AgentAction.ActionDelegate))
            sb.AppendLine("  delegate -> encargar de 1 a 4 subtareas independientes a perfiles engineer/reviewer/security-reviewer/test-engineer en worktrees Git aislados; puedes lanzar revisores independientes en paralelo. Indica id, task, profileId, maxDurationSeconds, maxIterations y maxOutputTokens. maxOutputTokens limita el total reservado para la salida del modelo de cada agente. Condor devuelve estado, duración, archivos y diff para que tú integres si corresponde.");
    }
}
