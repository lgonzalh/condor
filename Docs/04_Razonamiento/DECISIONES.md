# DECISIONES

Version: 3.0.0
Estado: Activo
Nivel: 04 - Diseno
Clasificacion: Decisiones Arquitectonicas

---

# Proposito

Registrar las decisiones arquitectonicas permanentes adoptadas durante el Nivel 04 del Proyecto Condor y las decisiones de evolucion registradas posteriormente mediante el ciclo de Evolucion Continua.

---

# DEC-001

Titulo:
Arquitectura por capas.

Decision:
El sistema se organizara en capas independientes con responsabilidades claramente definidas.

Estado:
Aceptada.

---

# DEC-002

Titulo:
Orquestador central.

Decision:
La coordinacion de todos los motores sera realizada exclusivamente por el Orquestador.

Estado:
Aceptada.

---

# DEC-003

Titulo:
Comunicacion mediante interfaces.

Decision:
Los componentes dependeran de contratos y nunca de implementaciones concretas.

Estado:
Aceptada.

---

# DEC-004

Titulo:
Motores desacoplados.

Decision:
Los motores especializados no podran comunicarse directamente entre si.

Estado:
Aceptada.

---

# DEC-005

Titulo:
Persistencia del conocimiento.

Decision:
Toda decision permanente debera registrarse mediante documentacion oficial.

Estado:
Aceptada.

---

# DEC-006

Titulo:
Evolucion incremental.

Decision:
La arquitectura permitira incorporar nuevos componentes sin redisenar el sistema completo.

Estado:
Aceptada.

---

# DEC-007

Titulo:
Tecnologia de implementacion del MVP 1.0.

Decision:
El MVP 1.0 se implementa con .NET 10 y C#, con proyectos separados para CLI, nucleo de dominio e infraestructura.

Esta es una decision de implementacion y no constituye una dependencia arquitectonica permanente.

Los contratos definidos en Condor.Core permanecen agnosticos a la tecnologia conforme a CONTRATOS_COMPONENTES.md.

Estado:
Aceptada.

Origen:
T-001 (Bootstrap del MVP y Assessment inicial).

---

# DEC-008

Titulo:
Estado local de Condor separado del conocimiento persistente del proyecto.

Decision:
El estado operativo local de Condor (por ejemplo, el resultado del Assessment) se almacena en el directorio de estado del usuario `%LOCALAPPDATA%\Condor\state\`.

Este estado local es transitorio y no constituye conocimiento persistente del proyecto.

La documentacion oficial en `Docs/` y `operacion/` constituye el conocimiento permanente y no puede ser reemplazada por estado local.

El mecanismo de almacenamiento es una propuesta de implementacion versionable y no un contrato arquitectonico congelado.

Estado:
Aceptada.

Origen:
T-001.

---

# DEC-009

Titulo:
Contrato AssessmentResult versionable.

Decision:
El resultado del Assessment se define mediante el esquema `AssessmentResult` version 1 en Condor.Core.

El esquema es versionable y evolucionara en el futuro sin considerarse inmutable.

La version del esquema se registra dentro del propio resultado serializado.

Estado:
Aceptada.

Origen:
T-001.

---

# DEC-010

Titulo:
Separacion de proyectos del MVP.

Decision:
El MVP se organiza en tres proyectos de codigo:

- `Condor.Cli`: entrada, interpretacion de argumentos y presentacion en terminal.
- `Condor.Core`: contratos, modelos de dominio y logica pura independiente de la plataforma.
- `Condor.Infrastructure`: implementaciones especificas de Windows (deteccion de hardware, herramientas y Ollama).

Las pruebas se organizan en proyectos separados: Unit (Condor.Core), Integration (Condor.Infrastructure) y Architecture (reglas de referencias).

Estado:
Aceptada.

Origen:
T-001.

---

# DEC-011

Titulo:
Idioma y convenciones en codigo.

Decision:
El codigo y los mensajes se ajustan estrictamente a las convenciones establecidas por las directivas de Condor:

- Idioma oficial: espanol latinoamericano.
- Los nombres tecnicos, identificadores, carpetas y archivos no utilizan tildes, acentos ni la letra n con tilde.
- Los mensajes de interfaz se presentan en espanol sin tildes, conforme a la directiva operativa.

No se incorporan reglas linguisticas adicionales.

Estado:
Aceptada.

Origen:
T-001.

---

# DEC-012

Titulo:
Correccion de referencias documentales.

Decision:
Se corrigen las referencias documentales de T-001 y AGENTE_CONDOR:

- `INTERFAZ_v2.md` se reemplaza por `Docs/07_Interfaz/INTERFAZ.md`.
- `EXPERIENCIA_USUARIO_v2.md` se reemplaza por `Docs/07_Interfaz/EXPERIENCIA_USUARIO.md`.
- `operacion/AGENTE_CONDOR.md` se reemplaza por la ubicacion real del contrato en la raiz del repositorio.

La correccion de ESTRUCTURA_REPOSITORIO.md queda registrada en DEUDA_EVOLUTIVA.md como deuda documental resuelta.

Estado:
Aceptada.

Origen:
T-001.

---

# DEC-013

Titulo:
Comando condor ask para inferencia local.

Decision:
T-002 introduce el comando:

`condor ask "<mensaje>" [--model <modelo>]`

para ejecutar una inferencia local mediante Ollama, coherente con la interfaz de terminal de Condor 1.0.

El comando es minimo y no interactivo; los flujos conversacionales completos pertenecen a tareas posteriores.

Estado:
Aceptada. Actualizada por DEC-025 (correccion del contrato publico al espanol).

Origen:
T-002.

---

# DEC-014

Titulo:
Extension aditiva de IStateStore con lectura del Assessment.

Decision:
Se agrega de forma aditiva `LoadAssessmentAsync` al contrato `IStateStore` para permitir consumir el resultado del Assessment persistido.

El metodo de escritura existente (`SaveAssessmentAsync`) no se modifica.

La extension es compatible con el esquema `AssessmentResult` 1.0.0 (DEC-009) y no constituye un cambio de contrato destructivo.

Estado:
Aceptada.

Origen:
T-002.

---

# DEC-015

Titulo:
Estrategia provisional de seleccion de modelo.

Decision:
En T-002 el modelo se selecciona asi:

1. El modelo explicito indicado con `--model` si fue proporcionado.
2. En caso contrario, temporalmente, el primer modelo disponible en el Assessment.

Esta estrategia es provisional. T-003 (Recomendador de modelos) sera responsable de la recomendacion inteligente basada en hardware, capacidades y modelos disponibles.

Estado:
Aceptada.

Origen:
T-002.

---

# DEC-016

Titulo:
Comunicacion con Ollama mediante /api/chat local sin streaming.

Decision:
La integracion con Ollama usa el endpoint local `http://127.0.0.1:11434/api/chat` con `stream: false`.

