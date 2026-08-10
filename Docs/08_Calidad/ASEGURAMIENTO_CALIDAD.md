# ASEGURAMIENTO_CALIDAD

Version: 1.0.0
Estado: En desarrollo
Nivel: 08 - Calidad
Clasificacion: Aseguramiento de Calidad

---

# PROPOSITO

Definir las practicas preventivas y de control mediante las cuales Condor mantiene y mejora la calidad durante todo el ciclo de vida del proyecto.

El aseguramiento de calidad busca prevenir defectos, detectar desviaciones tempranamente y mantener condiciones que permitan producir resultados conformes de forma repetible.

---

# ALCANCE

Este documento aplica a:

- procesos de desarrollo;
- arquitectura;
- codigo;
- documentacion;
- configuracion;
- pruebas;
- validacion;
- herramientas;
- integraciones;
- entregables;
- cambios;
- practicas de trabajo del proyecto.

El aseguramiento debe aplicarse de manera proporcional al riesgo, impacto y madurez del resultado.

---

# PRINCIPIO CENTRAL

La calidad no debe depender unicamente de encontrar errores al final.

Condor debe establecer condiciones que reduzcan la probabilidad de producir errores y permitan detectarlos antes de que se propaguen.

---

# OBJETIVOS

- Prevenir defectos.
- Mantener procesos consistentes.
- Detectar desviaciones tempranamente.
- Reducir retrabajo.
- Mantener la arquitectura.
- Mantener la documentacion sincronizada.
- Mejorar progresivamente las practicas de ingenieria.
- Facilitar la automatizacion de controles repetibles.
- Conservar evidencia de las decisiones y resultados relevantes.

---

# PRINCIPIOS DE ASEGURAMIENTO

## 1. Prevencion antes que correccion

Cuando sea posible, Condor debe evitar que un defecto llegue a implementacion en lugar de depender exclusivamente de detectarlo posteriormente.

## 2. Calidad integrada

El aseguramiento forma parte del desarrollo y no constituye una actividad aislada posterior.

## 3. Evidencia

Las afirmaciones relevantes sobre calidad deben poder sustentarse mediante evidencia.

## 4. Consistencia

Las practicas repetibles deben mantenerse consistentes entre iteraciones.

## 5. Mejora continua

Los hallazgos deben utilizarse para mejorar procesos, criterios, pruebas y controles.

## 6. Proporcionalidad

El nivel de control debe corresponder al riesgo y al impacto del resultado.

## 7. Independencia suficiente

Cuando sea necesario, una validacion o revision debe realizarse con una perspectiva diferente a la que produjo el resultado.

---

# ASEGURAMIENTO DURANTE EL CICLO

El aseguramiento acompana el ciclo:

Comprender

↓

Planificar

↓

Disenar

↓

Implementar

↓

Verificar

↓

Documentar

↓

Congelar

↓

Continuar

En cada etapa pueden aplicarse controles preventivos y de deteccion.

---

# CONTROLES EN COMPRENDER

Antes de iniciar una implementacion debe verificarse:

- objetivo comprendido;
- alcance identificado;
- restricciones conocidas;
- dependencias identificadas;
- informacion suficiente disponible;
- ambiguedades relevantes detectadas.

No debe implementarse una solucion cuando la falta de comprension pueda producir un resultado incorrecto.

---

# CONTROLES EN PLANIFICAR

La planificacion debe comprobar:

- tareas coherentes con el objetivo;
- dependencias consideradas;
- riesgos identificados;
- resultado esperado definido;
- criterios de aceptacion previstos;
- estrategia de verificacion considerada.

---

# CONTROLES EN DISENAR

El diseno debe comprobar:

- responsabilidades;
- interfaces;
- contratos;
- dependencias;
- impactos;
- compatibilidad con la arquitectura;
- estrategia de pruebas;
- estrategia de manejo de errores.

Las decisiones relevantes deben quedar registradas cuando tengan impacto permanente.

---

# CONTROLES EN IMPLEMENTAR

Durante la implementacion se debe controlar:

- alcance del cambio;
- respeto de arquitectura;
- coherencia con el diseno;
- manejo de errores;
- calidad del codigo;
- configuracion;
- dependencias;
- ausencia de cambios no justificados.

---

# CONTROLES EN VERIFICAR

La verificacion debe comprobar:

- pruebas aplicables ejecutadas;
- criterios evaluados;
- defectos registrados;
- regresiones consideradas;
- evidencia disponible;
- resultados interpretables.

---

