# PROTOCOLO_CONTINUA

Version: 1.0.0
Estado: Activo
Nivel: Global
Clasificacion: Protocolo de Ingenieria

---

# Proposito

Definir el comportamiento obligatorio de Condor cuando el usuario solicita continuar un proyecto.

Este protocolo aplica tanto al desarrollo de Condor como a cualquier proyecto desarrollado mediante Condor.

---

# Principio

El usuario expresa el objetivo.

Condor descubre el contexto.

Condor determina la estrategia.

Condor ejecuta la siguiente mejor accion.

---

# Activacion

Este protocolo se ejecuta cuando el usuario indica unicamente:

Continua

o cualquier instruccion equivalente cuyo objetivo sea proseguir el desarrollo.

---

# Flujo

## Paso 1

Identificar el proyecto y el directorio de trabajo.

## Paso 2

Ejecutar el PROTOCOLO_DESCUBRIMIENTO.

## Paso 3

Construir el contexto consolidado utilizando:

- Documento Maestro.
- Directiva Global.
- ADN Condor.
- Documentacion del nivel.
- Kanban.
- Demas activos de conocimiento.

## Paso 4

Determinar el estado actual del proyecto.

## Paso 5

Identificar la siguiente mejor accion.

## Paso 6

Validar dependencias y bloqueos.

## Paso 7

Ejecutar la accion.

## Paso 8

Validar el resultado.

## Paso 9

Actualizar:

- conocimiento;
- Kanban;
- estado del proyecto;
- artefactos relacionados.

## Paso 10

Dejar el proyecto listo para la siguiente ejecucion del protocolo.

---

# Reglas

- Nunca solicitar informacion que pueda descubrirse.
- Nunca comenzar implementando sin comprender el proyecto.
- Nunca finalizar una tarea sin actualizar el conocimiento.
- Toda accion debe reducir la incertidumbre del proyecto.
- Toda ejecucion debe dejar un artefacto permanente.

---

# Resultado esperado

Al finalizar el protocolo, el proyecto debera encontrarse en un estado mas comprensible, mas documentado y mas cercano a su objetivo que antes de iniciar la ejecucion.

---

Este documento forma parte del conocimiento permanente del Proyecto Condor.