La comunicacion es exclusivamente loopback (127.0.0.1) y no introduce dependencias externas ni servicios cloud.

El streaming queda fuera de T-002 y se evaluara cuando los motores posteriores (Builder, Verifier) lo requieran.

Estado:
Aceptada.

Origen:
T-002.

---

# DEC-017

Titulo:
Tarea T-002 y actualizacion de artefactos operativos.

Decision:
T-002 se formaliza mediante la creacion de `operacion/TAREAS/T-002.md` y la actualizacion de los artefactos operativos correspondientes (ESTADO_DESARROLLO, BACKLOG, KANBAN, REGISTRO_CAMBIOS, RELEVO e INVENTARIO_ARQUITECTURA) conforme al contrato AGENTE_CONDOR.md.

Estado:
Aceptada.

Origen:
T-002.

---

# DEC-018

Titulo:
Rama de trabajo de T-002.

Decision:
La implementacion de T-002 se realiza exclusivamente en la rama `feature/T-002-ollama` creada desde `main`.

La integracion a `main` se realizara posteriormente mediante revision y merge, sin eliminar la rama.

Estado:
Aceptada.

Origen:
T-002.

---

# DEC-019

Titulo:
Campos de ModelInfo ampliados para la recomendacion.

Decision:
`ModelInfo` incorpora de forma aditiva los campos `ContextLength`, `Capabilities`, `ParameterSize` y `Quantization` (normalizado) para describir el inventario real de Ollama.

La ampliacion es compatible con el esquema `AssessmentResult` 1.0.0 (DEC-009) y no modifica los campos existentes.

Estado:
Aceptada.

Origen:
T-003.

---

# DEC-020

Titulo:
Mapeo normalizado de la respuesta de Ollama /api/tags.

Decision:
El inventario de modelos de Ollama se consume mediante el parser `OllamaTagsParser` (Infrastructure), que mapea el formato snake_case real de la API (`parameter_size`, `quantization_level`, `context_length`, `capabilities`) y admite tanto el objeto `models` como el arreglo plano que emite `ollama list --format json`.

Un modelo con datos parciales no descarta el resto del inventario.

Estado:
Aceptada.

Origen:
T-003.

---

# DEC-021

Titulo:
Recomendador como logica pura en Condor.Core.

Decision:
El recomendador (`ModelRecommender`) y sus componentes de evaluacion (`ModelRoleClassifier`, `ModelMemoryBudget`) residen en `Condor.Core.Evaluation` y son logica pura: no realizan I/O, no dependen de infraestructura ni de frameworks.

Reciben el `AssessmentResult` persistido y producen `ModelRecommendationResult`.

Estado:
Aceptada.

Origen:
T-003.

---

# DEC-022

Titulo:
Heuristica de memoria ajustable y calibrada.

Decision:
La viabilidad por memoria se calcula con `ModelMemoryBudget`: pico estimado = tamano del modelo x 1,2; presupuesto = maximo entre (RAM libre - reserva) y (RAM total x 0,45).

