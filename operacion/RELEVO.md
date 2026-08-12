# RELEVO

Version: 1.6.0
Estado: Activo

## Ultimo trabajo

T-003 - Recomendador de modelos (cerrada e integrada).
T-004 - Descubrimiento de proyecto (completada, congelada e integrada mediante PR #2).

## Estado

T-003 completada, verificada e integrada en `main` mediante PR #1.

Merge commit:
`12a3c5b031da00f36d32a6f66322bcc1392573d9`

T-004 completada, verificada e integrada en `main` mediante PR #2 (merge `a90366338678988ec0a13fdf636bf72dc921dfd8`).

Estado actual de `main`:
`a90366338678988ec0a13fdf636bf72dc921dfd8`

## Evidencia

- `condor analizar` funciona y completa los detalles reales de los 6 modelos.
- `condor recomendar` recomienda `hhao/qwen2.5-coder-tools:7b` y ordena alternativas con puntajes.
- `condor recomendar --proposito vision` degrada correctamente.
- `condor recomendar --proposito raro` rechaza el proposito invalido.
- 98 pruebas: 9 arquitectura + 40 unitarias + 49 integracion
- T-004: 174/174 pruebas (10 arquitectura + 85 unitarias + 79 integracion); build sin errores ni advertencias; E2E sobre el repositorio real, proyecto sin Git, proyecto vacio y manifiesto con BOM UTF-8
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

## Funcionalidades disponibles

- `condor analizar [--json]`: analiza el entorno, persiste el Assessment e incluye el descubrimiento del proyecto local (seccion PROYECTO y campo `project`).
- `condor consultar "<mensaje>" [--modelo <modelo>]`: inferencia local mediante Ollama.
- `condor recomendar [--proposito desarrollo|general|vision]`: recomendacion de modelo local.
- `condor version`, `condor ayuda`, alias `-h/--help/-v/--version`.

## Funcionalidades pendientes

- T-005 Context Engine inicial (pendiente; no iniciada).
- T-006 Flujo de intencion → plan.
- T-007 Builder inicial.
- T-008 Verificacion inicial.
- T-009 Documentacion y continuidad.
- T-010 Capacidades avanzadas de desarrollo.
- T-011 Vision local.
- T-012 Instalador/puesta en marcha simplificada.

## Siguiente tarea exacta

T-005 - Context Engine inicial (pendiente; no iniciada).

## Regla

No borrar los modelos actuales. Son datos reales para validar el recomendador.

No hacer commit, push o merge sin autorizacion.
