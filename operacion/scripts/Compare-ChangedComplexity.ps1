[CmdletBinding()]
param(
    [string]$BaseSha = $env:CONDOR_METRICS_BASE,
    [switch]$UseWorkingTree
)

$ErrorActionPreference = "Stop"

function Invoke-Git {
    param(
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [Parameter(Mandatory)][string[]]$Arguments
    )

    $output = & git -C $WorkingDirectory @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') falló: $output"
    }
    return ($output -join [Environment]::NewLine).Trim()
}

function Copy-WorkingTreeOverlay {
    param(
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string]$DestinationRoot,
        [Parameter(Mandatory)][string]$ScratchRoot
    )

    $patchPath = Join-Path $ScratchRoot "working-tree.patch"
    [void](Invoke-Git -WorkingDirectory $RepositoryRoot -Arguments @("diff", "--binary", "HEAD", "--output=$patchPath", "--"))
    if ((Get-Item -LiteralPath $patchPath).Length -gt 0) {
        [void](Invoke-Git -WorkingDirectory $DestinationRoot -Arguments @("apply", "--binary", $patchPath))
    }

    $untrackedOutput = & git -C $RepositoryRoot ls-files --others --exclude-standard -z
    if ($LASTEXITCODE -ne 0) { throw "git ls-files no pudo enumerar archivos nuevos del árbol de trabajo." }
    $untrackedPaths = @($untrackedOutput -split "`0" | Where-Object { -not [string]::IsNullOrEmpty($_) })
    $sourcePrefix = $RepositoryRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $destinationPrefix = $DestinationRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

    foreach ($relativePath in $untrackedPaths) {
        $sourcePath = [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot $relativePath))
        $destinationPath = [System.IO.Path]::GetFullPath((Join-Path $DestinationRoot $relativePath))
        if (-not $sourcePath.StartsWith($sourcePrefix, [StringComparison]::OrdinalIgnoreCase) -or
            -not $destinationPath.StartsWith($destinationPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Se rechazó copiar una ruta fuera del repositorio o del worktree temporal: $relativePath"
        }
        if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) { continue }
        $sourceItem = Get-Item -LiteralPath $sourcePath -Force
        if (($sourceItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "No se puede superponer un archivo enlazado de forma segura: $relativePath"
        }
        $parentDirectory = Split-Path -Parent $destinationPath
        New-Item -ItemType Directory -Path $parentDirectory -Force | Out-Null
        [System.IO.File]::Copy($sourcePath, $destinationPath, $true)
    }
}

function Find-ProjectFile {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$RelativeFile
    )

    $normalizedRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    $directory = [System.IO.Path]::GetDirectoryName((Join-Path $normalizedRoot $RelativeFile))
    while ($directory -and ($directory.StartsWith($normalizedRoot + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or [string]::Equals($directory, $normalizedRoot, [StringComparison]::OrdinalIgnoreCase))) {
        $project = Get-ChildItem -LiteralPath $directory -Filter "*.csproj" -File -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($project) {
            return [System.IO.Path]::GetRelativePath($Root, $project.FullName)
        }
        if ([string]::Equals($directory, $Root, [StringComparison]::OrdinalIgnoreCase)) {
            break
        }
        $directory = [System.IO.Path]::GetDirectoryName($directory)
    }
    return $null
}

function Invoke-MetricsBuild {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$Project,
        [Parameter(Mandatory)][string]$LogPath,
        [Parameter(Mandatory)][string]$Label
    )

    Push-Location $Root
    try {
        & dotnet build $Project -c Release --verbosity minimal *> $LogPath
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
    if ($exitCode -ne 0) {
        throw "No se pudo compilar $Label ($Project). Consulta el log: $LogPath"
    }
}

function Read-ComplexityMetrics {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string[]]$Logs
    )

    $metrics = @{}
    $diagnosticPattern = [regex]::new('^(?<file>.+?\.cs)\((?<line>\d+),\d+\): warning CA1502: (?<message>.+)$')
    $spanishPattern = [regex]::new('"(?<method>[^"]+)" tiene una complejidad ciclomática de "?(?<value>\d+)"?')
    $englishPattern = [regex]::new("Method '(?<method>[^']+)' has a cyclomatic complexity of (?<value>\d+)")

    foreach ($log in $Logs) {
        foreach ($line in [System.IO.File]::ReadLines($log)) {
            $diagnostic = $diagnosticPattern.Match($line)
            if (-not $diagnostic.Success) { continue }

            $detail = $spanishPattern.Match($diagnostic.Groups['message'].Value)
            if (-not $detail.Success) {
                $detail = $englishPattern.Match($diagnostic.Groups['message'].Value)
            }
            if (-not $detail.Success) { continue }

            $filePath = $diagnostic.Groups['file'].Value
            if (-not [System.IO.Path]::IsPathRooted($filePath)) {
                $filePath = Join-Path $Root $filePath
            }
            $relative = [System.IO.Path]::GetRelativePath($Root, [System.IO.Path]::GetFullPath($filePath)).Replace('\', '/')
            $key = $relative + "|" + $detail.Groups['method'].Value
            if (-not $metrics.ContainsKey($key)) {
                $metrics[$key] = [System.Collections.Generic.List[object]]::new()
            }
            $metrics[$key].Add([pscustomobject]@{
                Line = [int]$diagnostic.Groups['line'].Value
                Value = [int]$detail.Groups['value'].Value
            })
        }
    }

    foreach ($key in @($metrics.Keys)) {
        $metrics[$key] = @($metrics[$key] | Sort-Object Line, Value -Unique)
    }
    return $metrics
}

