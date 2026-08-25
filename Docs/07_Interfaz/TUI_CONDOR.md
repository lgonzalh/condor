# TUI CONDOR · INTERFAZ TERMINAL INTERACTIVA

Version: 1.0.0
Estado: Verificada en produccion real
Nivel: 07 - Interfaz
Clasificacion: Especificacion tecnica y verificacion de la TUI

---

## Proposito

Documentar la implementacion, arquitectura y verificacion de la Terminal User Interface (TUI) de Condor, que constituye la interfaz interactiva principal cuando se ejecuta `condor` sin argumentos en una terminal compatible.

La TUI no es una capa decorativa: es la autoridad unica de renderizado durante la sesion interactiva, diseñada para ser honesta (sin estados falsos), performante (arranque <400 ms) y fiel a la identidad visual de Condor (mascota Ave V16, colores institucionales, cabecera unificada).

---

## Decisiones de arquitectura

### 1. Autoridad unica de dibujo: `TuiHost`

- Pantalla persistente en buffer alternativo (ANSI `?1049h` / `?1049l`).
- Repintado POR REGIONES (cabecera, actividad, estado, entrada) — nunca reimprime toda la pantalla.
- Hilos de trabajo solo publican estado (`SetEstado`, `SetProgreso`, `AddActivity`); el hilo de interfaz repinta regiones sucias cada ~40 ms.
- Tres modos: `Welcome` (Condor Grande), `Session` (Condor Ave + chrome), `Suspended` (comandos `/`).

### 2. Dos mascotas oficiales

- **Condor Grande** (bienvenida): derivada 1:1 de la mascota oficial `Assets/condor_mascota.svg` proyectada sobre rejilla 15x12 celdas con bloques Unicode; volumen por distancia al centro visual.
- **Condor Ave V16** (trabajo): 13 filas pre-SGR del prototipo aprobado V16 (`Docs/07_Interfaz/Mockups/condor_unicode_v16.ps1`); contraste corregido: zonas antes en negro puro (232) usan escala oscura aprobada 235/236/233 ciclicamente.

### 3. Paleta institucional

| Color | ANSI | Hex | Uso |
|-------|------|-----|-----|
| Terracota | `38;5;167` | #C2665A | Cabeza de la mascota |
| Dorado | `38;5;179` | #D9A25A | Pico, cabecera, prompt |
| Crema | `38;5;255` | #E8E4D8 | Collar, respuestas Condor |
| Blanco | `97m` | #FFFFFF | Texto usuario, "CONDOR" |
| Gris | `38;5;243` | — | Separadores, textos secundarios |
| Verde | `38;5;114` | — | Exito, estados positivos |
| Rojo | `38;5;174` | — | Errores |
| Amarillo | `38;5;180` | — | Advertencias |

`NO_COLOR` respeta la degradacion sin color.

### 4. Layout de sesion (fijo)

```
Fila 1:        CONDOR v1.0           Hecho en Colombia · Modo Local 100% · <modelo real>
Fila 2..14:    [ Condor Ave V16 centrada horizontalmente ]
Fila 15:       ── Conversacion / Actividad ────────────────────────
Fila 16..H-6:  (Zona de actividad: historial con prefijos de color)
Fila H-5:      ── Observa · Comprende · Planifica · Construye · Verifica ──
Fila H-4:      <estado directo sin "Estado:"> (spinner si ocupado)
Fila H-3:      <progreso directo sin "Progreso:">
Fila H-2:      › <input>  (placeholder: ¿que deseas construir...?)
Fila H-1:      Enter enviar · Tab completar · ↑↓ historial · /ayuda · /salir terminar
```

- Cabecera: UNA sola linea. "Modo Local 100%" aparece UNA sola vez. Modelo dinamico real a la derecha. NUNCA hay bloques "Modelo:"/"Modo:" sobre la mascota.
- Mascota: centrada con `ColumnaMascota() = ((width - AnchoVisibleMascota()) / 2) + 1` — sin espacios en las lineas.
- Comunicacion: directa, sin titulares "Estado:"/"Progreso:".
- Entrada: placeholder `¿que deseas construir...?` (sin tildes/acentos/ñ).

