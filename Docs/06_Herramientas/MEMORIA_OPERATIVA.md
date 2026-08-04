# MEMORIA_OPERATIVA

Version: 1.0.0
Estado: Activo
Nivel: 06 - Construccion
Clasificacion: Arquitectura

---

# PROPOSITO

Definir el mecanismo de memoria temporal utilizado durante la ejecucion de una solicitud en el Proyecto Condor.

La Memoria Operativa conserva exclusivamente la informacion necesaria para completar el ciclo de trabajo en curso.

---

# ALCANCE

La Memoria Operativa existe unicamente durante la ejecucion activa.

No constituye memoria permanente del proyecto.

---

# OBJETIVOS

- Mantener el contexto de ejecucion.
- Compartir informacion entre motores.
- Evitar reprocesamiento.
- Preservar la consistencia del flujo.
- Liberar recursos al finalizar la ejecucion.

---

# CONTENIDO

Puede almacenar temporalmente:

- Solicitud original.
- Objetivos derivados.
- Plan de ejecucion.
- Estado de los motores.
- Resultados parciales.
- Validaciones.
- Eventos de ejecucion.

---

# CICLO DE VIDA

Crear

↓

Actualizar

↓

Consultar

↓

Consolidar

↓

Liberar

---

# REGLAS

- No almacenar conocimiento permanente.
- No reemplazar la documentacion oficial.
- No persistir informacion al finalizar la ejecucion salvo autorizacion del Nucleo.
- Mantener coherencia durante toda la solicitud.

---

# INTERACCIONES

La Memoria Operativa es utilizada por:

- NUCLEO.md
- MOTORES.md
- ORQUESTACION.md
- EJECUCION.md

---

# CRITERIOS DE VALIDACION

- El contexto permanece consistente durante toda la ejecucion.
- Los motores reciben la informacion necesaria.
- No existe persistencia accidental.
- La memoria es liberada al finalizar el proceso.

---

# HISTORIAL DE CAMBIOS

| Version | Cambios |
|----------|----------|
| 1.0.0 | Version inicial. |
