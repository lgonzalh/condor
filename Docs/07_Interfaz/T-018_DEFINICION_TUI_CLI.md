# T-018 — Definición formal de TUI/CLI de Cóndor

> **Fuente de verdad: el código del repositorio.** Inspección directa de `Src/Condor.Cli` (agosto 2026). Los mockups/prototipos son referencia visual secundaria **nunca** arquitectónica. No migra ni rediseña la mascota pequeña. No modifica Core, Infrastructure, presupuesto, selección de modelos ni lógica del agente.

## 1. Interfaces reales existentes (no hay "TUI1/TUI2/CLI1/CLI2")

**Cuatro modos reales**, despachados desde `Program.cs::Main`:

| ID  | Nombre | Entrada (`Program.cs`) | Persistencia de dibujo | Surface de progreso |
|-----|--------|------------------------|------------------------|---------------------|
| I1  | TUI persistente | `args.Length==0` y `CondorTui.CanRun(out w,h)` → `CondorTui.RunAsync` | `TuiHost` (buffer alternativo; regiones) | `TuiStartupView` / `TuiAgentProgressView` (pintan sobre `TuiHost`) |
| I2  | CLI clásica interactiva | `args.Length==0` y **no** `CanRun` → `RunInteractiveAsync` → `Interpreter` | `Terminal` stream + `TuiScreen.Shared` (línea en sitio) | `StartupProgressPresenter` / `AgentProgressPresenter` → `TuiScreen.Shared` |
| I3  | CLI one-shot | `args.Length>0` → `IntentionRouter.Route` → `/slash` o free intent | `Terminal` (salida efímera) | `AgentProgressPresenter` → `TuiScreen.Shared` (solo tareas free) |
| I4  | Trivial | `--version` / `--help` | `Terminal` inmediata | — |

**Hallazgo clave — armonía vs duplicado:**
- El **resultado final** (`AgentRenderer`) es **único**: `RenderResult` en CLI, `BuildResultText` en TUI. La "respuesta de Condor" está bien unificada.
- La **presentación de progreso** está **duplicada** por modo: `IStartupProgressView` → `TuiStartupView` (TUI) y `StartupProgressPresenter` (CLI); `IAgentProgressView` → `TuiAgentProgressView` (TUI) y `AgentProgressPresenter` (CLI). Es la única duplicación estructural.

> Nota sobre nomenclatura interna: el código comenta `// transición limpia: ClearScreen + TUI 2 (Ave) antes de cualquier elemento de TUI 2` (`CondorTui.cs:171`). Esto **no** son modos distintos: son las fases internas de I1 — *Condor Grande* (welcome) y *Condor Ave* (sesión de trabajo) — dentro de la misma TUI (I1). No se expone como interfaz separada al usuario.

## 2. Flujo exacto de entrada desde `condor.exe`

1. **UTF-8**: `Console.OutputEncoding = UTF8`.
2. Instancia `AssessmentService`, `LocalStateStore`, `LocalModelSession` (única, reutilizable). Ctrl+C → cancela + `session.ReleaseAsync` (keep_alive=0).
3. **I4 trivial primero**: `IsVersion(args)`→`VersionInfo.Product + " " + VersionInfo.DisplayName`; `IsHelp(args)`→`RenderHelp()`. Salen sin bootstrap.
4. **Sin args**:
   - si `CanRun` (entrada/salida no redirigida **y** VT habilitado **y** `width>=80 && height>=24`) → **I1** (`CondorTui.RunAsync`).
   - si no → **I2** (`RunInteractiveAsync`).
5. **Con args**: `IntentionRouter.Route(first)`:
   - `/<comando>` (token conocido) → `HandleSlashAsync`. **Excepto `/ayuda`/`/version`**: antes se ejecuta bootstrap (`RunBootstrapAsync`) + preparación (`PrepareOnceAsync`). `/analizar` no es `AnalyzeCommand`; lo implementa `AssessCommand`. `/salir` NO está en la tabla del router (para TUI: `IsExit`; para CLI: fin de stream).
   - texto libre no vacío → **I3**: bootstrap → `AgentCommand.ExecuteAsync(new AgentService(...), args, token)`.
   - `/versatil, /exit, /quit` — **no son** comandos del router; manejados por `IsExit` (TUI) / no aplican (CLI one-shot).
