# ESTADO_DESARROLLO

Version: 1.3.0
Estado: Activo
Modo: Evolucion Continua
MVP: Condor 1.0

## Estado actual

Condor 0.1.0 cuenta con T-001, T-002 y T-003 completadas, verificadas, integradas en `main` y publicadas.

T-003 (Recomendador de modelos) fue integrada mediante PR #1 (merge `12a3c5b031da00f36d32a6f66322bcc1392573d9`) y queda cerrada y congelada.

## Estado funcional

Condor puede:
- ejecutarse localmente en Windows;
- analizar CPU, RAM, GPU, almacenamiento y sistema operativo;
- detectar Git, herramientas, Ollama y modelos locales;
- persistir el Assessment;
- comunicarse con Ollama mediante loopback;
- ejecutar inferencia local mediante `condor consultar`;
- seleccionar modelo mediante `--modelo`;
- usar provisionalmente el primer modelo disponible;
- recomendar un modelo local mediante `condor recomendar`;
- priorizar modelos para desarrollo y distinguir propositos (development, general y vision);
- explicar motivos, alternativas y limitaciones;
- degradar ante Assessment ausente, Ollama detenido o inventario vacio;
- degradar correctamente ante Ollama detenido o Assessment invalido.

## Restricciones vigentes

- Windows como plataforma oficial del MVP 1.0.
- Operacion local.
- Sin dependencia obligatoria de internet.
- Ollama como implementacion local inicial.
- Terminal como interfaz inicial.
- No descargar modelos automaticamente.
- La seleccion inteligente de modelos esta implementada en T-003 y no cambia la seleccion de condor consultar.
- Las herramientas agenticas son reemplazables.
- El conocimiento permanente debe quedar en el repositorio.

## Estado Git

Ultimo estado conocido de `main`:
`12a3c5b031da00f36d32a6f66322bcc1392573d9`

T-001, T-002 y T-003 estan integradas y publicadas.

## Siguiente tarea

`T-004 - Descubrimiento de proyecto` (referencia conservada; sin definicion formal aun).

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
