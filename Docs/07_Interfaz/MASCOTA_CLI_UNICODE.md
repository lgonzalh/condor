# MASCOTA_CLI_UNICODE

Version: 1.0.0
Estado: En desarrollo
Nivel: 07 - Interfaz
Clasificacion: Especificacion de interfaz y recurso terminal

---

## Proposito

Documentar la construccion de la mascota de trabajo de Condor para la
interfaz CLI de Windows, preservando el conocimiento obtenido durante las
pruebas V1-V16.

La mascota no es una imagen raster ni un recurso grafico externo.

Es arte Unicode de terminal construido mediante una matriz de caracteres
monoespaciados y colores ANSI.

---

## Decisiones aprobadas

### 1. Una sola fuente (dos presencias)

(α.03) Condor utiliza dos presencias de una UNICA fuente ANSI original
(`Docs/07_Interfaz/Mockups/condor_unicode_v16.ps1`, paleta 232/242/167/97):

- Condor **Grande**: bienvenida e inicio. Es el 100% de la fuente ANSI original,
  tal cual: caracteres ANSI, filas, espacios, bloques y secuencias 256 (232
  cuerpo, 242 sombreado, 167 cabeza, 97 blanco) y sus resets. No deriva de SVG.
  Invariante «no se escala / no se redibuja» preservado.
- Condor **Ave pequeña**: mascota de trabajo durante la sesión. Es una
  **adaptación gráfica compacta de la misma ave** (α.03), no un downscale ciego:
  la reducción 50% (`Scale50`) perdía patas/garras y deformaba el pico. La
  pequeña se construye con una matriz propia reducida (`CondorArt.PequenaMatrix`)
  que conserva la identidad cromática y la anatomía (cabeza terracota, punta de
  pico blanca, cuerpo gris, sombreado, ala/cola) y AÑADE patas y garras visibles.
  No es un segundo diseño ni una segunda identidad: misma paleta y misma ave.

**Interpretación de la fuente (T-018/auditoría α.02, ajustada en α.03).** El ANSI
original (`condor_unicode_v16.ps1`) es un cóndor alado en perfil: representa cabeza,
cuerpo, alas y cola; **no** representa patas ni garras como elementos distintos del
cuerpo. Conforme a la regla de «no redibujar / no sustituir», la mascota **grande**
conserva exactamente esa representación ANSI. En α.03 la mascota **pequeña** sí se
adapta a tamaño reducido con patas y garras (requisito explícito de α.03), manteniendo
la misma identidad cromática y anatómica.

### 2. Medio de representacion

La representacion oficial de la mascota de trabajo para v1.x es:

`Unicode Block Art + ANSI`

No se utiliza:

- imagen raster;
- SVG;
- HTML;
- canvas;
- renderer externo;
- ASCII basado exclusivamente en caracteres `#`, `@` o similares;
- dependencia de una fuente grafica especial.

### 3. Terminal objetivo

Version 1.x:

- Windows.
- Terminal monoespaciada.
- Fondo oscuro.
- Operacion mediante teclado.
- Ejecucion local.

Esto es coherente con las restricciones del Nivel 07 y con el criterio de
accesibilidad documentado para la interfaz. 

---

## Tecnologia de produccion

La implementacion real de Condor utiliza C#/.NET. La evidencia del proyecto
incluye compilacion de `Condor.slnx` y pruebas sobre el binario real.

Existe tambien `condor_cli.py`, pero ese archivo se documenta como
**prototipo visual** y no como implementacion definitiva del CLI.

Por tanto:

`Python -> prototipado visual`

`C#/.NET -> implementacion del producto`

La mascota debe integrarse finalmente en el componente de presentacion CLI
de C#/.NET y no convertirse en una dependencia del prototipo Python.

---

## Tecnica de construccion

La mascota se construye como una matriz logica.

Cada celda representa:

- caracter Unicode;
- color de primer plano;
- opcionalmente estado vacio.

Modelo conceptual:

```text
Celda
 ├── glyph
 ├── foreground
 └── visible
```

La salida de cada fila se escribe directamente al terminal.

No existe un proceso de renderizado de imagen.

---

## Unidad grafica

La unidad visual elegida es el bloque Unicode.

El caracter ocupa una posicion de la rejilla de terminal y mantiene una
relacion estable con la fuente monoespaciada.

Esto permite:

- controlar la silueta por celda;
- mantener el dibujo reproducible;
- modificar regiones concretas sin redibujar toda la mascota;
- conservar la misma mascota entre terminales compatibles.

---

## Paleta de Condor Ave

La V16 establece la siguiente distribucion conceptual:

| Region | Representacion |
|---|---|
| Alas | negro / casi negro |
| Cuerpo | gris oscuro |
| Cuello | blanco completo |
| Cabeza | rojo |
| Pico | rojo dentro de la cabeza / separacion visual |
| Patas y garras | gris oscuro / region de contraste |

La paleta se mantiene deliberadamente pequena.

El color no debe ser el unico mecanismo para reconocer la anatomia.

