# T-020 — Unificación de etiquetas de progreso y barra de estado de la CLI

> Subtarea de la época `07_Interfaz`. Deriva de la deuda documentada en **T-019**
> (auditoría de implementación TUI/CLI). Estado: **P1–P4 finalizados y verificados**;
> **P5 (mascota) bloqueado** pendiente de decisión (ver apartado al final).

## 1. Problema
- La **TUI** (`TuiStartupView`, `TuiAgentProgressView`) y la **CLI clásica**
  (`StartupProgressPresenter`, `AgentProgressPresenter`) mantenían **dos
  implementaciones duplicadas** de las mismas etiquetas, con textos **divergentes**:
  - CLI: `"Preparando entorno"`, `"Observando"`, `"Recursos detectados"`, barra de ancho 10.
  - TUI: `"Preparando el entorno local"`, `"Observando el proyecto (acción ruta)"`,
    `"Recursos del equipo detectados"`, barra de ancho 14.
- La **CLI carecía de barra de estado fija**: `IdentityHeader.RenderFooter` imprimía
  `©Condor - modelo - tiempo` en cada punto de espera, pero sin directorio ni
  estado, y no se reimprimía tras la respuesta del agente (modo one-shot sin barra).

## 2. Solución

### P1 — Origen único de etiquetas (Prioridad 1) ✅
- **Nuevo `Src/Condor.Cli/Presentation/AgentProgressLabels.cs`** (`internal static`):
  única fuente de verdad para `PhaseTag`, `PhaseEstado`, `Subject`, `BuildProgreso`,
  `StageTag`, `StageLabel`, `StageEstado`, `StageCompleted`, `BuildBar`,
  `FormatPercent`, `FormatElapsed`.
- **Delegación**: `TuiStartupView`, `TuiAgentProgressView`,
  `StartupProgressPresenter` y `AgentProgressPresenter` ahora delegan a este origen.
  Las superficies siguen siendo responsabilidad de cada presentador (TuiHost
  regiones vs TuiScreen línea de estado); **solo el texto de las etiquetas es
  compartido**, por lo que TUI y CLI no pueden volver a divergir.
- Las etiquetas canónicas se conservan **idénticas al texto rico de la TUI**
  (ver `EstadosHonestosTests`), por lo que la unificación no altera el output
  validado.

### P2 — Barra de estado fija de la CLI (Prioridad 2) ✅
- **Nuevo `Src/Condor.Cli/Presentation/CliStatusBar.cs`**: 
  - `BuildFooterText(...)` — texto puro y determinista: `©Condor · > <dir> · * <modelo> · <estado> · <versión> · ⚠` (el `⚠` solo en fallo). **Testeable**.
  - `RenderFooter(...)` — delega la E/S a `Terminal.WriteWhite`.
- `IdentityHeader.RenderFooter` ahora delega a `CliStatusBar` (firma extendida,
  retrocompatible con la llamada de 1 arg en `onBeforePrompt`). El `©` sigue
  **solo en el pie**, nunca en la cabecera superior.
- `AgentCommand.ExecuteAsync` reenvía la barra fija tras
  `AgentRenderer.RenderResult(...)`, con estado real de la respuesta
  (`Listo`/`Error` + `⚠`). Esto incluye el **modo one-shot**, que no vuelve al
  prompt.
- **No se anima ni redibuja durante la ejecución del agente**: el spinner del
  `AgentProgressPresenter` ocupa su propia línea y la barra permanece estática,
  evitando colisiones por el cursor. La barra se reinstala en cada punto de
  espera de entrada (`onBeforePrompt`) y al final de cada respuesta.

### P3 — Pruebas (Prioridad 3) ✅
- **Nuevo `Tests/Unit/Condor.Cli.Tests/CliStatusBarTests.cs` (6 tests)**: identidad
  permanente + versión, modelo-vacio → `modelo local`, alerta `⚠` en fallo,
  ausencia de `⚠` en OK, `©` exactamente una vez (solo en pie), estado vacío →
  `Entorno listo`.
- Resultado: **54/54 passed** (48 preexistentes + 6 nuevas), 0 fallas.

### P4 — Limpieza `.gitignore` (Prioridad 4) ✅
- Añadidas artefactos sueltos observados en el working tree: `/Release/`,
  `/publish/`, `err_cli.txt`, `out_cli.txt`, `validacion_tui.*`, `=0`.
- Se evitó patronrúa amplia (`*.png`/`*.svg`/`*.jpg`): los assets reales
  (`Assets/condor_mascota.svg`, etc.) siguen tracked.

## 3. Verificación
| Check | Resultado |
|---|---|
| `dotnet build -c Release` | 0 errores, 0 advertencias |
| `dotnet test -c Release` | 54/54 passed (48 previos + 6 nuevas) |
| `condor --version` | `Condor v1.0 · build interno α.01` (exit 0) |
| `condor --help` | OK, menciona `/ayuda` (exit 0) |
| 48 tests TUI preexistentes | Siguen verdes (sin regresión P1) |

## 4. Limitaciones / alcance fuera del ticket
- La barra fija en modo interactivo **no se verificó visualmente en TTY** (entorno
  de CI sin consola interactiva real). La lógica está guarded: si la salida está
  redirigida o la altura es insuficiente, `RenderFooter` degrada a `Console.WriteLine`
  (comportamiento previo). El texto (`BuildFooterText`) está cubierto por tests.
- **No se modificaron** `AgentInterpreter`, `Condor`, `Ollama`, `AgentResult`,
  `Mascota`, ni las estructuras de datos del Core/Infrastructure (T-020 lo prohibía).
- La barra se reimprime en los puntos de parada (antes del prompt y tras cada
  respuesta); durante el scroll intrarés de una respuesta, el pie visible está
  dado por la firma de respuesta (`©Condor - modelo - tiempo`). La persistencia
  entre operaciones sí se mantiene.

## 5. P5 — Mascota y firma (PRIORITY 5) — ⏸ BLOQUEADO, pendiente de decisión
T-020 P5 indica: *"La mascota grande será la ÚNICA. Reducida = misma mascota al 20%;
se elimina el Ave."* Esto **conflicta con invariantes documentadas** y con la
legibilidad:
- `Docs/07_Interfaz/MASCOTA_CLI_UNICODE.md` §3: **«Dos presencias de Condor»**
  (Grande y Ave coexisten).
- `CondorArt.cs`: invariante de diseño **«No se escala ni se transforma
  geométricamente»** (Grande 15×12, Ave 13×8).
- **20% de 15×12 ≈ 3×2 caracteres** → mascota ilegable; rompería las pruebas
  validadas de contraste/mascota (`Mascota_AveV16_ConservaGeometria_*`,
  `Mascota_GrandeEstandarizada_*`).

Se solicita decisión antes de tocar la mascota:
1. Mantener **dos presencias** (estado actual, invariante documentado).
2. Adoptar **Grande única al 20%** (T-020 literal) — con fuerte pérdida de calidad.
3. Otra alternativa.