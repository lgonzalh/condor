# CRITERIOS_ACEPTACION

Version: 1.0.0
Estado: En desarrollo
Nivel: 08 - Calidad
Clasificacion: Criterios de Aceptacion

---

# PROPOSITO

Definir las condiciones objetivas que deben cumplirse para aceptar un resultado del Proyecto Condor como conforme para su alcance y estado correspondiente.

Los criterios de aceptacion convierten las necesidades, requisitos y expectativas definidas en condiciones verificables.

---

# ALCANCE

Aplica a:

- funcionalidades;
- componentes;
- modulos;
- integraciones;
- interfaces;
- procesos;
- cambios;
- entregables;
- incrementos del sistema;
- resultados destinados a congelamiento.

La aplicacion de cada criterio dependera del alcance del resultado evaluado.

---

# PRINCIPIO

Un resultado no debe considerarse aceptado por percepcion, intencion o apariencia de funcionamiento.

Debe existir evidencia suficiente de que los criterios obligatorios fueron satisfechos.

---

# CARACTERISTICAS DE UN CRITERIO

Un criterio de aceptacion debe ser:

- claro;
- especifico;
- verificable;
- observable;
- relevante para el resultado;
- coherente con la arquitectura;
- trazable al requisito o necesidad correspondiente.

Debe evitar condiciones ambiguas como:

- funciona bien;
- es facil;
- es rapido;
- es correcto;

cuando no exista una forma objetiva de determinar su cumplimiento.

---

# ESTRUCTURA

Cada criterio debe poder expresarse mediante:

**Dado**

Condicion inicial o contexto.

**Cuando**

Accion, evento o circunstancia que inicia la evaluacion.

**Entonces**

Resultado observable que debe producirse.

Ejemplo:

```text
Dado un proyecto valido cargado en Condor
Cuando el usuario solicita continuar el desarrollo
Entonces Condor debe recuperar el contexto necesario antes de proponer una accion.
```

La estructura puede adaptarse cuando otro formato proporcione mayor precision.

---

# TIPOS DE CRITERIOS

## Funcionales

Definen el comportamiento que debe presentar una funcionalidad.

## Arquitectonicos

Definen condiciones estructurales que deben mantenerse.

## Tecnicos

Definen condiciones de funcionamiento, integridad, robustez o mantenimiento.

## Operativos

Definen condiciones necesarias para instalar, configurar y utilizar el resultado.

## Documentales

Definen condiciones necesarias para que la documentacion represente correctamente el resultado.

## Experiencia

Definen condiciones de comportamiento de la experiencia cuando corresponda.

## Calidad

Definen condiciones minimas necesarias para considerar aceptable un resultado.

---

# CRITERIOS OBLIGATORIOS Y COMPLEMENTARIOS

## Obligatorios

Su incumplimiento impide aceptar el resultado.

## Complementarios

Aportan calidad adicional, pero su incumplimiento no impide necesariamente continuar.

Los criterios complementarios deben quedar identificados para evitar que se interpreten accidentalmente como requisitos obligatorios.

---

# CRITERIOS DE ACEPTACION DEL RESULTADO

Un resultado puede aceptarse cuando:

- cumple los criterios obligatorios;
- existe evidencia suficiente;
- no existen no conformidades criticas abiertas;
- las pruebas aplicables fueron ejecutadas;
- los defectos relevantes estan registrados;
- la documentacion requerida esta actualizada;
- se conserva la trazabilidad necesaria;
- la arquitectura permanece conforme.

La aceptacion no significa ausencia absoluta de observaciones.

Significa que las condiciones necesarias para el estado correspondiente fueron satisfechas.

---

# CRITERIOS PARA CAMBIOS

Todo cambio debe evaluarse contra:

- criterios existentes afectados;
- nuevos criterios derivados del cambio;
- regresiones potenciales;
- restricciones arquitectonicas;
- documentacion relacionada.

Si un cambio altera el comportamiento esperado, los criterios afectados deben actualizarse antes de aceptar el cambio.

---

# CRITERIOS DE REGRESION

Cuando un cambio pueda afectar comportamiento existente, deben verificarse los criterios previamente aceptados que resulten afectados.

Un criterio previamente satisfecho no debe considerarse permanente si el cambio modifica su alcance.

---

# CRITERIOS ARQUITECTONICOS

Cuando corresponda, deben comprobarse condiciones como:

- responsabilidades correctamente separadas;
- dependencias permitidas;
- contratos respetados;
- interfaces consistentes;
- ausencia de dependencias prohibidas;
- cumplimiento de restricciones arquitectonicas.

La funcionalidad por si sola no constituye aceptacion cuando existe incumplimiento arquitectonico.

---

# CRITERIOS DOCUMENTALES

Cuando un resultado requiera documentacion, debe comprobarse:

- documento existente;
- nombre correcto;
- version correcta;
- contenido coherente con el resultado;
- referencias validas;
- ausencia de contradicciones relevantes;
- trazabilidad conservada.

---

# CRITERIOS DE INSTALACION Y OPERACION

Cuando corresponda, deben comprobarse:

- instalacion reproducible;
- configuracion inicial funcional;
- dependencias disponibles;
- ejecucion correcta;
- manejo de errores previsibles;
- instrucciones suficientes para operar el resultado.

Para la version inicial de Condor, Windows constituye la plataforma prioritaria definida para el proyecto.

---

# CRITERIOS DE EXPERIENCIA

Cuando el resultado afecte la experiencia de usuario, deben comprobarse los escenarios definidos para la funcionalidad.

La aceptacion debe considerar:

- comportamiento esperado;
- estados normales;
- estados de error;
- continuidad del flujo;
- coherencia con la experiencia definida;
- ausencia de pasos manuales innecesarios cuando estos puedan evitarse.

