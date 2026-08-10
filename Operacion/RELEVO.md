# RELEVO

Version: 1.0.0
Estado: Inicial

## Ultimo agente

Ninguno.

## Ultimo trabajo

Preparacion del sistema operativo de relevo multi-agente para el MVP de Condor.

## Estado

No existe implementacion funcional todavia.

## Decisiones vigentes

- Condor 1.0 sera local.
- Windows es la plataforma oficial inicial.
- La interfaz inicial sera terminal.
- Ollama sera la implementacion inicial para modelos locales.
- El modelo se seleccionara segun el assessment del entorno.
- Condor debe aprovechar herramientas de desarrollo avanzadas cuando sean viables.
- Vision sera una capacidad opcional dependiente del hardware y modelo.
- El agente debe poder ser reemplazado sin perder continuidad.

## Trabajo pendiente

Ver `BACKLOG.md` y `TAREAS/T-001.md`.

## Siguiente accion exacta

Completar T-001 siguiendo su ciclo completo y actualizar este archivo antes del relevo.

## Advertencias

No asumir un modelo LLM fijo antes del assessment.

No convertir capacidades deseadas en requisitos obligatorios si el hardware o el modelo no las soportan.

No comenzar con una arquitectura distribuida o dependencias cloud para resolver necesidades del MVP.
