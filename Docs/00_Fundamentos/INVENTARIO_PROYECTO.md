# INVENTARIO_PROYECTO

Version: 1.4.0
Estado: Activo
Nivel: Global
Clasificacion: Inventario Maestro
Fecha de actualizacion: 2026-10-30

---

# PROPOSITO

Constituir el indice maestro de documentos permanentes del Proyecto Condor, con ubicacion exacta y metadatos declarados en cada archivo. El indice de continuidad de la raiz y el inventario tecnico conservan sus funciones propias.

Este documento responde la pregunta:

> ¿Que documentos existen actualmente en el Proyecto Condor y donde se encuentran?

---

# ALCANCE

Incluye los archivos Markdown presentes bajo `Docs/`, `operacion/` y la raiz del repositorio. Los cuadernos historicos de `assets/` se registran en `PATRIMONIO_CONOCIMIENTO.md`; los artefactos no Markdown se enumeran por separado abajo.

El inventario describe el arbol local observado el 2026-10-30. La presencia local no implica que un archivo este versionado en Git.

---

# REGLA DE INVENTARIO

Todo documento Markdown permanente debe figurar con su ruta relativa actual. Las rutas distinguen archivos con nombres repetidos. Version, estado, nivel y clasificacion se copian de los metadatos del archivo; si el dato no esta declarado, se indica. Las dependencias se conservan solo cuando ya estaban declaradas en este inventario; no se deducen por cercania, enlaces ni nombre.

Los documentos planificados se identifican como tales en su propio estado. Esta tabla enumera archivos encontrados en el arbol y no convierte un estado previamente declarado.

---

# INVENTARIO DOCUMENTAL

Total observado: 160 archivos Markdown (133 en `Docs/`, 23 en `operacion/` y 4 en la raiz).

