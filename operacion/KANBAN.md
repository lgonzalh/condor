# KANBAN

Version: 2.4.0
Estado: Activo
Nivel: Global
Fecha de actualizacion: 2026-11-05

## DASHBOARD VIGENTE

**Corte historico de software: 2026-10-19.** La implementacion T-021 se verifica por separado en este corte documental.

| Que hacer | Haciendo | Hecho |
|---|---|---|
| Completar aceptación integrada Fases 1–4, confirmar checks requeridos y medir CAP-047; continuar CAP-060 y las capacidades restantes en orden. | T-022 — CAP-058 registra observaciones cerradas y mide proporción útil/descartes. CAP-059 ya implementada: el patrón repetido de timeout activa una instrucción aprobada que se verifica dentro del prompt. `DocumentingAgentServiceTests`: 6/6. Aceptación global de Fases 1–4 pendiente. | T-021 completada y publicada. T-022 conserva el plan maestro y evidencia por capacidad. La rama feature está respaldada en GitHub; `main` sigue intacta. Publicaciones y despliegues siguen siendo manuales. |

**Avance de implementación 2026-11-04:** CAP-029–CAP-031 se incorporaron a las instrucciones de ingeniería y del perfil de pruebas: cambios entre componentes requieren pruebas de integración, recorridos públicos requieren extremo a extremo, y cambios de interfaz requieren comparación de capturas o estados renderizados. CAP-040–CAP-043 agregan el check de formato y mantienen análisis de tipos, compilación, pruebas y CI; CAP-044 agrega auditoría NuGet bloqueante y un job independiente de Gitleaks 8.30.1 fijado por SHA-256 que analiza el checkout y el historial con salida redactada (escaneo local: 627 commits y checkout actual, cero hallazgos); CAP-045 activa CA1502 y compara cambios locales: `AgentService.RunAsync` pasa de 185 en `HEAD` a 20 y la última compilación Release deja 0 advertencias. CAP-046 compara el árbol local y consolidó tres duplicaciones reales; quedan señales de configuración de procesos y dobles de prueba para criterio humano. CAP-047 midió cinco compilaciones Release y el recorrido visible `condor --help`; falta una medición de inferencia del agente porque Ollama local no está disponible. CAP-048 habilita `merge_group`. La auditoría de dependencias y la compilación Release pasan; no ejecuté pruebas en esta continuación. Los runs remotos 42057269265 y 42059220548 expusieron que faltaba una nota si Ollama no estaba instalado; `OllamaDetector` ya informa ese estado. El run 42061068056 pasó todos los checks y las suites completas remotas 870/870.

**Actualización de calidad 2026-11-04:** CAP-045 compara complejidad antes/después en worktrees y reporta en CI para `pull_request`/`merge_group`; la comparación local `HEAD`→árbol de trabajo dio `AgentService.RunAsync=185→20`, bajo CA1502=25, y la compilación Release termina con 0 errores y 0 advertencias. CAP-046 comparó el árbol local con ventanas C# de seis líneas; consolidó tres duplicaciones reales (lista de extensiones, comprobación de enlaces y patrones de redacción). El comparador conserva señales de arranque de procesos y dobles de prueba para revisión humana. CAP-047 midió cinco compilaciones Release por versión (medianas 2.713,25 ms y 3.251,4 ms) y cinco arranques de `condor --help` (65,19 ms y 63,14 ms); el informe y las limitaciones están en T-022. La ayuda CLI no confirma rendimiento de inferencia; queda esa medición pendiente por indisponibilidad de Ollama local. La primera ejecución remota sigue pendiente.

**Seguimiento CAP-046 2026-11-05:** La medición de la rama frente a `origin/main` aún reporta ventanas repetidas adicionales (+4 en `AgentService`, +2 en `AgentDelegationService` y +10/+11 en dos clases de prueba). Se unificó una inicialización Git duplicada dentro de `AgentService`; la prueba focalizada pasó 1/1. Las demás señales requieren revisión semántica y CAP-046 sigue abierta; detalle y limitación del comparador constan en T-022.

**CAP-046 revalidada 2026-11-05:** `GitProcessStartInfoFactory` unifica el arranque Git de los dos flujos del agente. El diff actual reduce cinco bloques exactos; en la comparación acumulada ya no hay señal de duplicación lógica en `AgentService`, y la única de producción restante es una firma repetida por el doble de prueba. Pruebas focalizadas 5/5; no se asignaron estados a la matriz.

**CAP-047 2026-11-05:** Diez iteraciones A/B del escenario de revisión del agente dieron +0,61% de tiempo, +1,21% de CPU y −0,22% de memoria pico respecto al commit base. Build Release y pruebas focalizadas pasan; la medición corresponde al flujo del agente con LLM determinista, no a inferencia real. Detalle: T-022.

**CAP-057 — continuidad 2026-11-05:** El benchmark ahora valida conceptos del defecto además de ruta/línea/gravedad. Pruebas focalizadas 6/6; la corrida TUI real con `qwen2.5-coder:0.5b` completó 4 casos y obtuvo TP 0, FP 4, FN 3. CAP-057 sigue en curso; la baseline no se actualizó. Detalle y limitaciones en T-022.

**CAP-057 — iteración reviewer-v3 2026-11-05:** Se endurecieron las instrucciones y se protege la comparación por hash de prompt. Pruebas focalizadas 15/15; build Release 0/0. Qwen 1.5B completó con 1 error de JSON, 2 FP y 3 FN; qwen 0.5B completó con 4 FP y 3 FN. Sin baseline; CAP-057 sigue en curso.

**CAP-057 — criterio de comparación validado 2026-11-05:** La versión de suite ahora representa casos y puntuación (`reviewer-v2`); `PromptHash` representa instrucciones y contrato de solicitud para comparar cambios de prompt/herramienta sin cambiar la suite. `RequireJson` se activa en el runner. Una corrida completa imperfecta solo se guarda como baseline si se solicita `--actualizar-base`, y el mensaje aclara que es referencia, no aprobación. Con `qwen2.5-coder:0.5b`, el run `.condor/reviewer-benchmark/run-20261105-185601037.json` comparó el hash nuevo con baseline previa del mismo modelo: TP 0, FP 4, FN 3, errores 0, regresiones 0. Build Release 0/0; pruebas focalizadas 21/21. La medición está implementada; la precisión del modelo sigue siendo una mejora pendiente.

**CAP-048 2026-11-05:** Se actualizó la referencia remota: `main` no avanzó y sigue siendo la base común de la feature. En el HEAD actual se repitieron las pruebas Release de aprobación, revisión y delegación (5/5); build Release completo del mismo código fuente: 0 advertencias/errores. GitHub canceló el run de código por pushes posteriores; no se cuenta como check exitoso.

**CAP-053/CAP-054 2026-11-05:** La guía del perfil de ingeniería exige activar gradualmente con flag apagado y entorno controlado; se probó que `undo_file` restaura el contenido previo y retira archivos nuevos tras simular una regresión. Pruebas focalizadas 2/2. No se desplegó ni se revirtió ninguna publicación.

