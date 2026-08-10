# ESTADO_DESARROLLO

Version: 1.0.0
Estado: Inicial
Modo: Evolucion Continua
MVP: Condor 1.0

## Estado actual

Condor se encuentra en preparacion para la implementacion del MVP.

La documentacion fundacional y arquitectonica existe. El siguiente objetivo es convertir la arquitectura definida en un producto local ejecutable para Windows.

## Objetivo inmediato

Construir una primera version funcional que permita:

1. Ejecutarse localmente en Windows.
2. Detectar el entorno del usuario.
3. Analizar hardware y capacidades disponibles.
4. Detectar herramientas y modelos locales.
5. Recomendar una estrategia de modelos LLM compatible.
6. Integrarse localmente con Ollama.
7. Recibir una intencion del usuario desde terminal.
8. Descubrir el proyecto objetivo.
9. Preparar el contexto necesario para planificar una tarea.
10. Mantener el estado para permitir continuidad.

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

La primera tarea de implementacion es `T-001`.

## Siguiente accion

Ejecutar `operacion/TAREAS/T-001.md`.
