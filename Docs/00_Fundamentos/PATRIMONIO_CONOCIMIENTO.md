# PATRIMONIO_CONOCIMIENTO

Version: 1.3.0
Estado: Activo
Nivel: Global
Clasificacion: Inventario de Conocimiento

------------------------------------------------------------------------

# Proposito

Preservar y trazar el conocimiento fundacional del Proyecto Condor.

Este documento registra las ideas, principios, decisiones, restricciones
y conceptos permanentes surgidos durante la evolucion del proyecto,
indicando su origen y el documento donde quedaron incorporados.

Responde la pregunta:

> **¿Que sabemos, de donde surgio y donde vive ese conocimiento?**

------------------------------------------------------------------------

# Alcance

Incluye el conocimiento proveniente de:

-   Cuaderno I
-   Cuaderno II
-   Cuaderno III
-   Cuaderno IV
-   Cuaderno V
-   Revisiones arquitectonicas
-   Decisiones oficiales del proyecto

No sustituye la documentacion oficial; funciona como su trazabilidad.

## Fuentes historicas disponibles

Los cinco cuadernos se conservan en sus rutas actuales bajo `assets/`. Son transcripciones de conversaciones, no directivas vigentes. Una idea expresada por el usuario puede registrarse como origen; una propuesta del asistente no se considera aprobada por aparecer en la transcripcion. Los documentos oficiales y el estado real del repositorio determinan su incorporacion.

| Fuente | Ruta actual | Proposito y contenido principal |
|---|---|---|
| Cuaderno I | `assets/cuaderno I.txt` | Origen de Condor: problema, vision, persistencia del conocimiento y paso de la conversacion a artefactos del proyecto. Incluye propuestas y texto generado que no debe tomarse automaticamente como decision. |
| Cuaderno II | `assets/cuaderno II.txt` | Recuperacion del proyecto tras un reinicio local; separacion de reglas globales, de nivel, de chat y temporales; organizacion del contexto y prioridad de avanzar hacia un MVP. |
| Cuaderno III | `assets/cuaderno III.txt` | Lecciones sobre revisiones que elevan profundidad, estandar documental, autoauditoria, validacion y limites de entrega. Registra compromisos contradictorios del asistente; no son reglas vigentes por si solos. |
| Cuaderno IV | `assets/cuaderno IV.txt` | Revision arquitectonica global y seis restricciones de Condor 1.x expresadas directamente por el usuario: idioma, modelo local, ciclo de ingenieria, practicas compatibles con recursos, apertura sujeta a legalidad e instalacion Windows sencilla. Tambien contiene axiomas adicionales propuestos por el asistente, separados abajo. |
| Cuaderno V | `assets/cuaderno V.txt` | Evolucion de la experiencia del Nivel 07: terminal como medio inicial, descubrimiento antes de preguntar, evitar repetir conocimiento, unir intencion del usuario con contexto del proyecto y formalizar Context Engine. |

Los archivos estan presentes en el arbol local, pero `assets/` no esta versionado en Git al corte de esta actualizacion. Se conservaron sin modificar. La carpeta se registra como fuente historica, no como documentacion normativa.

------------------------------------------------------------------------

# Flujo

Descubrir

↓

Clasificar

↓

Incorporar

↓

Versionar

↓

Conservar

------------------------------------------------------------------------

# Categorias

-   Principio
-   Axioma
-   Decision
-   Restriccion
-   Arquitectura
-   Metodologia
-   Experiencia
-   Motor
-   Inventario
-   Evolucion futura

------------------------------------------------------------------------

# Estados

-   Pendiente
-   Incorporado parcialmente
-   Incorporado
-   Descartado
-   Evolucion futura

------------------------------------------------------------------------

# Registro de conocimiento y estado actual

Los estados reflejan la evidencia en los documentos vigentes al 2026-10-30. `Incorporado parcialmente` indica que existe una formalizacion relacionada, pero no toda la formulacion historica esta establecida como regla. Los nombres de destino se muestran con sus rutas actuales.

