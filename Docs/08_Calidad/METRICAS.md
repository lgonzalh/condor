# METRICAS

Version: 1.0.0
Estado: En desarrollo
Nivel: 08 - Calidad
Clasificacion: Metricas de Calidad

---

# PROPOSITO

Definir las metricas que permitiran observar y evaluar objetivamente la calidad, estabilidad, verificabilidad y evolucion del Proyecto Condor.

Las metricas no tienen como finalidad medir actividad por actividad. Su objetivo es proporcionar evidencia para apoyar decisiones de ingenieria.

---

# ALCANCE

Este documento aplica a las metricas relacionadas con:

- calidad;
- pruebas;
- validacion;
- aceptacion;
- defectos;
- regresiones;
- documentacion;
- trazabilidad;
- estabilidad;
- automatizacion;
- continuidad del proyecto.

Las metricas se aplicaran de forma progresiva conforme exista informacion suficiente para calcularlas.

---

# PRINCIPIOS

## 1. Medir para decidir

Una metrica debe aportar informacion util para una decision o revision.

## 2. No medir por medir

No se incorporaran metricas que generen trabajo sin aportar informacion relevante.

## 3. Las metricas necesitan contexto

Un valor aislado no demuestra calidad. Debe interpretarse junto con el alcance, riesgo, version y condiciones del resultado.

## 4. La tendencia importa

Cuando sea posible, Condor debe observar la evolucion de una metrica y no solamente su valor puntual.

## 5. Las metricas no sustituyen el criterio de ingenieria

Una metrica favorable no convierte automaticamente un resultado en conforme.

## 6. Las metricas deben ser reproducibles

El calculo debe poder repetirse bajo las mismas condiciones.

## 7. Automatizar cuando sea viable

Las metricas repetibles deben automatizarse progresivamente cuando las capacidades disponibles lo permitan.

---

# CATEGORIAS

Las metricas del Nivel 08 se agrupan en:

1. Calidad de resultados.
2. Pruebas.
3. Validacion.
4. Aceptacion.
5. Defectos.
6. Regresion.
7. Documentacion.
8. Trazabilidad.
9. Automatizacion.
10. Continuidad.

---

# METRICAS DE CALIDAD

## MQ-001 - Resultados conformes

Mide la proporcion de resultados evaluados que cumplen los criterios obligatorios.

Formula:

```text
Resultados conformes / Resultados evaluados * 100
```

Interpretacion:

Un aumento sostenido indica mayor estabilidad del proceso, pero debe analizarse junto con la severidad de los defectos y el alcance evaluado.

---

## MQ-002 - No conformidades abiertas

Mide la cantidad de no conformidades pendientes.

Debe analizarse por severidad:

- Critica;
- Alta;
- Media;
- Baja.

Las no conformidades criticas deben recibir prioridad inmediata.

---

## MQ-003 - Densidad de no conformidades

Mide las no conformidades en relacion con el tamano del resultado evaluado.

La unidad de medida dependera del tipo de artefacto.

No debe utilizarse una unidad artificial unicamente para producir un indicador.

---

# METRICAS DE PRUEBAS

## MT-001 - Cobertura de pruebas

Mide la proporcion del comportamiento definido que dispone de pruebas aplicables.

La cobertura puede medirse por diferentes dimensiones:

- codigo;
- requisitos;
- escenarios;
- componentes;
- criterios de aceptacion.

La cobertura de codigo no debe interpretarse como cobertura completa del sistema.

---

## MT-002 - Ejecucion de pruebas

Mide la proporcion de pruebas planificadas que fueron ejecutadas.

Formula:

```text
Pruebas ejecutadas / Pruebas planificadas * 100
```

---

## MT-003 - Tasa de exito de pruebas

Mide la proporcion de pruebas ejecutadas que finalizaron correctamente.

Formula:

```text
Pruebas exitosas / Pruebas ejecutadas * 100
```

Debe analizarse junto con pruebas bloqueadas e inestables.

---

## MT-004 - Pruebas inestables