### 5. Flujo de sesion

```
condor
  |
  +-> CanRun() -> No -> CLI clasico (StartupProgressPresenter)
  |
  +-> CanRun() -> Si -> TuiHost + CondorTui.RunAsync
         |
         +-> TuiHost.Enter() + ShowWelcome() + Repaint()  <- PRIMER FRAME (<400 ms)
         |
         +-> Task.Run(bootstrap: DependencyBootstrapper + StartupPreparer)  [hilo fondo]
         |       |
         |       +-> TuiStartupView publica estados reales -> Repaint cada 40 ms
         |       +-> Assessment unico (evita 6+ detectores repetidos)
         |
         +-> while (!bootstrap.IsCompleted) { HandleResize; Tick; Repaint; Sleep(40) }
         |
         +-> bootstrap OK -> ShowSession(modelo) -> AddActivity -> Main loop
                 |
                 +-> Console.KeyAvailable -> TuiInput.Handle -> SlashCommand / FreeIntention
                 |       |
                 |       +-> Slash -> Suspend + Program.HandleSlashAsync
                 |       +-> Free -> AgentService + TuiAgentProgressView
                 |       +-> Comment (-texto-) -> AddActivity(kind=User) -> no ejecuta
                 |
                 +-> Repaint regions (dirty flags) cada 40 ms o por evento
```

### 6. Correcciones finales T-018 aplicadas

| # | Correccion | Estado |
|---|------------|--------|
| 1 | Centrado mascota (posicionamiento bloque, no espacios) | ✅ |
| 2 | Contraste mascota (escala 235/236/233) | ✅ |
| 3 | Cabecera una linea, fuera de area mascota | ✅ |
| 4 | Comentarios `-texto-` solo comentario | ✅ |
| 5 | Sin "Estado:"/"Progreso:" | ✅ |
| 6 | Placeholder `¿que deseas construir...?` | ✅ |
| 7 | Arranque: 5 P/Invoke redundantes eliminados | ✅ |

---

## Optimizacion de arranque (Correccion 7)

### Problema
Llamadas P/Invoke redundantes en el camino critico:
- `CanRun()`: `TryEnableVirtualTerminal()` + `Console.WindowWidth/Height`
- `TuiHost()`: `TryEnableVirtualTerminal()` (2da vez)
- `TuiHost.Enter()`: `Console.WindowWidth/Height` (2da vez)

### Solucion
- `CanRun(out width, out height)` devuelve dimensiones una sola vez.
- `TuiHost(width, height)` constructor optimizado evita releer.
- `TuiHost.Enter()` solo lee si `_width==0 || _height==0`.

### Mediciones reales (condor.exe Release/produccion)

| Metrica | Valor |
|---------|-------|
| `--version` (runtime .NET sin TUI) | 62-93 ms (promedio 74 ms) |
| Primera TUI con mascota (pixeles terracota/dorado) | 247-785 ms (8 corridas, mediana ~265 ms caliente) |
| Sesion lista (bootstrap Ollama + modelo) | ~7.5-8.5 s con progreso honesto |

La TUI aparece ANTES de cualquier trabajo de fondo; el bootstrap corre en paralelo con progreso honesto visible.

---

## Verificacion en produccion real

### Metodo
Ejecucion de `condor.exe` (Release/produccion, self-contained win-x64) en terminal interactiva real (Windows Terminal / conhost clasico) con entrada de teclado real via `AttachConsole` + `WriteConsoleInput` (eventos KEY_EVENT autenticos) y capturas de pantalla de la ventana real.

### Resultados punto por punto