| ID | Conocimiento | Origen | Categoria | Estado | Documento destino o evidencia |
|---|---|---|---|---|---|
| CI-001 | Preservar el conocimiento por encima de la conversacion | Cuaderno I | Principio | Incorporado | `Docs/00_Fundamentos/ADN_CONDOR.md` |
| CI-002 | Convertir decisiones importantes en artefactos permanentes | Cuaderno I | Metodologia | Incorporado | `Docs/00_Fundamentos/DIRECTIVA_OPERATIVA_PROYECTO_CONDOR.md` |
| CI-003 | Comprender, planificar, disenar, implementar, verificar, documentar, congelar y continuar | Cuaderno IV | Metodologia | Incorporado | `Docs/00_Fundamentos/DIRECTIVA_OPERATIVA_PROYECTO_CONDOR.md` |
| CI-004 | Mantener un nucleo atomico de identidad del proyecto | Cuaderno IV | Axioma | Incorporado parcialmente; el ADN vigente contiene principios generales, pero no formaliza los identificadores A-001 a A-006 | `Docs/00_Fundamentos/ADN_CONDOR.md`; propuestas y procedencia abajo |
| CI-005 | Adaptar la capacidad al hardware disponible | Cuaderno IV | Principio | Incorporado parcialmente en Assessment, seleccion y presupuesto; no como axioma identificado | `Docs/01_Arquitectura_Ejecutable/ASSESSMENT_ENGINE.md`; `Docs/02_Memoria/INVENTARIO_ARQUITECTURA.md` |
| CI-006 | Assessment Engine descubre el entorno y las capacidades | Cuaderno IV | Arquitectura | Incorporado; ARQ-002 figura implementado | `Docs/01_Arquitectura_Ejecutable/ASSESSMENT_ENGINE.md`; `Docs/02_Memoria/INVENTARIO_ARQUITECTURA.md` |
| CI-007 | Context Engine reconstruye el contexto del proyecto | Cuaderno V | Arquitectura | Incorporado; especificacion y T-005 registrados | `Docs/01_Arquitectura_Ejecutable/CONTEXT_ENGINE.md`; `operacion/TAREAS/T-005.md` |
| CI-008 | Mantener inventarios con propositos distintos y referencias coherentes | Cuaderno IV | Inventario | En evolucion; esta actualizacion distingue continuidad, inventario maestro, arquitectura, funcionalidad y snapshot tecnico | `INVENTARIO_PROYECTO.md`; `Docs/00_Fundamentos/INVENTARIO_PROYECTO.md`; `Docs/02_Memoria/INVENTARIO_FUNCIONAL.md`; `Docs/02_Memoria/INVENTARIO_ARQUITECTURA.md`; `operacion/INVENTARIO_TECNICO_REPO.md` |
| CI-009 | La experiencia es el producto; la terminal es el medio inicial | Cuaderno V | Experiencia | Incorporado en la documentacion del Nivel 07 | `Docs/07_Interfaz/EXPERIENCIA_USUARIO.md`; `Docs/07_Interfaz/INTERFAZ.md`; `Docs/07_Interfaz/FLUJO_INTERACCION.md` |
| CI-010 | La IA y el ingeniero construyen Condor conjuntamente | Cuaderno V | Principio | Pendiente de formalizar como principio explicito; no se infiere ratificacion desde la transcripcion | `Docs/00_Fundamentos/CONDOR_CONTEXTO_MAESTRO.md` |
| CI-011 | Ciclo de ingenieria: planificar, construir, verificar y documentar | T-006 a T-009 | Motor | Incorporado en el ciclo y el rol Documenter | `operacion/TAREAS/T-006.md`; `operacion/TAREAS/T-007.md`; `operacion/TAREAS/T-008.md`; `operacion/TAREAS/T-009.md`; `Docs/01_Arquitectura_Ejecutable/DOCUMENTADOR.md` |
| CI-012 | Separar reglas globales, de nivel, de chat y temporales | Cuaderno II | Metodologia | Incorporado en la jerarquia de directivas | `Docs/00_Fundamentos/DIRECTIVA_GLOBAL.md`; `Docs/00_Fundamentos/DIRECTIVA_OPERATIVA_PROYECTO_CONDOR.md` |
| CI-013 | Descubrir antes de preguntar y no pedir conocimiento ya disponible | Cuaderno V | Experiencia | Incorporado en los contratos de experiencia e interaccion del Nivel 07 | `Docs/07_Interfaz/EXPERIENCIA_USUARIO.md`; `Docs/07_Interfaz/INTERFAZ.md`; `Docs/07_Interfaz/FLUJO_INTERACCION.md` |

## Restricciones expresadas por el usuario en el Cuaderno IV

