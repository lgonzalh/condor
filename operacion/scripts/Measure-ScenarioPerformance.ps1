[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BaseSha,
    [Parameter(Mandatory)][string]$Executable,
    [Parameter(Mandatory)][string[]]$ArgumentList,
    [switch]$UseWorkingTree,
    [string]$SetupExecutable,
    [string[]]$SetupArgumentList = @(),
    [ValidateRange(2, 20)][int]$Iterations = 3,
    [ValidateRange(1, 3600)][int]$TimeoutSeconds = 300
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

    $untrackedOutput = & git -C $RepositoryRoot ls-files --others --exclude-standard -z
    if ($LASTEXITCODE -ne 0) { throw "git ls-files no pudo enumerar los archivos nuevos del árbol de trabajo." }
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
        [System.IO.File]::SetLastWriteTimeUtc($destinationPath, $sourceItem.LastWriteTimeUtc)
    }
}

function Measure-Scenario {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$Label,
        [Parameter(Mandatory)][int]$Iteration
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $Executable
    $startInfo.WorkingDirectory = $Root
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in $ArgumentList) { $startInfo.ArgumentList.Add($argument) }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $clock = [System.Diagnostics.Stopwatch]::new()
    try {
        if (-not $process.Start()) { throw "No se pudo iniciar el escenario $Label." }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $peakWorkingSet = [long]0
        $lastChildCpu = @{}
        $childCpuTotal = [double]0
        $clock.Start()
        while (-not $process.WaitForExit(100)) {
            $tree = Get-ProcessTreeMetrics -RootProcessId $process.Id
            if ($tree.WorkingSetBytes -gt $peakWorkingSet) { $peakWorkingSet = $tree.WorkingSetBytes }
            foreach ($entry in $tree.ChildCpuMilliseconds.GetEnumerator()) {
                if ($lastChildCpu.ContainsKey($entry.Key)) {
                    $childCpuTotal += [Math]::Max(0, $entry.Value - $lastChildCpu[$entry.Key])
                }
                else {
                    $childCpuTotal += $entry.Value
                }
                $lastChildCpu[$entry.Key] = $entry.Value
            }
            if ($clock.Elapsed.TotalSeconds -ge $TimeoutSeconds) {
                try { $process.Kill($true) } catch { }
                [void]$process.WaitForExit(5000)
                throw "El escenario $Label excedió ${TimeoutSeconds}s y fue cancelado."
            }
        }
        $clock.Stop()
        $process.Refresh()
        $finalTree = Get-ProcessTreeMetrics -RootProcessId $process.Id
        if ($finalTree.WorkingSetBytes -gt $peakWorkingSet) { $peakWorkingSet = $finalTree.WorkingSetBytes }
        foreach ($entry in $finalTree.ChildCpuMilliseconds.GetEnumerator()) {
            if ($lastChildCpu.ContainsKey($entry.Key)) {
                $childCpuTotal += [Math]::Max(0, $entry.Value - $lastChildCpu[$entry.Key])
            }
            else {
                $childCpuTotal += $entry.Value
            }
            $lastChildCpu[$entry.Key] = $entry.Value
        }
        $stdoutText = $stdout.GetAwaiter().GetResult()
        $stderrText = $stderr.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) {
            throw "El escenario $Label terminó con código $($process.ExitCode). stdout: $stdoutText stderr: $stderrText"
        }
        return [pscustomobject]@{
            Version = $Label
            Iteration = $Iteration
            ElapsedMilliseconds = [Math]::Round($clock.Elapsed.TotalMilliseconds, 2)
            CpuMilliseconds = [Math]::Round($process.TotalProcessorTime.TotalMilliseconds + $childCpuTotal, 2)
            PeakWorkingSetBytes = $peakWorkingSet
        }
    }
    finally {
        $clock.Stop()
        $process.Dispose()
    }
}