**CAP-055/CAP-056 2026-11-05:** Informe del incidente con impacto, causa no confirmada y rutas: 1/1. La prueba de regresión del benchmark pasó y falló al restaurar la redacción defectuosa en un worktree temporal; el worktree se retiró. CI 42081978249 completó build, pruebas y escaneo de secretos exitosamente. Detalle: T-022.

**Recuperación CAP-028 2026-11-04:** Condor ofrece una corrección al primer plan inválido y detiene la ejecución sin cambios si el modelo repite uno ya rechazado; la huella se conserva solo en memoria y se borra ante nueva instrucción o avance. Build Release sin incremental: 0 errores y 0 advertencias. No se ejecutaron pruebas; Ollama está desconectado y la aceptación real sigue pendiente.

**Validación de TUI 2026-11-04:** CAP-014 se verificó en Condor interactivo mediante PTY: `/ayuda` y `/version` se ejecutaron consecutivamente en una sola sesión; `/salir` la cerró con código 0.

**Evaluación real del revisor 2026-11-04:** CAP-057 completó la suite `reviewer-v1` (4/4 casos, sin errores) con `qwen2.5-coder:1.5b` (F1 0,667) y `qwen2.5-coder:0.5b` (F1 0,857); el 0.5B omitió menos defectos, pero ambos marcaron como defecto el caso limpio, así que Condor no guardó baseline. El arranque frío de `lfm3:1.7b-q4_K_M` venció a los 45 s y el 3B no pasó la puerta de RAM. Corregí el texto para distinguir timeout de evaluación completa con puntaje imperfecto.

**Regresión CAP-056 2026-11-04:** el mensaje falso de benchmark incompleto quedó cubierto por `ReviewerBenchmarkCommandTests`. Las dos pruebas focalizadas pasan; al reintroducir el texto antiguo, `FormatNonPerfectMessage_SuiteCompletaConFalsoPositivo_NoLaLlamaIncompleta` falla como se espera y vuelve a pasar con la corrección.

**Historial de falsos positivos CAP-058 2026-11-04:** Condor conserva conteos adjudicados por revisión y los agrega históricamente y en 30 días, incluyendo la proporción de hallazgos útiles. No guarda contenido ni rutas de los hallazgos en el historial. La prueba de dos ejecuciones verifica 3 útiles, 1 descartado y 75%; `DocumentingAgentServiceTests`: 6/6.

**Avance de proceso y riesgo 2026-11-04:** CAP-001/CAP-006 reconocen señales críticas y altas en español además de inglés. Pruebas focalizadas cubren bajo, medio, alto y crítico (5/5). La comparación integrada de documentación y escritura crítica pasó 2/2: la tarea documental usa un plan mínimo y termina sin pedir aprobación; la escritura en `.env` requiere aprobación humana y se bloquea si cambia `HEAD` durante la espera. CAP-002 responde consultas informativas en modo de solo lectura: ante “¿Qué hace esta clase?”, la prueba del agente bloquea la edición propuesta y confirma que el archivo sigue idéntico (1/1); las variantes con tildes pasan 2/2. CAP-003 autoriza solo `Calculator.cs` según el plan y bloquea la edición fuera de alcance a `README.md` (1/1). CAP-004 lista el proyecto, lee `Calculator.cs` y su contrato `IArithmetic.cs`, y los relaciona en el plan antes de escribir (1/1). CAP-005 pregunta ante “mejora esto” antes de invocar al modelo, sin cambios ni acciones de herramienta (1/1). CAP-007 registra objetivo, alcance incluido/excluido y comprobación antes de la primera edición (1/1). CAP-008 exige un resultado esperado y una comprobación por cada tarea del plan; los casos de plan completo, incompleto, límites, prompt y persistencia pasan 7/7. CAP-009 restaura objetivo, alcance, plan, decisiones e historial desde una primera ejecución detenida; un nuevo servicio y almacén retoman la tarea, completan la edición autorizada y verifican el resultado (1/1). CAP-010 conserva la revisión obligatoria del diff y agrega `git_log`/`git_show` para revisar hashes, asuntos y archivos de commits; la prueba del flujo, la validación de hash y el prompt pasan 3/3. CAP-011 informa verificaciones reales, resultados fallidos, controles no ejecutados y el siguiente paso; `CierreResumeLoVerificadoLoNoComprobadoYLoPendiente` pasa y la suite de presentación queda 10/10. CAP-012 reexplora el alcance y exige un plan actualizado antes de escribir; su prueba confirma cambios en código, test y README antes del diff (1/1). CAP-013 ya está implementada y el mismo recorrido del agente lee, edita y verifica archivos en una sesión (1/1). CAP-014 ya está implementada: `CondorTui.RunAsync` espera entradas en un bucle y vuelve después de cada solicitud; queda pendiente validar dos solicitudes en una TUI real. CAP-015 entrega al modelo inventario, objetivo y resultados reales de las herramientas; el plan actualizado recibe contenido observado de Calculator, su test y README antes de editar (1/1). CAP-016 ya está implementada: el agente valida y ejecuta llamadas estructuradas y produce resultados reales de lectura, edición y diff (1/1). CAP-017 devuelve al modelo cada resultado de herramienta; el prompt del nuevo plan contiene las tres lecturas del proyecto antes de modificarlo (1/1). CAP-018 conserva requisito y evidencia reciente en una sesión extensa compactada (`RunAsync_ContextoExtenso_CompactaYConservaObjetivoYObservaciones`, 1/1). CAP-019 valida el perfil `@reviewer`: instrucción de solo lectura, lectura del archivo y bloqueo de una edición no autorizada, sin alterar el original (1/1). CAP-021–CAP-024 tienen una prueba de delegación con dos perfiles concurrentes: verifica worktrees separados, snapshot de cambios rastreados y no rastreados, límites declarados, diffs reportados, árbol padre intacto y limpieza (1/1). CAP-025 pasa una prueba de timeout que cancela al hijo tras un segundo y conserva intacto el padre (1/1). CAP-036/CAP-038 prueban que cada hallazgo queda ligado a archivo, línea, gravedad, evidencia y reproducción, y que uno abierto impide cerrar (1/1). CAP-039 limita las rondas, obliga a replantear y restringe la revisión delta a los cambios nuevos (3/3). Las pruebas focalizadas nuevas de CAP-019, CAP-021–CAP-025 y CAP-036/CAP-038 pasan 4/4; CAP-039 ya estaba incluida en la última suite completa previa (819/819). 819/819 (Arquitectura 22, Core 294, CLI 125, Infraestructura 378). La aceptación global de Fase 1 permanece abierta por los demás criterios pendientes.

**Avance de aprobaciones 2026-11-04:** CAP-049–CAP-052 aplican controles proporcionales, exigen aprobación para rutas sensibles y ligan el código al `HEAD`; una prueba integrada 1/1 confirma que cambiar el commit invalida la aprobación, registra la intervención humana y bloquea la escritura. En proyectos sin HEAD Git, la aprobación sigue vinculada al contenido exacto de la acción.

