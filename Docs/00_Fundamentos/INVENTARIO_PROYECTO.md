# INVENTARIO_PROYECTO

Version: 1.1.0
Estado: Activo
Nivel: Global
Clasificacion: Inventario Maestro

---

# PROPOSITO

Constituir el indice maestro del Proyecto Condor, registrando los artefactos permanentes del proyecto, su estado, dependencias y ubicacion dentro de la arquitectura documental.

Este documento responde la pregunta:

> ¿Que existe actualmente en el Proyecto Condor?

---

# ALCANCE

Aplica a todos los documentos permanentes del proyecto, independientemente del nivel al que pertenezcan.

No incluye conversaciones, borradores temporales ni notas de trabajo.

---

# REGLA DE INVENTARIO

Todo documento permanente existente o planificado debera estar registrado.

Los documentos planificados se identifican como Planificado y no se consideran existentes hasta que hayan sido entregados.

---

# ESTADOS

- Pendiente
- En progreso
- Listo
- Congelado
- Vigente
- Planificado

---

# INVENTARIO ACTUAL

| Documento | Nivel | Version | Estado | Clasificacion | Dependencias |
|-----------|-------|---------|--------|---------------|--------------|
| CONDOR_CONTEXTO_MAESTRO.md | Global | 2.1.0 | Listo | Constitucion | Ninguna |
| ADN_CONDOR.md | Global | Vigente | Listo | ADN | Contexto Maestro |
| DIRECTIVA_GLOBAL.md | Global | Vigente | Listo | Directiva | ADN |
| DIRECTIVA_OPERATIVA_PROYECTO_CONDOR.md | Global | 2.1.0 | Listo | Directiva | Directiva Global |
| ESTADO_PROYECTO.md | Global | 1.2.0 | En progreso | Operacion | Documentos globales |
| REGISTRO_DEUDA_ARQUITECTONICA.md | Global | 1.0.0 | En progreso | Registro | Todos |
| INVENTARIO_PROYECTO.md | Global | 1.1.0 | En progreso | Inventario | Ninguna |
| PATRIMONIO_CONOCIMIENTO.md | Global | Planificado | Planificado | Inventario | Cuadernos I-V |
| INVENTARIO_FUNCIONAL.md | Global | Planificado | Planificado | Inventario | Arquitectura |
| INVENTARIO_ARQUITECTURA.md | Global | Planificado | Planificado | Inventario | Arquitectura |
| MODELO_CICLO_VIDA_ARTEFACTOS.md | Global | Planificado | Planificado | Modelo | Directivas |
| EVOLUCION.md | 09 | Planificado | Planificado | Evolucion | ESTADO_PROYECTO.md |
| MEJORA_CONTINUA.md | 09 | Planificado | Planificado | Evolucion | EVOLUCION.md |
| VERSIONADO.md | 09 | Planificado | Planificado | Evolucion | EVOLUCION.md |
| MIGRACION.md | 09 | Planificado | Planificado | Evolucion | EVOLUCION.md |
| COMPATIBILIDAD.md | 09 | Planificado | Planificado | Evolucion | EVOLUCION.md |
| AUDITORIA.md | 09 | Planificado | Planificado | Evolucion | EVOLUCION.md |
| DEUDA_EVOLUTIVA.md | 09 | Planificado | Planificado | Evolucion | EVOLUCION.md |
| ROADMAP_EVOLUCION.md | 09 | Planificado | Planificado | Evolucion | EVOLUCION.md |

---

# REGLAS

1. Ningun documento permanente existente podra permanecer fuera del inventario.
2. Todo documento congelado debera actualizar su version en este inventario.
3. Toda eliminacion debera conservar trazabilidad historica.
4. Los documentos planificados no sustituyen documentos existentes.
5. El inventario no determina el nivel activo; esa funcion corresponde exclusivamente a ESTADO_PROYECTO.md.

---

# HISTORIAL DE CAMBIOS

| Version | Cambios |
|---------|---------|
| 1.1.0 | Se sincroniza el inventario con el Nivel 09 activo, se registran sus ocho entregables planificados y se establece que INVENTARIO_PROYECTO.md no determina el nivel activo. |
| 1.0.0 | Creacion del Inventario Maestro del Proyecto Condor. |
