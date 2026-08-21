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

---

## CÓNDOR BUSCA UNA SALIDA VIABLE (alternativas menores en el catalogo)

### Que se encontro
El selector (ModelSelector) y el auto-setup ya consideraban modelos del catalogo NO
instalados y ya descargaban automaticamente un "desired" viable (ModelAutoSetupService /
PullAsync). No habia una falla de seleccion: el LIMITE real era que el catalogo de Condor
solo tenia modelos de 3B hacia arriba. Por eso, cuando la RAM no permitia cargar ni el
qwen2.5-coder:3b, el selector descartaba TODO y Condor pedia intervencion de inmediato,
sin tener ninguna alternativa menor que buscar/descargar.

### Correccion (minima, sin duplicar seleccion/descarga)
- ModelCatalog.cs: se anade una escalera de alternativas menores reales de Ollama aptas
  para tareas de agente, con Purpose="agente" y perfil de recursos verificado:
  - qwen2.5-coder:1.5b (~0.92 GB)   -> alternativa al 3B.
  - llama3.2:1b (~1.28 GB)          -> alternativa general menor.
  - qwen2.5-coder:0.5b (~0.37 GB)   -> ultimo recurso (ultima salida viable).
- Con esto, OrderByCompatibility (FitsInRamStrict intacto) ya evalúa de mayor a menor
  capacidad: si el 3B no cabe pero el 1.5B si, el 1.5B es el "desired" y el
  ModelAutoSetupService lo descarga y verifica automaticamente (infraestructura existente).

Flujo resultante (casos de aceptacion):
A) RAM suficiente + instalado -> se usa el instalado (3B).
B) Sin instalado + recursos -> se descarga el viable adecuado del catalogo.
C) Instalados no caben + alternativa menor viable -> se elige/descarga la menor y se usa.
D) Alternativa menor tampoco cabe -> sigue bajando en el catalogo (0.5B) si es viable.
E) Ninguna viable -> se informa honesto (no ausencia), se sugiere liberar RAM, se pregunta
   opcionalmente y se reevalua.
F) Condor nunca se detiene solo por no caber los instalados si aun hay alternativa.

### Pruebas
- Unit ModelSelectorTests: instalados(3B) no caben pero 1.5B del catalogo si -> Desired=1.5B,
  AlreadyInstalled=false (se descarga); caida a 0.5B con menor RAM; solo bloquea al agotar
  el catalogo (RAM extrema).
- Unit ModelSelectorReproTests: analisis con RAM baja -> alternativa 0.5B (no bloquea);
  frontera 3B degrada a 1.5B.
- Integration AgentServiceResourceBlockTests: los casos de bloqueo/intervencion ahora usan
  RAM que no permite NINGUNA alternativa (headroom 0), para que sigan cubriendo la
  intervencion S/N solo cuando el catalogo se agota.

### Verificacion E2E real (con recursos disponibles)
- RAM suficiente (7.8-7.9 GB, runner 7B descargado): tarea completa con qwen2.5-coder:3b
  (exit 0) -> caso A.
- RAM extrema (runner 7B cargado ~4.2 GB, libre 3.4-3.8): el selector agota el catalogo
  (ni el 0.5B cabe con headroom 0) y reporta honestamente "modelo instalado no se pudo
  cargar ahora / bloqueo temporal" sin afirmar ausencia -> caso E/F (agotado).
- El caso C intermedio (3B no cabe, 1.5B si) esta demostrado por los tests de seleccion
  con RAM controlada; en este entorno el runner de Ollama mantiene un working set fijo
  (~4.2 GB) que impide sostener la RAM en el rango intermedio para el E2E.

---

## ANALISIS Y ORQUESTACION DEL AGENTE (inventario + separacion HALLAZGOS/RESULTADO)

### Problema
El analisis entregado era superficial y la orquestacion estaba duplicada: [HALLAZGOS] y
[RESULTADO] mostraban el MISMO texto (el `reason` del modelo), sin inventario del entorno,
sin motivo de seleccion de modelo ni capacidades verificadas.

