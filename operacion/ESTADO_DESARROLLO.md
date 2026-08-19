# ESTADO_DESARROLLO

Version: 3.2.0
Estado: Activo
Modo: Evolucion Continua
MVP: Condor 1.0

## Estado actual

T-001 a T-014 estan completadas, verificadas, integradas, publicadas y congeladas.

La linea base tecnica **Condor v1.0.0** quedo cerrada y etiquetada (tag `v1.0.0`).

**T-015 (Automatizacion de puesta en marcha y modelo LLM local)** es la tarea
de evolucion dentro de Condor v1.x incorpora la seleccion automatica del
modelo LLM local por capacidad de ingenieria dentro de un presupuesto seguro de
recursos, y su obtencion cuando es tecnicamente posible, sin modificar la linea
base v1.0.0. Incluye el comando agente `condor hacer`. **Completada y cerrada**
(evidencia funcional: suites verdes + E2E real).

Ultimo estado confirmado antes de T-015:
- Rama: main
- HEAD: a6de1e9 (antes de T-015)
- Working tree: con cambios de T-015

## Frontera funcional actual de la CLI

Condor dispone de:
- condor analizar
- condor contexto
- condor planear
- condor construir
- condor verificar
- condor avanzar
- condor examinar
- condor consultar
- condor recomendar
- condor preparar
- condor verificar-semantico
- condor hacer

T-015 extiende `condor preparar` y añade `condor hacer` para asegurar
automaticamente el modelo LLM local mas adecuado (presupuesto seguro de RAM/disco,
seleccion por maxima capacidad de ingenieria, obtencion via Ollama cuando es
posible, verificacion posterior y agente que cierra el ciclo real editando y
verificando con build/test), sin alterar los restantes comandos.

## Estado del MVP

Condor 1.0 MVP = T-001..T-012 completadas y congeladas.

T-013/T-014 son evolucion posterior orientada a robustecer la verificacion y
cerrar el ciclo real de ingenieria; quedan cerradas y congeladas.

T-015 es la continuacion de Condor dentro de v1.x: la obtencion automatica del
modelo LLM forma parte de la experiencia esperada de Condor (puesta en marcha
automatica sin intervencion manual del modelo).

## Restricciones vigentes

- Windows como plataforma oficial inicial.
- Operacion local.
- Sin dependencia obligatoria de cloud.
- La seleccion/obtencion automatica del modelo LLM local es parte de la puesta en
  marcha y del agente: se respeta presupuesto seguro (nunca superar RAM libre),
  seleccion por capacidad de ingenieria, limites, reintentos y verificacion.
- Sin Architect/Guardian.
- Sin integracion de vision en el ciclo.
- Mantener compatibilidad de comandos existentes.
- 1 archivo afectado = 1 commit individual.
- No reabrir la linea base `v1.0.0` ni quitar su tag.

## Siguiente accion

T-015 completada y cerrada. La evolucion continua opera sobre tareas
explicitamente justificadas posteriores (T-016 en adelante) dentro de Condor v1.x;
ninguna esta en curso. La linea base `v1.0.0` se mantiene cerrada y etiquetada.
