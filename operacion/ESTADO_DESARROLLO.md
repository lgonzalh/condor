# ESTADO_DESARROLLO

Version: 2.1.0
Estado: Activo
Modo: Evolucion Continua
MVP: Condor 1.0
Fecha: 2026-08-19

## CAPACIDADES VERIFICADAS

- Ejecucion local en Windows.
- Integracion con Ollama.
- Deteccion de modelos.
- Auto-preparacion/descarga en escenario sin modelo.
- Evaluacion de presupuesto seguro.
- Seleccion/fallback de modelos en parte del flujo.
- Progreso visual durante tareas.
- Ejecucion de herramientas como list_dir.
- Resultado estructurado del ciclo.

## PRUEBA ACTUAL

Modelo:
qwen2.5-coder:3b

Presupuesto observado:
8,2 GB disponibles / 3,7 GB seguros / Normal.

Resultado:
- "hola" funciona.
- "que modelo eres?" falla con ausencia de modelo compatible.
- Analisis de archivos tambien puede fallar con ausencia de modelo compatible.

## DIAGNOSTICO ACTIVO

Investigar exclusivamente la ruta:

intencion
-> routing
-> requisitos de capacidad
-> seleccion de modelo
-> AgentService
-> AgentEngine
-> Ollama

Hipotesis operativa:
el modelo existe y puede utilizarse, pero una condicion de compatibilidad/routing lo rechaza para ciertas intenciones.

No asumir la causa antes de reproducirla en codigo.

## TEST DE ACEPTACION INMEDIATO

A. Sin modelo local:
- Condor detecta ausencia.
- Calcula presupuesto.
- Descarga modelo viable.
- Verifica instalacion.
- Continua al ciclo.

B. Con qwen2.5-coder:3b:
- "hola" funciona.
- "que modelo eres?" funciona o produce un resultado coherente.
- una tarea de lectura/análisis de archivos llega a la ejecucion del modelo.
- no aparece falsamente "No hay un modelo compatible disponible".

C. Progreso:
- muestra etapas reales.
- no es solamente decorativo.
- refleja iteraciones/acciones reales.

D. Presupuesto:
- el modelo elegido debe respetar el presupuesto.
- no debe descargar un modelo inviable.
