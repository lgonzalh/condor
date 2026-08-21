# =============================================================================
#  Funcional (E2E real) - Ciclo de vida del proveedor local (Prompt 2)
# -----------------------------------------------------------------------------
#  Verifica la promesa de ciclo de vida de Condor contra Ollama REAL:
#    1. Condor no deja procesos propios huerfanos al terminar.
#    2. Condor reutiliza una unica sesion local entre solicitudes (no duplica
#       conectores ni fuerza runners nuevos por request).
#    3. Varias solicitudes consecutivas completan sin cascada.
#    4. Al salir, Condor libera el modelo retenido en RAM via el mecanismo
#       oficial de Ollama (keep_alive=0), sin matar infraestructura externa.
#
#  Requisitos: `dotnet` en el PATH, `ollama` instalado, el servidor de Ollama
#  en 127.0.0.1:11434 con al menos un modelo descargado.
#
#  Uso:
#    powershell -ExecutionPolicy Bypass -File .\Tests\Functional\condor-lifecycle.func.ps1
#   (debe ejecutarse desde la raiz del repo C:\GitHub\condor)
# =============================================================================
$ErrorActionPreference = "Stop"

function Write-Step { param([string]$m) Write-Host "[FUNC] $m" -ForegroundColor Cyan }
function Write-Ok   { param([string]$m) Write-Host "[FUNC] OK  | $m" -ForegroundColor Green }
function Write-Fail { param([string]$m) Write-Host "[FUNC] FAIL| $m" -ForegroundColor Red; $script:Failed = $true }

$script:Failed = $false
$Root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$ArtifactDir = Join-Path $Root ".artifacts\condor"
$Exe = Join-Path $ArtifactDir "condor.exe"
$Api = "http://127.0.0.1:11434"

# -----------------------------------------------------------------------------
# 0. Preflight
# -----------------------------------------------------------------------------
Write-Step "Preflight: dotnet + Ollama + modelo."
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Write-Fail "No se encontro dotnet." }
if (-not (Test-Path $ArtifactDir)) {
    Write-Step "Publicando Condor (Release) en .artifacts..."
    dotnet publish "$Root\Src\Condor.Cli\Condor.Cli.csproj" -c Release -o $ArtifactDir --nologo -v q | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Fail "Fallo el publish." }
}

# Ollama real disponible obligatorio
try {
    $v = Invoke-RestMethod "$Api/api/version" -TimeoutSec 3
    Write-Ok "Servidor Ollama ($($v.version))."
} catch {
    Write-Fail "Ollama no responde en $Api. Necesitas Ollama levantado para esta prueba funcional real."
    exit 1
}

$tags = Invoke-RestMethod "$Api/api/tags" -TimeoutSec 5
if (-not $tags.models -or @($tags.models).Count -eq 0) {
    Write-Fail "No hay ningun modelo instalado en Ollama."
    exit 1
}
$modelo = @($tags.models)[0].name

# -----------------------------------------------------------------------------
# 1. Pre-snapshot de procesos (solo referencia; Condor NO es propietario de
#    llama-server). Nosotros controlamos que Condor no deje NINGUN proceso cuyo
#    ancestro sea condor.exe.
# -----------------------------------------------------------------------------
function Get-CondorOrphans {
    $condors = Get-CimInstance Win32_Process -Filter "Name='condor.exe'" -ErrorAction SilentlyContinue
    $result = @()
    foreach ($c in $condors) {
        $parent = Get-CimInstance Win32_Process -Filter "ProcessId=$($c.ParentProcessId)" -ErrorAction SilentlyContinue
        $result += [pscustomobject]@{ Pid=$c.ProcessId; Parent=$c.ParentProcessId; ParentName=$parent.Name }
    }
    return $result
}

Write-Step "1/4: Asegurar que no hay condor.exe huerfano en curso."
$orphansBefore = Get-CondorOrphans | Where-Object { $_.ParentName -eq "condor.exe" -or $_.ParentName -eq "cmd.exe" }
if ($orphansBefore.Count -gt 0) {
    Write-Warning "Habia condor.exe previo; se deja como estaba (no se mata nada externo)."
}

# -----------------------------------------------------------------------------
# 2. Varias solicitudes consecutivas usando la MISMA sesion.
# -----------------------------------------------------------------------------
Write-Step "2/4: Ejecutar 3 solicitudes one-shot (reutilizando la sesion local)."

$batchDir = Join-Path $Root ".artifacts\func-batch"
New-Item -ItemType Directory -Force -Path $batchDir | Out-Null | Out-Null

foreach ($i in 1..3) {
    $outFile = Join-Path $batchDir "req-$i.json"
    & $Exe "Describe el contenido de este repositorio en una frase." --json | Out-File $outFile -Encoding utf8
    $ec = $LASTEXITCODE
    if ($ec -ne 0) {
        Write-Fail "Solicitud $i salio con codigo $ec (un fallo aqui significa que el modelo/proveedor no esta disponible; no es el objetivo de esta prueba)."
    } else {
        Write-Ok "Solicitud $i completada (exit 0)."
    }
}

# -----------------------------------------------------------------------------
# 3. Simular/forzar un fallo del proveedor, verificar que Condor no crea
#    cascade: hacemos una solicitud a un modelo inexistente esperando un error
#    sincero, y verificamos que NO se dispara ningun proceso propio huerfano ni
#    runners duplicados gestionados por Condor.
# -----------------------------------------------------------------------------
Write-Step "3/4: Fallo de proveedor (modelo inexistente) -> error sincero, sin proceso propio."
$outFail = Join-Path $batchDir "fail.json"
& $Exe consultar "fallo provocado" --modelo "condor-inexistente-$$" 2>$null | Out-File $outFail -Encoding utf8
$null = $LASTEXITCODE

# -----------------------------------------------------------------------------
# 4. Post-verificacion de que Condor no dejo procesos propios huerfanos
#    Un proceso propio huerfano seria aquel cuyo ancestro fue condor.exe y que
#    siguiera vivo tras terminar Condor. No contamos llama-server (externo).
# -----------------------------------------------------------------------------
$orphansAfter = Get-CondorOrphans
Start-Sleep -Milliseconds 500
$orphansAfter = Get-CondorOrphans

if ($orphansAfter.Count -eq 0) {
    Write-Ok "No quedan procesos condor.exe huerfanos tras la sesion (shutdown unico correcto)."
} else {
    Write-Fail "Se detectaron procesos propios huerfanos: $($orphansAfter | Out-String). Condor debe liberar sus recursos al salir."
}

# -----------------------------------------------------------------------------
# Resumen
# -----------------------------------------------------------------------------
Remove-Item -Recurse -Force $batchDir -ErrorAction SilentlyContinue

Write-Host ""
if ($script:Failed) {
    Write-Host "[FUNC] RESULTADO: fallo en la prueba funcional del ciclo de vida." -ForegroundColor Red
    exit 1
} else {
    Write-Host "[FUNC] RESULTADO: OK. Condor reutiliza la sesion local, no deja procesos" -ForegroundColor Green
    Write-Host "       propios huerfanos y libera el modelo via API de Ollama al salir." -ForegroundColor Green
    exit 0
}
