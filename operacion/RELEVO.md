# RELEVO

Version: 1.1.0
Estado: Activo

## Ultimo trabajo

T-002 - Integracion local con Ollama.

## Estado

T-002 completada, revisada, probada, integrada en `main` y publicada.

Commit T-002:
`dfaa5d6105c682e7b692f85350db22a892fdac11`

Merge T-002:
`e558efd36f4369cfd69a04887f43cbfef9fb2136`

## Evidencia

- `condor assess` funciona en el equipo real.
- `condor ask "di hola"` funciona.
- `condor ask "di hola" --model qwen3:8b` funciona.
- Ollama detenido produce degradacion clara.
- Assessment corrupto produce error controlado.
- Condor no requiere internet; Ollama se consume por loopback.

## Entorno real observado

- Windows 11 Home Single Language.
- Intel Core Ultra 7 255U.
- 12 nucleos / 14 hilos.
- 15,4 GB RAM.
- Intel Graphics.
- Aproximadamente 111 GB libres en C:.
- Ollama 0.31.1.
- 6 modelos locales.

## Modelos observados

- qwen-tools:7b
- qwen3:8b
- hhao/qwen2.5-coder-tools:7b
- qwen2.5-coder:7b
- deepseek-r1:7b
- deepseek-coder:6.7b

Son inventario real, no recomendacion definitiva.

## Observaciones pendientes

O-1 a O-6 de T-002 quedan como deuda futura y no bloquean el avance.

## Siguiente tarea exacta

`operacion/TAREAS/T-003.md`

Rama prevista:

`feature/T-003-model-recommender`

## Regla

No borrar los modelos actuales antes de T-003. Son datos reales para validar el recomendador.

No elegir manualmente un modelo como "el mejor" antes de ejecutar el recomendador.

No hacer commit, push o merge sin autorizacion.
