[CmdletBinding()]
param(
    [string]$BaseSha = $env:CONDOR_METRICS_BASE,
    [switch]$UseWorkingTree,
    [switch]$ShowExamples,
    [ValidateRange(4, 20)][int]$WindowLines = 6
)

$ErrorActionPreference = "Stop"

function Invoke-Git {
    param([Parameter(Mandatory)][string]$WorkingDirectory, [Parameter(Mandatory)][string[]]$Arguments)
    $output = & git -C $WorkingDirectory @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "git $($Arguments -join ' ') falló: $output" }
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

    $untrackedOutput = & git -C $RepositoryRoot ls-files --others --exclude-standard -z -- Src Tests
    if ($LASTEXITCODE -ne 0) { throw "git ls-files no pudo enumerar archivos C# nuevos." }
    $untrackedPaths = @($untrackedOutput -split "`0" | Where-Object {
        $_ -match '^(Src|Tests)[\\/].+\.cs$'
    })
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
            throw "No se puede superponer un archivo C# enlazado de forma segura: $relativePath"
        }
        $parentDirectory = Split-Path -Parent $destinationPath
        New-Item -ItemType Directory -Path $parentDirectory -Force | Out-Null
        [System.IO.File]::Copy($sourcePath, $destinationPath, $true)
    }
}

function Get-DuplicateBlocks {
    param([Parameter(Mandatory)][string]$Root)

    $files = Get-ChildItem -LiteralPath $Root -Directory | Where-Object { $_.Name -in @("Src", "Tests") } |
        ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -Filter "*.cs" -File -Recurse } |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Name -notmatch '\.(g|generated)\.cs$' }
    $blocksByFile = @{}
    $pathsByBlock = @{}

    foreach ($file in $files) {
        $relative = [System.IO.Path]::GetRelativePath($Root, $file.FullName).Replace('\', '/')
        $lines = [System.IO.File]::ReadAllLines($file.FullName)
        $blocks = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        for ($start = 0; $start -le $lines.Length - $WindowLines; $start++) {
            $normalized = [System.Collections.Generic.List[string]]::new()
            $valid = $true
            $characters = 0
            for ($offset = 0; $offset -lt $WindowLines; $offset++) {
                $line = $lines[$start + $offset].Trim()
                if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith('//') -or $line.StartsWith('*') -or
                    $line.StartsWith('using ', [StringComparison]::Ordinal)) {
                    $valid = $false
                    break
                }
                $line = [regex]::Replace($line, '\s+', ' ')
                $normalized.Add($line)
                $characters += $line.Length
            }
            if (-not $valid -or $characters -lt 100) { continue }

            $signature = [string]::Join("`n", $normalized)
            [void]$blocks.Add($signature)
        }
        $blocksByFile[$relative] = $blocks
        foreach ($block in $blocks) {
            if (-not $pathsByBlock.ContainsKey($block)) {
                $pathsByBlock[$block] = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
            }
            [void]$pathsByBlock[$block].Add($relative)
        }
    }

    $counts = @{}
    foreach ($path in $blocksByFile.Keys) {
        $count = 0
        foreach ($block in $blocksByFile[$path]) {
            if ($pathsByBlock[$block].Count -gt 1) { $count++ }
        }
        $counts[$path] = $count
    }
    return [pscustomobject]@{
        Counts = $counts
        BlocksByFile = $blocksByFile
        PathsByBlock = $pathsByBlock
    }
}

