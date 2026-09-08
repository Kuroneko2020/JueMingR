[CmdletBinding()]
param(
    [switch] $DeferGraphics,
    [string] $GraphicsDeferralReason
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

if ($DeferGraphics -and [string]::IsNullOrWhiteSpace($GraphicsDeferralReason)) {
    throw '-DeferGraphics requires an explicitly classified environment reason and task authorization.'
}
if (-not $DeferGraphics -and -not [string]::IsNullOrWhiteSpace($GraphicsDeferralReason)) {
    throw '-GraphicsDeferralReason requires -DeferGraphics; full checks remain the default.'
}

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$requiredProductionPaths = @(
    'scripts\phase0s\Install-Phase0S.ps1',
    'scripts\phase0s\Restore-Phase0S.ps1',
    'scripts\build-phase0s-validation-package.ps1',
    'scripts\build-phase0t-biome-validation-package.ps1',
    'scripts\phase0s\Phase0T-Biome-Owner-Test-Card.zh-CN.md',
    'scripts\phase0s\Phase0U-F5UI-Owner-Test-Card.zh-CN.md',
    'scripts\phase0s\Phase0V-Settings-Owner-Test-Card.zh-CN.md',
    'scripts\phase0s\Phase0W-Notes-Owner-Test-Card.zh-CN.md',
    'scripts\phase0s\Item-Automation-Owner-Test-Card.zh-CN.md',
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
        $suiteArguments = @('-Run', '-RepositoryRoot', $repositoryRoot)
        if ($DeferGraphics -and $suiteScript -ceq 'Invoke-LoadChainFixture.ps1') {
            $suiteArguments += @('-DeferGraphics', '-GraphicsDeferralReason', $GraphicsDeferralReason)
        }
        & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $repositoryRoot ('tests\Phase0S\' + $suiteScript)) @suiteArguments
        if ($LASTEXITCODE -ne 0) {
            throw "Phase 0-S suite $suiteScript failed with exit $LASTEXITCODE."
        }
    }
    if ($DeferGraphics) {
        Write-Output 'PASS: Phase 0-S non-graphical behavior and install/recovery tests passed.'
        Write-Output ('DEFERRED: Notes XNA pixels/clipping/state and F5 actual input/render consumers. Reason: ' + $GraphicsDeferralReason)
        Write-Output 'NOT COMPLETE: graphical checks and separate actual-XNB Notes previews remain pending; no full-suite PASS.'
    } else {
        Write-Output 'PASS: Phase 0-S behavior tests passed.'
    }
}
catch {
    [Console]::Error.WriteLine('FAIL: Phase 0-S behavior tests failed: ' + $_.Exception.Message)
    exit 1
}