**Avance de entrega y aprendizaje 2026-11-04:** CAP-053/054 requieren planificación de activación gradual y reversión, siempre manuales; CAP-055 registra incidentes con impacto y rutas afectadas (prueba focalizada 1/1); CAP-056 pide conservar regresiones permanentes; CAP-058 conserva hallazgos estructurados y calcula proporción descartada por revisión (4/4 pruebas focalizadas pasan); CAP-059 agrupa cuatro patrones allowlisted por proyecto y activa instrucciones aprobadas tras dos incidentes (5/5 pruebas focalizadas pasan), sin convertir texto libre en reglas. Siguen pendientes el escenario real de reversión y el agregado histórico/calibración de falsos positivos. CAP-057 ya cuenta con suite, runner y comparación contra línea base; falta la ejecución con un modelo real.

**Avance de métricas 2026-11-04:** CAP-064–CAP-066 agregan `/metricas` en terminal y TUI. Guarda eventos manuales por proyecto y calcula Lead Time, Deployment Frequency, Failure Rate, recuperación y Retrabajo con motivo. La publicación y el despliegue siguen siendo manuales; 5/5 pruebas focalizadas pasan. CAP-057 incluye una suite reproducible de cuatro casos, runner por modelo y comparación de regresiones (3/3 pruebas focalizadas); la ejecución real con `granite4.2:3b` agotó 45 s en el primer caso y dejó el resto como no ejecutado. El informe quedó guardado sin línea base. La evidencia de aceptación restante de T-022 continúa pendiente.

**Continuidad de métricas 2026-11-05:** la persistencia de CAP-064–CAP-066 informa errores de lectura/escritura y no confirma registros no guardados. Condor correlaciona tareas con registros manuales abiertos usando `[[condor:cambio:<id>]]` y `[[condor:incidente:<id>]]`; no infiere entregas ni despliegues. `condor /ayuda` compiló y arrancó con la instancia anterior activa; pruebas focalizadas del comando: 4/4; del vínculo automático: 3/3. La aceptación TUI real de la asociación sigue pendiente.

**Avance de producto y documentación 2026-11-04:** CAP-060–CAP-063 y CAP-067–CAP-069 se incorporaron a las instrucciones de ingeniería/revisión para declarar resultados medibles, comparar alternativas, minimizar cambios, justificar dependencias, respetar reglas por módulo, actualizar documentación y explicar el código actual con referencias. CAP-064–CAP-066 cuentan con `/metricas`, registro manual por proyecto y asociación automática de tareas a registros abiertos mediante referencias explícitas; la aceptación TUI real de esta asociación sigue pendiente. No se infieren métricas DORA de la duración de una sesión.

**Separacion de proveedores:** Condor conserva Ollama como opción local y admite un proveedor OpenAI-compatible opcional configurado mediante `CONDOR_OPENAI_COMPATIBLE_BASE_URL` y, si se requiere, `CONDOR_OPENAI_COMPATIBLE_API_KEY`. Graft usa OpenAI API para la capa semántica; el grafo estructural se actualizó, pero `graft build --deep` volvió al modo estructural al no encontrar `GRAFT_API_KEY`.

**Pendiente de decision, no tarea activa:** la aspiracion de software abierto del Cuaderno IV (A-005) sigue condicionada a resolver lo legal; `LICENSE` y `NOTICE` declaran All Rights Reserved.

**Alcance de la verificacion:** T-021 aporta build y suite 671/671 ejecutados el 2026-11-01; las cifras 664/664 corresponden al 2026-10-19 y no se reemplazan. La capa semantica completa de Graft se habia generado en una ejecucion previa del usuario (37 conceptos, 3.088 nodos); los nuevos nodos T-021 aun requieren su siguiente pasada `--deep`.

Los apartados siguientes son el tablero historico del 2026-08-19 y se conservan como antecedente; no representan trabajo abierto actual.

## EN PROGRESO

- T-022: incorporar y validar las 69 capacidades de Condor por fases. Estado: Fases 2–4 en desarrollo; perfiles, delegación aislada, límites, aprobación humana crítica, revisión independiente con rondas limitadas, compactación de contexto y comparación persistida con Ollama/proveedor OpenAI-compatible incorporados. CAP-020 validada en la TUI con qwen2.5-coder:1.5b y qwen2.5-coder:0.5b sobre el mismo prompt; el informe conserva respuestas y duración, y Condor restauró el modelo anterior. Una prueba previa en el orden inverso quedó parcial por RAM, por lo que la selección segura debe respetar el margen medido. Falta validar un proveedor OpenAI-compatible configurado y completar evidencia antes de cerrar fases.
- El pendiente de Graft semantico permanece separado del trabajo de producto.

## LISTO / VERIFICADO PARCIALMENTE

- Harness de presupuesto dinamico y seleccion inteligente: RAM como stock + reserva + presupuesto
  + 1-/1+ + seleccion por tarea. Reserva operativa configurable (`BudgetPolicy`), seleccion por
  suficiencia/eficiencia (no tamaño), reevaluacion en punto seguro con limite, adaptacion del
  prompt al modelo, modelo instalado del usuario como candidato. Pruebas 17 harness + 4 prompt.
- Estabilizacion del agente: causa raiz de la RAM fluctuante identificada y corregida.
- Promesa fundamental de inicio: con modelos instalados pero RAM baja, la sesion arranca
  igual (no se bloquea); Condor decide el modelo en cada tarea con recuperacion acotada.
- Intervencion opcional de RAM: si tras evaluar no hay modelo viable, Condor informa,
  sugiere liberar memoria (Opcion S/N, nunca forzada) y, si el usuario confirma, reevalua
  y continua; si no, sale limpio conservando la tarea.
- Progreso visible obligatorio durante todo el inicio: pantalla nunca se congela (banner,
  spinner y etapas "Revisando recursos"/"Evaluando modelos"/"Preparando modelo" hasta listo).
- Busqueda de salida viable: el catalogo ahora incluye alternativas menores (1.5B/1B/0.5B);
  si el modelo instalado no cabe, Condor busca y usa/descarga la alternativa menor viable
  antes de pedir intervencion.
- Analisis y orquestacion: se muestra inventario del entorno + motivo/capacidades del modelo,
  y se separa [HALLAZGOS] (evidencia) de [RESULTADO] (analisis) sin duplicacion.
- ADN conversacional y generalizacion: la respuesta final es natural (sin etiquetas tecnicas
  obligatorias), agnostica de ecosistema (no asume .NET) y termina con
  '©Condor · <modelo> · <tiempo>'.
- Identidad permanente y diferenciacion del origen: "©Condor" + eslogan se mantienen visibles
  desde el inicio hasta la respuesta; se muestra el modelo realmente utilizado y se colorea
  por origen (Cóndor azul, modelo gris, error rojo, advertencia amarillo).
- Identidad como zona persistente: la identidad se re-dibuja como zona fija antes de cada
  espera de entrada y permanece visible todo el ciclo (inicio, tarea, respuesta, error,
  finalizacion y nueva espera); muestra el modelo local REAL activo.
