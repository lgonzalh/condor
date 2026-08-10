# INVENTARIO_ARQUITECTURA

Version: 1.1.0
Estado: Activo
Nivel: Global
Clasificacion: Inventario Arquitectonico

------------------------------------------------------------------------

# Proposito

Registrar la arquitectura oficial del Proyecto Condor, identificando los
componentes, motores, servicios y documentos que conforman el sistema.

Este documento responde la pregunta:

> **¿Como esta estructurado Condor?**

No reemplaza la documentacion tecnica de cada componente; funciona como
el catalogo maestro de la arquitectura.

------------------------------------------------------------------------

# Alcance

Incluye todos los componentes definidos para la version 1.x del
proyecto, independientemente de su estado de implementacion.

------------------------------------------------------------------------

# Flujo

Identificar

↓

Clasificar

↓

Relacionar

↓

Versionar

↓

Actualizar

------------------------------------------------------------------------

# Estados

-   Planificado
-   Especificado
-   En desarrollo
-   Implementado
-   Validado
-   Congelado

------------------------------------------------------------------------

# Categorias

-   Kernel
-   Engine
-   Servicio
-   Infraestructura
-   Gobernanza
-   Interfaz
-   Documentacion

------------------------------------------------------------------------

# Inventario arquitectonico

  -------------------------------------------------------------------------------------------
  ID        Componente     Categoria         Estado         Documento responsable
  --------- -------------- ----------------- -------------- ---------------------------------
  ARQ-001   Kernel Condor  Kernel            Planificado    KERNEL_CONDOR.md

  ARQ-002   Assessment     Engine            Implementado   ASSESSMENT_ENGINE.md
            Engine                                          

  ARQ-003   Context Engine Engine            Planificado    CONTEXT_ENGINE.md

  ARQ-004   Planner        Engine            Planificado    KERNEL_CONDOR.md

  ARQ-005   Architect      Engine            Planificado    KERNEL_CONDOR.md

  ARQ-006   Builder        Engine            Planificado    KERNEL_CONDOR.md

  ARQ-007   Verifier       Engine            Planificado    KERNEL_CONDOR.md

  ARQ-008   Documenter     Servicio          Planificado    DOCUMENTADOR.md

  ARQ-009   Guardian       Servicio          Planificado    GUARDIAN.md

  ARQ-010   Sistema Global Gobernanza        Especificado   Inventarios Globales
            de Inventarios                                  

  ARQ-011   Modelo de      Gobernanza        Planificado    MODELO_CICLO_VIDA_ARTEFACTOS.md
            Ciclo de Vida                                   
            de Artefactos                                   

  ARQ-012   Interfaz CLI   Interfaz          En desarrollo  INTERFAZ.md
            Windows                                         

  ARQ-013   Integracion    Infraestructura   Especificado   ADN_CONDOR.md
            con Ollama                                      

  ARQ-014   Modelos LLM    Infraestructura   Especificado   ADN_CONDOR.md
            Locales                                         
  -------------------------------------------------------------------------------------------

Nota: ARQ-002 fue implementado inicialmente mediante T-001. ARQ-012
cuenta con una CLI inicial (identidad, estado y comando assess)
pendiente de evolucion con los motores posteriores.

------------------------------------------------------------------------

# Relaciones principales

-   El Kernel coordina los Engines.
-   Assessment Engine descubre el entorno y el proyecto.
-   Context Engine reconstruye el contexto operativo.
-   Planner, Architect y Builder ejecutan el ciclo de ingenieria.
-   Verifier valida resultados antes de documentar.
-   Documenter genera artefactos permanentes.
-   Guardian protege la coherencia del proyecto.

------------------------------------------------------------------------

# Reglas

1.  Todo componente arquitectonico debera estar registrado en este
    inventario.
2.  Cada componente debera tener un documento responsable.
3.  Ningun componente implementado podra carecer de especificacion.
4.  Toda modificacion arquitectonica debera reflejarse en este
    documento.

------------------------------------------------------------------------

# Historial de cambios

  Version   Cambios
  --------- -------------------------------------------------------------
  1.1.0     ARQ-002 pasa a Implementado y ARQ-012 a En desarrollo tras la
            ejecucion de T-001 (bootstrap del MVP y Assessment inicial).
  1.0.0     Creacion del Inventario Arquitectonico del Proyecto Condor.
