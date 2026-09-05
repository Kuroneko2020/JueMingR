[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$requiredProductionPaths = @(
    'scripts\phase0s\Install-Phase0S.ps1',
    'scripts\phase0s\Restore-Phase0S.ps1',
    'scripts\build-phase0s-validation-package.ps1',
    'scripts\build-phase0t-biome-validation-package.ps1',
    'scripts\phase0s\Phase0T-Biome-Owner-Test-Card.zh-CN.md',
    'scripts\phase0s\Phase0U-F5UI-Owner-Test-Card.zh-CN.md',
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

try {
    foreach ($suiteScript in @('Invoke-LoadChainFixture.ps1', 'Invoke-InstallRecoveryTests.ps1')) {
        & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $repositoryRoot ('tests\Phase0S\' + $suiteScript)) -Run -RepositoryRoot $repositoryRoot
        if ($LASTEXITCODE -ne 0) {
            throw "Phase 0-S suite $suiteScript failed with exit $LASTEXITCODE."
        }
    }
    Write-Output 'PASS: Phase 0-S behavior tests passed.'
}
catch {
    [Console]::Error.WriteLine('FAIL: Phase 0-S behavior tests failed: ' + $_.Exception.Message)
    exit 1
}