# CONTROLES EN DOCUMENTAR

La documentacion debe comprobar:

- correspondencia con el estado real;
- version;
- estructura;
- referencias;
- decisiones relevantes;
- trazabilidad;
- ausencia de contradicciones conocidas.

La documentacion forma parte del aseguramiento porque una documentacion incorrecta puede producir errores futuros aunque el codigo actual funcione.

---

# CONTROLES EN CONGELAR

Antes del congelamiento debe comprobarse:

- criterios obligatorios cumplidos;
- no conformidades criticas resueltas;
- pruebas obligatorias ejecutadas;
- documentacion actualizada;
- estado del proyecto sincronizado;
- evidencias relevantes conservadas.

---

# REVISIONES

Condor debe utilizar revisiones para detectar problemas que no hayan sido encontrados por controles automaticos.

Las revisiones pueden ser:

- revision de requisitos;
- revision arquitectonica;
- revision de codigo;
- revision documental;
- revision de pruebas;
- revision de experiencia;
- revision previa al congelamiento.

La profundidad debe ajustarse al riesgo.

---

# LISTAS DE CONTROL

Las listas de control deben utilizarse para actividades repetibles cuando aporten valor.

Una lista puede comprobar, por ejemplo:

- alcance;
- arquitectura;
- pruebas;
- documentacion;
- trazabilidad;
- criterios de aceptacion;
- estado de artefactos.

Las listas deben evolucionar cuando las revisiones revelen nuevos patrones de error.

---

# AUTOAUDITORIA

Antes de una revision formal, Condor debera realizar una autoauditoria cuando sus capacidades lo permitan.

La autoauditoria debe buscar:

- omisiones;
- contradicciones;
- duplicidades;
- incumplimientos;
- dependencias incorrectas;
- criterios sin evidencia;
- documentacion desactualizada;
- defectos evidentes;
- desviaciones arquitectonicas.

La autoauditoria reduce el costo de la revision, pero no sustituye la revision formal cuando esta sea necesaria.

---

# AUTOMATIZACION DE CONTROLES

Los controles repetibles deben automatizarse progresivamente cuando sea viable.

La decision debe considerar:

- capacidad del hardware;
- modelo LLM disponible;
- herramientas;
- estabilidad;
- costo computacional;
- beneficio.

Cuando no sea posible automatizar un control, debe existir una alternativa manual o semiautomatica cuando sea necesaria para preservar la calidad.

---

# CONTROL DE CAMBIOS

Todo cambio relevante debe evaluarse respecto a:

- requisitos afectados;
- arquitectura;
- criterios de aceptacion;
- pruebas;
- documentacion;
- trazabilidad;
- riesgos.

Los cambios no deben introducir modificaciones no relacionadas con el objetivo de la tarea sin una justificacion registrada.

---

# GESTION DE DEFECTOS

Los defectos detectados deben:

- registrarse;
- clasificarse;
- priorizarse;
- corregirse cuando corresponda;
- volver a verificarse;
- conservar trazabilidad.

Los defectos repetitivos deben analizarse para determinar si requieren:

- nueva prueba;
- nuevo control;
- modificacion del proceso;
- modificacion arquitectonica;
- registro como deuda arquitectonica.

---

# PREVENCION DE REGRESIONES

Los defectos relevantes que puedan repetirse deben convertirse, cuando sea viable, en controles o pruebas permanentes.

El objetivo es que un defecto corregido no vuelva a aparecer sin ser detectado.

---

# CONTROL DOCUMENTAL

Los documentos deben conservar:

- nombre oficial;
- version;
- estado;
- nivel;
- clasificacion;
- historial de cambios cuando corresponda.

Las modificaciones relevantes deben mantener coherencia con los documentos relacionados.

La existencia de varias fuentes para una misma verdad debe evitarse cuando genere ambiguedad.

---

# CONTROL DE TRAZABILIDAD

Los resultados relevantes deben poder relacionarse con:

Necesidad

↓

Requisito

↓

Decision

↓

Diseno

↓

Implementacion

↓

Verificacion

↓

Documentacion

↓

Version

La profundidad de la trazabilidad debe ser proporcional al impacto del resultado.

---

# CONTROL DE HERRAMIENTAS Y MODELOS

Cuando una herramienta o modelo LLM participe en un proceso de desarrollo, deben considerarse:

- version;
- configuracion;
- disponibilidad;
- limitaciones conocidas;
- reproducibilidad cuando sea necesaria;
- impacto de cambios de version.

