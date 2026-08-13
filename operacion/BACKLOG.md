# BACKLOG

Version: 2.1.0
Estado: Activo
Modo: Evolucion Continua

| ID | Prioridad | Trabajo | Estado |
|---|---|---|---|
| T-001 | Critica | Bootstrap del MVP y Assessment inicial | Completada |
| T-002 | Critica | Integracion local con Ollama | Completada |
| T-003 | Critica | Recomendador de modelos | Completada |
| T-004 | Alta | Descubrimiento de proyecto | Completada y congelada |
| T-005 | Alta | Context Engine inicial | Completada y verificada |
| T-006 | Alta | Flujo de intencion a plan | Diseno completado, pendiente de aprobacion |
| T-007 | Alta | Builder inicial | Pendiente |
| T-008 | Alta | Verificacion inicial | Pendiente |
| T-009 | Alta | Documentacion y continuidad | Pendiente |
| T-010 | Media | Capacidades avanzadas de desarrollo | Pendiente |
| T-011 | Media | Vision local | Pendiente |
| T-012 | Media | Instalador y puesta en marcha simplificada | Pendiente |

## Siguiente

`T-006 - Flujo de intencion a plan`

Estado: Diseno tecnico completado. Pendiente de revision formal y
aprobacion para implementacion.

T-005 quedo completada, verificada, integrada y congelada.

T-006 consumira el `ProjectContext` entregado por T-005 para interpretar la intencion del usuario y producir planes.

Contrato formalizado en `operacion/TAREAS/T-006.md` (version 1.0.0) y decision DEC-030 (D-E1 a D-E8).

Diseno tecnico completado en `operacion/TAREAS/T-006.md` (version 1.1.0) y registrado como DEC-031 (PROPUESTA, D-DE1 a D-DE6).

No iniciar T-006 desde codigo. Primero revision formal del diseno aprobada.
