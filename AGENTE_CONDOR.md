# AGENTE_CONDOR

Version: 1.2.0
Estado: Base operativa
Clasificacion: Contrato transversal de agentes

## Proposito

Definir el contrato comun que debe seguir cualquier herramienta agentica que trabaje sobre el repositorio de Condor.

El agente es reemplazable. El conocimiento, el estado y la trazabilidad pertenecen al proyecto.

Herramientas previstas para alternancia:
- OpenCode
- KiloCode
- Antigravity
- Trae
- Otras herramientas compatibles

## Regla principal

Un agente debe poder entrar, trabajar, verificar y salir dejando el proyecto en un estado que otro agente pueda continuar sin depender del historial de la conversacion.

## Entrada obligatoria

Antes de modificar codigo, el agente debe:

1. Leer `ESTADO_PROYECTO.md`.
2. Leer `Docs/00_Fundamentos/CONDOR_CONTEXTO_MAESTRO.md`.
3. Leer `Docs/00_Fundamentos/ADN_CONDOR.md`.
4. Leer `Docs/00_Fundamentos/DIRECTIVA_GLOBAL.md`.
5. Leer `Docs/00_Fundamentos/DIRECTIVA_OPERATIVA_PROYECTO_CONDOR.md`.
6. Leer `Docs/00_Fundamentos/PATRIMONIO_CONOCIMIENTO.md`.
7. Leer `Docs/02_Memoria/INVENTARIO_ARQUITECTURA.md`.
8. Leer `operacion/ESTADO_DESARROLLO.md`.
9. Leer `operacion/RELEVO.md`.
10. Leer `operacion/BACKLOG.md`.
11. Leer `operacion/KANBAN.md`.
12. Revisar el estado de Git antes de comenzar.

Si encuentra contradicciones entre fuentes oficiales, debe detener la modificacion afectada y registrar la discrepancia.

## Forma de trabajo

El agente debe seguir:

Comprender
→ Planificar
→ Disenar
→ Implementar
→ Verificar
→ Documentar
→ Congelar
→ Continuar

La documentacion debe ser proporcional al cambio. No se debe crear documentacion por burocracia.

## Reglas durante la implementacion

- No inventar requisitos.
- No modificar arquitectura sin registrar la decision.
- No modificar archivos no relacionados sin justificacion.
- No borrar conocimiento permanente.
- No depender de una conversacion para conservar contexto.
- Respetar la regla de trazabilidad Git (1 archivo afectado = 1 commit) definida en la seccion REGLA DE TRAZABILIDAD GIT de DIRECTIVA_OPERATIVA_PROYECTO_CONDOR.md, con su unica excepcion de cambios tecnicamente inseparables.
- Mantener la operacion local.
- Respetar las restricciones de Condor 1.0.
- Adaptar la estrategia al hardware y al modelo disponible.
- Incorporar mejores practicas cuando sean compatibles con el hardware, modelo, herramientas y beneficio real.
- Verificar antes de declarar terminado.

## Cierre obligatorio

Antes de entregar el relevo, el agente debe:

1. Ejecutar las verificaciones disponibles.
2. Registrar resultados.
3. Actualizar `operacion/ESTADO_DESARROLLO.md`.
4. Actualizar `operacion/RELEVO.md`.
5. Actualizar `operacion/BACKLOG.md`.
6. Actualizar `operacion/KANBAN.md`.
7. Actualizar `operacion/REGISTRO_CAMBIOS.md` si hubo una decision o cambio relevante (una sola vez por ciclo de trabajo, nunca por commit; su commit es el ultimo del ciclo y no se registra a si mismo).
8. Actualizar inventarios afectados.
9. Indicar la siguiente accion concreta.
10. Dejar el repositorio en un estado reproducible.

## Criterio de terminado

Una tarea no esta terminada solamente porque el codigo funciona localmente.

Esta terminada cuando:
- el objetivo esta implementado;
- existe evidencia de verificacion;
- el estado esta actualizado;
- el trabajo pendiente esta identificado;
- otro agente puede continuar sin reconstruir el contexto.

## Independencia de herramienta

Ninguna regla de este documento depende de OpenCode, KiloCode, Antigravity, Trae u otra herramienta.

La herramienta es un medio. El contrato de Condor es la referencia.