### Correcion (no altera seleccion, presupuesto, routing ni la estructura general de la interfaz)
- AgentModel.cs: nuevo `AgentInventory` (RAM total/libre, presupuesto seguro, presion, CPU,
  disco libre, modelos instalados, modelo seleccionado + motivo, capacidades verificadas).
  Se anade como campo opcional a `AgentResult`.
- AgentService.cs: `BuildInventoryAsync` recopila el inventario real (detectores Cpu/Storage/
  RAM, inventario de Ollama via /api/tags y capacidades del catalogo del modelo elegido)
  y lo adjunta a los resultados exitosos del agente. Solo datos reales; nunca inventa.
- AgentRenderer.cs:
  * [INVENTARIO] nuevo: recursos, CPU, disco, modelos instalados, modelo + motivo, capacidades.
  * [HALLAZGOS] ahora es EVIDENCIA objetiva observada (archivos inspeccionados con su
    naturaleza), derivada de los pasos reales de herramienta, NO la sintesis del modelo.
  * [RESULTADO] sigue siendo el analisis elaborado (`reason` del modelo). Asi HALLAZGOS y
    RESULTADO provienen del flujo correspondiente y ya no se duplican.
- Mejora moderada del prompt (sin inyectar el inventario al modelo): se pide un analisis util
  y focalizado (leer el contenido relevante, atender el archivo que la tarea mencione) sin
  romper el JSON del modelo local. Se descarto inyectar el inventario completo en el system
  prompt porque degradaba la fiabilidad del qwen2.5-coder:3b (JSON invalido); el inventario
  queda disponible y mostrado por Condor, orientando la decision de modelo aparte.

### Pruebas
- AgentRendererTests (2 nuevas): [HALLAZGOS] es evidencia distinta de [RESULTADO] (la sintesis
  aparece una sola vez); [INVENTARIO] se presenta cuando existe (modelo, capacidades, recursos).

### Verificacion E2E real (Ollama local)
- Tarea "cuentame que es esta aplicacion" sobre un proyecto .NET real (exit 0): la salida
  presentaba el inventario (RAM/CPU/disco/modelos, modelo con motivo y capacidades) y el
  analisis elaborado distinto de la evidencia observada.
- Indicador: se confirmo que no existe un caracter '|' junto al indicador de procesamiento;
  el indicador es el spinner circular (◐◓◑◒) en terminal interactiva y '·' en salida
  redirigida, seguido del texto de estado. No se introduce ni elimina simbolo extra.

---

## ADN CONVERSACIONAL Y GENERALIZACION DEL ANALISIS

### Cambios
- AgentRenderer.cs: la respuesta final es ahora UNA CONVERSACION NATURAL. Se eliminan los
  bloques tecnicos obligatorios ([PROGRESO]/[ANALISIS]/[HALLAZGOS]/[VERIFICACION]/[RESULTADO]/
  [INVENTARIO]/[CAMBIOS]). Se presenta en prosa: tarea, contexto breve del entorno, "Revisando:
  <archivos>", el analisis elaborado, "Modifique: <archivos>" si hubo cambios y la firmita final.
- AgentRenderer.cs: firma permanente del ADN de Condor al final de cada respuesta:
  "©Condor · <modelo> · <tiempo>" (segundos con 1 decimal, o milisegundos si es muy corto).
  El tiempo se mide en AgentCommand (Stopwatch) y se pasa al renderer.
- AgentService.cs: el prompt del sistema es ahora agnóstico de ecosistema: no se asume .NET;
  solo se usan build/test si el ecosistema detectado lo permite y la tarea lo requiere.
  El inventario no se inyecta al modelo (seguia degradando la fiabilidad del 3B).
- Se mantienen intactos: seleccion de modelos, presupuestos, escalera de alternativas,
  recuperacion de RAM e interfaz estructural. No se creo un sistema paralelo.

### Prueba liviana (E2E real, proyecto HTML, sin .NET, exit 0)
A) "Revisa y me cuentas qué tenemos aquí." -> respuesta natural: 'El directorio actual
   contiene tres archivos: app.js, estilos.css y index.html.' + firma
   '©Condor · qwen2.5-coder:3b · 93,4 s'. Sin etiquetas tecnicas; sin validacion .NET.
