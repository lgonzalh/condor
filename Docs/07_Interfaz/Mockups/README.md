# Condor CLI — Ave Trabajo V16

Evolución directa de V15. No cambia el tipo de artefacto ni incorpora renderer, imagen o mockup.

Cambios de V16:
- Mantiene la base Unicode Block Art de V15.
- Reduce la proporción horizontal del ave para hacerla más angosta.
- Los pixeles principales de la silueta pasan a negro/casi negro (`ANSI 232`).
- El cuello se conserva completamente blanco mediante caracteres Unicode explícitamente blancos (`ANSI 97`).
- Mantiene el fondo negro de la terminal.
- Mantiene PowerShell + CMD ejecutable.

Ejecución:
1. Ejecutar `ejecutar_condor_unicode_v16.cmd`.
2. Para salir, escribir `/salir`, `exit` o `quit`.
