# TRANSICIONES_KERNEL

Version: 1.0.0
Estado: Activo
Nivel: 01 - Arquitectura Ejecutable
Clasificacion: Transiciones

---

# Proposito

Definir las transiciones permitidas entre los estados del Kernel durante la ejecucion de una solicitud.

---

# Dependencias

- ESTADO_KERNEL.md
- CICLO_EJECUCION.md
- MODELO_EJECUCION.md
- ORQUESTADOR_KERNEL.md

---

# Transiciones permitidas

- Idle -> Initializing
- Initializing -> ContextLoaded
- ContextLoaded -> KnowledgeLoaded
- KnowledgeLoaded -> Planning
- Planning -> Designing
- Designing -> Implementing
- Implementing -> Reviewing
- Reviewing -> Validating
- Validating -> Documenting
- Documenting -> Completed

---

# Transiciones excepcionales

Desde cualquier estado operativo:

- -> Failed (error no recuperable)

Desde Failed:

- -> Idle (nueva ejecucion)
- -> Initializing (reintento autorizado)

---

# Reglas

- Toda transicion debe originarse en un evento valido.
- No existen transiciones directas que omitan estados obligatorios.
- Cada cambio de estado debe quedar registrado.
- El Orquestador valida todas las transiciones.

---

# Entradas

- Estado actual.
- Evento disparador.
- Resultado del componente activo.

---

# Salidas

- Nuevo estado.
- Registro de transicion.
- Estado consolidado del Kernel.

---

# Impacto

Toda modificacion requiere revisar ESTADO_KERNEL.md y ORQUESTADOR_KERNEL.md.

---

# Historial de cambios

| Version | Cambios |
|----------|---------|
| 1.0.0 | Primera version del modelo de transiciones del Kernel. |
