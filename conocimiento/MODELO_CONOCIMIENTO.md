# MODELO_CONOCIMIENTO

Version: 1.0.0
Estado: Activo
Nivel: 01 - Arquitectura
Clasificacion: Arquitectura

---

# Proposito

Definir el modelo conceptual del conocimiento utilizado por Condor.

---

# Principios

- El conocimiento es el activo principal.
- Todo conocimiento posee un origen.
- Todo conocimiento puede relacionarse.
- Ningun conocimiento debe quedar aislado.

---

# Activos de conocimiento

- Documento Maestro
- Directivas
- ADN
- Protocolos
- Documentos de nivel
- Arquitectura
- Kanban
- Decisiones
- Implementaciones
- Validaciones

---

# Relaciones

Cada activo puede relacionarse mediante:

- depende de;
- reemplaza;
- complementa;
- referencia;
- genera;
- actualiza.

---

# Ciclo de vida

1. Crear.
2. Validar.
3. Utilizar.
4. Actualizar.
5. Congelar.
6. Evolucionar.

---

# Reglas

- Toda implementacion debe referenciar conocimiento existente.
- Todo conocimiento nuevo debe integrarse al modelo.
- El modelo debe poder recorrerse desde cualquier activo.

---

# Dependencias

- CONDOR_CONTEXTO_MAESTRO.md
- ADN_CONDOR.md
- DIRECTIVA_GLOBAL.md
- PROTOCOLO_DESCUBRIMIENTO.md

---

Este documento forma parte del conocimiento permanente del Proyecto Condor.
