# CALIDAD

Version: 1.0.0
Estado: Activo
Nivel: 08 - Calidad
Clasificacion: Documento de Ingenieria

---

# PROPOSITO

Definir el marco de calidad del Proyecto Condor y establecer las condiciones que deben cumplirse para considerar un artefacto, componente, cambio o iteracion conforme antes de su congelamiento.

La calidad en Condor no se limita a las pruebas. Comprende la coherencia funcional, arquitectonica, tecnica, documental y operativa del proyecto.

---

# ALCANCE

Este documento aplica a:

- codigo fuente;
- arquitectura;
- configuracion;
- integraciones;
- documentacion;
- procesos;
- artefactos generados;
- cambios realizados por Condor;
- resultados producidos durante una iteracion.

La calidad debe evaluarse durante todo el ciclo de vida y no unicamente al finalizar una implementacion.

---

# PRINCIPIO CENTRAL

Un resultado no se considera terminado por haber sido generado.

Se considera terminado cuando puede ser verificado, cumple los criterios establecidos, mantiene la coherencia del proyecto, tiene su conocimiento documentado y queda preparado para congelamiento.

---

# OBJETIVOS

- Detectar defectos antes de que se propaguen.
- Mantener la coherencia arquitectonica.
- Verificar el cumplimiento de requisitos y restricciones.
- Reducir regresiones.
- Reducir deuda tecnica y documental.
- Garantizar resultados repetibles y trazables.
- Evitar que la calidad dependa exclusivamente de una revision manual.
- Preparar la automatizacion progresiva del aseguramiento de calidad.

---

# PRINCIPIOS DE CALIDAD

## 1. Calidad desde el origen

La calidad debe incorporarse desde Comprender y no agregarse al final de Implementar.

## 2. Todo debe ser verificable

Todo resultado relevante debe disponer de criterios que permitan determinar objetivamente si cumple o no cumple.

## 3. La arquitectura es un criterio de calidad

Un resultado funcional que rompe la arquitectura no se considera conforme.

## 4. La documentacion forma parte del resultado

Codigo, configuracion y documentacion deben permanecer sincronizados.

## 5. Toda correccion requiere nueva verificacion

Una modificacion posterior a un hallazgo debe volver a pasar por las validaciones afectadas.

## 6. Automatizar cuando sea viable

Las comprobaciones repetibles deben automatizarse progresivamente cuando las capacidades del hardware, herramientas y modelo lo permitan.

## 7. La calidad debe ser trazable

Todo hallazgo, correccion y aprobacion relevante debe poder relacionarse con el artefacto y criterio correspondiente.

## 8. La calidad debe preservar la simplicidad

Una solucion mas compleja no es una solucion de mayor calidad si la complejidad no aporta valor necesario.

---

# DIMENSIONES DE CALIDAD

## Calidad funcional

Verifica que el resultado cumple el comportamiento requerido.

## Calidad arquitectonica

Verifica:

- responsabilidades;
- dependencias;
- acoplamiento;
- cohesion;
- contratos;
- interfaces;
- cumplimiento de la arquitectura definida.

## Calidad tecnica

Verifica:

- mantenibilidad;
- legibilidad;
- robustez;
- manejo de errores;
- rendimiento razonable;
- seguridad aplicable;
- ausencia de defectos conocidos de alta severidad.

## Calidad documental

Verifica:

- existencia de la documentacion requerida;
- coherencia entre documentos;
- versionado;
- trazabilidad;
- ausencia de duplicidades relevantes;
- sincronizacion con el estado real del proyecto.

## Calidad operativa

Verifica:

- configuracion;
- instalacion;
- ejecucion;
- integraciones;
- reproducibilidad;
- comportamiento ante errores y condiciones esperadas.

## Calidad de experiencia

Cuando corresponda, verifica que la experiencia definida por el proyecto sea coherente con los criterios de interfaz, interaccion y comportamiento establecidos.

---

# CICLO DE CALIDAD

La calidad acompana el ciclo general de Condor:

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

En cada etapa deben identificarse los criterios de calidad aplicables.

---

# GATES DE CALIDAD

Cada resultado debera superar los controles correspondientes antes de avanzar.

## Gate 1 - Comprension

Verificar:

- objetivo identificado;
- alcance definido;
- contexto suficiente;
- dependencias conocidas;
- restricciones identificadas.

## Gate 2 - Diseno

Verificar:

- solucion coherente con la arquitectura;
- responsabilidades definidas;
- interfaces y contratos identificados;
- impacto evaluado.

## Gate 3 - Implementacion

Verificar:

- cambios limitados al alcance;
- arquitectura respetada;
- errores controlados;
- artefactos generados correctamente.

## Gate 4 - Verificacion

Verificar:

- pruebas aplicables ejecutadas;
- resultados satisfactorios;
- defectos registrados;
- regresiones controladas.

## Gate 5 - Documentacion

Verificar:

- documentacion actualizada;
- decisiones relevantes registradas;
- trazabilidad preservada;
- estado del proyecto sincronizado cuando corresponda.

## Gate 6 - Congelamiento

Verificar:

- criterios de aceptacion cumplidos;
- errores criticos inexistentes;
- no conformidades resueltas o formalmente aceptadas;
- artefactos preparados para congelamiento.

---

# AUTOAUDITORIA

