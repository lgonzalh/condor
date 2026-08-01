# DOC-002 Ontologia Fundamental

**Estado:** Borrador  
**Version:** 0.1.0

---

# Objetivo

Definir los conceptos fundamentales sobre los que se construye Cóndor. Estos conceptos son independientes de cualquier tecnologia, lenguaje o implementacion.

---

# Principios

- Cóndor modela la realidad, no la implementacion.
- Todo elemento del modelo es un concepto.
- Los conceptos existen antes que su representacion.
- La implementacion nunca modifica el significado.

---

# Conceptos Fundamentales

## Concepto

Unidad minima de significado dentro de Cóndor.

Representa cualquier elemento que pueda ser identificado y comprendido dentro de un contexto.

---

## Identidad

Propiedad que permite distinguir un concepto de cualquier otro.

La identidad permanece aunque cambien sus caracteristicas.

---

## Relacion

Vinculo entre dos o mas conceptos.

Las relaciones tambien poseen significado y forman parte del modelo.

---

## Contexto

Ambito en el que un concepto adquiere significado.

Un mismo concepto puede existir en distintos contextos sin perder su identidad.

---

## Estado

Condicion observable de un concepto en un momento determinado.

El estado puede cambiar sin alterar la identidad.

---

## Evento

Suceso que produce un cambio de estado en uno o mas conceptos.

Los eventos describen cambios; no representan conceptos permanentes.

---

# Regla Fundamental

Todo modelo Cóndor se construye unicamente mediante conceptos y relaciones definidos dentro de un contexto.

---

# Alcance

Esta ontologia define que puede existir dentro de Cóndor.

No define:

- Implementaciones.
- Bases de datos.
- Lenguajes de programacion.
- Diagramas.
- Persistencia.

Estos aspectos pertenecen a documentos posteriores.

---

# Relacion con otros documentos

- DOC-001 Filosofia y Manifiesto
- DOC-003 Fundamentos