Mide la cantidad o proporcion de pruebas que presentan resultados inconsistentes bajo condiciones equivalentes.

Una prueba inestable no debe utilizarse como evidencia confiable hasta ser investigada.

---

# METRICAS DE VALIDACION

## MV-001 - Criterios validados

Mide la proporcion de criterios de aceptacion evaluados.

Formula:

```text
Criterios evaluados / Criterios aplicables * 100
```

---

## MV-002 - Criterios conformes

Mide la proporcion de criterios evaluados que cumplen.

Formula:

```text
Criterios conformes / Criterios evaluados * 100
```

---

## MV-003 - Evidencia disponible

Mide la proporcion de criterios que cuentan con evidencia suficiente.

Formula:

```text
Criterios con evidencia / Criterios evaluados * 100
```

---

# METRICAS DE ACEPTACION

## MA-001 - Tasa de aceptacion

Mide la proporcion de resultados que alcanzan una decision de aceptacion.

Formula:

```text
Resultados aceptados / Resultados evaluados * 100
```

Debe distinguirse entre:

- aceptado;
- aceptado con observaciones;
- rechazado;
- bloqueado.

---

## MA-002 - Rechazos

Mide la cantidad de resultados rechazados durante la aceptacion.

Debe analizarse por causa y severidad.

---

# METRICAS DE DEFECTOS

## MD-001 - Defectos detectados

Mide la cantidad de defectos identificados durante una iteracion o version.

Debe conservarse el contexto del periodo y alcance evaluado.

---

## MD-002 - Defectos por severidad

Distribuye los defectos entre:

- Critica;
- Alta;
- Media;
- Baja.

Esta distribucion es mas informativa que unicamente contar defectos totales.

---

## MD-003 - Tiempo de resolucion

Mide el tiempo transcurrido entre la identificacion y el cierre de un defecto.

Debe utilizarse solamente cuando exista informacion temporal confiable.

---

# METRICAS DE REGRESION

## MR-001 - Regresiones detectadas

Mide la cantidad de comportamientos previamente conformes que dejan de cumplir despues de un cambio.

---

## MR-002 - Regresiones prevenidas

Mide los defectos potenciales detectados por pruebas de regresion antes de que lleguen a una entrega.

Esta metrica debe interpretarse con cautela porque depende de la calidad y alcance de la suite de regresion.

---

# METRICAS DOCUMENTALES

## MDoc-001 - Artefactos documentados

Mide la proporcion de artefactos que cuentan con la documentacion requerida.

Formula:

```text
Artefactos documentados / Artefactos que requieren documentacion * 100
```

---

## MDoc-002 - Coherencia documental

Mide la cantidad de inconsistencias relevantes detectadas entre documentacion y estado real del proyecto.

El objetivo es reducir progresivamente estas inconsistencias.

---

## MDoc-003 - Documentacion sincronizada

Mide la proporcion de cambios relevantes cuya documentacion asociada fue actualizada.

Formula:

```text
Cambios documentados / Cambios que requieren documentacion * 100
```

---

# METRICAS DE TRAZABILIDAD

## MTz-001 - Trazabilidad completa

Mide la proporcion de resultados relevantes que pueden recorrerse mediante:

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

Formula:

```text
Resultados con trazabilidad completa / Resultados evaluados * 100
```

---

## MTz-002 - Elementos sin trazabilidad

Mide la cantidad de elementos relevantes que no pueden relacionarse con su origen o resultado correspondiente.

---

# METRICAS DE AUTOMATIZACION

## MAT-001 - Automatizacion de pruebas

Mide la proporcion de pruebas repetibles que estan automatizadas.

Formula:

```text
Pruebas repetibles automatizadas / Pruebas repetibles identificadas * 100
```

---

## MAT-002 - Automatizacion de validaciones

Mide la proporcion de validaciones repetibles que pueden ejecutarse automaticamente.

---

## MAT-003 - Automatizacion util

No mide solamente cantidad de automatizaciones.

Evalua si la automatizacion:

- reduce trabajo manual;
- mejora repetibilidad;
- reduce errores;
- aporta evidencia;
- mantiene un costo razonable.

---

# METRICAS DE CONTINUIDAD

## MC-001 - Artefactos permanentes generados

Mide cuantos resultados de una iteracion se transforman en artefactos permanentes del proyecto.

Esta metrica refleja el principio de que el conocimiento relevante debe sobrevivir a la conversacion.

---

## MC-002 - Decisiones trazables

Mide la proporcion de decisiones relevantes que tienen registro permanente.

Formula:

```text
Decisiones registradas / Decisiones relevantes identificadas * 100
```

---

## MC-003 - Dependencia de contexto conversacional

Mide la cantidad de conocimiento necesario para continuar que permanece exclusivamente en conversaciones.

El objetivo de esta metrica es disminuir progresivamente esa dependencia.

---

# METRICAS DE PROCESO

Las metricas de proceso deben utilizarse con prudencia.

No se considerara que mas actividad equivale automaticamente a mayor calidad.

Pueden observarse, cuando sean utiles:

- ciclos de correccion;
- retrabajo;
- tiempo de validacion;
- tiempo de correccion;
- cantidad de iteraciones;
- automatizacion disponible.

Estas metricas deben utilizarse para detectar oportunidades de mejora y no como objetivos aislados.

---

# LINEAS BASE

Una metrica no debera recibir un objetivo numerico permanente sin disponer primero de una linea base suficiente.

El proceso recomendado es:

Medir

↓

Observar

↓

Establecer linea base

↓

Interpretar

↓

Definir objetivo

↓

Revisar

Los objetivos podran evolucionar con la madurez del proyecto.

---

# UMBRALES

Cuando sea necesario establecer umbrales, estos deben clasificarse como:

- Objetivo;
- Advertencia;
- Critico.

No todos los indicadores requieren umbrales.

Los umbrales deben estar relacionados con riesgo y contexto.

---

# INTERPRETACION

Las metricas deben analizarse considerando:

- version;
- nivel;
- alcance;
- tipo de artefacto;
- riesgo;
- severidad;
- hardware;
- modelo LLM;
- herramientas;
- condiciones de ejecucion.

No debe compararse directamente informacion producida bajo condiciones incompatibles.

---

# REGLAS

1. Una metrica debe tener un proposito identificable.
2. No se deben crear metricas unicamente por facilidad de medicion.
3. Toda metrica debe tener una definicion clara.
4. Las formulas deben ser reproducibles cuando correspondan.
5. Los valores deben conservar su contexto.
6. Las tendencias deben considerarse cuando exista historial suficiente.
7. Las metricas no sustituyen la validacion ni el juicio de ingenieria.
8. No deben utilizarse metricas para incentivar comportamientos que deterioren la calidad.
9. Las metricas deben evolucionar junto con el proyecto.
10. Los objetivos numericos deben establecerse despues de contar con una linea base razonable.

---

# RELACION CON LOS DEMAS DOCUMENTOS

`CALIDAD.md` define el marco general.

`VALIDACION.md` define el proceso de determinacion de conformidad.

`PRUEBAS.md` define la estrategia para obtener evidencia mediante pruebas.

`CRITERIOS_ACEPTACION.md` define las condiciones objetivas de aceptacion.

`METRICAS.md` define como observar cuantitativamente la evolucion de estos procesos y resultados.

---

# RESULTADO ESPERADO

Las metricas deben permitir a Condor observar:

- si la calidad mejora;
- donde aparecen defectos;
- que tan eficaz es la estrategia de pruebas;
- que tan completa es la validacion;
- que tan trazable es el proyecto;
- cuanto conocimiento se conserva;
- cuanto trabajo repetitivo se automatiza;
- donde existen oportunidades de mejora.

El objetivo no es producir mas numeros.

El objetivo es convertir datos de calidad en informacion util para continuar construyendo.

---

# HISTORIAL DE CAMBIOS

| Version | Cambios |
|----------|---------|
| 1.0.0 | Creacion del marco general de metricas del Nivel 08. |
