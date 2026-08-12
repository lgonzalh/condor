# ESTADO_DESARROLLO

Version: 2.0.0 Estado: Activo Modo: Evolucion Continua MVP: Condor 1.0

## Estado actual

Condor 0.1.0 cuenta con T-001, T-002, T-003 y T-004 completadas,
verificadas, integradas en `main`, publicadas y, en el caso de T-004,
formalmente congelada.

## Estado funcional

Condor puede:

-   ejecutarse localmente en Windows;
-   analizar sistema operativo, CPU, RAM, GPU y almacenamiento;
-   detectar Git, herramientas, Ollama y modelos locales;
-   persistir el Assessment;
-   comunicarse con Ollama mediante loopback;
-   ejecutar inferencia local;
-   recomendar un modelo local;
-   descubrir el proyecto objetivo;
-   identificar lenguajes, frameworks y manifiestos mediante senales
    objetivas;
-   extraer dependencias de primer nivel de manifiestos soportados;
-   detectar documentacion por presencia;
-   observar estado Git basico;
-   reportar estructura y volumen con limites;
-   degradar ante datos no verificables o limites excedidos;
-   emitir el perfil del proyecto en JSON.

## Contrato CLI vigente

Los comandos publicos estan en espanol:

-   `condor`
-   `condor analizar`
-   `condor analizar --json`
-   `condor recomendar`
-   `condor recomendar --proposito <tipo>`
-   `condor consultar "<mensaje>"`
-   `condor consultar "<mensaje>" --modelo <modelo>`
-   `condor version`
-   `condor ayuda`

Alias vigentes:

-   `-h`
-   `--help`
-   `-v`
-   `--version`

Valores de `--proposito`:

-   `desarrollo`
-   `general`
-   `vision`

No se debe reintroducir el contrato anterior en ingles.

## Estado Git

Ultimo estado confirmado:

`a60005022399d360672aa43ca053e8156ec03efa`

`HEAD == origin/main`

Working tree limpio.

Solo existe `main` local y `origin/main` remoto.

## Evidencia acumulada

T-001 a T-003: - funcionalidades verificadas; - integradas en main; -
publicadas.

T-004: - 174/174 pruebas; - build limpio; - E2E real; - pruebas
manuales; - PR #2 integrado; - cierre documental publicado; -
congelacion formal.

## Tareas

  ID      Trabajo                                      Estado
  ------- -------------------------------------------- ------------------------
  T-001   Bootstrap del MVP y Assessment inicial       Completada
  T-002   Integracion local con Ollama                 Completada
  T-003   Recomendador de modelos                      Completada
  T-004   Descubrimiento de proyecto                   Completada y congelada
  T-005   Context Engine inicial                       Pendiente
  T-006   Flujo de intencion a plan                    Pendiente
  T-007   Builder inicial                              Pendiente
  T-008   Verificacion inicial                         Pendiente
  T-009   Documentacion y continuidad                  Pendiente
  T-010   Capacidades avanzadas de desarrollo          Pendiente
  T-011   Vision local                                 Pendiente
  T-012   Instalador y puesta en marcha simplificada   Pendiente

## Siguiente tarea

`T-005 - Context Engine inicial`

T-005 no esta iniciada.

Primera etapa: reconocimiento y formalizacion de la tarea.

No iniciar implementacion hasta disponer de contrato y decisiones
aprobadas.

## Regla de continuidad

El conocimiento permanente debe permanecer en el repositorio.

No reconstruir el contexto desde conversaciones anteriores si el
repositorio contiene la informacion necesaria.

## Contexto de niveles

No existe nivel activo.

El estado oficial es `Evolucion Continua`.

No crear ni reabrir un nivel numerico para T-005.
