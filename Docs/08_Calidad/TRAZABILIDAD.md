# TRAZABILIDAD

Version: 1.0.0
Estado: En desarrollo
Nivel: 08 - Calidad
Clasificacion: Trazabilidad de Ingenieria

---

# PROPOSITO

Definir el mecanismo de trazabilidad del Nivel 08 para relacionar las necesidades, requisitos, criterios, decisiones, artefactos, evidencias, resultados y versiones del Proyecto Condor.

La trazabilidad permite conocer de donde proviene un resultado, por que existe, como fue construido, como fue verificado y con que evidencia fue aceptado.

Este documento formaliza la trazabilidad aplicada al proceso de calidad sin modificar por si mismo las directivas globales del proyecto.

---

# ALCANCE

Aplica a los resultados relevantes que requieran evidencia de:

- origen;
- requisitos;
- decisiones;
- implementacion;
- verificacion;
- validacion;
- aceptacion;
- documentacion;
- version.

La profundidad de la trazabilidad debe ser proporcional al impacto y riesgo del resultado.

---

# PRINCIPIO CENTRAL

Todo resultado relevante debe poder responder:

- ¿De donde surgio?
- ¿Que necesidad atiende?
- ¿Que requisito lo origina?
- ¿Que decision lo sustenta?
- ¿Donde fue implementado?
- ¿Como fue verificado?
- ¿Que evidencia existe?
- ¿Fue aceptado?
- ¿En que version quedo incorporado?

La trazabilidad convierte la historia de un resultado en informacion recuperable y verificable.

---

# CADENA DE TRAZABILIDAD

La cadena principal de trazabilidad de Condor para el Nivel 08 es:

Necesidad

↓

Requisito

↓

Criterio de aceptacion

↓

Decision

↓

Diseno

↓

Artefacto

↓

Implementacion

↓

Verificacion

↓

Evidencia

↓

Resultado

↓

Decision de aceptacion

↓

Documentacion

↓

Version

Esta cadena complementa la cadena utilizada por `VALIDACION.md`, que relaciona necesidad, requisito, criterio, artefacto, evidencia, resultado y decision. fileciteturn3file0

---

# ELEMENTOS DE TRAZABILIDAD

## 1. Necesidad

Representa el problema, objetivo o necesidad que justifica una modificacion o resultado.

Debe identificarse cuando sea relevante.

## 2. Requisito

Define una condicion que el resultado debe satisfacer.

## 3. Criterio de aceptacion

Convierte el requisito o necesidad en una condicion objetiva de decision.

## 4. Decision

Representa una decision de ingenieria que justifica una direccion concreta.

Las decisiones relevantes deben conservar su identificador y contexto. El `MODELO_DECISIONES.md` establece que una decision debe incluir identificador, titulo, fecha, contexto, decision, justificacion, impacto y documentos relacionados. fileciteturn3file6

## 5. Diseno

Representa la solucion definida antes o durante la implementacion.

## 6. Artefacto

Representa el resultado tangible relacionado con la implementacion.

Puede ser:

- codigo;
- configuracion;
- documento;
- componente;
- prueba;
- script;
- interfaz;
- otro artefacto verificable.

## 7. Implementacion

Representa el cambio mediante el cual el diseno se convierte en un resultado ejecutable o utilizable.

## 8. Verificacion

Representa la comprobacion realizada sobre el resultado.

## 9. Evidencia

Representa el material que sustenta el resultado de la verificacion o validacion.

## 10. Resultado

Representa el estado obtenido despues de las comprobaciones.

## 11. Decision de aceptacion

Representa la decision de aceptar, aceptar con observaciones, rechazar o bloquear el resultado.

## 12. Documentacion

Representa el registro permanente que conserva el conocimiento necesario para comprender y continuar el proyecto.

## 13. Version

Representa la identificacion del estado del artefacto dentro del control de versiones cuando corresponda.

---

# IDENTIFICADORES

Los elementos que requieran trazabilidad permanente deben utilizar identificadores estables.

Ejemplos:

```text
REQ-001
CA-001
DEC-001
ART-001
TEST-001
VAL-001
DEF-001
```

Los identificadores concretos dependeran del sistema documental adoptado por el proyecto.

No deben reutilizarse identificadores existentes para elementos diferentes.

---

# MATRIZ DE TRAZABILIDAD

Cuando sea necesario, la trazabilidad puede representarse mediante una matriz:

| ID | Origen | Requisito | Criterio | Decision | Artefacto | Evidencia | Resultado | Version |
|----|--------|-----------|----------|----------|-----------|-----------|-----------|---------|
| REQ-001 | NEC-001 | REQ-001 | CA-001 | DEC-001 | ART-001 | VAL-001 | Conforme | v1.x |

La matriz es una representacion operativa. No sustituye los artefactos que contienen la informacion original.

---

# DIRECCION DE LA TRAZABILIDAD

