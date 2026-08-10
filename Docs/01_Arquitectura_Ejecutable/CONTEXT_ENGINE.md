# CONTEXT_ENGINE

Version: 1.0.0 Estado: Activo Nivel: 03 Clasificacion: Engine

------------------------------------------------------------------------

# Proposito

Context Engine es el motor responsable de comprender el proyecto activo
y reconstruir el contexto de trabajo antes de iniciar cualquier
actividad de ingenieria.

Su objetivo es garantizar la continuidad del desarrollo sin depender del
historial de una conversacion.

Responde la pregunta:

> ¿Que proyecto tengo delante y como puedo continuarlo?

------------------------------------------------------------------------

# Responsabilidad

Context Engine debera:

-   Identificar el proyecto activo.
-   Reconstruir el contexto operativo.
-   Interpretar la intencion del usuario.
-   Localizar artefactos relevantes.
-   Detectar el punto de continuacion.
-   Entregar un contexto consistente al Kernel.

No modifica el proyecto ni implementa cambios.

------------------------------------------------------------------------

# Entradas

-   Resultado del Assessment Engine.
-   Repositorio del proyecto.
-   Documentacion disponible.
-   Inventarios.
-   Estado del proyecto.
-   Solicitud del usuario.

------------------------------------------------------------------------

# Salidas

-   Contexto operativo.
-   Resumen del proyecto.
-   Punto de continuacion.
-   Riesgos detectados.
-   Dependencias relevantes.
-   Recomendaciones para Planner.

------------------------------------------------------------------------

# Flujo

Inicio

↓

Recibir Assessment

↓

Identificar origen

↓

Reconstruir contexto

↓

Relacionar artefactos

↓

Detectar punto de continuacion

↓

Generar Contexto

↓

Entregar al Kernel

------------------------------------------------------------------------

# Capacidades

## Comprension

-   Interpretar la intencion del usuario.
-   Diferenciar entre idea nueva y proyecto existente.
-   Identificar el alcance solicitado.

## Reconstruccion

-   Analizar documentacion.
-   Analizar estructura del proyecto.
-   Recuperar decisiones permanentes.
-   Localizar artefactos relacionados.

## Continuidad

-   Determinar el ultimo estado conocido.
-   Detectar tareas pendientes.
-   Sugerir el siguiente paso.
-   Evitar repetir trabajo.

------------------------------------------------------------------------

# Principios

-   Comprender antes de actuar.
-   El usuario no debe repetir conocimiento existente.
-   La conversacion no es la fuente oficial de verdad.
-   El contexto debe reconstruirse mediante artefactos permanentes.
-   Adaptarse al proyecto antes que imponer un flujo.

------------------------------------------------------------------------

# Dependencias

Consume:

-   ASSESSMENT_ENGINE.md
-   INVENTARIO_PROYECTO.md
-   PATRIMONIO_CONOCIMIENTO.md
-   ESTADO_PROYECTO.md
-   CONDOR_CONTEXTO_MAESTRO.md

Produce informacion para:

-   KERNEL_CONDOR.md
-   Planner
-   Architect
-   Builder
-   Verifier

------------------------------------------------------------------------

# Criterios de aceptacion

-   Identifica correctamente el proyecto.
-   Reconstruye el contexto sin depender del historial del chat.
-   Localiza la documentacion relevante.
-   Determina el siguiente punto de trabajo.
-   Entrega un contexto consistente y verificable.

------------------------------------------------------------------------

# Historial de cambios

  Version   Cambios
  --------- ----------------------------------------------
  1.0.0     Creacion del Context Engine para Condor 1.x.
