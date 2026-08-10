# AUDITORIA

Version: 1.0.1
Estado: Activo
Nivel: 09 - Evolucion
Clasificacion: Auditoria

---

# PROPOSITO

Definir el proceso de auditoria del Proyecto Condor para evaluar periodicamente su coherencia, cumplimiento, trazabilidad, estado documental, arquitectura y capacidad de continuidad.

La auditoria proporciona una evaluacion estructurada del estado real del proyecto y permite detectar desviaciones antes de que comprometan su evolucion.

---

# ALCANCE

La auditoria puede evaluar:

- ADN y principios;
- directivas;
- arquitectura;
- documentacion;
- estado del proyecto;
- inventario documental;
- trazabilidad;
- calidad;
- compatibilidad;
- versionado;
- deuda;
- cumplimiento metodologico;
- continuidad;
- coherencia entre artefactos.

La auditoria no sustituye la revision de un entregable ni la validacion de una implementacion concreta.

---

# PRINCIPIOS

## Independencia del resultado

La auditoria debe evaluar el estado real del proyecto y no asumir conformidad por el simple hecho de que exista documentacion.

## Evidencia

Toda conclusion relevante debe sustentarse en evidencia verificable.

## Trazabilidad

Los hallazgos deben poder relacionarse con el artefacto, requisito, regla o decision afectada.

## Objetividad

Los hallazgos deben describirse sin alterar artificialmente su severidad para facilitar el cierre.

## Continuidad

La auditoria debe ayudar a que el proyecto pueda continuar con conocimiento suficiente y sin inconsistencias ocultas.

## Mejora

Un hallazgo no debe convertirse automaticamente en un bloqueo. Debe evaluarse segun su impacto y prioridad.

---

# TIPOS DE AUDITORIA

## Auditoria documental

Verifica existencia, estructura, version, estado, dependencias y coherencia de los documentos.

## Auditoria arquitectonica

Verifica que las decisiones y componentes respeten la arquitectura definida.

## Auditoria metodologica

Verifica el cumplimiento del ciclo:

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

## Auditoria de trazabilidad

Verifica que los cambios relevantes puedan recorrerse desde su origen hasta su resultado y version.

## Auditoria de compatibilidad

Verifica las reglas y declaraciones de compatibilidad establecidas por el proyecto.

## Auditoria de continuidad

Verifica que un desarrollador pueda comprender el estado actual y continuar el proyecto sin depender de conocimiento exclusivo de una conversacion.

---

# MOMENTOS DE AUDITORIA

La auditoria podra realizarse:

- al finalizar un nivel;
- antes de establecer una nueva linea base;
- antes de una evolucion relevante;
- despues de cambios arquitectonicos importantes;
- cuando se detecten inconsistencias;
- de forma periodica cuando el proyecto lo determine.

El cierre de un nivel requiere verificar que los criterios definidos para ese nivel hayan sido cumplidos.

---

# PREPARACION

Antes de una auditoria deberan identificarse:

- alcance;
- artefactos a evaluar;
- criterios aplicables;
- versiones vigentes;
- dependencias;
- estado oficial del proyecto;
- inventario documental vigente.

La fuente oficial para determinar el nivel activo es `ESTADO_PROYECTO.md`.

El inventario oficial de artefactos es `INVENTARIO_PROYECTO.md`.

---

# AREAS DE VERIFICACION

## 1. Identidad

Verificar:

- coherencia con el ADN;
- preservacion de principios;
- ausencia de decisiones que contradigan la identidad del proyecto.

## 2. Directivas

Verificar:

- cumplimiento de las reglas globales;
- prioridad documental;
- alcance correcto;
- separacion entre niveles;
- ausencia de reglas contradictorias.

## 3. Documentacion

Verificar:

- documentos existentes registrados;
- nombres correctos;
- versiones identificables;
- estados coherentes;
- dependencias validas;
- historial conservado.

## 4. Inventario

Verificar:

