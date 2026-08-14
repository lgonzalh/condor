# ESTADO_DESARROLLO

Version: 3.2.0
Estado: Activo
Modo: Evolucion Continua
MVP: Condor 1.0

## Estado actual

Condor cuenta con T-001 a T-014 completadas, verificadas e integradas en
`main`.

T-004 a T-014 estan formalmente congeladas.

El backlog del MVP 1.0 (T-001 a T-012) queda completado. T-013 y T-014 incorporan
la verificacion semantica (SD-02) y su integracion al ciclo de ingenieria
respectivamente. La evolucion posterior se define mediante el ciclo de Evolucion
Continua.

## Estado funcional

Condor puede:

- ejecutarse localmente en Windows;
- analizar el entorno local;
- detectar herramientas y modelos locales;
- persistir el Assessment;
- comunicarse con Ollama mediante loopback;
- ejecutar inferencia local;
- recomendar un modelo local;
- descubrir el proyecto objetivo;
- construir `ProjectProfile`;
- reconstruir `ProjectContext`;
- leer artefactos operativos de forma acotada;
- determinar punto de continuacion con evidencia;
- detectar riesgos estructurados;
- producir recomendaciones para Planner;
- persistir `context.json` como artefacto derivado;
- emitir `condor contexto`;
- emitir `condor contexto --json`;
- generar un `WorkPlan` desde la intencion del usuario;
- interpretar la intencion (nueva / continuar / modificar / indefinida);
- descomponer el objetivo en tareas con dependencias y prioridad;
- persistir `plan.json` como artefacto derivado;
- emitir `condor planear`;
- emitir `condor planear --json`;
- consumir el `WorkPlan` para derivar acciones de implementacion;
- aplicar cambios acotados de archivos sobre el proyecto objetivo (crear/actualizar);
- rechazar rutas absolutas y traversal fuera del objetivo;
- persistir `build.json` como artefacto derivado;
- emitir `condor construir`;
- emitir `condor construir --json`;
- verificar la integridad y acotacion de los cambios aplicados;
- comprobar existencia de archivos declarados como aplicados y coincidencia de contenido;
- registrar cada verificacion mediante `VerificationCheck`;
- persistir `verification.json` como artefacto derivado;
- emitir `condor verificar`;
- emitir `condor verificar --json`;
- mantener la documentacion permanente sincronizada con el estado real;
- especificar el rol de Documenter (DOCUMENTADOR.md);
- distinguir deuda pendiente (DEUDA_EVOLUTIVA.md) y siguiente linea (ROADMAP_EVOLUCION.md);
- orquestar el ciclo de ingenieria parcial (Planner -> Builder -> Verifier);
- regenerar de forma controlada e interna al ciclo (determinista, acotada);
- persistir el checkpoint del ciclo (`cycle.json`) como artefacto derivado;
- analizar una imagen local con un VLM local (si hay GPU y modelo de vision);
- degradar de forma estructurada cuando la vision no esta disponible;
- verificar la puesta en marcha y el estado del entorno (condor preparar);
- distinguir dependencias obligatorias de opcionales sin descargar nada;
- preservar el estado local de forma no destructiva;
- compilar el proyecto objetivo con --no-restore (condor verificar-semantico);
- ejecutar las pruebas del proyecto objetivo con --no-restore;
- diferenciar compilacion/pruebas correctas, fallidas y capacidades no disponibles;
- integrar la verificacion semantica como etapa del ciclo de ingenieria
  (condor avanzar);
- distinguir semantica correcta, no_disponible, incompleta y fallida en el ciclo;
- degradar de forma controlada;
- mantener limites deterministas.

## Contratos CLI vigentes

Los comandos publicos permanecen en espanol y sin tildes.

T-005 agrego:

- `condor contexto`
- `condor contexto --json`

T-006 agrego:

- `condor planear "<solicitud>"`
- `condor planear "<solicitud>" --json`

T-007 agrego:

- `condor construir`
- `condor construir --json`

T-008 agrego:

- `condor verificar`
- `condor verificar --json`

T-010 agrego:

- `condor avanzar "<solicitud>"`
- `condor avanzar "<solicitud>" --json`

T-011 agrego:

- `condor examinar "<imagen>"`
- `condor examinar "<imagen>" --json`

T-012 agrego:

- `condor preparar`
- `condor preparar --json`
- `condor preparar --actualizar`

T-013 agrego:

- `condor verificar-semantico`
- `condor verificar-semantico --compilar`
- `condor verificar-semantico --probar`
- `condor verificar-semantico --json`

No reintroducir contratos anteriores en ingles.

## Estado Git

Ultimo estado confirmado al cierre de la implementacion de T-014:

`cc93938`

`HEAD == origin/main`

Working tree limpio.