B) "Qué contiene index.html, ¿de qué trata la página?" -> analiza el CONTENIDO (no solo lista):
   'El archivo index.html contiene una pagina web basica con un titulo, un parrafo y un boton.
   La pagina parece ser una tienda virtual o una landing page.' + firma
   '©Condor · qwen2.5-coder:3b · 85,6 s'.
Se comprueba: identificacion de archivos, reconocimiento de tipos, analisis real del contenido,
ausencia de validacion .NET, respuesta natural, ausencia de etiquetas tecnicas obligatorias,
y modelo + tiempo al final.

---

## IDENTIDAD PERMANENTE Y DIFERENCIACION DEL ORIGEN (interfaz MVP - test liviano)

### Cambios
- Identidad permanente: la ventana muestra "©Condor" + "Observa · Comprende · Planifica ·
  Construye · Verifica" desde el inicio (banner de arranque) y durante el procesamiento de
  cada tarea (cabecera del presentador del agente), y en la respuesta (cabecera del
  renderer). No se pierde al iniciar/terminar tareas, errores o cambios de modelo.
- Modelo real visible: junto a la identidad se muestra el modelo LOCALMENTE UTILIZADO
  ("©Condor - qwen2.5-coder:3b"), tomado de result.Model (el realmente usado, nunca uno
  "sugerido"). El pie de respuesta conserva "©Condor · <modelo real> · <tiempo>".
- Diferenciacion visual del origen en el renderer de la respuesta: Cóndor en azul
  (Terminal.WriteBlue), el analisis producido por el modelo en gris (WriteDim), error real
  en rojo, advertencia en amarillo (WriteWarning); el texto de usuario usa el color por
  defecto. Sin prefijos "c:/q:/l:/d:" en la conversacion; la separacion es visual.
- Se conserva internamente el origen del analisis (el `reason` del modelo) para trazabilidad.
- Se elimino de la experiencia cualquier mensaje que tratara .NET como requisito universal:
  el toolset ya no emite "No se encontro manifiesto .NET." sino un mensaje general sobre
  sistema de build.
- Se confirmo que no existe un caracter '|' junto al indicador de procesamiento (el
  indicador es el spinner circular en interactivo y '·' en redirigido); no se introdujo ni
  quito simbolo extra.

### Pruebas livianas (E2E real)
- Arranque: muestra "©Condor" + eslogan desde el inicio (exit 0).
- Interaccion simple "cuentame que hay aqui": la respuesta final muestra la identidad
  "©Condor - qwen2.5-coder:3b" + eslogan, analisis (gris/rojo segun corresponda) y el pie
  "©Condor · qwen2.5-coder:3b · <tiempo>". Sin referencias .NET ni '|'.
- Tests de interfaz/progreso/startup: 45/45 verdes (renderer, ambos presentadores y preparador).
- Suites completas: 548 pruebas, 0 fallos; build Release 0 warnings/errors.

---

## IDENTIDAD PERMANENTE COMO ZONA DE LA INTERFAZ (correccion puntual)

### Cambio
- La identidad de Condor se trata ahora como una ZONA PERSISTENTE de la interfaz principal
  interactiva, no como texto que se imprime solo en determinados momentos:
  - IdentityHeader.cs (nuevo): zona con "©Condor - <modelo real>" + eslogan + separador;
    la primera linea muestra el modelo local REAL utilizado en ese momento.
  - Interpreter.cs: nueva dependencia opcional `onBeforePrompt` que re-dibuja la zona de
    identidad antes de CADA espera de entrada (>>>>), de modo que no desaparezca por el
    desplazamiento de la terminal y no se superponga con el prompt/estado/respuesta.
  - Program.cs: conecta el `onBeforePrompt` con `IdentityHeader.Render(prep.Model)` en la
    sesion interactiva (modelo real obtenido de la preparacion).
- Se conservan intactas: seleccion dinamica de modelos, presupuesto/RAM, escalera, cambio
  de modelo, adaptacion al modelo, estrategia de Ollama y seleccion de familias (Prompta 2).
- No se eliminaron ni reemplazaron funcionalidades; cambio minimo de interfaz.

