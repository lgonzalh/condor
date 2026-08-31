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

## 5. P5 — Mascota (Prioridad 5) ✅ IMPLEMENTADO (corrección T-020 P5)
Decisión del usuario: **una sola identidad**, con dos presencias del mismo arte:

- **Grande al 100%** (bienvenida/inicio): sin cambios, 1:1 del SVG, rejilla 15×12.
  Invariante «no se escala» preservado; los tests `Mascota_Grande_*` verdes.
- **Ave pequeña = Grande reducido proporcionalmente 2:1**: la `SmallCondorMatrix`
  (7×6, 6 filas) se deriva del `GrandeMatrix` mediante muestreo celda-centro
  (una sola identidad, no una segunda mascota). Reutiliza la misma paleta aprobada
  del Grande (cabeza terracota 167, pico dorado 179, collar blanco 255, cuerpo
  233-238). La mitad inferior del pico (`b`/`48;5;167`) se pierde por el muestreo
  2:1 de arte celular (propiedad inherente al escalado), pero la cabeza, el cuello,
  el pico superior y el cuerpo conservan la misma identidad del Grande.

Test reescrito: `Mascota_Ave_Pequena_DerivaDeGrandeAl50_ReutilizaPaleta` →
`Mascota_Ave_Pequena_EsElGrandeReducido2a1_ReutilizaPaleta` (6 filas, ancho 7,
escala 2:1 celda-centro, paleta del Grande, sin 232/242/97m). Las pruebas de
layout que usaban `Ave` estructuralmente (`Mascota_PosicionadaALaDerecha`,
`Tui2_Sesion_EntradaEnParteInferior`, `Mascota_ZonaLibre_DeTextoDeModelo`)
siguen verdes (el placeholder está anclado al pie; el modelo solo aparece en la
fila de cabecera).

Resultado: **54/54 verdes**; `condor --version`/`--help` sin cambio (el arte solo
se muestra en la TUI I1).
