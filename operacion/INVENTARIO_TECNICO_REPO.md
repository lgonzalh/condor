# INVENTARIO_TECNICO_REPO

Version: 1.3.0
Estado: Vigente (snapshot tecnico)
Nivel: Global
Clasificacion: Inventario Tecnico
Fecha de corte del snapshot base: 2026-10-19
Fecha de actualizacion: 2026-10-30
Fuente del software probado: `420026b` (2026-10-19). Configuracion Graft: `25af837` (2026-10-30).

## ESTADO POST-CORRECCION (2026-10-19)

Las secciones 6, 9 y 10 conservan el snapshot inicial del diagnostico. Tras el
cierre de correcciones, el estado verificable es:

- Build Release: 0 errores, 0 advertencias.
- Pruebas: 664/664 correctas (Cli 56, Architecture 22, Core 272,
  Infrastructure 314), 0 fallidas, 0 omitidas.
- UTF-8: 0 archivos `.cs`/`.md` con bytes invalidos y 0 coincidencias de
  mojibake; los 20 BOM UTF-8 existentes son validos.
- TUI: pruebas alineadas con la cabecera compacta, cabeza 8x8, barra
  workspace/estado/RAM y build interno `beta`.
- `AgentServiceResourceBlockTests`: la prueba de 0.5B usa stubs de LLM y de
  disponibilidad; ya no depende de un Ollama real.
- Lanzador: `C:\GitHub\condor\condor.cmd` publica en
  `C:\GitHub\condor\.artifacts\condor\condor.exe` cuando cualquier `.cs` o
  `.csproj` de `Src` (excluyendo `obj/` y `bin/`) es mas reciente. El comando
  global `condor` funciona desde cualquier directorio.
- `test-publish/` queda ignorado por Git; no se elimino porque requiere
  autorizacion explicita.

## PROPOSITO

Registrar el inventario estructural real del repositorio Condor y su estado
verificable en la fecha de corte, sin reemplazar los documentos oficiales
`INVENTARIO_PROYECTO.md`, `INVENTARIO_ARQUITECTURA.md` ni `INVENTARIO_FUNCIONAL.md`.

Este documento responde:

- que contiene el repositorio;
- como esta organizado el codigo;
- que se verifico con build y pruebas;
- que brechas existen entre el codigo, las pruebas y la documentacion.

## ACTUALIZACION DEL ARBOL LOCAL (2026-10-30)

El snapshot detallado de codigo, pruebas y estructura de las secciones siguientes corresponde al 2026-10-19. Este delta registra solo hechos locales revisados el 2026-10-30:

- `assets/` contiene los Cuadernos I-V en sus rutas actuales. Los archivos estan presentes localmente, aparecen sin seguimiento en Git y no se movieron ni modificaron.
- `AGENTS.md`, `.claude/`, `.ignore`, `.mcp.json` y `opencode.json` son configuracion de Graft y herramientas compatibles, versionada desde el commit `25af837`.
- `GRAFT_API_KEY` y `OPENAI_API_KEY` no estan configuradas en los ambitos Process, User ni Machine. `graft_repo_map` y `graft check` confirmaron el estado estructural desde Codex. El intento previo de `graft build --deep` se detuvo en 73/334 archivos y no genero la capa semantica ni invoco la API de OpenAI.
- No se ejecutaron pruebas ni build de Condor en esta actualizacion. Los resultados 664/664 y build 0/0 que aparecen abajo siguen siendo el snapshot verificado el 2026-10-19.

## METODO

1. `graft_repo_map` desde Codex para orientacion estructural.
2. `graft check` el 2026-10-30 para verificar frescura del grafo de cableado.
3. La capa semantica de `graft build --deep` no se completo. La CLI requiere una API key propia del proveedor; Codex no comparte su autenticacion con la CLI. No se configuro ni uso una API key de OpenAI.
4. `dotnet build Condor.slnx -c Release` y `dotnet test Condor.slnx -c Release --no-build` corresponden al snapshot verificado el 2026-10-19; no se repitieron en esta actualizacion documental.
5. `git status`, `git log`, `git remote -v`.
6. Verificacion de codificacion UTF-8 sobre `.cs` y `.md`.