Las limitaciones del modelo o hardware deben influir en la estrategia de control, pero no eliminan la responsabilidad de verificar el resultado.

---

# ASEGURAMIENTO Y HARDWARE

Los mecanismos de aseguramiento deben adaptarse al hardware disponible.

Cuando una tecnica avanzada no pueda ejecutarse localmente, Condor debe buscar una alternativa viable que preserve el objetivo de calidad.

Esto es especialmente importante para pruebas, automatizacion, analisis y ejecucion de modelos.

---

# ASEGURAMIENTO Y MEJORES PRACTICAS

Condor debe incorporar progresivamente practicas vigentes de ingenieria de software cuando sean compatibles con:

- hardware disponible;
- modelo LLM;
- herramientas;
- estabilidad;
- beneficio real.

Entre las practicas que pueden evaluarse se encuentran:

- loops de desarrollo;
- harnesses;
- pruebas automatizadas;
- integracion continua;
- analisis estatico;
- validaciones automaticas;
- revisiones sistematicas.

La adopcion debe ser progresiva y no debe convertirse en complejidad innecesaria.

---

# MEJORA CONTINUA

Los resultados de calidad deben alimentar el proceso de mejora:

Hallazgo

↓

Analisis

↓

Causa

↓

Accion correctiva o preventiva

↓

Verificacion

↓

Incorporacion al proceso

Cuando un hallazgo revele una mejora arquitectonica no bloqueante, debe registrarse en el registro oficial de deuda arquitectonica en lugar de interrumpir innecesariamente el desarrollo.

---

# INDICADORES

Las metricas definidas en `METRICAS.md` deben utilizarse para observar la eficacia del aseguramiento.

No deben utilizarse como objetivos aislados ni como sustituto del juicio de ingenieria.

Los indicadores deben ayudar a identificar:

- tendencias;
- regresiones;
- areas de riesgo;
- retrabajo;
- oportunidades de automatizacion;
- perdida de trazabilidad;
- problemas documentales.

---

# CRITERIOS DE EFICACIA

El aseguramiento puede considerarse eficaz cuando contribuye a:

- detectar defectos tempranamente;
- reducir defectos repetitivos;
- reducir retrabajo;
- mantener la arquitectura;
- mantener documentacion coherente;
- aumentar la repetibilidad;
- mejorar la trazabilidad;
- facilitar el siguiente ciclo de desarrollo.

La eficacia no debe medirse solamente por la cantidad de controles ejecutados.

---

# RELACION CON OTROS DOCUMENTOS

`CALIDAD.md` define el marco general de calidad.

`VALIDACION.md` define el proceso para determinar conformidad.

`PRUEBAS.md` define la estrategia de pruebas.

`CRITERIOS_ACEPTACION.md` define las condiciones objetivas de aceptacion.

`METRICAS.md` define indicadores para observar la evolucion.

`ASEGURAMIENTO_CALIDAD.md` define las practicas preventivas y de control que mantienen estas condiciones durante el ciclo.

---

# REGLAS

1. El aseguramiento comienza antes de la implementacion.
2. La prevencion debe priorizarse sobre la correccion cuando sea viable.
3. Los controles deben producir evidencia cuando corresponda.
4. Las revisiones deben ser proporcionales al riesgo.
5. Los defectos relevantes deben generar aprendizaje.
6. Las regresiones deben prevenirse mediante pruebas o controles cuando sea viable.
7. La documentacion debe formar parte del control de calidad.
8. Las decisiones relevantes deben conservar trazabilidad.
9. La automatizacion debe adoptarse cuando aporte valor real.
10. Las limitaciones del hardware o modelo deben generar estrategias alternativas y no eliminar el objetivo de calidad.
11. La mejora continua debe incorporarse al proceso sin introducir complejidad innecesaria.
12. El aseguramiento no sustituye la validacion ni las pruebas.

---

# RESULTADO ESPERADO

El aseguramiento de calidad debe permitir que Condor no dependa exclusivamente de detectar problemas al final.

Debe crear condiciones para:

- prevenir errores;
- detectar desviaciones tempranamente;
- mantener la arquitectura;
- conservar conocimiento;
- reducir retrabajo;
- producir resultados verificables;
- mejorar progresivamente la forma de desarrollar.

La calidad debe formar parte de la manera de construir Condor, no ser una actividad agregada despues de construirlo.

---

# HISTORIAL DE CAMBIOS

| Version | Cambios |
|----------|---------|
| 1.0.0 | Creacion del marco general de aseguramiento de calidad del Nivel 08. |
