# RELEVO

Version: 1.2.0
Estado: Activo

## Ultimo trabajo

T-003 - Recomendador de modelos.

## Estado

T-003 implementada y verificada en la rama `feature/T-003-model-recommender`, pendiente de revision e integracion en `main`.

T-002 sigue integrada y publicada en `main`:
`e558efd36f4369cfd69a04887f43cbfef9fb2136`

## Evidencia

- `condor analizar` funciona y completa los detalles reales de los 6 modelos.
- `condor recomendar` recomienda `hhao/qwen2.5-coder-tools:7b` y ordena alternativas con puntajes.
- `condor recomendar --proposito vision` degrada correctamente.
- `condor recomendar --proposito raro` rechaza el proposito invalido.
- 98 pruebas: 9 arquitectura + 40 unitarias + 49 integracion
- Determinismo: el mismo Assessment produce siempre la misma recomendacion.
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

- qwen-tools:7b (qwen2, 7.6B, Q4_K_M, tools)
- qwen3:8b (qwen3, 8.2B, Q4_K_M, tools/thinking)
- hhao/qwen2.5-coder-tools:7b (qwen2, 7.6B, Q4_K_M, tools)
- qwen2.5-coder:7b (qwen2, 7.6B, Q4_K_M, tools)
- deepseek-r1:7b (qwen2, 7.6B, Q4_K_M, thinking)
- deepseek-coder:6.7b (llama, 7B, Q4_0)

Inventario real con detalles completos tras T-003.

## Observaciones pendientes

O-1 a O-6 de T-002 quedan como deuda futura y no bloquean el avance.

Calibracion de la heuristica de memoria: los factores de ModelMemoryBudget son estimaciones iniciales; requieren mediciones reales para ajustarse.

## Siguiente tarea exacta

Integracion de T-003 en `main`; luego `operacion/TAREAS/T-004.md`.

## Regla

No borrar los modelos actuales. Son datos reales para validar el recomendador.

No hacer commit, push o merge sin autorizacion.
