# DOCUMENTER

Version: 1.1.0
Estado: Activo
Nivel: 01 - Arquitectura Ejecutable
Clasificacion: Componente

---

# Proposito

Generar un informe verificable de cada ejecucion de ingenieria de Condor a partir de sus resultados observados. El informe conserva el estado Antes, Durante y Despues sin reemplazar documentos existentes ni afirmar verificaciones no realizadas.

---

# Dependencias

- KERNEL_CONDOR.md
- CONTRATO_KERNEL.md
- VALIDATOR.md
- `IDocumenter` (Condor.Core) y sus modelos de solicitud y resultado.
- `AgentResult` y `CycleResult` como consumidores opcionales del resultado documental.

---

# Documentos relacionados

- ESTANDAR_DOCUMENTAL.md
- PROTOCOLO_DOCUMENTACION.md
- ESTADO_PROYECTO.md

---

# Responsabilidades

- Crear un informe Markdown por ejecucion de agente en lenguaje natural y por ciclo `/avanzar`.
- Registrar la intencion, las acciones/etapas observadas, el resultado y las limitaciones.
- Devolver el estado y la ruta relativa del informe para la CLI, la TUI y la salida JSON.
- Escribir UTF-8 sin BOM en `docs/condor/tareas/` con un nombre nuevo por ejecucion.
- Evitar incluir vistas previas de archivos y redactar formatos reconocibles de credenciales.
- Comunicar los fallos de escritura sin alterar ni falsear el resultado de la tarea.
- No elegir ni modificar documentos propietarios por inferencia; su actualizacion semantica permanece en la tarea aprobada.

---

# Entradas

- Intencion y directorio de trabajo.
- Pasos del agente o resultados del ciclo Planner/Builder/Verifier.
- Resultado y limitaciones que Condor haya observado.

---

# Salidas

- Informe Markdown con secciones `Antes`, `Durante` y `Despues`.
- `DocumentationResult` con exito, ruta relativa o motivo del fallo.

---

# Reglas

- La conversacion no es fuente de verdad para decisiones permanentes; la intencion se registra solo como contexto de ejecucion.
- Cada informe se crea con `CreateNew`; no reemplaza un archivo anterior.
- El contenido procede de datos observados; el componente no usa LLM ni proveedor externo.
- Un fallo o limite de la tarea tambien se documenta como tal.
- La salida no incluye contenido de archivos ni vistas previas de cambios.
- Una falla al escribir se reporta; no se presenta como informe generado.
- Los documentos oficiales propietarios no se alteran por inferencias automaticas.
- Las reglas de nombre estable aplican a documentos existentes; cada informe de ejecucion es un artefacto nuevo.
- La version vive exclusivamente dentro del documento.
- Todo informe se crea como archivo nuevo y queda listo para incorporarse al repositorio, sin reemplazar uno existente.

---

# Impacto

Toda modificacion requiere revisar ESTANDAR_DOCUMENTAL.md y PROTOCOLO_DOCUMENTACION.md.

---

# Historial de cambios

| Version | Cambios |
|----------|---------|
| 1.1.0 | T-021 implementa informes Antes/Durante/Despues en agente y `/avanzar`, con salida UTF-8, sin proveedor externo y sin sobreescritura. |
| 1.0.0 | Primera version del Documenter. |
