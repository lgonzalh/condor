# ESTRUCTURA_REPOSITORIO

Version: 2.0.0
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

# Estructura vigente

condor/
│
├── AGENTE_CONDOR.md
├── ESTADO_PROYECTO.md
├── INVENTARIO_PROYECTO.md
├── README.md
├── LICENSE
├── NOTICE
│
├── Docs/
│   ├── 00_Fundamentos/
│   ├── 01_Arquitectura_Ejecutable/
│   ├── 02_Memoria/
│   ├── 03_Planificacion/
│   ├── 04_Razonamiento/
│   ├── 05_Ejecucion/
│   ├── 06_Herramientas/
│   ├── 07_Interfaz/
│   ├── 08_Calidad/
│   └── 09_Evolucion/
│
├── operacion/
│   └── TAREAS/
│
├── Assets/
├── Samples/
├── Scripts/
├── Src/
├── Tests/
│
└── Condor.slnx

---

# Responsabilidad de cada carpeta

## Docs

Contiene el conocimiento permanente del proyecto, organizado por niveles estructurales del 00 al 09.

## operacion

Contiene el estado operativo del desarrollo y el mecanismo de continuidad entre agentes (ESTADO_DESARROLLO, RELEVO, BACKLOG, KANBAN, REGISTRO_CAMBIOS y TAREAS).

## Assets

Activos visuales no ejecutables (imagenes, plantillas).

## Samples

Ejemplos de uso y proyectos de muestra.

## Scripts

Scripts de apoyo al desarrollo y a la operacion.

## Src

Implementacion del sistema Condor (proyectos .NET del MVP 1.0).

## Tests

Pruebas unitarias, de integracion y de arquitectura.

---

# Reglas

- Ningun documento oficial se almacena fuera de Docs.
- Ningun archivo se duplica entre carpetas.
- Toda IA debe consultar primero Docs antes de modificar codigo.
- Toda nueva carpeta debe tener un proposito claramente definido.
- Los nombres de carpetas y archivos no utilizan tildes, acentos ni la letra n con tilde, conforme a la directiva operativa.
- La estructura vigente se ajusta a la convencion efectivamente utilizada por el repositorio real y por los documentos de nivel.

---

# Evolucion

La estructura podra ampliarse sin romper esta organizacion base.

---

# Historial de cambios

| Version | Cambios |
|---------|---------|
| 2.0.0 | Correccion de la estructura oficial para reflejar la estructura real del repositorio (Docs/00-09, operacion/, Src/, Tests/, Assets/, Samples/, Scripts/ y Condor.slnx). Se elimina la descripcion de una estructura previa (conocimiento/, codigo/, pruebas/, herramientas/, recursos/) que nunca existio en el repositorio. La discrepancia se registra en DEUDA_EVOLUTIVA.md. |
| 1.0.0 | Version inicial con una estructura previa no materializada. |

---

Este documento forma parte del conocimiento permanente del Proyecto Condor.
