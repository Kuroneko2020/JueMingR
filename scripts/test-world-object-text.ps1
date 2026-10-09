[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $ContentDirectory,
    [Parameter(Mandatory = $true)][string] $OutputDirectory,
    [ValidateSet('NpcSync', 'NpcLocalFailure', 'NpcDisplayIsolation', 'NpcCloseoutTerrain', 'NpcStrategy', 'NpcStrategyContinuous', 'NpcEventRetirement', 'NpcEventRetirementCpu', 'Full', 'NpcWorkerIntegration', 'NpcWorkerCatalogue', 'NpcLegalCoverage', 'NpcWorkerLinked', 'NpcWorkerLifetime', 'NpcWorkerAssets', 'NpcWorkerRandom', 'NpcWorkerPlayer', 'NpcWorkerEntity', 'NpcWorkerBirth', 'NpcWorkerContext', 'NpcWorkerImmunity', 'NpcWorkerLifecycle', 'NpcWorkerFields', 'NpcWorkerTransport', 'NpcWorkerPreparation', 'NpcMenuPreparation', 'NpcSnapshot', 'NpcPostDelivery', 'NpcGuardianQuery', 'NpcModeledImpact', 'NpcNameDraw', 'NpcDiagnosticsOff', 'NpcSessionCapacity', 'NpcFailureRecovery', 'NpcProduction', 'NpcRollingCpu', 'NpcRollingSelectionNegative', 'NpcBasicMotion', 'NpcFoundationRules', 'NpcFoundationContinuous', 'NpcPlayerPolicy', 'NpcSharedGeometry', 'NpcTargetMarker', 'NpcLongCoverage', 'NpcTileManifest', 'CombatCosts', 'CombatObservationCpu', 'CombatCpu', 'CombatFacingCpu', 'CombatHitsCpu', 'CombatReportCpu', 'CombatUiCpu', 'CombatVisual', 'CombatRelease', 'SelectionCpuCosts', 'SelectionCpuChecks', 'FootprintsVisual', 'QuickItemsVisual', 'CoinDepositCpu', 'CoinDepositVisual', 'AboutCpu', 'AboutVisual', 'ProcessingCpu', 'ProcessingVisual', 'ShortFeedbackCpu', 'ShortFeedbackVisual', 'ToolsCpu', 'ToolsVisual', 'ToolsTiming', 'ToolsBindings', 'ToolsExecution', 'FishingCpu', 'FishingVisual', 'BackgroundCpu', 'F5AutomationCpu')][string] $Scope = 'Full',
    [string] $WorkloadBaseline,
    [string] $ApprovedRetainedAssets
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (Test-Path -LiteralPath $OutputDirectory) { throw 'OutputDirectory must be new; preserve previous evidence.' }
if (-not (Test-Path -LiteralPath (Join-Path $ContentDirectory 'Fonts\Mouse_Text.xnb') -PathType Leaf)) { throw 'Actual matching Terraria Content directory is required.' }
# A neutral executable loads fixed EXE metadata and original XNB resources. It
# never constructs/initializes/runs Main, starts a server or reads player saves.
# This is an explicit specialist scope, never a cumulative delivery gate.
# Shared compile/structure preparation is reusable and does not run business.
if($WorkloadBaseline){throw 'WorkloadBaseline belongs to build/workload feedback, not an explicit specialist scope.'}
& (Join-Path $PSScriptRoot 'build.ps1') -Configuration Debug -PrepareOnly -ApprovedRetainedAssets $ApprovedRetainedAssets
if ($LASTEXITCODE -ne 0) { throw 'Debug build failed.' }
. (Join-Path $PSScriptRoot 'workload/Workload.Support.ps1')
$null=Initialize-WorkloadWorkspace $repositoryRoot $ApprovedRetainedAssets
$identity=Get-WorkloadIdentity $repositoryRoot
$inputs=Get-WorkloadEvidenceInput $repositoryRoot $identity
$probe=Ensure-WorkloadFixture $repositoryRoot 'NativeWorldTextProbe' $inputs
$previousOnly = [Environment]::GetEnvironmentVariable('JUEMINGR_STRATEGY_ONLY')
try {
    if ($Scope -eq 'NpcStrategy') { [Environment]::SetEnvironmentVariable('JUEMINGR_STRATEGY_ONLY',$null) }
    & $probe $repositoryRoot $ContentDirectory $OutputDirectory $Scope
} finally { [Environment]::SetEnvironmentVariable('JUEMINGR_STRATEGY_ONLY',$previousOnly) }
if ($LASTEXITCODE -ne 0) { throw 'Native world text check failed. A draw/layout failure is not an environment deferral.' }
if ($Scope -ceq 'ToolsBindings') {
    & $probe $repositoryRoot $ContentDirectory $OutputDirectory 'ToolsBindingsReload'
    if ($LASTEXITCODE -ne 0) { throw 'Native binding reload/dispatch check failed.' }
}
$inputs = @(& git -C $repositoryRoot ls-files -- src tests/NativeWorldTextProbe | ForEach-Object {
    $file = Join-Path $repositoryRoot $_
    [ordered]@{ path = $_; sha256 = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash }
})
if ($LASTEXITCODE -ne 0) { throw 'Source identity read failed.' }
$inputs | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'source-inputs.json') -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'artifacts\build\Debug\build-record.json') -Destination (Join-Path $OutputDirectory 'source-build-record.json')
if($identity.fingerprint -cne (Get-WorkloadIdentity $repositoryRoot).fingerprint){throw 'Specialist inputs changed during execution.'}
Write-Output ('SPECIALIST PASS: '+$Scope+'; exactly declared scope; cumulative delivery NOT_EVALUATED.')
