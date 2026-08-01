---
id: CONDOR-02
titulo: Arquitectura
version: 0.1.0
estado: Borrador
depende_de:
  - CONDOR-00
  - CONDOR-01
---

# Arquitectura

## Objetivo

Definir la arquitectura conceptual del marco Cóndor y establecer cómo se conectan sus documentos principales.

---

## Definición

La arquitectura de Cóndor organiza el conocimiento del proyecto en documentos independientes, trazables y evolutivos.

Cada documento responde una pregunta específica y depende únicamente de los conceptos necesarios para comprenderlo.

---

## Estructura general

```text
Cóndor
│
├── 00 Acta del Proyecto
├── 01 Filosofía y Manifiesto
├── 02 Arquitectura
└── 03 Modelo del Conocimiento
    ├── 03.01 Unidad Fundamental de Conocimiento
    ├── 03.02 Relaciones del Conocimiento
    ├── 03.03 Estructuras del Conocimiento
    ├── 03.04 Reglas del Conocimiento
    └── 03.05 Evolución del Conocimiento
```

---

## Principios de arquitectura documental

- Cada documento tiene un propósito único.
- Cada concepto se introduce una sola vez.
- Las dependencias deben ser explícitas.
- La lectura debe poder seguir una ruta progresiva.
- La documentación debe servir como fuente de verdad del marco Cóndor.

---

## Fuente de verdad

A partir de este punto, los entregables generados como parte de Cóndor se mantendrán en esta carpeta del repositorio local:

```text
C:\Users\lgonz\Documents\GitHub\condor\docs\02 Arquitectura
```