### Prueba liviana
- InterpreterTests (nuevo): la zona de identidad se reinpinta antes de cada espera de entrada.
- Verificacion E2E (real, exit 0 / incluye error de modelo local no atribuible): la identidad
  con el modelo real permanece visible en inicio, procesamiento, respuesta y errores/finalizacion,
  sin solapamientos.
- Suites completas: 549 pruebas, 0 fallos; build Release 0 warnings/errors.

---

## CICLO DE VIDA DEL PROVEEDOR LOCAL (resolver duplicados / orfanos / RAM retenida)

### Diagnostico (basado en evidencia, sin asumir)
- Condor NO gestiona el proceso llama-server.exe: es cliente HTTP de Ollama
  (127.0.0.1:11434) vía `/api/chat`, `/api/tags`, `/api/version`, `/api/pull`.
  El ownership de llama-server es de Ollama (lo lanza su servidor).
- Cada servicio (Vision, Agent, Ask) y cada deteccion instanciaba su propio
  `OllamaClient`/`HttpClient`, y no existia una sesion/proveedor unico.
- No habia shutdown unico: `/salir`/EOF hacian `return 0` sin liberar el modelo;
  Ollama retiene el modelo en RAM (keep_alive por defecto), de ahi que
  "llama-server siga vivo / RAM retenida" tras terminar Condor.
- No se matan procesos: Condor no es dueno externo; no se usa taskkill.

### Correccion (motor de ejecucion, sin tocar la TUI)
- `ILlmProviderLifecycle.cs` (nuevo): contrato de ciclo de vida de la sesion
  (ProviderName, ActiveModel, EnsureAvailableAsync, ReleaseAsync).
- `LocalModelSession.cs` (nuevo): sesion unica y reutilizable por ejecucion.
  Centraliza un unico HttpClient y el modelo activo. `EnsureAvailableAsync`
  deduplica (si la sesion ya esta activa para el MISMO modelo y el proveedor
  responde, se reutiliza sin crear nada). `ReleaseAsync` es idempotente y libera
  el modelo mediante el mecanismo oficial de Ollama (`keep_alive=0`).
- `OllamaClient.cs`: timeout publico y `ReleaseModelAsync` (`POST /api/generate`
  con `keep_alive=0`) para descargar el modelo de RAM sin matar procesos.
- `OllamaModelOperator.cs` y `ModelAutoSetupService.cs`: comparten el HttpClient
  de la sesion (no se crean conectores duplicados por tarea/retry).
- `AgentService.cs` y `VisionService.cs`: aceptan la sesion compartida; el agente
  registra el modelo activo antes de inferir.
- `Program.cs`: crea UNA `LocalModelSession`, la inyecta en todos los flujos y
  envuelve `Main` en `try/finally` para liberar la sesion en el shutdown unico
  (normal, error o cancelacion). Ctrl+C tambien libera la sesion antes de salir.

### Control de procesos y deduplicacion
- Una solicitud NO crea otra instancia si ya existe una sesion compatible
  (mismo modelo, proveedor disponible): se reutiliza.
- Un fallo del proveedor se diagnostica y se reporta de forma honesta; un retry
  NO crea una cascada de instancias. La liberacion solo ocurre al liberar la
  sesion (shutdown), no entre requests.
- Queda liberada la RAM del modelo al terminar Condor sin dejar procesos propios
  huerfanos ni depender de matar infraestructura externa de Ollama.

### Pruebas
- `LocalModelSessionLifecycleTests.cs` (nuevo, 13 pruebas): inicializacion unica,
  dos solicitudes consecutivas comparten sesion, fallo de proveedor sin cascada,
  retry sin duplicar instancia, timeout, cancelacion, cierre normal/anormal
  (finally), reutilizacion de sesion activa, cambio de modelo, liberacion
  idempotente y sin modelo. Todas con HttpClient/handler simulados.
- Suites completas: Core 242/242, Architecture 22/22, Integration 297/299.
  Las 2 rechazadas (`CompleteAsync_ModeloInexistente...` y
  `EnsureModel_ModeloDeseadoInstalado_ReutilizaSinPull`) son pruebas que requieren
  un Ollama REAL en 127.0.0.1:11434 (fallan igual en el commit base; no son
  regresion de este cambio).
