# ARCHITECT

Version: 1.0.0
Estado: Activo
Nivel: 01 - Arquitectura Ejecutable
Clasificacion: Componente

---

# Proposito

Definir la solucion arquitectonica para cada solicitud aprobada por el Planner, garantizando consistencia con la arquitectura y las directivas del Proyecto Condor.

---

# Dependencias

- KERNEL_CONDOR.md
- CONTRATO_KERNEL.md
- CONTEXT_MANAGER.md
- KNOWLEDGE_MANAGER.md
- PLANNER.md

---

# Documentos relacionados

- ARQUITECTURA_NUCLEO.md
- ARQUITECTURA_COMPONENTES.md
- MODELO_PROYECTO.md

---

# Responsabilidades

- Diseñar la arquitectura de la solucion.
- Seleccionar los componentes involucrados.
- Definir interfaces y contratos necesarios.
- Identificar impactos arquitectonicos.
- Garantizar el cumplimiento de las directivas del proyecto.

---

# Entradas

- Plan de ejecucion.
- Contexto consolidado.
- Conocimiento vigente.

---

# Salidas

- Diseño arquitectonico.
- Componentes participantes.
- Contratos requeridos.
- Restricciones tecnicas.

---

# Reglas

- No implementa codigo.
- No modifica la documentacion oficial.
- Toda decision arquitectonica debe ser trazable.
- Debe preservar la simplicidad y evitar duplicidades.

---

# Impacto

Toda modificacion requiere revisar KERNEL_CONDOR.md y ARQUITECTURA_COMPONENTES.md.

---

# Historial de cambios

| Version | Cambios |
|----------|---------|
| 1.0.0 | Primera version del Architect. |
