# REGISTRO_CAMBIOS

Version: 1.3.0
Estado: Activo

## Proposito

Registrar cambios de implementacion y decisiones relevantes que necesiten contexto adicional al historial de Git.

## Registro

| ID | Fecha | Agente | Tarea | Cambio | Resultado | Commit |
|---|---|---|---|---|---|---|
| CH-001 | 2026-08-10 | Preparacion | T-001 | Creacion del sistema de continuidad multi-agente y backlog inicial del MVP | Pendiente de implementacion | - |
| CH-002 | 2026-08-10 | Agente Condor | T-001 | Aprobacion de la tecnologia .NET 10/C# para el MVP 1.0 como decision de implementacion (DEC-007), definicion de estado local separado del conocimiento persistente (DEC-008), contrato AssessmentResult versionable (DEC-009), separacion de proyectos (DEC-010), convenciones de idioma (DEC-011) y correccion de referencias documentales (DEC-012) | Aprobadas y registradas en Docs/04_Razonamiento/DECISIONES.md | - |
| CH-003 | 2026-08-10 | Agente Condor | T-001 | Implementacion del MVP basico: Condor.slnx, proyectos Condor.Cli/Core/Infrastructure, Tests Unit/Integration/Architecture, .gitignore. Deteccion local de SO, CPU, RAM, GPU, almacenamiento, Git, herramientas y Ollama/modelos. Correccion de ESTRUCTURA_REPOSITORIO.md (v2.0.0) y registro de deuda resuelta DE-001 | T-001 completada, 28 pruebas superadas, verificacion manual de CLI y JSON | - |
| CH-004 | 2026-08-10 | Agente Condor | T-002 | Implementacion de la integracion local con Ollama: contrato ILlmClient y modelos LlmRequest/LlmResponse en Core, OllamaClient en Infrastructure (POST 127.0.0.1:11434/api/chat, stream:false, timeout 180 s, degradaciones claras), seleccion de modelo (explicito o primer disponible del Assessment), lectura aditiva LoadAssessmentAsync en IStateStore/LocalStateStore, comando `condor ask "<mensaje>" [--model <modelo>]` en la CLI. Decisiones DEC-013 a DEC-018 registradas. Correccion C-1 de la revision aplicada: LoadAssessmentAsync devuelve null ante JSON corrupto y LlmModelSelector maneja Tools/Ollama/Models ausentes; pruebas para assessment corrupto, parcial y comportamiento de ask ante assessment invalido | T-002 implementada, en revision. 50 pruebas superadas (9 arquitectura + 15 unit + 26 integracion), build 0W/0E, evidencia funcional real: inferencia con modelo implicito y explicito, 404 de modelo inexistente, degradacion con Ollama detenido y con assessment corrupto | - |