- `Tests/Functional/condor-lifecycle.func.ps1` (nuevo): prueba funcional REAL
  contra Ollama vivo (requiere servidor y modelo): varias solicitudes one-shot,
  fallo de proveedor, y comprobacion de ausencia de procesos propios huerfanos y
  de liberacion correcta. La TUI no se modifica.

---

## HARNESS DE PRESUPUESTO DINAMICO Y SELECCION INTELIGENTE DE MODELOS

### Objetivo
Convertir el presupuesto de RAM en un HARNESS: "RAM como STOCK + RESERVA +
presupuesto dinamico + 1-/1+ + seleccion por tarea". Condor ya no pregunta cual es
el modelo mas grande que cabe, sino el mas pequeno que sea SUFICIENTE para la
tarea, EFICIENTE y SEGURO dentro del presupuesto, conservando una reserva
operativa.

### Correccion (motor de ejecucion, sin tocar la TUI)
- `BudgetPolicy` (Core/Evaluation, nuevo): politica configurable de reservas y
  formula documentada:
  `presupuesto_real = RAM_libre - reservaSistema - reservaCondor - reservaOperativa - margenEstabilidad`.
  La reserva operativa (max(absoluto, RAM_libre*ratio)) nunca se presta al modelo;
  el presupuesto es >=0 y nunca supera la RAM libre real.
- `BudgetAssessment` (Core/Models, nuevo): veredicto auditable de stock/presupuesto/reserva.
- `TaskModelRequirement` + `TaskIntentClassifier` (nuevos): traduce la tarea a las
  capacidades requeridas (puro, sin IO, sin preferir familia).
- `ModelEfficiencyEvaluator` (nuevo): suficiencia funcional + eficiencia + deja margen.
- `ModelSelector.SelectForTask` (nuevo, puro): seleccion por tarea + 1- + 1+ +
  modelo instalado del usuario como candidato + insuficientes.
- `BudgetReevaluator` (nuevo): reevaluacion periodica (30 min configurable) en
  punto seguro con limite (sin loops); decisiones Keep/Upgrade/Downgrade con motivo.
- `ModelPromptBuilder` (Infrastructure, nuevo): adapta el prompt al modelo (JSON si
  soporta estructura, tool-use, multi-archivo, modelo en uso).
- `ModelAutoSetupService.EnsureModelForRequirementAsync` (nuevo): orquesta la
  seleccion/descarga por tarea con el harness. Se conserva `EnsureModelAsync`
  (seleccion clasica) para el arranque (sin regresion).
- `AgentService` (Infrastructure/Agent): clasifica la tarea, usa el harness,
  adapta el prompt, y reevalua el presupuesto en puntos seguros entre inferencias
  (cambia de modelo de forma acotada: libera el anterior y registra el nuevo en la
  sesion, sin duplicar runners).
- `AgentInventory` ganó presupuesto/reserva/operativa/1-/1+ (inventario razonado).

### Decisiones de diseno (regla de no destruccion)
- Se conservo la seleccion clasica (`RecommendFromCatalog`) para arranque y tests.
- El harness se acopla al que agregue `SelectForTask` para el flujo del agente;
  no se borro funcionalidad existente, se anadio una via por-tarea.
- La TUI/presentacion no se modifico.

### E2E real (Ollama v0.31.1, RAM libre ~6 GB)
El harness en ejecucion real:
- Calculo stock->reserva->presupuesto (6,5 GB libres -> ~2,0 GB de presupuesto).
- Rechazo cargar un modelo que agotaria el margen operativo (bloqueo TEMPORAL
  honesto, no "ausencia de modelo"), conservando la tarea.
- Refuso usar un modelo "pequeno que cabe" pero insuficiente para la tarea de
  agente (exige tool-use + coding); protege la reserva antes de presupuesto~0.
- Tras liberar un runner retenido en Ollama (keep_alive=0), la RAM subio y el
  harness reevalua (ver limite: la politica por defecto es conservadora; equipos
  con poca RAM libre pueden reportar bloqueo aun con modelo pequeno viable).

