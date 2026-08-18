# AGENTE_CONDOR

Version: 3.0.0
Estado: Vigente
Modo: Evolucion Continua

## Proposito

Definir el comportamiento operativo del agente que desarrolla y mantiene Condor.

## Fuente operativa

El agente debe consultar primero la documentacion oficial del repositorio y el estado Git real.

La fuente oficial para el nivel activo es ESTADO_PROYECTO.md. Actualmente no existe nivel activo: Condor opera en Evolucion Continua.

## Reglas de continuidad

- T-001..T-012 constituyen el MVP 1.0 completado.
- T-013 y T-014 son evolucion posterior, no una redefinicion del MVP.
- No crear tareas indefinidamente sin necesidad concreta.
- No crear Nivel 10.
- No reabrir tareas congeladas salvo solicitud explicita o dependencia arquitectonica critica.
- Mantener 1 archivo afectado = 1 commit individual.
- Verificar build, pruebas, arquitectura y E2E cuando corresponda.
- Publicar cambios en main y comprobar HEAD == origin/main y working tree limpio al cierre.
- Mantener la CLI publica en espanol y sin tildes.
- Operacion local; no introducir descargas automaticas ni dependencia cloud obligatoria.

## Frontera T-014

T-014 integra la verificacion semantica T-013 en `condor avanzar`.

Debe:
- reutilizar ISemanticVerificationService;
- mantener `condor verificar-semantico` independiente;
- conservar `condor verificar` intacto;
- degradar cuando la semantica no sea aplicable;
- no introducir Architect, Guardian ni vision-en-ciclo.

## Criterio de continuidad

Al finalizar una tarea, primero determinar si el objetivo definido esta realmente completo.

No crear una nueva T por inercia. Una nueva tarea requiere:
1. necesidad concreta;
2. alcance delimitado;
3. beneficio verificable;
4. fronteras claras;
5. criterio de cierre.

## Inicio de un nuevo chat

1. Leer este documento.
2. Leer ESTADO_PROYECTO.md, ESTADO_DESARROLLO.md, BACKLOG.md, KANBAN.md e INVENTARIO_PROYECTO.md.
3. Verificar Git.
4. Identificar la tarea activa.
5. No asumir contexto no documentado.
6. Comenzar por reconocimiento si la tarea no esta formalizada.

## Comandos Condor

Los comandos operan sobre el nivel activo. Como actualmente no existe nivel activo, cualquier operacion que deba abarcar el proyecto completo requiere el sufijo `Global` cuando corresponda a las reglas del proyecto.
