# BACKLOG

Version: 4.0.0
Estado: Activo
Modo: Evolucion Continua
MVP: Condor 1.0

## Estado

El backlog funcional originalmente definido para el MVP 1.0 (T-001 a T-012) esta completado y congelado.

La linea base tecnica **Condor v1.0.0** quedo cerrada y etiquetada (tag `v1.0.0`).

T-013 y T-014 corresponden a evolucion posterior del MVP:
- T-013: Verificacion semantica y de calidad — primera concrecion de SD-02/DE-002.
- T-014: Integracion de la verificacion semantica en el ciclo.

T-015 (Automatizacion de puesta en marcha y modelo LLM local): incorpora la
seleccion automatica del modelo LLM por capacidad de ingenieria dentro de un
presupuesto seguro de recursos y su obtencion durante la puesta en marcha.
**Completada y cerrada.**

T-016 (Experiencia de agente de ingenieria): correccion arquitectonica y
hardcodeo del motor agente. La CLI deja de ser un interprete de comandos: via
principal de intencion libre, preparacion automatica al iniciar, control por
slash, y motor agente robusto (patch quirurgico, harness externo real
build/test/restore, undo_file, resolucion de rutas, guarda anti-falsos-positivos).
**Completada y cerrada** (E2E real en proyectos .NET).

## Trabajo inmediato

| ID | Trabajo | Estado |
|---|---|---|
| T-001 | Bootstrap del MVP y Assessment inicial | Completada y congelada |
| T-002 | Integracion local con Ollama | Completada y congelada |
| T-003 | Recomendador de modelos | Completada y congelada |
| T-004 | Descubrimiento de proyecto | Completada y congelada |
| T-005 | Context Engine inicial | Completada y congelada |
| T-006 | Flujo de intencion a plan | Completada y congelada |
| T-007 | Builder inicial | Completada y congelada |
| T-008 | Verificacion inicial | Completada y congelada |
| T-009 | Documentacion y continuidad | Completada y congelada |
| T-010 | Capacidades avanzadas de desarrollo / ciclo | Completada y congelada |
| T-011 | Vision local | Completada y congelada |
| T-012 | Instalador y puesta en marcha simplificada | Completada y congelada |
| T-013 | Verificacion semantica y de calidad — primera concrecion | Completada y congelada |
| T-014 | Integracion de verificacion semantica en el ciclo | Completada, verificada y congelada |
| T-015 | Automatizacion de puesta en marcha y modelo LLM local | Completada y cerrada (v1.x) |
| T-016 | Experiencia de agente de ingenieria (intencion libre, preparacion automatica, slash, motor agente) | Completada y cerrada (v1.x; E2E .NET) |

## Frontera de Condor 1.0

El MVP 1.0 queda definido por T-001 a T-012. La linea base v1.0.0 permanece
cerrada y etiquetada.

La evolucion posterior (T-013, T-014, T-015, T-016...) se gestiona dentro de
Condor v1.x mediante tareas explicitamente justificadas y cerrables.

La validacion E2E de T-016 corresponde a proyectos .NET (L-008). El soporte
especializado para otros ecosistemas (TypeScript/Python, etc.) queda para una
evolucion posterior, sin considerarse defecto ni promesa implicita.

## Siguiente

T-015 y T-016 completadas y cerradas. La evolucion continua opera sobre tareas
explicitamente justificadas posteriores. El motor agente ya no se limita a
enrutar: corrige codigo real con harness externo como autoridad y guarda
anti-falsos-positivos.
