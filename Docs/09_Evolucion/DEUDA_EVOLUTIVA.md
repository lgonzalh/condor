# DEUDA_EVOLUTIVA

Version: 1.3.0
Estado: Activo
Nivel: 09 - Evolucion
Clasificacion: Deuda Evolutiva

---

# PROPOSITO

Registrar las mejoras, decisiones pendientes y oportunidades de evolucion que no deben incorporarse inmediatamente al desarrollo principal, pero que conservan valor para futuras versiones del Proyecto Condor.

Este documento permite preservar estas oportunidades sin interrumpir la linea principal de trabajo.

---

# ALCANCE

Aplica a deuda identificada durante la evolucion del Proyecto Condor que:

- no constituye un bloqueo actual;
- requiere una version posterior;
- depende de decisiones futuras;
- necesita mayor evidencia antes de implementarse;
- puede esperar sin comprometer la continuidad.

La deuda arquitectonica de alcance general se mantiene en `REGISTRO_DEUDA_ARQUITECTONICA.md`.

---

# PRINCIPIOS

## No perdida

Toda deuda relevante debe registrarse antes de ser descartada de la linea principal.

## No bloqueo automatico

Registrar deuda no implica detener el desarrollo.

## Trazabilidad

Cada elemento debe conservar su origen, razon e impacto.

## Priorizacion

La deuda debe evaluarse segun su valor, riesgo y dependencia.

## Evolucion controlada

La resolucion debe realizarse mediante el ciclo normal de Condor.

## Limpieza

La deuda debe revisarse periodicamente para evitar acumulacion innecesaria.

---

# ESTADOS

- Pendiente
- En analisis
- Planificada
- En implementacion
- Resuelta
- Descartada
- Aceptada

---

# PRIORIDADES

## Critica

La deuda compromete la continuidad, integridad, seguridad o arquitectura si permanece sin tratamiento.

## Alta

La deuda genera un impacto importante y debe planificarse prioritariamente.

## Media

La deuda aporta una mejora relevante, pero no compromete la continuidad.

## Baja

La deuda representa una mejora conveniente que puede esperar.

---

# CRITERIOS DE REGISTRO

Cada elemento debera registrar:

- identificador;
- origen;
- componente o documento afectado;
- descripcion;
- razon;
- impacto;
- prioridad;
- estado;
- version objetivo.

Cuando sea necesario tambien debera registrar:

- dependencia;
- riesgo;
- alternativa;
- criterio de resolucion.

---

# REGISTRO

## DE-001

- Origen: Reconocimiento previo a T-001 (Agente Condor).
- Componente o documento afectado: `Docs/00_Fundamentos/ESTRUCTURA_REPOSITORIO.md`.
- Descripcion: El documento describia una estructura del repositorio (conocimiento/, codigo/, pruebas/, herramientas/, recursos/) que nunca existio en el historial del repositorio, en contradiccion con la estructura real (Docs/00-09, operacion/, Src/, Tests/, Assets/, Samples/, Scripts/).
- Razon: Discrepancia documental entre una fuente oficial y el estado real del repositorio.
- Impacto: Referencias inexistentes y orientacion incorrecta para agentes y personas.
- Prioridad: Alta.
- Estado: Resuelta.
- Version objetivo: 2.0.0 de ESTRUCTURA_REPOSITORIO.md.
- Resolucion: ESTRUCTURA_REPOSITORIO.md fue corregido a la version 2.0.0 describiendo la estructura vigente real. La correccion quedo registrada en el historial del propio documento y en DECISIONES.md (DEC-012).
- Fecha de resolucion: 2026-08-10, durante la ejecucion de T-001.

## DE-002

