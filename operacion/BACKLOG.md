# BACKLOG

Version: 4.2.0
Estado: Activo
Modo: Evolucion Continua
MVP: Condor 1.0
Fecha de actualizacion: 2026-11-01

## ESTADO VIGENTE (CORTE HISTORICO DE SOFTWARE 2026-10-19)

| Que hacer | Haciendo | Hecho |
|---|---|---|
| Refrescar resumenes semanticos Graft para los 8 archivos nuevos/cambiados de T-021; `graft check` aun marca 25 resumenes obsoletos. | Ninguna tarea de producto Condor abierta. | T-021 completada y publicada; build Release 0 errores/0 advertencias y suite 671/671 al 2026-11-01. La evidencia 664/664 del 2026-10-19 sigue siendo historica. |

El estado de Graft se mantiene separado de las pruebas de Condor. Su capa semantica se genero previamente (37 conceptos, 3.088 nodos, 0 pendientes); los cambios T-021 requieren una nueva pasada `graft build --deep` para volver a sincronizar esos resumenes.

Los apartados marcados como historicos abajo se conservan para trazabilidad y no describen trabajo activo.

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

## Trabajo inmediato de estabilizacion (HISTORICO; CERRADO AL 2026-10-19)

| ID | Trabajo | Estado |
|---|---|---|
| T-014 | Integracion de verificacion semantica en el ciclo | Cerrada/absorbida por evolucion posterior |
| T-015 | Recomendador/seleccion de modelos por capacidad y presupuesto | Cerrada |
| T-016 | Prueba cliente incognito y coherencia del ciclo agente | Cerrada |
| T-021 | Generacion de informes Antes/Durante/Despues del agente y `/avanzar` | Completada, verificada y publicada |

Nota historica: T-016 registro el problema operativo de seleccion de modelo identificado entonces y quedo cerrada segun su ficha.

## T-015 - Frontera actual

Debe garantizar:
- presupuesto seguro <= RAM libre;
- variantes de modelos;
- capacidad de ingenieria;
- seleccion por tarea;
- descarga solo despues de seleccionar una variante viable;
- fallback honesto cuando ninguna variante sirve.

## T-016 - Objetivo historico

Eliminar la contradiccion:
"Modelo local listo: qwen2.5-coder:3b"
vs.
"No hay un modelo compatible disponible para la tarea."

Debe demostrarse con pruebas reproducibles.

## Siguiente mejor accion historica

Encontrar la causa exacta en routing/seleccion/AgentService/AgentEngine usando el caso reproducible ya observado.

## Fuera de alcance inmediato

Nuevos features, comercializacion, API de pago, ingles, vision nueva y ampliacion de agentes.