6. **Orden de aparición:** siempre I4 si aplica; luego I1 (si VT+tamaño) o I2; luego I3 (con args).
7. **Transiciones de bootstrap/selección modelo** (misma secuencia en I1 e I2):
   - `DependentBootstrapper.RunAsync` (Ollama: detectar/instalar/arrancar/verificar) → `StartupProgressPresenter`/`TuiStartupView`.
   - `StartupPreparer.RunAsync` (elige modelo local; no hay interacción usuario).
   - La selección ocurre **después** del bootstrap, **antes** del primer prompt/repl.
   - En I1: el bootstrap corre en **background** mientras el render loop mantiene la pantalla viva (`arranque` task + `while (!arranque.IsCompleted)` tick/sleep 40ms). La TUI aparece al instante.
8. **Entrada de I1 (loop principal):** `host.Enter()` (alt screen) → `ShowWelcome` + `SetWorkspace` → `Repaint` (imagen inmediata) → bootstrap bg → `ShowSession(model)` + `SetModel` + `SetWorkspace` → `SetEstado("En espera...", Success)` + `SetProgreso("—")` → `Console.ReadKey` loop:
   - `FreeIntentionRoute` → `AgentService.RunAsync` sobre task background + `TuiAgentProgressView`.
   - `SlashRoute` → `host.SuspendAsync(...)` (alt screen sale, corre comando establecido, reentra). `/ayuda` se muestra **in situ** (no suspende).
   - Comentario de usuario (`-texto-`) → registrado como actividad, **nunca** interpretado.
   - Doble `Esc` (o Ctrl+C sin tarea) → `break` → `host.Dispose()` + `"Hasta pronto."`.

## 3. Zonas internas de I1 (TUI persistente sobre `TuiHost`)

Dimensiones: `MinWidth=80`, `MinHeight=24`. Layout fijo de `TuiHost` (altura total = `HeaderHeight(15) + activity + BottomRows(4)`):

| Zona | Filas | Responsable (`TuiHost`) | Contenido | Estado |
|------|-------|--------------------------|-----------|--------|
| **Cabecera / identidad** | 1 (fila 0) | `PaintTitleRow`+`PaintTitleRowLocked` | `CONDOR` blanco negrita (izq); `Hecho en Colombia · Modo Local 100% · <modelo>` dorado (dcha) | ✅ Implementado. Única línea, modelo actualizado vía `SetModel`. |
| **Mascota (Condor Ave V16)** | 2–14 (13 filas) | `PaintHeaderLocked` | Ave V16 anclado a **derecha** (`ColumnaMascotaDerecha`); nombre institucional | ✅ Implementado. La identidad NO incluye texto de modelo en el área de la mascota. |
| **Separador actividad** | 1 (fila 14) | `PaintActivityBorder`/`PaintHeaderLocked` | `── Actividad del agente ──…` | ✅ Implementado. |
| **Conversación / actividad** | 15 (filas 15…) | `PaintActivity`/`PaintActivityLocked` | Histórico `ActivityKind`: `●` sistema, `◆` respuesta Condor (crema), `●` ok, `▲` aviso, `✗` error, `>` entrada usuario; word-wrap | ✅ Implementado. Diferenciación por prefijo/color. |
| **Separador entrada** | 1 (fila N-4) | `PaintFooterSeparator` | regla `──…` (slogan **no** aparece en sesión) | ✅ Implementado. |
| **Entrada del usuario** | 1 (fila N-3) | `RenderInput`/`PaintInputRegion` | `> ¿Qué deseas construir? …` o editor con caret; mensaje `Condor esta trabajando…  Esc + Esc interrumpe` cuando ocupa | ✅ Implementado. |
| **Estado / progreso** | 1 (fila N-2) | `PaintProgreso`/`PaintProgressLocked` | `<progreso>` (p.ej. `Iteracion 2`, `◐ Verificando X`, tiempo) — real, de `IAgentProgressView` | ✅ Implementado. Sólo fases con detalle real. |
| **Barra de estado (persistente)** | 1 (última, fila N-1) | `PaintStatus`/`PaintStatusLocked` | `> <workspace> · * <modelo> · <estado> · <version>` — **siempre visible**, actualizado vía `SetWorkspace`/`SetModel`/`SetEstado` | ✅ Implementado (la única zona fija que nunca se esconde). |