### Pruebas
- `HarnessBudgetTests` (Unit Core, 17): reserva minima, presupuesto, modelo grande
  descartado sin margen, eficiencia, seleccion por tarea, familias, modelo
  instalado del usuario, 1-/1+, subida/bajada de RAM, reevaluacion, continuidad,
  modelo insuficiente descartado, ausencia de loops.
- `ModelPromptBuilderTests` (Integration, 4): adaptacion del prompt por modelo.
- Suites completas: Architecture 22/22, Core 259/259, Integration 304/304.
- Build Release 0 warnings/errores. La TUI no se modifica.

### Anomalia registrada (limitacion de entorno, honesta)
La politica por defecto es conservadora a proposito. En maquinas con poca RAM
libre, el harness reporta bloqueo TEMPORAL en lugar de cargar un modelo pequeno
que "quepar numericamente", porque proteger la reserva es prioridad. `BudgetPolicy`
es configurable para ajustar el balance en despliegue.

---

## BOOTSTRAP DE DEPENDENCIAS — OLLAMA (puesta en marcha automatica)

### Problema
El usuario podia tener Ollama instalado e incluso la app abierta, pero el Ollama
Server no disponible; Condor podia quedarse esperando sin informar claramente. El
usuario no debia conocer "ollama serve", puertos ni procesos.

### Correccion (Infrastructure, nuevo namespace `DependencyBootstrap`; la TUI no se modifica)
- `OllamaHealthChecker`: distingue 4 estados reales (no-instalado / instalado /
  server caido / server OK) usando SIEMPRE el endpoint real
  (`/api/version` sobre 127.0.0.1:11434); nunca se trata como disponible solo por
  existir "ollama.exe".
- `OllamaProvisioner`: detecta -> instala (automatico) -> arranca el server ->
  espera con timeout/reintentos -> re-verifica el endpoint -> registra ownership.
- `OllamaAutoInstaller`: instalacion AUTOMATICA desde la fuente oficial
  (`OllamaSetup.exe`), sin confirmacion de Condor; el UAC de Windows es una
  autorizacion del SO y es aceptable. Instalacion correcta + reintentos acotados.
- `OllamaServerLauncher`: inicia `ollama serve` y registra quien lo inicio.
- `DependencyBootstrapper`: abstraccion detectar -> preparar -> verificar ->
  continuar por dependencias (hoy Ollama).
- `Program.cs`: invoca el bootstrap al inicio de la sesion interactiva y en los
  flujos one-shot que necesitan el proveedor, ANTES de preparar modelo. En fallo
  muestra error controlado (sin stack trace).
- `StartupStage`: nuevas etapas de bootstrap (BootstrappingDependencies,
  InstallingOllama, StartingOllamaServer, VerifyingOllamaServer) con sus etiquetas
  de progreso (aditivas, sin alterar estetica existente).

### Ownership
- Ollama preexistente -> se reutiliza y NO se cierra.
- Ollama iniciado por Condor -> `StartedByCondor` (registrado).
- Condor NO usa taskkill ni cierra Ollama ajeno; libera el modelo via keep_alive=0
  en la sesion.

### Dependencias de Windows
No hay binario nativo propio que dependa de VC++; no se instala Visual C++
Redistributable (sin necesidad tecnica comprobable). No se toca `LocalModelSession`
ni `AgentService` para este cambio (solo wiring de arranque).

### Pruebas
- `DependencyBootstrapTests` (Integration, 9): A-H (server disponible; server
  detenido->inicia; no instalado->instala; no puede iniciar->timeout/error;
  ya existia->reutiliza y no cierra; Condor inicio->registra propiedad; server deja
  de responder->no bloquea; cancelacion cooperativa). Mas regresion instalado vs
  server disponible.
- Suites completas: Architecture 22/22, Core 259/259, Integration 313/313.
- Build Release 0 warnings/errores.

### Validacion real (Windows, Ollama v0.31.1)
- Con server disponible: `condor /contexto` continua de inmediato (reutiliza).
- Con server detenido (simulando "app abierta pero server caido"): Cóndor detecta
  el estado, inicia `ollama serve`, verifica el endpoint real y continua al flujo
  normal; el endpoint vuelve a responder (0.31.1) y el ownership es StartedByCondor.
