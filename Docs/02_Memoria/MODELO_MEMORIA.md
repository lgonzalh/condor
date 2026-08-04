# MODELO_MEMORIA

Version: 1.0.0 Estado: Activo Nivel: 02 - Memoria Clasificacion:
Arquitectura

------------------------------------------------------------------------

# Proposito

Definir el modelo conceptual de memoria utilizado por Condor para
administrar el conocimiento del proyecto.

------------------------------------------------------------------------

# Tipos de memoria

## Memoria temporal

Existe solo durante la ejecucion de una conversacion o tarea.

Se descarta al finalizar.

------------------------------------------------------------------------

## Memoria de nivel

Contiene el conocimiento propio de un nivel del proyecto.

Su fuente oficial son los documentos del nivel activo.

------------------------------------------------------------------------

## Memoria de proyecto

Representa el conocimiento compartido por todos los niveles.

Se alimenta de:

-   CONDOR_CONTEXTO_MAESTRO.md
-   ADN_CONDOR.md
-   DIRECTIVA_GLOBAL.md
-   ESTADO_PROYECTO.md

------------------------------------------------------------------------

## Memoria permanente

Corresponde a decisiones oficialmente documentadas.

Solo puede modificarse mediante los procesos definidos por Condor.

------------------------------------------------------------------------

# Jerarquia

1.  Memoria permanente
2.  Memoria de proyecto
3.  Memoria de nivel
4.  Memoria temporal

La memoria superior prevalece sobre la inferior.

------------------------------------------------------------------------

# Flujo

Fuentes oficiales

↓

Carga de contexto

↓

Ejecucion

↓

Generacion de conocimiento

↓

Persistencia documental

↓

Actualizacion del estado

------------------------------------------------------------------------

# Historial

  Version   Cambio
  --------- --------------------
  1.0.0     Documento inicial.
