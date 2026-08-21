# HARNESS DE PRESUPUESTO DINÁMICO Y SELECCIÓN INTELIGENTE DE MODELOS

Version: 1.0.0
Estado: Vigente
Fecha: 2026-08-20
Nivel: Arquitectura de ejecución (motor de modelo)

## PROPOSITO

Cóndor no pregunta "¿cuál es el modelo más grande que cabe?". Razona:

> ¿Cuál es el modelo más pequeño que puede realizar eficazmente esta tarea,
> de forma eficiente, dentro del presupuesto actual y conservando una reserva
> operativa segura?

La memoria se trata como un recurso limitado con STOCK, PRESUPUESTO, RESERVA y
EVOLUCIÓN TEMPORAL. No se consume toda la memoria disponible ni se elige un
modelo solo porque "todavía cabe".

## FORMULA DE PRESUPUESTO

```
presupuesto_real = RAM_libre - reservaSistema - reservaCondor
                  - reservaOperativa - margenEstabilidad
```

- `reservaOperativa` = max(absoluto, RAM_libre * ratio). Nunca se presta al modelo.
- `presupuesto_real` >= 0 y NUNCA supera la RAM libre real (la cache no cuenta).
- `margenEstabilidad` = colchon anti-swapping para interoperar con el SO.

### Límites documentados

- Si `presupuesto_real <= 0` Cóndor NO carga modelo: reporta bloqueo TEMPORAL
  honesto (no "ausencia de modelo") y conserva la tarea.
- Un modelo DEBE caber en `presupuesto_real` Y dejar un margen residual
  (>= 10% del presupuesto o >= 0,5 GB) para no llevar el sistema a
  "RAM libre ≈ presupuesto ≈ 0".
- La reserva protege ANTES de que ocurra el bloqueo; no se confía en liberar
  después.

## SELECCIÓN POR TAREA

Para cada tarea Cóndor evalúa:

1. Qué necesita la tarea (`TaskModelRequirement`): nivel de codigo, archivos
   multiples, tool-use, salida estructurada.
2. ¿Qué modelos disponibles pueden realizarla? (suficiencia funcional).
3. ¿Cuánta RAM requiere cada modelo? (pico estimado).
4. ¿Qué margen queda tras cargarlo? (`LeavesMargin`).
5. ¿Qué tan adecuado es para la tarea y qué alternativas hay? (1- / 1+).

Reglas:

- `3B > 1.5B > 0.5B` NO es el único criterio. Se prefiere el MENOR suficiente
  (eficiencia) que conserve margen, salvo que la tarea se beneficie del mayor.
- Un modelo mayor que consuma excesivamente el presupuesto es PEOR que uno menor
  que complete la tarea eficazmente.
- Un modelo demasiado pequeño para una tarea (p. ej. sin tool-use para agente)
  se descarta aunque "quepa": NO pequeño-a-cualquier-precio.
- No hay familia favorita: Qwen, Llama, DeepSeek, Gemma, Mistral, Phi... se
  evalúan por tarea + capacidades + presupuesto, no por popularidad.
- Un modelo INSTALADO por el usuario es candidato válido aunque no sea la primera
  opción del catálogo (compatibilidad y presupuesto lo permiten).

## 1- Y 1+

- **1-** = modelo suficientemente capaz y eficiente para trabajar bajo el
  presupuesto actual (el elegido).
- **1+** = siguiente candidato razonable para cuando exista mayor margen.

Cuando la RAM cambia, Cóndor reevalúa en un punto seguro y puede pasar de 1- a 1+
(subir) o degradar, SIN interrumpir una inferencia en curso.

## REEVALUACIÓN DINÁMICA (BudgetReevaluator)

- Intervalo por defecto: **30 minutos** (configurable).
- Puntos seguros: antes de una nueva inferencia, entre acciones, al iniciar/
  finalizar tarea, o ante un cambio significativo de RAM.
- Decisiones: `KeepCurrent` / `UpgradeToNext` / `Downgrade`, con motivo.
- Límite de reevaluaciones (`maxReevaluations`): evita loops. Al agotarse,
  conserva el modelo actual (CONTINUIDAD DEL TRABAJO > CAMBIO DE MODELO).
- En una transición de modelo: libera el anterior (Ollama keep_alive=0) y
  registra el nuevo en la sesión (sin duplicar runners, sin taskkill).
- NUNCA cambia de modelo en medio de una inferencia.

## ADAPTACIÓN DEL PROMPT (ModelPromptBuilder)

El prompt del sistema se adapta al modelo SELECCIONADO (no es un texto único):

- Si soporta salida estructurada → se pide JSON estricto de acciones.
- Si NO la soporta → se pide respuesta directa en prosa (no se exige JSON).
- Si soporta tool-use → se listan las herramientas de edición/build.
- Si NO la soporta → se indica que no ejecuta herramientas externas.
- Si abarca proyecto multi-archivo → se refuerza el contexto relacional.
- Siempre indica el modelo local en uso y sus capacidades conocidas.

## INVENTARIO COMO PARTE DEL RAZONAMIENTO

`AgentInventory` incorpora: RAM total/libre, presupuesto real, reserva, reserva
operativa, modelo 1-, modelo 1+ y modelo seleccionado con motivo. No es
decorativo: alimenta la decisión y la trazabilidad del harness.

## PREPARACIÓN ANTICIPADA (1+)

Si el 1+ no está instalado y hay espacio de almacenamiento, Cóndor puede
preparar su disponibilidad SIN comprometer la reserva ni la tarea actual. La
anticipación está subordinada al harness (no se derrocha RAM por anticipar).

## CICLO DE VIDA

Se conserva lo implementado en Prompt 2 (lifecycle): una única sesión local
reutilizable, no duplicar runners, Ollama como propietario de llama-server, no
taskkill/Stop-Process/Kill, keep_alive=0 al cerrar, cierre por finally,
cancelación cooperativa. La selección dinámica no rompe este lifecycle y en cada
transición libera el anterior correctamente.

## COMPONENTES

| Componente | Ruta | Rol |
|---|---|---|
| `BudgetPolicy` | Core/Evaluation | Reserva configurable + formula + formula documentada |
| `BudgetAssessment` | Core/Models | Veredicto de stock/presupuesto/reserva |
| `TaskModelRequirement` + `TaskIntentClassifier` | Core | Qué capacidades exige la tarea |
| `ModelEfficiencyEvaluator` | Core/Evaluation | Suficiencia + eficiencia + margen |
| `ModelSelector.SelectForTask` | Core/Selection | Seleccion por tarea + 1-/1+ + instalado del usuario |
| `BudgetReevaluator` | Core/Evaluation | Reevaluacion periodica en punto seguro con limite |
| `ModelPromptBuilder` | Infrastructure/Agent | Adaptacion del prompt al modelo |
| `ModelAutoSetupService.EnsureModelForRequirementAsync` | Infrastructure/Setup | Orquesta seleccion/descarga por tarea |
| `AgentService` | Infrastructure/Agent | Wire del harness + reevaluacion en puntos seguros |

## LIMITACIONES E2E

La politica por defecto (reserva 2 GB o 25% + reservas de sistema/Condor + margen)
es conservadora a propósito. En equipos con poca RAM libre, puede reportar
bloqueo TEMPORAL incluso cuando un modelo pequeño "cabría" numéricamente; esto es
intencional (proteger la reserva antes de llegar a presupuesto≈0). La politica es
configurable via `BudgetPolicy` para ajustar el balance en despliegue.