---

# CRITERIOS PARA LLM

Cuando un resultado dependa de un modelo LLM, la aceptacion debe evaluar el comportamiento del sistema y no solamente la capacidad general del modelo.

Cuando corresponda deben comprobarse:

- cumplimiento de instrucciones;
- uso del contexto;
- respeto de restricciones;
- comportamiento estructurado;
- uso de herramientas;
- recuperacion ante errores;
- estabilidad suficiente para el flujo evaluado.

Un modelo diferente puede requerir nuevos criterios o nueva evidencia.

---

# CRITERIOS DE CONTEXTO Y MEMORIA

Cuando un resultado afecte la memoria o continuidad de Condor, deben comprobarse, segun corresponda:

- persistencia;
- recuperacion;
- continuidad entre sesiones;
- actualizacion;
- ausencia de perdida de decisiones;
- ausencia de duplicacion indebida;
- coherencia con el estado documentado.

Estos criterios protegen uno de los objetivos centrales del proyecto: preservar el conocimiento del proyecto y reducir la dependencia del historial de conversaciones.

---

# EVIDENCIA

Cada criterio obligatorio debe asociarse con una evidencia apropiada.

Puede utilizarse:

- prueba automatizada;
- prueba manual;
- resultado de integracion;
- inspeccion;
- revision documental;
- evidencia de ejecucion;
- resultado de herramienta;
- validacion de escenario.

La evidencia debe ser suficiente para determinar el estado del criterio.

---

# ESTADOS

Cada criterio debe clasificarse como:

- Cumple
- No cumple
- No aplica
- No verificable

Un criterio obligatorio en estado **No cumple** impide la aceptacion.

Un criterio obligatorio en estado **No verificable** impide la aceptacion cuando la evidencia sea necesaria para determinar el cumplimiento.

---

# PROCESO DE ACEPTACION

## 1. Identificar el resultado

Determinar exactamente que se pretende aceptar.

## 2. Identificar criterios

Seleccionar los criterios aplicables.

## 3. Ejecutar verificaciones

Obtener la evidencia correspondiente.

## 4. Evaluar

Clasificar cada criterio.

## 5. Registrar incumplimientos

Documentar los criterios no satisfechos.

## 6. Corregir

Realizar las acciones necesarias.

## 7. Revalidar

Volver a evaluar los criterios afectados.

## 8. Emitir decision

Aceptar o rechazar el resultado para el estado correspondiente.

---

# DECISIONES DE ACEPTACION

## Aceptado

Todos los criterios obligatorios cumplen.

## Aceptado con observaciones

Todos los criterios obligatorios cumplen y existen observaciones no bloqueantes registradas.

## Rechazado

Uno o mas criterios obligatorios no cumplen.

## Bloqueado

La decision no puede completarse por falta de evidencia o dependencia externa.

---

# TRAZABILIDAD

Cada criterio relevante debe poder relacionarse con:

Necesidad

↓

Requisito

↓

Criterio de aceptacion

↓

Artefacto

↓

Evidencia

↓

Resultado

↓

Decision

La trazabilidad permite conocer por que un resultado fue aceptado y que condiciones sustentaron la decision.

---

# RELACION CON VALIDACION

`VALIDACION.md` define el proceso general para determinar conformidad mediante criterios y evidencia.

`CRITERIOS_ACEPTACION.md` define las condiciones especificas contra las cuales se determina esa conformidad.

Los criterios constituyen una de las entradas principales del proceso de validacion.

---

# RELACION CON PRUEBAS

`PRUEBAS.md` define la estrategia para obtener evidencia mediante diferentes niveles de pruebas.

Una prueba puede demostrar el cumplimiento de uno o varios criterios, pero la aceptacion requiere evaluar el conjunto de criterios aplicables.

---

# RELACION CON CALIDAD

`CALIDAD.md` establece el marco general del Nivel 08.

Los criterios de aceptacion convierten parte de ese marco en condiciones objetivas de decision.

---

# EVOLUCION DE LOS CRITERIOS

Los criterios deben evolucionar cuando:

- se descubren nuevos requisitos;
- cambia el alcance;
- se modifica la arquitectura;
- aparece un nuevo riesgo;
- una prueba descubre una condicion no contemplada;
- una revision revela una omision.

Los cambios deben conservar trazabilidad y evitar contradicciones con criterios ya vigentes.

---

# REGLAS

1. Todo resultado relevante debe tener criterios de aceptacion aplicables.
2. Los criterios obligatorios deben ser verificables.
3. No debe aceptarse un resultado con criterios obligatorios incumplidos.
4. La evidencia debe conservarse cuando sea relevante para la decision.
5. Los cambios deben reevaluar los criterios afectados.
6. Los criterios deben permanecer alineados con los requisitos y la arquitectura.
7. La aceptacion no debe depender exclusivamente de percepcion subjetiva.
8. Los criterios deben distinguir claramente lo obligatorio de lo complementario.
9. Los criterios deben permitir una decision reproducible.
10. La aceptacion debe quedar trazable.

---

# RESULTADO ESPERADO

Los criterios de aceptacion deben permitir a Condor responder de forma objetiva:

- ¿Que debe cumplirse?
- ¿Como sabemos que se cumplio?
- ¿Que evidencia lo demuestra?
- ¿Que ocurre si no se cumple?
- ¿Puede aceptarse el resultado?
- ¿Puede avanzar hacia el congelamiento?

La aceptacion convierte un resultado tecnicamente terminado en un resultado formalmente conforme para el estado correspondiente.

---

# HISTORIAL DE CAMBIOS

| Version | Cambios |
|----------|---------|
| 1.0.0 | Creacion de los criterios generales de aceptacion del Nivel 08. |
