# INVENTARIO_PROYECTO

Version: 1.0.0\
Estado: Activo\
Nivel: Global\
Clasificacion: Inventario Maestro

------------------------------------------------------------------------

# Proposito

Constituir el indice maestro del Proyecto Condor, registrando los
artefactos permanentes del proyecto, su estado, dependencias y ubicacion
dentro de la arquitectura documental.

Este documento responde la pregunta:

> **¿Que existe actualmente en el Proyecto Condor?**

No describe el contenido de los documentos; unicamente mantiene su
inventario y trazabilidad.

------------------------------------------------------------------------

# Alcance

Aplica a todos los documentos permanentes del proyecto,
independientemente del nivel al que pertenezcan.

No incluye conversaciones, borradores temporales ni notas de trabajo.

------------------------------------------------------------------------

# Flujo de actualizacion

Inventariar

↓

Verificar

↓

Actualizar

↓

Versionar

↓

Congelar

------------------------------------------------------------------------

# Estructura del inventario

Cada registro debera contener:

-   Identificador (opcional si el nombre del documento es unico).
-   Nombre del documento.
-   Nivel.
-   Version.
-   Estado.
-   Clasificacion.
-   Dependencias.
-   Responsable.
-   Ultima revision.
-   Observaciones.

------------------------------------------------------------------------

# Estados Kanban

-   Pendiente
-   En progreso
-   Listo

------------------------------------------------------------------------

# Inventario actual

  ------------------------------------------------------------------------------------------------------------
  Documento                                Nivel    Version       Estado      Clasificacion    Dependencias
  ---------------------------------------- -------- ------------- ----------- ---------------- ---------------
  CONDOR_CONTEXTO_MAESTRO.md               Global   2.1.0         Listo       Constitucion     Ninguna

  ADN_CONDOR.md                            Global   Vigente       Listo       ADN              Contexto
                                                                                               Maestro

  DIRECTIVA_GLOBAL.md                      Global   Vigente       Listo       Directiva        ADN

  DIRECTIVA_OPERATIVA_PROYECTO_CONDOR.md   Global   Vigente       Listo       Directiva        Directiva
                                                                                               Global

  ESTADO_PROYECTO.md                       Global   Vigente       En progreso Operacion        Documentos
                                                                                               globales

  REGISTRO_DEUDA_ARQUITECTONICA.md         Global   1.0.0         En progreso Registro         Todos

  INVENTARIO_PROYECTO.md                   Global   1.0.0         En progreso Inventario       Ninguna

  PATRIMONIO_CONOCIMIENTO.md               Global   Planificado   Pendiente   Inventario       Cuadernos I-V

  INVENTARIO_FUNCIONAL.md                  Global   Planificado   Pendiente   Inventario       Arquitectura

  INVENTARIO_ARQUITECTURA.md               Global   Planificado   Pendiente   Inventario       Arquitectura

  MODELO_CICLO_VIDA_ARTEFACTOS.md          Global   Planificado   Pendiente   Modelo           Directivas
  ------------------------------------------------------------------------------------------------------------

------------------------------------------------------------------------

# Reglas

1.  Ningun documento permanente podra existir sin estar registrado aqui.
2.  Todo documento congelado debera actualizar su version en este
    inventario.
3.  Toda eliminacion debera conservar trazabilidad historica.
4.  Este documento es la fuente oficial del inventario documental.

------------------------------------------------------------------------

# Historial de cambios

  Version   Cambios
  --------- ------------------------------------------------------
  1.0.0     Creacion del Inventario Maestro del Proyecto Condor.