Rama activa: `main`. 

## Evidencia acumulada

T-001 a T-004: completadas, verificadas, integradas y publicadas.

T-004:
- 174/174 pruebas correctas;
- build limpio;
- E2E real;
- cierre documental;
- congelacion formal.

T-005:
- 102/102 pruebas unitarias;
- 93/93 pruebas de integracion;
- 11/11 pruebas de arquitectura;
- CLI verificada;
- E2E real;
- determinismo D-D11 verificado;
- D-D1 a D-D12 cumplidas;
- 51 commits auditados sin violaciones de `1 archivo = 1 commit`;
- publicacion completa en `origin/main`;
- cierre y congelacion formal.

T-006:
- 113/113 pruebas unitarias;
- 102/102 pruebas de integracion;
- 13/13 pruebas de arquitectura;
- CLI planear y planear --json verificadas;
- E2E real;
- determinismo D-E7 verificado;
- D-E1 a D-E8 (DEC-030) y D-DE1 a D-DE6 (DEC-031) cumplidas;
- publicacion completa en `origin/main`;
- cierre y congelacion formal.

T-007:
- 128/128 pruebas unitarias (Core);
- 115/115 pruebas de integracion (Infrastructure);
- 14/14 pruebas de arquitectura;
- build Release sin errores ni advertencias;
- CLI construir y construir --json verificadas;
- E2E real sobre un proyecto objetivo temporal;
- determinismo del Builder verificado (doble ejecucion);
- degradaciones y rechazo de rutas fuera de objetivo verificados;
- D-B1 a D-B5 (DEC-032) y D-DB1 a D-DB7 (DEC-033) cumplidas;
- commits auditados sin violaciones de `1 archivo = 1 commit`;
- publicacion completa en `origin/main`;
- cierre y congelacion formal.

T-008:
- 143/143 pruebas unitarias (Core);
- 127/127 pruebas de integracion (Infrastructure);
- 15/15 pruebas de arquitectura;
- build Release sin errores ni advertencias;
- CLI verificar y verificar --json verificadas;
- E2E real sobre un proyecto objetivo temporal (caso correcto e incorrecto);
- determinismo del Verifier verificado (doble ejecucion);
- degradaciones y deteccion de integridad/acotacion verificadas;
- D-V1 a D-V5 (DEC-034) y D-DV1 a D-DV7 (DEC-035) cumplidas;
- commits auditados sin violaciones de `1 archivo = 1 commit`;
- publicacion completa en `origin/main`;
- cierre y congelacion formal.

T-009:
- tarea exclusivamente documental (sin codigo);
- DOCUMENTADOR.md creado e integrado en los inventarios (FN-009/ARQ-008 Especificado);
- trazabilidad T-001 a T-008 preservada sin reescritura de historia;
- deuda pendiente (DEUDA_EVOLUTIVA DE-002) y siguiente linea (ROADMAP SD-01/SD-02) consolidados sin duplicidad;
- PATRIMONIO_CONOCIMIENTO actualizado (CI-011);
- revision documental satisfactoria;
- commits auditados sin violaciones de `1 archivo = 1 commit`;
- publicacion completa en `origin/main`;
- cierre y congelacion formal.

T-010:
- 152/152 pruebas unitarias (Core);
- 133/134 pruebas de integracion (Infrastructure; la unica fallida es una prueba de entorno de T-002 dependiente de Ollama, ajena a T-010);
- 16/16 pruebas de arquitectura;
- CLI avanzar y avanzar --json verificadas;
- E2E real del ciclo (planificar, construir, verificar) sobre objetivo temporal;
- determinismo del ciclo verificado (doble ejecucion) con CycleId deterministico;
- degradaciones y proteccion MaxIterations verificadas;
- checkpoint `cycle.json` persistido como artefacto derivado;
- D-C1 a D-C5 (DEC-037) y D-DY1 a D-DY8 (DEC-038) cumplidas;
- commits auditados sin violaciones de `1 archivo = 1 commit`;
- publicacion completa en `origin/main`;
- cierre y congelacion formal.

T-011:
- 157/157 pruebas unitarias (Core);
- 147/147 pruebas de integracion (Infrastructure; incluye compatibilidad textual de T-002);
- 17/17 pruebas de arquitectura;
- CLI examinar y examinar --json verificadas;
- extension aditiva multimodal de LlmRequest/OllamaClient sin romper texto;
- E2E real de degradacion (sin modelo de vision instalado) documentada;
- vision.json persistido solo con metadatos (sin imagen ni Base64);
- degradaciones y determinismo de la parte no-LLM verificadas;
- D-N1 a D-N5 (DEC-039) y D-DW1 a D-DW8 (DEC-040) cumplidas;
- commits auditados sin violaciones de `1 archivo = 1 commit`;
- publicacion completa en `origin/main`;
- cierre y congelacion formal.

