# REGISTRO_CAMBIOS

Version: 2.1.0
Fecha de continuidad: 2026-08-19

## Cambios recientes relevantes

### Presupuesto y recursos
Se implemento presupuesto seguro y clasificacion de estado de recursos.
Se verifico un escenario con 8,2 GB disponibles y 3,7 GB de presupuesto seguro.

### Auto-setup
Se implemento el tratamiento de inventario Ollama vacio y la preparacion de un modelo viable.
Se observo la descarga real de qwen2.5-coder:3b.

### Arranque honesto
El arranque fue ajustado para no declarar operatividad cuando no existe un modelo utilizable.

### Progreso
Se integro progreso de arranque y del agente.
En ejecucion real se observaron etapas como:
- Comprendiendo
- Observando / list_dir
- Finalizando

### Integracion CLI
Se integro el flujo de arranque, versionado y experiencia del agente.

## Incidencia abierta

RESUELTA: ver "INCIDENCIA: RAM fluctuante (resuelto)" al final de este archivo.
La contradiccion ("modelo listo" vs "no hay modelo compatible") era causada por la
RAM libre viva que fluctuaba bajo el presupuesto seguro, no por routing/intencion.

## Decision operativa

Suspender nuevas funcionalidades y concentrar el trabajo en esta causa raiz.

## Regla de cierre

No considerar estable el ciclo hasta reproducir y corregir la contradiccion con pruebas automatizadas y E2E.

---

## INCIDENCIA: RAM fluctuante (resuelto)

### Causa raiz identificada
El modelo qwen2.5-coder:3b no se perdia en routing ni por el texto de la tarea.
"hola" y una tarea de analisis recorren el MISMO camino:

AgentCommand -> AgentService.RunAsync -> ModelAutoSetupService.EnsureModelAsync
-> ModelSelector.RecommendFromCatalog -> OrderByCompatibility
-> ModelMemoryBudget.FitsInRamStrict.

EnsureModelAsync ignora la intencion (purpose siempre "agente"). La unica variable
decisoria es la instantanea de RAM viva (FreePhysicalMemory via CIM) de cada
invocacion. Cuando la RAM libre baja del umbral, incluso el modelo menor viable
(3B, pico ~2.16 GB; en 16 GB totales requiere libre >= ~6.66 GB) deja de cumplir
el presupuesto seguro -> desired == null -> AgentService mostraba el mensaje generico.

### Correccion (minima, un unico archivo de produccion afectado: AgentService.cs)
Cuando ModelAutoSetupService informa que el modelo esta bloqueado por recursos
(BlockedByResources) y no por ausencia, AgentService:
1. realiza una recuperacion ACOTADA (re-evalua la RAM viva un numero limitado de
   veces con un pequeno delay), aprovechando el modelo instalado si la RAM se libera;
2. si sigue bloqueado, comunica un bloqueo TEMPORAL por recursos con detalle
   (RAM libre, presupuesto seguro, consumidores de alto consumo) y deja explicito
   que NO es la ausencia de un modelo;
3. conserva la intencion de la tarea (Objective + Checkpoint) para no perderla;
4. no entra en bucles de reintento.

### Pruebas agregadas
- Tests/Unit/Condor.Core.Tests/ModelSelectorReproTests.cs (4 pruebas): seleccion
  con RAM suficiente, rechazo con RAM baja, independencia de la intencion, frontera 7.0/6.5 GB.
- Tests/Integration/Condor.Infrastructure.Tests/AgentServiceResourceBlockTests.cs (2 pruebas):
  mensaje honesto y tarea conservada; recuperacion acotada sin bucle infinito.

### Verificacion E2E real (Ollama local, qwen2.5-coder:3b instalado)
- (a) RAM suficiente -> tarea de analisis completada (success true, exit 0).
- (b) RAM insuficiente -> mensaje honesto de bloqueo temporal (no "no hay modelo"),
  tarea conservada, exit 1, termino en tiempo finito (sin bucle).
- (c) recuperacion posterior -> la misma tarea completada al liberarse RAM (exit 0).
- (d) tarea no perdida -> objective y checkpoint.task conservan la intencion.
