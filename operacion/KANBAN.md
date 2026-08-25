# KANBAN

Version: 2.1.0
Estado: Activo
Nivel: Global
Fecha: 2026-08-19

## EN PROGRESO

Sin incidencias activas de seleccion de modelo.

## LISTO / VERIFICADO PARCIALMENTE

- Harness de presupuesto dinamico y seleccion inteligente: RAM como stock + reserva + presupuesto
  + 1-/1+ + seleccion por tarea. Reserva operativa configurable (`BudgetPolicy`), seleccion por
  suficiencia/eficiencia (no tamaño), reevaluacion en punto seguro con limite, adaptacion del
  prompt al modelo, modelo instalado del usuario como candidato. Pruebas 17 harness + 4 prompt.
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
- Analisis y orquestacion: se muestra inventario del entorno + motivo/capacidades del modelo,
  y se separa [HALLAZGOS] (evidencia) de [RESULTADO] (analisis) sin duplicacion.
- ADN conversacional y generalizacion: la respuesta final es natural (sin etiquetas tecnicas
  obligatorias), agnostica de ecosistema (no asume .NET) y termina con
  '©Condor · <modelo> · <tiempo>'.
- Identidad permanente y diferenciacion del origen: "©Condor" + eslogan se mantienen visibles
  desde el inicio hasta la respuesta; se muestra el modelo realmente utilizado y se colorea
  por origen (Cóndor azul, modelo gris, error rojo, advertencia amarillo).
- Identidad como zona persistente: la identidad se re-dibuja como zona fija antes de cada
  espera de entrada y permanece visible todo el ciclo (inicio, tarea, respuesta, error,
  finalizacion y nueva espera); muestra el modelo local REAL activo.
- Build sin errores/advertencias en el ultimo cierre informado.
- Suites automatizadas verdes en el ultimo cierre informado (534 pruebas).
- Descarga de qwen2.5-coder:3b observada.
- Presupuesto seguro observado.
- Progreso visual observado (arranque y agente; nunca pantalla en negro).
- Ejecucion de herramientas observada.
- "hola" ejecutado correctamente.
- E2E real: tarea con RAM suficiente, bloqueo honesto con RAM insuficiente, recuperacion
  posterior, e inicio no-bloqueante con modelos instalados y RAM baja.

- **T-018 CORRECCIONES FINALES TUI PRODUCCION**: identidad visual final (mascota centrada, contraste aprobado, cabecera unica, comentarios, comunicacion directa, placeholder, arranque optimizado). Verificacion real sobre condor.exe publicado: arranque <400 ms, sesion con modelo real, comentario/ayuda/salir funcionando, 0 regresiones. Build 0w/0e. 17 fallos preexistentes (15 Core + 2 Infra) — 0 regresiones.

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