No se modifico codigo de produccion para producir este inventario.

## 1. IDENTIDAD DEL REPOSITORIO

| Campo | Valor |
|---|---|
| Producto | Condor |
| Version publica | 1.0 |
| Build interno | beta |
| Version de ensamblado | 1.0.0 |
| Rama | main |
| Commit del software probado | 420026b (2026-10-19) |
| Commit de configuracion Graft | 25af837 (2026-10-30) |
| Remoto | https://github.com/lgonzalh/condor.git |
| Solucion | Condor.slnx (7 proyectos) |
| Runtime | .NET 10 (net10.0) |
| Proveedor LLM de Condor | Ollama local en 127.0.0.1:11434 |
| Proveedor semantico de Graft | OpenAI API, pendiente de `GRAFT_API_KEY` |

## 2. ESTRUCTURA DEL REPOSITORIO

```text
condor/
├── Src/
│   ├── Condor.Cli/            47 archivos .cs   5.378 lineas
│   ├── Condor.Core/          142 archivos .cs   6.233 lineas
│   └── Condor.Infrastructure/ 52 archivos .cs   6.402 lineas
├── Tests/
│   ├── Unit/Condor.Cli.Tests/                  3 archivos .cs     645 lineas
│   ├── Unit/Condor.Core.Tests/                26 archivos .cs   3.944 lineas
│   ├── Integration/Condor.Infrastructure.Tests/50 archivos .cs  5.917 lineas
│   ├── Architecture/Condor.Architecture.Tests/ 13 archivos .cs     453 lineas
│   └── Functional/condor-lifecycle.func.ps1    (fuera de la solucion)
├── Docs/                      133 archivos .md en 10 niveles (00 a 09)
├── operacion/                 23 archivos .md (22 previos + este inventario) + TAREAS/T-001..T-016
├── assets/                    5 cuadernos fuente locales (sin seguimiento en Git)
├── build/                     vacio
├── tools/                     vacio
├── graft/                     grafo local, ignorado por git
├── graft-deep.cmd              lanzador local, clave temporal no persistida
├── Condor.slnx
├── README.md
├── ESTADO_PROYECTO.md
├── INVENTARIO_PROYECTO.md
├── AGENTE_CONDOR.md
└── AGENTS.md                  generado por graft init
```

Totales de codigo:

| Conjunto | Archivos .cs | Lineas |
|---|---:|---:|
| Produccion | 241 | 18.013 |
| Pruebas | 92 | 10.959 |
| Total C# | 333 | 28.972 |
| Mockup Python | 1 | 133 |

## 3. ARQUITECTURA POR CAPAS

Direccion de dependencias:

```text
Condor.Cli ───────────────> Condor.Core
      │                          ▲
      └────> Condor.Infrastructure ┘
```

- `Condor.Core`: dominio puro, contratos, modelos, evaluacion, seleccion,
  planificacion, contexto, verificacion, vision y serializacion. No depende de
  Infrastructure ni de la CLI.
- `Condor.Infrastructure`: adaptadores de sistema operativo, Ollama, deteccion,
  exploracion de proyectos, estado local, agentes, build, contexto, ciclo,
  verificacion semantica y vision.
- `Condor.Cli`: punto de entrada, routing, comandos, presentacion, TUI y version.

### 3.1 Condor.Core

| Area | Contenido principal |
|---|---|
| Agent | `AgentEngine`, `AgentActionParser` |
| Building | `BuildDeriver` |
| Catalog | `ModelCatalog` |
| Context | `ContextReconstructor`, `ContextLimits`, `OperativeArtifactCatalog` |
| Contracts | 17 interfaces: agente, assessment, build, contexto, ciclo, LLM, modelo, setup, estado, verificacion, vision |
| Cycle | `CycleEngine` |
| Evaluation | `BudgetPolicy`, `BudgetReevaluator`, `ModelMemoryBudget`, `ModelEfficiencyEvaluator`, `TaskIntentClassifier`, `LlmModelSelector` |
| Models | 71 DTOs y modelos de dominio |
| Planning | `PlanGenerator`, `PlanIntent` |
| Project | `ProjectClassifier`, `ProjectProfileBuilder`, parsers de manifiestos (Cargo, csproj, Go, package.json, pom.xml, pyproject, requirements, tsconfig) |
| Selection | `ModelSelector` |
| Semantic | `SemanticVerifier` |
| Serialization | Serializadores JSON por dominio |
| Setup | `SetupEvaluator` |
| Verification | `Verifier` |
| Vision | `VisionGate` |

