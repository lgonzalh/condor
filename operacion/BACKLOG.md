# BACKLOG

Version: 4.0.0
Estado: Activo
Modo: Evolucion Continua
MVP: Condor 1.0
Fecha: 2026-08-19

> **Nota de vigencia 2026-10-19.** Este documento conserva el backlog historico.
> El estado vigente es: build 0w/0e, suite 664/664, UTF-8 sin mojibake, TUI
> `beta` y lanzador global unico en `C:\GitHub\condor`. Ver
> `ESTADO_DESARROLLO.md` e `INVENTARIO_TECNICO_REPO.md`.

## Regla

No crear tareas por inercia. Una tarea nueva requiere necesidad concreta, alcance delimitado, beneficio verificable y criterio de cierre.

## Estado historico

T-001..T-012: completadas y congeladas como MVP documental.
T-013: completada y congelada.
T-014: integracion posterior del ciclo.

## Trabajo inmediato de estabilizacion

| ID | Trabajo | Estado |
|---|---|---|
| T-014 | Integracion de verificacion semantica en el ciclo | Cerrada/absorbida por evolucion posterior |
| T-015 | Recomendador/seleccion de modelos por capacidad y presupuesto | En estabilizacion |
| T-016 | Prueba cliente incognito y coherencia del ciclo agente | En investigacion |

Nota: T-016 representa el problema operativo actual identificado en pruebas reales. No se debe dividir en subtareas innecesarias.

## T-015 - Frontera actual

Debe garantizar:
- presupuesto seguro <= RAM libre;
- variantes de modelos;
- capacidad de ingenieria;
- seleccion por tarea;
- descarga solo despues de seleccionar una variante viable;
- fallback honesto cuando ninguna variante sirve.

## T-016 - Objetivo actual

Eliminar la contradiccion:
"Modelo local listo: qwen2.5-coder:3b"
vs.
"No hay un modelo compatible disponible para la tarea."

Debe demostrarse con pruebas reproducibles.

## Siguiente mejor accion

Encontrar la causa exacta en routing/seleccion/AgentService/AgentEngine usando el caso reproducible ya observado.

## Fuera de alcance inmediato

Nuevos features, comercializacion, API de pago, ingles, vision nueva y ampliacion de agentes.
