# ESTADO_PROYECTO

Version: 2.1.0
Estado: Vigente
Clasificacion: Estado del Proyecto
Fecha: 2026-08-19

## FUENTE OFICIAL

Actualmente no existe un nivel estructural activo.
Condor opera en Evolucion Continua.

## RESUMEN

Proyecto: Condor
MVP Condor 1.0: completado en la linea documental T-001..T-012.
T-013: completada/congelada.
T-014: integracion posterior del ciclo.
La evolucion actual corresponde a estabilizacion y verificacion del producto real.

## ESTADO REAL OBSERVADO

La aplicacion compila y el CLI arranca.

Se verifico:
- deteccion/preparacion de entorno;
- descarga automatica de qwen2.5-coder:3b en un escenario sin modelo;
- calculo de presupuesto seguro;
- mensajes de progreso durante una tarea;
- ejecucion de al menos una tarea sencilla ("hola").

Problema actual:
- Condor puede informar que qwen2.5-coder:3b esta listo;
- sin embargo, determinadas tareas terminan con "No hay un modelo compatible disponible para la tarea.";
- esto contradice el estado de modelo listo y debe resolverse antes de declarar estable el ciclo de agente.

## EVIDENCIA RECIENTE

Hardware/ejecucion observada:
- RAM disponible mostrada por Condor: 8,2 GB.
- Presupuesto seguro mostrado: 3,7 GB.
- Estado: Normal.
- Modelo descargado: qwen2.5-coder:3b.
- Progreso observado: Comprendiendo -> Observando/list_dir -> Finalizando.
- Una tarea "hola" finalizo correctamente.
- Otras tareas no seleccionan modelo compatible.

## ESTADO GIT

El usuario realizo push despues de la integracion.
El ultimo estado informado por el agente fue working tree limpio.
No se fija aqui un hash HEAD porque no fue proporcionado en el ultimo relevo.

## PRIORIDAD

Resolver la causa de seleccion/compatibilidad del modelo.

## NO PRIORITARIO AHORA

- nuevas funcionalidades;
- comercializacion;
- API de pago;
- traduccion al ingles;
- vision nueva;
- nuevos agentes;
- ampliacion documental por burocracia.

## CRITERIO DE SALIDA

El flujo cliente incognito debe funcionar de extremo a extremo antes de declarar estable la version real de Condor 1.0:
entorno nuevo -> Ollama/modelo -> presupuesto -> seleccion -> ejecucion -> progreso -> resultado.
