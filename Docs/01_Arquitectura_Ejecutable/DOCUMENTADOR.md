# DOCUMENTADOR

Version: 1.0.0 Estado: Especificado Nivel: 03 Clasificacion: Servicio

# Proposito

Definir el rol y la responsabilidad del Servicio Documenter del Proyecto Condor
(FN-009, ARQ-008). Este documento especifica QUIEN actualiza la documentacion
permanente y BAJO QUE criterios; NO redefine el flujo de desarrollo, que vive en
SISTEMA_DESARROLLO_CONDOR.md.

Responde la pregunta:

> ¿Como Condor mantiene su documentacion permanente coherente con el estado real?

# Rol

Documenter es el servicio responsable de que todo conocimiento permanente del
proyecto quede registrado en el documento propietario correcto, sea consistente
con el estado real y permita la continuidad sin depender de conversaciones.

El flujo general del desarrollo (Comprender -> Planificar -> Disenar -> Implementar
-> Verificar -> Documentar -> Congelar -> Continuar) es definido por
SISTEMA_DESARROLLO_CONDOR.md y no se repite en este documento.

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

- No implementar software ni motores; Documenter es documental.
- No modificar historial congelado de tareas completadas.
- Una unica fuente de verdad por decision (ESTRATEGIA_MEMORIA.md).
- Documentacion proporcional al cambio (DIRECTIVA_OPERATIVA_PROYECTO_CONDOR.md).
- Todo texto visible nuevo en espanol latinoamericano sin tildes ni acentos.

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

# Historial de cambios

  Version   Cambios
  --------- ---------------------------------------------------------
  1.0.0     Especificacion del rol y responsabilidad de Documenter
            (FN-009 / ARQ-008) tras T-009.
