# RELEVO

Fecha: 2026-10-30
Tipo: Continuidad de chat
Estado: Software estable al corte 2026-10-19; Graft semantico pendiente de API

## ESTADO VIGENTE

- Condor: el usuario confirma build y pruebas correctas al 2026-10-19 (0 errores, 0 advertencias; 664/664).
- T-001 a T-016: cerradas; T-002 ya estaba integrada en `main` aunque su ficha seguia en revision.
- El problema historico de compatibilidad/seleccion de modelo y presupuesto quedo resuelto al 2026-10-19.
- Graft: cableado estructural sincronizado; capa semantica 0%, 3.088 nodos pendientes. No hay `GRAFT_API_KEY` ni `OPENAI_API_KEY` en Process/User/Machine.
- Siguiente paso: el usuario configura una API key de OpenAI Platform localmente como `GRAFT_API_KEY`; despues se ejecutan `graft build --deep` y `graft check`.
- No solicitar ni registrar la clave en chat o repositorio. No ejecutar Ollama para Graft; Ollama se mantiene como integracion del producto Condor cuando el usuario ejecuta sus pruebas.
- Los Cuadernos I-V permanecen en `assets/` y no se modificaron.

Lo que sigue es el relevo historico de agosto, conservado para trazabilidad. Sus incidencias se consideran cerradas con la evidencia del 2026-10-19.

> **Nota de vigencia 2026-10-19.** Documento historico de continuidad. El estado
> vigente es: build 0w/0e, suite 664/664, UTF-8 sin mojibake, TUI `beta` y
> lanzador global unico en `C:\GitHub\condor`. Ver `ESTADO_DESARROLLO.md` e
> `INVENTARIO_TECNICO_REPO.md`.

## CONTEXTO

El chat anterior se volvio demasiado extenso. Se cerro la operacion con varios agentes y se decidio continuar con un unico agente integrador.

## ESTADO HISTORICO (2026-08-19)

El usuario ya realizo push de los commits existentes.

El software:
- compila;
- arranca;
- puede descargar qwen2.5-coder:3b;
- calcula presupuesto seguro;
- muestra progreso;
- puede ejecutar herramientas;
- puede completar "hola".

## PROBLEMA REPRODUCIBLE EN 2026-08-19 (CERRADO 2026-10-19)

Con qwen2.5-coder:3b listo y presupuesto Normal:
- "hola" funciona;
- "que modelo eres?" puede terminar con "No hay un modelo compatible disponible para la tarea";
- una tarea de lectura/análisis de archivos puede terminar con el mismo mensaje.

Esto indica una inconsistencia en la ruta de seleccion/compatibilidad del modelo.

## INSTRUCCION HISTORICA YA ENTREGADA AL AGENTE

Investigar unicamente por que qwen2.5-coder:3b, que ya fue descargado, detectado, seleccionado y utilizado exitosamente para "hola", posteriormente produce "No hay un modelo compatible disponible para la tarea" para otras entradas.

Debe:
1. reproducir ambos casos;
2. localizar exactamente donde se pierde/rechaza el modelo;
3. revisar routing -> seleccion -> AgentService/AgentEngine;
4. crear pruebas que reproduzcan la diferencia;
5. no modificar presupuesto, auto-setup, progreso, CLI o documentacion mientras no exista dependencia directa;
6. no hacer cambios especulativos.

## EVIDENCIA DE CONSOLA

- Modelo descargado: qwen2.5-coder:3b
- Recursos: 8,2 GB disponibles
- Presupuesto seguro: 3,7 GB
- Estado: Normal
- Progreso observado: Comprendiendo -> Observando/list_dir -> Finalizando
- "hola": OK
- Otras intenciones: rechazo por "No hay un modelo compatible disponible para la tarea"

## REGLA DEL NUEVO CHAT

No reiniciar el proyecto.
No abrir varios agentes.
No rehacer lo ya verificado.
Primero diagnosticar la causa raiz actual.

## PRIMER PASO

Leer:
AGENTE_CONDOR.md
ESTADO_PROYECTO.md
ESTADO_DESARROLLO.md
BACKLOG.md
KANBAN.md
INVENTARIO_PROYECTO.md
REGISTRO_CAMBIOS.md
RELEVO.md

Luego revisar Git real y continuar la investigacion activa.
