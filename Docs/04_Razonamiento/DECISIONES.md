# DECISIONES

Version: 1.1.0
Estado: Activo
Nivel: 04 - Diseno
Clasificacion: Decisiones Arquitectonicas

---

# Proposito

Registrar las decisiones arquitectonicas permanentes adoptadas durante el Nivel 04 del Proyecto Condor y las decisiones de evolucion registradas posteriormente mediante el ciclo de Evolucion Continua.

---

# DEC-001

Titulo:
Arquitectura por capas.

Decision:
El sistema se organizara en capas independientes con responsabilidades claramente definidas.

Estado:
Aceptada.

---

# DEC-002

Titulo:
Orquestador central.

Decision:
La coordinacion de todos los motores sera realizada exclusivamente por el Orquestador.

Estado:
Aceptada.

---

# DEC-003

Titulo:
Comunicacion mediante interfaces.

Decision:
Los componentes dependeran de contratos y nunca de implementaciones concretas.

Estado:
Aceptada.

---

# DEC-004

Titulo:
Motores desacoplados.

Decision:
Los motores especializados no podran comunicarse directamente entre si.

Estado:
Aceptada.

---

# DEC-005

Titulo:
Persistencia del conocimiento.

Decision:
Toda decision permanente debera registrarse mediante documentacion oficial.

Estado:
Aceptada.

---

# DEC-006

Titulo:
Evolucion incremental.

Decision:
La arquitectura permitira incorporar nuevos componentes sin redisenar el sistema completo.

Estado:
Aceptada.

---

# DEC-007

Titulo:
Tecnologia de implementacion del MVP 1.0.

Decision:
El MVP 1.0 se implementa con .NET 10 y C#, con proyectos separados para CLI, nucleo de dominio e infraestructura.

Esta es una decision de implementacion y no constituye una dependencia arquitectonica permanente.

Los contratos definidos en Condor.Core permanecen agnosticos a la tecnologia conforme a CONTRATOS_COMPONENTES.md.

Estado:
Aceptada.

Origen:
T-001 (Bootstrap del MVP y Assessment inicial).

---

# DEC-008

Titulo:
Estado local de Condor separado del conocimiento persistente del proyecto.

Decision:
El estado operativo local de Condor (por ejemplo, el resultado del Assessment) se almacena en el directorio de estado del usuario `%LOCALAPPDATA%\Condor\state\`.

Este estado local es transitorio y no constituye conocimiento persistente del proyecto.

La documentacion oficial en `Docs/` y `operacion/` constituye el conocimiento permanente y no puede ser reemplazada por estado local.

El mecanismo de almacenamiento es una propuesta de implementacion versionable y no un contrato arquitectonico congelado.

Estado:
Aceptada.

Origen:
T-001.

---

# DEC-009

Titulo:
Contrato AssessmentResult versionable.

Decision:
El resultado del Assessment se define mediante el esquema `AssessmentResult` version 1 en Condor.Core.

El esquema es versionable y evolucionara en el futuro sin considerarse inmutable.

La version del esquema se registra dentro del propio resultado serializado.

Estado:
Aceptada.

Origen:
T-001.

---

# DEC-010

Titulo:
Separacion de proyectos del MVP.

Decision:
El MVP se organiza en tres proyectos de codigo:

- `Condor.Cli`: entrada, interpretacion de argumentos y presentacion en terminal.
- `Condor.Core`: contratos, modelos de dominio y logica pura independiente de la plataforma.
- `Condor.Infrastructure`: implementaciones especificas de Windows (deteccion de hardware, herramientas y Ollama).

Las pruebas se organizan en proyectos separados: Unit (Condor.Core), Integration (Condor.Infrastructure) y Architecture (reglas de referencias).

Estado:
Aceptada.

Origen:
T-001.

---

# DEC-011

Titulo:
Idioma y convenciones en codigo.

Decision:
El codigo y los mensajes se ajustan estrictamente a las convenciones establecidas por las directivas de Condor:

- Idioma oficial: espanol latinoamericano.
- Los nombres tecnicos, identificadores, carpetas y archivos no utilizan tildes, acentos ni la letra n con tilde.
- Los mensajes de interfaz se presentan en espanol sin tildes, conforme a la directiva operativa.

No se incorporan reglas linguisticas adicionales.

Estado:
Aceptada.

Origen:
T-001.

---

# DEC-012

Titulo:
Correccion de referencias documentales.

Decision:
Se corrigen las referencias documentales de T-001 y AGENTE_CONDOR:

- `INTERFAZ_v2.md` se reemplaza por `Docs/07_Interfaz/INTERFAZ.md`.
- `EXPERIENCIA_USUARIO_v2.md` se reemplaza por `Docs/07_Interfaz/EXPERIENCIA_USUARIO.md`.
- `operacion/AGENTE_CONDOR.md` se reemplaza por la ubicacion real del contrato en la raiz del repositorio.

La correccion de ESTRUCTURA_REPOSITORIO.md queda registrada en DEUDA_EVOLUTIVA.md como deuda documental resuelta.

Estado:
Aceptada.

Origen:
T-001.

---

# Historial de Cambios

| Version | Cambio |
|---------|--------|
| 1.1.0 | Se incorporan DEC-007 a DEC-012 correspondientes a las decisiones aprobadas para T-001. |
| 1.0.0 | Primera version. |
