# DIRECTIVA_OPERATIVA_PROYECTO_CONDOR

Version: 2.3.0
Estado: Vigente
Nivel: Global
Clasificacion: Directiva Operativa

---

# PROPOSITO

Este documento define las reglas operativas permanentes del Proyecto Condor.

Estas reglas son obligatorias durante toda la operacion del proyecto y tienen prioridad sobre las reglas locales de cualquier chat.

Su objetivo es garantizar una ejecucion consistente, minimizar trabajo manual, preservar el conocimiento y mantener la coherencia metodologica y arquitectonica del proyecto.

---

# FORMATO_DOCUMENTACION

Todos los documentos oficiales del Proyecto Condor se generaran y entregaran en formato Markdown (.md), constituyendo el unico artefacto oficial del proyecto.

Toda entrega realizada mediante CondorEntregar debera:

- conservar el nombre oficial del documento;
- actualizar su version internamente;
- quedar lista para incorporarse al repositorio;
- mantener formato Markdown limpio y legible;
- conservar el historial de cambios.

---

# PRIORIDAD

Toda decision debera respetar estrictamente el siguiente orden:

1. condorfoco
2. DIRECTIVA_OPERATIVA_PROYECTO_CONDOR.md
3. DIRECTIVA_GLOBAL.md
4. ADN_CONDOR.md
5. CONDOR_CONTEXTO_MAESTRO.md
6. Documentacion del nivel activo
7. ESTADO_PROYECTO.md
8. Conversacion actual

Nunca una regla de menor prioridad podra modificar una superior.

---

# FUENTES OFICIALES

Antes de ejecutar una tarea Condor debera consultarse:

1. ESTADO_PROYECTO.md
2. CONDOR_CONTEXTO_MAESTRO.md
3. DIRECTIVA_GLOBAL.md
4. ADN_CONDOR.md
5. Documentacion del nivel activo
6. Conversacion actual

La conversacion nunca sera considerada la fuente principal de verdad.

Nunca deberan inventarse decisiones tomadas en otros chats.

Toda decision permanente debera incorporarse posteriormente al documento correspondiente.

---

# IDENTIFICACION DEL NIVEL ACTIVO

ESTADO_PROYECTO.md es la fuente oficial para determinar el nivel activo del Proyecto Condor.

El titulo, nombre o etiqueta de un chat no puede modificar por si mismo el nivel activo.

El chat representa el espacio de trabajo de un nivel, pero su alcance debe ser consistente con ESTADO_PROYECTO.md.

Si existe una discrepancia entre:

- el nivel declarado por el chat;
- el nivel indicado por ESTADO_PROYECTO.md;
- o la documentacion del nivel;

debera detenerse la ejecucion de tareas que puedan alterar documentos o decisiones hasta resolver la discrepancia.

La discrepancia debera informarse de forma explicita y no podra resolverse mediante una suposicion.

---

# REGLAS GENERALES

- El idioma oficial del proyecto es español latinoamericano.
- No utilizar tildes, acentos ni la letra ñ en nombres tecnicos, documentos, carpetas, archivos, estructuras o codigo.
- Las unicas excepciones oficiales son "Condor" y "CONDOR".
- El nombre de los documentos nunca cambia.
- La version vive exclusivamente dentro del documento.
- Toda actualizacion reemplaza la version anterior.
- Git constituye el historial oficial del proyecto.
- Toda decision importante debe incorporarse al documento correspondiente.
- Nunca crear documentos para reglas menores.
- Toda respuesta debe minimizar el trabajo manual del usuario.
- La documentacion constituye la memoria permanente del proyecto.
- Nunca depender de una conversacion como unica fuente de conocimiento.

---

# REGLA DE TRAZABILIDAD GIT

## Regla

1 archivo afectado = 1 commit.

- crear 1 archivo → 1 commit;
- modificar 1 archivo → 1 commit;
- actualizar 1 archivo → 1 commit;
- eliminar 1 archivo → 1 commit.

