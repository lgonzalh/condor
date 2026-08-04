# Cóndor

<img width="1095" height="547" alt="condor" src="https://github.com/user-attachments/assets/64de2b9c-a3ac-4bbd-ba0d-cd67a8c404e7" />

> **Observa · Comprende · Planifica · Construye · Verifica**

Cóndor es una plataforma de ingenieria de software asistida por inteligencia artificial que prioriza el conocimiento del proyecto antes que la generacion de codigo.

Su objetivo es preservar el contexto, mantener la coherencia arquitectonica y convertir cada decision en conocimiento permanente mediante documentacion viva, trazabilidad y desarrollo guiado por arquitectura.

En Condor la conversacion es temporal. La documentacion constituye la fuente oficial de verdad.

---

# Principios

- Arquitectura primero.
- El conocimiento dirige el desarrollo.
- Documentacion como parte de la ingenieria.
- Decisiones tecnicas trazables.
- Simplicidad por diseno.
- Evolucion continua.
- Una unica fuente de verdad.
- Minima friccion para el desarrollador.

---

# Filosofia

- Antes de escribir codigo -> Comprender.
- Antes de implementar -> Planificar.
- Antes de modificar -> Analizar.
- Antes de avanzar -> Validar.

- Antes de finalizar -> Documentar.

---

# Objetivos

- Preservar el contexto del proyecto.
- Mantener la coherencia arquitectonica.
- Reducir la repeticion y la perdida de conocimiento.
- Guiar el desarrollo mediante documentacion estructurada.
- Facilitar la continuidad entre sesiones, herramientas y modelos.
- Permitir que cualquier desarrollador pueda continuar un proyecto sin depender de conversaciones anteriores.

---

# Arquitectura documental

Toda decision relevante se transforma en un documento permanente.

Los documentos principales del proyecto son:

```text
ESTADO_PROYECTO.md
CONDOR_CONTEXTO_MAESTRO.md
DIRECTIVA_GLOBAL.md
ESTANDAR_DOCUMENTAL.md
```

Estos documentos constituyen la base operativa y documental del proyecto.

---

## Estructura del repositorio

```text
condor/
│
├── ESTADO_PROYECTO.md
├── README.md
├── LICENSE
├── NOTICE
│
├── Docs/
│   ├── 00_Fundamentos/
│   ├── 01_Arquitectura_Ejecutable/
│   ├── 02_Memoria/
│   ├── 03_Planificacion/
│   ├── 04_Razonamiento/
│   ├── 05_Ejecucion/
│   ├── 06_Herramientas/
│   ├── 07_Interfaz/
│   ├── 08_Calidad/
│   └── 09_Evolucion/
│
├── resources/
├── scripts/
├── src/
├── tests/
└── tools/
```

---

# Flujo de trabajo

```text
Comprender -> Planificar -> Disenar -> Implementar -> Verificar -> Documentar -> Congelar -> Continuar
```

---

## Estado del proyecto

Consulta el estado actualizado del proyecto en [ESTADO_PROYECTO.md](ESTADO_PROYECTO.md).

---

## Documentacion

La documentacion oficial del proyecto se organiza por niveles dentro de la carpeta `Docs`.

Los documentos principales para comprender el proyecto son:

```text
ESTADO_PROYECTO.md
Docs/
├── 00_Fundamentos/
│   ├── CONDOR_CONTEXTO_MAESTRO.md
│   ├── DIRECTIVA_GLOBAL.md
│   ├── ESTANDAR_DOCUMENTAL.md
│   └── ...
└── 01_Arquitectura_Ejecutable/
    ├── KERNEL_CONDOR.md
    ├── CONTRATO_KERNEL.md
    └── ...
```

### Punto de inicio

El orden recomendado de lectura es:

1. `ESTADO_PROYECTO.md`
2. `Docs/00_Fundamentos/`
3. `Docs/01_Arquitectura_Ejecutable/`
4. Nivel activo del proyecto.
---

# Vision

Condor busca convertirse en un motor de ingenieria capaz de comprender un proyecto completo antes de modificarlo, preservando el conocimiento, la arquitectura y la continuidad durante todo el ciclo de vida del software.

---

## Licencia

Actualmente el Proyecto Cóndor se publica bajo el modelo **All Rights Reserved**.

El repositorio es público con fines de transparencia, documentación y colaboración futura.

La disponibilidad pública del código fuente **no concede permisos** para copiar, modificar, redistribuir, crear trabajos derivados o explotar comercialmente el proyecto sin autorización expresa del titular de los derechos.

Consulte el archivo `LICENSE` para más información.