- Build sin errores/advertencias en el ultimo cierre informado.
- Suites automatizadas verdes en el ultimo cierre informado (664 pruebas).
- **Cierre 2026-10-19**: build 0w/0e, suite 664/664, UTF-8 sin mojibake,
  lanzador global unico (`C:\GitHub\condor`) y TUI con la ruta real de
  invocacion.
- Descarga de qwen2.5-coder:3b observada.
- Presupuesto seguro observado.
- Progreso visual observado (arranque y agente; nunca pantalla en negro).
- Ejecucion de herramientas observada.
- "hola" ejecutado correctamente.
- E2E real: tarea con RAM suficiente, bloqueo honesto con RAM insuficiente, recuperacion
  posterior, e inicio no-bloqueante con modelos instalados y RAM baja.

- **T-018 CORRECCIONES FINALES TUI PRODUCCION**: identidad visual final (mascota centrada, contraste aprobado, cabecera unica, comentarios, comunicacion directa, placeholder, arranque optimizado). Verificacion real sobre condor.exe publicado: arranque <400 ms, sesion con modelo real, comentario/ayuda/salir funcionando, 0 regresiones. Build 0w/0e. 17 fallos preexistentes (15 Core + 2 Infra) — 0 regresiones.

## PENDIENTE

- Repetir prueba cliente incognito completa.
- Verificar seleccion por capacidad de ingenieria.
- Verificar que el progreso represente el ciclo real.
- Actualizar documentacion oficial una vez que el comportamiento este validado.

## BLOQUEO ACTUAL

Resuelto: ya no existe la contradiccion de "modelo listo" seguido de "no hay modelo
compatible" para tareas que deberian ejecutarse. Cuando la RAM libre no alcanza el
presupuesto seguro, Condor informa un bloqueo TEMPORAL de recursos de forma honesta
y conserva la tarea. Ademas, el inicio ya no se bloquea cuando HAY modelos instalados:
la sesion arranca, se explica la RAM con honestidad y Condor decide/recupera el modelo
en cada tarea.

## SIGUIENTE MEJOR ACCION

Diagnostico de una sola causa raiz con un solo agente integrador (completado).

## DEFINITION OF DONE

- causa identificada (cubierta);
- correccion minima (cubierta);
- pruebas de regresion (cubiertas);
- prueba E2E real (cubierta);
- documentacion actualizada;
- commit limpio;
- push autorizado;
- estado Git verificado.

- 2026-11-04 — CAP-035 incorporación: una solicitud explícita de revisión multiagente exige los perfiles independientes `reviewer` y `security-reviewer` sobre el mismo diff. Una sola delegación no ejecuta la revisión ni reduce las modificaciones pendientes; el agente recibe una instrucción para contrastar ruta, línea y evidencia, releer cuando haya discrepancias y conservar hallazgos reproducibles. La revisión delta integrada confirma ambos perfiles y el rechazo de un único revisor; `MultiAgentReviewPolicyTests`, `AgentDelegationServiceTests` y las pruebas integradas focalizadas pasan 7/7, incluida la propagación de hallazgos estructurados al agente principal. La ejecución de revisores con modelos reales sigue pendiente.

- 2026-11-04 — CAP-037 verificada: `RunAsync_RevisionPreexistente_NoLaAtribuyeAlCambioDeOtroArchivo` crea un repositorio Git con un defecto ya presente en `Calculator.cs`, cambia únicamente `README.md` y presenta al revisor el hallazgo con `origin=pre-existing`. La comparación real con `HEAD` acepta esa procedencia y evita atribuirla al cambio actual; el cierre se mantiene bloqueado porque el defecto continúa abierto. Prueba focalizada: 1/1.

- 2026-11-04 — CAP-033 incorporación: el perfil `reviewer` debe comparar objetivo y diff contra `HEAD`, revisar interfaces, llamadores/dependientes, pruebas y documentación pertinentes, rastrear efectos en componentes relacionados y marcar `origin=uncertain` si falta evidencia. `PerfilReviewer_ComparaObjetivoDiffYComponentesRelacionados` confirma que estas instrucciones llegan al prompt; `ModelPromptBuilderTests` pasa 6/6.
- 2026-11-04 — CAP-034 incorporación: los perfiles de ingeniería y pruebas deben reproducir el defecto contra la versión base, seguir su causa por llamadores y componentes dependientes, y exigir una regresión que falle antes y pase con la corrección; si no se ejecuta la base, deben registrar la limitación. `PerfilesDeIngenieriaYPruebas_ExigenReproducirLaCausaAntesDeCorregir` valida las instrucciones del prompt. `ModelPromptBuilderTests`: 6/6; conjunto focalizado junto con CAP-033/035/037: 14/14.
- 2026-11-04 — CAP-028–CAP-031 refuerzo de instrucciones verificables: el perfil de pruebas exige seleccionar unitarias, integración, extremo a extremo o visual según el alcance; reproducir defectos contra base cuando sea posible y reportar los resultados realmente observados sin afirmar cobertura no ejecutada. `PerfilesDeIngenieriaYPruebas_ExigenReproducirLaCausaAntesDeCorregir` comprueba esas instrucciones; `ModelPromptBuilderTests`: 6/6. Falta demostrar el flujo con una tarea real de Condor sobre un proyecto de muestra.
- 2026-11-04 — CAP-007/CAP-011/CAP-027 y robustez del protocolo: se unificaron `scopePaths`/`excludedPaths`, el parser admite camelCase y snake_case y rechaza propiedades duplicadas que pudieran cambiar el alcance; Condor no declara completa una tarea de cambio con solo lectura o diagnóstico. Suite completa: 839/839 (Arquitectura 22, Core 297, CLI 127, Infraestructura 393). En la ejecución real CAP-028 con `qwen2.5-coder:3b`, el agente leyó el proyecto, pero no logró planificar ni crear la prueba; terminó sin reportar éxito. Una prueba diagnóstica manual confirmó el rojo inicial (`Expected: 1, Actual: -5`) y no cuenta como aceptación de Condor. CAP-028–CAP-031 siguen pendientes de validación real completa.
- 2026-11-04 — CAP-007/CAP-028: tras una corrida TUI fallida por ruta no explorada, Condor ahora lista con `list_dir` el directorio padre existente de una ruta nueva y vuelve a validar el mismo plan; integración focalizada 1/1, suite completa 840/840 (Arquitectura 22, Core 297, CLI 127, Infraestructura 394). La extracción redujo la complejidad de `AgentService.RunAsync` de 340 a 335, aún sobre CA1502 26. La repetición real listó `Sample.Tests`, pero el modelo generó planes inválidos y agotó límites sin crear la prueba ni ejecutar `dotnet test`; incidente `%TEMP%\condor-cap028-live-1fbe12f2f0f44ba9b2e08d8a1760f358\docs\20261104-191143628-incidente-del-agente-2330563084244a729630348b4d494b2d.md`. CAP-028–CAP-031 siguen abiertas para aceptación real completa.
- 2026-11-04 — CAP-031: dos baselines capturan la bienvenida y la sesión de la TUI a 110×34; el test reconstruye la cuadrícula y compara las filas contra el estado esperado, con diferencias explícitas. Las pruebas enfocadas pasan 2/2; CI ya ejecuta la suite CLI. `operacion/scripts/Update-TuiVisualSnapshots.ps1` permite regenerar las capturas solo mediante ejecución explícita y el perfil `test-engineer` ordena revisar el cambio antes de actualizarlas. CAP-031 ya tiene verificación visual automatizada; CAP-028–030 siguen pendientes.
- 2026-11-04 — CAP-031 estabilidad: los snapshots comparan las pantallas de bienvenida y sesión, normalizando solo el valor dinámico de RAM (`<RAM>`). Generación explícita y comparación normal pasan 2/2 cada una. Suite completa Release: 842/842 (Arquitectura 22, Core 297, CLI 129, Infraestructura 394). CAP-028–030 siguen abiertas para aceptación funcional real completa.
- 2026-11-04 — T-022 continúa como punto activo: suite Release 845/845; se afinó la corrección de planes con listas desalineadas y la prueba focalizada pasa 1/1. La ejecución real en Preguntar solo devolvió código, y `/avanzar` terminó sin cambios ni verificación semántica; CAP-028–031 siguen pendientes de aceptación mediante Codificar.