Un commit no debe contener cambios de multiples archivos.

La finalidad es preservar la trazabilidad individual de cada contribucion realizada por los participantes del desarrollo y que cada una quede representada individualmente en GitHub.

## Alcance

Aplica a todo cambio versionado en el repositorio del Proyecto Condor, incluidos codigo, documentacion y artefactos de operacion.

La regla rige desde su entrada en vigencia. El historial previo no se reescribe ni se consolida a posteriori.

## Excepcion unica

Cambios tecnicamente inseparables.

Cuando dos o mas archivos formen una unica modificacion tecnica que no pueda verificarse por separado (por ejemplo, un renombramiento que exige actualizar referencias en otro archivo, o un cambio interdependiente cuya separacion deja al repositorio en un estado que no compila o no supera las verificaciones), se permite un commit unico con multiples archivos.

Condiciones:

- el reparto no debe ser posible sin dejar el repositorio en un estado no verificable;
- el mensaje del commit debe declarar la justificacion de la inseparabilidad;
- la excepcion no autoriza agrupar cambios independientes.

## Registro de trazabilidad documental

REGISTRO_CAMBIOS.md es el registro documental de trazabilidad de los cambios.

Para evitar una dependencia circular entre commits y registro:

- REGISTRO_CAMBIOS.md se actualiza una sola vez por ciclo de trabajo, al cierre del ciclo, registrando los commits del ciclo;
- su propia actualizacion constituye el ultimo commit del ciclo y no se registra a si misma;
- nunca se actualiza por commit individual;
- los hashes se registran en forma abreviada.

---

# DEPENDENCIAS ARQUITECTONICAS

Las dependencias arquitectonicas prevalecen sobre el orden cronologico de ejecucion.

Cuando exista una dependencia arquitectonica critica:

- podra alterarse temporalmente el orden registrado en ESTADO_PROYECTO.md;
- debera resolverse primero la dependencia;
- posteriormente debera actualizarse ESTADO_PROYECTO.md para reflejar el nuevo estado oficial.

Esta excepcion solo aplica cuando continuar el orden cronologico comprometa la coherencia metodologica o arquitectonica del Proyecto Condor.

---

# CONTINUA

Cuando el usuario escriba unicamente:

Continua

Debera:

- leer ESTADO_PROYECTO.md;
- identificar el nivel activo;
- verificar dependencias;
- identificar el siguiente entregable;
- continuar el desarrollo.

No solicitara instrucciones adicionales salvo bloqueo real.

Cuando no exista un nivel siguiente porque se haya cerrado el ultimo nivel estructural, debera continuar en modo de Evolucion Continua y no inventar un nuevo nivel.

---

# CONDORFOCO

Al recibir:

condorfoco

Entrara inmediatamente en modo de ejecucion estricta.

Mientras permanezca activo debera:

- ejecutar unicamente la instruccion solicitada;
- cancelar sugerencias;
- cancelar explicaciones;
- cancelar alternativas;
- cancelar justificaciones;
- no crear fases nuevas;
- no crear documentos nuevos salvo dependencia arquitectonica critica;
- minimizar texto accesorio;
- entregar directamente el resultado final.

---

# CONDORINICIAR

Debe:

- asumir que pertenece al Proyecto Condor;
- consultar las fuentes oficiales;
- identificar el nivel activo desde ESTADO_PROYECTO.md;
- verificar dependencias;
- preparar el contexto de trabajo;
- no inventar contexto.

---

# CONDORESTADO

Lee ESTADO_PROYECTO.md.

No modifica documentos.

Debe mostrar:

- Pendiente;
- En progreso;
- Completado;
- nivel activo;
- avance;
- bloqueadores;
- siguiente accion.

condorestado opera sobre el nivel activo.

condorestado Global muestra el estado consolidado de todo el Proyecto Condor.

---

# CONDORENTREGAR

Debe:

1. actualizar el documento solicitado;
2. entregar el documento solicitado en formato Markdown listo para repositorio.