- correspondencia entre documentos realmente entregados e inventario;
- ausencia de documentos existentes sin registrar;
- ausencia de documentos marcados como planificados cuando ya fueron entregados;
- consistencia entre version, estado y clasificacion;
- correspondencia entre el inventario y la linea documental del nivel.

El inventario no determina el nivel activo.

## 5. Arquitectura

Verificar:

- coherencia entre decisiones y componentes;
- dependencias conocidas;
- ausencia de contradicciones arquitectonicas;
- respeto de niveles congelados.

## 6. Trazabilidad

Verificar la cadena aplicable:

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

Esta cadena debe utilizarse de acuerdo con los mecanismos de trazabilidad vigentes del proyecto.

## 7. Calidad

Verificar:

- criterios de aceptacion;
- pruebas aplicables;
- defectos;
- regresiones;
- no conformidades;
- documentacion de resultados.

La autoauditoria puede utilizarse antes de una auditoria formal cuando las capacidades disponibles lo permitan.

## 8. Evolucion

Verificar:

- cambios controlados;
- versionado correcto;
- compatibilidad evaluada;
- migraciones documentadas;
- deuda registrada;
- roadmap coherente.

## 9. Continuidad

Verificar:

- estado actual comprensible;
- siguiente accion identificable;
- conocimiento permanente;
- ausencia de dependencia critica de conversaciones.

---

# HALLAZGOS

Un hallazgo representa una diferencia verificable entre el estado esperado y el estado observado.

Los hallazgos se clasifican como:

## Critico

Compromete gravemente la arquitectura, continuidad, seguridad, integridad o capacidad de avanzar.

## Alto

Afecta de forma importante el proyecto y requiere tratamiento prioritario.

## Medio

Representa una desviacion relevante que debe corregirse o planificarse.

## Bajo

Representa una mejora o desviacion menor que no compromete la continuidad.

---

# REGISTRO DE HALLAZGOS

Cada hallazgo relevante debera registrar:

| Campo | Descripcion |
|-------|-------------|
| ID | Identificador unico |
| Area | Area auditada |
| Artefacto | Documento o componente afectado |
| Criterio | Regla o condicion evaluada |
| Evidencia | Evidencia encontrada |
| Hallazgo | Descripcion de la desviacion |
| Severidad | Critica, Alta, Media o Baja |
| Accion | Accion requerida |
| Responsable | Responsable definido cuando corresponda |
| Estado | Estado del hallazgo |
| Version objetivo | Version prevista para resolverlo |

---

# ESTADOS DE HALLAZGO

- Abierto
- En analisis
- Planificado
- En correccion
- Verificado
- Cerrado
- Aceptado

Un hallazgo no debera marcarse como cerrado sin evidencia de su tratamiento y verificacion cuando corresponda.

---

# NO CONFORMIDADES

Una no conformidad es un incumplimiento verificable de:

- requisito;
- criterio;
- restriccion;
- contrato;
- arquitectura;
- directiva;
- estandar.

Las no conformidades deberan gestionarse conforme a su severidad.

Un hallazgo de auditoria puede generar una no conformidad, deuda o mejora, segun su naturaleza.

---

# RELACION CON LA DEUDA

Un hallazgo que no sea bloqueante podra generar una entrada en:

`REGISTRO_DEUDA_ARQUITECTONICA.md`

o

`DEUDA_EVOLUTIVA.md`

La auditoria no modifica automaticamente estos registros. El hallazgo debe conservar su trazabilidad.

---

# SINCRONIZACION DOCUMENTAL

La auditoria debera distinguir entre:

- artefactos entregados durante un nivel;
- estado operativo del nivel;
- inventario de artefactos;
- cierre formal del nivel.

La entrega de un artefacto no implica por si misma el cierre del nivel.

`ESTADO_PROYECTO.md` se actualiza como parte del cierre formal del nivel mediante `condorcerrar`.

`INVENTARIO_PROYECTO.md` debera actualizarse durante `condorcerrar` para reflejar el conjunto definitivo de artefactos entregados y su estado posterior al cierre.

Por tanto:

Entrega

↓

Revision

↓

