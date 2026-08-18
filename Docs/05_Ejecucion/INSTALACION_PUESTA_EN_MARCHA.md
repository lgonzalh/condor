# INSTALACION Y PUESTA EN MARCHA

Version: 1.0.0
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

Acciones de Condor:

- Ninguna sobre la instalacion del software en esta version.

# Preparacion del entorno

Condor prepara el entorno mediante un diagnostico que no modifica tu sistema.

Acciones del usuario:

- Asegurar las dependencias externas que desees usar.

Acciones de Condor (automaticas):

- `condor preparar` lee el Assessment del entorno y diagnostica el estado de cada
  dependencia, sin descargar nada ni configurar el sistema.

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

- Detectar el estado de cada dependencia mediante el Assessment (`condor analizar`)
  y reportarlo en `condor preparar`.

# Modelos locales

En la puesta en marcha, Condor puede obtener automaticamente el modelo LLM local
compatible cuando es tecnicamente posible (Ollama utilizable, existe un modelo
compatible en el catalogo y el equipo tiene capacidad suficiente).

Acciones de Condor (automaticas, durante `condor preparar`):

- Evaluar el hardware y determinar la capacidad del equipo.
- Seleccionar un modelo compatible del catalogo.
- Comprobar el inventario de Ollama.
- Si el modelo deseado ya existe: reutilizarlo sin volver a descargarlo.
- Si no existe: obtenerlo mediante Ollama, con limite de tiempo, reintentos
  controlados y verificacion posterior de que quedo instalado.
- Continuar el flujo una vez el modelo este disponible.

Cuando la obtencion automatica no es posible (Ollama apagado/ausente, sin modelo
compatible, hardware insuficiente o descarga fallida tras reintentos), Condor
degrada de forma explicita y segura e indica el motivo.

Acciones manuales (alternativa/fallback):

- Instalar manualmente un modelo compatible (por ejemplo, usando
  `condor recomendar` para identificar uno adecuado).

# Configuracion inicial

La configuracion se organiza por nivel (sistema, proyecto, usuario y ejecucion).
La primera vez, Condor crea su directorio de estado local al realizar un analisis.

Acciones del usuario:

- Ejecutar `condor analizar` la primera vez para que Condor detecte el entorno.

Acciones de Condor (automaticas):

- Crear y mantener el estado local derivado en `%LOCALAPPDATA%\Condor\state\`.

# Verificacion

Acciones del usuario:

- Ejecutar `condor preparar` para comprobar que el entorno esta listo.

Acciones de Condor (automaticas):

- Verificar dependencias obligatorias y opcionales, estado del directorio local y
  disponibilidad general, con salida textual y `--json`.

# Puesta en marcha

Una vez listo, Condor opera mediante sus comandos:

- `condor analizar` - analiza el entorno.
- `condor contexto` - reconstruye el contexto del proyecto.
- `condor planear` - genera un plan de trabajo.
- `condor construir` - aplica los cambios del plan.
- `condor verificar` - comprueba los cambios aplicados.
- `condor avanzar` - ejecuta el ciclo de ingenieria parcial.
- `condor examinar` - analiza una imagen con un VLM local.
- `condor recomendar` - recomienda un modelo local.
- `condor consultar` - consulta al modelo local.

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
  1.0.0     Corte de la guia de instalacion y puesta en marcha (T-012).
