# ESTADO_DESARROLLO

Version: 1.2.0
Estado: Activo
Modo: Evolucion Continua
MVP: Condor 1.0

## Estado actual

Condor 1.0 cuenta con una primera base ejecutable para Windows y con la integracion local con Ollama implementada.

T-001 fue completada: la CLI inicial y el Assessment local funcionan, la deteccion del entorno es 100% local y el resultado estructurado queda disponible para los siguientes componentes.

T-002 fue implementada y esta en revision: el comando `condor ask` consulta al modelo local, selecciona modelo de forma explicita o usando el primero disponible del Assessment, y degrada con mensajes claros cuando Ollama no esta activo o el modelo no existe. No requiere dependencias externas y se comunica solo con 127.0.0.1:11434.

La tecnologia de implementacion es .NET 10 / C# (decision registrada en DECISIONES.md DEC-007). Los contratos permanecen agnosticos a la tecnologia.

## Objetivo inmediato

La siguiente tarea es:

`operacion/TAREAS/T-003.md`

Recomendador de modelos: Condor debe relacionar hardware/capacidades con modelos disponibles y recomendar una estrategia.

T-002 dejo la seleccion de modelo provisional (explicito o primer disponible); T-003 debe proponer la recomendacion inteligente registrada en DEC-016.

## Estado por tarea

| ID | Estado |
|---|---|
| T-001 Bootstrap del MVP y Assessment inicial | Completada |
| T-002 Integracion local con Ollama | En revision |
| T-003 Recomendador de modelos | Pendiente |
| T-004 Descubrimiento de proyecto | Pendiente |
| T-005 Context Engine inicial | Pendiente |
| T-006 Flujo de intencion a plan | Pendiente |
| T-007 Builder inicial | Pendiente |
| T-008 Verificacion inicial | Pendiente |
| T-009 Documentacion y continuidad | Pendiente |
| T-010 Capacidades avanzadas de desarrollo | Pendiente |
| T-011 Vision local | Pendiente |
| T-012 Instalador/puesta en marcha simplificada | Pendiente |

## Restricciones MVP 1.0

- Windows como plataforma oficial.
- Operacion local.
- LLM local.
- Ollama como implementacion inicial.
- Interfaz inicial basada en terminal.
- Sin dependencia obligatoria de servicios cloud.
- Instalacion y puesta en marcha simples.
- Otros sistemas operativos quedan fuera del MVP.

## Capacidades deseadas

- Herramientas de desarrollo locales.
- Seleccion de modelo basada en assessment.
- Modelos con capacidad de codigo.
- Vision cuando hardware, modelo y herramienta lo permitan.
- Degradacion funcional cuando una capacidad avanzada no sea viable.

## Punto exacto de continuidad

T-002 implementada y en revision. La siguiente tarea de implementacion es `T-003`, una vez aprobada T-002.

## Siguiente accion

Revisar `operacion/TAREAS/T-002.md` y su informe de ejecucion; al aprobarse, ejecutar `operacion/TAREAS/T-003.md`.
