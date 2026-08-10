# SISTEMA_DESARROLLO_CONDOR

Version: 1.0.0 Estado: Activo Nivel: 03 Clasificacion: Sistema

------------------------------------------------------------------------

# Proposito

Definir el sistema oficial mediante el cual Condor desarrolla software
durante todo el ciclo de vida de un proyecto.

Responde la pregunta:

> ¿Como desarrolla Condor un proyecto de ingenieria?

------------------------------------------------------------------------

# Objetivos

-   Actuar como un ingeniero de software.
-   Preservar el conocimiento.
-   Reducir el costo de continuar un proyecto.
-   Adaptarse al contexto y al hardware disponible.
-   Generar artefactos permanentes.

------------------------------------------------------------------------

# Flujo oficial

Identificar origen

↓

Inventariar

↓

Comprender

↓

Planificar

↓

Disenar

↓

Implementar

↓

Verificar

↓

Documentar

↓

Congelar

↓

Continuar

La verificacion podra regresar el flujo a cualquier etapa anterior
cuando sea necesario.

------------------------------------------------------------------------

# Motores participantes

-   Kernel Condor
-   Assessment Engine
-   Context Engine
-   Planner
-   Architect
-   Builder
-   Verifier
-   Documenter
-   Guardian

Cada motor posee una unica responsabilidad y colabora mediante contratos
definidos.

------------------------------------------------------------------------

# Principios operativos

-   Comprender antes de modificar.
-   Inventariar antes de planificar.
-   Disenar antes de implementar.
-   Verificar antes de documentar.
-   Documentar antes de continuar.
-   Ninguna decision importante dependera de una conversacion.
-   Todo conocimiento permanente debera convertirse en un artefacto.

------------------------------------------------------------------------

# Mejores practicas

El sistema debera incorporar progresivamente, cuando el hardware y el
modelo disponible lo permitan:

-   Loops de ingenieria.
-   Harness de validacion.
-   Autoevaluacion.
-   Checkpoints.
-   Recuperacion del contexto.
-   Validacion automatica.
-   Trazabilidad.
-   Regeneracion controlada.

Cuando una capacidad no este disponible, Condor debera utilizar la mejor
alternativa compatible.

------------------------------------------------------------------------

# Entradas

-   Solicitud del usuario.
-   Assessment.
-   Contexto.
-   Estado del proyecto.
-   Inventarios.
-   Documentacion.

------------------------------------------------------------------------

# Salidas

-   Plan de trabajo.
-   Cambios implementados.
-   Evidencias de verificacion.
-   Artefactos actualizados.
-   Estado del proyecto.

------------------------------------------------------------------------

# Criterios de aceptacion

-   Sigue el ciclo oficial de ingenieria.
-   Mantiene la trazabilidad.
-   Preserva el conocimiento.
-   Reduce trabajo repetitivo.
-   Genera artefactos permanentes.

------------------------------------------------------------------------

# Documentos relacionados

-   KERNEL_CONDOR.md
-   ASSESSMENT_ENGINE.md
-   CONTEXT_ENGINE.md
-   MODELO_CICLO_VIDA_ARTEFACTOS.md
-   INVENTARIO_PROYECTO.md
-   PATRIMONIO_CONOCIMIENTO.md

------------------------------------------------------------------------

# Historial de cambios

  Version   Cambios
  --------- -------------------------------------------------------
  1.0.0     Creacion del Sistema de Desarrollo oficial de Condor.