### 3.2 Condor.Infrastructure

| Area | Contenido principal |
|---|---|
| Agent | `AgentService`, `AgentToolset`, `AgentHarness`, `ModelPromptBuilder`, `RepoSnapshot` |
| Building | `BuildService`, `ProjectFileWriter` |
| Context | `ContextService`, `OperativeArtifactReader` |
| Cycle | `CycleService` |
| DependencyBootstrap | `DependencyBootstrapper`, `OllamaHealthChecker`, `OllamaProvisioner`, `OllamaAutoInstaller`, `OllamaServerLauncher` |
| Detection | CPU, GPU, RAM, almacenamiento, OS, Git, Ollama, herramientas y consumidores de RAM |
| Llm | `OllamaClient`, `LocalModelSession`, `OllamaModelOperator` |
| Planning | `PlanService` |
| Probing | `CimProbe`, `ProcessProbe` |
| Project | `DirectoryScanner`, `GitRepositoryProbe`, `ManifestFileReader`, `ProjectDetector` |
| Retry | `RetryPolicy` |
| SemanticVerification | `SemanticVerificationService`, `DotNetManifestProbe`, `ProcessRunner`, `ToolProbe` |
| Setup | `ModelAutoSetupService`, `SetupService`, `StateDirectoryProbe` |
| State | `LocalStateStore`, `ModelKardex` |
| Verification | `VerificationService`, `ProjectFileProbe` |
| Vision | `VisionService`, `ImageFileReader` |

### 3.3 Condor.Cli

| Area | Contenido principal |
|---|---|
| Entrada | `Program.Main` |
| Commands | 12 comandos: Advance, Agent, Ask, Assess, Build, Check, Context, Examine, Plan, Prepare, Recommend, Verify |
| Presentation | 23 renderers y presentadores; `Terminal`, `TuiScreen`, `IdentityHeader`, `CliStatusBar` |
| Routing | `IntentionRoute`, `Interpreter`, `StartupPreparer` |
| Tui | `CondorTui`, `TuiHost`, `TuiInput`, `TuiViews`, `Ansi`, `CondorArt` |
| Version | `VersionInfo` (fuente unica de version) |

## 4. FLUJOS PRINCIPALES

### 4.1 Arranque

`Program.Main` (`Src/Condor.Cli/Program.cs:L24-L147`):

1. Fija `Console.OutputEncoding = UTF8`.
2. Crea una unica `LocalModelSession` y un token cooperativo de apagado.
3. Resuelve `--version`, `--help`, modo interactivo o one-shot.
4. Sin argumentos: intenta TUI (`CondorTui.CanRun`) y, si no es posible,
   usa la CLI interactiva clasica.
5. En `finally` libera el modelo con `keep_alive=0`.

### 4.2 Bootstrap de Ollama

`DependencyBootstrapper.RunAsync`
(`Src/Condor.Infrastructure/DependencyBootstrap/DependencyBootstrap.cs:L39-L71`):

- detecta instalacion y estado real del servidor por `/api/version`;
- instala o inicia Ollama solo si es necesario;
- registra ownership (`StartedByCondor`) para no cerrar un servidor ajeno;
- entrega el assessment al flujo de preparacion.

### 4.3 Preparacion y seleccion

`StartupPreparer.RunAsync`
(`Src/Condor.Cli/Routing/StartupPreparer.cs:L49-L147`):

- calcula RAM/presupuesto;
- evalua modelos instalados y catalogo;
- si hay modelos instalados pero la RAM no alcanza, la sesion arranca con
  advertencia honesta y la decision se toma por tarea;
