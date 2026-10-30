[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $ContentDirectory,
    [Parameter(Mandatory = $true)][string] $OutputDirectory,
    [ValidateSet('BlueprintMirrorTrimVisual', 'BlueprintMirrorTrimCpu', 'BlueprintSupplyPresentationCpu', 'BlueprintSmartPlatformCpu', 'BlueprintActuatorNetworkCpu', 'BlueprintSupplySmartWiringCpu', 'BlueprintAppearanceCpu', 'BlueprintWiringCpu', 'BlueprintSpecialCpu', 'BlueprintPreparationCpu', 'BlueprintConstructionCpu', 'BlueprintMaterialsAutomationVisual', 'ItemsConsumerVisual', 'BrowserVisual', 'BlueprintMaterialsInput', 'BlueprintMaterialsVisual', 'BlueprintBuildContextCpu', 'BlueprintPersistenceCpu', 'BlueprintBuildThroughputCpu','BlueprintQualificationCpu', 'BlueprintBuildCpu', 'BlueprintWorldMatchCpu', 'BlueprintInput', 'BlueprintOriginal', 'BlueprintVisual', 'BlueprintCpu', 'MovementOriginal', 'MovementSmartFallCpu', 'MovementJumpCpu', 'MovementDashCpu', 'MovementTeleportCpu', 'ViewZoomSpeedCpu', 'ViewZoomSpeed', 'ViewCacheRange', 'ViewRangeConsumerCpu', 'ViewWaterRepair', 'ViewOriginal', 'ViewNetwork', 'ViewCombatCpu', 'Journey', 'JourneyReopen', 'JourneyKillCheck', 'JourneyCpu', 'JourneyPredictionCpu', 'NpcSync', 'NpcLocalFailure', 'NpcDisplayIsolation', 'NpcCloseoutTerrain', 'NpcStrategy', 'NpcStrategyContinuous', 'NpcEventRetirement', 'NpcEventRetirementCpu', 'Full', 'NpcWorkerIntegration', 'NpcWorkerCatalogue', 'NpcLegalCoverage', 'NpcWorkerLinked', 'NpcWorkerLifetime', 'NpcWorkerAssets', 'NpcWorkerRandom', 'NpcWorkerPlayer', 'NpcWorkerEntity', 'NpcWorkerBirth', 'NpcWorkerContext', 'NpcWorkerImmunity', 'NpcWorkerLifecycle', 'NpcWorkerFields', 'NpcWorkerTransport', 'NpcWorkerPreparation', 'NpcMenuPreparation', 'NpcSnapshot', 'NpcPostDelivery', 'NpcGuardianQuery', 'NpcModeledImpact', 'NpcNameDraw', 'NpcDiagnosticsOff', 'NpcSessionCapacity', 'NpcFailureRecovery', 'NpcProduction', 'NpcRollingCpu', 'NpcRollingSelectionNegative', 'NpcBasicMotion', 'NpcPhasedFlight', 'NpcAmbientFlight', 'NpcRelatedMotion','NpcTerrainFlight','NpcUmbrella','NpcPlantera','NpcBigMimic','NpcAquaticActions','NpcInvasionFlight','NpcInvasionGround','NpcEyeBoss','NpcSnowman','NpcSantaTank','NpcKingSlime','NpcSkeletonBoss','NpcInvasionFlightCpu','NpcInvasionFlightLoss','NpcBetsy','NpcTownCritter','NpcInvasionLightning','NpcDiveFlight','NpcAttachedWeapon','NpcDarkMage','NpcBloodNautilus','NpcGolem','NpcQueenBee','NpcBrainSwarm','NpcWallBoss','NpcTreeBoss','NpcCelestialTower','NpcPumpking','NpcIceQueen', 'NpcCultistBoss', 'NpcCultistRitual', 'NpcMoonLord', 'NpcMoonLeech', 'NpcQueenSlime', 'NpcWindyBalloon', 'NpcBird', 'NpcCaster', 'NpcMartianTurret', 'NpcSegmentedWorm', 'NpcSolitaryWorm', 'NpcSolarWorm', 'NpcEmpress', 'NpcDeerclops', 'NpcFishron', 'NpcFoundationRules', 'NpcFoundationContinuous', 'NpcPlayerPolicy', 'NpcSharedGeometry', 'NpcTargetMarker', 'NpcLongCoverage', 'NpcTileManifest', 'CombatPathContinuity', 'CombatCosts','CombatAttackCosts', 'CombatObservationCpu', 'CombatTerrainCpu', 'CombatAimCpu', 'CombatControlCpu', 'CombatNavigationCpu', 'CombatMechanicsCpu', 'CombatEffectsCpu', 'CombatCpu', 'CombatFacingCpu', 'CombatHitsCpu', 'CombatReportCpu', 'CombatUiCpu', 'CombatVisual', 'CombatHudVisual', 'CombatImpactVisual', 'CombatAimUi', 'CombatHints', 'CombatIntegrationCpu', 'CombatInputGeometryCpu', 'CombatSkyActionCpu', 'CombatRelease', 'SelectionCpuCosts', 'SelectionCpuChecks', 'FootprintsVisual', 'QuickItemsVisual', 'CoinDepositCpu', 'CoinDepositVisual', 'AboutCpu', 'AboutVisual', 'GuidanceCpu', 'PageCompositionCpu', 'ProcessingCpu', 'ProcessingVisual', 'ShortFeedbackCpu', 'ShortFeedbackVisual', 'ToolsCpu', 'ToolsVisual', 'ToolsTiming', 'ToolsBindings', 'ToolsExecution', 'FishingCpu', 'FishingVisual', 'BackgroundCpu', 'F5AutomationCpu')][string] $Scope = 'Full',
    [string] $WorkloadBaseline,
    [string] $ApprovedRetainedAssets
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (Test-Path -LiteralPath $OutputDirectory) { throw 'OutputDirectory must be new; preserve previous evidence.' }
if (-not (Test-Path -LiteralPath (Join-Path $ContentDirectory 'Fonts\Mouse_Text.xnb') -PathType Leaf)) { throw 'Actual matching Terraria Content directory is required.' }
# A neutral executable loads fixed EXE metadata and original XNB resources. It
# does not use ordinary full-game startup or read player saves. Some scopes
# initialize controlled native state and execute their declared original phases.
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
$previousViewCandidate = [Environment]::GetEnvironmentVariable('JUEMINGR_VIEW_NETWORK_CANDIDATE')
$previousOnly = [Environment]::GetEnvironmentVariable('JUEMINGR_STRATEGY_ONLY')
$cacheControls=@{}
foreach($name in @('JUEMINGR_VIEW_CACHE_FOCUS','JUEMINGR_VIEW_CACHE_PROFILE','JUEMINGR_VIEW_CACHE_BASELINE')){$cacheControls[$name]=[Environment]::GetEnvironmentVariable($name)}
New-Item -ItemType Directory -Path $OutputDirectory -ErrorAction Stop | Out-Null
try {
    if($Scope -in @('ViewCacheRange','ViewZoomSpeed')){foreach($name in $cacheControls.Keys){[Environment]::SetEnvironmentVariable($name,$null)}}
    if ($Scope -in @('BlueprintOriginal','MovementOriginal','ViewZoomSpeed','ViewCacheRange','ViewWaterRepair','ViewOriginal','ViewNetwork')) {
        # Full native assets need the original client's >2GB address space.
        # Patch only a disposable neutral test executable, never the fixture
        # cache, original Terraria binary, product, or installed payload.
        $fixtureProbe=$probe
        $probeRoot=Join-Path $OutputDirectory 'probe'
        New-Item -ItemType Directory -Path $probeRoot | Out-Null
        Get-ChildItem -LiteralPath (Split-Path $fixtureProbe) -File | Copy-Item -Destination $probeRoot
        $probe=Join-Path $probeRoot (Split-Path $fixtureProbe -Leaf)
        $bytes=[IO.File]::ReadAllBytes($probe);$pe=[BitConverter]::ToInt32($bytes,60)
        if([BitConverter]::ToUInt16($bytes,$pe+4) -ne 332){throw 'View specialist requires an I386 probe.'}
        $bytes[$pe+22]=$bytes[$pe+22] -bor 32;[IO.File]::WriteAllBytes($probe,$bytes)
        New-Item -ItemType Junction -Path (Join-Path $probeRoot 'Content') -Target ([IO.Path]::GetFullPath($ContentDirectory)) | Out-Null
        New-Item -ItemType Junction -Path (Join-Path $OutputDirectory 'Content') -Target ([IO.Path]::GetFullPath($ContentDirectory)) | Out-Null
        $native=Join-Path (Split-Path ([IO.Path]::GetFullPath($ContentDirectory))) 'ReLogic.Native.dll'
        if(-not(Test-Path -LiteralPath $native -PathType Leaf)){throw 'Matching original ReLogic.Native.dll required for native assets.'}
        Copy-Item -LiteralPath $native -Destination (Join-Path $OutputDirectory 'ReLogic.Native.dll')
        $candidate=Join-Path $repositoryRoot 'artifacts/build/Debug/work/bin/JueMingR.TerrariaHost/x86/Debug/net472/JueMingR.TerrariaHost.dll'
        if($Scope -in @('ViewZoomSpeed','ViewNetwork','ViewCacheRange')){[Environment]::SetEnvironmentVariable('JUEMINGR_VIEW_NETWORK_CANDIDATE',$candidate)}
        [ordered]@{schema='view-native-specialist-1';scope=$Scope;fixtureSha256=(Get-FileHash -LiteralPath $fixtureProbe).Hash;executedProbeSha256=(Get-FileHash -LiteralPath $probe).Hash;originalExeSha256=(Get-FileHash -LiteralPath (Join-Path $repositoryRoot 'external/TerrariaRefs/Terraria.exe')).Hash;nativeDllSha256=(Get-FileHash -LiteralPath $native).Hash;candidateSha256=(Get-FileHash -LiteralPath $candidate).Hash;laa='test-copy-only';skipAssemblyLoad='original test flag skips all-assembly preload; native Initialize/LoadContent remain';capture='Reach physical-size final sink; native intermediate targets/Draw retained';ordinaryPackageLoad='NOT_EVALUATED'} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'native-inputs.json') -Encoding UTF8
    }
    if ($Scope -eq 'NpcStrategy') { [Environment]::SetEnvironmentVariable('JUEMINGR_STRATEGY_ONLY',$null) }
    & $probe $repositoryRoot $ContentDirectory $OutputDirectory $Scope
} finally { [Environment]::SetEnvironmentVariable('JUEMINGR_STRATEGY_ONLY',$previousOnly); [Environment]::SetEnvironmentVariable('JUEMINGR_VIEW_NETWORK_CANDIDATE',$previousViewCandidate);foreach($name in $cacheControls.Keys){[Environment]::SetEnvironmentVariable($name,$cacheControls[$name])} }
if ($LASTEXITCODE -ne 0) { throw 'Native world text check failed. A draw/layout failure is not an environment deferral.' }
if ($Scope -ceq 'CombatAimUi') {
    & $probe $repositoryRoot $ContentDirectory $OutputDirectory 'CombatAimUiReload'
    if ($LASTEXITCODE -ne 0) { throw 'Native Aim binding/configuration reload check failed.' }
}
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
