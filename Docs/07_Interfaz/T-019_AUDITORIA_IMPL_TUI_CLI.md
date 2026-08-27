# T-019 — Auditoría técnica y propuesta de implementación TUI/CLI

> Especificación de referencia: `Docs/07_Interfaz/T-018_DEFINICION_TUI_CLI.md`. Esta auditoría compara la **implementación actual real** (repositorio en `\(agosto 2026\), incluyendo el commit UX `647cb69`) contra T-018. **No asume** que el código coincide con T-018: se comprueba contra el código. No modifica Core/Infrastructure/presupuesto/lógica de agente. No rediseña la mascota pequeña. Los mockups son referencia visual secundaria.

## 1. Arquitectura actual (evidencia de código)

### 1.1 Entrada — `Program.cs::Main` (despacho real de los 4 modos)
- UTF-8 (`Console.OutputEncoding`). Única sesión `LocalModelSession` (modelo único; Ctrl+C → `session.ReleaseAsync` keep_alive=0).
- **I4 trivial** (primero): `IsVersion(args)`→`Console.WriteLine(VersionInfo.Product + " " + VersionInfo.DisplayName)`; `IsHelp(args)`→`RenderHelp()`. Salen sin bootstrap.
- **Sin args**: `CondorTui.CanRun(out w,h)` → si VT + `Console.IsOutput/InputRedirected==false` + `width>=80 && height>=24` → **I1**; si no → **I2** (`RunInteractiveAsync`).
- **Con args**: `IntentionRouter.Route(first)` → `SlashRoute` (tabla `Commands/`, switch `HandleSlashAsync` en `Program.cs`; NOTA: `/analizar` lo sirve `AssessCommand`, **no** existe `AnalyzeCommand`) o `FreeIntentionRoute` → `AgentCommand.ExecuteAsync`.
- **Bootstrap/selección modelo**: `DependentBootstrapper.RunAsync` (Ollama) → `StartupPreparer.RunAsync` (modelo). Ocurren **después** del bootstrap, **antes** del prompt. En I1 corre en background con render loop 40ms manteniendo la pantalla viva.

### 1.2 I1 — TUI persistente sobre `TuiHost` (`Src/Condor.Cli/Tui/`, buffer alterno)
Layout fijo (`HeaderHeight=15`, `BottomRows=4`; `MinWidth=80`, `MinHeight=24`, pruebas 110×34):

| Zona | Filas lógicas | Responsable pintura | Responsable datos | Contenido actual |
|------|----------------|----------------------|-------------------|------------------|
| **Cabecera/identidad** | 0 (title row) | `PaintTitleRowLocked` | `SetModel` | `CONDOR` (blanco negrita) · `Hecho en Colombia · Modo Local 100% · <modelo>` (dorado, dcha). Única línea. |
| **Mascota (Condor Ave V16)** | 2–14 | `PaintHeaderLocked` → `CondorArt.Ave` | (estática) | Anclada a derecha (`ColumnaMascotaDerecha = _width - AnchoVisibleMascota() - 5`). Sin texto invadiendo su área. |
| **Separador actividad** | 14 | `PaintSeparatorLocked(..,"Actividad del agente")` | — | `── Actividad del agente ──…` |
| **Conversación/actividad** | 15…(N-4) | `PaintActivityLocked` | `AddActivity` | Histórico `ActivityPrefix`: sistema `●`, Condor `◆`(crema), ok `●`(verde), aviso `▲`, error `✗`, entrada `>`(usuario blanco). Word-wrap a `_width`. |
| **Separador entrada** | N-4 | `PaintSeparatorLocked(null)` | — | regla `──…` (slogan NO aparece en sesión). |
| **Entrada** | N-3 | `PaintInputRegionLocked` / `TuiInput.BuildInputLocked` | `RenderInput` | `> ¿Qué deseas construir? …` o editor con caret; `Condor esta trabajando… Esc + Esc interrumpe` cuando `_busy`. |
| **Progreso/iteración** | N-2 | `PaintProgresoLocked` | `SetProgreso` | `Iteracion N · RAM libre X GB, presupuesto Y GB (…) · ms:MSS` o `—`. |
| **Barra de estado (persistente, última)** | N-1 | `PaintStatusLocked` | `SetWorkspace`/`SetModel`/`SetEstado` | `> <workspace 28c> | * <modelo 22c> | <estado (nunca recortado)> | <version>` + spinner cuando `_busy`. |

- **Progreso real**: `TuiAgentProgressView` (`TuiViews.cs`) — fases `Understanding/Observing/Analyzing/Building/Verifying/Finalizing` → `SetEstado` (etiqueta real) + `SetProgreso` (iteración, RAM/presupuesto, mensaje, elapsed). Nada inventado.
- **Resultado del agente en TUI**: `CondorTui.cs:234` → `AgentRenderer.BuildResultText(result, elapsed)` → líneas → `host.AddActivity(line, result.Success ? Condor : Error)` (prefijo `◆` si éxito).
- **Sólo zona de entrada** es editable (`TuiInput`, "Editor de línea"). **No hay zona de editor/diff/visualizador de código** (grep `PaintEditor|Editor|Diff|FileView|CodeView` → única coincidencia: comentario del editor de línea de `TuiInput`).

### 1.3 I2 — CLI clásica interactiva (`Program.cs::RunInteractiveAsync` + `Interpreter`)
- Preparación: `StartupProgressPresenter` (`TuiScreen.Shared`) — spinner + etiquetas reales + barra `%` solo en downloads.
- `RenderWelcome(prep)`: `Modelo local listo: <m>`, `Directorio de trabajo: <ruta>`, instrucción.
- Loop `Interpreter.RunAsync` con `onBeforePrompt` → redibuja `IdentityHeader.Render` (superior: `Condor` + slogan + `> <dir>`; inferior: `©Condor - <modelo> - <tiempo>`) + prompt `> `.
- Entrada: `Console.ReadLine`. Resultado: `AgentRenderer.RenderResult`.
- **No hay barra de estado fija**: la identidad se redibuja cada prompt y **se desplaza con el scroll**.

### 1.4 I3 — CLI one-shot (`AgentCommand.ExecuteAsync` / `HandleSlashAsync`)
- `AgentProgressPresenter` → `TuiScreen.Shared` (sólo en free intent). Resultado: `AgentRenderer.RenderResult` o JSON (`AgentJson.Serialize` con `--json`).

### 1.5 Responsabilidades (verificado en código)
- **Condor**: identidad (`Terminal`/`IdentityHeader`/`AgentRenderer`); `©` **sólo** en pie de `AgentRenderer` y `IdentityHeader.RenderFooter` (no en `--version`/`--help`/header TUI); barra de estado (I1); progreso; zona de actividad.
- **Agente/modelo**: intención, herramientas, pasos, respuesta final. `AgentRenderer` **no imprime contenido completo de archivos** (regla explícita).
- **Unificado**: `AgentRenderer` (respuesta) + `IntentionRouter` (entrada) + `AgentResult` (contrato).

### 1.6 Tests (`Tests/Unit/Condor.Cli.Tests/TuiTests.cs` — 48/48 verdes, Release)
Cubren: identidad fija, geometría/Contraste Ave V16, derivación V16 de rejilla oficial, estado real de arranque/agente (fases, acción+ruta, error proveedor), sesión muestra identidad+modelo+zonas, Ave a la derecha / zona libre de texto de modelo, modelo dinámico, estado de verificación real, bienvenida con Ave grande sin titulares, comentarios de usuario no ejecutables, workspace de `CurrentDirectory`, entrada en parte inferior, ayuda in-situ sin suspender, salir. **No hay test de editor/diff (no existe)**; **no hay test de I2 (CLI)** ni de bootstrap real (dependa de Ollama). Snapshots validan I1.

## 2. Diferencias frente a T-018

| Ítem T-018 | Estado actual | Diferencia / confirmación |
|-------------|---------------|---------------------------|
| 4 modos I1–I4 | Identificados exactamente | ✅ Coincide |
| Zonas I1 (cabecera, mascota, actividad, entrada, progreso, barra estado) | Dibujadas por regiones en `TuiHost` | ✅ Coincide; barra de estado en **fila N-1, persistente, actualizable** (confirmado `PaintStatusLocked`) |
| Workspace/modelo visibles | `SetWorkspace`/`SetModel` → barra estado + header dinámico | ✅ Coincide; progreso muestra iteración+RAM/presupuesto |
| `©` sólo en pie | Sí (`AgentRenderer` + `RenderFooter`) | ✅ Coincide |
| Barra de estado fija en I2 | **No existe**; redibujado por prompt + scroll | ❌ Diferencia: T-018 la marcaba pendiente → sigue pendiente |
| Unificar presentación de progreso (`ISuperficieUi`) | **No hecho**; duplicación TUI (`TuiViews`) vs CLI (`Presentation`) | ❌ Diferencia: T-018 la propuso como cambio necesario → sigue pendiente |
| Editor/diff/código embebido | **No existe** (sólo editor de línea) | ✅ Coincide con lo "no implementado" de T-018 |
| Output crudo del modelo visible | No (sólo respuesta de Condor) | ✅ Coincide |

## 3. Problemas encontrados

1. **Pérdida de contexto visual en I2 (CLI)** — sin barra de estado fija, el workspace/modelo/estado se desplazan al hacer scroll. (Gravedad: medio; el usuario pierde referencia mientras el agente produce salida.)
2. **Duplicación de capas de progreso** — `IStartupProgressView`/`IAgentProgressView` tienen dos implementaciones (`TuiViews` vs `Presentation/*Presenter`), manteniendo renderizado, throttling y "líneas archivadas" dispersos y propensos a divergir. (Gravedad: alta — deuda estructural.)
3. **Sin feedback de "modelo activo" durante la tarea en I2** — solo aparece en el pie de identidad redibujado; en I1 el header lo muestra dinámicamente, pero en I2 no hay zona fija. (Gravedad: baja-media.)
4. **No hay test de I2 ni de bootstrap** — la CLI clásica y el arranque dependen de Ollama; no se validan con tests ni con ejecución real en CA. (Gravedad: alta — cobertura).
5. **Artefactos sueltos no versionados** (`Release/`, `err_cli.txt`, `out_cli.txt`, `=0`, screenshots) sin `.gitignore`. (Gravedad: baja.)

## 4. Cambios estrictamente necesarios (próxima fase — pendiente autorización)

> **T-019 NO implementa nada.** Propuesta para la siguiente fase, ordenada y mínima:

1. **(Alta) Unificar progreso tras una sola `ISuperficieUi`.** Un `enum SuperficieMode { Stream, AltScreen }` + una implementación por interfaz de progreso que dibuje sobre `TuiScreen` (stream) o `TuiHost` (alt-screen). Elimina `TuiStartupView`/`StartupProgressPresenter` y `TuiAgentProgressView`/`AgentProgressPresenter` colapsándolos a uno cada uno. Sin tocar lógica del agente.
2. **(Alta) Barra de estado fija en I2.** Reutilizar la misma fila de estado de I1 (workspace · modelo · estado · versión) sobre `TuiScreen`, fija en la parte inferior no scrollable. Sin tocar mascota ni identidad existente.
3. **(Media) Tests de I2 y routing** (snapshot de `IdentityHeader` + `AgentRenderer` sobre stream) para bloquear regresiones de I2, ahora con cobertura de la barra de estado fija.
4. **(Media) Refuerzo de separación respuesta/proceso** en zona de actividad (divisor entre "progreso/estado del agente" y "respuesta de Condor") usando los `ActivityKind` ya existentes.
5. **(Baja) `.gitignore`** para los artefactos sueltos.

## 5. Orden recomendado de implementación

1. Unificación de presentación de progreso (`ISuperficieUi`) — base común.
2. Barra de estado fija en I2 (soportada por el ítem 1).
3. Tests I2 + snapshot de identidad/barra.
4. Divisor respuesta/proceso.
5. `.gitignore`.

*(La máscota pequeña NO se toca en ningún paso.)*

## 6. Tests necesarios

- `Condor.Cli.Tests`: 48/48 actuales siguen verdes (validación base).
- Nuevos: snapshot de `IdentityHeader.Render`/`RenderFooter` sobre stream; snapshot de barra de estado fija I2 (workspace/modelo/estado/versión persistentes); test de `TuiHost`/`TuiScreen` compartiendo progreso vía `ISuperficieUi` (mismo output para stream y alt-screen).
- No tocar Core/Infrastructure: los tests de agente/cycle/build permanecen sin cambios.

## 7. Validación Release/producción

- `dotnet build -c Release` → 0 errores, 0 advertencias (verificado previamente).
- `condor --version` → `Condor v1.0 · build interno α.01` (verificado sobre `Release/condor.dll`).
- `condor --help` → identidad + uso (verificado).
- **Limitaciones:** I1/I2/I3 no ejecutables en este entorno no interactivo (VT + Ollama/no modelo). Validación real de I1/I2 pendiente de terminal interactiva + Ollama; se respalda con 48/48 tests unitarios (snapshots de I1) + ejecución real de I4.