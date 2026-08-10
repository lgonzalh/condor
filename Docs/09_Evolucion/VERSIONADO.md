# VERSIONADO

Version: 1.0.0
Estado: Activo
Nivel: 09 - Evolucion
Clasificacion: Versionado

---

# PROPOSITO

Definir las reglas para identificar, registrar y controlar las versiones de los artefactos y cambios del Proyecto Condor.

El versionado debe permitir conocer que cambio, cuando cambio y que estado tenia un artefacto sin depender de una conversacion.

---

# ALCANCE

Aplica a los documentos oficiales, componentes, entregables y versiones relevantes del Proyecto Condor.

No sustituye el historial de Git.

Git conserva el historial tecnico de cambios.

La version interna identifica el estado funcional o documental de un artefacto.

---

# PRINCIPIOS

## Trazabilidad

Toda version relevante debe poder relacionarse con el cambio que la origino.

## Continuidad

Una nueva version debe permitir comprender la relacion con la version anterior.

## Estabilidad

Una version congelada representa un estado estable y no debe modificarse sin una causa valida.

## Claridad

El numero de version debe comunicar la magnitud del cambio.

## Consistencia

Los documentos del proyecto deben aplicar las mismas reglas de versionado.

---

# FORMATO DE VERSION

Condor utilizara el formato:

`MAJOR.MINOR.PATCH`

Ejemplo:

`2.4.1`

Donde:

- `MAJOR` representa cambios incompatibles o una redefinicion fundamental.
- `MINOR` representa nuevas capacidades compatibles.
- `PATCH` representa correcciones o ajustes compatibles.

---

# MAJOR

Se incrementara `MAJOR` cuando exista un cambio que altere de forma incompatible:

- la identidad de un artefacto;
- su contrato principal;
- su comportamiento esencial;
- una regla arquitectonica fundamental;
- una estructura que obligue a adaptar consumidores existentes.

Un cambio de `MAJOR` requiere documentar la razon y el impacto.

---

# MINOR

Se incrementara `MINOR` cuando se incorporen capacidades nuevas sin romper el comportamiento establecido.

Ejemplos:

- nuevas secciones documentales;
- nuevas capacidades compatibles;
- nuevas reglas que no contradigan las existentes;
- ampliaciones funcionales compatibles.

---

# PATCH

Se incrementara `PATCH` cuando el cambio sea correctivo y no altere el comportamiento esencial.

Ejemplos:

- correcciones de errores;
- mejoras de redaccion;
- correcciones de referencias;
- ajustes de consistencia;
- correcciones menores de estructura.

---

# VERSION INICIAL

La primera version estable de un artefacto se identificara como:

`1.0.0`

Las versiones `0.x.x` podran utilizarse cuando el artefacto se encuentre en desarrollo experimental y su contrato aun no sea estable.

---

# VERSION Y ESTADO

La version y el estado son conceptos diferentes.

La version identifica el estado del artefacto.

El estado indica su situacion dentro del ciclo de trabajo.

Ejemplos de estado:

- Pendiente
- En progreso
- Activo
- Listo
- Congelado
- Vigente
- Completado

Un documento puede ser:

`Version: 1.0.0`
`Estado: Activo`

o:

`Version: 1.0.0`
`Estado: Congelado`

---

# VERSIONADO DOCUMENTAL

Todo documento oficial debera contener internamente:

- nombre;
- version;
- estado;
- nivel;
- clasificacion;
- historial de cambios.

El nombre oficial del documento no cambia cuando cambia su version.

Ejemplo:

`EVOLUCION.md`

puede evolucionar:

`1.0.0`

↓

`1.1.0`

↓

`1.1.1`

sin cambiar su nombre.

---

# HISTORIAL DE CAMBIOS

Cada documento oficial debera conservar un historial de cambios.

El historial debera indicar como minimo:

- version;
- cambio realizado.

Cuando el cambio sea relevante, debera registrar tambien su impacto o motivo.

---

# VERSIONADO Y GIT

Git constituye el historial oficial del proyecto.

La version interna del documento no reemplaza los commits, ramas, tags ni demas mecanismos de control de versiones.

Cuando corresponda, una version estable importante podra asociarse a un tag de Git.

La relacion entre documento y Git debera conservarse mediante trazabilidad.

---

# VERSIONADO DEL PROYECTO

El proyecto completo podra utilizar una version propia independiente de la version individual de sus documentos.

La version del proyecto debera representar una linea base integrada y verificable.

No debera incrementarse la version global simplemente por modificar un documento aislado.

---

# LINEA BASE

Una linea base representa un conjunto de artefactos considerados coherentes y estables para un determinado estado del proyecto.

Una linea base debera poder identificarse mediante:

- version;
- fecha;
- estado;
- conjunto de artefactos;
- criterios de estabilidad.

---

# CAMBIOS ENTRE LINEAS BASE

Antes de establecer una nueva linea base debera verificarse:

- coherencia arquitectonica;
- ausencia de regresiones conocidas;
- documentacion actualizada;
- trazabilidad;
- compatibilidad cuando aplique;
- cumplimiento de los criterios de aceptacion.

---

# CAMBIOS INCOMPATIBLES

Cuando una modificacion rompa compatibilidad debera:

1. identificarse explicitamente;
2. documentarse;
3. evaluar su impacto;
4. definir la estrategia de migracion cuando corresponda;
5. actualizar la version afectada.

No debera ocultarse una ruptura de compatibilidad dentro de una version menor o de parche.

---

# VERSIONADO Y CONGELAMIENTO

Un artefacto congelado conserva la version declarada hasta que exista una causa valida para modificarlo.

Si se modifica:

- debera incrementarse su version;
- debera registrarse el cambio;
- debera revisarse su impacto;
- debera volver a verificarse;
- debera volver a congelarse cuando corresponda.

---

# VERSIONADO Y EVOLUCION

Toda evolucion relevante debera determinar primero el nivel de version que corresponde.

El cambio no debera clasificarse por cantidad de lineas modificadas, sino por su impacto sobre el comportamiento, contrato, arquitectura o compatibilidad.

---

# VERSIONADO Y TRAZABILIDAD

Cuando un cambio sea relevante, debera poder recorrerse:

Necesidad

↓

Decision

↓

Cambio

↓

Version

↓

Verificacion

↓

Linea base

El objetivo es poder responder:

- por que existe esta version;
- que cambio contiene;
- que impacto produjo;
- desde que version evoluciono.

---

# REGLAS

1. Ninguna version debera inventarse sin representar un cambio real.
2. Ningun cambio incompatible debera ocultarse como PATCH.
3. Ningun documento oficial debera perder su historial de cambios.
4. El nombre del documento no cambia por version.
5. Git mantiene el historial tecnico.
6. La version interna mantiene la identidad del estado del artefacto.
7. Una version estable debe poder ser reconstruida y comprendida.
8. Todo cambio relevante debe conservar trazabilidad.

---

# RELACION CON OTROS DOCUMENTOS

Este documento se relaciona principalmente con:

- EVOLUCION.md
- MEJORA_CONTINUA.md
- ESTADO_PROYECTO.md
- DIRECTIVA_OPERATIVA_PROYECTO_CONDOR.md
- REGISTRO_DEUDA_ARQUITECTONICA.md
- COMPATIBILIDAD.md
- AUDITORIA.md
- DEUDA_EVOLUTIVA.md

---

# HISTORIAL DE CAMBIOS

| Version | Cambios |
|---------|---------|
| 1.0.0 | Creacion de las reglas de versionado del Proyecto Condor para el Nivel 09. |
