# AGENTE_CONDOR

Version: 4.0.0
Estado: Vigente
Modo: Evolucion Continua
Fecha de continuidad: 2026-08-19

## Proposito

Definir el comportamiento operativo del agente que mantiene Condor.

## Regla de entrada

Antes de modificar codigo:
1. Leer ESTADO_PROYECTO.md.
2. Leer ESTADO_DESARROLLO.md.
3. Leer BACKLOG.md.
4. Leer KANBAN.md.
5. Leer INVENTARIO_PROYECTO.md.
6. Revisar Git real.
7. Identificar la tarea activa.
8. No asumir contexto no documentado.

## Regla actual de trabajo

Trabajar con UN SOLO AGENTE INTEGRADOR.

No repartir la estabilizacion actual entre varios agentes. La coordinacion de varios agentes produjo trabajo entrelazado y costo innecesario.

## Restricciones

- Operacion local.
- Ollama como proveedor local inicial.
- Sin dependencia cloud obligatoria.
- No modificar trabajo ajeno.
- No usar git add -p, git add -A, reset, restore, checkout o clean salvo autorizacion explicita.
- No hacer commits o push sin autorizacion del usuario, salvo que la tarea lo indique expresamente.
- No declarar una funcionalidad terminada solo porque compila.
- Toda afirmacion funcional debe tener prueba reproducible.
- La CLI publica debe conservar el espanol sin tildes.

## Prioridad inmediata

Resolver la inconsistencia de seleccion/compatibilidad del modelo:

- qwen2.5-coder:3b fue descargado.
- Condor mostro "Modelo local listo: qwen2.5-coder:3b".
- Presupuesto observado: 8,2 GB disponibles; presupuesto seguro 3,7 GB; estado Normal.
- La tarea "hola" logra ejecutarse.
- Otras tareas como "que modelo eres?" y el analisis de archivos terminan con "No hay un modelo compatible disponible para la tarea."
- El agente debe encontrar la causa exacta antes de modificar codigo.

## Prohibicion temporal

No modificar presupuesto, descarga, progreso, CLI ni documentacion mientras se investiga la causa de seleccion/compatibilidad, salvo que la causa demuestre una dependencia directa.

## Criterio de cierre

Primero:
Comprender -> reproducir -> localizar causa -> corregir -> probar.

Despues:
Documentar -> congelar -> continuar.
