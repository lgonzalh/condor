@echo off
setlocal EnableDelayedExpansion
REM ======================================================================
REM  Condor - punto de entrada de usuario final
REM  Publica la CLI de Condor una sola vez y ejecuta condor.exe en el
REM  directorio actual desde el que se invoca.
REM
REM  Uso:
REM    condor                       -> prepara el entorno y abre sesion interactiva
REM    condor "<intencion libre>"   -> entrega la intencion al motor agente
REM    condor /analizar             -> analiza el proyecto/directorio
REM    condor /contexto /planear /construir /verificar /examinar /...
REM    condor /ayuda                -> muestra los comandos de control
REM  El usuario escribe lo que necesita en lenguaje natural; Condor prepara
REM  el entorno y actua. No se requieren modelos, herramientas o rutas.
REM ======================================================================

set "ROOT=%~dp0"
set "ROOT=%ROOT:~0,-1%"
set "OUT=%ROOT%\.artifacts\condor"
set "EXE=%OUT%\condor.exe"
set "NEED_PUBLISH=0"

REM Republicar cuando el fuente de la CLI es mas reciente que el ejecutable
REM (evita ejecutar un binario desactualizado sin que el usuario tenga que
REM recordar reconstruir a mano).
set "SRC=%ROOT%\Src\Condor.Cli\Condor.Cli.csproj"
if not exist "%SRC%" (
    echo [Condor] ERROR: no se encontro el proyecto de la CLI en %SRC%
    exit /b 1
)

for %%F in ("%SRC%" "%ROOT%\Src\Condor.Core\Condor.Core.csproj" "%ROOT%\Src\Condor.Infrastructure\Condor.Infrastructure.csproj") do (
    if not exist "%EXE%" (
        set "NEED_PUBLISH=1"
    ) else (
        for %%B in ("%EXE%") do for %%S in ("%%F") do (
            if "%%~tS" GTR "%%~tB" set "NEED_PUBLISH=1"
        )
    )
)

if "%NEED_PUBLISH%"=="1" (
    echo [Condor] Actualizando Condor...
    if not exist "%OUT%" mkdir "%OUT%"
    dotnet publish "%SRC%" -c Release -o "%OUT%" --nologo -v q
    if errorlevel 1 (
        echo [Condor] ERROR: no se pudo preparar Condor. Revisa que el SDK de .NET este instalado.
        exit /b 1
    )
)

if not exist "%EXE%" (
    echo [Condor] ERROR: no se encontro condor.exe en %EXE%
    exit /b 1
)

REM Ejecutar Condor en el directorio actual del usuario.
call "%EXE%" %*
exit /b %errorlevel%