function Get-ProcessTreeMetrics {
    param([Parameter(Mandatory)][int]$RootProcessId)

    $processes = @(Get-CimInstance -ClassName Win32_Process -Property ProcessId, ParentProcessId, WorkingSetSize, KernelModeTime, UserModeTime)
    $childrenByParent = @{}
    foreach ($item in $processes) {
        $parentId = [int]$item.ParentProcessId
        if (-not $childrenByParent.ContainsKey($parentId)) {
            $childrenByParent[$parentId] = [System.Collections.Generic.List[object]]::new()
        }
        $childrenByParent[$parentId].Add($item)
    }

    $processIds = [System.Collections.Generic.HashSet[int]]::new()
    $pending = [System.Collections.Generic.Stack[int]]::new()
    [void]$processIds.Add($RootProcessId)
    $pending.Push($RootProcessId)
    while ($pending.Count -gt 0) {
        $parentId = $pending.Pop()
        if (-not $childrenByParent.ContainsKey($parentId)) { continue }
        foreach ($child in $childrenByParent[$parentId]) {
            $childId = [int]$child.ProcessId
            if ($processIds.Add($childId)) { $pending.Push($childId) }
        }
    }

    $workingSet = [long]0
    $childCpu = @{}
    foreach ($item in $processes) {
        $processId = [int]$item.ProcessId
        if (-not $processIds.Contains($processId)) { continue }
        $workingSet += [long]$item.WorkingSetSize
        if ($processId -ne $RootProcessId) {
            $childCpu[$processId] = ([double]$item.KernelModeTime + [double]$item.UserModeTime) / 10000
        }
    }
    return [pscustomobject]@{ WorkingSetBytes = $workingSet; ChildCpuMilliseconds = $childCpu }
}

function Get-Median {
    param([Parameter(Mandatory)][double[]]$Values)
    $sorted = @($Values | Sort-Object)
    $middle = [int][Math]::Floor($sorted.Count / 2)
    if ($sorted.Count % 2 -eq 1) { return $sorted[$middle] }
    return ($sorted[$middle - 1] + $sorted[$middle]) / 2
}