**T-022 — continuidad 2026-11-04:** CAP-016/017: JSON de acción solicitado por transporte; pruebas focalizadas 3/3. CAP-028 real: dos corridas Codificar con qwen2.5-coder:3b cerraron honestamente sin cambios ni checks (121,9 s por loop/JSON inválido; 146,2 s por rechazo de planes). Incidentes registrados en T-022. Siguiente foco: calibración de riesgo para pruebas unitarias y detección de planes repetidos; CAP-028–031 siguen pendientes.

**T-022 — avance 2026-11-04:** CAP-006 calibrada para no tratar una exclusión como autorización de una operación crítica; pruebas 7/7 unitarias y 1/1 integrada. El reintento TUI real redujo el bloqueo de riesgo, pero falló al corregir arreglos desalineados del plan; no creó archivos ni ejecutó comprobaciones. CAP-028–031 continúan pendientes. Siguiente foco: corrección de planes JSON y tope de ciclos repetidos.

**Última validación real CAP-028–031 — 2026-11-04:** Codificar con `qwen2.5-coder:3b` creó `Sample.Tests/CalculatorTests.cs`, compiló tras restaurar y ejecutó el caso filtrado; la prueba falló (esperado 2, actual -8) porque `Sample/Calculator.cs` resta. La producción estaba excluida y no se modificó; Plan B no completó la corrección. Incidente: `%TEMP%\condor-cap028-live-final-20261104\docs\20261104-214155467-incidente-del-agente-3b02ccd9925f4fcab575b44ce223aa84.md`. CAP-028–031 siguen pendientes de aceptación completa. Siguiente: reportar el fallo real contra producción excluida sin modificar ni debilitar el test y continuar con la evidencia restante.

**Actualización de recuperación CAP-028 — 2026-11-04:** `AgentService` ahora conserva una prueba y comunica el esperado/obtenido cuando una tarea de creación de tests falla por aserción y el código probado está excluido; detiene Plan B para no debilitar el test ni tocar la exclusión. Proyecto xUnit temporal con build/test real: esperado 2, actual -8; test y código excluido permanecen intactos (1/1). Core 3/3; Infraestructura 4/4 pruebas focalizadas. La última revalidación en Codificar con `qwen2.5-coder:3b` no llegó a escribir: rechazó dos planes y se detuvo sin cambios ni pruebas; incidente `%TEMP%\condor-cap028-live-verified-20261104\docs\20261104-214953545-incidente-del-agente-da0e9653788640ed8a485d1e9af0e638.md`. CAP-028–031 permanecen pendientes de aceptación funcional completa. Siguiente: mejorar la corrección real de planes y continuar la matriz.

**Continuidad T-022 — planes, 2026-11-04:** Plan B ahora incluye el rechazo del último plan al recuperarse de una observación repetida; prueba integrada 1/1. Validación focalizada acumulada: Core 3/3, Infraestructura 5/5. La última corrida TUI con `qwen2.5-coder:3b` aún falló antes de escribir (listados repetidos, `done`/`create_file` y plan rechazados; 0 cambios/pruebas), incidente `%TEMP%\condor-cap028-live-finalcheck-20261104\docs\20261104-215605784-incidente-del-agente-8213f67b08a64c03ab321ae76f3625bc.md` (verificar nombre en disco). Condor canceló cambiar a `qwen2.5-coder:7b`; la aceptación funcional CAP-028–031 continúa pendiente. Siguiente: un plan determinista para cambios simples de un archivo y recuperación transparente de planes rechazados.

**Continuidad T-022 — 2026-11-04:** CAP-006: la negación explícita de una ruta crítica excluida ya no convierte por sí sola la tarea en crítica; una ruta crítica objetivo conserva el gate. CAP-016/017 y CAP-028: plan determinista de una tarea para escritura de archivo exacto con riesgo bajo/medio, alcance y exclusiones registrados. Pruebas: Core 9/9; creación/harness integrada 1/1; regresiones de planificación 5/5. La TUI en Preguntar no produjo evidencia ni cambios; CAP-028–031 siguen pendientes de aceptación real en Codificar. Próximo foco: repetir con el directorio correcto y validar generación, prueba filtrada y preservación de producción excluida.

Actualización: los dos escenarios integrados de plan acotado y recuperación pasan (2/2). La última TUI se ejecutó en Preguntar, sin cambios ni comprobaciones; CAP-028–031 continúan abiertas para validación en Codificar.

Evidencia: selector Codificar por teclado 1/1; corrida real en fixture detenida antes de herramientas por memoria insuficiente con qwen2.5-coder:0.5b (13,4 s), sin cambios ni pruebas. Incidente en `%TEMP%\condor-cap028-auto-plan-20261104\docs\20261104-222310571-incidente-del-agente-07dca84f4aac4a97a5df871d287048e2.md`. Plan acotado/recuperación: 2/2; CAP-028–031 siguen pendientes de aceptación con ejecución real.

CAP-041/CAP-042: verificado que `CS0029` durante build bloquea el cierre (1/1); build Release de solución completa: 0 errores y 18 advertencias CA1502 de complejidad. Fase 4 continúa abierta.

Suite completa `AgentHarnessTests`: 9/9, incluido el caso de tipos que bloquea el cierre.

CAP-028–CAP-030: TUI por teclado → Codificar → router → AgentService → test xUnit/harness pasa como recorrido integrado (1/1); resultado rojo esperado 2/actual -8, test conservado, producción excluida intacta y reporte honesto. Falta validar generación con inferencia real; el presupuesto seguro de RAM lo impidió en el último intento.

Alcance: la prueba usa `TuiInput` y las funciones de ruta/despacho compartidas por Condor, con respuestas de modelo controladas; no inicia `CondorTui.RunAsync` ni valida inferencia local. La aceptación funcional completa de CAP-028–CAP-030 sigue abierta.

