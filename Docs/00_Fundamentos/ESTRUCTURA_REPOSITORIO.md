# ESTRUCTURA_REPOSITORIO

Version: 2.0.2
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
├── AGENTS.md
├── .claude/
│   ├── helpers/
│   ├── skills/
│   └── settings.json
├── .ignore
├── .mcp.json
├── opencode.json
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
│   ├── ESTADO_DESARROLLO.md
│   ├── BACKLOG.md
│   ├── KANBAN.md
│   ├── REGISTRO_CAMBIOS.md
│   ├── RELEVO.md
│   ├── INVENTARIO_TECNICO_REPO.md
│   └── TAREAS/
│
├── assets/
│   ├── cuaderno I.txt
│   ├── cuaderno II.txt
│   ├── cuaderno III.txt
│   ├── cuaderno IV.txt
│   └── cuaderno V.txt
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

Contiene el estado operativo del desarrollo, el snapshot tecnico y el mecanismo de continuidad entre agentes. Sus archivos conservan sus rutas actuales. `operacion/KANBAN.md` es el tablero operativo; `Docs/00_Fundamentos/KANBAN.md` conserva la referencia metodologica.

## assets

Fuentes historicas transcritas de los Cuadernos I-V. Se conservan como material de origen; la trazabilidad y el estado de incorporacion viven en `Docs/00_Fundamentos/PATRIMONIO_CONOCIMIENTO.md`.

## Configuracion local de herramientas

`AGENTS.md`, `.claude/`, `.ignore`, `.mcp.json` y `opencode.json` conservan la configuracion de Graft y herramientas compatibles. Se mantienen en sus rutas actuales y estan versionados desde el commit `25af837` (2026-10-30); describen herramientas de desarrollo, no componentes normativos del producto Condor.

## Samples

Ejemplos de uso y proyectos de muestra.

## Scripts

Scripts de apoyo al desarrollo y a la operacion.

## Src

Implementacion del sistema Condor (proyectos .NET del MVP 1.0).

## Tests

Pruebas unitarias, de integracion y de arquitectura.

## Archivos de raiz

`AGENTE_CONDOR.md`, `ESTADO_PROYECTO.md` e `INVENTARIO_PROYECTO.md` permanecen en la raiz porque funcionan como entrada, estado global e indice de continuidad. `README.md`, `LICENSE` y `NOTICE` mantienen alli sus funciones publicas y legales.

---

# Reglas

- La documentacion formal por niveles se almacena en `Docs/`.
- Los archivos globales de entrada y continuidad permanecen en la raiz; el estado operativo permanece en `operacion/`; las fuentes historicas permanecen en `assets/`.
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
| 2.0.2 | Se corrige el estado Git de la configuracion Graft: los archivos estan versionados desde `25af837`; se mantienen rutas y propositos. |
| 2.0.1 | Se documentan las rutas actuales de `operacion/`, `assets/` y la configuracion local de herramientas; se aclaran los propositos de los archivos de raiz y se precisan las ubicaciones de los Cuadernos I-V sin mover ni renombrar archivos. |
| 2.0.0 | Correccion de la estructura oficial para reflejar la estructura real del repositorio (Docs/00-09, operacion/, Src/, Tests/, Assets/, Samples/, Scripts/ y Condor.slnx). Se elimina la descripcion de una estructura previa (conocimiento/, codigo/, pruebas/, herramientas/, recursos/) que nunca existio en el repositorio. La discrepancia se registra en DEUDA_EVOLUTIVA.md. |
| 1.0.0 | Version inicial con una estructura previa no materializada. |

---

Este documento forma parte del conocimiento permanente del Proyecto Condor.
