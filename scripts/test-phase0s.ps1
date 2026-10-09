[CmdletBinding()]
param(
    [ValidateSet('AllSynthetic','LegacySynthetic','InstallRecovery','PackageIntegrity')]
    [string] $Scope = 'AllSynthetic',
    [string] $ZipPath,
    [string] $BuildRecordPath,
    [string] $ExpectedSourceCommit,
    [string] $ExpectedZipSha256,
    [string] $ApprovedRetainedAssets,
    [switch] $DeferGraphics,
    [string] $GraphicsDeferralReason
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

# An existing artifact has its own source identity. This path deliberately
# does not enter build.ps1 or the historical full-product synthetic receiver.
if ($Scope -eq 'PackageIntegrity') {
    if ($DeferGraphics -or $GraphicsDeferralReason) { throw 'PackageIntegrity does not run graphical checks.' }
    $arguments=@('-ZipPath',$ZipPath,'-BuildRecordPath',$BuildRecordPath,'-ExpectedSourceCommit',$ExpectedSourceCommit,'-ExpectedZipSha256',$ExpectedZipSha256)
    if($ApprovedRetainedAssets){$arguments+=@('-ApprovedRetainedAssets',$ApprovedRetainedAssets)}
    & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'verify-existing-package.ps1') @arguments
    exit $LASTEXITCODE
}
if ($ZipPath -or $BuildRecordPath -or $ExpectedSourceCommit -or $ExpectedZipSha256 -or $ApprovedRetainedAssets) {throw 'Package arguments require -Scope PackageIntegrity.'}
if ($Scope -eq 'InstallRecovery' -and ($DeferGraphics -or $GraphicsDeferralReason)) {throw 'InstallRecovery does not run graphical checks.'}

if ($DeferGraphics -and [string]::IsNullOrWhiteSpace($GraphicsDeferralReason)) {
    throw '-DeferGraphics requires an explicitly classified environment reason and task authorization.'
}
if (-not $DeferGraphics -and -not [string]::IsNullOrWhiteSpace($GraphicsDeferralReason)) {
    throw '-GraphicsDeferralReason requires -DeferGraphics; full checks remain the default.'
}

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$requiredProductionPaths = @(
    'scripts\phase0s\About-Help-Feedback-Owner-Test-Card.zh-CN.md',
    'scripts\phase0s\Install-Phase0S.ps1',
    'scripts\phase0s\Restore-Phase0S.ps1',
    'scripts\build-phase0s-validation-package.ps1',
    'scripts\build-phase0t-biome-validation-package.ps1',
    'scripts\phase0s\Phase0T-Biome-Owner-Test-Card.zh-CN.md',
    'scripts\phase0s\Phase0U-F5UI-Owner-Test-Card.zh-CN.md',
    'scripts\phase0s\Phase0V-Settings-Owner-Test-Card.zh-CN.md',
    'scripts\phase0s\Phase0W-Notes-Owner-Test-Card.zh-CN.md',
    'scripts\phase0s\Item-Automation-Owner-Test-Card.zh-CN.md',
    'scripts\phase0s\World-Object-Text-Owner-Test-Card.zh-CN.md',
    'scripts\phase0s\Information-Summary-Owner-Test-Card.zh-CN.md',
    'scripts\phase0s\Direction-Equipment-Owner-Test-Card.zh-CN.md',
    'scripts\phase0s\Death-History-Owner-Test-Card.zh-CN.md',
    'scripts\phase0s\Map-Markers-Exploration-Owner-Test-Card.zh-CN.md',
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
    $suites = switch ($Scope) {
        'LegacySynthetic' { 'Invoke-LoadChainFixture.ps1' }
        'InstallRecovery' { 'Invoke-InstallRecoveryTests.ps1' }
        default { 'Invoke-LoadChainFixture.ps1'; 'Invoke-InstallRecoveryTests.ps1' }
    }
    Write-Output ('SCOPE: ' + $Scope + '; synthetic/file-boundary evidence only; normal Terraria loading requires owner execution.')
    $failedSuites = @()
    foreach ($suiteScript in $suites) {
        $suiteArguments = @('-Run', '-RepositoryRoot', $repositoryRoot)
        if ($DeferGraphics -and $suiteScript -ceq 'Invoke-LoadChainFixture.ps1') {
            $suiteArguments += @('-DeferGraphics', '-GraphicsDeferralReason', $GraphicsDeferralReason)
        }
        & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $repositoryRoot ('tests\Phase0S\' + $suiteScript)) @suiteArguments
        if ($LASTEXITCODE -ne 0) {
            # Preserve the failure while allowing the independent file-layer
            # checks to report their own result. Neither can certify game load.
            $failedSuites += "$suiteScript exit $LASTEXITCODE"
        }
    }
    if ($failedSuites.Count -ne 0) {throw ($failedSuites -join '; ')}
    if ($DeferGraphics) {
        Write-Output ('PASS: selected ' + $Scope + ' non-graphical synthetic/file checks only.')
        Write-Output ('DEFERRED: Notes XNA pixels/clipping/state and F5 actual input/render consumers. Reason: ' + $GraphicsDeferralReason)
        Write-Output 'NOT COMPLETE: graphical checks and separate actual-XNB Notes previews remain pending; no full-suite PASS.'
    } else {
        Write-Output ('PASS: selected ' + $Scope + ' synthetic/file checks only; no normal-game load or owner acceptance conclusion.')
    }
}
catch {
    [Console]::Error.WriteLine('FAIL: Phase 0-S behavior tests failed: ' + $_.Exception.Message)
    exit 1
}
