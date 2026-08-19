# REGISTRO_CAMBIOS

Version: 2.1.0
Fecha de continuidad: 2026-08-19

## Cambios recientes relevantes

### Presupuesto y recursos
Se implemento presupuesto seguro y clasificacion de estado de recursos.
Se verifico un escenario con 8,2 GB disponibles y 3,7 GB de presupuesto seguro.

### Auto-setup
Se implemento el tratamiento de inventario Ollama vacio y la preparacion de un modelo viable.
Se observo la descarga real de qwen2.5-coder:3b.

### Arranque honesto
El arranque fue ajustado para no declarar operatividad cuando no existe un modelo utilizable.

### Progreso
Se integro progreso de arranque y del agente.
En ejecucion real se observaron etapas como:
- Comprendiendo
- Observando / list_dir
- Finalizando

### Integracion CLI
Se integro el flujo de arranque, versionado y experiencia del agente.

## Incidencia abierta

Se detecto una contradiccion:
- Condor informa "Modelo local listo: qwen2.5-coder:3b".
- Algunas tareas terminan con "No hay un modelo compatible disponible para la tarea".
- "hola" logra ejecutarse.

La causa todavia no esta establecida.

## Decision operativa

Suspender nuevas funcionalidades y concentrar el trabajo en esta causa raiz.

## Regla de cierre

No considerar estable el ciclo hasta reproducir y corregir la contradiccion con pruebas automatizadas y E2E.
