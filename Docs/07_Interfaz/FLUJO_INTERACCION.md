# FLUJO_INTERACCION

Version: 2.0.0
Estado: En desarrollo
Nivel: 07 - Interfaz
Clasificacion: Flujo de Experiencia

------------------------------------------------------------------------

# Proposito

Definir el flujo de interaccion entre el usuario y Condor durante todo el
ciclo operativo de un proyecto.

El flujo representa el comportamiento esperado de Condor y no una simple
secuencia de pantallas.

------------------------------------------------------------------------

# Alcance

Aplica desde el inicio de Condor hasta la finalizacion y continuidad del
trabajo.

------------------------------------------------------------------------

# Principios

- La intencion inicia el flujo.
- El proyecto aporta el contexto.
- Condor comprende antes de actuar.
- Toda etapa comunica su estado.
- El flujo puede detenerse y continuar sin perder contexto.

------------------------------------------------------------------------

# Flujo principal

Usuario

↓

Expresa una intencion

↓

Condor identifica o descubre el proyecto

↓

Assessment del entorno

↓

Comprension del contexto

↓

Inventario del proyecto

↓

Planificacion

↓

Presentacion del plan

↓

Implementacion

↓

Verificacion

↓

Documentacion

↓

Entrega de resultados

↓

Continuidad

------------------------------------------------------------------------

# Etapas

## 1. Intencion

Entrada principal:

¿Que quieres construir?

## 2. Descubrimiento

Condor obtiene automaticamente toda la informacion posible.

## 3. Comprension

Relaciona la intencion con el contexto del proyecto.

## 4. Planificacion

Define estrategia, riesgos y acciones.

## 5. Implementacion

Ejecuta el plan aprobado.

## 6. Verificacion

Comprueba resultados y consistencia.

## 7. Documentacion

Actualiza los artefactos necesarios.

## 8. Continuidad

Preserva el estado para futuras sesiones.

------------------------------------------------------------------------

# Reglas

- Nunca implementar sin comprender.
- Nunca preguntar informacion ya conocida.
- Nunca perder el contexto del proyecto.
- Informar permanentemente el estado del proceso.

------------------------------------------------------------------------

# Criterios de aceptacion

El flujo sera valido cuando:

- Sea comprensible.
- Sea continuo.
- Sea recuperable.
- Minimice la intervencion manual.
- Mantenga trazabilidad entre etapas.

------------------------------------------------------------------------

# Historial de cambios

| Version | Cambios |
|----------|----------|
| 2.0.0 | Revision arquitectonica del Nivel 07. |
| 1.0.0 | Version inicial. |