**Zonas necesarias que NO existen aún (pendientes — NO implementar en T-018):**
- **Editor/área de código embebido**: no hay zona para view diffs/preview de archivos; los cambios se comunican solo vía actividad (`He modificado <paths>` del agente → actividad de Condor).
- **Ventana de output crudo del modelo**: no se muestra el texto del modelo/agente por separado; sólo la respuesta de Condor se renderiza (`AgentRenderer`).
- La distinción visual "respuesta de Condor vs proceso del agente" existe solo por prefijo (`◆` vs `●`); un divisor explícito entre ambas no está.

## 4. Zonas internas de I2 (CLI clásica)

El flujo de `RunInteractiveAsync` + `Interpreter`:

| Paso | Lugar | Responsable | Contenido |
|------|-------|-------------|-----------|
| Preparación | stream normal | `StartupProgressPresenter` (`TuiScreen`) | spinner + etiqueta de etapa real + barra `%` (solo downloads reales) + elapsed; etapas archivadas. |
| Bienvenida | stream normal | `RenderWelcome` (`Terminal`) | modelo (`Modelo local listo: <m>`); `Directorio de trabajo: <ruta>`; instrucción. |
| Prompt repetitivo | stream normal | `Interpreter` + `onBeforePrompt` | redibuja `IdentityHeader.Render` (superior: `Condor` + slogan + `> <dir>`; inferior: `©Condor - <modelo> - <tiempo>`) + `> ` prompt. |
| Entrada | stream normal | `Interpreter` | una línea `Console.ReadLine`. |
| Resultado final | stream normal | `AgentRenderer.RenderResult` (`Terminal`) | `Condor` + slogan; `Tarea:`; `Contexto del entorno:`; archivos revisados; errores; archivos modificados; pie `©Condor - <modelo> - <elapsed>`. |

**Pendientes de I2 (NO implementado aún — NO implementar en T-018):**
- No hay **barra de estado fija**; identidad se redibuja cada prompt y se desplaza con el scroll.
- La **línea en sitio** de `TuiScreen` compite con el scroll natural del stream.

## 5. Responsabilidades: qué es de Condor vs del modelo/agente

- **Condor (plataforma):** identidad (`Condor` + slogan + `©Condor` sólo en pie de `AgentRenderer`/`IdentityHeader.RenderFooter`), mascota (Ave V16), header/píe de identidad, barra de estado (I1) / redibujado de identidad (I2), zona de actividad/conversación con prefijos por `ActivityKind`, progreso de arranque y fases del agente, `> <dir>` en cabecera. El `©` aparece **únicamente** en el pie de `AgentRenderer` (`©Condor - <modelo> - <tiempo>`) y en `IdentityHeader.RenderFooter`.
- **Agente/modelo:** intención (`AgentService.RunAsync`), herramientas (`AgentToolset`), razonamiento/pasos y sus observaciones/cambios (propios de `AgentResult`). La **respuesta final** se entrega como `AgentResult` → renderizada por `AgentRenderer`. **El contenido completo de archivos NO se imprime** (regla explícita de `AgentRenderer`).
- **Unificado:** `AgentRenderer` (respuesta), `IntentionRouter` (entrada), `AgentResult` (contrato de datos).
- **Duplicado (deuda a documentar):** presentación de progreso — una implementación por modo (ver TUI vs CLI en §1).

