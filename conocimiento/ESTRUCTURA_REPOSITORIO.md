# ESTRUCTURA_REPOSITORIO

Version: 1.0.0
Estado: Activo
Nivel: Global
Clasificacion: Arquitectura

---

# Proposito

Definir la estructura oficial del repositorio del Proyecto Condor.

La organizacion del repositorio debera facilitar la comprension por personas e IA.

---

# Principios

- El conocimiento tiene prioridad sobre el codigo.
- Cada activo posee una ubicacion unica.
- Evitar duplicidades.
- Mantener una estructura simple y escalable.

---

# Estructura inicial

condor/

├── conocimiento/
│   ├── maestro/
│   ├── protocolos/
│   ├── directivas/
│   ├── arquitectura/
│   ├── desarrollo/
│   ├── decisiones/
│   └── niveles/
│
├── codigo/
│
├── pruebas/
│
├── herramientas/
│
├── recursos/
│
└── README.md

---

# Responsabilidad de cada carpeta

## conocimiento

Contiene el conocimiento permanente del proyecto.

## codigo

Implementacion del sistema Condor.

## pruebas

Pruebas funcionales, integracion y validacion.

## herramientas

Recursos de apoyo al desarrollo.

## recursos

Imagenes, plantillas y demas activos no ejecutables.

---

# Reglas

- Ningun documento oficial se almacena fuera de conocimiento.
- Ningun archivo se duplica entre carpetas.
- Toda IA debe consultar primero conocimiento antes de modificar codigo.
- Toda nueva carpeta debe tener un proposito claramente definido.

---

# Evolucion

La estructura podra ampliarse sin romper esta organizacion base.

---

Este documento forma parte del conocimiento permanente del Proyecto Condor.
