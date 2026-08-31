# INSTALACION Y PUESTA EN MARCHA

Version: 1.2.0
Estado: Activo
Nivel: 05 - Implementacion
Clasificacion: Guia de puesta en marcha

# Proposito

Explicar como instalar, preparar y poner en marcha Condor 1.0, distinguiendo
claramente las acciones que realiza Condor automaticamente de las acciones que
debe realizar manualmente el usuario.

# Instalacion de Condor

Condor 1.0 se distribuye como aplicacion de linea de comandos (.NET). Para instalarlo
debes tener disponible el runtime de .NET requerido por la distribucion.

Acciones del usuario:

- Instalar manualmente el runtime de .NET necesario si no esta presente.
- Descargar o compilar la distribucion de Condor segun corresponda.
- (Opcional) Exponer el comando `condor` en el PATH para invocarlo desde
  cualquier terminal: agregar la carpeta `%USERPROFILE%\.condor\bin` al PATH
  del usuario (la primera ejecucion prepara automaticamente la CLI).

Acciones de Condor:

- En la primera ejecucion, preparar automaticamente su CLI publicada
  (`condor.exe`) dentro de `.artifacts\condor` del proyecto y continuar.
- Ninguna otra sobre la instalacion del software del sistema en esta version.

# Como iniciar Condor

Desde una terminal, escribe:

```
condor
```

y Cóndor prepara automaticamente el entorno y abre una sesion interactiva
(lista para tu intencion). No muestra una lista rigida de comandos como si esa
lista definiera su capacidad.

No es necesario conocer rutas internas, parametros tecnicos, seleccion de
modelos, fases internas ni herramientas. Escribe directamente lo que necesitas:

```
> revisa por que no compila este proyecto
> continua el desarrollo de esta aplicacion
> crea una pagina web sencilla para este proyecto
```

Cada frase que no comienza con "/" se entrega al motor agente. Tambien puedes
usar una sola linea:

- `condor "revisa por que no compila este proyecto"`
- `condor "continua el desarrollo de esta aplicacion"`

Los comandos de control explicitos (diagnostico) usan "/":

- `condor /analizar`      Analiza el proyecto o directorio actual.
- `condor /contexto`      Reconstruye el contexto del proyecto.
- `condor /planear "<solicitud>"`
- `condor /construir` / `/verificar`
- `condor /recomendar`    Recomienda un modelo para el equipo.
- `condor /ayuda`         Muestra los comandos de control.
- `condor /salir`         Termina la sesion interactiva.

`/analizar` analiza el proyecto/directorio (estructura, contenido y senales).
El analisis de hardware, RAM, almacenamiento, GPU, Ollama y modelos forma parte
de la preparacion automatica que Condor realiza al iniciar; no pertenece a
ningun comando del usuario.

# Preparacion del entorno

Condor prepara el entorno de forma automatica y silenciosa al iniciar; no exige
que el usuario ejecute preparativos manuales para una tarea normal.

Acciones del usuario:

- Asegurar las dependencias externas que desees usar.

Acciones de Condor (automaticas, al iniciar):

- Detectar hardware, RAM libre, almacenamiento, GPU, Ollama y modelos.
- Calcular el presupuesto seguro y seleccionar el modelo mas adecuado para la
  tarea/equipo, reutilizarlo o instalarlo si es viable.
- Dejar el entorno preparado. Solo informa informacion relevante, errores o
  decisiones que requieran intervencion del usuario.

# Dependencias externas

Clasificacion de las dependencias:

## Obligatorias

- Condiciones necesarias para ejecutar Condor (por ejemplo, el runtime de .NET
  requerido por la distribucion).

## Opcionales

- Ollama (para modelos locales).
- Modelos locales.
- GPU (amplia capacidades).
- Git.
- Herramientas de desarrollo.

Acciones del usuario:

- Instalar manualmente Ollama, modelos, Git o herramientas si deseas usarlas.

Acciones de Condor (automaticas):

- Detectar el estado de cada dependencia durante la preparacion automatica del
  arranque y usarlo internamente para decidir capacidades y seleccion de modelo.

# Modelos locales

En la puesta en marcha, Condor selecciona el modelo LLM local mas adecuado dentro
de un presupuesto seguro de recursos y lo obtiene automaticamente cuando es
tecnicamente posible (Ollama utilizable, existe una variante plausible en el
catalogo, el equipo tiene presupuesto seguro de RAM y disco, y la capacidad de
ingenieria cubre la tarea).

Acciones de Condor (automaticas, durante la preparacion del arranque):