## 6. Qué debe permanecer visible durante el trabajo

- **I1 (TUI):** la **barra de estado** (fila N-1) y la **zona de actividad** permanecen siempre. El modelo en la cabecera se actualiza dinámicamente (`SetModel`, con restauración a `prep.Model` al terminar la tarea). El workspace en barra de estado se actualiza vía `SetWorkspace`. Mientras el agente trabaja, el teclado se reserva (Ctrl+C inactivo; solo Esc+Esc cancela cooperativamente) y la zona de entrada muestra el mensaje ocupado.
- **I2 (CLI):** cabecera/píe de identidad (redibujados cada prompt por `onBeforePrompt`) + directorio en cabecera y en `RenderWelcome`. Permanecen visibles entre prompts.
- **Persistencia de zonas:** en I1 se repinta por regiones sucias (`Host.Repaint`, hilos de trabajo sólo publican estado); en I2 se redibuja `IdentityHeader` antes de cada prompt para no perder contexto.

## 7. Validación real (Release/producción)

- **Build:** `dotnet build -c Release` → 0 errores, 0 advertencias (8 proyectos).
- **`condor --version`** → `Condor v1.0 · build interno α.01` (una línea, vía `VersionInfo`).
- **`condor --help`** → identidad `C O N D O R` + slogan + versión + ayuda de comandos (vía `RenderHelp`).
- **Limitaciones detectadas en este entorno (no interactivo):**
  - I1 (TUI) requiere terminal con VT + entrada/salida no redirigida + ≥80×24 → **no ejecutable** en CI/no-interactivo. No se pudo iniciar `condor` sin args aquí.
  - I2 (CLI clásica) e I3 (one-shot) requieren Ollama local + modelo descargado → **no ejecutables** sin proveedor. No se pudo validar bootstrap/end-to-end.
  - Se validó **estáticamente**: suite `Condor.Cli.Tests` (ejecución `dotnet test -c Release`) = **48/48 verdes** (cobertura de ruteo, identidad, renderizado de resultados y progreso).
  - Se validó **I4 en producción real**: `condor --version` y `condor --help` ejecutados sobre el binario de `Release/` → salida correcta y formateada en UTF-8.
  - No se ejecutó el binario real de I1/I2/I3 porque depende de Ollama/terminal interactiva no disponible.

## 8. Cambios estrictamente necesarios para T-018 (coherencia visual; **no** migran la mascota ni tocan agente/modelos/presupuesto)

1. **Unificar presentación de progreso** tras una superficie `ISuperficieUi` (`TuiHost` = alt-screen; `TuiScreen` = stream), con una sola implementación por interfaz (`TuiStartupView`/`StartupProgressPresenter` y `TuiAgentProgressView`/`AgentProgressPresenter` colapsan a un renderizador cada uno). Prioridad: altísima (duplicación raíz).
2. **Barra de estado persistente en I2** (alineada a I1) que incluya directorio, modelo y estado, sin depender del scroll. Prioridad: alta.
3. **Divisor visual entre "respuesta de Condor" y "actividad/proceso del agente"** dentro de la zona de conversación (reforzar prefijos/colores `◆`/`●`), sin mezclar responsabilidades. Prioridad: media.
4. **Compartir la identidad visual (header/píe)** entre I1 e I2 para evitar divergencia de formato. Prioridad: mediana.
5. `.gitignore` para artefactos sueltos (`err_cli.txt`, `out_cli.txt`, `validacion_tui.*`, `=0`, `Release/`, `publish/`). Prioridad: baja.

## 9. Pendientes documentados (NO implementar en T-018)

- Visualización del output crudo del modelo fuera de la respuesta de Condor (I1 e I2).
- Zona de editor/visualizador de diffs/código embebido.
- Barra de estado fija en I2 (dependiente del ítem 2 de §8).