Los factores son constantes publicas aisladas para calibrarse en el futuro con mediciones reales. La calibracion inicial se realizo con el equipo de desarrollo real, donde un modelo 7B Q4 (4,36 GB) es viable y un 8B queda al limite.

Estado:
Aceptada.

Origen:
T-003.

---

# DEC-023

Titulo:
Comando condor recommend con proposito.

Decision:
T-003 introduce el comando:

`condor recommend [--purpose <tipo>]`

con tipos `development` (por defecto), `general` y `vision`. La recomendacion lee el Assessment persistido, no descarga modelos y no cambia la seleccion de `condor ask`.

Estado:
Aceptada. Actualizada por DEC-025 (correccion del contrato publico al espanol).

Origen:
T-003.

---

# DEC-024

Titulo:
Prioridad de compatibilidad sobre tamano en la recomendacion.

Decision:
La recomendacion puntua candidatos viables con pesos por proposito (compatibilidad 35%, desarrollo 30%, memoria 20%, funcional 10% y estabilidad 5% para development) y no asume que el modelo mas grande es el mejor.

Los modelos que superan el presupuesto de memoria quedan descartados con su motivo; el resultado incluye motivos, alternativas y limitaciones.

Estado:
Aceptada.

Origen:
T-003.

---

# DEC-025

Titulo:
Correccion del contrato CLI al espanol sin tildes.

Decision:
El contrato publico de la CLI se traduce al espanol latinoamericano sin tildes conforme a la regla fundacional de idioma:

- `condor analizar [--json]` reemplaza a `condor assess [--json]`.
- `condor consultar "<mensaje>" [--modelo <modelo>]` reemplaza a `condor ask "<mensaje>" [--model <modelo>]`.
- `condor recomendar [--proposito <tipo>]` reemplaza a `condor recommend [--purpose <tipo>]`.
- `condor ayuda` reemplaza a `condor help`.
- `condor version` se conserva (palabra espanola sin tilde).
- Los valores publicos de proposito son `desarrollo`, `general` y `vision`.
- Se mantienen los alias tecnicos universales `-h`, `--help`, `-v`, `--version` y `--json`.

Los nombres internos (clases, metodos, namespaces y claves de `assessment.json`) no se modifican. Los comandos y argumentos ingleses previos dejan de ser contrato publico valido; los argumentos `--model` y `--purpose` se rechazan con un mensaje que indica su reemplazo.

Estado:
Aceptada.

Origen:
Auditoria del contrato CLI previa a continuar T-003. Correccion transversal, no constituye una tarea funcional nueva.

---

# DEC-026

Titulo:
Descubrimiento de proyecto (T-004).

Decision:
T-004 introduce el descubrimiento objetivo del proyecto local a partir del directorio de trabajo, que produce un perfil de proyecto aditivo al Assessment:

- El perfil se expone como campo `Project` de `AssessmentResult` y se persiste dentro de `assessment.json`; los Assessments existentes sin el campo siguen siendo validos y `SchemaVersion` se conserva en 1.0.0.
- La identidad se resuelve con 9 familias o ecosistemas de senales (C#, JavaScript/TypeScript, Python, Java, Go, Rust, C/C++, HTML/CSS y Shell/Windows) y frameworks solo con senal manifiesta.
- Los manifiestos se leen mediante contratos acotados; los formatos sin parser solo registran presencia.
- Los limites de exploracion se centralizan en un modelo unico con valores predeterminados: profundidad 6, 2000 directorios, 10000 archivos, 64 KB por manifiesto, 2 GB total, 30 segundos de descubrimiento, 10 segundos por operacion Git, 50 manifiestos, 100 dependencias, 5 cambios Git, hash de 8 caracteres y asunto de 80 caracteres.
- El descubrimiento excluye directorios generados (.git, node_modules, bin, obj, dist, build y .vs), no sigue puntos de reanalisis, no lee secretos ni binarios, consulta Git solo en modo lectura y no utiliza red.
- El resultado es determinista (orden ordinal en todas las colecciones) y se degrada por parte sin excepciones.
- La frontera con T-005 es estricta: T-004 observa y estructura; T-005 interpreta y determina el punto de continuacion.

Estado:
Aceptada.

Origen:
T-004 (formalizacion tras la aprobacion de alcance y de las decisiones D-1 a D-9).

---

# DEC-027

Titulo:
Diseno aprobado de T-004 (descubrimiento de proyecto).

Decision:
El diseno tecnico de T-004 fue aprobado para implementacion con las decisiones adicionales D-D1 a D-D7:

- D-D1: el enum `DetectionStatus` se amplia con el valor `Limited` al final de la enumeracion. Es aditivo: los valores existentes (`Detected`, `NotDetected`, `Error`) conservan su numeracion y la serializacion de Assessments guardados no cambia.
- D-D2: `ProcessProbe.RunAsync` se amplia con un directorio de trabajo opcional (valor predeterminado: comportamiento actual), necesario para ejecutar las operaciones Git dentro del proyecto descubierto.
- D-D3: los parsers de manifiestos residen en `Condor.Core.Project` como logica pura y reciben solo el texto ya acotado y metadatos necesarios; `ManifestFileReader` (Infrastructure) es el unico componente autorizado a abrir archivos de manifiesto. Los parsers no realizan IO y no reciben rutas con capacidad de apertura.
- D-D4: un manifiesto que supera el limite de 64 KB no se parsea: se registra `ParseError: true`, el `SizeBytes` real y `manifest-size` en `LimitsApplied`. No se agregan campos nuevos al modelo.
- D-D5: cuando el repositorio Git tiene HEAD detached o no tiene commits, `Branch` queda en null y `Commits` vacio, sin error (`Git.Status` en `Detected`).
- D-D6: el motivo de degradacion del descubrimiento se muestra en la seccion PROYECTO de la CLI; no se agrega a `Capabilities.Issues` para no alterar los consumidores actuales.
- D-D7: `GitRepositoryProbe` recibe el path de git desde `Tools.Git.Path` (detectado previamente por `GitDetector`) y no vuelve a buscarlo en PATH.

Estado:
Aceptada.

Origen:
Revision del diseno de T-004 previa a la implementacion.

---

# DEC-028

Titulo:
Formalizacion del contrato de T-005 (Context Engine inicial).

Decision:
El contrato de T-005 queda formalizado en `operacion/TAREAS/T-005.md` (version 1.0.0):

- T-005 reconstruye el contexto operativo del proyecto activo a partir del ProjectProfile (T-004) y de los artefactos operativos oficiales del proyecto (`operacion/`), sin LLM, sin internet y sin dependencia del historial de conversaciones;
- la frontera con T-004 es estricta: T-004 observa y estructura; T-005 interpreta, resume y determina el punto de continuacion;
- la intencion libre del usuario y la generacion de planes pertenecen a T-006; T-005 acota la capacidad "interpretar la intencion" de CONTEXT_ENGINE.md a la intencion implicita de continuar el proyecto activo;
- la capacidad se expone mediante el comando publico `condor contexto` (texto y `--json`);
- el contexto se persiste como artefacto derivado y transitorio en el estado local (`context.json`), sin modificar `assessment.json` ni el conocimiento permanente.

Estado:
Aceptada.

Origen:
T-005 (fase de formalizacion del contrato).

---

# DEC-029

Titulo:
Diseno tecnico aprobado de T-005 (Context Engine inicial).

Decision:
El diseno tecnico de T-005 fue aprobado con las decisiones D-D1 a D-D12:

- D-D1: el modelo principal se denomina tecnicamente `ProjectContext` (concepto de dominio: "contexto operativo") y reside en `Condor.Core.Models`. Los identificadores tecnicos y las claves JSON permanecen en ingles, coherentes con el esquema tecnico del Assessment (DEC-011 permite identificadores en ambas lenguas; se descarta una clase en espanol para preservar la coherencia del esquema tecnico aprobado en T-005.md). El modelo tiene `SchemaVersion` propio.
- D-D2: se reutiliza el enum existente `DetectionStatus` (Detected, NotDetected, Error, Limited) para el estado del contexto; no se amplia.
- D-D3: `IContextService` expone un unico metodo `BuildContextAsync(CancellationToken)` en `Condor.Core.Contracts`, siguiendo el patron de `IAssessmentService`; las entradas se cargan internamente desde `IStateStore` y del `WorkingDirectory` del Assessment.
- D-D4: `IStateStore` se extiende de forma aditiva con `SaveContextAsync` y `LoadContextAsync` (precedente DEC-014); `context.json` vive en el mismo directorio de estado local; serializacion UTF-8 sin BOM (precedente de correccion BOM de T-004).
- D-D5: `ContextReconstructor` reside en `Condor.Core.Context` como logica pura (patron de `Condor.Core.Project` en T-004 y de `Condor.Core.Evaluation` en T-003): recibe el Assessment, la lista de artefactos operativos ya acotados (`OperativeArtifact`) y `ContextLimits`; no realiza IO. `OperativeArtifactCatalog` en Core define los 5 nombres oficiales y la correspondencia a `OperativeArtifactKind`.
- D-D6: `OperativeArtifactReader` (Infrastructure) es el unico componente autorizado a abrir archivos; lee solo los 5 artefactos oficiales bajo `operacion/` con limite de 64 KB por archivo y orden fijo del catalogo; un archivo excesivo o inaccesible se omite con su estado y se declara en `LimitsApplied`; nunca lanza excepciones.
- D-D7: el punto de continuacion se determina con heuristica determinista de patrones textuales acotados: tareas pendientes (lineas con "T-0XX" y "pendiente"), siguiente tarea (proximidad textual de "siguiente" y "T-0XX") y ultima actividad (ultima fila "CH-0XX" de REGISTRO_CAMBIOS o, en su defecto, el ultimo commit Git del ProjectProfile); cada hallazgo registra evidencia textual; sin evidencia, `ContinuationPoint` queda en `NotDetected` con motivo, sin inventar.
- D-D8: assessment ausente o ilegible se degrada a `NotDetected` con un unico motivo ("no hay assessment disponible o ilegible; ejecuta condor analizar") porque `IStateStore` no distingue la causa; no se modifica el contrato vigente de estado (DEC-003); la alternativa de extender el contrato se descarta por no aportar valor al consumidor.
- D-D9: la persistencia de `context.json` ocurre tras cada ejecucion de `condor contexto` como artefacto derivado y transitorio (DEC-008): regenerable, sin valor permanente y sin escritura en el repositorio del proyecto.
- D-D10: la CLI incorpora `condor contexto` y `condor contexto --json` en espanol sin tildes (DEC-025); sin assessment, salida degradada con mensaje instructivo y exit code 1 (patron de T-004); el comando se agrega a la ayuda y al estado inicial.
- D-D11: determinismo: todas las colecciones se ordenan ordinalmente antes de construir el resultado (lenguajes, frameworks, riesgos por severidad y nombre, dependencias por nombre, evidencias en orden de deteccion), la lectura usa el orden fijo del catalogo y `GeneratedAtUtc` es la unica excepcion; se incluye una prueba de doble ejecucion (patron D-6 de T-004).
- D-D12: los limites del contexto se centralizan en `ContextLimits` (`Condor.Core.Context`) con valores predeterminados: 64 KB por artefacto, 5 artefactos, 400 lineas escaneadas por artefacto, 10 tareas pendientes, 8 recomendaciones y 15 segundos totales; el modelo es independiente de `DiscoveryLimits` de T-004 (congelado) para evitar acoplamiento entre motores.

Estado:
Aceptada.

Origen:
Revision del diseno de T-005 previa a la implementacion.

---

# DEC-030

Titulo:
Formalizacion del contrato de T-006 (Flujo de intencion a plan).

Decision:
El contrato de T-006 queda formalizado en `operacion/TAREAS/T-006.md` (version 1.0.0):

- T-006 implementa la version inicial del Planner (ARQ-004): transforma la solicitud del usuario en un plan de ejecucion estructurado a partir del `ProjectContext` reconstruido por T-005, sin LLM, sin internet y sin dependencia del historial de conversaciones;
- la frontera con T-005 es estricta: T-005 entrega contexto y recomendaciones; T-006 consume ese contexto, interpreta la intencion del usuario y genera el plan; la capacidad "interpretar la intencion" de CONTEXT_ENGINE.md queda delegada a T-006 (coherente con DEC-028);
- la frontera con T-007 es estricta: T-006 produce el plan; T-007 (Builder) lo consume para implementar cambios; T-006 no implementa cambios;
- la capacidad se expone mediante el comando publico `condor planear` (texto y `--json`);
- el plan se persiste como artefacto derivado y transitorio en el estado local (`plan.json`), sin modificar `assessment.json` ni `context.json`;
- la version inicial interpreta la intencion de forma acotada y determinista (nueva / continuar / modificar / indefinida) mediante heuristica textual, sin LLM ni Ollama.

Las decisiones D-E1 a D-E8 que dan forma tecnica a T-006 son:

- D-E1: el modelo principal se denomina tecnicamente `WorkPlan` (concepto de dominio: "plan de trabajo") junto con `PlanTask` y `PlanLimits`, y reside en `Condor.Core.Models` (patron D-D1). Los identificadores tecnicos y las claves JSON permanecen en ingles. El modelo tiene `SchemaVersion` propio.
- D-E2: `IPlanService` expone un unico metodo `BuildPlanAsync(string userRequest, CancellationToken)` en `Condor.Core.Contracts`, siguiendo el patron de `IAssessmentService` e `IContextService`; las entradas se cargan internamente desde `IStateStore` y la solicitud del usuario.
- D-E3: la interpretacion de la intencion en la version inicial es determinista y acotada (nueva / continuar / modificar / indefinida), por heuristica textual, sin LLM; se preserva la restriccion de T-005 y las restricciones MVP (operacion local).
- D-E4: `IStateStore` se extiende de forma aditiva con `SavePlanAsync` y `LoadPlanAsync` (precedente D-D4); `plan.json` vive en el mismo directorio de estado local; serializacion UTF-8 sin BOM.
- D-E5: `PlanGenerator` reside en `Condor.Core.Planning` como logica pura (patron D-D5): recibe el `ProjectContext`, la solicitud y `PlanLimits`; no realiza IO. `PlanService` (Infrastructure) orquesta la carga y delega en `PlanGenerator`.
- D-E6: la CLI incorpora `condor planear` y `condor planear --json` en espanol sin tildes (DEC-025); sin contexto, salida degradada con mensaje instructivo y exit code 1 (patron D-D10); el comando se agrega a la ayuda y al estado inicial.
- D-E7: determinismo: todas las colecciones se ordenan ordinalmente antes de construir el resultado y `GeneratedAtUtc` es la unica excepcion (patron D-D11); se incluye una prueba de doble ejecucion.
- D-E8: limite de frontera T-006/T-007: T-006 no implementa cambios en el proyecto objetivo; entrega el plan a T-007. No se reimplementan capacidades congeladas de T-004 ni T-005.

Estado:
Aceptada.

Origen:
Reconocimiento y formalizacion de T-006 (Flujo de intencion a plan).

---

# DEC-031

Titulo:
Diseno tecnico completo de T-006 (Flujo de intencion a plan).

Estado:
Aprobada.

Decision:
El diseno tecnico de T-006 se completa en `operacion/TAREAS/T-006.md` (version 1.1.0), siguiendo los patrones de T-004 y T-005. D-E1 a D-E8 quedan ratificados sin cambios. Se incorporan las siguientes resoluciones tecnicas del diseno, marcadas como PROPUESTA y pendientes de aprobacion humana por no estar fijadas explicitamente por el contrato:

- D-DE1: `PlanGenerator` se ubica en `Condor.Core.Planning` como logica pura (patron D-D5) y `PlanIntent` clasifica la intencion de forma determinista por heuristica textual ordinal en espanol sin tildes (orden nueva → continuar → modificar → indefinida).
- D-DE2: `PlanJson` en `Condor.Core.Serialization` sigue el patron de `ContextJson`/`AssessmentJson` (camelCase, `WriteIndented`, ignorar nulls).
- D-DE3: `PlanLimits` centralizado (patron D-D12) con valores de referencia propuestos: `MaxTasks = 12`, `MaxObjectiveLength = 240`, `MaxTaskDetailLength = 320`, `MaxEvidenceItems = 30`, `PlanTimeoutMilliseconds = 15_000`.
- D-DE4: el `WorkPlan` se construye tomando tareas base segun la intencion, una tarea por cada `PlannerRecommendation` de T-005 y tareas derivadas de riesgos (`ContextRisk`), con prioridad segun severidad, truncadas a `MaxTasks` y con `DependsOn` sobre tareas previas.
- D-DE5: `plan.json` se persiste por cada ejecucion de `condor planear`, UTF-8 sin BOM (patrones D-D4 y D-D9), sin modificar `assessment.json` ni `context.json`.
- D-DE6: la CLI `condor planear "<solicitud>"` y `condor planear "<solicitud>" --json` en espanol sin tildes; sin contexto, `NotDetected` con motivo instructivo y exit code 1.

Estas decisiones se presentan a revision formal. Su ratificacion convierte el diseno de T-006 en aprobado y habilita la implementacion autorizada.

Origen:
Diseno tecnico de T-006 (Flujo de intencion a plan).

---

# DEC-032

Titulo:
Formalizacion del contrato de T-007 (Builder inicial).

Decision:
El contrato de T-007 queda formalizado en `operacion/TAREAS/T-007.md`
(version 0.1.0, propuesta):

- T-007 implementa la version inicial del Builder (ARQ-006 / FN-007): consume el
  `WorkPlan` entregado por T-006 y ejecuta un conjunto acotado y determinista de
  cambios sobre el proyecto objetivo, dentro del flujo del Kernel
  (Planner -> Builder -> Verifier);
- la frontera con T-006 es estricta: T-007 consume `WorkPlan` de solo lectura y
  no amplia ni modifica los modelos congelados de T-006 (`WorkPlan`, `PlanTask`,
  `PlanLimits`);
- la frontera con T-008 (Verificacion inicial) es estricta: T-007 aplica y
  registra cambios; la verificacion de calidad pertenece a T-008;
- la version inicial opera sin LLM (Ollama) y deriva las acciones de forma
  determinista a partir de las tareas del plan por heuristica textual (patron
  D-E3), sin inventar rutas ni contenidos sin base;
- las operaciones soportadas son crear y actualizar archivos bajo la raiz del
  proyecto objetivo (`WorkingDirectory` del plan), con validacion de rutas (sin
  `..` ni absolutas) y sin escrituras fuera del objetivo;
- la escritura se realiza dentro del repositorio objetivo mediante un comando
  publico nuevo `condor construir` (texto y `--json`), en espanol sin tildes;
- el resultado se persiste como artefacto derivado y transitorio en el estado
  local (`build.json`), sin escribir conocimiento permanente en el objetivo ni
  modificar `assessment.json`, `context.json` ni `plan.json`.

Las decisiones D-B1 a D-B5 que definen el alcance son:

- D-B1: el Builder ejecuta acciones archivo-acotadas y deterministas sobre el
  proyecto objetivo, derivadas de las tareas del plan, sin LLM.
- D-B2: la escritura se realiza dentro del repositorio objetivo mediante un CLI
  dedicado (`condor construir`).
- D-B3: las operaciones soportadas en la version inicial son crear y actualizar
  archivos (mas la creacion implicita de directorios); la eliminacion queda fuera
  de alcance.
- D-B4: `build.json` se persiste como artefacto derivado y transitorio en el
  estado local (`%LOCALAPPDATA%\Condor\state\`), sin tocar el objetivo ni otros
  artefactos derivados.
- D-B5: el contenido de los cambios se deriva directamente del plan, sin usar el
  LLM local en esta version, preservando determinismo y operacion local.

Estado:
Aceptada (para formalizacion contractual). Su alcance queda sujeto a la
ratificacion del diseno tecnico (DEC-033) antes de implementar.

Origen:
Reconocimiento y formalizacion de T-007 (Builder inicial).

---

# DEC-033

Titulo:
Diseno tecnico de T-007 (Builder inicial).

Estado:
Propuesta (Pendiente de ratificacion para implementacion).

Decision:
El diseno tecnico de T-007 se consolida en `operacion/TAREAS/T-007.md`
(con D-B1 a D-B5 ratificados). Se incorporan las siguientes resoluciones
tecnicas, pendientes de aprobacion formal:

- D-DB1: `BuildDeriver` reside en `Condor.Core.Building` como logica pura
  (patron D-D5): recibe el `WorkPlan` y `BuildLimits`; no realiza IO.
  `BuildService` (Infrastructure) orquesta la carga y delega la derivacion;
  `ProjectFileWriter` es el unico componente con IO de archivos sobre el
  objetivo.
- D-DB2: `BuildJson` en `Condor.Core.Serialization` sigue el patron de
  `ContextJson`/`AssessmentJson`/`PlanJson` (camelCase, `WriteIndented`, ignorar
  nulls).
- D-DB3: `BuildLimits` centralizado (patron D-D12) con valores de referencia
  propuestos: `MaxActions = 24`, `MaxContentLength = 64_000`,
  `MaxRelativePathLength = 260`, `BuildTimeoutMilliseconds = 15_000`.
- D-DB4: las acciones se derivan por heuristica textual determinista (patron
  D-E3) desde `Title`/`Detail` de cada `PlanTask`, en orden ordinal, con tipos
  `Crear`/`Actualizar` y validacion de rutas; se truncan a `MaxActions`.
- D-DB5: `build.json` se persiste tras cada ejecucion de `condor construir`,
  UTF-8 sin BOM (patrones D-D4 y D-D9), sin modificar otros artefactos derivados.
- D-DB6: la CLI `condor construir` (texto y `--json`) en espanol sin tildes; sin
  plan, `NotDetected` con motivo instructivo y exit code 1.
- D-DB7: determinismo (patron D-E7): mismo plan produce el mismo resultado, con
  la unica excepcion de `GeneratedAtUtc`; se incluye una prueba de doble
  ejecucion.

Estas decisiones se presentan a revision formal. Su ratificacion convierte el
diseno de T-007 en aprobado y habilita la implementacion autorizada.

Origen:
Diseno tecnico de T-007 (Builder inicial).

---

# DEC-034

Titulo:
Formalizacion del contrato de T-008 (Verificacion inicial).

Decision:
El contrato de T-008 queda formalizado en `operacion/TAREAS/T-008.md`
(version 1.0.0):

- T-008 implementa la version inicial del Verifier (ARQ-007 / FN-008): consume
  el `BuildResult` entregado por T-007 y comprueba que los cambios declarados
  como aplicados fueron escritos de forma correcta, completa y acotada sobre el
  proyecto objetivo, dentro del flujo del Kernel (Planner -> Builder -> Verifier);
- la frontera con T-007 es estricta: T-008 consume `BuildResult` de solo lectura
  y no re-aplica ni modifica cambios en el proyecto objetivo;
- la frontera con T-005 es estricta: T-008 consume `ProjectContext` de solo
  lectura como base de referencia (WorkingDirectory/RootName) sin reconstruirlo;
- el alcance v1.0 del Verifier es la verificacion de integridad y acotacion de la
  escritura: archivos declarados como aplicados existen con el contenido
  declarado, las rutas permanecen dentro del WorkingDirectory, y se registran
  acciones aplicadas/omitidas/fallidas conforme al resultado de T-007;
- quedan FUERA de alcance de T-008 v1.0: compilar el proyecto objetivo, ejecutar
  sus pruebas, analizar la calidad semantica del codigo, validar su arquitectura,
  evaluar la coherencia funcional del cambio, re-derivar el `WorkPlan`, re-aplicar
  cambios, re-descubrir el proyecto y reconstruir el contexto; estas capacidades
  quedan reservadas para evoluciones posteriores;
- la capacidad se expone mediante el comando publico `condor verificar` (texto y
  `--json`);
- el resultado se persiste como artefacto derivado y transitorio en el estado
  local (`verification.json`), sin modificar `assessment.json`, `context.json`,
  `plan.json` ni `build.json`.

Las decisiones D-V1 a D-V5 que definen el alcance son:

- D-V1: el Verifier verifica la integridad y acotacion de los cambios aplicados
  por T-007: archivos existentes con contenido declarado dentro del
  `WorkingDirectory`.
- D-V2: la verificacion semantica y de calidad queda fuera del alcance v1.0 y se
  reserva para evoluciones posteriores sin contaminar esta responsabilidad
  inicial.
- D-V3: las acciones omitidas/fallidas declaradas se registran como checks
  informativos y no como fallas del Verifier.
- D-V4: `verification.json` se persiste como artefacto derivado y transitorio en
  el estado local, sin tocar el objetivo ni otros artefactos derivados.
- D-V5: el Verifier opera sin LLM, sin red y sin modificar el proyecto objetivo.

Estado:
Aceptada (contrato aprobado). El alcance queda ratificado expresamente por el
usuario; la implementacion aguarda el diseno tecnico (DEC-035).

Origen:
Reconocimiento y formalizacion de T-008 (Verificacion inicial).

---

# DEC-035

Titulo:
Diseno tecnico de T-008 (Verificacion inicial).

Estado:
Propuesta (Pendiente de ratificacion para implementacion).

Decision:
El diseno tecnico de T-008 se consolida en `operacion/TAREAS/T-008.md`
(con D-V1 a D-V5 ratificados). Se incorporan las siguientes resoluciones
tecnicas, pendientes de aprobacion formal:

- D-DV1: `Verifier` reside en `Condor.Core.Verification` como logica pura
  (patron D-D5): recibe el `BuildResult`, el `ProjectContext` y la informacion
  de archivos leida; no realiza IO. `VerificationService` (Infrastructure)
  orquesta la carga y delega la comparacion; `ProjectFileProbe` es el unico
  componente con IO de lectura sobre el objetivo (no escribe).
- D-DV2: `VerificationJson` en `Condor.Core.Serialization` sigue el patron de
  `BuildJson`/`PlanJson`/`ContextJson` (camelCase, `WriteIndented`, ignorar
  nulls, enum como string).
- D-DV3: `VerificationLimits` centralizado (patron D-D12) con valores de
  referencia propuestos: `MaxChecks = 24`, `MaxContentLength = 64_000`,
  `VerifyTimeoutMilliseconds = 15_000`.
- D-DV4: la verificacion compara cada `BuildAction` de `BuildResult` con el
  estado real del archivo en orden ordinal: aplicadas (existe + contenido),
  omitidas/fallidas (informativas), con validacion de acotacion de ruta.
- D-DV5: `verification.json` se persiste tras cada ejecucion de
  `condor verificar`, UTF-8 sin BOM (patrones D-D4 y D-D9), sin modificar otros
  artefactos derivados.
- D-DV6: la CLI `condor verificar` (texto y `--json`) en espanol sin tildes; sin
  build, `NotDetected` con motivo instructivo y exit code 1.
- D-DV7: determinismo (patron D-E7): mismo `BuildResult` y mismo estado de
  archivos producen el mismo resultado, con la unica excepcion de
  `GeneratedAtUtc`; se incluye una prueba de doble ejecucion.

Estas decisiones se presentan a revision formal. Su ratificacion convierte el
diseno de T-008 en aprobado y habilita la implementacion autorizada.

Origen:
Diseno tecnico de T-008 (Verificacion inicial).

---

# Historial de Cambios

| Version | Cambio |
|---------|--------|
| 3.0.0 | Se incorporan DEC-034 (formalizacion del contrato de T-008, decisiones D-V1 a D-V5) y DEC-035 (diseno tecnico de T-008, D-DV1 a D-DV7, propuesta pendiente de ratificacion). |
| 2.0.0 | Se incorporan DEC-032 (formalizacion del contrato de T-007, decisiones D-B1 a D-B5) y DEC-033 (diseno tecnico de T-007, D-DB1 a D-DB7, propuesta pendiente de ratificacion). |
| 1.8.0 | Se incorpora DEC-030 (formalizacion del contrato de T-006, decisiones D-E1 a D-E8). |
| 1.7.0 | Se incorporan DEC-028 (formalizacion del contrato de T-005) y DEC-029 (diseno tecnico aprobado de T-005, decisiones D-D1 a D-D12). |
| 1.6.0 | Se incorpora DEC-027 (diseno aprobado de T-004, decisiones D-D1 a D-D7). |
| 1.5.0 | Se incorpora DEC-026 (descubrimiento de proyecto, T-004). |
| 1.4.0 | Se incorpora DEC-025 (correccion del contrato CLI al espanol) y se actualizan DEC-013 y DEC-023. |
| 1.3.0 | Se incorporan DEC-019 a DEC-024 correspondientes a las decisiones aprobadas para T-003. |
| 1.2.0 | Se incorporan DEC-013 a DEC-018 correspondientes a las decisiones aprobadas para T-002. |
| 1.1.0 | Se incorporan DEC-007 a DEC-012 correspondientes a las decisiones aprobadas para T-001. |
| 1.0.0 | Primera version. |