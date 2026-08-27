# Auditoría de la experiencia TUI/CLI de Cóndor

> Fuente de verdad: la implementación real del repositorio (inspección de código),
> no documentación externa ni mockups. Estado capturado el 2026-08-27, tras las
> correcciones de UX/UI previas (barra de estado persistente, Ave V16 a la derecha,
> orden de identidad y directorio visible en CLI).

## 1. Auditoría breve del estado actual

Cóndor no expone una nomenclatura rígida "TUI1/TUI2/TUI3" ni "CLI1/CLI2". En la
práctica existen **tres modos de ejecución reales** más un modo trivial, todos
despachados desde `Program.Main` (`Src/Condor.Cli/Program.cs`):

- **I1 — TUI persistente** (interactiva, sin argumentos, terminal con soporte VT
  y tamaño ≥ 80×24).
- **I2 — CLI clásica interactiva** (sin argumentos, sin VT o sin tamaño
  suficiente / E/S redirigida en entrada).
- **I3 — CLI one-shot** (con argumentos: intención libre o comando `/`).
- **I4 — Comandos triviales** (`--version`, `--help`).

La diferencia central entre I1 e I2 es la **autoridad de dibujo**: I1 usa una
pantalla alterna (alternate buffer) con repintado por regiones (`TuiHost`);
I2/I3 usan el flujo normal de consola con una línea de estado reescrita en
sitio (`TuiScreen`). Ambos mecanismos coexisten y cada interfaz de progreso
(`IStartupProgressView`, `IAgentProgressView`) tiene **dos implementaciones**
(una por modo).

El directorio de trabajo es visible en ambos modos (I1 en la barra de estado
inferior; I2 en la cabecera superior y en el mensaje de bienvenida).

## 2. Inventario real de interfaces

### I1 — TUI persistente
- **Entrada:** `Program.Main` → `args.Length == 0` y `Tui.CondorTui.CanRun(...)`
  → `Tui.CondorTui.RunAsync(...)` (`Src/Condor.Cli/Tui/CondorTui.cs`).
- **Flujo:** `TuiHost.Enter()` (pantalla alterna) → `ShowWelcome()` → preparación
  real (bootstrap + selección de modelo) con feedback en la propia TUI →
  `ShowSession(model)` → bucle de teclado + repintado por regiones. Entrada con
  editor en línea (`TuiInput`/`RenderInput`). Doble `Esc` interrumpe tareas.
- **Progreso:** `TuiStartupView(host)` y `TuiAgentProgressView(host)`
  (`Src/Condor.Cli/Tui/TuiViews.cs`) pintan directo en `TuiHost`.

### I2 — CLI clásica interactiva
- **Entrada:** `Program.Main` → `args.Length == 0` y `!CanRun()` →
  `RunInteractiveAsync(...)` (`Program.cs`) → `Interpreter` (bucle)
  (`Src/Condor.Cli/Routing/Interpreter.cs`).
- **Flujo:** `StartupProgressPresenter` (vía `TuiScreen.Shared`) durante la
  preparación → `RenderWelcome(prep)` → `Interpreter.RunAsync`: por cada prompt
  invoca `onBeforePrompt` (redibuja `IdentityHeader`) y escribe `"> "`, lee una
  línea, enruta con `IntentionRouter.Route` a comando `/` (`HandleSlashAsync`) o
  intención libre (`AgentCommand.ExecuteAsync`).
- **Progreso:** `StartupProgressPresenter` / `AgentProgressPresenter`
  (`Src/Condor.Cli/Presentation/*Presenter.cs`) → `TuiScreen.Shared`
  (línea de estado en sitio en el flujo normal de consola).

### I3 — CLI one-shot
- **Entrada:** `Program.Main` → `args.Length > 0` → `IntentionRouter.Route`
  → `SlashRoute` (`HandleSlashAsync`) o `FreeIntentionRoute` (`AgentCommand`).
- **Flujo:** bootstrap + preparación + ejecución, salida mínima vía `Terminal`
  y los `*Renderer` (`Src/Condor.Cli/Presentation/`). Sin chrome persistente:
  la salida es directa y efímera.

### I4 — Comandos triviales
- `--version` / `--help` → salida inmediata vía `Terminal` / `RenderHelp()`.

## 3. Mapa de zonas por interfaz

Leyenda: ✅ presente · ⚠️ presente pero efímero/mezclado · ❌ ausente.

| Elemento                      | I1 (TUI)                                 | I2 (CLI clásica)                         | I3 (one-shot) |
|-------------------------------|------------------------------------------|-------------------------------------------|---------------|
| Identidad de Cóndor           | Fila 1 (título + Ave V16 a la derecha)   | `IdentityHeader.Render` (arriba)         | `Terminal`/render efímero |
| Directorio de trabajo         | Barra de estado (última fila, `> ruta`)  | `IdentityHeader.Render(..., dir)` + `RenderWelcome` ("Directorio de trabajo: …") | no persistente (solo si el renderer lo incluye) |
| Modelo activo                 | Cabecera (fila 1) + barra de estado (`* modelo`) | Pie `©Condor - <modelo> - <tiempo>`    | implícito en salida |
| Conversación / actividad      | Zona "Actividad del agente" (scroll)     | Salida de consola (scroll natural)       | salida directa |
| Actividad / proceso del agente| Misma zona de actividad (prefijos ◆ ● ▲ ✗) | `TuiScreen` (línea de estado en sitio)   | `TuiScreen` durante la tarea |
| Respuestas de Cóndor          | Zona de actividad, `ActivityKind.Condor` (◆) | `Terminal`/`*Renderer`                  | `*Renderer` |
| Respuestas del modelo (crudo) | ❌ no separado de "respuesta de Cóndor"  | ❌                                        | ❌ |
| Entrada del usuario           | Editor en línea (fila de entrada, `> `)  | `"> "` en `Interpreter` + `IdentityHeader` arriba | stdin una línea |
| Errores / estado              | `ActivityKind.Error` (✗) + barra de estado + línea de progreso | `Terminal.WriteError` + `TuiScreen`    | `Terminal.WriteError` |
| Código / editor embebido      | ❌ (el agente usa herramientas de archivo) | ❌                                        | ❌ |
| Barra de estado persistente   | ✅ última fila: `> dir | * modelo | estado | versión` | ⚠️ cabecera + pie, se desplazan (no barra fija) | ❌ |

