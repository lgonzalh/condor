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

---

## PROMESA FUNDAMENTAL: Condor no se bloquea al inicio si hay modelos instalados

### Problema
Aunque se corrigió el fallo del AgentService ante RAM fluctuante, el flujo de INICIO
todavia bloqueaba la sesion cuando no quedaba un modelo verificable-ahora: si la RAM
libre no alcanzaba el presupuesto seguro, StartupPreparer devolvia `Ready=false` y
Program.cs mostraba "Condor no puede iniciar" aun con qwen2.5-coder:3b/7b instalados.
Eso violaba la promesa: "Hay modelos instalados pero no puedo usarlos -> no inicio".

### Correcion (minima, dos archivos de produccion)
- StartupPreparer.cs: al no quedar un modelo "listo" ahora, condor distingue si el
  inventario REAL de Ollama (/api/tags) contiene al menos un modelo instalado.
  * Si HAY modelos instalados -> `Ready=true` (la sesion arranca), `NeedsIntervention=true`
    con un motivo honesto: RAM momentaneamente insuficiente, se decide el modelo en
    cada tarea y se recupera de forma acotada al liberarse memoria. Cerrar apps es
    una sugerencia OPCIONAL, no obligatoria.
  * Si NO hay ningun modelo instalado -> se mantiene `Ready=false` (sin capacidad
    operativa no se entra a la sesion en silencio).
- Program.cs: RenderWelcome evita duplicar el aviso de intervencion (una sola
  advertencia clara). El gate de inicio (`!prep.Ready`) solo bloquea cuando
  realmente no hay ningun modelo instalado/obtenible.

### Presupuestos
La decision autonoma conserva las reglas de presupuesto seguro (FitsInRamStrict no
se relaja): se reutiliza el modelo instalado si cabe, se descarga si falta, se elige
el modelo menor viable con RAM limitada, y si nada cabe se explica y se sugiere
liberar memoria de forma opcional sin bloquear.

### Prueba agregada
- Tests/Integration/Condor.Infrastructure.Tests/StartupPreparerTests.cs:
  RunAsync_ModelosInstaladosPeroRamaBaja_ArrancaSesionSinBloquear (la sesion arranca
  con modelos instalados pese a RAM baja, con advertencia honesta y sin afirmar modelo listo).

### Verificacion E2E real (Ollama local, qwen2.5-coder:3b y 7b instalados)
- Inicio sin argumentos con RAM baja (5,6-5,9 GB): EXITCODE=0, la sesion arranca, se
  muestran la etapa de arranque y la advertencia honesta; no se bloquea.
- One-shot con RAM baja: fallo honesto temporal, tarea conservada (exit 1).
- One-shot con RAM suficiente (6,9 GB): la misma tarea completada con
  qwen2.5-coder:3b (exit 0) -> recuperacion posterior demostrada.

---

## INTERVENCION OPCIONAL DE RAM (promesa de Condor)

### Problema cubierto
Cuando Condor ya inicio, hay modelos instalados, y tras evaluar/re-evaluar recursos NO
existe ningun modelo que pueda ejecutarse con la RAM disponible, Condor NO debe terminar
con exit 1 ni cerrarse: debe informar, sugerir liberar memoria de forma OPCIONAL y, si
el usuario confirma, volver a evaluar y continuar.

### Correcion (minima)
- Src/Condor.Core/Contracts/IUserConfirmation.cs (nuevo): contrato de confirmacion
  interactiva opcional (`AskToReleaseRamAsync`). Cóndor NUNCA cierra aplicaciones por
  su cuenta.
- Src/Condor.Cli/Presentation/ConsoleRamConfirmation.cs (nuevo): confirmador de consola
  que lee [S/N], con reintentos acotados (sin bucle infinito). Solo en consola real.
