# RELEVO

Version: 1.1.0
Estado: Activo

## Ultimo agente

Agente Condor (OpenCode).

## Ultimo trabajo

Implementacion de T-001: bootstrap del MVP 1.0 con .NET 10 / C#.

Entregados:

- CLI inicial (`Src/Condor.Cli`) con identidad, estado inicial, `assess` y `assess --json`.
- Assessment local (`Src/Condor.Infrastructure`) con deteccion de SO, CPU, RAM, GPU, almacenamiento, Git, herramientas y Ollama/modelos.
- Contratos y modelos agnosticos (`Src/Condor.Core`).
- Pruebas Unit, Integration y Architecture (28 pruebas superadas).
- Correcciones documentales (T-001, ESTRUCTURA_REPOSITORIO.md) y registro de decisiones (DEC-007 a DEC-012).

## Estado

T-001 completada. No existe pendiente dentro de T-001.

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

## Trabajo pendiente

Ver `BACKLOG.md`.

## Siguiente accion exacta

Ejecutar T-002 (Integracion local con Ollama): detectar Ollama, consultar modelos disponibles y ejecutar una inferencia local, consumiendo el resultado del Assessment generado por T-001.

Comandos para ejecutar Condor:

- `dotnet run --project Src/Condor.Cli`
- `dotnet run --project Src/Condor.Cli -- assess`
- `dotnet run --project Src/Condor.Cli -- assess --json`
- `dotnet test Condor.slnx`

## Advertencias

- No asumir un modelo LLM fijo antes del assessment.
- No convertir capacidades deseadas en requisitos obligatorios si el hardware o el modelo no las soportan.
- No comenzar con una arquitectura distribuida o dependencias cloud para resolver necesidades del MVP.
- No modificar contratos de Condor.Core sin registrar la decision.
- El resultado del Assessment persiste en el estado local; el conocimiento permanente sigue viviendo en Docs/ y operacion/.
