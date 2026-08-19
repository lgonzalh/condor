# RELEVO — EVOLUCION v1.x (T-015)

Version: 4.1.0
Estado: T-015 cerrada
Modo: Evolucion Continua
Alcance: evolucion de Condor dentro de v1.x (T-015) posterior a la base v1.0.0

## Estado real

- T-001 a T-014: completadas, publicadas y congeladas.
- Linea base tecnica **Condor v1.0.0**: cerrada y etiquetada (tag `v1.0.0`);
  no se modifica ni elimina.
- **T-015 (Automatizacion de puesta en marcha y modelo LLM local)**: tarea de
  evolucion v1.x **completada y cerrada** (evidencia funcional).
- No existe nivel activo.
- No crear Nivel 10.
- Condor opera en Evolucion Continua.

## T-015 (cerrada)

T-015 incorpora la seleccion y obtencion automatica del modelo LLM local durante
la puesta en marcha, alineada con la experiencia esperada de Condor, bajo un
presupuesto seguro de recursos:

1. Evaluar hardware real (RAM total/libre, disco, GPU, Ollama).
2. Calcular presupuesto seguro: `SafeBudgetGb = max(0, ramFree - margenOperativo)`;
   nunca se usa un porcentaje de RAM total si la libre real es menor.
3. Descartar preventivamente modelos cuyo pico (peso * 1.2 + KV) supera el
   presupuesto, aunque esten instalados.
4. Seleccionar por maxima capacidad de ingenieria dentro del presupuesto
   (no "el mas pequeno que cabe" ni "el mas potente").
5. Comprobar el inventario de Ollama.
6. Si el deseado ya existe: reutilizarlo (NO volver a descargar).
7. Si el deseado no existe y una alternativa instalada es tan capaz: reutilizar.
8. Si no existe y cabe: obtenerlo mediante Ollama, con timeout, reintentos
   limitados y verificacion posterior contra `/api/tags`.
9. Si falla o no hay viable: degradar de forma segura y explicita, sin estado
   inconsistente.
10. Continuar el flujo una vez disponible.

Flujo objetivo: Assessment -> evaluar hardware -> presupuesto seguro -> descartar
inviables -> seleccion por capacidad -> comprobar inventario -> (reutilizar |
obtener -> verificar) -> actualizar Assessment/estado -> continuar.

Extiende `condor preparar` y el agente (`condor hacer`) de forma aditiva; no rompe
los comandos existentes.

## Validacion de T-015

- Build Release: 0 errores, 0 advertencias.
- Unitarias (Core): 197/197 (incluye ModelMemoryBudget corregido, ModelSelector
  por capacidad, catalogo de variantes).
- Arquitectura: 22/22 (incluye ModelSelection y agente puro).
- Integracion (Infra): 177/177 (+ ModelAutoSetup, + Retry, + agente).
- Descarte preventivo: con RAM libre ~4.9 GB, un 7B (pico ~5.2) no es elegible
  pese a estar instalado; `qwen2.5-coder:3b` si lo es.
- E2E REAL: `condor hacer` sin `--modelo` sobre un proyecto con defecto real
  selecciono `qwen2.5-coder:3b`, obtenido automaticamente; el agente hizo
  read -> edit -> build -> test y el harness confimo build y pruebas; la
  verificacion externa `dotnet test` resulto 2/2. Exito de origen externo.
- Prueba de reutilizacion: con el modelo instalado, no se vuelve a descargar.
- Degradaciones y reintentos: cubiertas por pruebas de integracion y RetryPolicy.

## Frontera v1.x

- No se modifica ni elimina el tag `v1.0.0`.
- No se reabre el cierre del MVP 1.0 ni se altera su alcance historico.
- No se incorporan Architect, Guardian ni vision-en-ciclo.
- La obtencion automatica del modelo es una evolucion v1.x, no un cambio de la
  base v1.0.0.

## Regla de limite

La evolucion v1.x se gestiona mediante tareas explicitamente justificadas y
cerrables; no se expande indefinidamente el backlog.

## Nota de cierre (T-015)

- La E2E demuestra un ciclo agente real sobre un defecto puntual; no constituye
  una garantia de resolucion universal de cualquier proyecto o tarea.
- El 7B instalado fue descartado por presupuesto de memoria; `qwen2.5-coder:3b`
  fue seleccionado por capacidad de ingenieria dentro del presupuesto seguro.
- Vinculo: `operacion/TAREAS/T-015.md` (version 2.1.0, cerrada).
