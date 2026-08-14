# INVENTARIO_FUNCIONAL

Version: 1.7.0\
Estado: Activo\
Nivel: Global\
Clasificacion: Inventario Funcional

------------------------------------------------------------------------

# Proposito

Registrar y clasificar las capacidades funcionales del Proyecto Condor.

Este documento constituye el inventario oficial de las funcionalidades
del sistema durante toda su evolucion.

Responde la pregunta:

> **¿Que hace, que hara y que aun no hace Condor?**

------------------------------------------------------------------------

# Alcance

Incluye funcionalidades implementadas, especificadas, en desarrollo y
planificadas para la version 1.x.

No describe la implementacion tecnica; solo el alcance funcional.

------------------------------------------------------------------------

# Flujo

Identificar

↓

Clasificar

↓

Registrar

↓

Versionar

↓

Actualizar

------------------------------------------------------------------------

# Estados

-   Planificada
-   Especificada
-   En desarrollo
-   Implementada
-   Validada
-   Congelada

------------------------------------------------------------------------

# Clasificacion

-   Motor
-   Servicio
-   Desarrollo
-   Experiencia
-   Documentacion
-   Gobernanza
-   Infraestructura

------------------------------------------------------------------------

# Inventario funcional

  ----------------------------------------------------------------------------------------------------
  ID       Capacidad         Categoria         Estado         Documento responsable
  -------- ----------------- ----------------- -------------- ----------------------------------------
  FN-001   Inventariar       Gobernanza        Especificada   INVENTARIO_PROYECTO.md
           proyectos                                          

  FN-002   Preservar         Gobernanza        Especificada   PATRIMONIO_CONOCIMIENTO.md
           conocimiento                                       

  FN-003   Descubrir         Motor             Implementada   CONTEXT_ENGINE.md
           automaticamente                                    
           el contexto                                        

  FN-004   Evaluar proyecto  Motor             Planificada    ASSESSMENT_ENGINE.md
           y entorno                                          

  FN-005   Planificar tareas Desarrollo        Implementada   KERNEL_CONDOR.md
           de ingenieria                                      

  FN-006   Disenar           Desarrollo        Planificada    SISTEMA_DESARROLLO_CONDOR.md
           soluciones                                         

  FN-007   Implementar       Desarrollo        Implementada    SISTEMA_DESARROLLO_CONDOR.md
            cambios                                            

  FN-008   Verificar         Desarrollo        Implementada    SISTEMA_DESARROLLO_CONDOR.md
            resultados                                         

  FN-009   Documentar        Documentacion     Especificada   DOCUMENTADOR.md
            automaticamente                                    

  FN-010   Mantener          Gobernanza        Especificada   DIRECTIVA_GLOBAL.md
           coherencia                                         
           arquitectonica                                     

  FN-011   Guiar al          Experiencia       Especificada   ADN_CONDOR.md
           desarrollador                                      
           como un ingeniero                                  

  FN-012   Adaptarse al      Infraestructura   Especificada   ADN_CONDOR.md
           hardware                                           
           disponible                                         

  FN-013   Trabajar con      Infraestructura   Especificada   ADN_CONDOR.md
           modelos locales                                    

  FN-014   Operar            Infraestructura   Especificada   ADN_CONDOR.md
           inicialmente                                       
           sobre Windows                                      

  FN-015   Ejecutar el ciclo Desarrollo        Especificada   DIRECTIVA_OPERATIVA_PROYECTO_CONDOR.md
            Comprender →                                       
            Planificar →                                       
            Disenar →                                          
            Implementar →                                      
            Verificar →                                        
            Documentar →                                       
            Congelar →                                         
            Continuar                                          

  FN-016   Ejecutar el       Desarrollo        Implementada   KERNEL_CONDOR.md
            ciclo de                                          
            ingenieria                                        
            parcial                                        
            (condor avanzar)                               

  FN-017   Analizar una      Infraestructura   Implementada   DECISIONES.md
            imagen con un                                     
            VLM local                                        
            (condor examinar)                              

  FN-018   Verificar la      Infraestructura   Implementada   DECISIONES.md
            puesta en                                         
            marcha                                          
            (condor preparar)                              

  FN-019   Compilar y        Motor             Implementada   DECISIONES.md
            probar el                                         
            proyecto                                          
            objetivo                                       
            (condor verificar-semantico)                   
  ----------------------------------------------------------------------------------------------------

------------------------------------------------------------------------

# Reglas

1.  Toda capacidad funcional debera tener un identificador permanente.
2.  Toda funcionalidad debera estar asociada a un documento responsable.
3.  Ninguna funcionalidad podra considerarse implementada sin
    validacion.
4.  Las capacidades futuras permaneceran registradas aun cuando no
    formen parte de la version actual.

------------------------------------------------------------------------

# Historial de cambios

  Version   Cambios
  --------- --------------------------------------------------------
  1.7.0     Se registra FN-019 (Compilar y probar el proyecto objetivo, condor
            verificar-semantico) como Implementada tras T-013, primera concrecion
            de la verificacion semantica y de calidad (SD-02). T-013 queda
            formalmente congelada.
  1.6.0     Se registra FN-018 (Verificar la puesta en marcha, condor preparar)
            como Implementada tras T-012, con diagnostico no destructivo y
            separacion de dependencias obligatorias/opcionales. T-012 queda
            formalmente congelada.
  1.5.0     Se registra FN-017 (Analizar una imagen con un VLM local, condor
            examinar) como Implementada tras T-011, condicionada a VisionCapable
            y a un modelo de vision disponible, con degradacion controlada.
            T-011 queda formalmente congelada.
  1.4.0     Se registra FN-016 (Ejecutar el ciclo de ingenieria parcial,
            condor avanzar) como Implementada tras T-010, que orquesta Planner,
            Builder y Verifier sin modificar los motores congelados. T-010 queda
            formalmente congelada.
  1.3.0     Se registra FN-009 (Documentar automaticamente) como Especificada tras
            T-009 con la creacion de DOCUMENTADOR.md que define el rol de
            Documenter. T-009 queda formalmente congelada.
  1.2.0     Se registra FN-008 (Verificar resultados) como Implementada tras
            la ejecucion de T-008 (Verifier inicial): comando condor verificar
            que comprueba la integridad y acotacion de los cambios aplicados por
            T-007. T-008 queda formalmente congelada.
  1.1.0     Se registra FN-007 (Implementar cambios) como Implementada tras
            la ejecucion de T-007 (Builder inicial): comando condor construir
            que consume el WorkPlan y aplica cambios acotados sobre el proyecto
            objetivo. T-007 queda formalmente congelada.
  1.0.0     Creacion del Inventario Funcional del Proyecto Condor.