- si no hay ningun modelo instalado ni obtenible, no entra en sesion.

`ModelAutoSetupService.EnsureModelForRequirementAsync`
(`Src/Condor.Infrastructure/Setup/ModelAutoSetupService.cs:L152-L238`)
selecciona por requisito de tarea y presupuesto.

### 4.4 Agente

`AgentService.RunAsync`
(`Src/Condor.Infrastructure/Agent/AgentService.cs:L58-L530`):

1. clasifica la intencion (`TaskIntentClassifier`);
2. si es consulta conversacional, usa `RunDirectConsultAsync`
   (`AgentService.cs:L532-L689`);
3. si es tarea de ingenieria, selecciona modelo, prepara prompt, ejecuta
   herramientas reales, verifica y entrega resultado con checkpoint e inventario.

`AgentToolset` (`Src/Condor.Infrastructure/Agent/AgentToolset.cs:L14-L366`)
ejecuta acciones de lectura, escritura, edicion, busqueda, git, build y test.

### 4.5 Presupuesto y modelos

- `BudgetPolicy.Assess` (`Src/Condor.Core/Evaluation/BudgetPolicy.cs:L88-L113`):
  `presupuesto = RAM libre - reserva sistema - reserva Condor - reserva operativa - margen`.
- `ModelMemoryBudget.FitsInRamStrict`
  (`Src/Condor.Core/Evaluation/ModelMemoryBudget.cs:L62-L74`): condicion estricta
  de porcentaje de RAM total y presupuesto seguro.
- `ModelSelector.SelectForTask`
  (`Src/Condor.Core/Selection/ModelSelector.cs:L271-L466`): seleccion por tarea,
  1-/1+, modelo instalado del usuario y minimo viable.
- `ModelSelector.RecommendFromCatalog`
  (`ModelSelector.cs:L12-L133`): seleccion clasica usada en arranque.
- `LocalModelSession` (`Src/Condor.Infrastructure/Llm/LocalModelSession.cs:L24-L158`):
  un solo `HttpClient`, modelo activo y liberacion idempotente.

### 4.6 TUI

- `CondorTui.RunAsync` (`Src/Condor.Cli/Tui/CondorTui.cs:L65-L368`).
- `TuiHost` (`Src/Condor.Cli/Tui/TuiHost.cs:L58-L961`): render, mascota,
  actividad, entrada, estado y geometria.
- `TuiScreen`: autoridad unica de renderizado en esperas.
- Referencia visual: mascota V16, ANSI, geometria 120x30.

### 4.7 Contexto, proyecto y verificacion

- `ContextReconstructor.Reconstruct`
  (`Src/Condor.Core/Context/ContextReconstructor.cs:L15-L63`).
- `ProjectClassifier.Classify`
  (`Src/Condor.Core/Project/ProjectClassifier.cs:L15-L28`).
- `Verifier.Verify` (`Src/Condor.Core/Verification/Verifier.cs:L19-L122`).
- `SemanticVerificationService.VerifySemanticAsync`
  (`Src/Condor.Infrastructure/SemanticVerification/SemanticVerificationService.cs:L36-L114`).
- `VisionGate.Evaluate` (`Src/Condor.Core/Vision/VisionGate.cs:L22-L46`).

## 5. COMANDOS DE LA CLI

| Comando | Funcion |
|---|---|
| `condor` | Prepara entorno y abre sesion interactiva/TUI |
| `condor <intencion>` | Ejecuta una intencion one-shot con el agente |
| `/analizar` | Analiza el proyecto o directorio actual |
| `/contexto` | Reconstruye el contexto del proyecto |
| `/planear "<solicitud>"` | Genera un plan de trabajo |
| `/construir` | Aplica cambios del plan |
| `/verificar` | Comprueba cambios aplicados |
| `/avanzar "<solicitud>"` | Ejecuta el ciclo de ingenieria parcial |
| `/examinar "<imagen>"` | Analiza una imagen localmente |
| `/recomendar "<tipo>"` | Recomienda un modelo para el equipo |
| `/consultar "<mensaje>"` | Consulta al modelo local |
| `/verificar-semantico` | Compila y ejecuta pruebas del proyecto |
| `/preparar` | Refresca la preparacion del entorno |
| `/ayuda`, `/version`, `/salir` | Ayuda, version y salida |

