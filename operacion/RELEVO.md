# RELEVO

Version: 1.2.0
Estado: Activo

## Ultimo agente

Agente Condor (OpenCode).

## Ultimo trabajo

Implementacion de T-002 (integracion local con Ollama), en revision.

Entregados:

- Comando `condor ask "<mensaje>" [--model <modelo>]` en `Src/Condor.Cli`.
- Contrato agnostico `ILlmClient` y modelos `LlmRequest`/`LlmResponse` en `Src/Condor.Core`.
- `OllamaClient` en `Src/Condor.Infrastructure/Llm` (POST a `http://127.0.0.1:11434/api/chat`, `stream:false`, timeout de 180 s, sin dependencias externas).
- Seleccion de modelo `LlmModelSelector`: modelo explicito > primer modelo disponible del Assessment (provisional; T-003 hara la recomendacion inteligente).
- Lectura aditiva `LoadAssessmentAsync` en `IStateStore` y `LocalStateStore` (sin romper `SaveAssessmentAsync`).
- Pruebas Unit, Integration y Architecture (45 pruebas superadas, 17 nuevas para T-002).
- Decisiones DEC-013 a DEC-018 en `Docs/04_Razonamiento/DECISIONES.md` (v1.2.0) y especificacion `operacion/TAREAS/T-002.md`.

## Estado

T-002 implementada y en revision. Build 0W/0E, 45/45 pruebas, evidencia funcional real verificada (inferencia local, modelo explicito/implicito, degradaciones).

## Decisiones vigentes

- Condor 1.0 sera local.
- Windows es la plataforma oficial inicial.
- La interfaz inicial sera terminal.
- Ollama sera la implementacion inicial para modelos locales.
- El modelo se seleccionara segun el assessment del entorno.
- Condor debe aprovechar herramientas de desarrollo avanzadas cuando sean viables.
- Vision sera una capacidad opcional dependiente del hardware y modelo.
- El agente debe poder ser reemplazado sin perder continuidad.
- La tecnologia de implementacion es .NET 10 / C# (DEC-007), decision de implementacion, no dependencia arquitectonica permanente.
- El estado local de Condor vive en `%LOCALAPPDATA%\Condor\state\` y no constituye conocimiento persistente (DEC-008).
- El contrato AssessmentResult es version 1.0.0 y es versionable (DEC-009).
- El comando de consulta es `condor ask "<mensaje>" [--model <modelo>]` (DEC-013).
- `IStateStore` incorpora `LoadAssessmentAsync` de forma aditiva (DEC-014).
- La seleccion de modelo es: explicito > primer modelo del Assessment; la recomendacion inteligente queda para T-003 (DEC-015).
- La comunicacion con Ollama usa `/api/chat` con `stream:false`, solo loopback 127.0.0.1 y sin dependencias externas (DEC-016).
- T-002 incluye su especificacion en `operacion/TAREAS/T-002.md` (DEC-017).
- T-002 se desarrolla en la rama `feature/T-002-ollama` (DEC-018).

## Trabajo pendiente

Ver `BACKLOG.md`. T-002 esta en revision (ramas: `feature/T-002-ollama`).

## Siguiente accion exacta

Revisar el informe de T-002; al aprobarse, autorizar el commit de la rama `feature/T-002-ollama` y posteriormente el merge a `main`. Despues, ejecutar T-003 (Recomendador de modelos).

Comandos para ejecutar Condor:

- `dotnet run --project Src/Condor.Cli`
- `dotnet run --project Src/Condor.Cli -- assess`
- `dotnet run --project Src/Condor.Cli -- assess --json`
- `dotnet run --project Src/Condor.Cli -- ask "<mensaje>"`
- `dotnet run --project Src/Condor.Cli -- ask "<mensaje>" --model <modelo>`
- `dotnet test Condor.slnx`

## Advertencias

- No asumir un modelo LLM fijo antes del assessment.
- No convertir capacidades deseadas en requisitos obligatorios si el hardware o el modelo no las soportan.
- No comenzar con una arquitectura distribuida o dependencias cloud para resolver necesidades del MVP.
- No modificar contratos de Condor.Core sin registrar la decision.
- No escribir en rutas del repositorio con herramientas de edicion directa (fallan): escribir en `%TEMP%\opencode\` y copiar con `Copy-Item -Force`.
- El resultado del Assessment persiste en el estado local; el conocimiento permanente sigue viviendo en Docs/ y operacion/.
