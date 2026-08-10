# MODELO_CICLO_VIDA_ARTEFACTOS

Version: 1.0.0 Estado: Activo Nivel: Global Clasificacion: Modelo

------------------------------------------------------------------------

# Proposito

Definir el ciclo de vida oficial de todos los artefactos permanentes del
Proyecto Condor.

Responde la pregunta:

> ¿Como nace, evoluciona y se consolida un artefacto en Condor?

------------------------------------------------------------------------

# Alcance

Aplica a documentos, arquitectura, codigo, configuraciones, inventarios
y cualquier activo permanente.

------------------------------------------------------------------------

# Principios

-   Ningun artefacto nace congelado.
-   Todo artefacto debe ser verificable.
-   Todo cambio debe mantener trazabilidad.
-   La documentacion forma parte del artefacto.
-   La verificacion puede regresar el flujo a etapas anteriores.

------------------------------------------------------------------------

# Flujo de ingenieria

Identificar origen

↓

Inventariar

↓

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

¿Cumple?

-   No: regresar a Comprender.
-   Si: continuar.

↓

Documentar

↓

Aprobar

↓

Congelar

↓

Continuar

------------------------------------------------------------------------

# Estados

  Estado          Descripcion
  --------------- -----------------------------------
  Planificado     Identificado pero aun no existe.
  Especificado    Definicion formal creada.
  En desarrollo   En construccion o modificacion.
  Implementado    Desarrollo completado.
  Verificado      Cumple las validaciones.
  Aprobado        Aceptado oficialmente.
  Documentado     Registrado como fuente de verdad.
  Congelado       Linea base vigente.

------------------------------------------------------------------------

# Checkpoint

Todo checkpoint debera registrar:

-   Version.
-   Fecha.
-   Artefactos afectados.
-   Estado del proyecto.
-   Observaciones.

------------------------------------------------------------------------

# Reglas

1.  Ningun artefacto podra omitir etapas del ciclo.
2.  Todo artefacto congelado debera estar documentado.
3.  Los inventarios deberan reflejar el estado real.
4.  Una modificacion reinicia el ciclo desde la etapa correspondiente.

------------------------------------------------------------------------

# Documentos relacionados

-   CONDOR_CONTEXTO_MAESTRO.md
-   ADN_CONDOR.md
-   DIRECTIVA_GLOBAL.md
-   DIRECTIVA_OPERATIVA_PROYECTO_CONDOR.md
-   INVENTARIO_PROYECTO.md
-   PATRIMONIO_CONOCIMIENTO.md

------------------------------------------------------------------------

# Historial de cambios

  Version   Cambios
  --------- --------------------------------------------------------------
  1.0.0     Creacion del modelo oficial del ciclo de vida de artefactos.