- Evaluar el hardware (RAM total/libre, disco, GPU) y calcular el presupuesto
  seguro: `RAM libre - reservaSistema - reservaCondor - margenOperativo`
  (cada una con piso ~1.5 GB). Nunca se usa un porcentaje de RAM total si la
  RAM libre real es menor, y el presupuesto nunca supera la RAM libre.
- Descartar preventivamente modelos cuyo pico de memoria (peso + KV/contexto +
  overhead) supera el presupuesto seguro, aunque ya esten instalados.
- Seleccionar el modelo de maxima capacidad de ingenieria viable dentro del
  presupuesto (parametros, cuantizacion, contexto, capacidades de codigo,
  structured output, tool use), no "el mas pequeno que cabe" ni "el mas potente".
- Comprobar el inventario de Ollama.
- Si el modelo deseado ya existe: reutilizarlo sin volver a descargarlo.
- Si el deseado no existe y una alternativa instalada tiene capacidad equivalente
  o mayor: reutilizarla.
- Si no existe: obtenerlo mediante Ollama, con limite de tiempo, reintentos
  controlados y verificacion posterior de que quedo instalado.
- Continuar el flujo una vez el modelo este disponible.

Cuando la obtencion automatica no es posible (Ollama apagado/ausente, sin variante
compatible, presupuesto seguro insuficiente o descarga fallida tras reintentos),
Condor degrada de forma explicita y segura e indica el motivo, sin forza un modelo
inviable ni declarar exito sin evidencia.

Acciones manuales (alternativa/fallback):

- Instalar manualmente un modelo compatible (por ejemplo, usando
  `condor /recomendar` para identificar uno adecuado).

# Configuracion inicial

La configuracion se organiza por nivel (sistema, proyecto, usuario y ejecucion).
La primera vez, Condor crea su directorio de estado local al realizar un analisis.

Acciones del usuario:

- Ninguna: Condor prepara el entorno automaticamente al iniciar.

Acciones de Condor (automaticas):

- Crear y mantener el estado local derivado en `%LOCALAPPDATA%\Condor\state\`.

# Verificacion

Acciones del usuario:

- `condor /preparar` si deseas forzar un reinicio de la preparacion o ver el
  diagnostico detallado. No es necesario para usar Condor normalmente.

Acciones de Condor (automaticas):

- Verificar dependencias obligatorias y opcionales, estado del directorio local y
  disponibilidad general, con salida textual y `--json`.

# Puesta en marcha

Una vez listo, Condor opera por intencion libre: el usuario expresa con palabras
lo que necesita y Condor comprende, analiza, selecciona modelo y estrategia,
actua con herramientas reales, verifica externamente y entrega el resultado.

Los comandos de control (diagnostico) usan "/":

- `condor /analizar` - analiza el proyecto o directorio actual.
- `condor /contexto` - reconstruye el contexto del proyecto.
- `condor /planear` - genera un plan de trabajo.
- `condor /construir` - aplica los cambios del plan.
- `condor /verificar` - comprueba los cambios aplicados.
- `condor /avanzar` - ejecuta el ciclo de ingenieria parcial.
- `condor /examinar` - analiza una imagen con un VLM local.
- `condor /recomendar` - recomienda un modelo local.
- `condor /consultar` - consulta al modelo local.

Estos comandos son herramientas de control del usuario y NO representan la
totalidad de las capacidades de Condor. La via principal es la intencion libre:
cualquier frase que no comienza con "/" pasa directamente al motor agente.

# Notas sobre automatizacion

Condor automatiza la puesta en marcha: detecta hardware, selecciona y obtiene el
modelo LLM local cuando es tecnicamente posible, y continuua el flujo sin exigir
pasos manuales innecesarios.

Aquellas acciones que requieren instalar software del sistema (p. ej. .NET u
Ollama) o descargar manualmente un modelo cuando la obtencion automatica no es
posible quedan delegadas al usuario y se indican explicitamente como acciones
manuales.

# Historial de cambios

  Version   Cambios
  --------- ---------------------------------------------------------
  1.2.0     Separacion arquitectonica: preparacion automatica al iniciar,
            comandos de control con "/" y via principal de intencion libre
            al motor agente. Incluye el hardcodeo funcional del motor:
            edicion quirurgica (patch), harness externo real
            build/test/restore, recuperacion (undo_file) y guarda
            anti-falsos-positivos (T-016; E2E en proyectos .NET).
  1.1.0     Se documenta el punto de entrada de usuario final (`condor`)
            y la exposicion del comando en el PATH.
  1.0.0     Corte de la guia de instalacion y puesta en marcha (T-012).