- Src/Condor.Infrastructure/Agent/AgentService.cs: tras la recuperacion acotada, si no
  queda modelo viable, si hay confirmador pregunta [S/N]; SI -> re-evalua UNA vez mas y,
  si ahora hay modelo viable, continua automaticamente; NO -> salida limpia conservando
  la tarea (Objective + Checkpoint). Sin confirmador, el comportamiento por defecto es
  la salida limpia honesta actual.
- Src/Condor.Cli/Program.cs: conecta el confirmador solo en consola interactiva (no JSON,
  no entrada redirigida), via PromptIfInteractive.

### Presupuestos
FitsInRamStrict y los presupuestos de seguridad permanecen intactos. No hay bucles
infinitos ni reintentos automaticos ilimitados. No hay APIs cloud.

### Pruebas agregadas
- AgentServiceResourceBlockTests (3): NO -> sale limpio y conserva la tarea; SI -> re-evalua
  una vez mas y (se sigue RAM baja) sale acotado sin bucle; SI + RAM liberada -> re-evalua y
  continua (no aparece el motivo de RAM).
- ConsoleRamConfirmationTests (4): S -> true; N -> false; s minuscula -> true; sin respuesta
  valida tras reintentos acotados -> false.

### Verificacion E2E real (recursos disponibles, sin forzar liberar memoria)
- One-shot con RAM baja (2,3-2,4 GB): mensaje honesto "bloqueo temporal por recursos",
  tarea conservada, exit 1, sin bucle. En --json no pregunta (no contamina la salida).
- La interaccion [S/N] queda demostrada por ConsoleRamConfirmationTests y por los tests de
  integracion de AgentService (SI re-evalua y continua; NO sale limpio).
- La ejecucion exitosa con RAM suficiente se demostro previamente (6,9 GB, qwen2.5-coder:3b).

---

## PROGRESO VISIBLE OBLIGATORIO DURANTE TODO EL INICIO

### Problema
Al ejecutar `condor` la terminal mostraba solo el banner ("CONDOR / Observa·Comprende...")
y quedaba visualmente en negro durante la preparacion: entre el banner y la primera etapa
reportada, o entre etapas, no habia ninguna linea de estado activa (el spinner no se
mostraba porque no habia una etapa "en curso").

### Correcion (minima, reutiliza el presentador de progreso existente)
- StartupProgressPresenter.cs: en Start() se inicia una etapa en curso
  (PreparingEnvironment) para que el spinner sea visible desde el primer instante; al
  completar una etapa se mantiene una etapa en curso (en vez de ponerse en null) para que
  el indicador nunca desaparezca mientras Condor sigue trabajando (solo se sustituye al
  llegar la siguiente etapa o al detenerse con Stop). No se crea una barra nueva ni un
  sistema paralelo.
- StartupPreparer.cs: se reportan sempre las etapas "Revisando recursos" (RAM libre) y
  "Evaluando modelos disponibles" en todos los caminos (con o sin assessment previo), de
  modo que el flujo emite actividad real desde el inicio.

El flujo visible queda: Preparando entorno -> Revisando recursos -> ✓ Recursos detectados
-> Evaluando modelos -> ✓ Modelos evaluados -> Seleccionando/Preparando/Verificando modelo ->
✓ Entorno preparado -> Entorno listo. No se simula progreso falso; cada etapa es real.

### Pruebas agregadas
- StartupPreparerTests.RunAsync_ConModeloInstalado_ReportaEtapasDeProgresoVisibles:
  con modelo instalado reporta ReviewingResources y EvaluatingModels (actividad visible).

### Verificacion E2E real (recursos disponibles, sin forzar liberar memoria)
- `condor` sin argumentos: la terminal emite secuencialmente "Preparando entorno...",
  "Revisando recursos...", "✓ Recursos detectados", "Evaluando modelos...",
  "✓ Modelos evaluados", "Preparando entorno..." (con Tiempo 00:19 indicando actividad
  prolongada y spinner), "✓ Entorno preparado", "Entorno listo..." y luego la advertencia.
  No queda pantalla en negro.
- One-shot --json conserva su salida JSON (el inicio no usa el presentador de arranque).
