# INVENTARIO_FUNCIONAL

Version: 1.0.0\
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

  FN-007   Implementar       Desarrollo        Planificada    SISTEMA_DESARROLLO_CONDOR.md
           cambios                                            

  FN-008   Verificar         Desarrollo        Planificada    SISTEMA_DESARROLLO_CONDOR.md
           resultados                                         

  FN-009   Documentar        Documentacion     Planificada    DOCUMENTADOR.md
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
  1.0.0     Creacion del Inventario Funcional del Proyecto Condor.