El usuario declaro seis condiciones no negociables para Condor 1.0. Los identificadores A-001 a A-006 aparecen en la respuesta posterior del asistente; se conservan aqui como referencias de esa conversacion, no como IDs normativos ya incorporados al ADN.

| Referencia | Condicion expresada | Estado en documentos actuales | Evidencia o destino |
|---|---|---|---|
| A-001 | Espanol latinoamericano sin tildes para Condor 1.0; otros idiomas despues | Incorporado parcialmente en decisiones de CLI y Nivel 07; falta explicitarlo como axioma del ADN | `Docs/04_Razonamiento/DECISIONES.md`; `Docs/07_Interfaz/ACCESIBILIDAD.md`; `Docs/00_Fundamentos/ADN_CONDOR.md` |
| A-002 | Modelo local descargado gratuitamente mediante Ollama | La integracion local con Ollama esta documentada e implementada; la condicion atomica no esta escrita como tal en el ADN | `Docs/02_Memoria/INVENTARIO_ARQUITECTURA.md`; `Docs/05_Ejecucion/INSTALACION_PUESTA_EN_MARCHA.md`; `Docs/00_Fundamentos/ADN_CONDOR.md` |
| A-003 | Condor debe actuar durante el ciclo de ingenieria, no solo generar codigo | Incorporado en el ciclo y los motores documentados | `Docs/00_Fundamentos/ADN_CONDOR.md`; `Docs/01_Arquitectura_Ejecutable/KERNEL_CONDOR.md`; `Docs/00_Fundamentos/DIRECTIVA_OPERATIVA_PROYECTO_CONDOR.md` |
| A-004 | Aprovechar loops, harness y practicas vigentes segun hardware y modelo disponibles | Incorporado parcialmente mediante Harness, presupuesto y ciclo; no consta la formulacion atomica completa | `Docs/04_Razonamiento/HARNESS_PRESUPUESTO.md`; `Docs/01_Arquitectura_Ejecutable/KERNEL_CONDOR.md`; `Docs/00_Fundamentos/ADN_CONDOR.md` |
| A-005 | Vocacion de software abierto, con publicacion condicionada a resolver asuntos legales | Pendiente; los archivos legales actuales declaran All Rights Reserved, coherente con no afirmar una licencia abierta | `LICENSE`; `NOTICE` |
| A-006 | Instalacion sencilla, sin piezas que el usuario deba unir; Windows como primer sistema operativo | Incorporado parcialmente en puesta en marcha y CLI Windows; falta consolidarlo como axioma del ADN | `Docs/05_Ejecucion/INSTALACION_PUESTA_EN_MARCHA.md`; `Docs/07_Interfaz/TUI_CONDOR.md`; `Docs/00_Fundamentos/ADN_CONDOR.md` |

## Propuestas del asistente no ratificadas como axiomas

El Cuaderno IV tambien contiene las propuestas A-000 y A-007 a A-009, formuladas por el asistente despues de la lista del usuario. No se promueven aqui como decisiones nuevas. A-007 a A-009 se solapan con CI-001, CI-005 y principios generales del ADN; A-000 requiere ratificacion explicita antes de convertirse en axioma.

------------------------------------------------------------------------

# Reglas

1.  Ninguna idea fundacional debera perderse en conversaciones.
2.  Todo conocimiento permanente debera registrarse antes o durante su
    incorporacion.
3.  Cada registro debera indicar su origen y documento destino.
4.  Este documento representa el patrimonio intelectual del Proyecto
    Condor.
5.  Las propuestas del asistente en una transcripcion no se convierten en
    decisiones sin evidencia de aceptacion del usuario o formalizacion vigente.
6.  Los archivos fuente de `assets/` se conservan en su ruta original; este
    documento registra su procedencia sin reescribirlos.

------------------------------------------------------------------------

# Historial de cambios

| Version | Cambios |
|---|---|
| 1.3.0 | Se identifican las rutas y propositos de los cinco cuadernos; se actualizan los estados CI-004 a CI-010 y se distingue lo expresado por el usuario de las propuestas del asistente. |
| 1.2.0 | CI-011 pasa a Incorporado: el ciclo operativo y el rol Documenter quedan trazados a T-006..T-009 y DOCUMENTADOR.md. |
| 1.1.0 | CI-007 pasa a Incorporado: CONTEXT_ENGINE.md y T-005 formalizan el Context Engine. |
| 1.0.0 | Creacion del inventario oficial del patrimonio de conocimiento. |
