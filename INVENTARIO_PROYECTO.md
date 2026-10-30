# INVENTARIO_PROYECTO

Version: 1.3.0
Estado: Vigente
Nivel: Global
Fecha: 2026-10-30

## PROPOSITO

Indice de continuidad de los artefactos principales utilizados para mantener Condor entre chats. No sustituye el inventario maestro documental ni el inventario tecnico del repositorio.

## DOCUMENTOS DE CONTINUIDAD

| Documento | Ruta actual | Estado o proposito |
|---|---|---|
| CONDOR_CONTEXTO_MAESTRO.md | `Docs/00_Fundamentos/CONDOR_CONTEXTO_MAESTRO.md` | Fuente maestra del contexto del proyecto |
| AGENTE_CONDOR.md | `AGENTE_CONDOR.md` | Directiva vigente para el agente |
| ESTADO_PROYECTO.md | `ESTADO_PROYECTO.md` | Estado oficial del proyecto |
| ESTADO_DESARROLLO.md | `operacion/ESTADO_DESARROLLO.md` | Estado operativo de desarrollo |
| BACKLOG.md | `operacion/BACKLOG.md` | Trabajo pendiente y priorizado |
| KANBAN.md | `operacion/KANBAN.md` | Tablero operativo; la referencia metodologica esta en `Docs/00_Fundamentos/KANBAN.md` |
| REGISTRO_CAMBIOS.md | `operacion/REGISTRO_CAMBIOS.md` | Historial operativo de cambios |
| RELEVO.md | `operacion/RELEVO.md` | Continuidad para el siguiente agente |
| REGISTRO_DEUDA_ARQUITECTONICA.md | `Docs/01_Arquitectura_Ejecutable/REGISTRO_DEUDA_ARQUITECTONICA.md` | Deuda arquitectonica vigente |
| PATRIMONIO_CONOCIMIENTO.md | `Docs/00_Fundamentos/PATRIMONIO_CONOCIMIENTO.md` | Trazabilidad de conocimiento y fuentes historicas |
| INVENTARIO_PROYECTO.md | `Docs/00_Fundamentos/INVENTARIO_PROYECTO.md` | Inventario maestro de documentos permanentes |
| INVENTARIO_FUNCIONAL.md | `Docs/02_Memoria/INVENTARIO_FUNCIONAL.md` | Inventario de capacidades funcionales |
| INVENTARIO_ARQUITECTURA.md | `Docs/02_Memoria/INVENTARIO_ARQUITECTURA.md` | Inventario de componentes y estado arquitectonico |
| INVENTARIO_TECNICO_REPO.md | `operacion/INVENTARIO_TECNICO_REPO.md` | Snapshot tecnico del repositorio; no reemplaza los inventarios oficiales |

Este indice mantiene su funcion de continuidad. El inventario maestro conserva su nombre y ruta bajo `Docs/00_Fundamentos/`; el snapshot tecnico conserva su ruta bajo `operacion/`.

## FUENTES HISTORICAS

Los cuadernos son transcripciones historicas y no sustituyen el estado actual documentado. Sus rutas y trazabilidad son:

| Fuente | Ruta actual | Enfoque historico |
|---|---|---|
| Cuaderno I | `assets/cuaderno I.txt` | Origen, vision, persistencia del conocimiento y proposito del proyecto |
| Cuaderno II | `assets/cuaderno II.txt` | Recuperacion del contexto y separacion de reglas globales, locales y temporales |
| Cuaderno III | `assets/cuaderno III.txt` | Revision documental, calidad, validacion y limites de entrega |
| Cuaderno IV | `assets/cuaderno IV.txt` | Revision global y restricciones declaradas para Condor 1.x |
| Cuaderno V | `assets/cuaderno V.txt` | Experiencia operativa, descubrimiento e interaccion del Nivel 07 |

El detalle de ideas, decisiones y estado de incorporacion se mantiene en `Docs/00_Fundamentos/PATRIMONIO_CONOCIMIENTO.md`. Los cuadernos permanecen en `assets/`; son fuentes historicas, no documentos normativos.

## ESTADO DEL SOFTWARE

La linea actual incluye:
- infraestructura de agente;
- deteccion y seleccion de modelos locales;
- presupuesto seguro;
- preparacion e instalacion de dependencias;
- progreso de CLI;
- ciclo de desarrollo asistido;
- experiencia de terminal para Windows.

El estado vigente y sus prioridades se consultan en `ESTADO_PROYECTO.md` y `operacion/ESTADO_DESARROLLO.md`.

## REGLA

El nuevo chat debe tomar los documentos actuales y el Git real como fuente operativa, no reconstruir el proyecto desde conversaciones antiguas. Este indice orienta la continuidad; el inventario maestro registra los documentos y `PATRIMONIO_CONOCIMIENTO.md` conserva la procedencia historica.

## HISTORIAL DE CAMBIOS

| Version | Cambios |
|---|---|
| 1.3.0 | Se explicitan rutas y propositos de continuidad, se distinguen los inventarios y se registran las rutas y temas de los Cuadernos I-V en `assets/`. |
| 1.2.0 | Indice de continuidad vigente del proyecto. |