## 6. INVENTARIO DE PRUEBAS

| Proyecto | Archivos .cs | Lineas | Pruebas descubiertas | Resultado |
|---|---:|---:|---:|---|
| Condor.Architecture.Tests | 13 | 453 | 22 | 22 OK |
| Condor.Cli.Tests | 3 | 645 | 56 | 56 OK |
| Condor.Core.Tests | 26 | 3.944 | 272 | 272 OK |
| Condor.Infrastructure.Tests | 50 | 5.917 | 314 | 314 OK |
| Total | 92 | 10.959 | 664 | 664 OK, 0 fallas |

Comando ejecutado:

```text
dotnet test Condor.slnx -c Release --no-build
```

Duracion observada: 3 min 42 s (Infrastructure es la suite lenta).

`Tests/Functional/condor-lifecycle.func.ps1` existe, pero no forma parte de la
solucion ni se ejecuto en esta verificacion.

## 7. DOCUMENTACION Y OPERACION

- `Docs/`: 133 archivos `.md` distribuidos en 10 niveles (00_Fundamentos a
  09_Evolucion), mas mockups en `Docs/07_Interfaz/Mockups/`.
- `operacion/`: 23 archivos `.md` (22 previos mas este inventario).
  - `TAREAS/` contiene T-001 a T-016.
  - `ESTADO_DESARROLLO.md` conserva el cierre historico de T-018 (625 pruebas)
    y agrega la actualizacion 2026-10-19 con 664/664 y build 0w/0e.
- `KANBAN.md`, `BACKLOG.md`, `RELEVO.md` y `ESTADO_PROYECTO.md` se alinean en
  esta actualizacion con el corte de software 2026-10-19 y separan el pendiente
  de Graft de la operacion de Condor.
- `Docs/07_Interfaz/` contiene T-018, T-019 y T-020. No se localizo una ficha
  T-017 ni se crearon duplicados en `operacion/TAREAS/`; el hueco es de
  trazabilidad documental, no evidencia por si mismo de una tarea activa.
- `Docs/02_Memoria/INVENTARIO_ARQUITECTURA.md` aclara que ARQ-012 sigue En
  desarrollo como superficie, mientras Architect, Guardian y ciclo de vida de
  artefactos permanecen en estados planificados/especificados.

## 8. GRAFT

