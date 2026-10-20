#!/usr/bin/env python3
"""
png_to_ansi.py
Convierte una imagen tipo pixel art (como la mascota de un CLI/TUI) en
arte Unicode coloreado con codigos ANSI truecolor (24-bit), listo para
imprimir en una terminal moderna (Windows Terminal, PowerShell 7, etc.)

Tecnica:
- Cada caracter de terminal es aprox. 2x mas alto que ancho.
- Se usa el caracter medio-bloque superior "▀" (U+2580).
- El color de "texto" (foreground) pinta la mitad superior del caracter.
- El color de "fondo" (background) pinta la mitad inferior.
- Asi, UN caracter representa DOS pixeles verticales -> se duplica
  la resolucion vertical aparente. Es la misma tecnica que usan
  varias mascotas de CLI hechas en pixel art (incluida la de Claude Code).

Uso:
    python png_to_ansi.py imagen.png --cell 36
    python png_to_ansi.py imagen.png --cols 8 --rows 8
    python png_to_ansi.py imagen.png --cell 36 --ps1 salida.ps1

Si no se indica --cell, el script intenta auto-detectar el tamano de
celda buscando los cambios de color en la primera fila de pixeles.
"""
import argparse
from PIL import Image

ESC = chr(27)


def detect_cell_size(img):
    """Autodetecta el tamano de celda (en px) buscando cambios de color."""
    w, h = img.size
    px = img.load()
    changes = []
    prev = px[0, 0]
    for x in range(w):
        c = px[x, 0]
        if c != prev:
            changes.append(x)
            prev = c
    if not changes:
        raise ValueError("No se detectaron cambios de color en la fila 0; "
                          "indica --cell manualmente.")
    # el primer cambio suele ser el tamano de celda (o un divisor comun)
    from math import gcd
    from functools import reduce
    g = reduce(gcd, changes) if len(changes) > 1 else changes[0]
    return g


def load_grid(path, cell=None):
    img = Image.open(path).convert("RGB")
    w, h = img.size
    if cell is None:
        cell = detect_cell_size(img)
    cols, rows = w // cell, h // cell
    px = img.load()
    grid = []
    for r in range(rows):
        row = []
        for c in range(cols):
            x = c * cell + cell // 2
            y = r * cell + cell // 2
            row.append(px[x, y])
        grid.append(row)
    return grid, cols, rows, cell


def grid_to_ansi_lines(grid, rows, cols):
    lines = []
    for i in range(0, rows, 2):
        top = grid[i]
        bot = grid[i + 1] if i + 1 < rows else grid[i]
        line = ""
        for c in range(cols):
            fg = top[c]
            bg = bot[c]
            line += (f"{ESC}[38;2;{fg[0]};{fg[1]};{fg[2]}m"
                      f"{ESC}[48;2;{bg[0]};{bg[1]};{bg[2]}m\u2580")
        line += f"{ESC}[0m"
        lines.append(line)
    return lines


def grid_to_powershell(grid, rows, cols, title="CONDOR"):
    """Genera un script .ps1 con Write-Host + escapes truecolor,
    en el mismo estilo usado en Condor CLI V16.

    IMPORTANTE: el caracter de medio bloque se construye en runtime con
    [char]0x2580 en vez de escribir el caracter Unicode literal en el
    archivo .ps1. Windows PowerShell 5.1 puede leer el archivo con la
    codepage ANSI/OEM del sistema en vez de UTF-8 si no detecta BOM,
    lo que corrompe cualquier caracter multibyte embebido directamente
    (aparece como texto tipo "a-,,,"). Generandolo con [char]0x2580 el
    .ps1 queda en ASCII puro y el resultado es identico sin importar
    la codepage con la que se abra el archivo.
    """
    out = []
    out.append("# Generado automaticamente por png_to_ansi.py")
    out.append("# Fuerza la salida a UTF-8 para que el caracter de bloque se imprima bien")
    out.append("# incluso en Windows PowerShell 5.1 con la consola clasica.")
    out.append("[Console]::OutputEncoding = [System.Text.Encoding]::UTF8")
    out.append("$e = [char]27")
    out.append("$block = [char]0x2580  # medio bloque superior, construido en runtime")
    out.append("")
    for i in range(0, rows, 2):
        top = grid[i]
        bot = grid[i + 1] if i + 1 < rows else grid[i]
        parts = []
        for c in range(cols):
            fg = top[c]
            bg = bot[c]
            parts.append(
                f'"$e[38;2;{fg[0]};{fg[1]};{fg[2]}m$e[48;2;{bg[0]};{bg[1]};{bg[2]}m$block"'
            )
        joined = " + ".join(parts) + f' + "$e[0m"'
        out.append(f"Write-Host ({joined}) -NoNewline")
        out.append('Write-Host ""')
    return "\n".join(out)


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                  formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("image")
    ap.add_argument("--cell", type=int, default=None,
                     help="Tamano de celda en pixeles (si se omite, se autodetecta)")
    ap.add_argument("--ans", default=None, help="Ruta de salida .ans (texto con escapes ANSI)")
    ap.add_argument("--ps1", default=None, help="Ruta de salida .ps1 (script PowerShell)")
    args = ap.parse_args()

    grid, cols, rows, cell = load_grid(args.image, args.cell)
    print(f"Grilla detectada: {cols}x{rows} (celda={cell}px)")

    lines = grid_to_ansi_lines(grid, rows, cols)
    text = "\n".join(lines)

    if args.ans:
        with open(args.ans, "w", encoding="utf-8") as f:
            f.write(text)
        print(f"ANSI guardado en {args.ans}")

    if args.ps1:
        ps1 = grid_to_powershell(grid, rows, cols)
        with open(args.ps1, "w", encoding="utf-8") as f:
            f.write(ps1)
        print(f"PowerShell guardado en {args.ps1}")

    if not args.ans and not args.ps1:
        print(text)


if __name__ == "__main__":
    main()
