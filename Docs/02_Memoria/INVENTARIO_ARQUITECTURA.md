# INVENTARIO_ARQUITECTURA

Version: 1.7.0
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

  ARQ-003   Context Engine Engine            Implementado   CONTEXT_ENGINE.md

  ARQ-004   Planner        Engine            Implementado   KERNEL_CONDOR.md

  ARQ-005   Architect      Engine            Planificado    KERNEL_CONDOR.md

  ARQ-006   Builder        Engine            Implementado   KERNEL_CONDOR.md

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

  ARQ-013   Integracion    Infraestructura   Implementado   ADN_CONDOR.md
            con Ollama                                      

  ARQ-014   Modelos LLM    Infraestructura   Especificado   ADN_CONDOR.md
            Locales                                         

  ARQ-015   Recomendador   Servicio          Implementado   DECISIONES.md
            de Modelos                                       
  -------------------------------------------------------------------------------------------

Nota: ARQ-002 fue implementado inicialmente mediante T-001. ARQ-012
cuenta con una CLI inicial (identidad, estado, analizar y consultar) pendiente
de evolucion con los motores posteriores. ARQ-013 fue implementado
mediante T-002 (OllamaClient local y comando consultar). ARQ-015 fue implementado mediante T-003 (ModelRecommender, ModelRoleClassifier, ModelMemoryBudget y comando recomendar). ARQ-002 fue extendido mediante T-004 (descubrimiento de proyecto: ProjectDetector, parsers de manifiestos, seccion PROYECTO y campo project). El contrato publico de la CLI fue corregido al espanol por DEC-025. ARQ-003 fue implementado mediante T-005 (Context Engine inicial: ContextReconstructor en Condor.Core, OperativeArtifactReader y ContextService en Condor.Infrastructure, y comando condor contexto en la CLI; verificacion integral completada con unitarias, integracion, arquitectura, CLI, E2E y determinismo D-D11). ARQ-004 fue implementado mediante T-006 (Planner inicial: WorkPlan, PlanTask y PlanLimits en Condor.Core, PlanGenerator y PlanIntent en Condor.Core.Planning, PlanService en Condor.Infrastructure, comando condor planear en la CLI; verificacion integral con unitarias, integracion, arquitectura, CLI, E2E y determinismo D-E7). ARQ-006 fue implementado mediante T-007 (Builder inicial: BuildAction, BuildActionKind, BuildResult y BuildLimits en Condor.Core, BuildDeriver en Condor.Core.Building, BuildService y ProjectFileWriter en Condor.Infrastructure, comando condor construir en la CLI; verificacion integral con unitarias, integracion, arquitectura, CLI, E2E y determinismo).

------------------------------------------------------------------------

# Relaciones principales

-   El Kernel coordina los Engines.
-   Assessment Engine descubre el entorno y el proyecto.
-   Context Engine reconstruye el contexto operativo.
-   Planner, Architect y Builder ejecutan el ciclo de ingenieria.
-   Verifier valida resultados antes de documentar.
-   Documenter genera artefactos permanentes.
-   Guardian protege la coherencia del proyecto.
-   La integracion con Ollama (ARQ-013) consume el Assessment (ARQ-002)
    para seleccionar el modelo y ejecuta la inferencia local.
-   El Recomendador de Modelos (ARQ-015) consume el Assessment (ARQ-002)
    y el inventario de Ollama (ARQ-013) para producir una recomendacion
    explicable.

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
  1.7.0     Se registra ARQ-006 (Builder) como Implementado tras la ejecucion
            de T-007 (Builder inicial): BuildDeriver en Condor.Core, BuildService
            y ProjectFileWriter en Condor.Infrastructure, comando condor construir
            en la CLI. T-007 queda formalmente congelada.
  1.6.0     Se registra que ARQ-002 fue extendido mediante T-004 (descubrimiento de proyecto), integrado en main por PR #2 (merge a903663). T-004 queda cerrada y congelada.
  1.5.0     Se confirma ARQ-015 integrado en main tras el cierre de
            T-003 (PR #1, merge 12a3c5b). T-003 queda cerrada.
  1.4.0     Se actualizan las referencias al contrato CLI tras la
            correccion DEC-025 (analizar, consultar, recomendar, ayuda).
  1.3.0     Se incorpora ARQ-015 (Recomendador de Modelos) tras la
            ejecucion de T-003, con la logica pura en Condor.Core y el
            comando condor recommend en la CLI.
  1.2.0     ARQ-013 pasa a Implementado tras la ejecucion de T-002
            (OllamaClient local y comando ask). Se actualiza la nota y
            la relacion con ARQ-002.
  1.1.0     ARQ-002 pasa a Implementado y ARQ-012 a En desarrollo tras la
            ejecucion de T-001 (bootstrap del MVP y Assessment inicial).
  1.0.0     Creacion del Inventario Arquitectonico del Proyecto Condor.