La trazabilidad debe poder recorrerse en ambas direcciones cuando sea necesario.

## Hacia adelante

Permite responder:

> ¿Que resultado produjo esta necesidad?

Necesidad

↓

Requisito

↓

Criterio

↓

Decision

↓

Artefacto

↓

Evidencia

↓

Resultado

## Hacia atras

Permite responder:

> ¿Por que existe este resultado?

Version

↓

Artefacto

↓

Implementacion

↓

Decision

↓

Requisito

↓

Necesidad

La trazabilidad bidireccional facilita auditorias, mantenimiento y continuidad.

---

# TRAZABILIDAD DE DECISIONES

Las decisiones de ingenieria constituyen un punto central de la trazabilidad.

El proyecto ya establece que las decisiones deben preservar contexto arquitectonico, evitar repetir decisiones y facilitar la continuidad. fileciteturn3file6

Toda implementacion relevante debe poder relacionarse con las decisiones que la originan cuando estas existan.

Las decisiones reemplazadas deben conservar su historial.

---

# TRAZABILIDAD DE PRUEBAS

Cada prueba relevante debe poder relacionarse con el comportamiento o criterio que evalua.

Ejemplo:

```text
REQ-010
   ↓
CA-010
   ↓
TEST-021
   ↓
EVID-021
   ↓
Conforme
```

Esto permite determinar que requisito esta cubierto por una prueba y que evidencia sustenta el resultado.

---

# TRAZABILIDAD DE VALIDACION

Cada validacion relevante debe poder relacionarse con:

```text
Necesidad
↓
Requisito
↓
Criterio
↓
Artefacto
↓
Evidencia
↓
Resultado
↓
Decision
```

Cuando exista implementacion posterior, la cadena puede continuar hasta la version correspondiente. Esta estructura esta definida en el proceso de validacion del Nivel 08. fileciteturn3file13

---

# TRAZABILIDAD DE ACEPTACION

Los criterios de aceptacion deben permitir identificar:

- requisito relacionado;
- evidencia utilizada;
- resultado obtenido;
- decision final.

Un criterio obligatorio no debe considerarse aceptado sin evidencia suficiente.

---

# TRAZABILIDAD DE DEFECTOS

Todo defecto relevante debe poder relacionarse con:

```text
Artefacto
↓
Prueba / Validacion
↓
Evidencia
↓
Defecto
↓
Correccion
↓
Revalidacion
```

Cuando corresponda, el defecto tambien debe relacionarse con una prueba de regresion para evitar su reaparicion.

---

# TRAZABILIDAD DOCUMENTAL

Los documentos relevantes deben poder relacionarse con:

- nivel;
- artefactos afectados;
- decisiones;
- requisitos;
- resultados;
- version.

La documentacion debe representar el estado real del proyecto.

La existencia de multiples fuentes para una misma verdad debe evitarse cuando genere ambiguedad. El aseguramiento de calidad establece este control documental de forma explicita. fileciteturn3file4

---

# TRAZABILIDAD DE VERSION

Cuando un resultado llegue a un estado versionado, debe ser posible identificar:

- artefacto;
- cambio;
- evidencia;
- resultado;
- version.

La version no constituye por si sola trazabilidad completa.

Una referencia de Git debe representar el punto final de una cadena de conocimiento, no reemplazarla.

---

# TRAZABILIDAD Y CONVERSACIONES

Las conversaciones pueden constituir una fuente de descubrimiento y razonamiento, pero no deben ser la unica ubicacion de una decision relevante.

El conocimiento que tenga valor permanente debe transformarse en un artefacto del proyecto.

Esto protege la continuidad y reduce la dependencia del contexto conversacional.

---

# TRAZABILIDAD Y CONTINUIDAD

La trazabilidad debe permitir que otro desarrollador pueda reconstruir:

- que se necesitaba;
- que se decidio;
- por que se decidio;
- que se construyo;
- como se verifico;
- que evidencia existe;
- que fue aceptado;
- en que version quedo.

Esto esta alineado con el principio de preservar el conocimiento y facilitar la continuidad del proyecto.

---

# TRAZABILIDAD Y AUTOMATIZACION

Cuando sea viable, Condor debera automatizar la captura y comprobacion de relaciones de trazabilidad.

Puede automatizarse, entre otros:

- identificacion de requisitos;
- relacion entre pruebas y criterios;
- referencias entre documentos;
- deteccion de elementos sin origen;
- deteccion de artefactos sin evidencia;
- comprobacion de referencias de version;
- generacion de matrices.

La automatizacion estara condicionada por el hardware, modelo LLM, herramientas, estabilidad y beneficio disponible. Los documentos de calidad establecen este mismo criterio para la automatizacion. fileciteturn3file18

---

# COMPLETITUD

La completitud de trazabilidad puede evaluarse mediante:

```text
Elementos con trazabilidad completa
/
Elementos que requieren trazabilidad
* 100
```

Esta medicion corresponde a la metrica `MTz-001 - Trazabilidad completa` definida en `METRICAS.md`. fileciteturn3file17

Tambien debe observarse la cantidad de elementos relevantes que carecen de relacion con su origen o resultado mediante `MTz-002 - Elementos sin trazabilidad`.

---

# NIVELES DE TRAZABILIDAD

## Nivel 1 - Basica

Relacion entre artefacto y requisito.

## Nivel 2 - Operativa

Incluye requisito, criterio, artefacto y evidencia.

## Nivel 3 - Ingenieria

Incluye necesidad, requisito, criterio, decision, diseno, implementacion, evidencia y resultado.

## Nivel 4 - Completa

Incluye la cadena completa hasta documentacion y version.

La profundidad requerida dependera del impacto del resultado.

---

# PERDIDA DE TRAZABILIDAD

Una ruptura de trazabilidad ocurre cuando un elemento relevante no puede relacionarse con el elemento anterior o posterior que deberia sustentarlo.

Ejemplos:

- requisito sin criterio;
- criterio sin evidencia;
- artefacto sin requisito cuando este es necesario;
- implementacion sin decision arquitectonica cuando la decision existe;
- resultado sin prueba o validacion aplicable;
- version sin artefactos identificables.

Una ruptura debe registrarse y corregirse cuando sea relevante para la conformidad.

---

# CONTROL Y REVISION

Durante una revision de calidad deben comprobarse, cuando corresponda:

- elementos sin identificador;
- referencias inexistentes;
- relaciones incompletas;
- documentos desactualizados;
- criterios sin evidencia;
- pruebas sin criterio asociado;
- artefactos sin origen;
- resultados sin decision;
- versiones sin correspondencia verificable.

La autoauditoria de Condor debe buscar este tipo de omisiones antes de una revision formal cuando sus capacidades lo permitan. fileciteturn3file1

---

# RELACION CON DEUDA ARQUITECTONICA

La formalizacion de una cadena de trazabilidad desde necesidad hasta version Git aparece registrada como deuda arquitectonica DA-004.

Este documento implementa la trazabilidad requerida especificamente para el Nivel 08 y el proceso de calidad, pero no modifica por si mismo la directiva global ni cierra DA-004. El registro de deuda mantiene DA-004 como pendiente hasta la revision correspondiente. fileciteturn3file14

---

# RELACION CON OTROS DOCUMENTOS

`CALIDAD.md` establece el marco general de calidad.

`VALIDACION.md` utiliza la trazabilidad para sustentar las decisiones de conformidad.

`PRUEBAS.md` proporciona evidencia que puede incorporarse a la cadena.

`CRITERIOS_ACEPTACION.md` define las condiciones que deben relacionarse con evidencia y resultados.

`METRICAS.md` mide la completitud de la trazabilidad.

`ASEGURAMIENTO_CALIDAD.md` establece controles para preservar la trazabilidad.

`MODELO_DECISIONES.md` define la estructura de las decisiones de ingenieria.

---

# REGLAS

1. Todo resultado relevante debe tener el nivel de trazabilidad que corresponda a su impacto.
2. Los identificadores permanentes no deben reutilizarse.
3. Una prueba relevante debe poder relacionarse con el criterio que evalua.
4. Una validacion relevante debe conservar la relacion entre criterio, evidencia y resultado.
5. Las decisiones relevantes deben conservar su contexto.
6. Los defectos relevantes deben poder rastrearse hasta su correccion y revalidacion.
7. La documentacion debe permanecer relacionada con el estado real del proyecto.
8. La conversacion no debe ser la unica fuente de una decision permanente.
9. La version Git no sustituye la cadena de conocimiento.
10. Las rupturas de trazabilidad relevantes deben registrarse y corregirse.
11. La automatizacion de trazabilidad debe incorporarse cuando sea viable.
12. La profundidad de trazabilidad debe ser proporcional al riesgo e impacto.

---

# RESULTADO ESPERADO

La trazabilidad debe permitir a Condor reconstruir la historia de ingenieria de un resultado sin depender de la memoria de una persona, un modelo o una conversacion.

Debe ser posible recorrer:

```text
Por que existe
    ↓
Que debia cumplir
    ↓
Que se decidio
    ↓
Que se construyo
    ↓
Como se verifico
    ↓
Que evidencia existe
    ↓
Que resultado obtuvo
    ↓
Por que fue aceptado
    ↓
Donde quedo documentado
    ↓
En que version existe
```

La trazabilidad convierte el conocimiento del proceso de desarrollo en una estructura permanente, verificable y recuperable.

---

# HISTORIAL DE CAMBIOS

| Version | Cambios |
|----------|---------|
| 1.0.0 | Creacion del marco general de trazabilidad del Nivel 08. |