- Origen: Cierre de T-008 (Verificacion inicial).
- Componente o documento afectado: Verifier (T-008) y evoluciones posteriores.
- Descripcion: La verificacion semantica y de calidad (compilacion, ejecucion de pruebas, calidad del codigo, validacion arquitectonica y coherencia funcional del proyecto objetivo) quedo fuera del alcance v1.0 del Verifier y se reservo para evoluciones posteriores.
- Razon: Preservar la frontera inicial del Verifier (T-008 = comprobar que lo construido fue aplicado correctamente) sin contaminar su responsabilidad.
- Impacto: La v1.0 solo verifica integridad y acotacion; las capacidades semanticas quedan pendientes.
- Prioridad: Media.
- Estado: Parcialmente atendida (T-013).
- Version objetivo: Una evolucion posterior de Verifier; ya implementada en su primera parte (T-013).
- Avance: T-013 implemento la primera concrecion de la verificacion semantica: compilar y ejecutar pruebas del proyecto objetivo (`condor verificar-semantico`). Las capacidades de calidad, arquitectura y coherencia funcional permanecen como evolucion posterior (roadmap SD-02).
- Relacion: Se referencia en ROADMAP_EVOLUCION.md; la primera concrecion se implemento en T-013 (DEC-043/DEC-044).

---

# GESTION

La deuda evolutiva debera seguir el siguiente flujo:

Deteccion

↓

Registro

↓

Priorizacion

↓

Planificacion

↓

Implementacion

↓

Verificacion

↓

Documentacion

↓

Cierre

---

# RESOLUCION

Un elemento de deuda se considerara resuelto cuando:

1. la necesidad haya sido atendida;
2. el cambio haya sido implementado;
3. el resultado haya sido verificado;
4. la documentacion afectada haya sido actualizada;
5. la trazabilidad haya sido conservada.

---

# DESCARTE

Una deuda podra descartarse cuando:

- la necesidad haya desaparecido;
- exista una alternativa superior;
- el costo supere justificadamente su valor;
- una decision arquitectonica posterior la vuelva innecesaria.

El descarte debe conservar la razon de la decision.

---

# RELACION CON DEUDA ARQUITECTONICA

`REGISTRO_DEUDA_ARQUITECTONICA.md` constituye el registro oficial de deuda arquitectonica del proyecto.

Este documento se concentra en deuda propia del proceso evolutivo del Nivel 09.

Cuando un elemento de deuda evolutiva revele un impacto arquitectonico general, debera trasladarse o registrarse tambien en el registro arquitectonico correspondiente, conservando la trazabilidad.

---

# REVISION

La deuda evolutiva debera revisarse:

- al finalizar cada ciclo evolutivo relevante;
- antes de establecer una nueva linea base;
- durante las auditorias;
- al finalizar el Nivel 09.

Los elementos sin valor deberan ser descartados formalmente.

---

# REGLAS

1. No implementar deuda solo porque fue registrada.
2. No eliminar deuda sin conservar la razon del descarte.
3. No convertir deuda en bloqueo sin evaluar su impacto.
4. No duplicar innecesariamente deuda entre registros.
5. Toda deuda relevante debe tener identificador.
6. Toda resolucion debe conservar trazabilidad.
7. La deuda debe mantenerse comprensible para futuras etapas.
8. La acumulacion de deuda debe ser revisada periodicamente.
9. No crear entradas artificiales para completar el registro.
10. Un registro vacio es un estado valido cuando no existen deudas evolutivas identificadas.

---

# RELACION CON OTROS DOCUMENTOS

Este documento se relaciona principalmente con:

- EVOLUCION.md
- MEJORA_CONTINUA.md
- VERSIONADO.md
- MIGRACION.md
- COMPATIBILIDAD.md
- AUDITORIA.md
- REGISTRO_DEUDA_ARQUITECTONICA.md
- ESTADO_PROYECTO.md

---

# HISTORIAL DE CAMBIOS

| Version | Cambios |
|---------|---------|
| 1.3.0 | Se actualiza DE-002 a Parcialmente atendida tras T-013 (primera concrecion de la verificacion semantica: compilar y ejecutar pruebas). |
| 1.2.0 | Se incorpora DE-002 como deuda pendiente derivada del cierre de T-008: verificacion semantica y de calidad del Verifier reservada para una evolucion posterior (ver ROADMAP_EVOLUCION.md). T-009 queda formalmente congelada. |
| 1.1.0 | Se incorpora DE-001 como deuda documental resuelta durante la ejecucion de T-001 (correccion de ESTRUCTURA_REPOSITORIO.md). |
| 1.0.1 | Se elimina DE-001 por no corresponder a una deuda evolutiva real identificada. Se establece que el registro puede permanecer vacio y no deben crearse entradas artificiales. |
| 1.0.0 | Creacion del registro de deuda evolutiva del Proyecto Condor para el Nivel 09. |
