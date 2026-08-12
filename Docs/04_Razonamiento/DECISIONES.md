# DECISIONES

Version: 1.7.0
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

# Historial de Cambios

| Version | Cambio |
|---------|--------|
| 1.7.0 | Se incorporan DEC-028 (formalizacion del contrato de T-005) y DEC-029 (diseno tecnico aprobado de T-005, decisiones D-D1 a D-D12). |
| 1.6.0 | Se incorpora DEC-027 (diseno aprobado de T-004, decisiones D-D1 a D-D7). |
| 1.5.0 | Se incorpora DEC-026 (descubrimiento de proyecto, T-004). |
| 1.4.0 | Se incorpora DEC-025 (correccion del contrato CLI al espanol) y se actualizan DEC-013 y DEC-023. |
| 1.3.0 | Se incorporan DEC-019 a DEC-024 correspondientes a las decisiones aprobadas para T-003. |
| 1.2.0 | Se incorporan DEC-013 a DEC-018 correspondientes a las decisiones aprobadas para T-002. |
| 1.1.0 | Se incorporan DEC-007 a DEC-012 correspondientes a las decisiones aprobadas para T-001. |
| 1.0.0 | Primera version. |