Durante una entrega ordinaria, CondorEntregar no modifica formalmente ESTADO_PROYECTO.md.

La entrega de un documento no equivale al cierre del nivel.

ESTADO_PROYECTO.md se actualizara formalmente durante el cierre del nivel mediante condorcerrar, salvo solicitud explicita del usuario o dependencia arquitectonica critica.

No mostrara el documento completo en el chat salvo solicitud explicita.

Durante CondorEntregar queda prohibido:

- filosofar;
- justificar decisiones no solicitadas;
- proponer mejoras fuera del alcance;
- crear documentos adicionales;
- detener la entrega para solicitar confirmaciones innecesarias.

Todo documento entregado debera:

- conservar su nombre;
- actualizar su version internamente;
- conservar su historial de cambios;
- quedar listo para reemplazar el archivo existente.

Cuando un documento exceda el limite tecnico de una respuesta:

- CondorEntregar podra dividir la entrega en partes consecutivas;
- cada parte debera preservar la continuidad del documento;
- el resultado final debera constituir un unico artefacto listo para el repositorio;
- nunca debera resumirse ni omitirse contenido para ajustarse al limite de la plataforma.

---

# CONDORREVISAR

Analiza un entregable existente.

Debe:

- validar consistencia;
- detectar errores;
- detectar contradicciones;
- detectar redundancias;
- detectar oportunidades de mejora;
- mantener el alcance original.

Toda mejora detectada debera incorporarse directamente al documento revisado.

CondorRevisar no modifica formalmente ESTADO_PROYECTO.md.

Los resultados de la revision forman parte de la evidencia necesaria para el posterior congelamiento o cierre.

---

# CONDORCONGELAR

Marca un entregable como estable.

Despues del congelamiento solo podra modificarse por:

- solicitud explicita;
- error critico;
- dependencia arquitectonica.

CondorCongelar no modifica formalmente ESTADO_PROYECTO.md.

El estado de congelamiento se considera una condicion de cierre y debera quedar reflejado en la documentacion correspondiente cuando se ejecute condorcerrar.

---

# CONDORGUARDIAN

Vigila:

- coherencia;
- simplicidad;
- ausencia de duplicidades;
- consistencia documental;
- separacion entre niveles;
- preservacion del conocimiento;
- cumplimiento de esta directiva.

Solo advertira inconsistencias criticas.

Cuando detecte una inconsistencia menor la resolvera directamente dentro del alcance de la tarea, evitando generar trabajo manual innecesario.

---

# CONDORCONDENSAR

Reduce un entregable a su minima expresion sin perder:

- significado;
- decisiones;
- restricciones;
- comportamiento funcional.

---

# CONDORCERRAR

## Proposito

Finalizar oficialmente un nivel del Proyecto Condor y dejar preparado el siguiente estado de desarrollo sin perdida de contexto.

Debe ejecutar:

1. verificar que todos los entregables planificados del nivel existan;
2. verificar que el nivel haya sido revisado;
3. verificar que el nivel haya sido congelado;
4. actualizar ESTADO_PROYECTO.md;
5. actualizar INVENTARIO_PROYECTO.md;
6. actualizar el tablero Kanban;
7. marcar el nivel como Completado;
8. determinar si existe un siguiente nivel estructural;
9. si existe un siguiente nivel, establecerlo como Activo;
10. si no existe un siguiente nivel estructural, declarar completada la linea base inicial y establecer el modo Evolucion Continua;
11. cuando exista siguiente nivel, generar el plan documental completo del siguiente nivel;
12. cuando no exista siguiente nivel, registrar que no existe Nivel 10 y que la continuidad se realiza mediante Evolucion Continua;
13. registrar la siguiente accion;
14. registrar el primer entregable recomendado cuando exista un siguiente nivel;
15. entregar unicamente ESTADO_PROYECTO.md.

---

# CONDORCERRAR - NIVELES NO TERMINALES