$repositoryRoot = [System.IO.Path]::GetFullPath((Invoke-Git -WorkingDirectory $PSScriptRoot -Arguments @("rev-parse", "--show-toplevel")))
$onWindows = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)
if (-not $onWindows) { throw "La medición del árbol de procesos requiere Windows y Win32_Process." }
$headSha = Invoke-Git -WorkingDirectory $repositoryRoot -Arguments @("rev-parse", "HEAD")
$afterLabel = if ($UseWorkingTree) { "working-tree" } else { "HEAD" }
if ($SetupArgumentList.Count -gt 0 -and [string]::IsNullOrWhiteSpace($SetupExecutable)) {
    throw "Debes indicar -SetupExecutable cuando proporcionas -SetupArgumentList."
}
$resolvedBaseSha = Invoke-Git -WorkingDirectory $repositoryRoot -Arguments @("rev-parse", "$BaseSha^{commit}")
$scratchRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("condor-performance-" + [Guid]::NewGuid().ToString("N"))
$beforeRoot = Join-Path $scratchRoot "before"
$afterRoot = Join-Path $scratchRoot "after"
$createdWorktrees = [System.Collections.Generic.List[string]]::new()
New-Item -ItemType Directory -Path $scratchRoot | Out-Null
try {
    [void](Invoke-Git -WorkingDirectory $repositoryRoot -Arguments @("worktree", "add", "--detach", $beforeRoot, $resolvedBaseSha))
    $createdWorktrees.Add($beforeRoot)
    [void](Invoke-Git -WorkingDirectory $repositoryRoot -Arguments @("worktree", "add", "--detach", $afterRoot, $headSha))
    $createdWorktrees.Add($afterRoot)

    if ($UseWorkingTree) {
        Copy-WorkingTreeOverlay -RepositoryRoot $repositoryRoot -DestinationRoot $afterRoot -ScratchRoot $scratchRoot
    }

    if (-not [string]::IsNullOrWhiteSpace($SetupExecutable)) {
        $measureExecutable = $Executable
        $measureArguments = $ArgumentList
        try {
            $Executable = $SetupExecutable
            $ArgumentList = $SetupArgumentList
            foreach ($entry in @(@("base-setup", $beforeRoot), @("after-setup", $afterRoot))) {
                $null = Measure-Scenario -Root $entry[1] -Label $entry[0] -Iteration 0
            }
        }
        finally {
            $Executable = $measureExecutable
            $ArgumentList = $measureArguments
        }
    }

    $measurements = [System.Collections.Generic.List[object]]::new()
    for ($iteration = 1; $iteration -le $Iterations; $iteration++) {
        $order = if ($iteration % 2 -eq 1) { @(@("base", $beforeRoot), @($afterLabel, $afterRoot)) } else { @(@($afterLabel, $afterRoot), @("base", $beforeRoot)) }
        foreach ($entry in $order) {
            $measurements.Add((Measure-Scenario -Root $entry[1] -Label $entry[0] -Iteration $iteration))
        }
    }

    $summary = foreach ($label in @("base", $afterLabel)) {
        $values = @($measurements | Where-Object Version -eq $label)
        [pscustomobject]@{
            Version = $label
            MedianElapsedMilliseconds = Get-Median -Values ([double[]]@($values.ElapsedMilliseconds))
            MedianCpuMilliseconds = Get-Median -Values ([double[]]@($values.CpuMilliseconds))
            MedianPeakWorkingSetBytes = Get-Median -Values ([double[]]@($values.PeakWorkingSetBytes))
        }
    }
    $reportDirectory = Join-Path $repositoryRoot ".condor/performance"
    New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
    $reportPath = Join-Path $reportDirectory ("scenario-" + (Get-Date -Format "yyyyMMdd-HHmmssfff") + ".json")
    $report = [pscustomobject]@{
        SchemaVersion = "1.0"
        RecordedAtUtc = [DateTime]::UtcNow.ToString("o")
        BaseSha = $resolvedBaseSha
        HeadSha = $headSha
        MeasuredAfter = $afterLabel
        IncludesWorkingTree = [bool]$UseWorkingTree
        Executable = $Executable
        Arguments = $ArgumentList
        SetupExecutable = $SetupExecutable
        SetupArguments = $SetupArgumentList
        Iterations = $Iterations
        MemoryMetric = "Peak aggregate working set of the foreground process and descendants, sampled every 100ms; shared pages can be counted more than once."
        Summary = @($summary)
        Measurements = @($measurements)
    }
    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $reportPath -Encoding utf8
    Write-Host ("Escenario: {0} {1}" -f $Executable, ($ArgumentList -join ' '))
    foreach ($result in $summary) {
        Write-Host ("  {0}: mediana {1} ms, CPU {2} ms, memoria pico {3} bytes" -f $result.Version, $result.MedianElapsedMilliseconds, $result.MedianCpuMilliseconds, $result.MedianPeakWorkingSetBytes)
    }
    Write-Host "CPU y memoria incluyen procesos descendientes; la memoria es una suma de working sets muestreada cada 100 ms y puede contar páginas compartidas más de una vez."
    Write-Output $reportPath
}
finally {
    foreach ($worktree in $createdWorktrees) {
        try { [void](Invoke-Git -WorkingDirectory $repositoryRoot -Arguments @("worktree", "remove", "--force", $worktree)) } catch { }
    }
    $fullScratchRoot = [System.IO.Path]::GetFullPath($scratchRoot)
    $tempPrefix = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $fullScratchRoot.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Se rechazó limpiar un directorio de rendimiento fuera de la carpeta temporal: $fullScratchRoot"
    }
    if (Test-Path -LiteralPath $fullScratchRoot) { Remove-Item -LiteralPath $fullScratchRoot -Recurse -Force }
}
