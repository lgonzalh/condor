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
for /f "usebackq delims=" %%N in (`powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "$branch=git -C '%ROOT%' branch --show-current; if ([string]::IsNullOrWhiteSpace($branch)) {$branch='detached'}; if ($branch -eq 'main') {'condor'} else {'condor-' + ($branch -replace '[^A-Za-z0-9._-]','-')}"`) do set "PROFILE=%%N"
if not defined PROFILE set "PROFILE=condor"
REM Cada version del codigo y su configuracion se publica en carpeta propia.
REM Una sesion anterior conserva sus archivos y no bloquea la publicacion nueva.
for /f "usebackq delims=" %%N in (`powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "$root='%ROOT%'; $files=@(Get-ChildItem -LiteralPath (Join-Path $root 'Src') -Recurse | Where-Object { -not $_.PSIsContainer -and $_.FullName -notmatch '\\(obj|bin)\\' }); $files += @(Get-ChildItem -LiteralPath $root | Where-Object { -not $_.PSIsContainer -and $_.Name -match '^(condor\.cmd|Condor\.slnx|Directory\..*\.(props|targets)|NuGet\.Config|global\.json)$' }); $stream=New-Object IO.MemoryStream; foreach($f in ($files | Sort-Object FullName)) { $name=[Text.Encoding]::UTF8.GetBytes($f.FullName.Substring($root.Length).ToLowerInvariant()); $bytes=[IO.File]::ReadAllBytes($f.FullName); $stream.Write($name,0,$name.Length); $stream.WriteByte(0); $stream.Write($bytes,0,$bytes.Length); $stream.WriteByte(0) }; $sha=[Security.Cryptography.SHA256]::Create(); ([BitConverter]::ToString($sha.ComputeHash($stream.ToArray()))).Replace('-','').Substring(0,12).ToLowerInvariant()"`) do set "FINGERPRINT=%%N"
if not defined FINGERPRINT set "FINGERPRINT=unknown"
set "OUT=%ROOT%\.artifacts\%PROFILE%-%FINGERPRINT%"
set "ARTIFACTS=%ROOT%\.artifacts\%PROFILE%-%FINGERPRINT%-build"
set "EXE=%OUT%\condor.exe"
set "NEED_PUBLISH=0"

REM El identificador de contenido cambia si cambia una fuente o configuracion
REM de compilacion. Un binario existente en esta carpeta corresponde al mismo
REM contenido y puede reutilizarse sin sobrescribir archivos abiertos.
set "SRC=%ROOT%\Src\Condor.Cli\Condor.Cli.csproj"
if not exist "%SRC%" (
    echo [Condor] ERROR: no se encontro el proyecto de la CLI en %SRC%
    exit /b 1
)

set "NEED_PUBLISH=1"
if exist "%EXE%" set "NEED_PUBLISH=0"

if "%NEED_PUBLISH%"=="1" (
    echo [Condor] Actualizando Condor...
    if not exist "%OUT%" mkdir "%OUT%"
    dotnet publish "%SRC%" -c Release -p:ArtifactsPath=%ARTIFACTS% -o "%OUT%" --nologo -v q
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