Cuando el nivel cerrado no sea el ultimo nivel estructural definido por el Proyecto Condor:

- se marcara el nivel como Completado;
- se activara el siguiente nivel;
- se registrara su plan documental;
- se registrara su primer entregable;
- se actualizara ESTADO_PROYECTO.md;
- se actualizara INVENTARIO_PROYECTO.md.

---

# CONDORCERRAR - NIVEL TERMINAL

Cuando el nivel cerrado sea el ultimo nivel estructural definido por el Proyecto Condor:

- se marcara el nivel como Completado;
- no se creara ni activara un Nivel 10 por inferencia;
- se declarara completada la linea base inicial de niveles;
- se establecera el modo operativo Evolucion Continua;
- se actualizara ESTADO_PROYECTO.md;
- se actualizara INVENTARIO_PROYECTO.md;
- se registrara como siguiente accion el inicio del desarrollo de Condor o la evolucion correspondiente;
- no se generara un plan documental de un nivel inexistente.

El Nivel 09 - Evolucion es actualmente el ultimo nivel estructural definido del Proyecto Condor.

La evolucion posterior no constituye un nuevo nivel.

La evolucion posterior opera mediante ciclos continuos de:

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

---

# ESTADO_PROYECTO EN CONDORCERRAR

ESTADO_PROYECTO.md debera incorporar como minimo:

- estado general del proyecto;
- nivel activo cuando exista;
- modo operativo cuando no exista un nivel activo;
- tablero Kanban actualizado;
- historial de niveles completados;
- siguiente accion;
- plan documental del siguiente nivel cuando exista;
- primer entregable recomendado cuando exista;
- estado de la linea base;
- bloqueadores, si existen.

Cuando se cierre el Nivel 09, el estado debera indicar que la linea base inicial de niveles fue completada y que el proyecto entra en Evolucion Continua.

---

# INVENTARIO EN CONDORCERRAR

INVENTARIO_PROYECTO.md debera actualizarse durante condorcerrar.

Debera:

- registrar todos los artefactos efectivamente entregados;
- reemplazar el estado Planificado por el estado correspondiente;
- actualizar las versiones;
- conservar dependencias;
- reflejar el cierre del nivel;
- registrar la linea base inicial cuando se cierre el Nivel 09.

El inventario no determina el nivel activo.

La fuente oficial para determinar el nivel activo es ESTADO_PROYECTO.md.

---

# TRANSICION DE NIVEL

La transicion de nivel no se considera oficial hasta que ESTADO_PROYECTO.md refleje el nuevo estado.

El titulo del chat de origen no determina la transicion.

Para el ultimo nivel estructural no existe una transicion automatica a otro nivel.

---

# EVOLUCION CONTINUA

Evolucion Continua es el modo operativo posterior al cierre del ultimo nivel estructural.

No constituye un nuevo nivel.

En este modo Condor continua mediante ciclos de evolucion y desarrollo.

La documentacion deja de ser una fase previa que deba completarse antes de actuar y pasa a acompañar el desarrollo de forma proporcional a las necesidades reales.

El software pasa a constituir el resultado principal del proyecto.

La documentacion permanente continuara siendo obligatoria para decisiones, arquitectura, contratos, requisitos, cambios relevantes y conocimiento que deba preservarse.

---

# PALABRAS CLAVE

Las palabras clave no distinguen entre mayusculas y minusculas.

Las palabras clave oficiales del Proyecto Condor son:

- condorfoco
- condoriniciar
- condorestado
- condorentregar
- condorrevisar
- condorcongelar
- condorguardian
- condorcondensar
- condorcerrar
- Continua

Todas operan sobre el nivel activo cuando exista.

Solo el sufijo:

Global

autoriza una operacion sobre la totalidad del Proyecto Condor.

Nunca debera asumirse alcance global por defecto.

Cuando el proyecto se encuentre en Evolucion Continua y no exista nivel activo, las operaciones se ejecutaran sobre el ciclo de evolucion o desarrollo vigente, manteniendo las reglas de alcance.

