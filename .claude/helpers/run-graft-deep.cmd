@echo off
setlocal
cd /d "%~dp0..\.."

echo Este proceso solicitara la clave completa con entrada oculta.
echo La clave solo estara en memoria durante este proceso y no se guardara en disco.
echo graft build --deep llamara a OpenAI y puede generar cargos por uso de API.
echo.

powershell.exe -NoLogo -NoProfile -Command "$ErrorActionPreference='Continue'; $buildExitCode=1; $checkExitCode=1; $secureKey=Read-Host 'Pega la clave completa de OpenAI (entrada oculta)' -AsSecureString; $credential=[System.Net.NetworkCredential]::new('', $secureKey); $env:GRAFT_API_KEY=$credential.Password; Remove-Variable secureKey,credential; try { graft build --deep; $buildExitCode=$LASTEXITCODE; Write-Host ('graft build --deep exit code: '+$buildExitCode); graft check; $checkExitCode=$LASTEXITCODE; Write-Host ('graft check exit code: '+$checkExitCode) } finally { Remove-Item Env:GRAFT_API_KEY -ErrorAction SilentlyContinue }; if ($buildExitCode -ne 0) { exit $buildExitCode }; exit $checkExitCode"

echo.
echo Puedes cerrar esta ventana; la variable de clave ya fue eliminada del proceso.
pause
endlocal
