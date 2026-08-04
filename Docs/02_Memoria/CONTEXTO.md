# CONTEXTO

Version: 1.0.0 Estado: Activo Nivel: 02 - Memoria Clasificacion:
Arquitectura

------------------------------------------------------------------------

# Proposito

Definir como Condor construye, carga, mantiene y reduce el contexto
durante la ejecucion de una tarea.

------------------------------------------------------------------------

# Principios

-   El contexto se construye a partir de fuentes oficiales.
-   El contexto minimo suficiente tiene prioridad sobre el contexto
    extenso.
-   La conversacion complementa el contexto, pero no lo sustituye.
-   El contexto debe ser reproducible desde la documentacion.

------------------------------------------------------------------------

# Orden de carga

1.  CONDOR_CONTEXTO_MAESTRO.md
2.  ADN_CONDOR.md
3.  DIRECTIVA_GLOBAL.md
4.  ESTADO_PROYECTO.md
5.  Documentacion del nivel activo.
6.  Conversacion actual.

------------------------------------------------------------------------

# Construccion

Para cada tarea Condor debe:

1.  Identificar el nivel activo.
2.  Localizar las fuentes oficiales.
3.  Cargar solo el conocimiento necesario.
4.  Ejecutar la tarea.
5.  Persistir las decisiones relevantes en la documentacion.

------------------------------------------------------------------------

# Reduccion

Cuando el contexto exceda el necesario:

-   eliminar redundancias;
-   conservar decisiones;
-   mantener dependencias;
-   preservar coherencia.

------------------------------------------------------------------------

# Restricciones

-   Nunca utilizar la conversacion como fuente principal de verdad.
-   Nunca inventar decisiones no documentadas.
-   Nunca mezclar conocimiento entre niveles salvo dependencia
    arquitectonica.

------------------------------------------------------------------------

# Historial

  Version   Cambio
  --------- --------------------
  1.0.0     Documento inicial.
