# DOC-002 Ontología Fundamental

**Estado:** Draft  
**Versión:** 0.1.0

---

# Objetivo

Definir los conceptos fundamentales sobre los que se construye Condor. Estos conceptos son independientes de cualquier tecnología, lenguaje o implementación.

---

# Principios

- Condor modela la realidad, no la implementación.
- Todo elemento del modelo es un concepto.
- Los conceptos existen antes que su representación.
- La implementación nunca modifica el significado.

---

# Conceptos Fundamentales

## Concepto

Unidad mínima de significado dentro de Condor.

Representa cualquier elemento que pueda ser identificado y comprendido dentro de un contexto.

---

## Identidad

Propiedad que permite distinguir un concepto de cualquier otro.

La identidad permanece aunque cambien sus características.

---

## Relación

Vínculo entre dos o más conceptos.

Las relaciones también poseen significado y forman parte del modelo.

---

## Contexto

Ámbito en el que un concepto adquiere significado.

Un mismo concepto puede existir en distintos contextos sin perder su identidad.

---

## Estado

Condición observable de un concepto en un momento determinado.

El estado puede cambiar sin alterar la identidad.

---

## Evento

Suceso que produce un cambio de estado en uno o más conceptos.

Los eventos describen cambios; no representan conceptos permanentes.

---

# Regla Fundamental

Todo modelo Condor se construye únicamente mediante conceptos y relaciones definidos dentro de un contexto.

---

# Alcance

Esta ontología define qué puede existir dentro de Condor.

No define:

- Implementaciones.
- Bases de datos.
- Lenguajes de programación.
- Diagramas.
- Persistencia.

Estos aspectos pertenecen a documentos posteriores.

---

# Relación con otros documentos

- DOC-001 Filosofía y Manifiesto
- DOC-003 Fundamentos
