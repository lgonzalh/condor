# DIRECTIVA_OPERATIVA_PROYECTO_CONDOR

Version: 2.1.0
Estado: En desarrollo
Nivel: Global
Clasificacion: Directiva Operativa

---

# PROPOSITO

Este documento define las reglas operativas permanentes del Proyecto Condor.

Estas reglas son obligatorias durante toda la operacion del proyecto y tienen prioridad sobre las reglas locales de cualquier chat.

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

# DEPENDENCIAS ARQUITECTONICAS

Las dependencias arquitectonicas prevalecen sobre el orden cronologico.

Cuando exista una dependencia arquitectonica critica:

- podra alterarse temporalmente el orden registrado en ESTADO_PROYECTO.md;
- debera resolverse primero la dependencia;
- posteriormente debera actualizarse ESTADO_PROYECTO.md para reflejar el estado oficial.

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
2. actualizar internamente ESTADO_PROYECTO.md;
3. entregar el documento solicitado en formato Markdown listo para repositorio.

No mostrara el documento completo en el chat salvo solicitud explicita.

Durante CondorEntregar queda prohibido:

- filosofar;
- justificar decisiones no solicitadas;
- proponer mejoras fuera del alcance;
- crear documentos adicionales;
- detener la entrega para solicitar confirmaciones innecesarias.

ESTADO_PROYECTO.md solo se entregara cuando:

- el usuario lo solicite explicitamente;
- se complete un conjunto de entregables;
- se cierre oficialmente un nivel.

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

---

# CONDORCONGELAR

Marca un entregable como estable.

Despues del congelamiento solo podra modificarse por:

- solicitud explicita;
- error critico;
- dependencia arquitectonica.

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

---

# CONDORCONDENSAR

Reduce un entregable a su minima expresion sin perder:

- significado;
- decisiones;
- restricciones;
- comportamiento funcional.

---

# CONDORCERRAR

Finaliza oficialmente un nivel.

Debe:

1. verificar que todos los entregables planificados existan;
2. verificar que el nivel haya sido revisado;
3. verificar que el nivel haya sido congelado;
4. actualizar ESTADO_PROYECTO.md;
5. marcar el nivel como Completado;
6. establecer el siguiente nivel como Activo;
7. registrar el plan documental del siguiente nivel;
8. registrar el primer entregable recomendado;
9. entregar unicamente ESTADO_PROYECTO.md.

La transicion de nivel no se considera oficial hasta que ESTADO_PROYECTO.md refleje el nuevo nivel activo.

El titulo del chat de origen no determina la transicion.

---

# REGLA DE PERSISTENCIA

Toda decision relevante debera convertirse en un documento permanente antes de continuar.

La conversacion nunca sera considerada memoria permanente.

---

# REGLA DE CONSISTENCIA

Cuando se detecte una diferencia entre documentos, debera prevalecer el documento de mayor prioridad definido en esta directiva.

Cuando la diferencia afecte al nivel activo y no pueda resolverse mediante esa jerarquia, la ejecucion debera detenerse y solicitar resolucion explicita.

---

# OBJETIVO PERMANENTE

Cada respuesta debera dejar el Proyecto Condor objetivamente mas avanzado que antes.

Toda accion debera:

- preservar la coherencia arquitectonica;
- preservar el conocimiento;
- minimizar trabajo manual;
- reducir friccion;
- producir un artefacto permanente cuando corresponda.

---

# HISTORIAL DE CAMBIOS

| Version | Cambios |
|---------|---------|
| 2.1.0 | Se formaliza ESTADO_PROYECTO.md como fuente oficial para determinar el nivel activo y se establece el protocolo de deteccion y tratamiento de discrepancias entre chat, estado y documentacion. Se refuerza la transicion formal de niveles mediante condorcerrar. |
| 2.0.0 | Refactorizacion integral de la Directiva Operativa. |
