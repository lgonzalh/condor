# RELEVO — EVOLUCION v1.x (T-015)

Version: 4.0.0
Estado: Preparado
Modo: Evolucion Continua
Alcance: evolucion de Condor dentro de v1.x (T-015) posterior a la base v1.0.0

## Estado real

- T-001 a T-014: completadas, publicadas y congeladas.
- Linea base tecnica **Condor v1.0.0**: cerrada y etiquetada (tag `v1.0.0`);
  no se modifica ni elimina.
- **T-015 (Automatizacion de puesta en marcha y modelo LLM local)**: tarea de
  evolucion v1.x en curso.
- No existe nivel activo.
- No crear Nivel 10.
- Condor opera en Evolucion Continua.

## T-015 (en curso)

T-015 incorpora la obtencion automatica del modelo LLM local durante la puesta
en marcha, alineada con la experiencia esperada de Condor:

1. Evaluar hardware y determinar capacidad.
2. Seleccionar un modelo compatible del catalogo (determinista, en Core).
3. Comprobar el inventario de Ollama.
4. Si el modelo deseado ya existe: reutilizarlo (NO volver a descargar).
5. Si no existe: obtenerlo mediante Ollama, con timeout, reintentos limitados y
   verificacion posterior contra `/api/tags`.
6. Si falla: degradar de forma segura y explicita, sin dejar estado inconsistente.
7. Continuar el flujo una vez disponible.

Flujo objetivo: Assessment -> evaluar hardware -> seleccion -> comprobar inventario
-> (reutilizar | obtener -> verificar) -> actualizar Assessment/estado -> continuar.

Extiende `condor preparar` de forma aditiva; no rompe los comandos existentes.

## Validacion de T-015

- Build Release: 0 errores, 0 advertencias.
- Unitarias (Core): 186/186 (+ ModelSelector).
- Arquitectura: 20/20 (+ ModelSelection).
- Integracion (Infra): 177/177 (+ ModelAutoSetup, + Retry).
- Prueba real desde 0 modelos: `condor preparar` selecciono `llama3.2:3b`,
  lo obtuvo via Ollama, verifico instalacion y permitio inferencia real.
- Prueba de reutilizacion: con el modelo instalado, `condor preparar` no vuelve a
  descargar (se reutiliza).
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
