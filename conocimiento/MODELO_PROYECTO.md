# MODELO_PROYECTO

Version: 1.0.0
Estado: Activo
Nivel: 01 - Arquitectura
Clasificacion: Arquitectura

---

# Proposito

Definir la representacion conceptual de un proyecto para Condor.

---

# Definicion

Un proyecto es una unidad de conocimiento, implementacion y evolucion administrada por Condor.

El proyecto constituye el contexto principal sobre el cual operan todos los protocolos.

---

# Componentes

- Identidad.
- Objetivo.
- Alcance.
- Estado.
- Conocimiento.
- Arquitectura.
- Implementacion.
- Decisiones.
- Kanban.
- Riesgos.
- Dependencias.

---

# Estados posibles

- Inicial.
- Descubrimiento.
- Planificacion.
- Implementacion.
- Validacion.
- Evolucion.
- Congelado.

---

# Reglas

- Todo proyecto posee un Documento Maestro.
- Todo proyecto mantiene un unico estado oficial.
- Todo proyecto conserva su historial de decisiones.
- Toda implementacion debe poder relacionarse con el conocimiento que la origina.
- El proyecto debe poder continuar sin depender de conversaciones previas.

---

# Relaciones

El modelo se integra con:

- MODELO_CONTEXTO.md
- MODELO_CONOCIMIENTO.md
- GRAFO_CONOCIMIENTO.md
- MODELO_DECISIONES.md
- KANBAN.md

---

# Resultado esperado

Condor debe poder comprender el proyecto, determinar su estado y continuar su desarrollo con la minima intervencion del usuario.

---

Este documento forma parte del conocimiento permanente del Proyecto Condor.
