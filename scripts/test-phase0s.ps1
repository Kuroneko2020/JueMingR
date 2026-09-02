[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$requiredProductionPaths = @(
    'scripts\phase0s\Install-Phase0S.ps1',
    'scripts\phase0s\Restore-Phase0S.ps1',
    'scripts\build-phase0s-validation-package.ps1',
    'src\JueMingR.Bootstrap\Phase0SAppDomainManager.cs',
    'src\JueMingR.TerrariaHost\Phase0SLoadChainHost.cs',
    'eng\Harmony.baseline.json'
)
$missing = @($requiredProductionPaths | Where-Object {
    -not [System.IO.File]::Exists((Join-Path $repositoryRoot $_))
})

if ($missing.Count -ne 0) {
    [Console]::Error.WriteLine('RED: required Phase 0-S production files are missing:')
    foreach ($path in $missing) {
        [Console]::Error.WriteLine('- ' + $path)
    }
    exit 3
}

. (Join-Path $repositoryRoot 'tests\Phase0S\Invoke-LoadChainFixture.ps1')
. (Join-Path $repositoryRoot 'tests\Phase0S\Invoke-InstallRecoveryTests.ps1')

try {
    Invoke-Phase0SLoadChainFixtureTests -RepositoryRoot $repositoryRoot
    Invoke-Phase0SInstallRecoveryTests -RepositoryRoot $repositoryRoot
    Write-Output 'PASS: Phase 0-S behavior tests passed.'
}
catch {
    [Console]::Error.WriteLine('FAIL: Phase 0-S behavior tests failed: ' + $_.Exception.Message)
    exit 1
}
