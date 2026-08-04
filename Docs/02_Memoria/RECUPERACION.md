# RECUPERACION

Version: 1.0.0 Estado: Activo Nivel: 02 - Memoria Clasificacion:
Arquitectura

------------------------------------------------------------------------

# Proposito

Definir como Condor localiza, selecciona y recupera el conocimiento
necesario para ejecutar una tarea con el menor contexto posible y la
mayor precision.

------------------------------------------------------------------------

# Principios

-   Recuperar solo el conocimiento relevante.
-   Priorizar las fuentes oficiales.
-   Evitar cargar informacion innecesaria.
-   Mantener la trazabilidad hacia la fuente original.

------------------------------------------------------------------------

# Flujo de recuperacion

1.  Identificar el objetivo de la tarea.
2.  Determinar el nivel activo.
3.  Localizar las fuentes oficiales aplicables.
4.  Recuperar unicamente la informacion necesaria.
5.  Verificar consistencia entre las fuentes.
6.  Construir el contexto operativo.

------------------------------------------------------------------------

# Estrategia

La recuperacion debe realizarse en capas:

1.  Estado del proyecto.
2.  Documentacion global.
3.  Documentacion del nivel activo.
4.  Conversacion actual.

Cada capa solo se consulta cuando aporta informacion relevante.

------------------------------------------------------------------------

# Reglas

-   Nunca asumir conocimiento no documentado.
-   Nunca recuperar documentos sin relacion con la tarea.
-   Ante conflicto entre fuentes, prevalece la de mayor prioridad.
-   Toda recuperacion debe poder reproducirse siguiendo la misma
    secuencia.

------------------------------------------------------------------------

# Resultado esperado

Al finalizar la recuperacion, Condor debe disponer del contexto minimo
suficiente para ejecutar la tarea sin perder coherencia arquitectonica.

------------------------------------------------------------------------

# Historial

  Version   Cambio
  --------- --------------------
  1.0.0     Documento inicial.
