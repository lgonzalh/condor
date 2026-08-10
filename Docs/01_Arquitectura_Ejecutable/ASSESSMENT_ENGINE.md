# ASSESSMENT_ENGINE

Version: 1.0.0 Estado: Activo Nivel: 03 Clasificacion: Engine

------------------------------------------------------------------------

# Proposito

Assessment Engine es el primer motor operativo de Condor.

Su responsabilidad es comprender automaticamente el entorno antes de
iniciar cualquier tarea de ingenieria.

Su objetivo es reducir configuraciones manuales y proporcionar al resto
del sistema un contexto confiable para la toma de decisiones.

------------------------------------------------------------------------

# Responsabilidad

Antes de planificar, disenar o implementar, Assessment Engine debera
descubrir:

-   Hardware disponible.
-   Sistema operativo.
-   Capacidades del equipo.
-   Modelos LLM disponibles.
-   Herramientas instaladas.
-   Proyecto activo.
-   Estructura del repositorio.
-   Estado general del proyecto.

No implementa cambios.

Solo observa, analiza y reporta.

------------------------------------------------------------------------

# Entradas

-   Sistema operativo.
-   Directorio de trabajo.
-   Configuracion local.
-   Repositorio.
-   Modelos instalados.
-   Herramientas disponibles.

------------------------------------------------------------------------

# Salidas

-   Perfil del entorno.
-   Perfil del proyecto.
-   Restricciones detectadas.
-   Capacidades disponibles.
-   Recomendaciones para Planner.

------------------------------------------------------------------------

# Flujo

Inicio

↓

Descubrir entorno

↓

Descubrir proyecto

↓

Evaluar capacidades

↓

Detectar restricciones

↓

Generar Assessment

↓

Entregar Contexto

------------------------------------------------------------------------

# Capacidades

## Hardware

-   CPU
-   Memoria RAM
-   GPU
-   Espacio disponible
-   Sistema de archivos

## Software

-   Sistema operativo
-   Git
-   Ollama
-   Python
-   Node
-   .NET
-   Java
-   Docker
-   Otras herramientas relevantes

## Modelos

-   Modelos instalados.
-   Capacidades.
-   Restricciones.
-   Compatibilidad.

## Proyecto

-   Lenguaje.
-   Framework.
-   Dependencias.
-   Arquitectura.
-   Estado Git.
-   Documentacion disponible.

------------------------------------------------------------------------

# Principios

-   Descubrir antes de preguntar.
-   Adaptarse al hardware del usuario.
-   No asumir configuraciones.
-   Minimizar la intervencion manual.
-   Entregar evidencia verificable.

------------------------------------------------------------------------

# Dependencias

Consume:

-   CONDOR_CONTEXTO_MAESTRO.md
-   ADN_CONDOR.md
-   DIRECTIVA_GLOBAL.md

Produce informacion para:

-   CONTEXT_ENGINE.md
-   Planner
-   Architect
-   Builder
-   Verifier

------------------------------------------------------------------------

# Criterios de aceptacion

-   Detecta automaticamente el entorno.
-   Detecta automaticamente el proyecto.
-   Identifica restricciones.
-   Recomienda la mejor estrategia disponible.
-   No modifica el sistema del usuario.

------------------------------------------------------------------------

# Historial de cambios

  Version   Cambios
  --------- -------------------------------------------------
  1.0.0     Creacion del Assessment Engine para Condor 1.x.