Antes de una revision formal, Condor debera realizar, cuando sus capacidades lo permitan, una autoauditoria del resultado.

La autoauditoria debe buscar como minimo:

- contradicciones;
- omisiones;
- duplicidades;
- incumplimiento de restricciones;
- inconsistencias arquitectonicas;
- inconsistencias documentales;
- errores evidentes;
- criterios de calidad no verificados;
- trazabilidad incompleta.

La autoauditoria no sustituye la revision ni la validacion.

Su objetivo es reducir defectos detectables y trabajo manual.

Este principio se deriva de la experiencia acumulada durante la evolucion documental del proyecto: la revision no solo detecta errores, tambien descubre requisitos y profundidad que deben incorporarse al proceso de calidad. fileciteturn2file19

---

# NO CONFORMIDADES

Una no conformidad es cualquier incumplimiento verificable de un requisito, criterio, restriccion, contrato, arquitectura o estandar aplicable.

Las no conformidades se clasifican como:

- Critica: impide continuar o compromete gravemente el proyecto.
- Alta: afecta de forma importante la funcionalidad, arquitectura o estabilidad.
- Media: requiere correccion antes del congelamiento, pero no bloquea inmediatamente.
- Baja: no compromete el resultado y puede planificarse posteriormente.

Toda no conformidad relevante debe registrar:

- identificador;
- artefacto afectado;
- descripcion;
- severidad;
- causa conocida, cuando exista;
- accion correctiva;
- resultado de la nueva verificacion.

---

# CRITERIOS GENERALES DE CONFORMIDAD

Un artefacto puede considerarse conforme cuando:

- cumple su proposito;
- respeta su alcance;
- cumple los requisitos aplicables;
- mantiene la arquitectura;
- supera las verificaciones correspondientes;
- no contiene errores criticos abiertos;
- conserva la trazabilidad necesaria;
- mantiene sincronizada la documentacion;
- esta preparado para el siguiente estado del ciclo de vida.

---

# CALIDAD Y AUTOMATIZACION

Condor debera incorporar progresivamente mecanismos de calidad automatizados cuando sean tecnicamente viables.

La automatizacion estara condicionada por:

- capacidad del hardware disponible;
- capacidades del modelo LLM;
- herramientas disponibles;
- costo computacional;
- estabilidad del mecanismo;
- beneficio obtenido.

Cuando una automatizacion no sea viable, Condor debera mantener una alternativa manual o semiautomatica que preserve el criterio de calidad.

---

# RELACION CON OTROS ARTEFACTOS

El Nivel 08 se descompone en los siguientes documentos:

- `CALIDAD.md`: marco general de calidad.
- `VALIDACION.md`: proceso de validacion.
- `PRUEBAS.md`: estrategia y ejecucion de pruebas.
- `CRITERIOS_ACEPTACION.md`: condiciones objetivas de aceptacion.
- `METRICAS.md`: indicadores de calidad.
- `ASEGURAMIENTO_CALIDAD.md`: mecanismos preventivos y de control.
- `TRAZABILIDAD.md`: relacion entre necesidad, decision, implementacion y evidencia.

Esta separacion evita concentrar responsabilidades diferentes en un unico documento y mantiene cada artefacto enfocado en una responsabilidad.

El plan documental oficial del Nivel 08 establece estos siete entregables y define `CALIDAD.md` como el primero. fileciteturn2file4

---

# RELACION CON LAS PRUEBAS EXISTENTES

La estrategia de pruebas ya definida establece pruebas unitarias, de integracion, de sistema, regresion, arquitectura y aceptacion, ademas de criterios de aprobacion y registro de resultados. `CALIDAD.md` no reemplaza esa estrategia; establece el marco superior dentro del cual las pruebas constituyen una evidencia de calidad. fileciteturn2file2

---

# REGLAS

1. Ningun resultado se considerara terminado solamente por haber sido generado.
2. Ningun cambio critico avanzara sin la verificacion correspondiente.
3. Toda correccion relevante requerira nueva verificacion.
4. Ninguna violacion arquitectonica se considerara aceptable por el simple hecho de que el resultado funcione.
5. La documentacion forma parte de la calidad del resultado.
6. Los hallazgos relevantes deben conservar trazabilidad.
7. La calidad debe evaluarse de forma proporcional al riesgo y al impacto.
8. La automatizacion debe utilizarse cuando aporte valor y sea viable.
9. Las limitaciones del hardware o del modelo no eliminan el criterio de calidad; determinan la estrategia aplicable.
10. El congelamiento solo debe producirse despues de superar los criterios definidos para el artefacto.

---

# RESULTADO ESPERADO

El Nivel 08 debe establecer una base que permita a Condor responder de forma objetiva:

- ¿Esto funciona?
- ¿Cumple lo solicitado?
- ¿Respeta la arquitectura?
- ¿Puede mantenerse?
- ¿Puede verificarse nuevamente?
- ¿La documentacion representa el estado real?
- ¿Puede continuar el proyecto sin perder conocimiento?
- ¿Esta listo para congelarse?

La calidad no es una etapa posterior al desarrollo.

Es una propiedad que debe acompañar cada etapa del desarrollo.

---

# HISTORIAL DE CAMBIOS

| Version | Cambios |
|----------|---------|
| 1.0.0 | Creacion del marco general de calidad del Nivel 08. |
