# ESTADO_DESARROLLO

Version: 2.1.0
Estado: Activo
Modo: Evolucion Continua
MVP: Condor 1.0
Fecha: 2026-08-19

## CAPACIDADES VERIFICADAS

- Ejecucion local en Windows.
- Integracion con Ollama.
- Deteccion de modelos.
- Auto-preparacion/descarga en escenario sin modelo.
- Evaluacion de presupuesto seguro.
- Seleccion/fallback de modelos en parte del flujo.
- Progreso visual durante tareas.
- Ejecucion de herramientas como list_dir.
- Resultado estructurado del ciclo.

## PRUEBA ACTUAL

Modelo:
qwen2.5-coder:3b

Presupuesto observado:
8,2 GB disponibles / 3,7 GB seguros / Normal.

Resultado tras la correccion:
- "hola" funciona.
- Cuando la RAM libre es suficiente, las tareas de analisis/lectura llegan al modelo.
- Cuando la RAM libre cae bajo el presupuesto seguro, Condor informa un bloqueo
  TEMPORAL por recursos (no "no hay modelo compatible"), conserva la tarea y la
  completa en cuanto la RAM se libera.
- Al INICIO, si hay modelos instalados (qwen2.5-coder:3b/7b) aunque la RAM no alcance
  el presupuesto seguro, la sesion ARRANCA igual (no se bloquea): se explica la RAM,
  Condor decide el modelo en cada tarea y lo recupera de forma acotada al liberarse
  memoria; cerrar aplicaciones es una sugerencia opcional, no obligatoria.
- Si en plena tarea no hay ningun modelo viable con la RAM disponible, Condor NO se
  cierra: informa "RAM insuficiente", sugiere liberar memoria (Opcion S/N) y, si el
  usuario confirma, reevalua y continua; si no, sale limpio conservando la tarea.
- Durante TODO el inicio hay indicador visual activo y mensajes de estado (banner,
  spinner y etapas: revisar recursos/RAM, evaluar modelos, seleccionar/preparar/verificar
  modelo, hasta "entorno listo"); la pantalla nunca queda aparentemente congelada.

## DIAGNOSTICO ACTIVO

Resuelto. La causa raiz era la RAM libre viva (FreePhysicalMemory via CIM) que
fluctua por invocacion y, al caer bajo el presupuesto seguro, hacia que incluso el
modelo menor viable (qwen2.5-coder:3b) fuera rechazado por FitsInRamStrict. No era
routing, ni intencion, ni ausencia del modelo. Corregido en AgentService con una
recuperacion acotada y un fallo honesto diferenciado que preserva la tarea.

## TEST DE ACEPTACION INMEDIATO

A. Sin modelo local:
- Condor detecta ausencia.
- Calcula presupuesto.
- Descarga modelo viable.
- Verifica instalacion.
- Continua al ciclo.

B. Con qwen2.5-coder:3b:
- "hola" funciona.
- "que modelo eres?" funciona o produce un resultado coherente.
- una tarea de lectura/análisis de archivos llega a la ejecucion del modelo.
- con RAM insuficiente NO aparece falsamente "No hay un modelo compatible": aparece
  un bloqueo temporal de recursos que conserva la tarea.

C. Progreso:
- muestra etapas reales.
- no es solamente decorativo.
- refleja iteraciones/acciones reales.

D. Presupuesto:
- el modelo elegido debe respetar el presupuesto.
- no debe descargar un modelo inviable.
