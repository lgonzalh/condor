# INTEGRACIONES

Version: 1.2.0
Estado: Activo
Nivel: 05 - Implementacion
Clasificacion: Documento de Ingenieria

---

# Proposito

Definir la arquitectura de integraciones del Proyecto Condor, estableciendo contratos, adaptadores, flujos y reglas para la comunicacion entre modulos internos y servicios externos.

---

# Principios

- Toda integracion se realiza mediante interfaces.
- Ningun modulo depende de una implementacion concreta.
- Toda integracion externa utiliza un adaptador.
- Las dependencias son reemplazables y verificables.

---

# Integraciones Internas

| Origen | Destino | Contrato | Resultado |
|--------|---------|----------|-----------|
| Kernel | Memoria | IMemoria | Contexto |
| Kernel | Planificador | IPlanificador | Plan |
| Planificador | Arquitecto | IArquitecto | Especificacion |
| Arquitecto | Implementador | IImplementador | Cambios |
| Implementador | Revisor | IRevisor | Observaciones |
| Revisor | Validador | IValidador | Validacion |
| Validador | Documentador | IDocumentador | Documentacion |

---

# Integraciones Externas

## Modelos de lenguaje

Adaptador:
- IModeloIA

Funciones:
- inferencia
- herramientas
- contexto

Endpoints utilizados:
- `GET /api/version` - verificacion del servidor.
- `GET /api/tags` - inventario de modelos (`name`, `size`, `details.family`, `details.parameter_size`, `details.quantization_level`, `details.context_length`, `capabilities`).
- `POST /api/chat` - inferencia local con stream desactivado.

---

## Sistema de archivos

Adaptador:
- IFileSystem

Funciones:
- lectura
- escritura
- versionado

---

## Control de versiones

Adaptador:
- IGitProvider

Funciones:
- estado
- commit
- ramas
- etiquetas

---

## Herramientas externas

Adaptador:
- IToolProvider

Funciones:
- invocacion
- resultados
- registro

---

# Flujo de Integracion

Solicitud

↓

Interfaz

↓

Adaptador

↓

Servicio

↓

Respuesta

↓

Normalizacion

↓

Modulo solicitante

---

# Manejo de Errores

- Registrar toda falla.
- Reintentos solo cuando proceda.
- Aislar errores externos.
- No propagar excepciones sin contexto.

---

# Reglas

- No consumir servicios externos directamente.
- No acoplar logica de negocio con adaptadores.
- Toda nueva integracion requiere un contrato.
- Toda integracion debe ser documentada y validada.

---

# Historial

| Version | Fecha | Cambio |
|----------|------------|----------------------------------------------|
| 1.2.0 | 2026-08-11 | Se documentan los endpoints reales de Ollama utilizados por T-002 y T-003. |
| 1.1.0 | 2026-08-04 | Regeneracion incorporando contratos, adaptadores y arquitectura de integraciones. |