## 4. Problemas / inconsistencias encontrados

1. **Doble autoridad de dibujo.** `TuiHost` (pantalla alterna, regiones) e
   `TuiScreen` (flujo normal, línea en sitio) son dos mecanismos paralelos que
   ambos se autodefinen como "la única autoridad del dibujo". Cada interfaz de
   progreso tiene dos implementaciones (`TuiViews` para TUI, `Presenters` para
   CLI). Riesgo de divergencia y duplicación conceptual.
2. **Zonas mezcladas en la TUI.** Conversación, respuestas de Cóndor y
   actividad/proceso del agente conviven en una sola región de scroll
   ("Actividad del agente") con diferenciación solo por prefijo/color. No hay
   separación explícita entre "respuesta" y "proceso".
3. **Respuestas del modelo no distinguidas.** El texto crudo del modelo no se
   expone por separado de la respuesta de Cóndor; el resultado del agente se
   muestra como `ActivityKind.Condor`.
4. **Barra de estado no uniforme.** I1 tiene barra inferior persistente; I2/I3
   no (solo cabecera/pie que se desplazan). La experiencia no es coherente
   entre modos.
5. **Identidad duplicada.** TUI (`PaintTitleRowLocked`) y CLI
   (`IdentityHeader`) renderizan la identidad en código distinto; cualquier
   cambio de marca debe hacerse en dos sitios.
6. **Higiene del repositorio.** Existen artefactos sueltos en la raíz
   (`err_cli.txt`, `input.txt`, `out_cli.txt`, `validacion_tui.out.txt`,
   `validacion_tui.ps1`, `=0`) y carpetas `Release/`, `publish/` sin seguimiento
   claro; deberían ignorarse en `.gitignore`.
7. **Sin zona de editor/código.** Ninguna interfaz muestra un editor o visor de
   diffs estructurado; el agente opera sobre archivos con herramientas, y la
   salida de cambios queda en la zona de actividad/conversación.

## 5. Propuesta de estructura futura

Mantener los dos modos reales (TUI persistente e CLI clásica) pero **unificar la
autoridad de dibujo** tras una abstracción `ISuperficieUi` con dos
implementaciones (`SuperficieAlt` = `TuiHost`, `SuperficieStream` = `TuiScreen`).
Los presentadores de progreso targetearían `ISuperficieUi`, dejando una sola
implementación por interfaz de progreso.

Zonas que deben distinguirse claramente en ambos modos:
- **Cabecera / identidad:** `CONDOR` + versión + lema + modelo + directorio.
- **Estado de sesión / modelo / recursos:** visible y persistente.
- **Conversación / actividad:** scroll con prefijos; separar visualmente
  "respuesta de Cóndor" de "actividad/proceso del agente" (mantener el Ave V16
  como mascota de sesión, sin migrarlo).
- **Entrada del usuario:** línea de prompt claramente delimitada.
- **Barra de estado inferior persistente:** directorio de trabajo, modelo
  activo, estado de ejecución y versión. Debe existir en I1 e I2 (y, si aplica,
  I3).

Principios: no introducir funcionalidades nuevas; conservar el Ave V16 y la
identidad actual; el directorio de trabajo debe permanecer visible durante toda
la sesión; las responsabilidades visuales deben ser evidentes sin bloque de
terminal continuo.

## 6. Archivos a modificar en la siguiente tarea

- `Src/Condor.Cli/Tui/TuiHost.cs` — separación de zonas dentro de la región de
  actividad (respuesta vs proceso) y pulido de la barra de estado.
- `Src/Condor.Cli/Presentation/IdentityHeader.cs` — coherencia de cabecera/pie
  con la TUI (misma fuente de identidad).
- `Src/Condor.Cli/Presentation/TuiScreen.cs` — clarificar rol y alinear con la
  barra de estado de la TUI.
- `Src/Condor.Cli/Presentation/StartupProgressPresenter.cs`,
  `AgentProgressPresenter.cs`, `Src/Condor.Cli/Tui/TuiViews.cs` — consolidar
  tras `ISuperficieUi` para eliminar la doble implementación de progreso.
- `Src/Condor.Cli/Routing/Interpreter.cs` — conectar una barra de estado
  persistente en la CLI clásica.
- `Src/Condor.Cli/Program.cs` — asegurar que el directorio de trabajo se pasa
  de forma consistente a todas las superficies.
- `.gitignore` — excluir artefactos de validación (`err_cli.txt`, `out_cli.txt`,
  `validacion_tui.*`, `=0`, `Release/`, `publish/`).