T-012:
- 166/166 pruebas unitarias (Core);
- 154/154 pruebas de integracion (Infrastructure);
- 18/18 pruebas de arquitectura;
- CLI preparar y preparar --json verificadas;
- E2E real: diagnostico `Detected` en el entorno real con dependencias
  obligatorias/opcionales diferenciadas;
- comportamiento no destructivo y preservacion del estado local verificados;
- determinismo del diagnostico verificado (doble ejecucion);
- INSTALACION_PUESTA_EN_MARCHA.md creada (guia de puesta en marcha);
- D-P1 a D-P5 (DEC-041) y D-DS1 a D-DS9 (DEC-042) cumplidas;
- commits auditados sin violaciones de `1 archivo = 1 commit`;
- publicacion completa en `origin/main`;
- cierre y congelacion formal.

T-013:
- 180/180 pruebas unitarias (Core);
- 161/162 pruebas de integracion (Infrastructure; la unica fallida es una prueba
  de entorno de T-002 dependiente de Ollama, ajena a T-013);
- 19/19 pruebas de arquitectura;
- CLI verificar-semantico (con --compilar/--probar/--json) verificadas;
- E2E real sobre un proyecto .NET temporal: build exitoso, test exitoso,
  compilacion fallida, no-restore efectivo y degradaciones;
- `verificacion_semantica.json` persistido como artefacto derivado (resumen);
- --no-restore sin restore implicito; contencion y timeout aplicados;
- D-SD1 a D-SD5 (DEC-043) y D-ST1 a D-ST9 (DEC-044) cumplidas;
- commits auditados sin violaciones de `1 archivo = 1 commit`;
- publicacion completa en `origin/main`;
- cierre y congelacion formal.

T-014:
- build Release sin errores ni advertencias;
- 180/180 pruebas unitarias (Core); 167/168 de integracion (la unica fallida es
  una prueba de entorno de T-002 dependiente de Ollama, ajena a T-014);
  19/19 de arquitectura;
- ciclo `condor avanzar` integra la verificacion semantica (T-013) como etapa,
  de forma aditiva y reutilizando SemanticVerificationService;
- distingue semantica correcta / no_disponible / incompleta / fallida;
- `cycle.json` guarda resumen y referencia a `verificacion_semantica.json` sin
  duplicar su contenido;
- E2E real sobre proyecto .NET temporal: semantica correcta y compilacion
  fallida reflejadas en el ciclo; no falso exito;
- compatibilidad con `condor verificar` y `condor verificar-semantico`;
- D-IN1 a D-IN5 (DEC-045) y D-IC1 a D-IC6 (DEC-046) cumplidas;
- commits auditados sin violaciones de `1 archivo = 1 commit`;
- publicacion completa en `origin/main`;
- cierre y congelacion formal.

## Tareas

| ID | Trabajo | Estado |
|---|---|---|
| T-001 | Bootstrap del MVP y Assessment inicial | Completada |
| T-002 | Integracion local con Ollama | Completada |
| T-003 | Recomendador de modelos | Completada |
| T-004 | Descubrimiento de proyecto | Completada y congelada |
| T-005 | Context Engine inicial | Completada, verificada y congelada |
| T-006 | Flujo de intencion a plan | Completada, verificada y congelada |
| T-007 | Builder inicial | Completada, verificada y congelada |
| T-008 | Verificacion inicial | Completada, verificada y congelada |
| T-009 | Documentacion y continuidad | Completada, verificada y congelada |
| T-010 | Capacidades avanzadas de desarrollo | Completada, verificada y congelada |
| T-011 | Vision local | Completada, verificada y congelada |
| T-012 | Instalador y puesta en marcha simplificada | Completada, verificada y congelada |
| T-013 | Verificacion semantica y de calidad (SD-02) | Completada, verificada y congelada |
| T-014 | Integracion de la verificacion semantica en el ciclo | Completada, verificada y congelada |

## Siguiente evolucion

El backlog del MVP 1.0 (T-001 a T-012) queda completado y congelado.

T-013 y T-014 incorporan la verificacion semantica (SD-02, compilar/probar) y su
integracion al ciclo de ingenieria.

La evolucion posterior se define mediante el ciclo de Evolucion Continua,
continuando la linea SD-02 hacia capacidades de calidad/arquitectura/coherencia.

## Regla de continuidad

El conocimiento permanente debe permanecer en el repositorio.

No reconstruir el contexto desde conversaciones anteriores si el repositorio contiene la informacion necesaria.

## Contexto de niveles

No existe nivel activo.

El estado oficial es `Evolucion Continua`.

La evolucion posterior no crea un Nivel 10.