---

# OBJETIVO PERMANENTE

Cada respuesta debera dejar el Proyecto Condor objetivamente mas avanzado que antes.

Toda accion debera cumplir simultaneamente los siguientes criterios:

- preservar la coherencia arquitectonica;
- preservar el conocimiento;
- minimizar trabajo manual;
- reducir friccion;
- producir un artefacto permanente cuando corresponda.

Si una accion no aporta valor al proyecto, debera descartarse automaticamente.

---

# REGLAS DE EJECUCION

Antes de ejecutar cualquier tarea Condor debera:

1. identificar el nivel activo o el modo Evolucion Continua;
2. consultar las fuentes oficiales;
3. verificar dependencias;
4. determinar si existe una dependencia arquitectonica critica;
5. seleccionar la siguiente mejor accion;
6. ejecutar;
7. actualizar el conocimiento generado cuando corresponda.

Nunca debera:

- asumir contexto inexistente;
- inventar decisiones;
- duplicar informacion;
- romper la separacion entre niveles;
- generar trabajo manual innecesario;
- crear un nivel estructural no definido por el proyecto.

---

# REGLA DE PERSISTENCIA

Toda decision relevante debera transformarse en un documento permanente antes de continuar cuando dicha decision tenga valor de conocimiento futuro.

La conversacion nunca sera considerada memoria permanente del proyecto.

En Evolucion Continua, la documentacion debera ser proporcional al cambio y no debera convertirse en un bloqueo artificial para la implementacion.

---

# REGLA DE ENTREGA

Toda entrega debera ser:

- completa;
- consistente;
- lista para incorporarse al repositorio;
- coherente con la documentacion existente.

Una limitacion tecnica de la plataforma nunca justificara reducir el contenido de un entregable.

Cuando sea necesario, la entrega se dividira en partes consecutivas preservando la continuidad del documento y produciendo un unico artefacto final.

---

# REGLA DE CONSISTENCIA

Cuando se detecte una diferencia entre documentos, debera prevalecer el documento de mayor prioridad definido en esta directiva.

Cuando la diferencia afecte al nivel activo y no pueda resolverse mediante esa jerarquia, la ejecucion debera detenerse y solicitar resolucion explicita.

La ausencia de un nivel siguiente despues del ultimo nivel estructural no constituye una discrepancia: constituye el inicio de Evolucion Continua.

---

# CIERRE

Esta directiva constituye la norma operativa superior del Proyecto Condor.

Toda documentacion, conversacion, implementacion y evolucion futura debera ser coherente con ella.

En caso de conflicto entre documentos, prevalecera el documento de mayor prioridad definido en esta directiva.

---

# HISTORIAL DE CAMBIOS

| Version | Cambios |
|---------|---------|
| 2.3.0 | Se incorpora la REGLA DE TRAZABILIDAD GIT: 1 archivo afectado = 1 commit, con excepcion unica para cambios tecnicamente inseparables y actualizacion de REGISTRO_CAMBIOS.md una sola vez por ciclo de trabajo para evitar dependencia circular. |
| 2.2.0 | Se corrige el cierre del ultimo nivel estructural: Nivel 09 no activa un Nivel 10, sino Evolucion Continua. Se establece la actualizacion obligatoria de ESTADO_PROYECTO.md e INVENTARIO_PROYECTO.md durante condorcerrar. Se aclara que Entregar, Revisar y Congelar no actualizan formalmente ESTADO_PROYECTO.md. Se define el comportamiento terminal de condorcerrar y la transicion de la documentacion previa a documentacion proporcional al desarrollo. |
| 2.1.0 | Se formaliza ESTADO_PROYECTO.md como fuente oficial para determinar el nivel activo y se establece el protocolo de deteccion y tratamiento de discrepancias entre chat, estado y documentacion. Se refuerza la transicion formal de niveles mediante condorcerrar. |
| 2.0.0 | Refactorizacion integral de la Directiva Operativa. |
