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

REM Publicar la primera vez si el ejecutable no existe.
if not exist "%EXE%" (
    echo [Condor] Preparando Condor en la primera ejecucion, puede tardar unos segundos...
    if not exist "%OUT%" mkdir "%OUT%"
    dotnet publish "%ROOT%\Src\Condor.Cli\Condor.Cli.csproj" -c Release -o "%OUT%" --nologo -v q
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
