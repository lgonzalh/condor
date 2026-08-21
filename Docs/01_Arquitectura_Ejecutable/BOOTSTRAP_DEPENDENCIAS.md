# BOOTSTRAP DE DEPENDENCIAS — OLLAMA

Version: 1.0.0
Estado: Vigente
Nivel: Ejecución (puesta en marcha)
Fecha: 2026-08-21

## PROPOSITO

Cuando el usuario ejecuta `condor`, Cóndor debe preparar automáticamente el
entorno necesario ANTES de comenzar el flujo normal. El usuario no administra
manualmente las dependencias que Cóndor puede detectar, preparar y verificar.

Principio UX: "El usuario inicia Cóndor; Cóndor prepara el entorno necesario."

## FLUJO

```
Usuario
  ↓
condor
  ↓
Bootstrap de dependencias (Cóndor)
  ↓
Verificar dependencias
  ↓
Preparar Ollama
  ↓
Verificar server real (endpoint)
  ↓
Verificar modelo/configuración
  ↓
Entorno listo
  ↓
Flujo normal de Cóndor
```

## ESTADOS DE OLLAMA (OllamaHealthChecker)

Se distinguen claramente (nunca se trata como "disponible" solo porque existe
"ollama.exe"):

| Estado | Condición | Acción |
|---|---|---|
| NotInstalled | No hay ejecutable `ollama` en PATH | Instalar automáticamente |
| InstalledServerDown | Hay ejecutable, el endpoint real no responde | Iniciar `ollama serve` y esperar |
| ServerAvailable | El endpoint `/api/version` responde | Reutilizar, continuar |

La comprobación válida de "server disponible" es el **endpoint local real**
(`http://127.0.0.1:11434/api/version`), el mismo que usa `OllamaClient`.

## INSTALACIÓN AUTOMÁTICA (OllamaAutoInstaller)

- Si Ollama no está instalado, Cóndor lo instala **automáticamente** desde la
  fuente oficial (`https://ollama.com/download/OllamaSetup.exe`), **sin
  confirmación interactiva de Cóndor**.
- Si Windows requiere elevación/UAC, el propio instalador de Windows solicita la
  autorización del sistema operativo. Eso NO es una confirmación funcional de
  Cóndor; es una autorización del SO y es aceptable.
- Cóndor espera la finalización, verifica que quedó instalado, verifica/inicia el
  server y continúa.
- Si la instalación no puede realizarse (permisos, conectividad, otra causa
  técnica), Cóndor produce un **error controlado** que explica al usuario qué
  ocurrió, sin stack traces.

## OWNERSHIP DE PROCESOS

- Ollama que **ya existía** antes de Cóndor → Cóndor lo reutiliza y NO lo cierra.
- Ollama que **Cóndor inició** → Cóndor lo registra (`StartedByCondor`).
- Cóndor NO ejecuta taskkill ni cierra arbitrariamente una instancia de Ollama
  preexistente. La liberación del modelo se hace vía `keep_alive=0` en la sesión
  (`LocalModelSession`), no cerrando procesos de Ollama/llama-server.

## TIMEOUT Y RECUPERACIÓN

- Cada etapa tiene timeout, estado visible (progreso), cancelación cooperativa y
  error controlado.
- Espera del server: reintentos acotados (por defecto ~40 s), comprobando el
  endpoint.
- Tras los intentos sin resultado, produce error controlado:
  `Ollama instalado: [OK] · Ollama Server: [ERROR]` con Motivo y opción de
  reintento; el detalle técnico queda en campos de diagnóstico (no en la UI).

## DEPENDENCIAS DE WINDOWS

Solo se preparan dependencias con necesidad técnica comprobable. Ollama se
distribuye autocontenido; NO se instala Visual C++ Redistributable porque no hay
binario nativo de Cóndor que dependa de él (no hay necesidad técnica comprobada).
No se instalan componentes de Windows indiscriminadamente.

## COMPONENTES

| Componente | Ruta | Rol |
|---|---|---|
| `OllamaHealthChecker` | Infrastructure/DependencyBootstrap | Distingue instado / proceso / server caído / server OK (endpoint real) |
| `OllamaProvisioner` | Infrastructure/DependencyBootstrap | Orquesta detect → instalar → arrancar → esperar → re-verificar → ownership |
| `OllamaAutoInstaller` | Infrastructure/DependencyBootstrap | Instalación automática desde fuente oficial (UAC del SO) |
| `OllamaServerLauncher` | Infrastructure/DependencyBootstrap | Inicia `ollama serve` y registra ownership |
| `OllamaProvisioningResult` | Core/Models | Estado + ownership + acción + diagnóstico |
| `DependencyBootstrapper` | Infrastructure/DependencyBootstrap | Abstracción detectar → preparar → verificar → continuar |
| `StartupStage.*` | Core/Models | Etapas de bootstrap en el progreso |

## INTEGRACIÓN

- `Program.cs` invoca `DependencyBootstrapper` al inicio de la sesión interactiva
  y en los flujos one-shot (slash / intención libre) que necesitan el proveedor,
  ANTES de preparar el modelo.
- Si el bootstrap no queda listo, se muestra un error controlado (no stack trace)
  y Cóndor termina; no espera indefinidamente.

## PRUEBAS

`DependencyBootstrapTests` (Integration, 9) cubren los escenarios A-H del detalle:
server disponible → continúa; server detenido → lo inicia y verifica; no instalado
→ instalación automática; instalado pero no inicia → timeout + error controlado;
ya existía → reutiliza y no cierra; Condor inició → registra propiedad; server deja
de responder → no bloquea; cancelación cooperativa.
