$ErrorActionPreference = 'Stop'

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$project = Join-Path $repoRoot 'Tests\Unit\Condor.Cli.Tests\Condor.Cli.Tests.csproj'
$previousValue = [Environment]::GetEnvironmentVariable('CONDOR_UPDATE_TUI_SNAPSHOTS', 'Process')
$previousSnapshotDirectory = [Environment]::GetEnvironmentVariable('CONDOR_TUI_SNAPSHOT_DIRECTORY', 'Process')

try {
    [Environment]::SetEnvironmentVariable('CONDOR_UPDATE_TUI_SNAPSHOTS', '1', 'Process')
    [Environment]::SetEnvironmentVariable(
        'CONDOR_TUI_SNAPSHOT_DIRECTORY',
        (Join-Path $repoRoot 'Tests\Unit\Condor.Cli.Tests\VisualSnapshots'),
        'Process')
    dotnet test $project -c Release --no-restore --filter FullyQualifiedName~TuiVisualSnapshotTests
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    [Environment]::SetEnvironmentVariable('CONDOR_UPDATE_TUI_SNAPSHOTS', $previousValue, 'Process')
    [Environment]::SetEnvironmentVariable('CONDOR_TUI_SNAPSHOT_DIRECTORY', $previousSnapshotDirectory, 'Process')
}