Correcciones

↓

Congelamiento

↓

Condorcerrar

↓

Actualizar ESTADO_PROYECTO

↓

Actualizar INVENTARIO_PROYECTO

↓

Cerrar nivel

↓

Activar siguiente nivel

Esta secuencia evita que los documentos de control global queden desincronizados respecto de los entregables reales.

---

# RESULTADO DE LA AUDITORIA

Una auditoria debera concluir con uno de los siguientes resultados:

## Conforme

No se identifican desviaciones relevantes dentro del alcance auditado.

## Conforme con observaciones

Existen hallazgos menores que no comprometen la continuidad.

## Requiere correccion

Existen hallazgos que deben ser tratados antes de establecer el estado objetivo.

## Bloqueado

Existe al menos un hallazgo critico que impide continuar de forma segura o coherente.

---

# CRITERIO DE CIERRE

Una auditoria se considerara cerrada cuando:

1. el alcance haya sido evaluado;
2. los hallazgos hayan sido registrados;
3. las acciones requeridas hayan sido definidas;
4. los hallazgos criticos hayan sido resueltos o formalmente aceptados;
5. la evidencia necesaria haya sido conservada;
6. el resultado de auditoria haya sido documentado.

---

# AUDITORIA Y CONGELAMIENTO

Una auditoria puede ser requisito previo para establecer una nueva linea base o congelar un conjunto de artefactos cuando el alcance del cambio lo requiera.

El resultado de auditoria no sustituye `condorcongelar`.

El congelamiento continua siendo una accion explicita del proceso Condor.

---

# AUDITORIA Y AUTOAUDITORIA

La autoauditoria es una comprobacion previa realizada por Condor cuando sus capacidades lo permitan.

Debe buscar como minimo:

- contradicciones;
- omisiones;
- duplicidades;
- incumplimiento de restricciones;
- inconsistencias arquitectonicas;
- inconsistencias documentales;
- errores evidentes;
- trazabilidad incompleta;
- desincronizacion entre artefactos e inventario;
- desincronizacion entre el estado operativo y los artefactos entregados.

La autoauditoria reduce defectos detectables, pero no sustituye la auditoria formal ni la decision de aceptacion.

---

# REGLAS

1. No declarar conformidad sin evidencia suficiente.
2. No ocultar hallazgos.
3. No modificar el resultado para facilitar el cierre.
4. No confundir auditoria con implementacion.
5. No modificar automaticamente niveles congelados.
6. Toda desviacion relevante debe conservar trazabilidad.
7. Todo hallazgo debe tener un estado.
8. Los hallazgos deben evaluarse segun impacto real.
9. La auditoria debe dejar el proyecto mas comprensible que antes.
10. La auditoria debe facilitar la continuidad.
11. La entrega de documentos no equivale al cierre de un nivel.
12. El cierre de un nivel debe sincronizar `ESTADO_PROYECTO.md` e `INVENTARIO_PROYECTO.md`.

---

# RELACION CON OTROS DOCUMENTOS

Este documento se relaciona principalmente con:

- EVOLUCION.md
- MEJORA_CONTINUA.md
- VERSIONADO.md
- MIGRACION.md
- COMPATIBILIDAD.md
- REGISTRO_DEUDA_ARQUITECTONICA.md
- DEUDA_EVOLUTIVA.md
- ESTADO_PROYECTO.md
- INVENTARIO_PROYECTO.md
- DIRECTIVA_OPERATIVA_PROYECTO_CONDOR.md

---

# HISTORIAL DE CAMBIOS

| Version | Cambios |
|---------|---------|
| 1.0.1 | Se incorpora la auditoria del inventario y la regla de sincronizacion durante `condorcerrar` entre los artefactos entregados, `INVENTARIO_PROYECTO.md` y `ESTADO_PROYECTO.md`. Se eliminan referencias no establecidas a `TRAZABILIDAD.md` y `CALIDAD.md`. |
| 1.0.0 | Creacion del marco de auditoria del Proyecto Condor para el Nivel 09. |