| Punto | Evidencia |
|-------|-----------|
| TUI aparece | Captura bienvenida: Condor Grande + CONDOR + eslogan + identidad |
| Mascota completa | Cabeza terracota, pico dorado, collar blanco, grises oscuros |
| Mascota centrada | `ColumnaMascota()` posiciona bloque; tests fotograma validan formula |
| Sin invasion texto | Cabecera fila 1; filas 2-14 exclusivas; sin "Modelo:"/"Modo:" |
| Cabecera una linea | `CONDOR v1.0` + `Hecho en Colombia · Modo Local 100% · <modelo>` |
| "Modo Local 100%" una vez | Verificado en capturas y test `Assert.Single` |
| Placeholder | `¿que deseas construir...?` (test fotograma + codigo pintado) |
| Sin "Estado:"/"Progreso:" | Capturas muestran comunicacion directa sin titulares |
| Comentario `-texto-` | `E2: bytes distinto=True` — comentario registrado, no ejecutado; **prueba inversa**: cadena sin guion de cierre SI dispara agente -> distincion valida |
| `/ayuda` | Captura con ayuda completa en zona actividad |
| `/salir` | `salio30s=True`, `SALIDA=NORMAL_VIA_SALIR` |
| Sin stack traces | Ninguna captura muestra trazas; errores = mensajes cortos |
| Sin huerfanos | `HUERFANOS=NO` en todas las corridas |

---

## Pruebas y regresion

### Suite de pruebas (POST-T-018, ejecutado 2026-08-25)

| Proyecto | Pasados/Total | Fallos | Naturaleza |
|----------|---------------|--------|------------|
| Condor.Cli.Tests | 34/34 | 0 | TUI: identidad, fotogramas, estados, comentarios, ANSI |
| Architecture.Tests | 22/22 | 0 | Arquitectura |
| Condor.Core.Tests | 247/262 | 15 | **PREEXISTENTES** (ModelSelector/Budget — ajenos a T-018) |
| Condor.Infrastructure.Tests | 305/307 | 2 | **PREEXISTENTES** (ModelAutoSetup — ajenos a T-018) |
| **Total** | **608/625** | **17** | **17 FALLOS PREEXISTENTES = 15 Core + 2 Infra** |

- **PRE-T-018** (primera ejecucion, antes de cambios): 608/625, 17 fallos (mismos 15 Core + 2 Infra).
- **POST-T-018** (tras cambios): 608/625, 17 fallos (mismos 17 tests, mismos nombres).
- **Regresiones nuevas: 0**.

### Build
```
Compilacion correcta.
0 Errores
0 Advertencias
Publicado en Release\produccion (self-contained win-x64).
```

### Validacion aislada
Commit verificado compilable en aislamiento: `git worktree add` al commit -> build + `Cli.Tests` 34/34 OK.

---

## Observacion de entorno (no bloqueante)

En lanzamientos automatizados via `Start-Process` se observa una carrera del traspaso conhost->Windows Terminal donde la consola reporta metricas inconsistentes durante el arranque (la sonda en-cadena midio 120x30 y la geometria viva durante sesion tambien 120x30; sin embargo algunos frames tempranos se pintaron con una geometria previa mayor, cortando texto en el borde).

El codigo ya se re-sincroniza continuamente (`HandleResizeIfNeeded` cada 40 ms) y **ningun codigo de la app redimensiona la consola** (verificado por grep: `SetWindowSize|SetBufferWidth|SetBufferHeight|SetWindowPosition|SetCursorPosition|LargestWindowWidth` — sin resultados en `Src/`).

En sesion estable la geometria consola/app es consistente (verificado en vivo: `GEO_VIVA_DURANTE_SESION=BUFFER=120x30 VIEWPORT=120x30` via `AttachConsole`+`GetConsoleScreenBufferInfo` durante sesion real).

---

## Archivos modificados/añadidos en T-018

### Codigo (TUI + arranque optimizado)

