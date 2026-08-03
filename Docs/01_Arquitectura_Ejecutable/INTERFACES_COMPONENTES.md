# INTERFACES_COMPONENTES

Version: 1.0.0
Estado: Activo
Nivel: 01 - Arquitectura Ejecutable
Clasificacion: Interfaces

---

# Proposito

Definir las interfaces de comunicacion entre el Kernel y los componentes del Proyecto Condor, estableciendo contratos consistentes para el intercambio de informacion.

---

# Dependencias

- KERNEL_CONDOR.md
- CONTRATO_KERNEL.md
- INTEGRACION_KERNEL.md
- FLUJO_KERNEL.md

---

# Interfaces

## IContextManager

### Entrada

- Solicitud del usuario.
- Estado del proyecto.

### Salida

- Contexto consolidado.

---

## IKnowledgeManager

### Entrada

- Contexto consolidado.

### Salida

- Conocimiento vigente.

---

## IPlanner

### Entrada

- Contexto.
- Conocimiento.

### Salida

- Plan de ejecucion.

---

## IArchitect

### Entrada

- Plan de ejecucion.

### Salida

- Diseño arquitectonico.

---

## IImplementer

### Entrada

- Diseño arquitectonico.

### Salida

- Artefactos implementados.

---

## IReviewer

### Entrada

- Artefactos implementados.

### Salida

- Resultado de revision.

---

## IValidator

### Entrada

- Resultado de revision.

### Salida

- Resultado de validacion.

---

## IDocumenter

### Entrada

- Resultado validado.

### Salida

- Documentacion actualizada.
- Estado del proyecto sincronizado.

---

# Reglas

- Todas las interfaces son desacopladas.
- Ningun componente invoca directamente a otro.
- El Kernel orquesta todas las llamadas.
- Las entradas y salidas deben ser deterministas y trazables.

---

# Impacto

Toda modificacion requiere revisar KERNEL_CONDOR.md, CONTRATO_KERNEL.md e INTEGRACION_KERNEL.md.

---

# Historial de cambios

| Version | Cambios |
|----------|---------|
| 1.0.0 | Primera version de las interfaces de componentes. |
