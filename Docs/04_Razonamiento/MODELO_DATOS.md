# MODELO_DATOS

Version: 1.0.0
Estado: Activo
Nivel: 04 - Diseno
Clasificacion: Modelo de Datos

---

# Proposito

Definir el modelo conceptual de datos del Proyecto Condor y las entidades permanentes que soportan el conocimiento, la ejecucion y la evolucion del sistema.

---

# Principios

- El conocimiento es persistente.
- Los documentos son la fuente oficial de verdad.
- El estado del proyecto es unico.
- Las relaciones deben minimizar redundancias.

---

# Entidades

## Proyecto
Representa la unidad principal administrada por Condor.

Atributos principales:
- Identificador
- Nombre
- Estado
- Nivel activo
- Fecha de creacion

---

## Documento

Representa un artefacto permanente del proyecto.

Atributos principales:
- Nombre
- Version
- Estado
- Clasificacion
- Ruta
- Fecha de actualizacion

---

## Nivel

Representa una etapa del proyecto.

Atributos principales:
- Codigo
- Nombre
- Estado
- Objetivo

---

## Entregable

Representa un documento planificado dentro de un nivel.

Atributos principales:
- Nombre
- Estado
- Dependencias

---

## Decision

Representa una decision arquitectonica permanente.

Atributos principales:
- Identificador
- Descripcion
- Justificacion
- Impacto

---

## Componente

Representa un componente arquitectonico.

Atributos principales:
- Nombre
- Responsabilidad
- Interfaces

---

## EstadoProyecto

Representa el seguimiento del proyecto.

Atributos principales:
- Nivel activo
- Kanban
- Bloqueadores
- Siguiente accion

---

# Relaciones

- Un Proyecto contiene multiples Niveles.
- Un Nivel contiene multiples Entregables.
- Un Documento puede registrar una Decision.
- Un Componente puede implementar multiples Interfaces.
- EstadoProyecto resume el estado del Proyecto.

---

# Historial de Cambios

| Version | Cambio |
|----------|--------|
| 1.0.0 | Primera version. |
