# ESTADO_DESARROLLO

Version: 4.0.0
Estado: Activo
Modo: Evolucion Continua
MVP: Condor 1.0

## Estado actual

T-001 a T-014 estan completadas, verificadas, integradas, publicadas y congeladas.

La linea base tecnica **Condor v1.0.0** quedo cerrada y etiquetada (tag `v1.0.0`).

**T-015 (Automatizacion de puesta en marcha y modelo LLM local)** es una tarea
de evolucion dentro de Condor v1.x: seleccion automatica del modelo LLM local por
capacidad de ingenieria dentro de un presupuesto seguro de recursos, y su
obtencion cuando es tecnicamente posible. **Completada y cerrada.**

**T-016 (Experiencia de agente de ingenieria: intencion libre, preparacion
automatica, control por slash y hardcodeo del motor agente)** es una correccion
arquitectonica: la CLI pasa de ser un interprete de comandos a un agente de
ingenieria con intencion libre como via principal, preparacion automatica al
iniciar, comandos de control con `/`, y un motor agente robusto (edicion
quirurgica, harness externo real con build/test/restore, recuperacion, guarda
anti-falsos-positivos). **Completada y cerrada** (evidencia funcional: suites
verdes + E2E real verificado externamente en proyectos .NET).

Ultimo estado confirmado antes de T-015:
- Rama: main
- Antes de T-015: HEAD a6de1e9
- T-016 consolidada tras T-015 en la misma serie v1.x

## Frontera funcional actual de la CLI

Condor se usa por intencion libre (via principal) y por comandos de control:

- `condor` → prepara el entorno automaticamente y abre sesion interactiva.
- `condor "<intencion>"` → entrega la intencion al motor agente (one-shot).
- `condor /analizar` → analiza el proyecto o directorio actual.
- `condor /contexto`, `/planear`, `/construir`, `/verificar`, `/avanzar`,
  `/examinar`, `/recomendar`, `/consultar`, `/verificar-semantico`, `/preparar`.
- `condor /ayuda`, `/salir`, `condor -h/--help`, `condor -v/--version`.

La preparacion (hardware, RAM, almacenamiento, GPU, Ollama, modelos, presupuesto
seguro y seleccion) ocurre de forma automatica y silenciosa al iniciar condor; no
es un requisito previo que el usuario deba ejecutar. El analisis de hardware NO
pertenece a `/analizar`; pertenece a la preparacion automatica.

## Estado del MVP

Condor 1.0 MVP = T-001..T-012 completadas y congeladas.

T-013/T-014 son evolucion posterior orientada a robustecer la verificacion y
cerrar el ciclo real de ingenieria; quedan cerradas y congeladas.

T-015 y T-016 son la continuacion de Condor dentro de v1.x: la puesta en marcha
automatica del modelo y la experiencia de agente de ingenieria por intencion
libre.

## Restricciones vigentes

- Windows como plataforma oficial inicial.
- Operacion local.
- Sin dependencia obligatoria de cloud.
- La seleccion/obtencion automatica del modelo LLM local es parte de la puesta en
  marcha y del agente: se respeta presupuesto seguro (nunca superar RAM libre),
  seleccion por capacidad de ingenieria, limites, reintentos y verificacion.
- El exito del agente depende exclusivamente del harness externo externo
  (build/test/restore reales); jamas de la declaracion del modelo. Guarda
  anti-falsos-positivos: si se modifican archivos de prueba para que "pasen",
  Condor no confirma exito.
- La intencion libre es una via de primera clase; nunca se responde "comando
  desconocido". Los comandos de control son diagnosticos y no un requisito.
- Sin Architect/Guardian.
- Sin integracion de vision en el ciclo.
- L-008: la validacion E2E de T-016 corresponde a proyectos .NET; el soporte
  especializado para otros ecosistemas (TypeScript, Python, etc.) queda para una
  evolucion posterior, sin considerarse defecto ni promesa implicita.
- 1 archivo afectado = 1 commit individual.
- No reabrir la linea base `v1.0.0` ni quitar su tag.

## Siguiente accion

T-015 y T-016 estan completadas y cerradas. La evolucion continua opera sobre
tareas explicitamente justificadas posteriores (T-017 en adelante) dentro de
Condor v1.x; ninguna esta en curso. La linea base `v1.0.0` se mantiene cerrada y
etiquetada.
