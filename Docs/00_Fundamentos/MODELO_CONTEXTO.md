# MODELO_CONTEXTO

Version: 1.0.0
Estado: Activo
Nivel: 01 - Arquitectura
Clasificacion: Arquitectura

---

# Proposito

Definir que constituye el contexto completo de un proyecto para Condor y como debe consolidarse antes de cualquier intervencion.

---

# Definicion

El contexto es el conjunto minimo de conocimiento necesario para comprender el estado real de un proyecto y determinar la siguiente mejor accion.

El contexto tiene prioridad sobre el codigo.

---

# Fuentes de contexto

Condor construye el contexto utilizando, en este orden:

1. Documento Maestro.
2. Directiva Global.
3. ADN Condor.
4. Protocolos.
5. Documentacion del nivel.
6. Kanban.
7. Grafo de conocimiento.
8. Modelo de decisiones.
9. README.
10. Codigo fuente.

---

# Componentes del contexto

- Objetivo del proyecto.
- Estado actual.
- Alcance.
- Arquitectura.
- Decisiones vigentes.
- Dependencias.
- Riesgos.
- Bloqueos.
- Siguiente mejor accion.

---

# Reglas

- El contexto siempre se consolida antes de planificar.
- Ninguna fuente sustituye al conjunto del contexto.
- Si una fuente falta, Condor intentara reconstruirla.
- Solo cuando el contexto siga siendo insuficiente se consultara al usuario.

---

# Resultado esperado

Al finalizar la consolidacion del contexto, Condor debera poder continuar el proyecto con la minima intervencion del usuario.

---

# Dependencias

- CONDOR_CONTEXTO_MAESTRO.md
- ADN_CONDOR.md
- DIRECTIVA_GLOBAL.md
- PROTOCOLO_DESCUBRIMIENTO.md
- PROTOCOLO_CONTINUA.md
- MODELO_CONOCIMIENTO.md
- GRAFO_CONOCIMIENTO.md

---

Este documento forma parte del conocimiento permanente del Proyecto Condor.
