# ESTADO_DESARROLLO

Version: 1.1.0
Estado: Activo
Modo: Evolucion Continua
MVP: Condor 1.0

## Estado actual

Condor 1.0 cuenta con una primera base ejecutable para Windows.

T-001 fue completada: la CLI inicial y el Assessment local funcionan, la deteccion del entorno es 100% local y el resultado estructurado queda disponible para los siguientes componentes.

La tecnologia de implementacion es .NET 10 / C# (decision registrada en DECISIONES.md DEC-007). Los contratos permanecen agnosticos a la tecnologia.

## Objetivo inmediato

La siguiente tarea es:

`operacion/TAREAS/T-002.md`

Integracion local con Ollama: Condor debe detectar Ollama, consultar modelos disponibles y ejecutar una inferencia local.

El Assessment ya detecta Ollama y los modelos disponibles; T-002 debe consumir esa informacion sin romper el contrato de Condor.Core.

## Estado por tarea

| ID | Estado |
|---|---|
| T-001 Bootstrap del MVP y Assessment inicial | Completada |
| T-002 Integracion local con Ollama | Pendiente |
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

T-001 completada. La siguiente tarea de implementacion es `T-002`.

## Siguiente accion

Ejecutar `operacion/TAREAS/T-002.md`.