La silueta debe continuar siendo reconocible sin color.

---

## Geometria aprobada

La V15 establecio la proporcion de referencia.

La V16 corrige exclusivamente:

1. reduccion de anchura;
2. mayor presencia de pixeles negros;
3. cuello completamente blanco.

No se debe volver a alterar la escala general como consecuencia de
pequenas correcciones anatomicas.

---

## Regla de proporcion

La matriz debe conservar una relacion de aspecto estable.

No se debe:

- comprimir horizontalmente una imagen ya terminada;
- estirar verticalmente;
- aplicar escalado no entero;
- adaptar la mascota mediante transformaciones graficas.

La geometria se modifica directamente en la matriz.

---

## Regla de anatomia

La lectura visual sigue esta jerarquia:

1. silueta de ave;
2. alas;
3. cabeza;
4. cuello;
5. pico;
6. cuerpo;
7. patas/garras.

Si una modificacion mejora un detalle pero deteriora la lectura de ave, la
modificacion debe rechazarse.

---

## Construccion en C#/.NET

La integracion final debe seguir este patron:

```csharp
public sealed record CondorPixel(
    char Glyph,
    ConsoleColor Color
);

public static class CondorAveArt
{
    public static readonly CondorPixel[][] Art =
    [
        // Matriz Unicode V16.
        // Cada fila conserva el ancho de la mascota.
    ];
}
```

La capa de presentacion convierte cada `CondorPixel` en salida de consola.

Ejemplo conceptual:

```csharp
foreach (var row in CondorAveArt.Art)
{
    foreach (var pixel in row)
    {
        Console.ForegroundColor = pixel.Color;
        Console.Write(pixel.Glyph);
    }

    Console.ResetColor();
    Console.WriteLine();
}
```

La matriz es el recurso de identidad.

La rutina de consola solamente la presenta.

Esto mantiene separadas:

- identidad visual;
- datos de la mascota;
- mecanismo de salida.

---

## No hacer

La implementacion no debe introducir:

- imagenes externas;
- archivos PNG/JPG como dependencia de ejecucion;
- conversion automatica de imagen a caracteres en tiempo de ejecucion;
- dependencias de UI grafica;
- fuentes externas obligatorias;
- calculos de escalado de la mascota;
- logica de negocio dentro del componente visual.

---

## Estados de uso

### Inicio

Se utiliza Condor Grande.

Objetivo:

- bienvenida;
- identidad;
- inicio de la experiencia.

### Trabajo

Se utiliza Condor Ave.

Objetivo:

- acompanamiento;
- estado;
- actividad;
- progreso;
- continuidad.

La mascota pequena puede permanecer visible en estados de trabajo sin competir
con la informacion principal de la CLI.

---

## Evolucion V1-V16

Las iteraciones no fueron desperdicio.

Constituyen evidencia de diseño.

### V1-V3

Se exploro una representacion de condor grande mediante bloques de
diferentes tamanos.

Problema:

La silueta resultaba mas cercana a una forma abstracta que a un ave.

### V4-V6

Se aumento el detalle y se intento aproximar un condor real.

Problema:

El exceso de detalle no aportaba reconocimiento dentro de la terminal.

### V7-V9

Se intento reducir la mascota a una cabeza o ave muy simple.

Problema:

La cabeza aislada no comunicaba suficientemente la idea de ave.

### V10-V11

Se adopto Unicode Block Art como tecnica de representacion.

Resultado:

La silueta comenzo a funcionar como figura terminal real.

### V12-V14

Se trabajo la proporcion y la lectura de la silueta.

### V15

Se establecio la proporcion de referencia.

Tambien se resolvio la necesidad de representar el cuello blanco sin
interpretarlo como transparencia.

### V16

Se consolida:

- proporcion mas angosta;
- cuerpo en negro/gris muy oscuro;
- cuello completamente blanco;
- representacion nativa de terminal.

V16 es la referencia actual de integracion.

---

## Criterios de aceptacion

La mascota se considera lista para integracion cuando:

- se reconoce como ave sin depender del color;
- mantiene la proporcion V16;
- el cuello blanco es completamente visible;
- el cuerpo conserva el contraste oscuro;
- no requiere imagen externa;
- funciona en Windows Terminal;
- se imprime correctamente con una fuente monoespaciada;
- puede incorporarse al CLI C#/.NET como recurso determinista;
- puede ser sustituida por una version futura sin modificar la logica de
  negocio.

---

## Trazabilidad

Fuentes de diseño:

- Mockups del flujo CLI de Condor.
- Prototipos ejecutables de mascota V1-V16.
- `condor_cli.py` como prototipo visual.
- Documentacion del Nivel 07.
- `ACCESIBILIDAD_v2.md`.
- `COMPONENTES_UI_v2.md`.
- `VALIDACION_INTERFAZ_v2.md`.

La mascota queda documentada como componente de identidad visual del Nivel 07.

---

## Historial

| Version | Cambio |
|---|---|
| 1.0.0 | Documentacion inicial de la mascota Unicode y su integracion prevista en C#/.NET. |
