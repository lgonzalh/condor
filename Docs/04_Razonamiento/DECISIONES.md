# DECISIONES

Version: 1.2.0
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

# DEC-013

Titulo:
Comando condor ask para inferencia local.

Decision:
T-002 introduce el comando:

`condor ask "<mensaje>" [--model <modelo>]`

para ejecutar una inferencia local mediante Ollama, coherente con la interfaz de terminal de Condor 1.0.

El comando es minimo y no interactivo; los flujos conversacionales completos pertenecen a tareas posteriores.

Estado:
Aceptada.

Origen:
T-002.

---

# DEC-014

Titulo:
Extension aditiva de IStateStore con lectura del Assessment.

Decision:
Se agrega de forma aditiva `LoadAssessmentAsync` al contrato `IStateStore` para permitir consumir el resultado del Assessment persistido.

El metodo de escritura existente (`SaveAssessmentAsync`) no se modifica.

La extension es compatible con el esquema `AssessmentResult` 1.0.0 (DEC-009) y no constituye un cambio de contrato destructivo.

Estado:
Aceptada.

Origen:
T-002.

---

# DEC-015

Titulo:
Estrategia provisional de seleccion de modelo.

Decision:
En T-002 el modelo se selecciona asi:

1. El modelo explicito indicado con `--model` si fue proporcionado.
2. En caso contrario, temporalmente, el primer modelo disponible en el Assessment.

Esta estrategia es provisional. T-003 (Recomendador de modelos) sera responsable de la recomendacion inteligente basada en hardware, capacidades y modelos disponibles.

Estado:
Aceptada.

Origen:
T-002.

---

# DEC-016

Titulo:
Comunicacion con Ollama mediante /api/chat local sin streaming.

Decision:
La integracion con Ollama usa el endpoint local `http://127.0.0.1:11434/api/chat` con `stream: false`.

La comunicacion es exclusivamente loopback (127.0.0.1) y no introduce dependencias externas ni servicios cloud.

El streaming queda fuera de T-002 y se evaluara cuando los motores posteriores (Builder, Verifier) lo requieran.

Estado:
Aceptada.

Origen:
T-002.

---

# DEC-017

Titulo:
Tarea T-002 y actualizacion de artefactos operativos.

Decision:
T-002 se formaliza mediante la creacion de `operacion/TAREAS/T-002.md` y la actualizacion de los artefactos operativos correspondientes (ESTADO_DESARROLLO, BACKLOG, KANBAN, REGISTRO_CAMBIOS, RELEVO e INVENTARIO_ARQUITECTURA) conforme al contrato AGENTE_CONDOR.md.

Estado:
Aceptada.

Origen:
T-002.

---

# DEC-018

Titulo:
Rama de trabajo de T-002.

Decision:
La implementacion de T-002 se realiza exclusivamente en la rama `feature/T-002-ollama` creada desde `main`.

La integracion a `main` se realizara posteriormente mediante revision y merge, sin eliminar la rama.

Estado:
Aceptada.

Origen:
T-002.

---

# Historial de Cambios

| Version | Cambio |
|---------|--------|
| 1.2.0 | Se incorporan DEC-013 a DEC-018 correspondientes a las decisiones aprobadas para T-002. |
| 1.1.0 | Se incorporan DEC-007 a DEC-012 correspondientes a las decisiones aprobadas para T-001. |
| 1.0.0 | Primera version. |