| Ruta | Nivel | Version | Estado | Clasificacion | Dependencias declaradas |
|---|---|---|---|---|---|
| `AGENTE_CONDOR.md` | No declarado | 4.0.0 | Vigente | No declarada | No declaradas |
| `Docs/00_Fundamentos/00_FUNDAMENTOS.md` | 00 - Fundamentos | 1.0.0 | Activo | Documento del Nivel | No declaradas |
| `Docs/00_Fundamentos/ACTA_CONGELAMIENTO_NIVEL_00.md` | 00 - Fundamentos | 1.0.0 | Congelado | No declarada | No declaradas |
| `Docs/00_Fundamentos/ACTUALIZACION_TRANSVERSAL_NIVEL_00.md` | 00 - Fundamentos | 1.0.0 | Activo | Plan de Ejecucion | No declaradas |
| `Docs/00_Fundamentos/ADN_CONDOR.md` | Global | 2.0.0 | Activo | Protocolo de Ingenieria | Contexto Maestro |
| `Docs/00_Fundamentos/ARQUITECTURA_COMPONENTES.md` | 01 - Arquitectura | 1.0.0 | Activo | Arquitectura | No declaradas |
| `Docs/00_Fundamentos/ARQUITECTURA_NUCLEO.md` | 01 - Arquitectura | 1.0.0 | Activo | Arquitectura | No declaradas |
| `Docs/00_Fundamentos/CONDOR_CONTEXTO_MAESTRO.md` | Global | 2.1.0 | Activo | Constitucion | Ninguna |
| `Docs/00_Fundamentos/CONTRATOS_COMPONENTES.md` | 01 - Arquitectura | 1.0.0 | Activo | Arquitectura | No declaradas |
| `Docs/00_Fundamentos/DIRECTIVA_GLOBAL.md` | Global | 1.0.0 | Congelado | Directiva Operativa | ADN |
| `Docs/00_Fundamentos/DIRECTIVA_OPERATIVA_PROYECTO_CONDOR.md` | Global | 2.3.0 | Vigente | Directiva Operativa | Directiva Global |
| `Docs/00_Fundamentos/ESTANDAR_DOCUMENTAL.md` | Global | 1.1.0 | Activo | Estandar | No declaradas |
| `Docs/00_Fundamentos/ESTRUCTURA_REPOSITORIO.md` | Global | 2.0.3 | Activo | Arquitectura | No declaradas |
| `Docs/00_Fundamentos/FLUJO_INTERNO_NUCLEO.md` | 01 - Arquitectura | 1.0.0 | Activo | Arquitectura | No declaradas |
| `Docs/00_Fundamentos/GRAFO_CONOCIMIENTO.md` | 01 - Arquitectura | 1.0.0 | Activo | Arquitectura | No declaradas |
| `Docs/00_Fundamentos/INVENTARIO_PROYECTO.md` | Global | 1.4.0 | Activo | Inventario Maestro | Ninguna |
| `Docs/00_Fundamentos/KANBAN.md` | Global | 1.0.0 | Activo | Gestion del Proyecto | No declaradas |
| `Docs/00_Fundamentos/MODELO_CICLO_VIDA_ARTEFACTOS.md` | Global | 1.0.0 | Activo | Modelo | Directivas |
| `Docs/00_Fundamentos/MODELO_CONOCIMIENTO.md` | 01 - Arquitectura | 1.0.0 | Activo | Arquitectura | No declaradas |
| `Docs/00_Fundamentos/MODELO_CONTEXTO.md` | 01 - Arquitectura | 1.0.0 | Activo | Arquitectura | No declaradas |
| `Docs/00_Fundamentos/MODELO_DECISIONES.md` | 01 - Arquitectura | 1.0.0 | Activo | Arquitectura | No declaradas |
| `Docs/00_Fundamentos/MODELO_PROYECTO.md` | 01 - Arquitectura | 1.0.0 | Activo | Arquitectura | No declaradas |
| `Docs/00_Fundamentos/PATRIMONIO_CONOCIMIENTO.md` | Global | 1.3.0 | Activo | Inventario de Conocimiento | Cuadernos I-V |
| `Docs/00_Fundamentos/PLAN_CONSOLIDACION_NIVEL_00.md` | 00 - Fundamentos | 1.0.0 | Activo | Plan de Consolidacion | No declaradas |
| `Docs/00_Fundamentos/PROTOCOLO_CONTINUA.md` | Global | 1.0.0 | Activo | Protocolo de Ingenieria | No declaradas |
| `Docs/00_Fundamentos/PROTOCOLO_DESCUBRIMIENTO.md` | Global | 1.0.0 | Activo | Protocolo de Ingenieria | No declaradas |
| `Docs/00_Fundamentos/PROTOCOLO_DOCUMENTACION.md` | 01 - Arquitectura | 1.0.0 | Activo | Protocolo de Ingenieria | No declaradas |
| `Docs/00_Fundamentos/PROTOCOLO_IMPLEMENTACION.md` | 01 - Arquitectura | 1.0.0 | Activo | Protocolo de Ingenieria | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/ACTA_CONGELAMIENTO_NIVEL_01.md` | 01 - Arquitectura Ejecutable | 1.0.0 | Congelado | Acta | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/ARCHITECT.md` | 01 - Arquitectura Ejecutable | 1.0.0 | Activo | Componente | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/ASSESSMENT_ENGINE.md` | 03 | 1.0.0 | Activo | Engine | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/BOOTSTRAP_DEPENDENCIAS.md` | Ejecución (puesta en marcha) | 1.0.0 | Vigente | No declarada | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/CICLO_EJECUCION.md` | 01 - Arquitectura Ejecutable | 1.0.0 | Activo | Ciclo | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/CONTEXT_ENGINE.md` | 03 | 1.0.0 | Activo | Engine | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/CONTRATO_KERNEL.md` | 01 - Arquitectura Ejecutable | 1.0.0 | Activo | Contrato | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/CONTRATOS_INTERCOMPONENTES.md` | 01 - Arquitectura Ejecutable | 1.0.0 | Activo | Contratos | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/DOCUMENTADOR.md` | 03 | 1.0.0 | Especificado | Servicio | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/DOCUMENTER.md` | 01 - Arquitectura Ejecutable | 1.0.0 | Activo | Componente | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/ESPECIFICACION_KERNEL.md` | 01 - Arquitectura Ejecutable | 1.0.0 | Activo | Especificacion | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/ESTADO_KERNEL.md` | 01 - Arquitectura Ejecutable | 1.0.0 | Activo | Estado | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/EVENTOS_KERNEL.md` | 01 - Arquitectura Ejecutable | 1.0.0 | Activo | Eventos | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/FLUJO_KERNEL.md` | 01 - Arquitectura Ejecutable | 1.0.0 | Activo | Flujo | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/IMPLEMENTER.md` | 01 - Arquitectura Ejecutable | 1.0.0 | Activo | Componente | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/INTEGRACION_KERNEL.md` | 01 - Arquitectura Ejecutable | 1.0.0 | Activo | Integracion | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/INTERFACES_COMPONENTES.md` | 01 - Arquitectura Ejecutable | 1.0.0 | Activo | Interfaces | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/KERNEL_CONDOR.md` | 03 | 1.0.0 | Activo | Kernel | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/KNOWLEDGE_MANAGER.md` | 01 - Arquitectura Ejecutable | 1.0.0 | Activo | Componente | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/MANEJO_ERRORES_KERNEL.md` | 01 - Arquitectura Ejecutable | 1.0.0 | Activo | Manejo de Errores | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/MODELO_EJECUCION.md` | 01 - Arquitectura Ejecutable | 1.0.0 | Activo | Modelo | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/MOTOR_EJECUCION.md` | 01 - Arquitectura Ejecutable | 1.0.0 | Activo | Motor | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/ORQUESTADOR_KERNEL.md` | 01 - Arquitectura Ejecutable | 1.0.0 | Activo | Orquestacion | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/PIPELINE_KERNEL.md` | 01 - Arquitectura Ejecutable | 1.0.0 | Activo | Pipeline | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/PLANNER.md` | 01 - Arquitectura Ejecutable | 1.0.0 | Activo | Componente | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/PRIMER_CASO_DE_USO.md` | 01 - Arquitectura Ejecutable | 1.0.0 | Activo | Caso de Uso | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/REGISTRO_DEUDA_ARQUITECTONICA.md` | Global | 1.0.0 | Activo | Registro Arquitectonico | Todos |
| `Docs/01_Arquitectura_Ejecutable/REVIEWER.md` | 01 - Arquitectura Ejecutable | 1.0.0 | Activo | Componente | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/SISTEMA_DESARROLLO_CONDOR.md` | 03 | 1.0.0 | Activo | Sistema | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/TRANSICIONES_KERNEL.md` | 01 - Arquitectura Ejecutable | 1.0.0 | Activo | Transiciones | No declaradas |
| `Docs/01_Arquitectura_Ejecutable/VALIDATOR.md` | 01 - Arquitectura Ejecutable | 1.0.0 | Activo | Componente | No declaradas |
| `Docs/02_Memoria/CONTEXTO.md` | 02 - Memoria | 1.0.0 | Activo | No declarada Arquitectura | No declaradas |
| `Docs/02_Memoria/ESTRATEGIA_MEMORIA.md` | 02 - Memoria | 1.0.0 | Activo | No declarada Arquitectura | No declaradas |
| `Docs/02_Memoria/FUENTES.md` | 02 - Memoria | 1.0.0 | Activo | No declarada Arquitectura | No declaradas |
| `Docs/02_Memoria/INVENTARIO_ARQUITECTURA.md` | Global | 2.5.0 | Activo | Inventario Arquitectonico | Arquitectura |
| `Docs/02_Memoria/INVENTARIO_FUNCIONAL.md` | Global | 1.8.0 | Activo | Inventario Funcional | Arquitectura |
| `Docs/02_Memoria/MEMORIA.md` | 02 - Memoria | 1.0.0 | Activo | No declarada Arquitectura | No declaradas |
| `Docs/02_Memoria/MODELO_MEMORIA.md` | 02 - Memoria | 1.0.0 | Activo | No declarada Arquitectura | No declaradas |
| `Docs/02_Memoria/PERSISTENCIA.md` | 02 - Memoria | 1.0.0 | Activo | No declarada Arquitectura | No declaradas |
| `Docs/02_Memoria/RECUPERACION.md` | 02 - Memoria | 1.0.0 | Activo | No declarada Arquitectura | No declaradas |
| `Docs/02_Memoria/SINCRONIZACION.md` | 02 - Memoria | 1.0.0 | Activo | No declarada Arquitectura | No declaradas |
| `Docs/03_Planificacion/CASOS_USO.md` | 03 - Planificacion | 1.0.0 | Activo | Casos de uso del Nivel | No declaradas |
| `Docs/03_Planificacion/ESTRATEGIA_EJECUCION.md` | 03 - Planificacion | 1.0.0 | Activo | Estrategia de ejecucion | No declaradas |
| `Docs/03_Planificacion/FLUJOS.md` | 03 - Planificacion | 1.0.0 | Activo | Flujos del Nivel | No declaradas |
| `Docs/03_Planificacion/OBJETIVOS.md` | 03 - Planificacion | 1.0.0 | Activo | Objetivos del Nivel | No declaradas |
| `Docs/03_Planificacion/PLANIFICACION.md` | 03 - Planificacion | 1.0.0 | Activo | Plan del Nivel | No declaradas |
| `Docs/03_Planificacion/PRIORIDADES.md` | 03 - Planificacion | 1.0.0 | Activo | Prioridades del Nivel | No declaradas |
| `Docs/03_Planificacion/REQUISITOS.md` | 03 - Planificacion | 1.0.0 | Activo | Requisitos del Nivel | No declaradas |
| `Docs/03_Planificacion/ROADMAP.md` | 03 - Planificacion | 1.0.0 | Activo | Roadmap del Nivel | No declaradas |
| `Docs/04_Razonamiento/COMPONENTES.md` | 04 - Diseno | 1.0.0 | Activo | Componentes | No declaradas |
| `Docs/04_Razonamiento/DECISIONES.md` | 04 - Diseno | 4.1.0 | Activo | Decisiones Arquitectonicas | No declaradas |
| `Docs/04_Razonamiento/DIAGRAMAS.md` | 04 - Diseno | 1.0.0 | Activo | Diagramas Arquitectonicos | No declaradas |
| `Docs/04_Razonamiento/DISENO.md` | 04 - Diseno | 1.0.0 | Activo | Diseno Arquitectonico | No declaradas |
| `Docs/04_Razonamiento/HARNESS_PRESUPUESTO.md` | Arquitectura de ejecución (motor de modelo) | 1.1.0 | Vigente | No declarada | No declaradas |
| `Docs/04_Razonamiento/INTERFACES.md` | 04 - Diseno | 1.0.0 | Activo | Interfaces | No declaradas |
| `Docs/04_Razonamiento/MODELO_DATOS.md` | 04 - Diseno | 1.0.0 | Activo | Modelo de Datos | No declaradas |
| `Docs/04_Razonamiento/PATRONES.md` | 04 - Diseno | 1.0.0 | Activo | Patrones Arquitectonicos | No declaradas |
| `Docs/04_Razonamiento/VALIDACION_DISENO.md` | 04 - Diseno | 1.0.0 | Activo | Validacion del Diseno | No declaradas |
| `Docs/05_Ejecucion/CONFIGURACION.md` | 05 - Implementacion | 1.1.0 | Activo | Documento de Ingenieria | No declaradas |
| `Docs/05_Ejecucion/ESTRATEGIA_PRUEBAS.md` | 05 - Implementacion | 1.0.0 | Activo | Documento de Ingenieria | No declaradas |
| `Docs/05_Ejecucion/FLUJOS_IMPLEMENTACION.md` | 05 - Implementacion | 1.1.0 | Activo | Documento de Ingenieria | No declaradas |
| `Docs/05_Ejecucion/GESTION_ERRORES.md` | 05 - Implementacion | 1.1.0 | Activo | Documento de Ingenieria | No declaradas |
| `Docs/05_Ejecucion/IMPLEMENTACION.md` | 05 - Implementacion | 1.0.0 | Activo | Documento de Ingenieria | No declaradas |
| `Docs/05_Ejecucion/INSTALACION_PUESTA_EN_MARCHA.md` | 05 - Implementacion | 1.2.0 | Activo | Guia de puesta en marcha | No declaradas |
| `Docs/05_Ejecucion/INTEGRACIONES.md` | 05 - Implementacion | 1.2.0 | Activo | Documento de Ingenieria | No declaradas |
| `Docs/05_Ejecucion/MODULOS.md` | 05 - Implementacion | 1.1.0 | Activo | Documento de Ingenieria | No declaradas |
| `Docs/05_Ejecucion/VALIDACION_IMPLEMENTACION.md` | 05 - Implementacion | 1.1.0 | Activo | Documento de Ingenieria | No declaradas |
| `Docs/06_Herramientas/CICLO_VIDA.md` | 06 - Construccion | 1.0.0 | Activo | Arquitectura | No declaradas |
| `Docs/06_Herramientas/EJECUCION.md` | 06 - Construccion | 1.0.0 | Activo | Arquitectura | No declaradas |
| `Docs/06_Herramientas/MEMORIA_OPERATIVA.md` | 06 - Construccion | 1.0.0 | Activo | Arquitectura | No declaradas |
| `Docs/06_Herramientas/MOTORES.md` | 06 - Construccion | 1.0.0 | Activo | Arquitectura | No declaradas |
| `Docs/06_Herramientas/NUCLEO.md` | 06 - Construccion | 1.0.0 | Activo | Arquitectura | No declaradas |
| `Docs/06_Herramientas/ORQUESTACION.md` | 06 - Construccion | 1.0.0 | Activo | Arquitectura | No declaradas |
| `Docs/06_Herramientas/PREPARACION_MVP.md` | 06 - Construccion | 1.0.0 | Activo | Planificacion | No declaradas |
| `Docs/06_Herramientas/VALIDACION_NUCLEO.md` | 06 - Construccion | 1.0.0 | Activo | Validacion | No declaradas |
| `Docs/07_Interfaz/ACCESIBILIDAD.md` | 07 - Interfaz | 2.0.0 | En desarrollo | Accesibilidad y Usabilidad | No declaradas |
| `Docs/07_Interfaz/AUDITORIA_TUI_CLI.md` | No declarado | No declarada | No declarado | No declarada | No declaradas |
| `Docs/07_Interfaz/COMPONENTES_UI.md` | 07 - Interfaz | 2.0.0 | En desarrollo | Componentes de Interfaz | No declaradas |
| `Docs/07_Interfaz/EXPERIENCIA_USUARIO.md` | 07 - Interfaz | 2.0.0 | En desarrollo | Arquitectura de Experiencia | No declaradas |
| `Docs/07_Interfaz/FLUJO_INTERACCION.md` | 07 - Interfaz | 2.0.0 | En desarrollo | Flujo de Experiencia | No declaradas |
| `Docs/07_Interfaz/INTERFAZ.md` | 07 - Interfaz | 2.0.0 | En desarrollo | Arquitectura de Experiencia | No declaradas |
| `Docs/07_Interfaz/MASCOTA_CLI_UNICODE.md` | 07 - Interfaz | 1.0.0 | En desarrollo | Especificacion de interfaz y recurso terminal | No declaradas |
| `Docs/07_Interfaz/MOCKUPS_NIVEL_07_ACTUALIZACION.md` | 07 - Interfaz | 1.0.0 | En desarrollo | Especificacion de mockups | No declaradas |
| `Docs/07_Interfaz/Mockups/README.md` | No declarado | No declarada | No declarado | No declarada | No declaradas |
| `Docs/07_Interfaz/NAVEGACION.md` | 07 - Interfaz | 2.0.0 | En desarrollo | Navegacion y Continuidad | No declaradas |
| `Docs/07_Interfaz/README.md` | No declarado | No declarada | No declarado | No declarada | No declaradas |
| `Docs/07_Interfaz/T-018_DEFINICION_TUI_CLI.md` | No declarado | No declarada | No declarado | No declarada | No declaradas |
| `Docs/07_Interfaz/T-019_AUDITORIA_IMPL_TUI_CLI.md` | No declarado | No declarada | No declarado | No declarada | No declaradas |
| `Docs/07_Interfaz/T-020_IMPLEMENTACION.md` | No declarado | No declarada | **P1–P5 finalizados y verificados** | No declarada | No declaradas |
| `Docs/07_Interfaz/TUI_CONDOR.md` | 07 - Interfaz | 1.0.0 | Verificada en produccion real | Especificacion tecnica y verificacion de la TUI | No declaradas |
| `Docs/07_Interfaz/VALIDACION_INTERFAZ.md` | 07 - Interfaz | 2.0.0 | En desarrollo | Validacion de Experiencia | No declaradas |
| `Docs/08_Calidad/ASEGURAMIENTO_CALIDAD.md` | 08 - Calidad | 1.0.0 | En desarrollo | Aseguramiento de Calidad | No declaradas |
| `Docs/08_Calidad/CALIDAD.md` | 08 - Calidad | 1.0.0 | Activo | Documento de Ingenieria | No declaradas |
| `Docs/08_Calidad/CRITERIOS_ACEPTACION.md` | 08 - Calidad | 1.0.0 | En desarrollo | Criterios de Aceptacion | No declaradas |
| `Docs/08_Calidad/METRICAS.md` | 08 - Calidad | 1.0.0 | En desarrollo | Metricas de Calidad | No declaradas |
| `Docs/08_Calidad/PRUEBAS.md` | 08 - Calidad | 1.0.0 | En desarrollo | Estrategia de Pruebas | No declaradas |
| `Docs/08_Calidad/TRAZABILIDAD.md` | 08 - Calidad | 1.1.0 | En desarrollo | Trazabilidad de Ingenieria | No declaradas |
| `Docs/08_Calidad/VALIDACION.md` | 08 - Calidad | 1.1.0 | En desarrollo | Proceso de Ingenieria | No declaradas |
| `Docs/09_Evolucion/AUDITORIA.md` | 09 - Evolucion | 1.0.1 | Activo | Auditoria | EVOLUCION.md |
| `Docs/09_Evolucion/COMPATIBILIDAD.md` | 09 - Evolucion | 1.0.0 | Activo | Compatibilidad | EVOLUCION.md |
| `Docs/09_Evolucion/DEUDA_EVOLUTIVA.md` | 09 - Evolucion | 1.3.0 | Activo | Deuda Evolutiva | EVOLUCION.md |
| `Docs/09_Evolucion/EVOLUCION.md` | 09 - Evolucion | 1.1.0 | Activo | Evolucion | ESTADO_PROYECTO.md |
| `Docs/09_Evolucion/MEJORA_CONTINUA.md` | 09 - Evolucion | 1.1.0 | Activo | Mejora Continua | EVOLUCION.md |
| `Docs/09_Evolucion/MIGRACION.md` | 09 - Evolucion | 1.0.0 | Activo | Migracion | EVOLUCION.md |
| `Docs/09_Evolucion/ROADMAP_EVOLUCION.md` | 09 - Evolucion | 1.4.0 | Activo | Roadmap | EVOLUCION.md |
| `Docs/09_Evolucion/VERSIONADO.md` | 09 - Evolucion | 1.0.0 | Activo | Versionado | EVOLUCION.md |
| `ESTADO_PROYECTO.md` | No declarado | 2.2.0 | Vigente | Estado del Proyecto | Documentos globales |
| `INVENTARIO_PROYECTO.md` | Global | 1.3.0 | Vigente | No declarada | No declaradas |
| `operacion/BACKLOG.md` | No declarado | 4.1.0 | Activo | No declarada | No declaradas |
| `operacion/ESTADO_DESARROLLO.md` | No declarado | 2.2.0 | Activo | No declarada | No declaradas |
| `operacion/INVENTARIO_TECNICO_REPO.md` | Global | 1.3.0 | Vigente (snapshot tecnico) | Inventario Tecnico | No declaradas |
| `operacion/KANBAN.md` | Global | 2.2.0 | Activo | No declarada | No declaradas |
| `operacion/README.md` | No declarado | No declarada | No declarado | No declarada | No declaradas |
| `operacion/REGISTRO_CAMBIOS.md` | No declarado | 2.2.0 | No declarado | No declarada | No declaradas |
| `operacion/RELEVO.md` | No declarado | No declarada | Software estable al corte 2026-10-19; Graft semantico pendiente de API | No declarada | No declaradas |
| `operacion/TAREAS/T-001.md` | No declarado | 1.1.0 | Completada | No declarada | No declaradas |
| `operacion/TAREAS/T-002.md` | No declarado | 1.3.0 | Cerrada, validada y congelada | No declarada | No declaradas |
| `operacion/TAREAS/T-003.md` | No declarado | 1.2.0 | Completada y cerrada (integrada en main, publicada en GitHub) | No declarada | No declaradas |
| `operacion/TAREAS/T-004.md` | No declarado | 1.2.0 | Completada y congelada (integrada mediante PR #2) | No declarada | No declaradas |
| `operacion/TAREAS/T-005.md` | No declarado | 1.3.0 | Cerrada y congelada | No declarada | No declaradas |
| `operacion/TAREAS/T-006.md` | No declarado | 1.2.0 | Cerrada y congelada (completada, verificada, integrada y publicada) | No declarada | No declaradas |
| `operacion/TAREAS/T-007.md` | No declarado | 1.0.0 | Cerrada y congelada (completada, verificada, integrada y publicada) | No declarada | No declaradas |
| `operacion/TAREAS/T-008.md` | No declarado | 1.0.0 | Cerrada y congelada (completada, verificada, integrada y publicada) | No declarada | No declaradas |
| `operacion/TAREAS/T-009.md` | No declarado | 1.1.0 | Cerrada y congelada (completada, verificada, integrada y publicada) | No declarada | No declaradas |
| `operacion/TAREAS/T-010.md` | No declarado | 1.1.0 | Cerrada y congelada (completada, verificada, integrada y publicada) | No declarada | No declaradas |
| `operacion/TAREAS/T-011.md` | No declarado | 1.1.0 | Cerrada y congelada (completada, verificada, integrada y publicada) | No declarada | No declaradas |
| `operacion/TAREAS/T-012.md` | No declarado | 1.1.0 | Cerrada y congelada (completada, verificada, integrada y publicada) | No declarada | No declaradas |
| `operacion/TAREAS/T-013.md` | No declarado | 1.1.0 | Cerrada y congelada (completada, verificada, integrada y publicada) | No declarada | No declaradas |
| `operacion/TAREAS/T-014.md` | No declarado | 1.1.0 | Cerrada y congelada (completada, verificada, integrada y publicada) | No declarada | No declaradas |
| `operacion/TAREAS/T-015.md` | No declarado | 2.1.0 | Completada y cerrada | No declarada | No declaradas |
| `operacion/TAREAS/T-016.md` | No declarado | 1.0.0 | Completada y cerrada | No declarada | No declaradas |
| `README.md` | No declarado | No declarada | No declarado | No declarada | No declaradas |
---

# FUENTES Y ARTEFACTOS COMPLEMENTARIOS

## Cuadernos historicos

Los cinco archivos de `assets/` se conservan en su ruta actual. Son fuentes de trazabilidad, no documentos normativos ni filas de la tabla de documentos permanentes. Su contenido y relacion con las decisiones formalizadas se detallan en `PATRIMONIO_CONOCIMIENTO.md`.

- `assets/cuaderno I.txt`
- `assets/cuaderno II.txt`
- `assets/cuaderno III.txt`
- `assets/cuaderno IV.txt`
- `assets/cuaderno V.txt`

## Otros artefactos no Markdown

- `LICENSE` y `NOTICE` en la raiz.
- `graft-deep.cmd` en la raiz: solicita `GRAFT_API_KEY` con entrada oculta y ejecuta el build profundo y su verificacion sin guardar la clave.
- `Docs/07_Interfaz/Mockups/condor_mockups_nivel_07_actualizados.html`
- `Docs/07_Interfaz/Mockups/condor_pequeno.ans`
- `Docs/07_Interfaz/Mockups/condor_pequeno.ps1`
- `Docs/07_Interfaz/Mockups/condor_unicode_v16.ps1`
- `Docs/07_Interfaz/Mockups/ejecutar_condor_unicode_v16.cmd`
- `Docs/07_Interfaz/Mockups/png_to_ansi.py`

`Docs/07_Interfaz/Mockups/README.md` figura en la tabla de los 160 archivos Markdown.

Los archivos de configuracion local de herramientas (`AGENTS.md`, `.mcp.json`, `opencode.json`, `.claude/` y `.ignore`) quedan fuera del inventario documental permanente; el estado de versionado se determina con Git.

---

# REGLAS

1. Ningun documento Markdown permanente existente debe quedar fuera del inventario.
2. Las rutas se mantienen exactamente donde estan; actualizar este indice no implica mover ni renombrar archivos.
3. Los metadatos faltantes se muestran como no declarados, sin completarlos por inferencia.
4. Toda eliminacion debera conservar trazabilidad historica.
5. Este inventario no determina el nivel activo; esa funcion corresponde exclusivamente a `ESTADO_PROYECTO.md`.

---

# HISTORIAL DE CAMBIOS

| Version | Cambios |
|---|---|
| 1.4.0 | Se registra el lanzador `graft-deep.cmd` en los artefactos de raiz y se sincronizan las versiones documentales. |
| 1.3.0 | Se sincronizan los metadatos con las actualizaciones documentales del 2026-10-30; se mantienen las 160 rutas y su ubicacion. |
| 1.2.0 | Se amplia a las 160 rutas Markdown observadas, se corrigen estados copiandolos de las fuentes y se separan cuadernos y artefactos auxiliares. |
| 1.1.0 | Se sincroniza el inventario con el Nivel 09 activo, se registran sus ocho entregables y se establece que INVENTARIO_PROYECTO.md no determina el nivel activo. |
| 1.0.0 | Creacion del Inventario Maestro del Proyecto Condor. |