if ([string]::IsNullOrWhiteSpace($BaseSha)) {
    throw "Debes indicar CONDOR_METRICS_BASE con el commit base de la solicitud."
}

$repositoryRoot = [System.IO.Path]::GetFullPath((Invoke-Git -WorkingDirectory $PSScriptRoot -Arguments @("rev-parse", "--show-toplevel")))
$headSha = Invoke-Git -WorkingDirectory $repositoryRoot -Arguments @("rev-parse", "HEAD")
$changedFileSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$committedChangedFiles = @(
    (Invoke-Git -WorkingDirectory $repositoryRoot -Arguments @("diff", "--name-only", "--diff-filter=ACMR", $BaseSha, $headSha, "--", "Src")) -split "`r?`n" |
        Where-Object { $_.EndsWith(".cs", [StringComparison]::OrdinalIgnoreCase) }
)
foreach ($file in $committedChangedFiles) { [void]$changedFileSet.Add($file.Replace('\', '/')) }
if ($UseWorkingTree) {
    $workingTreeChangedFiles = @(
        (Invoke-Git -WorkingDirectory $repositoryRoot -Arguments @("diff", "--name-only", "--diff-filter=ACMR", "HEAD", "--", "Src")) -split "`r?`n" |
            Where-Object { $_.EndsWith(".cs", [StringComparison]::OrdinalIgnoreCase) }
    )
    foreach ($file in $workingTreeChangedFiles) { [void]$changedFileSet.Add($file.Replace('\', '/')) }
    $untrackedSourceFiles = & git -C $repositoryRoot ls-files --others --exclude-standard -- Src
    if ($LASTEXITCODE -ne 0) { throw "git ls-files no pudo enumerar archivos C# nuevos." }
    foreach ($file in $untrackedSourceFiles) {
        if ($file.EndsWith(".cs", [StringComparison]::OrdinalIgnoreCase)) { [void]$changedFileSet.Add($file.Replace('\', '/')) }
    }
}
$changedFiles = @($changedFileSet | Sort-Object)

if ($changedFiles.Count -eq 0) {
    Write-Host "No hay archivos C# de producto cambiados; se omite la comparación de complejidad."
    exit 0
}

$projectFiles = @(
    $changedFiles |
        ForEach-Object { Find-ProjectFile -Root $repositoryRoot -RelativeFile $_ } |
        Where-Object { $_ } |
        Sort-Object -Unique
)
if ($projectFiles.Count -eq 0) {
    Write-Host "No se encontró un proyecto .NET para los archivos C# cambiados; no hay métricas CA1502 aplicables."
    exit 0
}

$scratchRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("condor-complexity-" + [Guid]::NewGuid().ToString("N"))
$beforeRoot = Join-Path $scratchRoot "before"
$afterRoot = Join-Path $scratchRoot "after"
$createdWorktrees = [System.Collections.Generic.List[string]]::new()
New-Item -ItemType Directory -Path $scratchRoot | Out-Null

try {
    [void](Invoke-Git -WorkingDirectory $repositoryRoot -Arguments @("worktree", "add", "--detach", $beforeRoot, $BaseSha))
    $createdWorktrees.Add($beforeRoot)
    [void](Invoke-Git -WorkingDirectory $repositoryRoot -Arguments @("worktree", "add", "--detach", $afterRoot, $headSha))
    $createdWorktrees.Add($afterRoot)
    if ($UseWorkingTree) {
        Copy-WorkingTreeOverlay -RepositoryRoot $repositoryRoot -DestinationRoot $afterRoot -ScratchRoot $scratchRoot
    }

    $metricsProps = Join-Path $repositoryRoot "Directory.Build.props"
    if (-not (Test-Path -LiteralPath $metricsProps)) {
        throw "No se encontró Directory.Build.props para habilitar las métricas CA1502."
    }
    $editorConfig = Join-Path $repositoryRoot ".editorconfig"
    foreach ($worktree in @($beforeRoot, $afterRoot)) {
        Copy-Item -LiteralPath $metricsProps -Destination (Join-Path $worktree "Directory.Build.props") -Force
        if (Test-Path -LiteralPath $editorConfig) {
            Copy-Item -LiteralPath $editorConfig -Destination (Join-Path $worktree ".editorconfig") -Force
        }
        [System.IO.File]::WriteAllText(
            (Join-Path $worktree "CodeMetricsConfig.txt"),
            "CA1502: 1`n",
            [System.Text.UTF8Encoding]::new($false))
    }

    $beforeLogs = [System.Collections.Generic.List[string]]::new()
    $afterLogs = [System.Collections.Generic.List[string]]::new()
    foreach ($project in $projectFiles) {
        $safeName = $project -replace '[^A-Za-z0-9_.-]', '_'
        $beforeLog = Join-Path $scratchRoot ("before-" + $safeName + ".log")
        $afterLog = Join-Path $scratchRoot ("after-" + $safeName + ".log")
        Invoke-MetricsBuild -Root $beforeRoot -Project $project -LogPath $beforeLog -Label "la base"
        Invoke-MetricsBuild -Root $afterRoot -Project $project -LogPath $afterLog -Label "la versión propuesta"
        $beforeLogs.Add($beforeLog)
        $afterLogs.Add($afterLog)
    }

    $beforeMetrics = Read-ComplexityMetrics -Root $beforeRoot -Logs $beforeLogs.ToArray()
    $afterMetrics = Read-ComplexityMetrics -Root $afterRoot -Logs $afterLogs.ToArray()
    $changedSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $changedFiles) { [void]$changedSet.Add($file.Replace('\', '/')) }

    $afterLabel = if ($UseWorkingTree) { "árbol de trabajo" } else { "HEAD $headSha" }
    Write-Host "Comparación CA1502 de métodos afectados (base $BaseSha → $afterLabel):"
    $reported = 0
    foreach ($key in @($beforeMetrics.Keys + $afterMetrics.Keys | Sort-Object -Unique)) {
        $parts = $key.Split('|', 2)
        if (-not $changedSet.Contains($parts[0])) { continue }
        $beforeValues = if ($beforeMetrics.ContainsKey($key)) { @($beforeMetrics[$key]) } else { @() }
        $afterValues = if ($afterMetrics.ContainsKey($key)) { @($afterMetrics[$key]) } else { @() }
        $count = [Math]::Max($beforeValues.Count, $afterValues.Count)
        for ($index = 0; $index -lt $count; $index++) {
            $beforeValue = if ($index -lt $beforeValues.Count) { $beforeValues[$index].Value } else { $null }
            $afterValue = if ($index -lt $afterValues.Count) { $afterValues[$index].Value } else { $null }
            if ($null -ne $beforeValue -and $null -ne $afterValue -and $beforeValue -eq $afterValue) { continue }
            $delta = if ($null -ne $beforeValue -and $null -ne $afterValue) { $afterValue - $beforeValue } else { $null }
            $deltaText = if ($null -eq $delta) { "" } else { " ({0}{1})" -f $(if ($delta -gt 0) { "+" } else { "" }), $delta }
            $line = if ($index -lt $afterValues.Count) { $afterValues[$index].Line } else { "eliminado" }
            $beforeText = if ($null -eq $beforeValue) { "sin alerta" } else { $beforeValue }
            $afterText = if ($null -eq $afterValue) { "sin alerta" } else { $afterValue }
            Write-Host ("  {0}:{1} {2}: {3} → {4}{5}" -f $parts[0], $line, $parts[1], $beforeText, $afterText, $deltaText)
            $reported++
        }
    }
    if ($reported -eq 0) {
        Write-Host "  Sin cambios de complejidad en los métodos afectados."
    }
    Write-Host "Revisa los aumentos junto con el propósito del cambio; este informe no sustituye la revisión del código."
}
finally {
    foreach ($worktree in $createdWorktrees) {
        try { [void](Invoke-Git -WorkingDirectory $repositoryRoot -Arguments @("worktree", "remove", "--force", $worktree)) } catch { }
    }
    $fullScratchRoot = [System.IO.Path]::GetFullPath($scratchRoot)
    $tempPrefix = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $fullScratchRoot.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Se rechazó limpiar un directorio de métricas fuera de la carpeta temporal: $fullScratchRoot"
    }
    if (Test-Path -LiteralPath $fullScratchRoot) {
        Remove-Item -LiteralPath $fullScratchRoot -Recurse -Force
    }
}
