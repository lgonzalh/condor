$Host.UI.RawUI.WindowTitle = "Condor CLI - Ave Trabajo V16"
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}

$Host.UI.RawUI.ForegroundColor = "White"
$Host.UI.RawUI.BackgroundColor = "Black"
Clear-Host

Write-Host ""
Write-Host "CONDOR CLI  v0.1.0"
Write-Host "Observa · Comprende · Planifica"
Write-Host "Construye · Verifica"
Write-Host "Agente de Desarrollo Local"
Write-Host ("-" * 62)
Write-Host ""

$art = @(
    "[0m                                      [0m"
    "[0m  [38;5;232m▄▄[0m                        [38;5;232m██[0m [38;5;232m██[0m     [0m"
    "[38;5;232m████[0m                      [38;5;232m████████[0m    [0m"
    "[38;5;232m▀▀[38;5;242m██[38;5;232m█▄▄▄[0m                [38;5;232m▄▄████████▄▄[0m  [0m"
    "[0m  [38;5;242m▀[38;5;232m█████▄▄▄▄[0m           [38;5;232m▄██████[38;5;242m██[38;5;232m██▀▀[0m  [0m"
    "[0m    [38;5;242m▀[38;5;232m▀[38;5;242m█[38;5;232m█████[0m          [38;5;232m██████[38;5;242m████[38;5;232m██[0m    [0m"
    "[0m      [38;5;232m▀▀███████[0m [38;5;232m████████[38;5;242m██[38;5;232m█[38;5;242m███[38;5;232m█[38;5;242m██[0m     [0m"
    "[0m      [38;5;167m▄▄████[38;5;232m▀▀██████████[38;5;242m██[38;5;232m█[38;5;242m██[38;5;232m██[0m       [0m"
    "[0m     [38;5;242m███[38;5;167m████[97m█[38;5;232m▄██████████[38;5;242m██[38;5;232m▀▀[38;5;242m▀[38;5;232m▀[0m        [0m"
    "[0m     [38;5;242m▀[0m [38;5;232m▄▄▄████████████▄▄▄▄▄▄[0m          [0m"
    "[0m    [38;5;242m▀▀██[38;5;232m▀▀[0m  [38;5;232m██████████████████[0m        [0m"
    "[0m       [38;5;242m▄▄▄▄▄▀▀▀[0m [38;5;232m▀▀██████▀▀▀▀[0m          [0m"
    "[0m       [38;5;242m▀[0m                              [0m"
)

foreach ($line in $art) { Write-Host $line }

Write-Host ""
Write-Host "CÓNDOR"
Write-Host "Observa · Comprende · Planifica · Construye · Verifica"
Write-Host ""
Write-Host "¿Qué quieres construir?"
Write-Host ""
Write-Host "> " -NoNewline
$inputText = Read-Host

while ($inputText -notin @("/salir", "exit", "quit")) {
    Write-Host ""
    Write-Host "[CONDOR] Entrada recibida."
    Write-Host "[CONDOR] Modo local: activo"
    Write-Host ""
    Write-Host "> " -NoNewline
    $inputText = Read-Host
}

Clear-Host
Write-Host "Hasta pronto."
