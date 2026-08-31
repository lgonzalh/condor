# HARNESS DE PRESUPUESTO DINÁMICO Y SELECCIÓN INTELIGENTE DE MODELOS

Version: 1.1.0
Estado: Vigente
Fecha: 2026-08-25
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
presupuesto_real = RAM_libre - reservaSistema(1.5 GB) - reservaCondor(1.5 GB) - margenOperativo(min 1.5 GB)
```

- `margenOperativo` = min(3, max(1.5, RAM_total * 0.08)). Combina la reserva
  operativa de seguridad y el margen de estabilidad en un unico valor basado en
  la RAM TOTAL (no en la libre). Se obtiene de
  `ModelMemoryBudget.OperatingMarginGb(totalGb)`, manteniendo coincidencia
  entre `BudgetPolicy.Assess` y `ModelMemoryBudget.Snapshot`.

- Las tres componentes cada una tienen un PISO de ~1.5 GB: `reservaSistema = 1.5`,
  `reservaCondor = 1.5` y `margenOperativo = max(1.5, ...)`. En equipos pequenos esto
  suma ~4.5 GB de margen minimo garantizado.
- `presupuesto_real` >= 0 y NUNCA supera la RAM libre real (la cache no cuenta).
- El margen operativo NUNCA se presta al modelo.

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

El modelo seleccionado (1-) permanece fijo durante toda la tarea. Cóndor ya no
reevalúa ni cambia de modelo entre 1- y 1+ durante la ejecución: esto garantiza
continuidad y evita loops de reevaluación. La transición 1-/1+ ocurre una sola
vez al inicio, basada en el presupuesto de memoria real.

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
| `ModelPromptBuilder` | Infrastructure/Agent | Adaptacion del prompt al modelo |
| `ModelAutoSetupService.EnsureModelForRequirementAsync` | Infrastructure/Setup | Orquesta seleccion/descarga por tarea |
| `AgentService` | Infrastructure/Agent | Wire del harness: modelo fijo para toda la tarea |

## LIMITACIONES E2E

La politica por defecto usa `margenOperativo = OperatingMarginGb(RAM_total)`
= min(3, max(1.5, RAM_total * 0.08)), que combina la reserva operativa y el
margen de estabilidad. En equipos con poca RAM libre, puede reportar bloqueo
TEMPORAL incluso cuando un modelo pequeño "cabría" numéricamente; esto es
intencional (proteger el margen antes de llegar a presupuesto≈0). La politica es
configurable vía `BudgetPolicy` para ajustar el balance en despliegue.
