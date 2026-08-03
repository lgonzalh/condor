# FLUJO_INTERNO_NUCLEO

Version: 1.0.0
Estado: Activo
Nivel: 01 - Arquitectura
Clasificacion: Arquitectura

---

# Proposito

Definir el flujo interno de trabajo del nucleo de Condor desde la recepcion de un objetivo hasta la entrega de un artefacto.

---

# Flujo principal

1. Recibir objetivo.
2. Ejecutar Protocolo de Descubrimiento.
3. Consolidar conocimiento.
4. Determinar la siguiente mejor accion.
5. Validar dependencias.
6. Ejecutar la accion.
7. Validar el resultado.
8. Actualizar el conocimiento permanente.
9. Actualizar el estado del proyecto.
10. Finalizar.

---

# Reglas

- Ninguna implementacion inicia sin descubrimiento.
- Toda accion consume conocimiento antes de ejecutarse.
- Toda accion genera conocimiento al finalizar.
- El flujo debe minimizar preguntas al usuario.
- El usuario expresa objetivos; Condor determina la estrategia.

---

# Entradas

- Objetivo del usuario.
- Conocimiento existente.
- Estado del proyecto.

---

# Salidas

- Artefactos.
- Actualizacion documental.
- Estado actualizado.
- Siguiente mejor accion.

---

# Dependencias

- CONDOR_CONTEXTO_MAESTRO.md
- ADN_CONDOR.md
- PROTOCOLO_DESCUBRIMIENTO.md
- ARQUITECTURA_NUCLEO.md
- ARQUITECTURA_COMPONENTES.md
- CONTRATOS_COMPONENTES.md

---

Este documento forma parte del conocimiento permanente del Proyecto Condor.
