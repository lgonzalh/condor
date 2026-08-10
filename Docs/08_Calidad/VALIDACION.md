# VALIDACION

Version: 1.0.0
Estado: En desarrollo
Nivel: 08 - Calidad
Clasificacion: Proceso de Ingenieria

---

# PROPOSITO

Definir el proceso mediante el cual Condor determina si un resultado cumple los requisitos, criterios, restricciones y condiciones de calidad establecidos antes de avanzar en el ciclo de vida.

La validacion proporciona evidencia para decidir si un resultado es aceptable para su proposito.

---

# ALCANCE

Aplica a:

- requisitos;
- disenos;
- componentes;
- codigo;
- configuraciones;
- integraciones;
- interfaces;
- documentacion;
- procesos;
- entregables;
- cambios y correcciones.

La validacion se aplica de forma proporcional al riesgo, impacto y alcance del resultado.

---

# PRINCIPIO DE VALIDACION

Condor no debe asumir que un resultado es correcto porque fue generado, compilado o ejecutado.

La conformidad debe demostrarse mediante evidencia verificable.

---

# OBJETIVOS

- Confirmar que el resultado cumple su proposito.
- Verificar el cumplimiento de requisitos.
- Detectar incumplimientos antes del congelamiento.
- Proporcionar evidencia objetiva.
- Evitar regresiones.
- Mantener la trazabilidad entre criterio y resultado.
- Permitir repetir la validacion cuando el resultado cambie.

---

# RELACION CON EL CICLO CONDOR

La validacion forma parte del ciclo:

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

La validacion puede ejecutarse en diferentes momentos del ciclo y no debe reservarse exclusivamente para el final.

---

# VALIDACION Y VERIFICACION

## Verificacion

Determina si el resultado fue construido conforme a las especificaciones, restricciones y criterios definidos.

Pregunta principal:

> ¿Se construyo correctamente?

## Validacion

Determina si el resultado satisface la necesidad y el proposito para el que fue construido.

Pregunta principal:

> ¿Se construyo lo correcto?

Ambas actividades son complementarias.

---

# ENTRADAS

La validacion puede utilizar como entrada:

- requisitos;
- criterios de aceptacion;
- especificaciones;
- decisiones arquitectonicas;
- contratos;
- restricciones;
- escenarios de uso;
- resultados esperados;
- documentacion del nivel;
- artefactos implementados;
- evidencia de pruebas.

---

# SALIDAS

Una validacion debe producir como minimo:

- resultado de validacion;
- evidencia utilizada;
- criterios evaluados;
- incumplimientos encontrados, si existen;
- decision de conformidad;
- acciones pendientes, cuando correspondan.

---

# ESTADOS DE VALIDACION

Un resultado puede encontrarse en uno de los siguientes estados:

- Pendiente
- En validacion
- Conforme
- Conforme con observaciones
- No conforme
- Bloqueado

## Conforme

Todos los criterios aplicables fueron satisfechos.

## Conforme con observaciones

Los criterios esenciales fueron satisfechos y existen observaciones que no impiden continuar.

## No conforme

Uno o mas criterios relevantes no fueron satisfechos.

## Bloqueado

La validacion no puede completarse por una dependencia o evidencia faltante.

---

# NIVELES DE VALIDACION

## Validacion de requisitos

Comprueba que los requisitos:

- son claros;
- son verificables;
- son coherentes;
- tienen alcance definido;
- no presentan contradicciones conocidas.

## Validacion de diseno

Comprueba que el diseno:

- responde a los requisitos;
- respeta la arquitectura;
- define responsabilidades;
- contempla dependencias;
- permite una implementacion verificable.

## Validacion de implementacion

Comprueba que el resultado implementado:

- corresponde al diseno;
- cumple los contratos;
- mantiene las restricciones;
- presenta el comportamiento esperado.

## Validacion de integracion

Comprueba que los componentes funcionan correctamente dentro del contexto en el que deben operar.

## Validacion de sistema

Comprueba el comportamiento del sistema como conjunto.

## Validacion de experiencia

Cuando corresponda, comprueba que la experiencia resultante responde a los escenarios y criterios definidos para el usuario.

## Validacion documental

Comprueba que la documentacion representa el estado real del artefacto y conserva la trazabilidad necesaria.

---

# METODOS DE VALIDACION

Dependiendo del resultado y del riesgo, Condor podra utilizar:

- inspeccion;
- revision documental;
- analisis estatico;
- pruebas automatizadas;
- pruebas manuales;
- ejecucion controlada;
- pruebas de integracion;
- pruebas de sistema;
- pruebas de aceptacion;
- comparacion contra criterios definidos;
- revision arquitectonica.

La seleccion del metodo debe ser proporcional al riesgo y al impacto.

---

# EVIDENCIA

La validacion debe apoyarse en evidencia suficiente y pertinente.

Ejemplos:

- resultados de pruebas;
- registros de ejecucion;
- salidas esperadas y obtenidas;
- capturas o evidencias de interfaz cuando sean necesarias;
- resultados de herramientas;
- informes de analisis;
- revisiones documentales;
- registros de errores;
- trazabilidad de requisitos.

