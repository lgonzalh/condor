# DOCUMENTADOR

Version: 1.1.0 Estado: Activo Nivel: 03 Clasificacion: Servicio

# Proposito

Definir el rol y la responsabilidad del Servicio Documenter del Proyecto Condor
(FN-009, ARQ-008), y los criterios para mantener la documentacion permanente.
Este documento no redefine el flujo de desarrollo, que vive en
SISTEMA_DESARROLLO_CONDOR.md. El contrato tecnico del componente implementado se
mantiene en DOCUMENTER.md.

Responde la pregunta:

> ¿Como Condor mantiene su documentacion permanente coherente con el estado real?

# Rol

Documenter es el servicio responsable de que todo conocimiento permanente del
proyecto quede registrado en el documento propietario correcto, sea consistente
con el estado real y permita la continuidad sin depender de conversaciones.

El flujo general del desarrollo (Comprender -> Planificar -> Disenar -> Implementar
-> Verificar -> Documentar -> Congelar -> Continuar) es definido por
SISTEMA_DESARROLLO_CONDOR.md y no se repite en este documento.

# Implementacion vigente

Desde T-021, `IDocumenter` genera un informe UTF-8 nuevo para cada ejecucion de
agente en lenguaje natural y cada ciclo `/avanzar`, bajo
`docs/condor/tareas/`. Cada informe separa `Antes`, `Durante` y `Despues`, y
registra solamente la intencion, los pasos o etapas observados, el estado final
y las limitaciones disponibles. El resultado devuelve la ruta del informe; si
la escritura falla, informa ese fallo sin ocultar ni alterar el resultado de la
tarea.

El informe registra la ejecucion y aporta trazabilidad permanente. No sustituye
la actualizacion semantica del documento propietario: Condor no elige ni edita
un documento oficial por inferencia cuando la tarea no lo identifica.

# Responsabilidades

- Registrar las decisiones permanentes en su documento propietario (DECISIONES.md
  y directivas).
- Mantener los inventarios (arquitectonico, funcional, de proyecto) sincronizados
  con el estado real.
- Actualizar los artefactos operativos de continuidad (ESTADO_DESARROLLO, RELEVO,
  BACKLOG, KANBAN, REGISTRO_CAMBIOS) al cerrar cada ciclo.
- Preservar la documentacion historica de las tareas congeladas sin reescribir su
  contenido.
- Separar documentacion historica, estado actual, decisiones, deuda y siguiente
  linea de desarrollo.
- Consolidar la deuda pendiente en DEUDA_EVOLUTIVA.md y la siguiente linea en
  ROADMAP_EVOLUCION.md, sin duplicar contenido entre ellos.

# Reglas

- La politica no implementa software ni motores; el contrato tecnico de la
  implementacion esta en DOCUMENTER.md.
- No modificar historial congelado de tareas completadas.
- Una unica fuente de verdad por decision (ESTRATEGIA_MEMORIA.md).
- Documentacion proporcional al cambio (DIRECTIVA_OPERATIVA_PROYECTO_CONDOR.md).
- Todo texto visible nuevo en espanol latinoamericano sin tildes ni acentos.
- El informe de tarea registra el resultado observado y no afirma pruebas que no se ejecutaron.

# Relaciones

- Coordina con el Kernel (KERNEL_CONDOR.md) en la etapa Documentar del ciclo.
- Depende del estado real producido por Planner, Builder y Verifier (T-006, T-007,
  T-008) y de la configuracion (ESTADO_PROYECTO.md).
- Referencia SISTEMA_DESARROLLO_CONDOR.md como fuente del flujo oficial.

# Criterios de aceptacion

- La documentacion refleja el estado real del codigo y de las decisiones.
- Todo conocimiento permanente es trazable a un documento propietario.
- La continuidad es posible sin recuperar conversaciones.
- No existen duplicidades catalogadas entre documentos.
- Las rutas de agente y `/avanzar` generan informes `Antes`/`Durante`/`Despues`
  sin proveedor externo y sin sobreescribir informes previos.
- El fallo documental se devuelve de forma visible y diferenciada del resultado
  de la tarea.

# Historial de cambios

  Version   Cambios
  --------- ---------------------------------------------------------
  1.1.0     T-021 implementa informes Antes/Durante/Despues para el agente y
            `/avanzar`; el informe no reemplaza las actualizaciones semanticas
            de documentos propietarios.
  1.0.0     Especificacion del rol y responsabilidad de Documenter
            (FN-009 / ARQ-008) tras T-009.