**Continuidad T-022 2026-11-04:** CAP-011 y CAP-028–CAP-030: la entrega tras Plan B refleja el estado “build/test no aplican” guardado en el checkpoint. La regresión pasa 1/1; suite Infraestructura 405/405 y TuiShortcutTests 21/21. El recorrido TUI→AgentService→harness prueba preservar un xUnit rojo y la ruta de producción excluida (1/1). La inferencia/generación dentro de CondorTui.RunAsync con modelo real sigue pendiente.
**Continuidad de calidad T-022 2026-11-04:** CAP-045: se separaron las acciones de /metricas; DeliveryMetricsCommand.ExecuteAsync ya no supera el umbral CA1502. Pruebas focalizadas 4/4, CLI 130/130; reconstrucción limpia de CLI y dependencias: 0 errores y 17 advertencias de complejidad, ninguna de /metricas. Reducir las restantes sigue en curso.
**Continuidad de calidad T-022 2026-11-04:** CAP-045: SelectListRenderer.Render se separó en cálculo de geometría y renderizado de fila conservando la presentación; SelectListTests 17/17. Tras ambos refactors, CLI 130/130 y reconstrucción limpia de CLI/dependencias sin errores con 16 advertencias CA1502; DeliveryMetricsCommand.ExecuteAsync y SelectListRenderer.Render ya no están entre ellas. Las advertencias restantes siguen en curso.
**Continuidad de calidad T-022 2026-11-04:** CAP-045: BuildFrame se dividió entre pintado completo y de regiones sucias sin cambiar los estados de presentación; CLI 130/130. La reconstrucción limpia de CLI y dependencias queda en 13 advertencias CA1502 y 0 errores. Ya no aparecen DeliveryMetricsCommand.ExecuteAsync, SelectListRenderer.Render, TuiInput.Handle, TuiInput.HandleSelectList ni TuiHost.BuildFrame; reducir las restantes continúa en curso.
**Continuidad T-022 2026-11-04:** CAP-045: TryRepairMissingRequiredHtmlLinkAsync se dividió entre selección de candidatos y operaciones de reparación seguras; ya no supera CA1502. AgentHarnessTests 9/9, reparación integrada 1/1 y pruebas combinadas afectadas 27/27. La regresión de modelo inexistente ahora simula HTTP 404 y no depende de un servidor Ollama local. Reconstrucción limpia de la solución: 0 errores y 12 advertencias CA1502. El rerun completo de Infraestructura no terminó; la aceptación global sigue abierta.
**Continuidad de calidad T-022 2026-11-04:** CAP-045: se extrajo de AgentService.RunAsync la preparación de la puerta de revisión independiente/multiagente, incluidos el límite de rondas y la revisión delta. Release de Condor.slnx: 0 errores y 7 advertencias CA1502; complejidad de RunAsync bajó de 349 a 329. No se ejecutaron pruebas en esta continuación; el rerun completo de Infraestructura aún no concluye.

**Continuidad de calidad T-022 2026-11-04:** CAP-045: IsContentExtension ahora consulta un conjunto de extensiones normalizadas; conserva la lista permitida y deja de generar CA1502. Build Release: 0 errores, 7 advertencias CA1502. No se ejecutaron pruebas en esta continuación.

**Continuidad de calidad T-022 2026-11-04:** CAP-045: RunDirectConsultAsync separa la selección/bloqueo de recursos del flujo de inferencia y entrega; EnsureModelAsync delega la reevaluación previa a descarga. Ambos quedan bajo CA1502. Build Release de Condor.slnx: 0 errores y 4 advertencias CA1502, sin advertencias nuevas de nulabilidad. No se ejecutaron pruebas en esta continuación.

**Continuidad de calidad T-022 2026-11-04:** CAP-045: BuildInventoryAsync separa inventario base y detectores opcionales; ValidateReviewFindings coordina la validación de campos, evidencia literal y atribución del cambio. Ambos dejan de generar CA1502. Build Release: 0 errores y 3 advertencias CA1502. No se ejecutaron pruebas en esta continuación.

**Continuidad de calidad T-022 2026-11-04:** CAP-045: ModelPromptBuilder.BuildSystemPrompt separa la composición de instrucciones del perfil y acciones; queda bajo CA1502. Build Release completo sin incremental: 0 errores, 6 advertencias CA1502 en AgentEngine.ValidateAction, AgentEngine.IsReadableContent, ModelSelector.SelectForTask, ModelSelector.BuildInstalledCandidate, AgentService.RunAsync y CondorTui.RunAsync. git diff --check sin errores; esta continuación no ejecutó pruebas.

**Continuidad de calidad T-022 2026-11-04:** CAP-045: ModelSelector.SelectForTask y BuildInstalledCandidate, y AgentEngine.ValidateAction e IsReadableContent, dejan de superar CA1502 tras dividir la selección de modelos y la validación por familias. Build Release completo sin incremental: 0 errores y 2 advertencias CA1502 restantes (AgentService.RunAsync=329, CondorTui.RunAsync=104). git diff --check limpio; esta continuación no ejecutó pruebas.

**Continuidad de calidad T-022 2026-11-04:** CAP-045: `CondorTui.RunAsync` separa preparación del entorno, configuración de sesión, actualización de RAM/cabecera, entradas ocupada/libre, despacho de comandos y finalización; deja de superar CA1502. En `AgentService.RunAsync` se extraen la salida por modelo no disponible, la aplicación de requisitos agregados durante la ejecución, la reparación HTML acotada de Plan B y la admisión/recuperación de acciones estructuradas: complejidad baja de 329 a 276, aún sobre el umbral 26. Build Release completo sin incremental: 0 errores y 1 advertencia CA1502 (`AgentService.RunAsync=276`). Esta continuación no ejecutó pruebas; la reducción de complejidad del agente sigue abierta.

**Continuidad de calidad T-022 — 2026-11-04:** CAP-045: se dividió el cierre de acciones y se aisló la solicitud y recuperación del modelo; `AgentService.RunAsync` bajó de complejidad 276 a 124. Build Release completo sin incremental: 0 errores, 1 advertencia CA1502 (`RunAsync=124`). No se ejecutaron pruebas; la fase continúa abierta.

**Continuidad de calidad T-022 — 2026-11-04:** CAP-045: se separó el flujo de acciones admitidas y la verificación de cambios; `AgentService.RunAsync` bajó de complejidad 276 a 77, con un único aviso CA1502 en ese método. Build Release: 0 errores. No se ejecutaron pruebas; fase abierta.

**Continuidad de calidad T-022 — 2026-11-04:** CAP-045: el control de progreso por iteración quedó separado; `AgentService.RunAsync` bajó de complejidad 77 a 69. Build Release: 0 errores y 1 advertencia CA1502. Sin pruebas en esta continuación; quedan pendientes las validaciones reales de CAP-046/047.

**Continuidad de calidad T-022 — 2026-11-04:** CAP-045: se encapsuló el control de progreso y recuperación Plan B; `AgentService.RunAsync` bajó de 77 a 69. Build Release: 0 errores y 1 advertencia CA1502. Sin pruebas en esta continuación. Las validaciones reales de CAP-046/047 siguen pendientes.

