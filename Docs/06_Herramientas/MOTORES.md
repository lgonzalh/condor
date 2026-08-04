# MOTORES

Version: 1.0.0
Estado: Activo
Nivel: 06 - Construccion
Clasificacion: Arquitectura

---

# PROPOSITO

Definir los motores especializados del Proyecto Condor.

Cada motor implementa una unica responsabilidad y es invocado por el Nucleo.

---

# PRINCIPIOS

- Una responsabilidad por motor.
- Desacoplamiento del Nucleo.
- Interfaces estables.
- Independencia funcional.
- Reutilizacion.

---

# MOTORES BASE

## Planificador

Analiza la solicitud y genera el plan de ejecucion.

## Arquitecto

Valida la coherencia arquitectonica antes de cualquier modificacion.

## Implementador

Realiza los cambios autorizados.

## Revisor

Analiza la calidad del resultado obtenido.

## Validador

Verifica reglas, restricciones y consistencia.

## Documentador

Actualiza la documentacion generada por la ejecucion.

---

# CICLO

Solicitud

↓

Planificador

↓

Arquitecto

↓

Implementador

↓

Revisor

↓

Validador

↓

Documentador

↓

Resultado

---

# RESTRICCIONES

- Ningun motor controla el flujo global.
- Ningun motor modifica directamente a otro.
- Toda coordinacion pertenece al Nucleo.

---

# DEPENDENCIAS

- NUCLEO.md
- ORQUESTACION.md
- EJECUCION.md

---

# HISTORIAL DE CAMBIOS

| Version | Cambios |
|----------|----------|
| 1.0.0 | Version inicial. |
