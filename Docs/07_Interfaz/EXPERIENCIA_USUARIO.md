# EXPERIENCIA_USUARIO

Version: 2.0.0
Estado: En desarrollo
Nivel: 07 - Interfaz
Clasificacion: Arquitectura de Experiencia

------------------------------------------------------------------------

# Proposito

Definir la experiencia de usuario que Condor debe ofrecer durante todo el
ciclo de vida de un proyecto.

La experiencia describe como debe comportarse Condor.
La interfaz es solamente el medio para comunicar ese comportamiento.

------------------------------------------------------------------------

# Alcance

Aplica desde la primera ejecucion hasta la finalizacion y continuidad de
un proyecto.

------------------------------------------------------------------------

# Objetivo

Permitir que cualquier persona pueda utilizar Condor sin estudiar su
funcionamiento interno.

Condor se adapta al usuario.

Nunca el usuario a Condor.

------------------------------------------------------------------------

# Principios

- Descubrir antes de preguntar.
- Comprender antes de actuar.
- Planificar antes de implementar.
- Informar antes de finalizar.
- Preservar el contexto.
- Reducir la carga cognitiva.
- Mantener continuidad.
- Actuar como un ingeniero.

------------------------------------------------------------------------

# Experiencia esperada

## Primer uso

Condor analiza automaticamente el entorno.

Detecta:

- Hardware.
- Sistema operativo.
- Herramientas instaladas.
- Modelos disponibles.
- Proyectos compatibles.

Solo solicita informacion cuando no puede inferirla.

------------------------------------------------------------------------

## Proyecto existente

Condor identifica automaticamente:

- Tecnologia.
- Estado.
- Riesgos.
- Punto de continuacion.

Propone un plan antes de modificar el proyecto.

------------------------------------------------------------------------

## Proyecto nuevo

La pregunta principal es:

¿Que quieres construir?

La respuesta representa una intencion.

Condor transforma esa intencion en un plan de trabajo.

------------------------------------------------------------------------

## Durante el trabajo

El usuario siempre conoce:

- Que hace Condor.
- Por que lo hace.
- Que falta.
- Que sigue.

Nunca percibe que el sistema esta detenido.

------------------------------------------------------------------------

## Continuidad

Al reiniciar Condor debe intentar recuperar:

- Proyecto activo.
- Contexto.
- Estado.
- Trabajo pendiente.

El usuario no debera repetir informacion ya conocida.

------------------------------------------------------------------------

# Escenarios de referencia

- Estudiante iniciando su primer proyecto.
- Desarrollador continuando un proyecto.
- Usuario con hardware limitado.
- Usuario sin experiencia tecnica.
- Proyecto sin documentacion previa.

------------------------------------------------------------------------

# Restricciones version 1.x

- Windows como plataforma oficial.
- Interfaz basada en terminal.
- Modelos LLM locales.
- Operacion local.

------------------------------------------------------------------------

# Criterios de aceptacion

La experiencia sera satisfactoria cuando:

- El usuario pueda comenzar rapidamente.
- Condor reduzca preguntas innecesarias.
- El contexto permanezca disponible.
- Toda accion importante sea explicada.
- Continuar un proyecto sea natural.

------------------------------------------------------------------------

# Historial de cambios

| Version | Cambios |
|----------|----------|
| 2.0.0 | Revision arquitectonica del Nivel 07. |
| 1.0.0 | Version inicial. |
