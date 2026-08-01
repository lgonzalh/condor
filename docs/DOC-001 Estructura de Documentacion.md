# 01 - Estructura de Documentacion

> **Documento:** DOC-001  
> **Proyecto:** Cóndor  
> **Version:** 1.0  
> **Estado:** Borrador

---

## Proposito

Definir la organizacion, estructura y estandares de la documentacion oficial del proyecto.

---

## Principios

- Cada documento tiene una unica responsabilidad.
- La informacion existe en un unico lugar.
- La documentacion evoluciona junto con el proyecto.
- Todo documento debe aportar valor.
- El conocimiento debe localizarse facilmente.

---

## Estructura Documental

| Documento | Identificador | Pregunta que responde |
|-----------|---------------|-----------------------|
| 00 - Acta de Constitucion | DOC-000 | ¿Por que existe Cóndor? |
| 01 - Estructura de Documentacion | DOC-001 | ¿Como se organiza la documentacion? |
| 02 - Resumen de Arquitectura | DOC-002 | ¿Como esta concebida la arquitectura? |
| 03 - Componentes del Sistema | DOC-003 | ¿Cuales son los componentes principales? |
| 04 - Pila Tecnologica | DOC-004 | ¿Con que tecnologias se construye? |
| 05 - Principios de Desarrollo | DOC-005 | ¿Como se desarrolla el proyecto? |
| 06 - Estructura del Proyecto | DOC-006 | ¿Como se organiza el codigo? |
| 07 - Registro de Decisiones Arquitectonicas | DOC-007 | ¿Como se registran las decisiones arquitectonicas? |
| 08 - Flujo de Desarrollo | DOC-008 | ¿Como evoluciona el proyecto? |
| 09 - Glosario | DOC-009 | ¿Que significa cada termino? |

---

## Estandar Documental

Todos los documentos deberan:

- Utilizar Markdown como formato oficial.
- Mantener una estructura uniforme.
- Tener un unico proposito.
- Ser independientes.
- Permanecer consistentes con el Acta de Constitucion.

---

## Convenciones

### Encabezado

Todos los documentos utilizaran el siguiente formato:

```text
Documento
Proyecto
Version
Estado
```

### Titulos

Se utilizaran unicamente tres niveles:

```text
#
##
###
```

### Separadores

```text
---
```

### Codigo

Todo bloque de codigo debera indicar el lenguaje correspondiente.

---

## Estados

| Estado | Descripcion |
|---------|-------------|
| Borrador | En elaboracion |
| En Revision | En revision |
| Aprobado | Aprobado |
| Obsoleto | Obsoleto |

---

## Definicion de Terminado

Un documento se considera terminado cuando:

- Cumple una unica responsabilidad.
- Sigue el estandar documental.
- Es consistente con el Acta de Constitucion.
- Puede comprenderse en pocos minutos.
- Esta listo para ser versionado en Git.

---

## Referencias

- DOC-000 · Acta de Constitucion