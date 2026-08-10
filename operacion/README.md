# OPERACION

Esta carpeta contiene el estado operativo del desarrollo de Condor y el mecanismo de continuidad entre agentes.

No sustituye:
- ADN_CONDOR.md
- DIRECTIVAS
- CONTEXTO MAESTRO
- INVENTARIOS
- DOCUMENTACION ARQUITECTONICA

Su funcion es responder:

> ¿Que esta pasando ahora y que debe hacer el siguiente agente?

## Orden de lectura para un agente

1. `../ESTADO_PROYECTO.md`
2. `../Docs/00_Fundamentos/CONDOR_CONTEXTO_MAESTRO.md`
3. `../Docs/00_Fundamentos/ADN_CONDOR.md`
4. `../Docs/00_Fundamentos/DIRECTIVA_GLOBAL.md`
5. `../Docs/00_Fundamentos/DIRECTIVA_OPERATIVA_PROYECTO_CONDOR.md`
6. `../Docs/00_Fundamentos/PATRIMONIO_CONOCIMIENTO.md`
7. `../Docs/02_Memoria/INVENTARIO_ARQUITECTURA.md`
8. `ESTADO_DESARROLLO.md`
9. `RELEVO.md`
10. `BACKLOG.md`
11. `KANBAN.md`
12. Tarea indicada por el relevo.

## Regla

Si otro agente puede continuar leyendo solamente esta carpeta y las fuentes oficiales del proyecto, el mecanismo de relevo esta funcionando.

Si necesita recuperar una conversacion para entender que hacer, el relevo esta incompleto.
