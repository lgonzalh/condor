# ESTADO_DESARROLLO

Version: 1.1.0
Estado: Activo
Modo: Evolucion Continua
MVP: Condor 1.0

## Estado actual

Condor 0.1.0 cuenta con T-001 y T-002 completadas, verificadas e integradas en `main`.

## Estado funcional

Condor puede:
- ejecutarse localmente en Windows;
- analizar CPU, RAM, GPU, almacenamiento y sistema operativo;
- detectar Git, herramientas, Ollama y modelos locales;
- persistir el Assessment;
- comunicarse con Ollama mediante loopback;
- ejecutar inferencia local mediante `condor ask`;
- seleccionar modelo mediante `--model`;
- usar provisionalmente el primer modelo disponible;
- degradar correctamente ante Ollama detenido o Assessment invalido.

## Restricciones vigentes

- Windows como plataforma oficial del MVP 1.0.
- Operacion local.
- Sin dependencia obligatoria de internet.
- Ollama como implementacion local inicial.
- Terminal como interfaz inicial.
- No descargar modelos automaticamente.
- La seleccion inteligente de modelos pertenece a T-003.
- Las herramientas agenticas son reemplazables.
- El conocimiento permanente debe quedar en el repositorio.

## Estado Git

Ultimo estado conocido de `main`:
`e558efd36f4369cfd69a04887f43cbfef9fb2136`

T-001 y T-002 estan integradas y publicadas.

## Siguiente tarea

`T-003 - Recomendador de modelos`

Objetivo: convertir el Assessment y el inventario de modelos en una recomendacion local, explicable y adecuada al hardware real.

## Secuencia prevista

T-003 → recomendacion de modelo
T-004 → descubrimiento de proyecto
T-005 → Context Engine inicial
T-006 → intencion → plan
T-007 → Builder inicial
T-008 → verificacion
T-009 → documentacion y continuidad
T-010 → capacidades avanzadas
T-011 → vision local
T-012 → instalacion y puesta en marcha

## Continuidad

El siguiente agente debe leer `AGENTE_CONDOR.md` y `operacion/` antes de modificar codigo.
