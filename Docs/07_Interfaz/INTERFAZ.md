# INTERFAZ

Version: 2.0.0
Estado: En desarrollo
Nivel: 07 - Interfaz
Clasificacion: Arquitectura de Experiencia

------------------------------------------------------------------------

# Proposito

Definir la arquitectura de la experiencia e interfaz de Condor para la version 1.x.

Este documento establece como debe comportarse Condor frente al usuario,
independientemente de la tecnologia utilizada para implementarlo.

La interfaz es el medio.
La experiencia es el producto.

------------------------------------------------------------------------

# Alcance

Este documento define:

- Filosofia de la interfaz.
- Arquitectura de la experiencia.
- Estados operativos.
- Componentes principales.
- Principios de interaccion.
- Responsabilidades del sistema.
- Restricciones de la version 1.x.

------------------------------------------------------------------------

# Principios

- La complejidad pertenece a Condor.
- El usuario expresa una intencion.
- Condor descubre antes de preguntar.
- Condor comprende antes de actuar.
- Condor planifica antes de implementar.
- Condor informa permanentemente.
- Condor preserva el contexto.
- Toda decision debe reducir el costo de continuar el proyecto.

------------------------------------------------------------------------

# Objetivo

Permitir que cualquier persona pueda construir, continuar o comprender un
proyecto sin conocer previamente la arquitectura interna de Condor.

------------------------------------------------------------------------

# Unidad principal

La unidad principal de trabajo es el proyecto.

El chat nunca constituye la unidad de trabajo.

------------------------------------------------------------------------

# Punto de entrada

La pantalla inicial tendra como elemento central la pregunta:

¿Que quieres construir?

La respuesta representa una intencion, no un comando.

------------------------------------------------------------------------

# Arquitectura de experiencia

Usuario

↓

Intencion

↓

Descubrimiento

↓

Comprension

↓

Planificacion

↓

Implementacion

↓

Verificacion

↓

Documentacion

↓

Continuidad

------------------------------------------------------------------------

# Estados operativos

- Inicio.
- Assessment.
- Descubrimiento del proyecto.
- Comprension.
- Planificacion.
- Implementacion.
- Verificacion.
- Documentacion.
- Finalizacion.
- Continuacion.

Cada estado comunica:

- Que ocurre.
- Por que ocurre.
- Que sigue.

------------------------------------------------------------------------

# Componentes principales

## Identidad

Zona PERSISTENTE de la interfaz principal interactiva: permanece visible durante todo el
ciclo de vida de la sesion (inicio, preparacion, espera de entrada, tarea, procesamiento,
respuesta, errores, finalizacion y espera de una nueva tarea). No es un texto que se
imprima solo en determinados momentos; se re-dibuja como zona fija en cada punto de espera
para que no desaparezca por el desplazamiento de la terminal.

Formato base:

    ©Condor - <MODELO LOCAL REAL>
    Observa · Comprende · Planifica · Construye · Verifica
    ------------------------------------------------------

La primera linea muestra SIEMPRE el modelo local REAL que Condor esta utilizando en ese
momento (nunca uno sugerido, anterior o supuesto). Si el modelo cambia, esta linea se
actualiza al modelo realmente activo.

## Area de intencion

Lugar donde el usuario expresa el objetivo.

## Panel de estado

Comunica permanentemente el estado operativo.

## Consola de actividad

Muestra el trabajo realizado por Condor.

## Barra de contexto

Presenta proyecto, entorno y modo de trabajo.

------------------------------------------------------------------------

# Continuidad

Condor debera recuperar automaticamente:

- Proyecto activo.
- Contexto operativo.
- Estado del trabajo.
- Decisiones relevantes.

El usuario no debera repetir informacion ya conocida.

------------------------------------------------------------------------

# Restricciones version 1.x

- Sistema operativo oficial: Windows.
- Interfaz principal basada en terminal.
- Ejecucion local.
- Modelos LLM locales.
- Configuracion automatica siempre que sea posible.

------------------------------------------------------------------------

# Criterios de aceptacion

La interfaz se considera conforme cuando:

- Reduce la carga cognitiva.
- Comunica el estado del sistema.
- Mantiene el contexto.
- Descubre antes de preguntar.
- Permite continuar el trabajo sin friccion.

------------------------------------------------------------------------

# Historial de cambios

| Version | Cambios |
|----------|----------|
| 2.0.0 | Revision arquitectonica del Nivel 07. |
| 1.0.0 | Version inicial. |
