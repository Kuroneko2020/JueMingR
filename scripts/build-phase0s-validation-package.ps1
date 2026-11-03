[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $OutputDirectory,
    [ValidateSet('Phase0S', 'Phase0TBiome', 'Phase0UF5UI', 'Phase0VSettings', 'Phase0WNotes', 'ItemAutomation', 'UnifiedHotkeys', 'EntityLabels', 'WorldTargets', 'WorldObjectText', 'InformationSummary', 'DirectionEquipment', 'DeathHistory', 'MapMarkersExploration', 'Footprints', 'ItemBrowser', 'KeepFavoritedQuickItems', 'CoinDeposit', 'AboutHelpFeedback', 'RecoveryBuffsServices', 'ContinuousProcessing')]
    [string] $Profile = 'Phase0S',
    [string] $WorkloadBaseline,
    [string] $ApprovedRetainedAssets,
    [string] $StageDevelopmentRecord,
    [switch] $BlueprintDiagnostic,
    [switch] $TargetGestureDiagnostic,
    [switch] $Rebuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd('\')
. (Join-Path $PSScriptRoot 'phase0s\Phase0S.ScriptSupport.ps1')
. (Join-Path $PSScriptRoot 'workload/Workload.Support.ps1')
if($StageDevelopmentRecord -and ($Profile -cne 'ContinuousProcessing' -or $WorkloadBaseline)){throw 'This explicit stage uses the existing complete ContinuousProcessing profile without formal workload selectors.'}
if($BlueprintDiagnostic -and ($Profile -cne 'ContinuousProcessing' -or -not $StageDevelopmentRecord)){throw 'BlueprintDiagnostic requires the existing complete ContinuousProcessing owner-test candidate record.'}
if($TargetGestureDiagnostic -and ($Profile -cne 'ContinuousProcessing' -or -not $StageDevelopmentRecord -or $BlueprintDiagnostic)){throw 'TargetGestureDiagnostic requires its separate complete ContinuousProcessing owner-test candidate record.'}
function Test-PackageBuildAdmission($Record,[bool]$ExpectedBlueprintDiagnostic=$false,[bool]$ExpectedTargetGestureDiagnostic=$false) {
    $recordTargetDiagnostic=$null -ne $Record -and $null -ne $Record.PSObject.Properties['targetGestureDiagnostic'] -and [bool]$Record.targetGestureDiagnostic
    if($recordTargetDiagnostic -ne $ExpectedTargetGestureDiagnostic){return $false}
    $recordDiagnostic=$null -ne $Record -and $null -ne $Record.PSObject.Properties['blueprintDiagnostic'] -and [bool]$Record.blueprintDiagnostic
    if($recordDiagnostic -ne $ExpectedBlueprintDiagnostic){return $false}
    if($StageDevelopmentRecord){return Test-AimStageDevelopmentBuild $repositoryRoot $Record $StageDevelopmentRecord $admission}
    return Test-WorkloadDelivery $repositoryRoot $Record $WorkloadBaseline -RetainedAssets $admission
}
function Invoke-Phase0SGit {
    param([Parameter(Mandatory = $true)][string[]] $Arguments)

    $output = @(& git -C $repositoryRoot @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw 'A required Git query failed.'
    }
    return $output
}

function Copy-Phase0SBuilderFileCreateNew {
    param(
        [Parameter(Mandatory = $true)][string] $SourcePath,
        [Parameter(Mandatory = $true)][string] $DestinationPath
    )

    if (-not (Test-Phase0SOrdinaryFile -Path $SourcePath)) {
        throw 'A fixed package source is not an ordinary file.'
    }
    $sourceLength = (Get-Item -LiteralPath $SourcePath -Force -ErrorAction Stop).Length
    $sourceHash = Get-Phase0SFileSha256 -Path $SourcePath
    $source = [System.IO.File]::Open($SourcePath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
    try {
        $destination = [System.IO.File]::Open($DestinationPath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        try {
            $source.CopyTo($destination)
            $destination.Flush($true)
        }
        finally {
            $destination.Dispose()
        }
    }
    finally {
        $source.Dispose()
    }
    $destinationItem = Get-Item -LiteralPath $DestinationPath -Force -ErrorAction Stop
    if ($destinationItem.Length -ne $sourceLength -or (Get-Phase0SFileSha256 -Path $DestinationPath) -cne $sourceHash) {
        throw 'A fixed package source changed while being copied.'
    }
}

function Write-Phase0SBuilderTextCreateNew {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string] $Text
    )

    $bytes = (New-Object System.Text.UTF8Encoding($false, $true)).GetBytes($Text)
    Copy-Phase0SBytesCreateNew -Bytes $bytes -DestinationPath $Path
}

function Get-Phase0SBuildOutputPath {
    param(
        [Parameter(Mandatory = $true)][string] $ProjectName,
        [Parameter(Mandatory = $true)][string] $FileName
    )

    $path = Join-Path $repositoryRoot ('artifacts\build\Release\work\bin\' + $ProjectName + '\x86\Release\net472\' + $FileName)
    if (-not (Test-Phase0SOrdinaryFile -Path $path)) {
        throw 'A required Release build output is missing.'
    }
    return $path
}

$gitRoot = [string] (Invoke-Phase0SGit -Arguments @('rev-parse', '--show-toplevel') | Select-Object -First 1)
if ([System.IO.Path]::GetFullPath($gitRoot.Trim()).TrimEnd('\') -cne $repositoryRoot) {
    throw 'The script directory is not the active Git repository root.'
}
$status = @(Invoke-Phase0SGit -Arguments @('status', '--porcelain=v1', '--untracked-files=all'))
$admission=Initialize-WorkloadWorkspace $repositoryRoot $ApprovedRetainedAssets -RequireCommitted
$sourceIdentity=Get-WorkloadIdentity $repositoryRoot
if ($status.Count -ne 0 -and $null -eq $admission) {
    throw 'Phase 0-S owner package requires a clean commit.'
}
$sourceCommit = ([string] (Invoke-Phase0SGit -Arguments @('rev-parse', 'HEAD') | Select-Object -First 1)).Trim()
if ($sourceCommit -notmatch '^[0-9a-f]{40}$') {
    throw 'The source commit identity is invalid.'
}
$packageId = $(if ($Profile -eq 'ContinuousProcessing') { 'continuous-processing-' } elseif ($Profile -eq 'RecoveryBuffsServices') { 'recovery-buffs-services-' } elseif ($Profile -eq 'AboutHelpFeedback') { 'about-help-feedback-' } elseif ($Profile -eq 'CoinDeposit') { 'coin-deposit-' } elseif ($Profile -eq 'KeepFavoritedQuickItems') { 'favorite-quick-items-' } elseif ($Profile -eq 'ItemBrowser') { 'item-browser-' } elseif ($Profile -eq 'Footprints') { 'footprints-' } elseif ($Profile -eq 'MapMarkersExploration') { 'map-markers-exploration-' } elseif ($Profile -eq 'DeathHistory') { 'death-history-' } elseif ($Profile -eq 'DirectionEquipment') { 'direction-equipment-' } elseif ($Profile -eq 'InformationSummary') { 'information-summary-' } elseif ($Profile -eq 'WorldObjectText') { 'world-object-text-' } elseif ($Profile -eq 'WorldTargets') { 'world-targets-' } elseif ($Profile -eq 'EntityLabels') { 'entity-labels-' } elseif ($Profile -eq 'UnifiedHotkeys') { 'unified-hotkeys-' } elseif ($Profile -eq 'ItemAutomation') { 'item-automation-' } elseif ($Profile -eq 'Phase0WNotes') { 'phase0w-notes-' } elseif ($Profile -eq 'Phase0VSettings') { 'phase0v-settings-' } elseif ($Profile -eq 'Phase0UF5UI') { 'phase0u-f5-ui-' } elseif ($Profile -eq 'Phase0TBiome') { 'phase0t-biome-' } else { 'phase0s-' }) + $sourceCommit

$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\')
if ([string]::IsNullOrWhiteSpace($outputRoot) -or (Get-Phase0SPathState -Path $outputRoot).exists) {
    throw 'OutputDirectory must name a new directory.'
}
$outputParent = Split-Path -Parent $outputRoot
if (-not (Test-Phase0SOrdinaryDirectory -Path $outputParent)) {
    throw 'OutputDirectory parent must be an existing ordinary directory.'
}
$repositoryPrefix = $repositoryRoot + '\'
if ($outputRoot.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    $outputsPrefix = (Join-Path $repositoryRoot 'outputs').TrimEnd('\') + '\'
    $artifactsPrefix = (Join-Path $repositoryRoot 'artifacts').TrimEnd('\') + '\'
    if (-not $outputRoot.StartsWith($outputsPrefix, [System.StringComparison]::OrdinalIgnoreCase) -and
        -not $outputRoot.StartsWith($artifactsPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'Repository-local output must be beneath outputs or artifacts.'
    }
}

# One bounded phase shares parsed records and digests; rebuild and promotion
# each cross a fresh verification boundary. Leases forbid in-phase replacement.
Start-WorkloadReadWindow
try {
$buildRecordPath = Join-Path $repositoryRoot 'artifacts\build\Release\build-record.json'
$existingBuild = Read-WorkloadJson $buildRecordPath
$buildOutput = @()
if (-not $Rebuild -and (Test-PackageBuildAdmission $existingBuild -ExpectedBlueprintDiagnostic ([bool]$BlueprintDiagnostic) -ExpectedTargetGestureDiagnostic ([bool]$TargetGestureDiagnostic))) {
    Write-Host ('REUSED verified Release build; admission='+$(if($StageDevelopmentRecord){'explicit stage, full qualification INCOMPLETE'}else{'complete applicable check evidence'}))
} else {
    Stop-WorkloadReadWindow
    $buildArguments=@{Configuration='Release';WorkloadBaseline=$WorkloadBaseline}
    if($StageDevelopmentRecord){$buildArguments.StageDevelopmentRecord=$StageDevelopmentRecord}
    if($BlueprintDiagnostic){$buildArguments.BlueprintDiagnostic=$true}
    if($TargetGestureDiagnostic){$buildArguments.TargetGestureDiagnostic=$true}
    if($null -eq $admission){$buildArguments.RequireClean=$true}else{$buildArguments.ApprovedRetainedAssets=$ApprovedRetainedAssets}
    $buildOutput = @(& (Join-Path $PSScriptRoot 'build.ps1') @buildArguments 2>&1)
    if (-not $?) { throw 'The locked Release build failed.' }
    Start-WorkloadReadWindow
}
$statusAfterBuild = @(Invoke-Phase0SGit -Arguments @('status', '--porcelain=v1', '--untracked-files=all'))
# Bind packaging to the same clean commit across the build, not merely its earlier HEAD label.
$headAfterBuild = ([string] (Invoke-Phase0SGit -Arguments @('rev-parse', 'HEAD') | Select-Object -First 1)).Trim()
if (($null -eq $admission -and $statusAfterBuild.Count -ne 0) -or $headAfterBuild -cne $sourceCommit -or $sourceIdentity.fingerprint -cne (Get-WorkloadIdentity $repositoryRoot).fingerprint) {
    throw 'The source tree changed during the Release build.'
}

$buildRecordPath = Join-Path $repositoryRoot 'artifacts\build\Release\build-record.json'
$buildRecord = Read-WorkloadJson $buildRecordPath
if ([int] $buildRecord.schemaVersion -ne 4 -or [string] $buildRecord.commit -cne $sourceCommit -or
    (-not [bool] $buildRecord.clean -and -not (Test-WorkloadRestrictedRecord $buildRecord $admission)) -or [string] $buildRecord.sdk -cne '10.0.203' -or
    [string] $buildRecord.configuration -cne 'Release') {
    throw 'The Release build record does not describe the clean source commit.'
}
if (-not (Test-PackageBuildAdmission $buildRecord -ExpectedBlueprintDiagnostic ([bool]$BlueprintDiagnostic) -ExpectedTargetGestureDiagnostic ([bool]$TargetGestureDiagnostic))) {
    throw 'The Release package requires the completed matching automatic workload gate.'
}
if (@($buildRecord.outputs | Where-Object { [string] $_.path -match '(?i)(^|\\)0Harmony\.dll$' }).Count -ne 0) {
    throw 'Ordinary Release output unexpectedly contains Harmony.'
}


$hostOutput = Get-Phase0SBuildOutputPath -ProjectName 'JueMingR.TerrariaHost' -FileName 'JueMingR.TerrariaHost.dll'
$platformOutput = Get-Phase0SBuildOutputPath -ProjectName 'JueMingR.Platform' -FileName 'JueMingR.Platform.dll'
$featuresOutput = Get-Phase0SBuildOutputPath -ProjectName 'JueMingR.Features' -FileName 'JueMingR.Features.dll'
$infrastructureOutput = Get-Phase0SBuildOutputPath -ProjectName 'JueMingR.Infrastructure' -FileName 'JueMingR.Infrastructure.dll'
$workerOutput = Get-Phase0SBuildOutputPath -ProjectName 'JueMingR.PredictionWorker' -FileName 'JueMingR.PredictionWorker.exe'
$workerConfigOutput = Get-Phase0SBuildOutputPath -ProjectName 'JueMingR.PredictionWorker' -FileName 'JueMingR.PredictionWorker.exe.config'
$hostIdentity = Get-Phase0SAssemblyFileIdentity -Path $hostOutput
if ($hostIdentity.simpleName -cne 'JueMingR.TerrariaHost' -or $hostIdentity.version -cne '0.0.0.0') {
    throw 'The Host Release output identity is invalid.'
}

$harmonyPath = Join-Path $repositoryRoot 'external\Harmony\0Harmony.dll'
$harmonyLicensePath = Join-Path $repositoryRoot 'external\Harmony\LICENSE'
$harmonyIdentity = Get-Phase0SAssemblyFileIdentity -Path $harmonyPath
if ($harmonyIdentity.fullName -cne '0Harmony, Version=2.4.2.0, Culture=neutral, PublicKeyToken=null' -or
    $harmonyIdentity.mvid -cne '024a0e6e-c8c2-437e-ad04-7b6279389c23' -or
    $harmonyIdentity.sha256 -cne '7B9E756306FA3D7620E02A857C8927A6AB04973F9BD8A77D3866700A6DEAC55C' -or
    (Get-Phase0SFileSha256 -Path $harmonyLicensePath) -cne '407DDD98200C9F17F49942B5A7791E288A2BD553B59BF2F462DB87DBB21C50BC') {
    throw 'Prepared Harmony identity or license is invalid.'
}

$packageDirectoryName = $(if ($Profile -eq 'ContinuousProcessing') { 'JueMingR-Continuous-Processing-' } elseif ($Profile -eq 'RecoveryBuffsServices') { 'JueMingR-Recovery-Buffs-Services-' } elseif ($Profile -eq 'AboutHelpFeedback') { 'JueMingR-About-Help-Feedback-' } elseif ($Profile -eq 'CoinDeposit') { 'JueMingR-Coin-Deposit-' } elseif ($Profile -eq 'KeepFavoritedQuickItems') { 'JueMingR-Keep-Favorited-Quick-Items-' } elseif ($Profile -eq 'ItemBrowser') { 'JueMingR-Item-Browser-' } elseif ($Profile -eq 'Footprints') { 'JueMingR-Footprints-' } elseif ($Profile -eq 'MapMarkersExploration') { 'JueMingR-Map-Markers-Exploration-' } elseif ($Profile -eq 'DeathHistory') { 'JueMingR-Death-History-' } elseif ($Profile -eq 'DirectionEquipment') { 'JueMingR-Direction-Equipment-' } elseif ($Profile -eq 'InformationSummary') { 'JueMingR-Information-Summary-' } elseif ($Profile -eq 'WorldObjectText') { 'JueMingR-World-Object-Text-' } elseif ($Profile -eq 'WorldTargets') { 'JueMingR-World-Targets-' } elseif ($Profile -eq 'EntityLabels') { 'JueMingR-Entity-Labels-' } elseif ($Profile -eq 'UnifiedHotkeys') { 'JueMingR-Unified-Hotkeys-' } elseif ($Profile -eq 'ItemAutomation') { 'JueMingR-Item-Automation-' } elseif ($Profile -eq 'Phase0WNotes') { 'JueMingR-Phase0W-Notes-' } elseif ($Profile -eq 'Phase0VSettings') { 'JueMingR-Phase0V-Settings-' } elseif ($Profile -eq 'Phase0UF5UI') { 'JueMingR-Phase0U-F5UI-' } elseif ($Profile -eq 'Phase0TBiome') { 'JueMingR-Phase0T-Biome-' } else { 'JueMingR-Phase0S-' }) + $sourceCommit
. (Join-Path $PSScriptRoot 'TwoFileBundle.Support.ps1')
$stagingRoot=Join-Path $outputParent ((Split-Path -Leaf $outputRoot)+'.bundle-new-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($stagingRoot)|Out-Null
$internal=Join-Path $stagingRoot '.build/internal'
[IO.Directory]::CreateDirectory($internal)|Out-Null
foreach($path in @($hostOutput,$platformOutput,$featuresOutput,$infrastructureOutput,$workerOutput,$workerConfigOutput,$harmonyPath)){
    Copy-Phase0SBuilderFileCreateNew $path (Join-Path $internal (Split-Path -Leaf $path))
}
Copy-Phase0SBuilderFileCreateNew $harmonyLicensePath (Join-Path $internal 'Harmony-2.4.2-LICENSE.txt')
Copy-Phase0SBuilderFileCreateNew (Join-Path $repositoryRoot 'THIRD-PARTY-NOTICES.md') (Join-Path $internal 'THIRD-PARTY-NOTICES.md')
Write-JmrRuntimeManifest -Internal $internal -SourceCommit $sourceCommit -PackageId $packageId -HostIdentity $hostIdentity -HarmonyIdentity $harmonyIdentity
$standard=Join-Path $stagingRoot $packageDirectoryName
$bundle=New-JmrTwoFileBundle -RepositoryRoot $repositoryRoot -InputDirectory $internal -WorkDirectory (Join-Path $stagingRoot '.build/bundle') -StandardDirectory $standard -SourceCommit $sourceCommit
$zipFileName=$packageDirectoryName+'.zip'
$zipPath=Join-Path $stagingRoot $zipFileName
New-JmrTwoFileZip $standard $zipPath
$setupBuildRoot=Join-Path $stagingRoot '.build/setup'
$setupOutput=@(& dotnet.exe build (Join-Path $repositoryRoot 'src/JueMingR.Setup/JueMingR.Setup.csproj') --configuration Release --nologo -p:Platform=x86 "-p:SourceRevisionId=$sourceCommit" "-p:JueMingRSetupPayloadDirectory=$standard" "-p:JueMingRBuildRoot=$setupBuildRoot" 2>&1)
$setupOutput|ForEach-Object{Write-Host $_}
if($LASTEXITCODE -ne 0){throw 'The final single-file Setup did not build.'}
$setupFileName=$packageDirectoryName+'-Setup.exe'
$setupPath=Join-Path $stagingRoot $setupFileName
Copy-Phase0SBuilderFileCreateNew (Join-Path $setupBuildRoot 'bin/JueMingR.Setup/x86/Release/net472/JueMingR.Setup.exe') $setupPath
$setupResources=@(foreach($file in $bundle.files){[ordered]@{name=('JueMingR.Setup.Payload.'+$file.path);path=$file.path;length=$file.length;sha256=$file.sha256}})
$externalBuildRecord=[ordered]@{
    schemaVersion=1;distributionFormat='two-file-1';sourceCommit=$sourceCommit;clean=($null -eq $admission);
    workspaceDirty=($null -ne $admission);committedProductSourceClean=$true;releaseEligible=($null -eq $admission -and -not $StageDevelopmentRecord);
    workspace=$admission;packageId=$packageId;sdk='10.0.203';configuration='Release';blueprintDiagnostic=[bool]$BlueprintDiagnostic;targetGestureDiagnostic=[bool]$TargetGestureDiagnostic;
    targetFramework='net472';platformTarget='x86';buildEntry='scripts/build.ps1';solution='JueMingR.sln';
    sourceBuildRecordSha256=(Get-Phase0SFileSha256 $buildRecordPath);sourceFingerprint=$buildRecord.sourceFingerprint;workload=$buildRecord.workload;
    target=[ordered]@{simpleName='Terraria';version='1.4.5.8';mvid='2c29f6c3-4bd9-4add-9c58-da159804e083';sha256='960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3'};
    harmony=[ordered]@{package='Lib.Harmony';version='2.4.2';assemblyMvid=$harmonyIdentity.mvid;assemblySha256=$harmonyIdentity.sha256;licenseSha256=(Get-Phase0SFileSha256 $harmonyLicensePath)};
    packageDirectory=[ordered]@{name='';files=@($bundle.files)};bundle=$bundle;
    archive=[ordered]@{fileName=$zipFileName;length=[long](Get-Item -LiteralPath $zipPath).Length;sha256=(Get-Phase0SFileSha256 $zipPath)};
    setup=[ordered]@{fileName=$setupFileName;length=[long](Get-Item -LiteralPath $setupPath).Length;sha256=(Get-Phase0SFileSha256 $setupPath);resources=$setupResources};
    generatedAtUtc=[DateTime]::UtcNow.ToString('O',[Globalization.CultureInfo]::InvariantCulture)
}
$recordPath=Join-Path $stagingRoot 'phase-0-s-build-record.json'
Write-Phase0SBuilderTextCreateNew $recordPath (($externalBuildRecord|ConvertTo-Json -Depth 12)+"`n")
# The public consumer verifies both independently produced channels before promotion.
& powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'verify-existing-package.ps1') -BuildRecordPath $recordPath -ZipPath $zipPath -SetupPath $setupPath -ExpectedSourceCommit $sourceCommit -ExpectedZipSha256 $externalBuildRecord.archive.sha256 -ApprovedRetainedAssets $ApprovedRetainedAssets
if($LASTEXITCODE -ne 0){throw 'The two distribution channels failed their public consumer.'}
Stop-WorkloadReadWindow
Start-WorkloadReadWindow
$promotionAdmission=Initialize-WorkloadWorkspace $repositoryRoot $ApprovedRetainedAssets -RequireCommitted
if($sourceIdentity.fingerprint -cne (Get-WorkloadIdentity $repositoryRoot).fingerprint -or
    ($admission|ConvertTo-Json -Depth 8 -Compress) -cne ($promotionAdmission|ConvertTo-Json -Depth 8 -Compress) -or
    -not (Test-PackageBuildAdmission $buildRecord -ExpectedBlueprintDiagnostic ([bool]$BlueprintDiagnostic) -ExpectedTargetGestureDiagnostic ([bool]$TargetGestureDiagnostic))){throw 'Package inputs or evidence changed before promotion.'}
# Keep build intermediates and verification as package-external developer evidence.
[IO.Directory]::Move($stagingRoot,$outputRoot)
[ordered]@{schemaVersion=1;status='success';packageId=$packageId;directoryName=$packageDirectoryName;archiveFileName=$zipFileName;archiveSha256=$externalBuildRecord.archive.sha256;setupFileName=$setupFileName;setupSha256=$externalBuildRecord.setup.sha256;buildRecordFileName='phase-0-s-build-record.json'}|ConvertTo-Json -Compress
} finally { Stop-WorkloadReadWindow }