**Continuidad de calidad T-022 — 2026-11-04:** CAP-045: se separaron la restauración de continuidad y el manejo de ambigüedad; `AgentService.RunAsync` bajó de 69 a 64. Build Release: 0 errores, 1 advertencia CA1502. No se ejecutaron pruebas; fase abierta.

**Continuidad T-022 — 2026-11-04:** CAP-045: `RunAsync` bajó de complejidad 64 a 49; Build Release: 0 errores y 1 advertencia CA1502. CAP-048: protección remota de `main` exige los checks `Condor CI / build-and-test` y `Condor CI / secret-scan`, con base actualizada y enforcement para administradores. GitHub todavía lista cero workflows; no se publicó ni ejecutó CI.

Continuidad T-022 2026-11-04 — Selector TUI reutiliza modelo confirmado por /api/ps y mantiene gate de RAM para modelos no residentes; builds limpios. Prueba funcional en fixture: TUI sin salida tras >2 min en análisis, sin cambios; CAP-028–031 no aceptadas. Bloqueo activo: ciclo de inferencia/análisis; retomar diagnóstico y luego la validación real.

Continuidad T-022 2026-11-04 — Reparada la selección de contexto por directorio en el planificador/ciclo. Evidencia: /avanzar reporta el fixture correcto; 0 cambios aplicados, semántica no_disponible; builds limpios. CAP-028–031 no se marcan aceptadas; siguiente bloqueo: generación real TDD por agente y verificación semántica.

**Continuidad T-022 — 2026-11-05:** el prompt ya indica explorar la carpeta padre de archivos nuevos, pasar de observación a plan y reparar el archivo/línea del diagnóstico. Prueba enfocada `ModelPromptBuilderTests`: 6/6. En TUI con `qwen2.5-coder:3b`, Condor escribió una prueba con contenido incorrecto; el build detectó CS1002, pero Plan B no reparó ese archivo y el test solicitado no se ejecutó. El reintento permaneció en análisis más de dos minutos. CAP-028–CAP-031 siguen abiertas; próximo foco: reparación determinista guiada por diagnóstico y nueva aceptación TUI completa.

**Continuidad T-022 — 2026-11-05:** Condor rechaza en `create_file`/`edit_file` que el contenido de un archivo fuente sea solo la ruta o el nombre; el prompt exige código completo y reparar el primer diagnóstico real de build/test. `AgentToolsetTests` + `ModelPromptBuilderTests`: 16/16. Aún falta repetir el caso en TUI con modelo local después de la barrera; la corrida real previa no reparó el test inválido. CAP-028–CAP-031 siguen abiertas y T-022 continúa activa.

**Continuidad T-022 — 2026-11-05:** Plan B ahora explica cómo recuperar cuando el contenido fuente repite solo la ruta o el nombre. El caso integrado confirma rechazo, reintento con instrucción específica y creación válida; pruebas enfocadas de herramienta/prompt/Plan B: 17/17. No se ha repetido aún la ejecución TUI con modelo local; CAP-028–CAP-031 permanecen abiertas.

Validación adicional 2026-11-05: la ampliación del TUI simulado con el error de ruta quedó sin salida por más de un minuto y se interrumpió; no cuenta como aceptación. La barrera conserva 17/17 pruebas enfocadas, sin atribuirle una validación TUI real.

**Continuidad T-022 — 2026-11-05:** CAP-031: snapshots visuales de bienvenida y sesión pasan 2/2 bajo `--artifacts-path`; los baselines se copian con el proyecto de pruebas y la herramienta de actualización conserva una ruta fuente explícita. La evidencia queda registrada en T-022; revisión independiente CAP-032 continúa como próximo foco.



**Continuidad T-022 — 2026-11-05:** CAP-032: `done` ahora cierra por la ruta de control de Condor y no como acción de herramienta; las verificaciones de una modificación todavía continúan el plan. La prueba de revisión delta confirma alcance limitado a los cambios posteriores al replan y la presencia de ambos perfiles independientes. Validación focalizada de política, delegación y cierre: 9/9. Los dobles de prueba no reemplazan la aceptación con inferencia real; T-022 sigue activa.

**Continuidad T-022 — 2026-11-05:** CAP-039: se comprueban replan obligatorio en la tercera ronda y detención sin delegación en la quinta, además de la revisión delta final. Conjunto focalizado: 11/11. Evidencia automatizada con dobles; sigue pendiente observar inferencia real para la aceptación funcional.

**Continuidad T-022 — 2026-11-05:** CAP-036–CAP-038: los hallazgos trazables conservan evidencia y reproducción, el defecto preexistente se separa del diff nuevo y un hallazgo abierto impide cerrar. Conjunto de revisión y delegación: 13/13; no se afirma validación con inferencia real.

**Continuidad T-022 — 2026-11-05:** CAP-040: los checks locales de formato y estilo detectaron y permitieron corregir una sangría incorrecta; después, ambos verificadores pasaron sobre la solución completa.

**Continuidad T-022 — 2026-11-05:** CAP-041/CAP-042: compilación Release completa en salida aislada, 0 advertencias y 0 errores; se preservó la instancia Condor que mantenía bloqueadas las DLL normales.

**Continuidad T-022 — 2026-11-05:** CAP-028: la recuperación estructurada ya dispone de una iteración explícita para corregir JSON inválido, y los dos escenarios integrados pasan. CAP-043: build Release 0/0 y suites completas 863/863 en salida aislada. La ejecución remota de GitHub Actions sigue pendiente.

**Continuidad T-022 — 2026-11-05:** CAP-044: Gitleaks 8.30.1, con checksum verificado, escaneó el checkout y 627 commits; no encontró hallazgos.

**Continuidad T-022 — 2026-11-05:** CAP-045: los aumentos de complejidad quedan bajo el umbral CA1502=25. CAP-046: se revisaron las señales de duplicación de procesos Git y dobles de prueba; no se consolidaron porque tienen límites y escenarios distintos. CAP-047: la medición de inferencia real sigue pendiente por indisponibilidad de Ollama.

**Continuidad T-022 — 2026-11-05, ejecución real CAP-028/030:** el fixture .NET comenzó con la prueba de suma fallida (`Expected 2, Actual -8`). Desde la TUI, Condor modificó solo `Calculator.cs`; tras una aclaración porque el modelo pidió contenido faltante, restauró dependencias, compiló y pasó la prueba. La ejecución independiente de `dotnet test` confirma 1/1 y el archivo de prueba quedó intacto. Un caso distinto confirmó la creación y lectura posterior de un archivo en la raíz. El recorrido TUI duró 264,8 s y Ollama registró cerca de 15,2 tokens/s; es una observación no comparable, no un benchmark controlado. La prueba completa de Infraestructura quedó sin salida final y se interrumpió; no se cuenta como pase. CAP-029 y la aceptación global de Fase 3 siguen abiertas; CAP-031 conserva sus snapshots 2/2.

