# KANBAN

Version: 2.1.0
Estado: Activo
Nivel: Global
Fecha: 2026-08-19

## EN PROGRESO

Sin incidencias activas de seleccion de modelo.

## LISTO / VERIFICADO PARCIALMENTE

- Estabilizacion del agente: causa raiz de la RAM fluctuante identificada y corregida.
- Promesa fundamental de inicio: con modelos instalados pero RAM baja, la sesion arranca
  igual (no se bloquea); Condor decide el modelo en cada tarea con recuperacion acotada.
- Intervencion opcional de RAM: si tras evaluar no hay modelo viable, Condor informa,
  sugiere liberar memoria (Opcion S/N, nunca forzada) y, si el usuario confirma, reevalua
  y continua; si no, sale limpio conservando la tarea.
- Progreso visible obligatorio durante todo el inicio: pantalla nunca se congela (banner,
  spinner y etapas "Revisando recursos"/"Evaluando modelos"/"Preparando modelo" hasta listo).
- Busqueda de salida viable: el catalogo ahora incluye alternativas menores (1.5B/1B/0.5B);
  si el modelo instalado no cabe, Condor busca y usa/descarga la alternativa menor viable
  antes de pedir intervencion.
- Build sin errores/advertencias en el ultimo cierre informado.
- Suites automatizadas verdes en el ultimo cierre informado (534 pruebas).
- Descarga de qwen2.5-coder:3b observada.
- Presupuesto seguro observado.
- Progreso visual observado (arranque y agente; nunca pantalla en negro).
- Ejecucion de herramientas observada.
- "hola" ejecutado correctamente.
- E2E real: tarea con RAM suficiente, bloqueo honesto con RAM insuficiente, recuperacion
  posterior, e inicio no-bloqueante con modelos instalados y RAM baja.

## PENDIENTE

- Repetir prueba cliente incognito completa.
- Verificar seleccion por capacidad de ingenieria.
- Verificar que el progreso represente el ciclo real.
- Actualizar documentacion oficial una vez que el comportamiento este validado.

## BLOQUEO ACTUAL

Resuelto: ya no existe la contradiccion de "modelo listo" seguido de "no hay modelo
compatible" para tareas que deberian ejecutarse. Cuando la RAM libre no alcanza el
presupuesto seguro, Condor informa un bloqueo TEMPORAL de recursos de forma honesta
y conserva la tarea. Ademas, el inicio ya no se bloquea cuando HAY modelos instalados:
la sesion arranca, se explica la RAM con honestidad y Condor decide/recupera el modelo
en cada tarea.

## SIGUIENTE MEJOR ACCION

Diagnostico de una sola causa raiz con un solo agente integrador (completado).

## DEFINITION OF DONE

- causa identificada (cubierta);
- correccion minima (cubierta);
- pruebas de regresion (cubiertas);
- prueba E2E real (cubierta);
- documentacion actualizada;
- commit limpio;
- push autorizado;
- estado Git verificado.