La evidencia debe permitir comprender por que se determino el resultado de la validacion.

---

# CRITERIOS DE VALIDACION

Antes de validar un resultado deben identificarse los criterios aplicables.

Cada criterio debe poder clasificarse como:

- Cumple
- No cumple
- No aplica
- No verificable

Un criterio marcado como No verificable debe generar una observacion o bloqueo cuando sea necesario para determinar la conformidad.

---

# PROCESO

## 1. Identificar el objeto

Determinar exactamente que se va a validar.

## 2. Identificar los criterios

Determinar contra que condiciones se evaluara.

## 3. Preparar la evidencia

Seleccionar pruebas, inspecciones, resultados o informacion necesaria.

## 4. Ejecutar

Realizar las comprobaciones correspondientes.

## 5. Registrar resultados

Conservar los resultados y evidencias relevantes.

## 6. Evaluar conformidad

Determinar el estado de validacion.

## 7. Corregir

Cuando exista una no conformidad, ejecutar la accion correctiva correspondiente.

## 8. Revalidar

Toda correccion relevante debe volver a validarse.

## 9. Registrar cierre

Conservar el resultado final y dejar preparada la trazabilidad.

---

# VALIDACION AUTOMATIZADA

Condor debera automatizar progresivamente las validaciones repetibles cuando las capacidades disponibles lo permitan.

La automatizacion dependera de:

- hardware disponible;
- modelo LLM;
- herramientas;
- estabilidad;
- costo computacional;
- beneficio obtenido.

Cuando una validacion no pueda automatizarse, debera existir una alternativa manual o semiautomatica.

La limitacion tecnologica no elimina la necesidad de validar.

---

# AUTOVALIDACION DE CONDOR

Cuando sea posible, Condor debera validar sus propios resultados antes de presentarlos como terminados.

La autovalidacion debe buscar como minimo:

- incumplimientos evidentes;
- contradicciones;
- omisiones;
- errores de estructura;
- violaciones de restricciones;
- inconsistencias con la arquitectura;
- criterios de aceptacion no satisfechos;
- documentacion desactualizada.

La autovalidacion no sustituye la validacion formal cuando esta sea necesaria.

---

# REVALIDACION

Debe realizarse una nueva validacion cuando:

- se modifica el resultado;
- cambia un requisito relevante;
- cambia una dependencia;
- cambia la arquitectura afectada;
- se corrige una no conformidad;
- aparece una regresion;
- cambia una restriccion aplicable.

La profundidad de la revalidacion dependera del impacto del cambio.

---

# CRITERIO DE APROBACION

Un resultado puede avanzar hacia congelamiento cuando:

- los criterios obligatorios cumplen;
- las evidencias requeridas estan disponibles;
- no existen no conformidades criticas abiertas;
- las observaciones restantes estan identificadas;
- la documentacion aplicable esta actualizada;
- la trazabilidad necesaria esta preservada.

La aprobacion no implica que el resultado sea perfecto. Implica que cumple las condiciones establecidas para el estado en el que se encuentra.

---

# TRAZABILIDAD

Cada validacion relevante debe poder relacionarse con:

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

Cuando exista implementacion posterior, la trazabilidad puede continuar hasta la version correspondiente.

---

# RELACION CON PRUEBAS

Las pruebas constituyen una fuente de evidencia para la validacion.

No toda validacion es una prueba y no toda prueba constituye por si sola una validacion completa.

La estrategia detallada de pruebas pertenece a `PRUEBAS.md`.

---

# RELACION CON CALIDAD

`CALIDAD.md` establece el marco general de calidad del Nivel 08.

`VALIDACION.md` define como se determina la conformidad mediante criterios y evidencia.

Los documentos posteriores del nivel complementaran este proceso mediante pruebas, criterios de aceptacion, metricas, aseguramiento y trazabilidad.

---

# REGLAS

1. No asumir conformidad sin evidencia suficiente.
2. Validar contra criterios previamente identificados.
3. Registrar los resultados relevantes.
4. Revalidar despues de correcciones relevantes.
5. Mantener trazabilidad entre criterio y evidencia.
6. No utilizar la ausencia de errores conocidos como unica evidencia de conformidad.
7. Ajustar la profundidad de validacion al riesgo.
8. Automatizar validaciones repetibles cuando sea viable.
9. No confundir validacion con pruebas.
10. No congelar un resultado sin cumplir los criterios obligatorios de validacion.

---

# RESULTADO ESPERADO

La validacion debe permitir a Condor responder de forma objetiva:

- ¿El resultado satisface la necesidad?
- ¿Cumple los requisitos?
- ¿Respeta las restricciones?
- ¿Existe evidencia suficiente?
- ¿Que criterios fueron evaluados?
- ¿Que incumplimientos permanecen?
- ¿Puede continuar hacia el siguiente estado?

La validacion convierte una suposicion de correccion en una decision sustentada por evidencia.

---

# HISTORIAL DE CAMBIOS

| Version | Cambios |
|----------|---------|
| 1.0.0 | Creacion del proceso general de validacion del Nivel 08. |