**Continuidad T-022 — 2026-11-05:** CAP-014 volvió a verificarse con `/ayuda`, `/version` y `/salir` en una sola sesión TUI. CAP-064–CAP-066: persistencia manual desde TUI/CLI y vínculo de `AgentService` a cambio en curso e incidente abierto confirmados; la tarea guarda su identificador de ejecución (1/1). La ampliación al escenario de incidente también pasa localmente 1/1; queda pendiente su CI remota y la aceptación con modelo local dentro de la TUI. El run 42061068056 pasó las cuatro suites (870/870). T-022 sigue activa; continúo con la validación TUI y las capacidades siguientes.

**Continuidad T-022 — 2026-11-05:** CAP-043: ambos runs previos revelaron que el runner sin Ollama recibía `Note` vacío; `OllamaDetector` ahora explica que no hay instalación ni servidor. Prueba focalizada local 1/1. El run 42061068056 confirmó la corrección y pasó formato, estilo, build, escaneo de secretos y las cuatro suites (870/870).

**Continuidad T-022 — 2026-11-05:** CAP-023: publicación aislada por huella probada con una instancia anterior activa; `condor --help` compiló, arrancó (código 0) y luego reutilizó el binario. Dos delegaciones concurrentes escribieron el mismo archivo en sus worktrees sin interferir (1/1); la copia principal quedó intacta. CI 42063628431 pasó 870/870 para el commit base; 42064771195 fue cancelado y no cuenta como pase.

**Continuidad T-022 — 2026-11-05:** CAP-024: los eventos del observador se renderizan por `TuiAgentProgressView`; el frame de `TuiHost` confirma identidad de ambos agentes, terminación, duración y archivo cambiado `Shared.md`. `AgentDelegationServiceTests` pasa 3/3, con límite de ejecución y cancelación controlada. La validación es automatizada, sin inferencia local. Los runs 42065301435 y 42065879766 no cubren esta última revisión; falta check remoto para el commit actual.

**Continuidad T-022 — 2026-11-05:** los jobs remotos `secret-scan` y `build-and-test` del run 42066296488 para `4279b74` finalizaron correctamente. CAP-025 añade una integración que comprueba el presupuesto del hijo en el `LlmRequest` real (128 tokens frente al máximo general de 1024); focalizada 1/1. Este test nuevo aún requiere CI remota.

**Continuidad T-022 — 2026-11-05:** CAP-027: la regla de evidencia suficiente para `done` pasa 12/12 pruebas focalizadas. La revalidación del harness no se cuenta como pase porque la salida compartida estaba bloqueada por la instancia activa; el run 42067922196 para `6b8ddb0` completó exitosamente.

**Continuidad T-022 — 2026-11-05:** CAP-029 cubre éxito y fallo integrados: `RunAsync_CorrigeProyectoReal_VerificaBuildYPruebasAntesDeCerrar` pasa 1/1; `AgentServiceResourceBlockTests` pasa 53/53. El éxito recorre herramienta, Git, harness, restore/build/xUnit y revisión `git_diff`; el fallo TUI conserva el test y respeta código excluido. Validado en worktree aislado con modelo simulado. El criterio CAP-029 queda cubierto; la aceptación global de Fase 3 sigue pendiente. El run 42067922196 de `6b8ddb0` terminó exitosamente; el run 42071259054 de `88fce0c` pasó `secret-scan`, pero `build-and-test` fue cancelado por una publicación posterior y no cuenta como pase.

**Continuidad T-022 — 2026-11-05:** CAP-033: el revisor contextual lee el componente cambiado y su dependiente, detecta y reproduce la regresión, bloquea el cierre y vuelve a aprobar tras la corrección sin modificar archivos. `RunAsync_PerfilReviewer_RespetaSoloLecturaYRechazaEdicion` y `RunAsync_PerfilReviewer_DetectaRegresionRelacionadaYApruebaTrasCorregirla`: 2/2 en Release aislado. Cliente simulado.

**Continuidad T-022 — 2026-11-05:** CAP-034: la prueba de regresión llama a `OrderTotals.Total`, que depende de `Calculator.Add`; falla antes del agente contra el commit base y pasa tras corregir la causa con el harness. CAP-034 focalizada: 1/1; CAP-033 relacionadas + CAP-034: 3/3 en Release aislado. Build/test son reales; cliente simulado. La clase completa no concluyó y no se cuenta. Fase 3 global sigue pendiente.

**Continuidad T-022 — 2026-11-05:** CAP-035 ya implementada: revisión multiagente requiere perfiles independientes, contrasta el mismo diff, pasa hallazgos estructurados y mide falsos positivos; el conjunto focalizado pasa 10/10. CAP-036 ya implementada: hallazgo reproducible/trazable bloquea el cierre y conserva sus datos (1/1). CAP-037 ya implementada: los defectos previos a la base no se atribuyen al cambio actual (1/1). Validación Release aislada con clientes simulados; sin inferencia real. Aceptación global de Fase 3 pendiente.

**Continuidad T-022 — 2026-11-05:** CAP-038/039 ya implementadas; 5/5 escenarios Release: un hallazgo crítico mantiene bloqueado el cierre, la tercera ronda obliga a replantear y cambiar, la quinta detiene el ciclo y la revisión delta queda limitada a los cambios nuevos. La prueba crítica fue reforzada; modelos simulados. Fase 3 global sigue pendiente.

**Continuidad T-022 — 2026-11-05:** CAP-040 ya implementada y revalidada: el formato detectó una infracción temporal con errores `WHITESPACE` y código 2; retirado el archivo de prueba, whitespace y style error pasan sobre la solución completa. `git diff --check` también pasa.

**Continuidad T-022 — 2026-11-05:** CAP-041/042 ya implementadas y revalidadas: `CS0029` en el fixture impide el cierre (AgentHarnessTests 9/9), y build Release completo pasa con 0 advertencias/errores en salida aislada. Fase 4 sigue pendiente.

**Continuidad T-022 — 2026-11-05:** CAP-043, secuencia local de CI: formato, estilo, build Release (0 advertencias/errores) y suites 873/873. Infraestructura pasó 417/417 al usar un worktree llamado `condor`, como exige una prueba E2E de detección. La ejecución GitHub sigue pendiente y la protección de rama no se ha podido confirmar.

**Continuidad T-022 — 2026-11-05:** CI remoto 42077018439 para `80368f1` pasó ambos jobs: NuGet Audit, formato, estilo, build, pruebas y Gitleaks del checkout/historial. Se omitieron comparaciones de complejidad/duplicación porque el evento fue push. Protección de rama sin confirmar (API 401); CAP-043/044 siguen en curso.

**Continuidad T-022 — 2026-11-05:** CAP-045 comparó 261 métodos entre `origin/main` y la rama actual mediante worktrees y builds aislados; no hay métodos actuales sobre CA1502=25. La complejidad de la rama queda dentro del límite medido.

**Continuidad T-022 — 2026-11-05:** CAP-058 registra en el informe cada observación resuelta o aceptada/descartada con ruta, gravedad, descripción y justificación, y presenta proporciones individual e histórica de hallazgos útiles. DocumentingAgentServiceTests: 6/6. La secuencia continúa con CAP-059; la aceptación global de Fases 1–4 sigue pendiente.