- La liberacion del modelo retenido permanece por keep_alive=0 (sin matar procesos).
- NO se hizo commit/push (requiere autorizacion explicita).

---

## T-017 — TUI OPERACIONAL Y FLUJO HONESTO (sin "Verificando" ambiguo)

### Problema
- El estado generico "Verificando" ocultaba el bloqueo real por RAM; tras el
  bloqueo se preguntaba "[S/N] liberar memoria" y Ctrl+C producía una
  OperationCanceledException visible (excepción técnica al usuario).
- Dos presentadores ("StartupProgressPresenter", "AgentProgressPresenter")
  redibujaban bloques multi-línea con aritmética de cursor ANSI independiente:
  peleaban por el cursor y la salida quedaba rota.
- El catálogo concentraba el segmento pequeño en una sola familia (qwen2).

### Correccion
- `AgentService`: fallo RÁPIDO y controlado ante presupuesto de RAM insuficiente.
  Se elimina el bucle de re-evaluación oculto y el confirmador de liberación de
  memoria como mecanismo normal (O1). Nueva pantalla honesta "MODELO NO
  EJECUTABLE": Modelo / RAM requerida estimada / RAM disponible / Motivo /
  Presupuesto permitido / Consumidores informativos / Acción. Sin stack traces.
  La tarea se conserva (Objective + Checkpoint).
- Eliminados: `IUserConfirmation`, `ConsoleRamConfirmation` y su wiring en
  `Program.cs` (`PromptIfInteractive`). Ya no se pregunta S/N por RAM jamás.
- `ModelSelector.SelectForTask`: expone `MinimumViable` (candidato mínimo
  suficiente que no cabe) para informar la RAM requerida real del bloqueo.
- `TuiScreen` (nuevo): autoridad ÚNICA de renderizado en esperas. Una sola línea
  de estado reescrita en su sitio + zona de actividad persistente (las etapas
  concluidas se archivan y quedan en el scroll). Mecánica sin cálculos de
  altura ni borrados de bloque: elimina los conflictos de cursor.
- Presentadores migrados a `TuiScreen`. Estados reales por etapa con etiqueta
  operacional: [ENTORNO]/[MEMORIA]/[OLLAMA]/[MODELO]/[VERIFICACION]/[DECISION]
  en arranque y [SOLICITUD]/[AGENTE]/[VERIFICACION]/[RESPUESTA] en agente. Si hay
  mensaje, SIEMPRE se muestra (nunca una fase genérica sin detalle). Salida
  redirigida: líneas compactas deduplicadas (E2E estable).
- `ModelKardex` (nuevo): kardex local de modelos (`kardex_modelos.json`) junto
  al estado; registra Instalado / RechazadoPorPresupuesto / FalloObtencion con
  fecha y motivo. El inventario vivo de Ollama sigue mandando; el kardex es
  historial para diagnóstico. Enganchado en ambas rutas de selección.
- Descargas: solo se obtienen modelos admitidos por el presupuesto vigente
  (selección clásica con FitsInRamStrict + harness con margen 1−); nunca se
  auto-descarga un modelo que el presupuesto determine no ejecutable.
- `ModelCatalog`: diversidad de familias en el segmento <=1.5B con `gemma3:1b`
  (~0.8 GB, familia gemma3) junto a qwen2.5-coder:0.5b/1.5b y llama3.2:1b.

### O4 (bootstrap Ollama)
Sin regresiones: detección real del server (/api/version), instalación/arranco
automático, ownership, reutilización sin segundo server y liberación keep_alive=0
permanecen intactos (ningún archivo de DependencyBootstrap modificado).

### Pruebas
- `AgentServiceResourceBlockTests` reescrito al nuevo contrato: fallo rápido con
  pantalla MODELO NO EJECUTABLE (datos concretos, sin "liberar memoria"), acotado
  (<=4 evaluaciones) y compatible-no-disponible sin descargas fuera de presupuesto.
- Suites completas: Architecture 22/22, Core 259/259, Integration 307/307
  (3 pruebas nuevas sustituyen a las del confirmador eliminado).
- Build Cli/Core/Infrastructure: 0 warnings, 0 errores. Árbol del commit
  verificado compilable en aislamiento (stash --keep-index + build).