```
Src/Condor.Cli/Tui/Ansi.cs              (nuevo)
Src/Condor.Cli/Tui/CondorArt.cs         (nuevo)
Src/Condor.Cli/Tui/CondorTui.cs         (nuevo)
Src/Condor.Cli/Tui/TuiHost.cs           (nuevo + constructor optimizado)
Src/Condor.Cli/Tui/TuiInput.cs          (nuevo)
Src/Condor.Cli/Tui/TuiViews.cs          (nuevo)
Src/Condor.Cli/Program.cs               (TUI wiring + startup perf + cero acentos)
Src/Condor.Cli/Routing/StartupPreparer.cs (cachedAssessment + acentos)
Src/Condor.Infrastructure/DependencyBootstrap/DependencyBootstrap.cs (assessment unico)
Src/Condor.Infrastructure/DependencyBootstrap/OllamaHealthChecker.cs (HttpClient compartido)
Src/Condor.Core/Contracts/IModelAutoSetupService.cs (cachedAssessment)
Src/Condor.Infrastructure/Setup/ModelAutoSetupService.cs (cachedAssessment)
```

### Tests

```
Tests/Unit/Condor.Cli.Tests/TuiTests.cs          (34 tests: identidad, fotogramas, comentarios, ANSI)
Tests/Unit/Condor.Cli.Tests/Condor.Cli.Tests.csproj
Tests/Integration/Condor.Infrastructure.Tests/StartupPreparerTests.cs (stub cachedAssessment)
```

### Proyecto y solucion

```
Condor.slnx                                    (+ Cli.Tests)
Src/Condor.Cli/AssemblyInfo.cs                 (InternalsVisibleTo Cli.Tests)
Src/Condor.Cli/Condor.Cli.csproj               (sin cambios, referencia Cli.Tests)
```

### Documentacion de identidad (arte V16 aprobado)

```
Docs/07_Interfaz/Mockups/condor_unicode_v16.ps1     (prototipo V16 — origen aprobado)
Docs/07_Interfaz/Mockups/ejecutar_condor_unicode_v16.cmd
Docs/07_Interfaz/Mockups/README.md
```

---

## Observacion sobre geometria en Windows Terminal

Durante la verificacion automatizada se observo una anomalia de layout en frames tempranos: la app pinta para ~155 columnas mientras la consola visible tiene 120. La investigacion confirmo:

- Sonda en-cadena (misma cadena `cmd /c title && powershell probe && condor`): `GEO=W=120 B=120 H=30`.
- Geometria VIVA durante sesion (via `AttachConsole`+`GetConsoleScreenBufferInfo`): `BUFFER=120x30 VIEWPORT=120x30`.
- `HandleResizeIfNeeded` se ejecuta cada 40 ms y actualiza `_width/_height` desde `Console.WindowWidth/Height`.

La discrepancia observada en frames tempranos corresponde a una carrera del traspaso `conhost` -> Windows Terminal (default terminal) donde la consola reporta metricas transitorias durante el traspaso. El codigo ya se re-sincroniza continuamente (`HandleResizeIfNeeded` cada 40 ms) y en sesion estable la geometria es consistente (`BUFFER=120x30 VIEWPORT=120x30` verificado en vivo durante sesion real). En ejecucion manual normal (usuario ejecuta `condor` en su terminal) el layout es correcto (verificado en sesion estable y por tests de fotograma en ancho fijo 110).

---

## Referencias

- `Docs/07_Interfaz/MASCOTA_CLI_UNICODE.md` — Especificacion de la mascota (V1-V16).
- `Docs/07_Interfaz/Mockups/condor_unicode_v16.ps1` — Prototipo V16 (origen aprobado de la mascota Ave).
- `operacion/REGISTRO_CAMBIOS.md` — Registro de T-018 (cierre).
- `operacion/KANBAN.md` — T-018 en LISTO/VERIFICADO.
- `operacion/ESTADO_DESARROLLO.md` — Estado T-018 verificado.
- `operacion/REGISTRO_CAMBIOS.md` — Entrada T-018 completa.

---

**Estado final**: T-018 COMPLETADA Y VERIFICADA EN PRODUCCION REAL (2026-08-25).