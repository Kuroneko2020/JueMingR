[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2.0
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $repository 'scripts/workload/Workload.Support.ps1')
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('JueMingR-content-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixture)|Out-Null
$checks=0
function Assert-Content([bool]$Pass,[string]$Reason){if(!$Pass){throw ('Native Content: '+$Reason)};$script:checks++}
function Reject-Content([scriptblock]$Action,[string]$Reason){$rejected=$false;try{& $Action|Out-Null}catch{$rejected=$true};Assert-Content $rejected $Reason}
function Put-ContentFixture([string]$Relative,[string]$Value){$p=Join-Path $fixture $Relative;[IO.Directory]::CreateDirectory((Split-Path -Parent $p))|Out-Null;[IO.File]::WriteAllText($p,$Value)}
try{
    Put-ContentFixture 'external/TerrariaRefs/Terraria.exe' 'identity fixture only; never executed'
    Put-ContentFixture 'original/Terraria.exe' 'identity fixture only; never executed'
    $gameHash=(Get-FileHash (Join-Path $fixture 'original/Terraria.exe')).Hash
    $baseline=[ordered]@{profileId='controlled-content-fixture';files=@(@{logicalName='Terraria.exe';sha256=$gameHash})}
    Put-ContentFixture 'eng/TerrariaReferences.baseline.json' ($baseline|ConvertTo-Json -Depth 4)
    $receipt=[ordered]@{profileId=$baseline.profileId;baselineSha256=(Get-FileHash (Join-Path $fixture 'eng/TerrariaReferences.baseline.json')).Hash;sourceHashesUnchanged=$true;source=@{terrariaInstallDirectory=(Join-Path $fixture 'original')};files=@(@{logicalName='Terraria.exe';sha256=$gameHash})}
    Put-ContentFixture 'external/TerrariaRefs/.juemingr-reference-set.json' ($receipt|ConvertTo-Json -Depth 4)
    foreach($asset in @(Get-WorkloadNativeContentScopes|ForEach-Object{Get-WorkloadNativeContentFiles $_}|Sort-Object -Unique)){Put-ContentFixture ('original/Content/'+$asset) ('controlled asset '+$asset)}
    foreach($scope in @(Get-WorkloadNativeContentScopes)){
        $plan=@(Get-WorkloadPlan $fixture 'checks' 'architecture.exe' @() @('check:native-'+$scope))
        Assert-Content ($plan.Count -eq 1 -and $plan[0].arguments[1] -ceq (Join-Path $fixture 'original/Content') -and $plan[0].arguments[-1] -ceq $scope) ('actual matching Content argv '+$scope)
        $name='native-'+$scope
        $before=[pscustomobject]@{inputs=@('scripts/workload/Workload.NativeContent.ps1:A','native-content/file/Fonts/Mouse_Text.xnb:A')}
        $after=[pscustomobject]@{inputs=@('scripts/workload/Workload.NativeContent.ps1:B','native-content/file/Fonts/Mouse_Text.xnb:B')}
        Assert-Content ((Get-WorkloadCheckFingerprint $before $name) -cne (Get-WorkloadCheckFingerprint $after $name)) ('bound validation owner/resource change invalidates consumer '+$scope)
        $ownerOnly=[pscustomobject]@{inputs=@('scripts/workload/Workload.NativeContent.ps1:B','native-content/file/Fonts/Mouse_Text.xnb:A')}
        Assert-Content ((Get-WorkloadCheckFingerprint $before $name) -cne (Get-WorkloadCheckFingerprint $ownerOnly $name)) ('validation owner alone remains bound '+$scope)
    }
    foreach($scope in @('NpcInvasionFlightCpu','NpcProduction','NpcPlantera','NpcEyeBoss','CombatEffectsCpu')){
        $plan=@(Get-WorkloadPlan $fixture 'checks' 'architecture.exe' @() @('check:native-'+$scope))
        Assert-Content ($plan.Count -eq 1 -and $plan[0].arguments[1] -ceq '--cpu') ('CPU argv unchanged '+$scope)
        Assert-Content ((Get-WorkloadCheckFingerprint $before ('native-'+$scope)) -ceq (Get-WorkloadCheckFingerprint $after ('native-'+$scope))) ('unrelated CPU proof excludes Content-only owner/resources '+$scope)
    }
    $oldRows=Get-WorkloadNativeContentRows $fixture
    Put-ContentFixture 'original/Content/Images/Misc/noise.xnb' 'changed original asset fixture'
    $newRows=Get-WorkloadNativeContentRows $fixture
    foreach($scope in @('NpcInvasionFlight','NpcInvasionFlightLoss','NpcBetsy','NpcDarkMage')){
        Assert-Content ((Get-WorkloadCheckFingerprint ([pscustomobject]@{inputs=$oldRows}) ('native-'+$scope)) -cne (Get-WorkloadCheckFingerprint ([pscustomobject]@{inputs=$newRows}) ('native-'+$scope))) ('actual shader byte change invalidates '+$scope)
    }
    Assert-Content ((Get-WorkloadCheckFingerprint ([pscustomobject]@{inputs=$oldRows}) 'native-NpcTreeBoss') -ceq (Get-WorkloadCheckFingerprint ([pscustomobject]@{inputs=$newRows}) 'native-NpcTreeBoss')) 'constructor-only consumer does not consume screen filters'
    Remove-Item -LiteralPath (Join-Path $fixture 'original/Content/ScreenShader.xnb')
    Reject-Content {Get-WorkloadPlan $fixture 'checks' 'architecture.exe' @() @('check:native-NpcInvasionFlight')} 'missing required original shader rejects full mixed scope'
    Assert-Content ((Get-WorkloadNativeContentArgument $fixture 'NpcTreeBoss') -ceq (Join-Path $fixture 'original/Content')) 'constructor-only input does not acquire unrelated shader requirement'
    Remove-Item -LiteralPath (Join-Path $fixture 'original/Content/Fonts/Mouse_Text.xnb')
    Reject-Content {Get-WorkloadNativeContentArgument $fixture 'NpcTreeBoss'} 'missing constructor font rejects actual consumer'
    Assert-Content ((Get-WorkloadNativeContentArgument $fixture 'NpcInvasionFlightCpu') -ceq '--cpu') 'missing graphics data cannot block independent CPU dispatch'
    # Restore all assets so these identity negatives cannot pass merely because
    # an earlier missing-font/shader test still makes resource admission fail.
    Put-ContentFixture 'original/Content/Fonts/Mouse_Text.xnb' 'controlled asset Fonts/Mouse_Text.xnb'
    Put-ContentFixture 'original/Content/ScreenShader.xnb' 'controlled asset ScreenShader.xnb'
    Assert-Content ((Get-WorkloadNativeContentArgument $fixture 'NpcBetsy') -ceq (Join-Path $fixture 'original/Content')) 'identity-negative premise has complete admitted assets'
    Put-ContentFixture 'original/Terraria.exe' 'different source game'
    Reject-Content {Get-WorkloadNativeContentArgument $fixture 'NpcBetsy'} 'mismatched source game cannot supply Content'
    Put-ContentFixture 'original/Terraria.exe' 'identity fixture only; never executed'
    $receipt.files[0].sha256='0'*64
    Put-ContentFixture 'external/TerrariaRefs/.juemingr-reference-set.json' ($receipt|ConvertTo-Json -Depth 4)
    Reject-Content {Get-WorkloadNativeContentArgument $fixture 'NpcDarkMage'} 'mismatched prepared original EXE identity is fatal'
    Assert-Content ((Get-WorkloadNativeContentArgument $fixture 'NpcProduction') -ceq '--cpu') 'default old worker CPU window retains its original argument even without Content admission'
    Write-Output ('PASS Native Content checks='+$checks+'; real plan/projection, source/receipt/resource negatives; file fixtures only, no XNA or game execution.')
}finally{
    $absolute=[IO.Path]::GetFullPath($fixture);$temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if(!$absolute.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -or (Split-Path -Leaf $absolute) -notlike 'JueMingR-content-*'){throw 'Refuse fixture cleanup outside exact temporary root'}
    Remove-Item -LiteralPath $absolute -Recurse -Force
}