| Metrica | Valor |
|---|---|
| Archivos indexados | 334 (333 C# + 1 Python) |
| Simbolos | 2.754 |
| Aristas de cableado | 2.079 |
| Tarjetas Markdown | 334 |
| Nodos semanticos pendientes | 3.088 |
| Tier semantico | 0% |
| Estado del cableado | En sincronia con el codigo (2026-10-30) |

`graft check` confirma que el grafo estructural esta actualizado, pero tambien indica `deep layer: not built`. Los 3.088 nodos siguen pendientes de conceptos y resumenes. El intento local anterior se detuvo a los 73/334 archivos; no completo la capa semantica ni invoco la API directa de OpenAI. La revision y los cambios documentales de este informe se hicieron con Codex.

La CLI de Graft necesita una configuracion de proveedor independiente, incluida `GRAFT_API_KEY`; la autenticacion de Codex no se transfiere automaticamente al proceso CLI. Para completar la capa con OpenAI, el usuario debe crear y configurar localmente su API key de plataforma. El uso de la API se factura aparte por consumo; esta revision no utilizo la API.

## 9. ESTADO VERIFICADO

### 9.1 Build

```text
dotnet build Condor.slnx -c Release
```

- Resultado: compilacion correcta.
- Errores: 0.
- Advertencias: 0.

Las 5 advertencias nullable del snapshot inicial (3 CS8602 en
`ModelSelector.cs` y 2 CS8601 en `AgentService.cs`) quedaron corregidas con
guardas minimas y no cambian el comportamiento.

### 9.2 Pruebas

- Total: 664.
- Correctas: 664.
- Fallidas: 0.
- Omitidas: 0.

Las 13 fallas de CLI eran expectativas obsoletas de
`Tests/Unit/Condor.Cli.Tests/TuiTests.cs`; se alinearon con el contrato vigente
(`beta`, cabecera compacta, cabeza 8x8, bienvenida `Iniciando ...`, barra
workspace/estado/RAM).

La falla de Infrastructure se elimino desacoplando la prueba de 0.5B de Ollama:
se inyectan `ILlmClient`, `ILlmProviderDiagnostics` y un `HttpMessageHandler`
stub.

### 9.3 Git

- Rama: `main`.
- El snapshot de software y pruebas corresponde a `420026b` (2026-10-19).
- La configuracion Graft se versiono en `25af837` (2026-10-30); no modifica codigo de Condor.
- `graft-deep.cmd` vive en la raiz, solicita la clave con entrada oculta, ejecuta `graft build --deep` y `graft check`, y elimina `GRAFT_API_KEY` del proceso al finalizar.
- `assets/` permanece local y sin seguimiento; los Cuadernos I-V no se editaron.
- Los cambios de esta actualizacion son documentales y de configuracion Graft; no se ejecutaron pruebas ni build de Condor.

### 9.4 Codificacion UTF-8 y anti-Mojibake

Se revisaron 488 archivos `.cs` y `.md` existentes antes de crear este inventario.
**Estado final:** 0 archivos con bytes UTF-8 invalidos y 0 coincidencias de
mojibake en `.cs`/`.md`. Los hallazgos siguientes son el snapshot inicial y
quedaron corregidos.

| Archivo | Hallazgo inicial |
|---|---|
| `Src/Condor.Cli/Presentation/IdentityHeader.cs` | UTF-8 invalido: 21 bytes Windows-1252 (`0xA9`, `0xB7`, `0xF3`, `0xF1`) |
| `Src/Condor.Infrastructure/Agent/AgentService.cs` | 10 ocurrencias de mojibake en cadenas de runtime (separador U+00C2 U+00B7 en lugar de `·`; U+00C3 U+00B1 en "español"; U+00E2 U+20AC U+2026 en lugar de `…`) |
| `Src/Condor.Core/Selection/ModelSelector.cs` | 282 ocurrencias de mojibake en comentarios (secuencias U+00C3/U+0192 en lugar de `1-` y "tamaño") |
| `Src/Condor.Cli/Tui/CondorArt.cs` | 2 comentarios con caracteres deteriorados |
| `operacion/REGISTRO_CAMBIOS.md` | 3 ocurrencias |
| `operacion/TAREAS/T-008.md` | 1 ocurrencia |
| `Docs/07_Interfaz/MASCOTA_CLI_UNICODE.md` | 2 ocurrencias |
| `Tests/Integration/Condor.Infrastructure.Tests/DependencyBootstrapTests.cs` | 1 ocurrencia |

Total inicial: 301 coincidencias de mojibake en 7 archivos, mas 21 bytes
invalidos en `IdentityHeader.cs`. Todos fueron corregidos. Los 20 archivos con
BOM UTF-8 se conservan; el BOM es valido y no es mojibake.

## 10. HALLAZGOS DEL SNAPSHOT INICIAL Y ESTADO ACTUAL

> **Cierre 2026-10-19:** los P0/P1/P2 de esta tabla quedaron corregidos,
> mitigados o registrados como deuda documental. La capa semantica de Graft
> sigue pendiente de configurar un proveedor y una API key propios para la CLI de Graft (P2); no afecta el build ni las pruebas.
> T-018..T-020 tienen documentos en `Docs/07_Interfaz/`; no se localizo T-017.
> Este hueco de trazabilidad no se convierte en una tarea de producto sin una
> fuente que la identifique.
> `README.md` ya no es un hallazgo bloqueante: se agrego la seccion de ejecucion
> global.

| Prioridad | Hallazgo | Evidencia | Impacto | Accion minima propuesta |
|---|---|---|---|---|
| P0 | Suite de pruebas en rojo | 14 fallas de 663 | No se puede declarar cierre verde | Actualizar pruebas TUI al contrato vigente o revertir la TUI; aislar la prueba que exige 0.5B |
| P0 | Mojibake en cadenas de runtime | `AgentService.cs` lineas 230, 247, 266, 648, 667, 1077, 1158, 1206, 1247 | Mensajes al usuario con separadores y "español" doblemente codificados | Reemplazar por `·`, `español`, `…` sin cambiar logica |
| P0 | UTF-8 invalido en fuente | `IdentityHeader.cs` | Texto deteriorado en CLI clasica | Recodificar el archivo a UTF-8 sin cambiar contenido |
| P1 | Documentacion operativa desalineada | `BACKLOG.md`, `RELEVO.md`, `KANBAN.md`, `ESTADO_PROYECTO.md` | Continuidad confusa | Sincronizar estado y fechas; no borrar historia |
| P2 | T-017 no localizada; T-018..T-020 viven en `Docs/07_Interfaz/` | `operacion/TAREAS/` llega a T-016 | Hueco de trazabilidad, no defecto de producto | Mantener las rutas; crear T-017 solo si existe una fuente para esa tarea |
| P1 | Advertencias nullable | 3 en ModelSelector, 2 en AgentService | Ruido de compilacion | Guardas minimas de nulabilidad |
| P2 | `test-publish/` conservado localmente | Esta ignorado por Git y no se versiona | Artefacto residual; su eliminacion requiere autorizacion | Conservarlo hasta autorizacion expresa |
| P2 | Capa semantica de graft incompleta | 3.088 nodos pendientes (0%) | Sin resumenes conceptuales | Configurar una API key OpenAI local en `GRAFT_API_KEY` y ejecutar `graft build --deep` |
| P3 | README menciona Samples/Scripts | Directorios inexistentes | Deriva documental menor | Corregir README o crear directorios si se requieren |

## 11. CRITERIO DE CIERRE PARA EL SIGUIENTE CICLO

1. Build sin errores y sin advertencias nuevas.
2. Suite completa verde o fallas explicitamente justificadas por entorno.
3. Cero mojibake en cadenas de runtime y en fuentes.
4. Grafo de graft en sincronia; capa semantica generada o ausencia declarada.
5. Documentos operativos alineados con HEAD.
6. Arbol de trabajo sin artefactos compilados no rastreados.

Estado del software al corte 2026-10-19: build, pruebas y codificacion cubiertos.
La documentacion operativa se alineo el 2026-10-30. El grafo de cableado sigue
sincronizado; la capa semantica permanece en 0% y la CLI no tiene una API key configurada. El
artefacto `test-publish/` ya esta ignorado, pero se conserva hasta recibir
autorizacion para eliminarlo.

## 12. TRAZABILIDAD

- `graft/INDEX.md`
- `graft/.graph/wiring.json`
- `Src/Condor.Cli/Program.cs`
- `Src/Condor.Cli/Routing/StartupPreparer.cs`
- `Src/Condor.Infrastructure/Agent/AgentService.cs`
- `Src/Condor.Infrastructure/DependencyBootstrap/DependencyBootstrap.cs`
- `Src/Condor.Infrastructure/Setup/ModelAutoSetupService.cs`
- `Src/Condor.Core/Evaluation/BudgetPolicy.cs`
- `Src/Condor.Core/Selection/ModelSelector.cs`
- `Src/Condor.Infrastructure/Llm/LocalModelSession.cs`
- `operacion/ESTADO_DESARROLLO.md`
- `operacion/KANBAN.md`
- `operacion/BACKLOG.md`
- `operacion/RELEVO.md`

## ACTUALIZACIONES

| Version | Fecha | Cambio |
|---|---|---|
| 1.3.0 | 2026-10-30 | Se registra el lanzador seguro de Graft en la raiz y el flujo de uso temporal de la clave. |
| 1.2.0 | 2026-10-30 | Se alinea Git con los commits vigentes, se documenta Graft pendiente de API y se distingue la evidencia de software del 19-10 de los cambios documentales actuales. |