if ([string]::IsNullOrWhiteSpace($BaseSha)) { throw "Debes indicar CONDOR_METRICS_BASE con el commit base." }
$repositoryRoot = [System.IO.Path]::GetFullPath((Invoke-Git -WorkingDirectory $PSScriptRoot -Arguments @("rev-parse", "--show-toplevel")))
$headSha = Invoke-Git -WorkingDirectory $repositoryRoot -Arguments @("rev-parse", "HEAD")
$changedFileSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$committedChangedFiles = @(
    (Invoke-Git -WorkingDirectory $repositoryRoot -Arguments @("diff", "--name-only", "--diff-filter=ACMR", $BaseSha, $headSha, "--", "*.cs")) -split "`r?`n" |
        Where-Object { $_ -match '^(Src|Tests)[\\/].+\.cs$' } | ForEach-Object { $_.Replace('\', '/') }
)
foreach ($file in $committedChangedFiles) { [void]$changedFileSet.Add($file) }
if ($UseWorkingTree) {
    $workingTreeChangedFiles = @(
        (Invoke-Git -WorkingDirectory $repositoryRoot -Arguments @("diff", "--name-only", "--diff-filter=ACMR", "HEAD", "--", "*.cs")) -split "`r?`n" |
            Where-Object { $_ -match '^(Src|Tests)[\\/].+\.cs$' } | ForEach-Object { $_.Replace('\', '/') }
    )
    foreach ($file in $workingTreeChangedFiles) { [void]$changedFileSet.Add($file) }
    $untrackedSourceFiles = & git -C $repositoryRoot ls-files --others --exclude-standard -- Src Tests
    if ($LASTEXITCODE -ne 0) { throw "git ls-files no pudo enumerar archivos fuente nuevos." }
    foreach ($file in $untrackedSourceFiles) {
        if ($file -match '^(Src|Tests)[\\/].+\.cs$') { [void]$changedFileSet.Add($file.Replace('\', '/')) }
    }
}
$changedFiles = @($changedFileSet | Sort-Object)
if ($changedFiles.Count -eq 0) {
    Write-Host "No hay archivos C# de producto o pruebas cambiados; se omite la comparación de duplicación."
    exit 0
}

$scratchRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("condor-duplication-" + [Guid]::NewGuid().ToString("N"))
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

    $before = Get-DuplicateBlocks -Root $beforeRoot
    $after = Get-DuplicateBlocks -Root $afterRoot
    $afterLabel = if ($UseWorkingTree) { "árbol de trabajo" } else { "HEAD $headSha" }
    Write-Host "Comparación de bloques duplicados de $WindowLines líneas (base $BaseSha → $afterLabel):"
    $reported = 0
    foreach ($path in $changedFiles) {
        $beforeCount = if ($before.Counts.ContainsKey($path)) { $before.Counts[$path] } else { 0 }
        $afterCount = if ($after.Counts.ContainsKey($path)) { $after.Counts[$path] } else { 0 }
        if ($beforeCount -eq $afterCount) { continue }
        $delta = $afterCount - $beforeCount
        $sign = if ($delta -gt 0) { "+" } else { "" }
        Write-Host ("  {0}: {1} → {2} bloques ({3}{4})" -f $path, $beforeCount, $afterCount, $sign, $delta)
        $reported++
        if ($ShowExamples -and $delta -gt 0 -and $after.BlocksByFile.ContainsKey($path)) {
            $exampleCount = 0
            foreach ($block in $after.BlocksByFile[$path]) {
                if ($after.PathsByBlock[$block].Count -le 1) { continue }
                Write-Host ("    Bloque repetido con {0}: {1}" -f
                    ([string]::Join(", ", @($after.PathsByBlock[$block] | Where-Object { $_ -ne $path }))),
                    $block.Replace("`n", " | "))
                $exampleCount++
                if ($exampleCount -ge 2) { break }
            }
        }
    }
    if ($reported -eq 0) { Write-Host "  Sin variación de bloques duplicados en los archivos modificados." }
    Write-Host "Los bloques idénticos son una señal para revisar; no equivalen por sí solos a duplicación semántica."
}
finally {
    foreach ($worktree in $createdWorktrees) {
        try { [void](Invoke-Git -WorkingDirectory $repositoryRoot -Arguments @("worktree", "remove", "--force", $worktree)) } catch { }
    }
    $fullScratchRoot = [System.IO.Path]::GetFullPath($scratchRoot)
    $tempPrefix = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $fullScratchRoot.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Se rechazó limpiar un directorio fuera de la carpeta temporal: $fullScratchRoot"
    }
    if (Test-Path -LiteralPath $fullScratchRoot) { Remove-Item -LiteralPath $fullScratchRoot -Recurse -Force }
}
