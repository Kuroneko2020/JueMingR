[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $root 'scripts/workload/Workload.Support.ps1')
function Assert-Evidence([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw ('Evidence contract: ' + $Message) }
}
Assert-Evidence (-not (Test-WorkloadReusable $root $null ([pscustomobject]@{inputs=@()}) 'workload-Routing' 'signature')) 'missing retired evidence allows new independent execution without claiming reuse'
# Standalone archive verification never calls a product CPU check. Its bytes
# remain recorded, but only its own evidence projection may depend on them.
foreach ($path in @('scripts/verify-existing-package.ps1','scripts/phase0s/PackageVerification.Support.ps1','tests/Phase0S/Invoke-PackageVerificationChecks.ps1')) {
    $route = Get-WorkloadRoute @($path)
    Assert-Evidence (($route.groups -join ',') -ceq 'core,package-tools') ('bounded package tool route: ' + $path)
    $old = [pscustomobject]@{inputs=@('src/Provider.cs:A',($path+':A'))}
    $new = [pscustomobject]@{inputs=@('src/Provider.cs:A',($path+':B'))}
    Assert-Evidence ((Get-WorkloadCheckFingerprint $old 'workload-PackageVerification') -cne (Get-WorkloadCheckFingerprint $new 'workload-PackageVerification')) 'own tool changes invalidate'
    foreach ($name in @('native-FishingCpu','native-CombatCpu')) {
        Assert-Evidence ((Get-WorkloadCheckFingerprint $old $name) -ceq (Get-WorkloadCheckFingerprint $new $name)) 'unrelated CPU inputs remain equal'
    }
}
foreach ($path in @('scripts/phase0s/Install-Phase0S.ps1','scripts/phase0s/Restore-Phase0S.ps1','scripts/phase0s/Phase0S.ScriptSupport.ps1','tests/NativeWorldTextProbe/NativeChecks.cs','tests/NativeWorldTextProbe/NativeToolExecutionChecks.cs','environment:runtime')) {
    $old = [pscustomobject]@{inputs=@($path+':A')}; $new = [pscustomobject]@{inputs=@($path+':B')}
    Assert-Evidence ((Get-WorkloadCheckFingerprint $old 'native-FishingCpu') -cne (Get-WorkloadCheckFingerprint $new 'native-FishingCpu')) ('shared inputs remain strict: ' + $path)
}
$sharedTools='tests/NativeWorldTextProbe/NativeToolExecutionChecks.cs'
$old=[pscustomobject]@{inputs=@($sharedTools+':A')};$new=[pscustomobject]@{inputs=@($sharedTools+':B')}
foreach($name in @('native-CombatCadence','native-CombatBoundary','native-CombatFacing','native-CombatHit','native-CombatProjectile','native-ToolsWait','native-ToolsSeed','native-ToolsCaptureWorkload','native-NpcDisplayIsolation')){
 Assert-Evidence ((Get-WorkloadCheckFingerprint $old $name) -cne (Get-WorkloadCheckFingerprint $new $name)) ('shared Sample/Reset remains input: '+$name)
}
$combatPlan=@(Get-WorkloadPlan $root 'checks' 'architecture.exe' @() @('combat-host') | ForEach-Object {$_.name})
foreach($name in @('native-NpcStrategy','native-NpcStrategyContinuous','native-NpcFoundationContinuous','native-NpcEventRetirementCpu')) {
    Assert-Evidence ($combatPlan -contains $name) ('actual combat obligations include '+$name)
    $missing=@($combatPlan | Where-Object {$_ -cne $name})
    $partial=[pscustomobject]@{mode='Related';inputFingerprint='input';requiredChecks=$combatPlan;checkCount=$missing.Count;results=@($missing | ForEach-Object {[pscustomobject]@{name=$_;result='PASS';disposition='EXECUTED'}})}
    Assert-Evidence (-not (Test-WorkloadCoverage $partial $combatPlan 'input')) ('missing actual scope cannot inherit completion: '+$name)
}
$unrelatedPlan=@(Get-WorkloadPlan $root 'checks' 'architecture.exe' @() @('fishing-host') | ForEach-Object {$_.name})
Assert-Evidence ($unrelatedPlan -notcontains 'native-NpcStrategy' -and $unrelatedPlan -notcontains 'native-NpcStrategyContinuous') 'unrelated fishing does not run strategy scopes'
foreach ($pair in @(@{path='tests/NativeWorldTextProbe/NativeNpcLifetimeChecks.cs';consumer='native-NpcStrategy';group='combat-host'},@{path='tests/NativeWorldTextProbe/NativeOuterInputBoundaryChecks.cs';consumer='native-InputBoundary';group='input-boundary'})) {
    $leafRoute=Get-WorkloadRoute @($pair.path)
    Assert-Evidence ($leafRoute.groups -contains ('check:'+$pair.consumer) -and $leafRoute.groups -notcontains 'shared-host') ('precise leaf route: '+$pair.path)
    $old=[pscustomobject]@{inputs=@($pair.path+':A')};$new=[pscustomobject]@{inputs=@($pair.path+':B')}
    Assert-Evidence ((Get-WorkloadCheckFingerprint $old $pair.consumer) -cne (Get-WorkloadCheckFingerprint $new $pair.consumer)) 'new leaf invalidates actual consumer'
    Assert-Evidence ((Get-WorkloadCheckFingerprint $old 'native-FishingCpu') -ceq (Get-WorkloadCheckFingerprint $new 'native-FishingCpu')) 'new leaf does not claim unrelated fishing dependency'
    $leafPlan=@(Get-WorkloadPlan $root 'checks' 'architecture.exe' @() $leafRoute.groups | ForEach-Object {$_.name})
    Assert-Evidence ($leafPlan -contains $pair.consumer) 'leaf route actually invokes its permanent consumer'
}
foreach ($path in @('tests/NativeWorldTextProbe/NativeCombatStrategyChecks.cs','tests/NativeWorldTextProbe/NativeCombatRollingControlChecks.cs','tests/NativeWorldTextProbe/NativeNpcLifetimeChecks.cs')) {
    $old=[pscustomobject]@{inputs=@($path+':A')};$new=[pscustomobject]@{inputs=@($path+':B')}
    Assert-Evidence ((Get-WorkloadCheckFingerprint $old 'native-NpcStrategy-rollchoice') -cne (Get-WorkloadCheckFingerprint $new 'native-NpcStrategy-rollchoice')) 'explicit rollchoice scope binds its real dispatch/rolling/lifetime leaf'
}
foreach($path in @('tests/NativeWorldTextProbe/NativeCombatFlyingTailChecks.cs','tests/NativeWorldTextProbe/NativeCombatFiniteControlChecks.cs')) {
    $old=[pscustomobject]@{inputs=@($path+':A')};$new=[pscustomobject]@{inputs=@($path+':B')}
    Assert-Evidence ((Get-WorkloadCheckFingerprint $old 'native-NpcStrategy') -cne (Get-WorkloadCheckFingerprint $new 'native-NpcStrategy')) 'family test invalidates actual strategy obligation'
    Assert-Evidence ((Get-WorkloadCheckFingerprint $old 'native-FishingCpu') -ceq (Get-WorkloadCheckFingerprint $new 'native-FishingCpu')) 'family-only test does not invalidate fishing'
}
# Real shared consumers and source families use the same projection as routing.
foreach($case in @(
    @{path='tests/NativeWorldTextProbe/NativeCombatFoundationChecks.cs';names=@('native-NpcFoundationRules','native-NpcFoundationContinuous','native-NpcStrategyContinuous')},
    @{path='src/JueMingR.Platform/Items/ItemOperationOwnership.cs';names=@('native-FishingCpu','native-CombatCpu','native-ToolsCpu')},
    @{path='src/JueMingR.TerrariaHost/Combat/Prediction/SegmentedNpcPrediction.cs';names=@('native-NpcSync','native-NpcWorkerIntegration')}
)){
    $old=[pscustomobject]@{inputs=@($case.path+':A')};$new=[pscustomobject]@{inputs=@($case.path+':B')}
    foreach($name in $case.names){Assert-Evidence ((Get-WorkloadCheckFingerprint $old $name) -cne (Get-WorkloadCheckFingerprint $new $name)) ('shared actual consumer invalidated: '+$name)}
}
foreach($path in @('tests/NativeWorldTextProbe/NativeChecks.cs','tests/NativeWorldTextProbe/NativeWorldTextProbe.csproj','src/JueMingR.TerrariaHost/JueMingR.TerrariaHost.csproj')){
    $route=Get-WorkloadRoute @($path)
    Assert-Evidence ($route.groups -contains 'legacy-worker') ('shared dispatch/compile input selects old chain: '+$path)
    $names=@(Get-WorkloadPlan $root 'checks' 'architecture.exe' @() $route.groups|ForEach-Object {$_.name})
    Assert-Evidence ($names -contains 'native-NpcWorkerIntegration') 'actual worker check remains in shared-input plan'
    $old=[pscustomobject]@{inputs=@($path+':A')};$new=[pscustomobject]@{inputs=@($path+':B')}
    Assert-Evidence ((Get-WorkloadCheckFingerprint $old 'native-NpcWorkerIntegration') -cne (Get-WorkloadCheckFingerprint $new 'native-NpcWorkerIntegration')) 'shared compile/dispatch bytes invalidate worker conclusion'
}
$path='tests/NativeWorldTextProbe/NativeCombatFoundationContinuousChecks.cs'
$old=[pscustomobject]@{inputs=@($path+':A')};$new=[pscustomobject]@{inputs=@($path+':B')}
$route=Get-WorkloadRoute @($path)
Assert-Evidence (@(Get-WorkloadPlan $root 'checks' 'architecture.exe' @() $route.groups|ForEach-Object {$_.name}) -contains 'native-NpcFiniteFlight') 'finite-flight reflected GateControls consumer selected'
Assert-Evidence ((Get-WorkloadCheckFingerprint $old 'native-NpcFiniteFlight') -cne (Get-WorkloadCheckFingerprint $new 'native-NpcFiniteFlight')) 'finite-flight reflected GateControls changes invalidate'
Assert-Evidence ((Get-WorkloadCheckFingerprint $old 'native-FishingCpu') -ceq (Get-WorkloadCheckFingerprint $new 'native-FishingCpu') -and (Get-WorkloadCheckFingerprint $old 'native-NpcWorkerIntegration') -ceq (Get-WorkloadCheckFingerprint $new 'native-NpcWorkerIntegration')) 'ordinary foundation leaf does not invalidate fishing/old worker'
$old=[pscustomobject]@{inputs=@('src/Provider.cs:A','scripts/build.ps1:A','docs/guide.md:A')}
$new=[pscustomobject]@{inputs=@('src/Provider.cs:A','scripts/build.ps1:B','docs/guide.md:A')}
Assert-Evidence ((Get-WorkloadCompileFingerprint $old) -cne (Get-WorkloadCompileFingerprint $new)) 'actual solution compile recipe change retires preparation'
Assert-Evidence ((Get-WorkloadCheckFingerprint $old 'native-FishingCpu') -ceq (Get-WorkloadCheckFingerprint $new 'native-FishingCpu')) 'compile recipe does not mechanically resign unrelated business'
$new.inputs[1]='scripts/build.ps1:A';$new.inputs[2]='docs/guide.md:B'
Assert-Evidence ((Get-WorkloadCompileFingerprint $old) -ceq (Get-WorkloadCompileFingerprint $new)) 'documentation does not alter preparation identity'
$worker='src/JueMingR.TerrariaHost/Combat/Prediction/NativeNpcEligibility.cs'
$old=[pscustomobject]@{inputs=@($worker+':A')};$new=[pscustomobject]@{inputs=@($worker+':B')}
Assert-Evidence ((Get-WorkloadCheckFingerprint $old 'native-NpcSync') -ceq (Get-WorkloadCheckFingerprint $new 'native-NpcSync')) 'dormant worker-only source does not invalidate default correction'
Assert-Evidence ((Get-WorkloadCheckFingerprint $old 'native-NpcWorkerIntegration') -cne (Get-WorkloadCheckFingerprint $new 'native-NpcWorkerIntegration')) 'worker-only source invalidates its actual old-chain consumer'
Assert-Evidence ((Get-WorkloadRoute @('tests/NativeWorldTextProbe/NewUnreviewedChecks.cs')).unknown.Count -eq 1) 'new unknown assertions require classification'

# The page-only provider owns arrangement, not the automation execution chain.
$page = Get-WorkloadRoute @('src/JueMingR.TerrariaHost/F5/MiscAutomationPanel.cs')
Assert-Evidence ($page.groups -contains 'pages-host') 'page arrangement must select actual page composition/input checks'
Assert-Evidence ($page.groups -notcontains 'fishing-host' -and $page.groups -notcontains 'storage-host') 'page arrangement must not select unrelated fishing/storage execution'
$shared = Get-WorkloadRoute @('src/JueMingR.Platform/Items/ItemOperationOwnership.cs')
foreach ($group in @('fishing-host','tools-host','quick-items-host','coin-deposit-host','processing-host','combat-host')) {
    Assert-Evidence ($shared.groups -contains $group) ('shared item provider must include ' + $group)
}
Write-Output 'PASS: local page exclusions and transitive shared item consumers.'
$before = [pscustomobject]@{inputs=@('src/Provider.cs:A','tests/NativeWorldTextProbe/NativePageCompositionChecks.cs:A','environment:x:A')}
$after = [pscustomobject]@{inputs=@('src/Provider.cs:A','tests/NativeWorldTextProbe/NativePageCompositionChecks.cs:B','environment:x:A')}
Assert-Evidence ((Get-WorkloadCheckFingerprint $before 'native-FishingCpu') -ceq (Get-WorkloadCheckFingerprint $after 'native-FishingCpu')) 'local page assertion repair preserves unrelated fishing inputs'
Assert-Evidence ((Get-WorkloadCheckFingerprint $before 'native-PageCompositionCpu') -cne (Get-WorkloadCheckFingerprint $after 'native-PageCompositionCpu')) 'local page assertion repair invalidates its actual consumer'
$after.inputs[0]='src/Provider.cs:B'
Assert-Evidence ((Get-WorkloadCheckFingerprint $before 'native-FishingCpu') -cne (Get-WorkloadCheckFingerprint $after 'native-FishingCpu')) 'shared production changes still invalidate'
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('JueMingR-evidence-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixture) | Out-Null
try {
    $recipeRoot=Join-Path $fixture 'recipe';$recipeFile=Join-Path $recipeRoot 'scripts/workload/Workload.Evidence.ps1'
    [IO.Directory]::CreateDirectory((Split-Path -Parent $recipeFile))|Out-Null
    [IO.File]::WriteAllText($recipeFile,'dotnet build -p:Option=A');$recipeBefore=Get-WorkloadFixtureRecipeFingerprint $recipeRoot
    [IO.File]::WriteAllText($recipeFile,'dotnet build -p:Option=B')
    Assert-Evidence ($recipeBefore -cne (Get-WorkloadFixtureRecipeFingerprint $recipeRoot)) 'fixture actual compile argument owner changes preparation identity'
    $preparedFile=Join-Path $fixture 'artifacts/build/Debug/work/bin/prepared.bin'
    [IO.Directory]::CreateDirectory((Split-Path -Parent $preparedFile))|Out-Null;[IO.File]::WriteAllText($preparedFile,'inert recorded compile output')
    $compileBefore=[pscustomobject]@{inputs=@('src/Provider.cs:A','scripts/build.ps1:A','docs/guide.md:A');fingerprint='unused-global-lock'}
    $compileAfter=[pscustomobject]@{inputs=@('src/Provider.cs:A','scripts/build.ps1:B','docs/guide.md:A');fingerprint='unused-global-lock'}
    $preparedRecord=[pscustomobject]@{schemaVersion=4;configuration='Debug';sdk='10.0.203';inputFingerprint='unused-global-lock';compileInputs=@(Get-WorkloadCompileInputs $compileBefore);compileFingerprint=(Get-WorkloadCompileFingerprint $compileBefore);outputs=@([pscustomobject]@{path='bin/prepared.bin';length=(Get-Item $preparedFile).Length;sha256=(Get-FileHash $preparedFile).Hash})}
    Assert-Evidence (Test-WorkloadBuildMatch $fixture $preparedRecord $null $compileBefore) 'complete matching prepared record is usable'
    Assert-Evidence (-not (Test-WorkloadBuildMatch $fixture $preparedRecord $null $compileAfter)) 'compile recipe change rejects actual preparation match even if global lock text is unchanged'
    $compileAfter.inputs[1]='scripts/build.ps1:A';$compileAfter.inputs[2]='docs/guide.md:B'
    Assert-Evidence (Test-WorkloadBuildMatch $fixture $preparedRecord $null $compileAfter) 'pure documentation can reuse preparation'
    $pointerRoot=Join-Path $fixture 'pointer-chain';$archiveRoot=Join-Path $pointerRoot 'artifacts/build/evidence-artifacts'
    $retainedArchive=Join-Path $archiveRoot ('C'*64);$unusedArchive=Join-Path $archiveRoot ('B'*64)
    foreach($directory in @($retainedArchive,$unusedArchive)){[IO.Directory]::CreateDirectory($directory)|Out-Null;[IO.File]::WriteAllText((Join-Path $directory '0-owned.bin'),'self-owned pointer sample')}
    $pointerB=Join-Path $pointerRoot 'qualification.json';$pointerCache=Join-Path $pointerRoot 'original-cache.json'
    $originalCache=[ordered]@{outputs=@([ordered]@{path=(Join-Path $retainedArchive '0-owned.bin');length=25})}
    Write-WorkloadJson $pointerCache $originalCache
    $originalB=[ordered]@{legacyCachePath=$pointerCache;legacyCacheSha256=(Get-FileHash $pointerCache).Hash}
    Write-WorkloadJson $pointerB $originalB
    Save-WorkloadApplicabilityPointer $pointerRoot ([pscustomobject]@{commit=('a'*40);fingerprint=('B'*64)}) ([pscustomobject]@{fingerprint=('C'*64)}) $pointerB
    $pointer=Join-Path $pointerRoot 'artifacts/build/workload-checkpoints/current-applicability.json';$originalPointer=Read-WorkloadJson $pointer
    foreach($bad in @('missing','damaged','type','path','valid-json-sha','legacy-cache-sha','cycle-sha')){
        if($bad -eq 'missing'){Remove-Item -LiteralPath $pointerB}
        if($bad -eq 'damaged'){[IO.File]::WriteAllText($pointerB,'{broken')}
        if($bad -eq 'type'){$invalid=$originalPointer|ConvertTo-Json|ConvertFrom-Json;$invalid.path=42;Write-WorkloadJson $pointer $invalid}
        if($bad -eq 'path'){$invalid=$originalPointer|ConvertTo-Json|ConvertFrom-Json;$invalid.path='not-a-record.txt';Write-WorkloadJson $pointer $invalid}
        if($bad -eq 'valid-json-sha'){[IO.File]::WriteAllText($pointerB,'{}')}
        if($bad -eq 'legacy-cache-sha'){[IO.File]::WriteAllText($pointerCache,'{}')}
        if($bad -eq 'cycle-sha'){
            # The pointer root was already parsed. A false digest on its
            # incoming cycle must still block deletion instead of deduping it.
            $cycle=[ordered]@{legacyCachePath=$pointerCache;legacyCacheSha256=$originalB.legacyCacheSha256;path=$pointer;executionId=('a'*32);sha256=('0'*64)}
            Write-WorkloadJson $pointerB $cycle
            $invalid=$originalPointer|ConvertTo-Json|ConvertFrom-Json;$invalid.sha256=(Get-FileHash $pointerB).Hash;Write-WorkloadJson $pointer $invalid
        }
        $blocked=$false;$failure='';try{Remove-UnusedWorkloadArtifacts $pointerRoot @() -Collect -RetainedRecords @()}catch{$blocked=$true;$failure=$_.Exception.Message}
        Assert-Evidence ($blocked -and [IO.Directory]::Exists($retainedArchive) -and [IO.Directory]::Exists($unusedArchive)) ('formal pointer mandatory '+$bad+' blocks deletion')
        if($bad -in @('valid-json-sha','legacy-cache-sha','cycle-sha')){Assert-Evidence ($failure -like 'Required retention edge digest mismatch:*') ('declared edge digest is the rejecting boundary: '+$bad)}
        Write-WorkloadJson $pointer $originalPointer;Write-WorkloadJson $pointerB $originalB;Write-WorkloadJson $pointerCache $originalCache
    }
    Remove-UnusedWorkloadArtifacts $pointerRoot @() -Collect -RetainedRecords @()
    Assert-Evidence ([IO.Directory]::Exists($retainedArchive) -and -not [IO.Directory]::Exists($unusedArchive)) 'actual current-applicability pointer to qualification to original cache retains archive and allows independent unused sample'
    foreach($edgeName in @('applicabilityRecord','batchPath')) {
        $edgeRoot=Join-Path $fixture ('declared-'+$edgeName)
        $edgeBase=Join-Path $edgeRoot 'artifacts/build/evidence-artifacts'
        $keep=Join-Path $edgeBase ('C'*64);$unused=Join-Path $edgeBase ('D'*64)
        $child=Join-Path $edgeRoot 'child.json';$parent=Join-Path $edgeRoot 'parent.json'
        $digestName=if($edgeName -ceq 'batchPath'){'batchSha256'}else{'applicabilitySha256'}
        $childValue=[ordered]@{outputs=@([ordered]@{path=(Join-Path $keep '0-owned.bin');length=6})}
        foreach($bad in @('replaced-json','null','empty','number','malformed','second-edge')) {
            foreach($dir in @($keep,$unused)){[IO.Directory]::CreateDirectory($dir)|Out-Null;[IO.File]::WriteAllText((Join-Path $dir '0-owned.bin'),'sample')}
            Write-WorkloadJson $child $childValue
            $declared=[ordered]@{};$declared[$edgeName]=$child;$declared[$digestName]=(Get-FileHash $child).Hash
            if($bad -ceq 'replaced-json'){Write-WorkloadJson $child ([ordered]@{outputs=@()})}
            elseif($bad -ceq 'null'){$declared[$digestName]=$null}
            elseif($bad -ceq 'empty'){$declared[$digestName]=''}
            elseif($bad -ceq 'number'){$declared[$digestName]=42}
            elseif($bad -ceq 'malformed'){$declared[$digestName]='not-a-sha'}
            if($bad -ceq 'second-edge') {
                $invalid=[ordered]@{};$invalid[$edgeName]=$child;$invalid[$digestName]=('0'*64)
                Write-WorkloadJson $parent ([ordered]@{edges=@($declared,$invalid)})
            } else {Write-WorkloadJson $parent $declared}
            $blocked=$false;try{Remove-UnusedWorkloadArtifacts $edgeRoot @() -Collect -RetainedRecords @($parent)}catch{$blocked=$true}
            Assert-Evidence ($blocked -and [IO.Directory]::Exists($keep) -and [IO.Directory]::Exists($unused)) ($edgeName+' declared '+$bad+' refuses all deletion')
        }
        Write-WorkloadJson $child $childValue
        $declared=[ordered]@{};$declared[$edgeName]=$child;$declared[$digestName]=(Get-FileHash $child).Hash
        Write-WorkloadJson $parent $declared
        Remove-UnusedWorkloadArtifacts $edgeRoot @() -Collect -RetainedRecords @($parent)
        Assert-Evidence ([IO.Directory]::Exists($keep) -and -not [IO.Directory]::Exists($unused)) ($edgeName+' valid declaration retains child archive and collects independent sample')
        # Missing digest is a genuine old-format contract, unlike declared null.
        $declared.Remove($digestName);Write-WorkloadJson $parent $declared
        Remove-UnusedWorkloadArtifacts $edgeRoot @() -Collect -RetainedRecords @($parent)
        Assert-Evidence ([IO.Directory]::Exists($keep)) ($edgeName+' absent old-format digest remains legal')
    }
    $readPath=Join-Path $fixture 'read-window.json';Write-WorkloadJson $readPath ([ordered]@{value=1})
    Start-WorkloadReadWindow
    try {
        $first=Read-WorkloadJson $readPath;$firstHash=(Get-WorkloadFileHash $readPath).Hash
        Assert-Evidence ([object]::ReferenceEquals($first,(Read-WorkloadJson $readPath))) 'one phase parses a record once'
        Assert-Evidence ($script:WorkloadReadWindow.Count -eq 1 -and (Get-WorkloadFileHash $readPath).Hash -ceq $firstHash) 'hash and parsed JSON share one stable read lease'
        $blocked=$false;try{[IO.File]::WriteAllText($readPath,'{"value":2}')}catch{$blocked=$true}
        Assert-Evidence $blocked 'same-phase replacement is refused rather than silently consumed'
    }finally{Stop-WorkloadReadWindow}
    [IO.File]::WriteAllText($readPath,'{"value":2}')
    Start-WorkloadReadWindow
    try{Assert-Evidence ((Read-WorkloadJson $readPath).value -eq 2 -and (Get-WorkloadFileHash $readPath).Hash -cne $firstHash) 'a new boundary rereads actual changed content'}finally{Stop-WorkloadReadWindow}
    # This miniature Git repository contains only self-owned reference notes.
    $workspace=Join-Path $fixture 'workspace';[IO.Directory]::CreateDirectory($workspace)|Out-Null
    $null=Invoke-WorkloadGit $workspace @('init','-q')
    $null=Invoke-WorkloadGit $workspace @('-c','user.name=Fixture','-c','user.email=fixture@example.invalid','commit','--allow-empty','-m','fixture')
    $note=Join-Path $workspace 'note.txt';[IO.File]::WriteAllText($note,'retained note')
    $declaration=Join-Path $fixture 'retained.json'
    $decl=[ordered]@{schema='retained-workspace-assets-1';authorization='self-owned test fixture';files=@([ordered]@{path='note.txt';sha256=(Get-FileHash $note).Hash;length=(Get-Item $note).Length;tracked=$false})}
    Write-WorkloadJson $declaration $decl
    $retained=Read-WorkloadRetainedAssets $workspace $declaration -RequireCommitted
    Assert-Evidence (-not $retained.clean -and -not $retained.releaseEligible -and $retained.workspaceDirty) 'explicit admission remains dirty and non-release'
    Start-WorkloadReadWindow
    try{
        $null=Read-WorkloadRetainedAssets $workspace $declaration -RequireCommitted
        $blocked=$false;try{[IO.File]::WriteAllText($note,'in-phase replacement')}catch{$blocked=$true}
        Assert-Evidence $blocked 'restricted assets are bound by the same phase lease as their declaration'
    }finally{Stop-WorkloadReadWindow}
    foreach($forbidden in @('global.json','src/Hidden.cs','../outside.txt')){
        $decl.files[0].path=$forbidden;Write-WorkloadJson $declaration $decl
        $rejected=$false;try{$null=Read-WorkloadRetainedAssets $workspace $declaration}catch{$rejected=$true}
        Assert-Evidence $rejected ('executable or escaping retained declaration rejected: '+$forbidden)
    }
    $decl.files[0].path='note.txt';Write-WorkloadJson $declaration $decl
    [IO.File]::WriteAllText((Join-Path $workspace 'unknown.txt'),'undeclared')
    $rejected=$false;try{$null=Read-WorkloadRetainedAssets $workspace $declaration -RequireCommitted}catch{$rejected=$true}
    Assert-Evidence $rejected 'new undeclared dirt cannot enter a restricted package candidate'
    [IO.File]::WriteAllText($note,'changed bytes')
    $rejected=$false;try{$null=Read-WorkloadRetainedAssets $workspace $declaration}catch{$rejected=$true}
    Assert-Evidence $rejected 'retained asset identity is never a path-only exclusion'
    $tiny=Join-Path $fixture 'tests/Tiny';[IO.Directory]::CreateDirectory($tiny) | Out-Null
    [IO.File]::WriteAllText((Join-Path $tiny 'Tiny.csproj'),'<Project><ItemGroup><Compile Include="../Linked.cs" /></ItemGroup></Project>')
    [IO.File]::WriteAllText((Join-Path $tiny 'Local.cs'),'local locked source')
    $linked=Join-Path $fixture 'tests/Linked.cs';[IO.File]::WriteAllText($linked,'linked locked source')
    $tinyInputs=[pscustomobject]@{inputs=@(Get-ChildItem -LiteralPath (Join-Path $fixture 'tests') -Recurse -File | ForEach-Object {$_.FullName.Substring($fixture.Length+1).Replace('\','/')+':'+(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash})}
    $tinyBefore=Get-WorkloadFixtureInputFingerprint $fixture 'Tiny' $tinyInputs
    [IO.File]::WriteAllText($linked,'changed before fixture compile');$rejected=$false
    try {$null=Get-WorkloadFixtureInputFingerprint $fixture 'Tiny' $tinyInputs} catch {$rejected=$_.Exception.Message.Contains('locked batch')}
    Assert-Evidence $rejected 'linked compiler input cannot change after the batch lock'
    [IO.File]::WriteAllText($linked,'linked locked source')
    Assert-Evidence ((Get-WorkloadFixtureInputFingerprint $fixture 'Tiny' $tinyInputs) -ceq $tinyBefore) 'unchanged actual fixture compiler footprint is stable'
    $bin=Join-Path $fixture 'artifacts/build/Debug/checks/probe'
    [IO.Directory]::CreateDirectory($bin) | Out-Null
    $exe=Join-Path $bin 'probe.exe'; $config=$exe+'.config'
    [IO.File]::WriteAllText($exe,'isolated artifact'); [IO.File]::WriteAllText($config,'<configuration/>')
    $captured=@(Get-WorkloadEvidenceOutputs $fixture ([pscustomobject]@{outputs=@()}) $exe)
    Assert-Evidence (@($captured | Where-Object {$_.path -ceq $config}).Count -eq 1) 'actual exe.config is captured'
    $archived=@(Save-WorkloadArtifacts $fixture $captured)
    Assert-Evidence (Test-WorkloadLiveArtifacts $archived $true) 'complete original fixture directory matches'
    [IO.File]::WriteAllText($config,'invalid runtime configuration')
    Assert-Evidence (-not (Test-WorkloadLiveArtifacts $archived $false)) 'runtime config changes invalidate even across leaf revisions'
    Assert-Evidence (Test-WorkloadEvidenceOutputs $fixture $archived) 'original execution bytes remain independently verifiable'
    [IO.File]::WriteAllText($config,'<configuration/>')
    [IO.File]::WriteAllText((Join-Path $bin 'extra.dll'),'unexpected dependency')
    Assert-Evidence (-not (Test-WorkloadLiveArtifacts $archived $true)) 'new dependency cannot hide outside the original file list'
    [IO.File]::WriteAllText($archived[0].path,'damaged original')
    Assert-Evidence (-not (Test-WorkloadEvidenceOutputs $fixture $archived)) 'damaged archived execution cannot supply evidence'
    $repaired=@(Save-WorkloadArtifacts $fixture $captured)
    Assert-Evidence ((Test-WorkloadEvidenceOutputs $fixture $repaired) -and $repaired[0].path -cne $archived[0].path -and [IO.File]::ReadAllText($archived[0].path) -ceq 'damaged original') 'fresh execution uses distinct archive and preserves damaged original'
    $archived=$repaired
    $artifact = Join-Path $fixture 'check.bin'
    [IO.File]::WriteAllText($artifact, 'original')
    $output = [pscustomobject]@{path=$artifact; length=8; sha256=(Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash}
    $evidence = [pscustomobject]@{schemaVersion=2; status='PASS'; name='one'; signature='signature'; inputFingerprint='input';
        executionId=[Guid]::NewGuid().ToString('N'); sourceCommit=('a'*40); sourceFingerprint=('B'*64); milliseconds=3; outputs=@($output)}
    Assert-Evidence (Test-WorkloadEvidence $evidence 'input' 'one' 'signature') 'valid execution identity can be reused'
    Assert-Evidence (Test-WorkloadEvidenceOutputs $root $evidence.outputs) 'actual recorded artifact must still match'
    foreach ($field in @('schemaVersion','executionId','sourceCommit','outputs')) {
        $broken = $evidence | ConvertTo-Json -Depth 6 | ConvertFrom-Json
        $broken.PSObject.Properties.Remove($field)
        Assert-Evidence (-not (Test-WorkloadEvidence $broken 'input' 'one' 'signature')) ('missing ' + $field + ' cannot inherit PASS')
    }
    foreach ($status in @('FAILED','PENDING','CANCELLED','DEFERRED')) {
        $evidence.status=$status
        Assert-Evidence (-not (Test-WorkloadEvidence $evidence 'input' 'one' 'signature')) ($status+' is not reusable')
    }
    $evidence.status='PASS'
    Assert-Evidence (-not (Test-WorkloadEvidence $evidence 'changed-source-fixture-or-dependency' 'one' 'signature')) 'changed executable input invalidates'
    Assert-Evidence (-not (Test-WorkloadEvidence $evidence 'input' 'one' 'changed-options')) 'changed actual invocation invalidates'
    [IO.File]::WriteAllText($artifact, 'tampered')
    Assert-Evidence (-not (Test-WorkloadEvidenceOutputs $root $evidence.outputs)) 'same filename/length does not authorize changed bytes'
    $result=[pscustomobject]@{status='PASS';mode='Related';inputFingerprint='input';requiredChecks=@('one','two');checkCount=1;
        results=@([pscustomobject]@{name='one';result='PASS';disposition='EXECUTED'})}
    Assert-Evidence (-not (Test-WorkloadCoverage $result @('one','two') 'input')) 'partial success is not final coverage'
    $result.results+= [pscustomobject]@{name='two';result='PASS';disposition='REUSED'}; $result.checkCount=2
    Assert-Evidence (Test-WorkloadCoverage $result @('one','two') 'input') 'execution plus verified reuse may complete obligations'
    $result.mode='Feedback'
    Assert-Evidence (-not (Test-WorkloadCoverage $result @('one','two') 'input')) 'feedback can never authorize delivery'
    $result.mode='Related'; $result.requiredChecks=@('one')
    Assert-Evidence (-not (Test-WorkloadCoverage $result @('one','two') 'input')) 'a narrowed baseline cannot erase earlier obligations'
    Assert-Evidence (-not (Test-WorkloadDelivery $fixture ([pscustomobject]@{schemaVersion=3}))) 'old build records cannot be upgraded to trusted delivery'
    $failure=Join-Path $fixture 'failure.ps1'
    $support=Join-Path $root 'scripts/workload/Workload.Support.ps1'
    $body = @'
param([string] $Support)
$ErrorActionPreference='Stop'
. $Support
Invoke-WorkloadProcess 'expected-failure' (Get-Command powershell.exe).Source @('-NoProfile','-Command','exit 7')
throw 'Incorrectly returned from a failed process.'
'@
    [IO.File]::WriteAllText($failure,$body)
    $start=New-Object Diagnostics.ProcessStartInfo
    $start.FileName=(Get-Command powershell.exe).Source
    $start.Arguments='-NoProfile -ExecutionPolicy Bypass -File "'+$failure+'" -Support "'+$support+'"'
    $start.UseShellExecute=$false; $start.CreateNoWindow=$true; $start.RedirectStandardOutput=$start.RedirectStandardError=$true
    $process=New-Object Diagnostics.Process; $process.StartInfo=$start
    try {
        [void]$process.Start(); $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync(); $process.WaitForExit()
        $text=$stdout.GetAwaiter().GetResult()+$stderr.GetAwaiter().GetResult()
        Assert-Evidence ($process.ExitCode -ne 0 -and $text.Contains('failed with exit 7') -and -not $text.Contains('Incorrectly returned')) 'real child failure propagates nonzero'
    } finally {$process.Dispose()}
    # Match the formal package entry's parent 2>&1 capture with a native
    # receiver, rather than PowerShell's different stderr forwarding behavior.
    $stderrExe=Join-Path $fixture 'stderr-receiver.exe'
    Add-Type -TypeDefinition 'using System; public static class WorkloadStderrReceiver { public static int Main(string[] args) { if(args[0]=="--list-checks") { Console.WriteLine("legacy-one"); return 0; } if(args[0]=="wait") { System.IO.File.WriteAllText(args[1],System.Diagnostics.Process.GetCurrentProcess().Id.ToString()); Console.WriteLine("known child running"); while(!System.IO.File.Exists(args[2]))System.Threading.Thread.Sleep(10); return 130; } if(args[0]=="only") { if(Environment.GetEnvironmentVariable("JUEMINGR_STRATEGY_ONLY")!=null || Environment.GetEnvironmentVariable("JUEMINGR_FOUNDATION_STRATEGY_ONLY")!=null) return 9; Console.WriteLine("only-cleared"); return 0; } Console.Error.WriteLine("ordinary native information"); return int.Parse(args[0]); } }' -OutputAssembly $stderrExe -OutputType ConsoleApplication
    # Exercise real child exits. Completion is durable before the batch's
    # source stability decision; neither a missing qualification nor a later
    # interrupted attempt may borrow a previously qualified success.
    $locked=[pscustomobject]@{inputs=@('source:locked');fingerprint=(Get-WorkloadHash @('source:locked'))}
    $sourceLock=[pscustomobject]@{commit=('a'*40);fingerprint=('B'*64)}
    $batch=New-WorkloadCheckpointBatch $fixture $sourceLock $locked @('checkpoint-one','checkpoint-two')
    $check=[pscustomobject]@{name='checkpoint-one';executable=$stderrExe;arguments=@('0');project=''}
    $attempt=Start-WorkloadCheckpoint $fixture $batch $check 'signature'
    $attempt.beforeOutputs=@(Get-WorkloadEvidenceOutputs $fixture ([pscustomobject]@{outputs=@()}) $stderrExe | ForEach-Object {[pscustomobject]@{path=$_.path;livePath=$_.path;sha256=$_.sha256;length=$_.length}})
    Invoke-WorkloadProcess $check.name $check.executable $check.arguments -Checkpoint $attempt
    $completed=Read-WorkloadJson $attempt.path
    Assert-Evidence ($completed.status -ceq 'EXECUTED_AWAITING_STABILITY' -and $completed.exitCode -eq 0 -and $completed.executionId -ceq $attempt.executionId -and $completed.startedUtc -and $completed.endedUtc -and $completed.outputSha256) 'real completed child persists immediately with original identity/output/exit'
    Assert-Evidence (-not (Test-WorkloadCheckpointQualified $fixture $completed)) 'interrupted batch cannot claim an unperformed stability check'
    $commandOutputs=@(Get-WorkloadEvidenceOutputs $fixture ([pscustomobject]@{outputs=@()}) $stderrExe)
    $completed.outputs=@(Save-WorkloadArtifacts $fixture $commandOutputs)
    Write-WorkloadJson $attempt.path $completed
    Complete-WorkloadCheckpointQualification $fixture @($completed) $batch
    Assert-Evidence (Test-WorkloadCheckpointQualified $fixture $completed) 'stable source and actual archived outputs qualify the original completion'
    Remove-Item -LiteralPath ($completed.path+'.qualification.json')
    $recovered=Restore-WorkloadCheckpointEvidence $fixture $sourceLock $locked $check.name 'signature' ('a'*40)
    Assert-Evidence ($null -ne $recovered -and $recovered.executionId -ceq $completed.executionId -and $recovered.executedUtc -ceq $completed.endedUtc) 'default entry can qualify saved pending completion while preserving original execution identity and end'
    $ready=Join-Path $fixture 'known-child.pid';$stop=Join-Path $fixture 'known-child.stop'
    $cancelCheck=[pscustomobject]@{name='checkpoint-two';executable=$stderrExe;arguments=@('wait',$ready,$stop);project=''}
    $cancel=Start-WorkloadCheckpoint $fixture $batch $cancelCheck 'cancel-command'
    $pipeline=[PowerShell]::Create()
    try {
        [void]$pipeline.AddScript('param($support,$exe,$arguments,$receipt) $ErrorActionPreference="Stop"; . $support; Invoke-WorkloadProcess "checkpoint-two" $exe $arguments -Checkpoint $receipt').AddArgument($support).AddArgument($stderrExe).AddArgument($cancelCheck.arguments).AddArgument($cancel)
        $async=$pipeline.BeginInvoke();$deadline=[DateTime]::UtcNow.AddSeconds(5)
        while(-not [IO.File]::Exists($ready)){if([DateTime]::UtcNow -gt $deadline){throw 'Known cancellation child did not start.'};Start-Sleep -Milliseconds 10}
        $running=Read-WorkloadJson $cancel.path
        Assert-Evidence ($running.status -ceq 'RUNNING' -and $running.startedUtc -and $null -eq $running.exitCode) 'actual child reached RUNNING before interruption'
        # Stop only this test's own pipeline/native child. This uses the same
        # PowerShell pipeline cancellation boundary as an interrupted runner.
        $pipeline.Stop();try {$null=$pipeline.EndInvoke($async)} catch { }
        $cancelled=Read-WorkloadJson $cancel.path
        Assert-Evidence ($cancelled.status -ceq 'RUNNING' -and $null -eq $cancelled.exitCode -and $null -eq $cancelled.endedUtc -and -not (Test-WorkloadCheckpointQualified $fixture $cancelled)) 'actual cancelled child has no invented normal exit/end/qualification'
        [IO.File]::WriteAllText(($cancel.path+'.pending'),'{"status":"PASS","exitCode":0}')
        Assert-Evidence ((Read-WorkloadJson $cancel.path).status -ceq 'RUNNING' -and -not (Test-WorkloadCheckpointQualified $fixture $cancelled)) 'unpublished partial writer cannot upgrade original running receipt'
        Assert-Evidence ($null -ne (Restore-WorkloadCheckpointEvidence $fixture $sourceLock $locked 'checkpoint-one' 'signature' ('a'*40))) 'completed sibling remains recoverable after actual next-child cancellation'
    } finally {
        # If the runtime has not already terminated its native child, ask only
        # the fixture PID with this exact executable path to finish itself.
        if([IO.File]::Exists($ready)) {
            $ownedProcess=Get-Process -Id ([int][IO.File]::ReadAllText($ready)) -ErrorAction SilentlyContinue
            if($null -ne $ownedProcess) {
                if(-not [string]::Equals($ownedProcess.MainModule.FileName,$stderrExe,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected cancellation child identity.'}
                [IO.File]::WriteAllText($stop,'finish owned fixture');if(-not $ownedProcess.WaitForExit(3000)){throw 'Owned cancellation child did not finish.'}
            }
        }
        $pipeline.Dispose()
    }
    $different=[pscustomobject]@{inputs=@('source:changed');fingerprint=(Get-WorkloadHash @('source:changed'))}
    Assert-Evidence ($null -eq (Restore-WorkloadCheckpointEvidence $fixture $sourceLock $different $check.name 'signature' ('a'*40))) 'changed compiler/configuration input cannot recover old pending completion'
    $qualifiedEntry=[pscustomobject]@{schemaVersion=2;status='PASS';name=$check.name;signature='signature';inputFingerprint=(Get-WorkloadCheckFingerprint $locked $check.name);executionId=$completed.executionId;sourceCommit=('a'*40);sourceFingerprint=('B'*64);milliseconds=1;outputs=$completed.outputs;allInputFingerprint=$locked.fingerprint;inputs=$locked.inputs;detectionCommit=('a'*40);checkpointPath=$completed.path}
    Assert-Evidence (Test-WorkloadReusable $fixture $qualifiedEntry $locked $check.name 'signature') 'existing reuse entry consumes qualified real checkpoint'
    $legacy=$qualifiedEntry | ConvertTo-Json -Depth 10 | ConvertFrom-Json
    $legacy | Add-Member -MemberType NoteProperty -Name executedUtc -Value $completed.endedUtc
    $legacy.PSObject.Properties.Remove('checkpointPath');$legacy.name='legacy-one';$legacy.signature=Get-WorkloadHash (@($stderrExe)+@('0'))
    $legacyPath=Join-Path $fixture 'legacy-cache.json';Write-WorkloadJson $legacyPath ([ordered]@{schemaVersion=2;results=@($legacy)})
    $appPath=Join-Path $fixture 'fixed-applicability.json'
    $application=[ordered]@{schema='fixed-workload-applicability-1';qualificationId=[Guid]::NewGuid().ToString('N');commit=$sourceLock.commit;sourceFingerprint=$sourceLock.fingerprint;inputFingerprint=$locked.fingerprint;requiredChecks=@('legacy-one');additionalChecks=@();legacyCachePath=$legacyPath;legacyCacheSha256=(Get-FileHash -LiteralPath $legacyPath).Hash;inputSets=@([ordered]@{fingerprint=$locked.fingerprint;differences=@()});decisions=@([ordered]@{name='legacy-one';action='QUALIFIED';originalExecutionId=$legacy.executionId;requiresCurrent=@();reason='controlled unchanged byte/command proof'})}
    Write-WorkloadJson $appPath $application
    $admitted=Read-WorkloadApplicability $fixture $appPath $sourceLock $locked @('legacy-one')
    # The reader consumes real approved assertion byte deltas; this inert
    # receipt exercises admission only, never claims Strategy was executed.
    $rolling='tests/NativeWorldTextProbe/NativeCombatRollingControlChecks.cs'
    $lifetime='tests/NativeWorldTextProbe/NativeNpcLifetimeChecks.cs'
    $oldRows=@($rolling+':85DEEE19205C8243CE39DDE9E187468820BFB98E537719B38A77E9BE8D90C828')
    $newRows=@(($rolling+':16ECAAD59329232199E93D157D1E63473F4455331F72C614AF4EC0C51C130EE0'),($lifetime+':5E15817C5FD2E9FC1ADB3BFC6587CE6BE0FF173FE59C84A81B54283111DE069E'))
    $nativeLegacy=$legacy|ConvertTo-Json -Depth 10|ConvertFrom-Json;$nativeLegacy.name='native-NpcStrategy';$nativeLegacy.inputs=$oldRows
    $nativeLegacy.allInputFingerprint=Get-WorkloadHash $oldRows;$nativeLegacy.inputFingerprint=Get-WorkloadCheckFingerprint $nativeLegacy $nativeLegacy.name
    $profileCache=Join-Path $fixture 'profile-original.json';Write-WorkloadJson $profileCache ([ordered]@{schemaVersion=2;results=@($nativeLegacy)})
    $proofs=@('native-NpcStrategy-rollchoice','native-NpcStrategyContinuous','native-NpcRollingCpu','native-NpcRollingSelectionNegative')
    $required=@('native-NpcStrategy')+@($proofs|Where-Object {$_ -cne 'native-NpcStrategy-rollchoice'})
    $profileRecord=[ordered]@{schema='fixed-workload-applicability-1';qualificationId=[Guid]::NewGuid().ToString('N');qualificationProfile='issue112-lifetime-delta-20261008';commit=$sourceLock.commit;sourceFingerprint=$sourceLock.fingerprint;inputFingerprint=(Get-WorkloadHash $newRows);requiredChecks=$required;additionalChecks=@('native-NpcStrategy-rollchoice');legacyCachePath=$profileCache;legacyCacheSha256=(Get-FileHash $profileCache).Hash;inputSets=@([ordered]@{fingerprint=$nativeLegacy.allInputFingerprint;differences=@(Get-WorkloadInputDifferences $oldRows $newRows)});decisions=@([ordered]@{name='native-NpcStrategy';action='QUALIFIED';originalExecutionId=$nativeLegacy.executionId;requiresCurrent=$proofs;reason='explicit reviewed lifetime assertion delta'})+@($required|Where-Object {$_ -cne 'native-NpcStrategy'}|ForEach-Object {[ordered]@{name=$_;action='EXECUTE';requiresCurrent=@()}})}
    $profilePath=Join-Path $fixture 'profile.json';Write-WorkloadJson $profilePath $profileRecord
    $profileInputs=[pscustomobject]@{inputs=$newRows;fingerprint=(Get-WorkloadHash $newRows)}
    $null=Read-WorkloadApplicability $fixture $profilePath $sourceLock $profileInputs $required
    $fishingLegacy=$nativeLegacy|ConvertTo-Json -Depth 10|ConvertFrom-Json;$fishingLegacy.name='native-FishingCpu'
    $fishingLegacy.inputFingerprint=Get-WorkloadCheckFingerprint $fishingLegacy $fishingLegacy.name
    Write-WorkloadJson $profileCache ([ordered]@{schemaVersion=2;results=@($nativeLegacy,$fishingLegacy)})
    $profileRecord.legacyCacheSha256=(Get-FileHash $profileCache).Hash
    $required+=@('native-FishingCpu');$profileRecord.requiredChecks=$required
    $profileRecord.decisions+=@([ordered]@{name='native-FishingCpu';action='QUALIFIED';originalExecutionId=$fishingLegacy.executionId;requiresCurrent=@();reason='unchanged related projection'})
    Write-WorkloadJson $profilePath $profileRecord
    $mixed=Read-WorkloadApplicability $fixture $profilePath $sourceLock $profileInputs $required
    Assert-Evidence (@($mixed.record.decisions|Where-Object {$_.action -ceq 'QUALIFIED'}).Count -eq 2) 'Strategy reviewed lifetime delta and unchanged Fishing qualify together'
    # Strategy is explicitly executed here so only the unreviewed Fishing
    # qualification can cause rejection of the changed fishing leaf.
    $fishingChanged=$profileRecord|ConvertTo-Json -Depth 20|ConvertFrom-Json
    @($fishingChanged.decisions|Where-Object name -CEQ 'native-NpcStrategy')[0].action='EXECUTE'
    $changed=$newRows+@('tests/NativeWorldTextProbe/NativeFishingChecks.cs:'+('F'*64))
    $fishingChanged.inputFingerprint=Get-WorkloadHash $changed;$fishingChanged.inputSets[0].differences=@(Get-WorkloadInputDifferences $oldRows $changed)
    Write-WorkloadJson $profilePath $fishingChanged
    $blocked=$false;$failure='';try{$null=Read-WorkloadApplicability $fixture $profilePath $sourceLock ([pscustomobject]@{inputs=$changed;fingerprint=(Get-WorkloadHash $changed)}) $required}catch{$blocked=$true;$failure=$_.Exception.Message}
    Assert-Evidence ($blocked -and $failure -like '*explicit reviewed task proof: native-FishingCpu') 'lifetime profile cannot qualify changed Fishing inputs'
    foreach($extra in @('tests/NativeWorldTextProbe/NativeCombatFamilyControlChecks.cs','tests/NativeWorldTextProbe/NativeCombatFlyingTailChecks.cs','src/JueMingR.TerrariaHost/Combat/NpcPredictionSource.cs','scripts/verify-existing-package.ps1',$lifetime)){
        $changed=if($extra -ceq $lifetime){@($newRows[0],($extra+':'+('F'*64)))}else{$newRows+@($extra+':'+('F'*64))};$profileRecord.inputFingerprint=Get-WorkloadHash $changed
        $profileRecord.inputSets[0].differences=@(Get-WorkloadInputDifferences $oldRows $changed);Write-WorkloadJson $profilePath $profileRecord
        $blocked=$false;try{$null=Read-WorkloadApplicability $fixture $profilePath $sourceLock ([pscustomobject]@{inputs=$changed;fingerprint=(Get-WorkloadHash $changed)}) $required}catch{$blocked=$true}
        Assert-Evidence $blocked ('lifetime profile rejects unreviewed delta/assertion bytes: '+$extra)
    }
    Save-WorkloadApplicabilityPointer $fixture $sourceLock $locked $appPath
    Assert-Evidence ((Read-WorkloadApplicabilityPointer $fixture $sourceLock $locked) -ceq $appPath) 'finite pointer survives absence of a Debug build record before compilation'
    $pointerPath=Join-Path $fixture 'artifacts/build/workload-checkpoints/current-applicability.json'
    $pointerOriginal=[IO.File]::ReadAllText($pointerPath)
    [IO.File]::WriteAllText($pointerPath,'{partial')
    $rejected=$false;try{$null=Read-WorkloadApplicabilityPointer $fixture $sourceLock $locked}catch{$rejected=$true}
    Assert-Evidence $rejected 'damaged adopted pointer cannot silently revert to full execution'
    [IO.File]::WriteAllText($pointerPath,$pointerOriginal,(New-Object Text.UTF8Encoding($false)))
    Assert-Evidence ($null -eq (Read-WorkloadApplicabilityPointer $fixture $sourceLock $different)) 'normal stale pointer is preserved but cannot qualify the current candidate'
    Assert-Evidence ([IO.File]::ReadAllText($pointerPath) -ceq $pointerOriginal) 'stale pointer original is not re-signed'
    $pointerBatch=New-WorkloadCheckpointBatch $fixture $sourceLock $locked @('legacy-one') $admitted
    $savedBatch=Read-WorkloadJson $pointerBatch.path
    Assert-Evidence ($savedBatch.applicabilityRecord -ceq $appPath -and $savedBatch.applicabilitySha256 -ceq $admitted.sha256) 'immutable checkpoint batch retains finite record identity'
    $legacyCheck=[pscustomobject]@{name='legacy-one';executable=$stderrExe;arguments=@('0')}
    $original=Get-WorkloadQualifiedOriginal $fixture $admitted $legacyCheck $locked
    Assert-Evidence ($null -ne $original -and $original.executionId -ceq $legacy.executionId -and $original.sourceCommit -ceq $legacy.sourceCommit) 'finite qualification retains original execution/source identity'
    $legacyReceipt=$completed | ConvertTo-Json -Depth 12 | ConvertFrom-Json
    $legacyReceipt.name=$legacy.name;$legacyReceipt.signature=$legacy.signature
    $legacyReceipt.path=Join-Path (Split-Path -Parent $completed.path) 'stable-legacy.json'
    Write-WorkloadJson $legacyReceipt.path $legacyReceipt
    $legacyLatest=Join-Path $fixture 'artifacts/build/workload-checkpoints/latest-legacy-one.json'
    Write-WorkloadJson $legacyLatest ([ordered]@{executionId=$legacyReceipt.executionId;path=$legacyReceipt.path})
    Complete-WorkloadCheckpointQualification $fixture @($legacyReceipt) $batch
    Assert-Evidence ($null -ne (Get-WorkloadQualifiedOriginal $fixture $admitted $legacyCheck $locked)) 'matching stable latest permits reviewed cross-candidate original'
    Assert-Evidence (Test-WorkloadReusable $fixture $legacy $locked $legacy.name $legacy.signature) 'matching stable latest has identical direct reuse semantics'
    foreach($status in @('FAILED','RUNNING','PREPARING','EXECUTED_AWAITING_STABILITY')) {
        $badLatest=Join-Path (Split-Path -Parent $completed.path) 'bad-latest.json'
        $badReceipt=$legacyReceipt | ConvertTo-Json -Depth 12 | ConvertFrom-Json
        $badReceipt.path=$badLatest;$badReceipt.executionId=[Guid]::NewGuid().ToString('N');$badReceipt.status=$status
        Write-WorkloadJson $badLatest $badReceipt
        Write-WorkloadJson $legacyLatest ([ordered]@{executionId=$badReceipt.executionId;path=$badLatest})
        Assert-Evidence ($null -eq (Get-WorkloadQualifiedOriginal $fixture $admitted $legacyCheck $locked) -and -not (Test-WorkloadReusable $fixture $legacy $locked $legacy.name $legacy.signature)) ('later '+$status+' retires both direct and cross-candidate reuse')
    }
    [IO.File]::WriteAllText($legacyLatest,'{corrupt')
    Assert-Evidence ($null -eq (Get-WorkloadQualifiedOriginal $fixture $admitted $legacyCheck $locked)) 'corrupt latest never revives qualified old PASS'
    Write-WorkloadJson $legacyLatest ([ordered]@{executionId=$legacyReceipt.executionId;path=$legacyReceipt.path})
    $rejected=$false;try{$null=Read-WorkloadApplicability $fixture $appPath $sourceLock $locked @('legacy-one','missing')}catch{$rejected=$true}
    Assert-Evidence $rejected 'JSON cannot shrink actual cumulative obligations'
    $otherScope=[pscustomobject]@{name='legacy-one';executable=$stderrExe;arguments=@('7')}
    Assert-Evidence ($null -eq (Get-WorkloadQualifiedOriginal $fixture $admitted $otherScope $locked)) 'changed actual scope/command cannot borrow qualified old execution'
    $changedApplication=$application | ConvertTo-Json -Depth 12 | ConvertFrom-Json
    $changedApplication.inputFingerprint=$differentFingerprint=Get-WorkloadHash @('source:changed')
    Write-WorkloadJson $appPath $changedApplication
    $rejected=$false;try{$null=Read-WorkloadApplicability $fixture $appPath $sourceLock ([pscustomobject]@{fingerprint=$differentFingerprint;inputs=@('source:changed')}) @('legacy-one')}catch{$rejected=$true}
    Assert-Evidence $rejected 'matching top lock cannot hide an unexplained original-to-candidate source change'
    $changedApplication.inputFingerprint=$differentFingerprint=Get-WorkloadHash @('source:locked','environment:DefineConstants:CHANGED')
    $changedApplication.inputSets[0].differences=@('environment:DefineConstants|<absent>|CHANGED')
    Write-WorkloadJson $appPath $changedApplication
    $rejected=$false;try{$null=Read-WorkloadApplicability $fixture $appPath $sourceLock ([pscustomobject]@{fingerprint=$differentFingerprint;inputs=@('source:locked','environment:DefineConstants:CHANGED')}) @('legacy-one')}catch{$rejected=$true}
    Assert-Evidence $rejected 'a manually listed changed symbol/environment is still outside the fixed source qualification'
    Write-WorkloadJson $appPath $application
    $outputOriginal=[IO.File]::ReadAllText($completed.outputPath)
    [IO.File]::AppendAllText($completed.outputPath,'damaged output')
    Assert-Evidence (-not (Test-WorkloadCheckpointQualified $fixture $completed)) 'changed command output cannot retain stability qualification'
    [IO.File]::WriteAllText($completed.outputPath,$outputOriginal,(New-Object Text.UTF8Encoding($false)))
    Assert-Evidence (Test-WorkloadCheckpointQualified $fixture $completed) 'restored original output identity remains qualified'
    # Controlled source/build projections isolate the real delivery qualifier.
    # Catalogue child, original archive, command and JSON/hash checks stay real;
    # this is a validator control, not a Release build/package acceptance.
    & {
        param($testRoot,$oldSource,$inputLock,$oldCheck,$oldEntry,$recordPath,$recordValue)
        $currentSource=[pscustomobject]@{commit=('c'*40);fingerprint=$oldSource.fingerprint}
        function Get-WorkloadIdentity {param($Root) return $currentSource}
        function Get-WorkloadEvidenceInput {param($Root,$Identity) return $inputLock}
        function Get-WorkloadChanges {param($Root,$Baseline) return [pscustomobject]@{reason='';paths=@()}}
        function Get-WorkloadRoute {param($Paths) return [pscustomobject]@{groups=@('controlled');unknown=@()}}
        function Get-WorkloadPlan {param($Root,$ChecksRoot,$Architecture,$Catalog,$Groups) return $oldCheck}
        function Test-WorkloadBuildMatch {param($Root,$Record,$Identity) return $true}
        function Test-WorkloadOutputs {param($Root,$Outputs) return $true}
        $catalogPath=Join-Path $testRoot 'artifacts/build/Debug/work/bin/JueMingR.ArchitectureTests/x86/Debug/net472/JueMingR.ArchitectureTests.exe'
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($catalogPath)) | Out-Null
        [IO.File]::Copy($oldCheck.executable,$catalogPath,$true)
        Write-WorkloadJson (Join-Path $testRoot 'artifacts/build/Debug/build-record.json') ([ordered]@{configuration='Debug'})
        $candidateApp=$recordValue | ConvertTo-Json -Depth 12 | ConvertFrom-Json
        $candidateApp.commit=$currentSource.commit
        Write-WorkloadJson $recordPath $candidateApp
        $cachePath=Join-Path $testRoot 'artifacts/build/workload-evidence.json'
        Write-WorkloadJson $cachePath ([ordered]@{schemaVersion=2;results=@($oldEntry)})
        $receipt=[ordered]@{name='legacy-one';result='PASS';disposition='QUALIFIED';executionId=$oldEntry.executionId;sourceCommit=$oldEntry.sourceCommit;qualificationId=$candidateApp.qualificationId}
        $delivery=[ordered]@{schemaVersion=4;clean=$true;configuration='Release';sdk='10.0.203';commit=$currentSource.commit;sourceFingerprint=$currentSource.fingerprint;inputFingerprint=$inputLock.fingerprint;outputs=@();workload=[ordered]@{status='PASS';mode='Related';requestedBaseline='';inputFingerprint=$inputLock.fingerprint;requiredChecks=@('legacy-one');checkCount=1;results=@($receipt);applicabilityRecord=$recordPath;applicabilitySha256=(Get-FileHash -LiteralPath $recordPath).Hash}}
        $delivery=$delivery | ConvertTo-Json -Depth 12 | ConvertFrom-Json
        $receipt=$delivery.workload.results[0]
        Assert-Evidence (Test-WorkloadDelivery $testRoot $delivery) 'actual Release qualifier consumes locked finite record while retaining historical source'
        $receipt.sourceCommit=$currentSource.commit
        Assert-Evidence (-not (Test-WorkloadDelivery $testRoot $delivery)) 'unchanged historical ID cannot be retagged to candidate source in result'
        $receipt.sourceCommit=$oldEntry.sourceCommit
        $retagged=$oldEntry | ConvertTo-Json -Depth 10 | ConvertFrom-Json
        $retagged.sourceCommit=$currentSource.commit
        Write-WorkloadJson $cachePath ([ordered]@{schemaVersion=2;results=@($retagged)})
        Assert-Evidence (-not (Test-WorkloadDelivery $testRoot $delivery)) 'unchanged historical ID cannot be retagged to candidate source in cache'
        Write-WorkloadJson $cachePath ([ordered]@{schemaVersion=2;results=@($oldEntry)})
        $delivery.workload.applicabilitySha256=('0'*64)
        Assert-Evidence (-not (Test-WorkloadDelivery $testRoot $delivery)) 'Release refuses missing or changed finite record identity'
    } $fixture $sourceLock $locked $legacyCheck $legacy $appPath $application
    Write-WorkloadJson $appPath $application
    # A real compiler option changes the executable's observable conclusion,
    # while receipt/cache/applicability and Release consumption stay real.
    & {
        $recipeRoot=Join-Path $fixture 'behavior-recipe';[IO.Directory]::CreateDirectory($recipeRoot)|Out-Null
        $recipePaths=@('scripts/build.ps1','scripts/workload/Workload.Evidence.ps1','scripts/test-workload-regressions.ps1','scripts/test-world-object-text.ps1')
        foreach($relative in $recipePaths){$target=Join-Path $recipeRoot $relative;[IO.Directory]::CreateDirectory((Split-Path -Parent $target))|Out-Null;[IO.File]::Copy((Join-Path $root $relative),$target)}
        $project=Join-Path $recipeRoot 'Tiny.csproj';$tinySource=Join-Path $recipeRoot 'Tiny.cs'
        [IO.File]::WriteAllText($project,'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net472</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems><AutomaticallyUseReferenceAssemblyPackages>false</AutomaticallyUseReferenceAssemblyPackages></PropertyGroup><ItemGroup><Compile Include="Tiny.cs" /></ItemGroup></Project>')
        [IO.File]::WriteAllText($tinySource,"using System; class Tiny { static void Main() {`n#if BEHAVIOR_CHANGED`nConsole.WriteLine(`"CHANGED`");`n#else`nConsole.WriteLine(`"BASE`");`n#endif`n} }")
        $fixtureProject=Join-Path $recipeRoot 'tests/Tiny';[IO.Directory]::CreateDirectory($fixtureProject)|Out-Null
        [IO.File]::Copy($project,(Join-Path $fixtureProject 'Tiny.csproj'));[IO.File]::Copy($tinySource,(Join-Path $fixtureProject 'Tiny.cs'))
        $differentProject=Join-Path $recipeRoot 'tests/DifferentFixture';[IO.Directory]::CreateDirectory($differentProject)|Out-Null
        [IO.File]::WriteAllText((Join-Path $differentProject 'DifferentFixture.csproj'),[IO.File]::ReadAllText($project).Replace('<OutputType>','<AssemblyName>Tiny</AssemblyName><DefineConstants>BEHAVIOR_CHANGED</DefineConstants><OutputType>'))
        [IO.File]::Copy($tinySource,(Join-Path $differentProject 'Tiny.cs'))
        [IO.File]::WriteAllText((Join-Path $recipeRoot '.gitignore'),"artifacts/`nobj/`nbin/`nlegacy.json`napplicability.json`n")
        & git -C $recipeRoot init --quiet
        & git -C $recipeRoot -c core.autocrlf=false add scripts tests Tiny.cs Tiny.csproj .gitignore
        & git -C $recipeRoot -c user.name=WorkloadFixture -c user.email=fixture@example.invalid commit --quiet -m 'disposable original recipe'
        if($LASTEXITCODE -ne 0){throw 'Disposable recipe history could not be recorded.'}
        $recipeExe=Join-Path $recipeRoot 'artifacts/build/Debug/checks/tiny/Tiny.exe'
        function Invoke-TinyRecipe {
            # Evaluate the actual production parameter owner, then let MSBuild
            # consume that vector against this small isolated project.
            $tokens=$null;$errors=$null;$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $recipeRoot 'scripts/build.ps1'),[ref]$tokens,[ref]$errors)
            $owner=@($ast.FindAll({param($node) $node -is [Management.Automation.Language.AssignmentStatementAst] -and $node.Left.Extent.Text -ieq '$buildArguments'},$true))
            if($errors.Count -or $owner.Count -lt 1){throw 'Tiny recipe parameter owner is ambiguous.'}
            $solutionPath=$project;$Configuration='Debug';$workRoot=Join-Path $recipeRoot 'artifacts/build/Debug/work';$referencesDirectory='unused';$harmonyReferencesDirectory='unused';$commit='fixture'
            $extra=@($ast.FindAll({param($node) $node -is [Management.Automation.Language.AssignmentStatementAst] -and $node.Left.Extent.Text -ceq '$extraOption'},$true))
            if($extra.Count -eq 1){. ([scriptblock]::Create($extra[0].Extent.Text))}
            foreach($assignment in $owner){. ([scriptblock]::Create($assignment.Extent.Text))}
            if($case -in @('fixture-option','fixture-project')){
                $recipeAst=[Management.Automation.Language.Parser]::ParseFile((Join-Path $recipeRoot 'scripts/workload/Workload.Evidence.ps1'),[ref]$tokens,[ref]$errors)
                $fixtureOwner=@($recipeAst.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq 'Ensure-WorkloadFixture'},$true))[0]
                $compileCall=@($fixtureOwner.FindAll({param($node) $node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -ceq 'dotnet.exe'},$true))[0]
                $Root=$recipeRoot;$Project='Tiny';$checks=$workRoot
                foreach($assignment in $fixtureOwner.FindAll({param($node) $node -is [Management.Automation.Language.AssignmentStatementAst] -and $node.Left.Extent.Text -ieq '$Project'},$true)){. ([scriptblock]::Create($assignment.Extent.Text))}
                . ([scriptblock]::Create($compileCall.Extent.Text+' -p:OutputPath="'+[IO.Path]::GetDirectoryName($recipeExe)+'\" | Out-Host'))
            }elseif($case -ceq 'release-debug-option'){
                $debugWork=$workRoot;$dotnetCommand=Get-Command dotnet.exe
                $compileCall=@($ast.FindAll({param($node) $node -is [Management.Automation.Language.CommandAst] -and $node.CommandElements[0].Extent.Text -ieq '$dotnetCommand.Source' -and $node.CommandElements[1].Extent.Text -ieq 'build'},$true))[0]
                . ([scriptblock]::Create($compileCall.Extent.Text+' -p:OutputPath="'+[IO.Path]::GetDirectoryName($recipeExe)+'\" | Out-Host'))
            }else{& dotnet.exe @buildArguments ('-p:OutputPath='+[IO.Path]::GetDirectoryName($recipeExe)+'\') | Out-Host}
            if($LASTEXITCODE -ne 0){throw 'Actual small recipe compilation failed.'}
            return @(& $recipeExe)[0]
        }
        $case='original';Assert-Evidence ((Invoke-TinyRecipe) -ceq 'BASE') 'original real compile has the baseline behavior'
        $originalSource=Get-WorkloadIdentity $recipeRoot
        $oldInput=[pscustomobject]@{inputs=$originalSource.inputs;fingerprint=(Get-WorkloadHash $originalSource.inputs)}
        $oldCheck=[pscustomobject]@{name='native-FishingCpu';executable=$recipeExe;arguments=@()}
        $oldEntry=[pscustomobject]@{schemaVersion=2;status='PASS';name=$oldCheck.name;signature=(Get-WorkloadHash @($recipeExe));inputFingerprint=(Get-WorkloadCheckFingerprint $oldInput $oldCheck.name);allInputFingerprint=$oldInput.fingerprint;inputs=$oldInput.inputs;executionId=[Guid]::NewGuid().ToString('N');sourceCommit=$originalSource.commit;sourceFingerprint=$originalSource.fingerprint;detectionCommit=$originalSource.commit;milliseconds=1;executedUtc=[DateTime]::UtcNow.ToString('o');outputs=@(Save-WorkloadArtifacts $recipeRoot @(Get-WorkloadEvidenceOutputs $recipeRoot ([pscustomobject]@{outputs=@()}) $recipeExe))}
        $originalBuild=[IO.File]::ReadAllText((Join-Path $recipeRoot 'scripts/build.ps1'))
        $originalEvidence=[IO.File]::ReadAllText((Join-Path $recipeRoot 'scripts/workload/Workload.Evidence.ps1'))
        foreach($unsupported in @("`$BuildArguments[0]='build'","`$BuildArguments.SetValue('build',0)","Set-Variable -Name BuildArguments -Value @('build')")){
            $rejected=$false
            try{$null=Get-WorkloadBehaviorRecipeProjection 'scripts/build.ps1' $originalBuild.Replace('& $dotnetCommand.Source @buildArguments',($unsupported+"`n& `$dotnetCommand.Source @buildArguments"))}catch{$rejected=$true}
            Assert-Evidence $rejected 'finite owner refuses index/member/indirect mutation instead of claiming equivalence'
        }
        $extraCompile=$originalBuild+"`n& `$dotnetCommand.Source build `$solutionPath`n"
        $rejected=$false;try{$null=Get-WorkloadBehaviorRecipeProjection 'scripts/build.ps1' $extraCompile}catch{$rejected=$true}
        Assert-Evidence $rejected 'unregistered additional compile cannot claim equivalence'
        foreach($case in @('output-only','release-debug-option','case-append','fixture-project','compiler-option','indirect-option','fixture-option','execution-scope')) {
            $candidateBuild=$originalBuild
            if($case -ceq 'output-only'){$candidateBuild+="`nWrite-Host 'unrelated tool report wording'`n"}
            if($case -ceq 'compiler-option'){$candidateBuild=$candidateBuild.Replace("'-p:Platform=x86',","'-p:Platform=x86', '-p:DefineConstants=BEHAVIOR_CHANGED',")}
            if($case -ceq 'release-debug-option'){$candidateBuild=$candidateBuild.Replace('-p:Platform=x86 "-p:JueMingRBuildRoot=$debugWork"','-p:Platform=x86 -p:DefineConstants=BEHAVIOR_CHANGED "-p:JueMingRBuildRoot=$debugWork"')}
            if($case -ceq 'case-append'){$candidateBuild=$candidateBuild.Replace('& $dotnetCommand.Source @buildArguments',"`$BuildArguments += '-p:DefineConstants=BEHAVIOR_CHANGED'`n& `$dotnetCommand.Source @buildArguments")}
            if($case -ceq 'indirect-option'){$candidateBuild=$candidateBuild.Replace('$buildArguments = @(',"`$extraOption='-p:DefineConstants=BEHAVIOR_CHANGED'`n`$buildArguments = @(").Replace("'-p:Platform=x86',","'-p:Platform=x86', `$extraOption,")}
            [IO.File]::WriteAllText((Join-Path $recipeRoot 'scripts/build.ps1'),$candidateBuild,(New-Object Text.UTF8Encoding($false)))
            $candidateEvidence=if($case -ceq 'fixture-option'){$originalEvidence.Replace(' --configuration Debug --nologo -p:Platform=x86',' --configuration Debug --nologo -p:Platform=x86 -p:DefineConstants=BEHAVIOR_CHANGED')}elseif($case -ceq 'fixture-project'){$originalEvidence.Replace('param([string] $Root,[string] $Project,$InputIdentity)',"param([string] `$Root,[string] `$Project,`$InputIdentity)`n    `$Project='DifferentFixture'")}else{$originalEvidence}
            [IO.File]::WriteAllText((Join-Path $recipeRoot 'scripts/workload/Workload.Evidence.ps1'),$candidateEvidence,(New-Object Text.UTF8Encoding($false)))
            $runner=Join-Path $recipeRoot 'scripts/test-workload-regressions.ps1'
            $originalRunner=[IO.File]::ReadAllText((Join-Path $root 'scripts/test-workload-regressions.ps1'))
            [IO.File]::WriteAllText($runner,$(if($case -ceq 'execution-scope'){$originalRunner.Replace('Invoke-WorkloadProcess $check.name $check.executable $check.arguments -Checkpoint $attempt',"Invoke-WorkloadProcess `$check.name `$check.executable @('changed-scope') -Checkpoint `$attempt")}else{$originalRunner}),(New-Object Text.UTF8Encoding($false)))
            $observed=Invoke-TinyRecipe
            Assert-Evidence ($observed -ceq $(if($case -in @('release-debug-option','case-append','fixture-project','compiler-option','indirect-option','fixture-option')){'CHANGED'}else{'BASE'})) ('real compiled behavior: '+$case)
            $currentSource=Get-WorkloadIdentity $recipeRoot
            $inputLock=[pscustomobject]@{inputs=$currentSource.inputs;fingerprint=(Get-WorkloadHash $currentSource.inputs)}
            $expected=$case -ceq 'output-only'
            Assert-Evidence ((Test-WorkloadReusable $recipeRoot $oldEntry $inputLock $oldCheck.name $oldEntry.signature) -eq $expected) ('real REUSED boundary: '+$case)
            $legacyPath=Join-Path $recipeRoot 'legacy.json';Write-WorkloadJson $legacyPath ([ordered]@{schemaVersion=2;results=@($oldEntry)})
            $recordPath=Join-Path $recipeRoot 'applicability.json'
            $recordValue=[ordered]@{schema='fixed-workload-applicability-1';qualificationId=[Guid]::NewGuid().ToString('N');commit=$currentSource.commit;sourceFingerprint=$currentSource.fingerprint;inputFingerprint=$inputLock.fingerprint;requiredChecks=@($oldCheck.name);additionalChecks=@();legacyCachePath=$legacyPath;legacyCacheSha256=(Get-FileHash $legacyPath).Hash;inputSets=@([ordered]@{fingerprint=$oldInput.fingerprint;differences=@(Get-WorkloadInputDifferences $oldInput.inputs $inputLock.inputs)});decisions=@([ordered]@{name=$oldCheck.name;action='QUALIFIED';originalExecutionId=$oldEntry.executionId;requiresCurrent=@();reason='controlled original recipe'})}
            Write-WorkloadJson $recordPath $recordValue
            $qualified=$null;try{$qualified=Read-WorkloadApplicability $recipeRoot $recordPath $currentSource $inputLock @($oldCheck.name)}catch{}
            Assert-Evidence (($null -ne $qualified) -eq $expected) ('real QUALIFIED reader boundary: '+$case)
            $controlled=[pscustomobject]@{record=($recordValue|ConvertTo-Json -Depth 12|ConvertFrom-Json);legacy=[pscustomobject]@{results=@($oldEntry)}}
            $qualifiedOriginal=Get-WorkloadQualifiedOriginal $recipeRoot $controlled $oldCheck $inputLock
            Assert-Evidence (($null -ne $qualifiedOriginal) -eq $expected) ('direct qualified consumer cannot bypass recipe admission: '+$case)
            if($expected){Assert-Evidence ($qualifiedOriginal.executionId -ceq $oldEntry.executionId) 'unrelated output keeps original executionId'}
            & {
            function Get-WorkloadIdentity {param($Root) return $currentSource}
            function Get-WorkloadEvidenceInput {param($Root,$Identity) return $inputLock}
            function Get-WorkloadChanges {param($Root,$Baseline) return [pscustomobject]@{reason='';paths=@()}}
            function Get-WorkloadRoute {param($Paths) return [pscustomobject]@{groups=@('controlled');unknown=@()}}
            function Get-WorkloadPlan {param($Root,$ChecksRoot,$Architecture,$Catalog,$Groups) return $oldCheck}
            function Test-WorkloadBuildMatch {param($Root,$Record,$Identity) return $true}
            function Test-WorkloadOutputs {param($Root,$Outputs) return $true}
            $catalogPath=Join-Path $recipeRoot 'artifacts/build/Debug/work/bin/JueMingR.ArchitectureTests/x86/Debug/net472/JueMingR.ArchitectureTests.exe';[IO.Directory]::CreateDirectory((Split-Path -Parent $catalogPath))|Out-Null;[IO.File]::Copy($recipeExe,$catalogPath,$true)
            Write-WorkloadJson (Join-Path $recipeRoot 'artifacts/build/Debug/build-record.json') ([ordered]@{configuration='Debug';commit=$currentSource.commit})
            Write-WorkloadJson (Join-Path $recipeRoot 'artifacts/build/workload-evidence.json') ([ordered]@{schemaVersion=2;results=@($oldEntry)})
            foreach($disposition in @('REUSED','QUALIFIED')) {
                $receipt=[ordered]@{name=$oldCheck.name;result='PASS';disposition=$disposition;executionId=$oldEntry.executionId;sourceCommit=$oldEntry.sourceCommit;qualificationId=$recordValue.qualificationId}
                $workload=[ordered]@{status='PASS';mode='Related';requestedBaseline='';inputFingerprint=$inputLock.fingerprint;requiredChecks=@($oldCheck.name);checkCount=1;results=@($receipt)}
                if($disposition -ceq 'QUALIFIED'){$workload.applicabilityRecord=$recordPath;$workload.applicabilitySha256=(Get-FileHash $recordPath).Hash}
                $delivery=[ordered]@{schemaVersion=4;clean=$true;configuration='Release';sdk='10.0.203';commit=$currentSource.commit;sourceFingerprint=$currentSource.fingerprint;inputFingerprint=$inputLock.fingerprint;outputs=@();workload=$workload}
                Assert-Evidence ((Test-WorkloadDelivery $recipeRoot ($delivery|ConvertTo-Json -Depth 12|ConvertFrom-Json)) -eq $expected) ('real Delivery '+$disposition+' boundary: '+$case)
            }
            }
        }
    }
    $latestPath=Join-Path $fixture ('artifacts/build/workload-checkpoints/latest-'+$check.name+'.json')
    Remove-Item -LiteralPath $latestPath
    Assert-Evidence (-not (Test-WorkloadReusable $fixture $qualifiedEntry $locked $check.name 'signature')) 'deleted latest cannot downgrade a new checkpoint row to legacy PASS'
    [IO.File]::WriteAllText($latestPath,'{truncated')
    Assert-Evidence (-not (Test-WorkloadReusable $fixture $qualifiedEntry $locked $check.name 'signature')) 'damaged retirement marker cannot fall back to otherwise reusable old PASS'
    Write-WorkloadJson $latestPath ([ordered]@{executionId=$completed.executionId;path=$completed.path})
    $next=Start-WorkloadCheckpoint $fixture $batch $check 'signature'
    Assert-Evidence ($next.executionId -cne $completed.executionId -and -not (Test-WorkloadCheckpointQualified $fixture $completed)) 'running next attempt retires old success without rewriting its original receipt'
    $interrupted=Read-WorkloadJson $next.path
    Assert-Evidence ($interrupted.status -ceq 'PREPARING' -and $null -eq $interrupted.endedUtc -and $null -eq $interrupted.exitCode) 'unfinished invocation has no invented exit/end/PASS'
    $check.name='checkpoint-two';$check.arguments=@('7')
    $bad=Start-WorkloadCheckpoint $fixture $batch $check 'signature-seven'
    $failed=$false
    try {Invoke-WorkloadProcess $check.name $check.executable $check.arguments -Checkpoint $bad} catch {$failed=$true}
    $badResult=Read-WorkloadJson $bad.path
    Assert-Evidence ($failed -and $badResult.status -ceq 'FAILED' -and $badResult.exitCode -eq 7 -and -not (Test-WorkloadCheckpointQualified $fixture $badResult)) 'failed actual child preserves nonzero evidence and cannot qualify'
    $priorUnknown=[Environment]::GetEnvironmentVariable('JUEMINGR_UNKNOWN_ONLY')
    try {
        $env:JUEMINGR_UNKNOWN_ONLY='1';$rejected=$false
        try {$null=Start-WorkloadCheckpoint $fixture $batch $check 'unknown'} catch {$rejected=$_.Exception.Message.Contains('Unknown inherited')}
        Assert-Evidence $rejected 'unknown inherited scope cannot masquerade as complete named scope'
    } finally {[Environment]::SetEnvironmentVariable('JUEMINGR_UNKNOWN_ONLY',$priorUnknown)}
    try {
        $env:JUEMINGR_FOUNDATION_TYPO_ONLY='1';$rejected=$false
        try {$null=Get-WorkloadClearedEnvironment} catch {$rejected=$_.Exception.Message.Contains('Unknown inherited')}
        Assert-Evidence $rejected 'foundation wildcard must not accept an unknown selector'
    } finally {Remove-Item Env:JUEMINGR_FOUNDATION_TYPO_ONLY}
    $qualification=$completed.path+'.qualification.json'
    [IO.File]::WriteAllText($qualification,'{"status":"STABLE"}')
    Assert-Evidence (-not (Test-WorkloadCheckpointQualified $fixture $completed)) 'partial or corrupt qualification fails closed'
    Remove-UnusedWorkloadArtifacts $fixture @()
    Assert-Evidence (Test-WorkloadEvidenceOutputs $fixture $archived) 'checkpoint-referenced historical artifacts survive cache garbage collection'
    $archiveBase=Join-Path $fixture 'artifacts/build/evidence-artifacts'
    $unused=Join-Path $archiveBase ('F'*64);$unknown=Join-Path $archiveBase ('E'*64)
    [IO.Directory]::CreateDirectory($unused)|Out-Null;[IO.Directory]::CreateDirectory($unknown)|Out-Null
    [IO.File]::WriteAllText((Join-Path $unused '0-owned.bin'),'discardable sample')
    [IO.File]::WriteAllText((Join-Path $unknown '0-owned.bin'),'uncertain sample')
    $unknownRecord=Join-Path $fixture 'artifacts/build/workload-checkpoints/unknown.pending'
    [IO.File]::WriteAllText($unknownRecord,('{"outputs":["'+$unknown.Replace('\','\\')+'\\0-owned.bin"'))
    Write-WorkloadJson (Join-Path $fixture 'artifacts/build/workload-checkpoints/historical-archive.json') ([ordered]@{outputs=$archived;status='RETAINED_ORIGINAL'})
    foreach($kind in @('undeclared','missing','outside')){
        $rejected=$false
        try{switch($kind){
            'undeclared'{Remove-UnusedWorkloadArtifacts $fixture @() -Collect}
            'missing'{Remove-UnusedWorkloadArtifacts $fixture @() -Collect -RetainedRecords (Join-Path $fixture 'missing.json')}
            'outside'{Remove-UnusedWorkloadArtifacts $fixture @() -Collect -RetainedRecords (Join-Path $root 'eng/TerrariaReferences.baseline.json')}
        }}catch{$rejected=$true}
        Assert-Evidence ($rejected -and [IO.Directory]::Exists($unused)) ('retention declaration refuses before deletion: '+$kind)
    }
    # A required indirect edge must be resolved before deciding that a real
    # archive is unused. Exercise the current PowerShell collector, not a model.
    $chainArchive=Join-Path $archiveBase ('D'*64)
    [IO.Directory]::CreateDirectory($chainArchive)|Out-Null
    [IO.File]::WriteAllText((Join-Path $chainArchive '0-owned.bin'),'indirect sample')
    $a=Join-Path $fixture 'retained-A.json';$b=Join-Path $fixture 'retained-B.json'
    Write-WorkloadJson $a ([ordered]@{applicabilityRecord=$b;description='optional missing.json is prose'})
    foreach($kind in @('missing','damaged','outside')) {
        if($kind -eq 'damaged'){[IO.File]::WriteAllText($b,'{bad')}
        if($kind -eq 'outside'){Write-WorkloadJson $a ([ordered]@{applicabilityRecord=(Join-Path (Split-Path -Parent $fixture) 'outside.json')})}
        $rejected=$false;try{Remove-UnusedWorkloadArtifacts $fixture @() -Collect -RetainedRecords @($a)}catch{$rejected=$true}
        Assert-Evidence ($rejected -and [IO.Directory]::Exists($chainArchive) -and [IO.Directory]::Exists($unused)) ('indirect '+$kind+' prevents deletion of uncertain archives')
    }
    Write-WorkloadJson $a ([ordered]@{applicabilityRecord=$b;description=(Join-Path $fixture 'optional-prose.json')})
    Write-WorkloadJson $b ([ordered]@{outputs=@([ordered]@{path=(Join-Path $chainArchive '0-owned.bin')});batchPath=$a})
    # Incomplete recovery JSON is itself an uncertainty, not an archive index.
    $rejected=$false;try{Remove-UnusedWorkloadArtifacts $fixture @() -Collect -RetainedRecords @($a)}catch{$rejected=$true}
    Assert-Evidence ($rejected -and [IO.Directory]::Exists($unused)) 'damaged recovery record blocks the selected archive root'
    Write-WorkloadJson $unknownRecord ([ordered]@{outputs=@([ordered]@{path=(Join-Path $unknown '0-owned.bin')})})
    Remove-UnusedWorkloadArtifacts $fixture @() -Collect -RetainedRecords @($appPath,$a)
    Assert-Evidence ([IO.Directory]::Exists($chainArchive)) 'A to B to actual archive survives; circular record edge is deduplicated'
    Assert-Evidence (-not [IO.Directory]::Exists($unused) -and [IO.Directory]::Exists($unknown) -and (Test-WorkloadEvidenceOutputs $fixture $archived)) ('explicit collection: unused='+[IO.Directory]::Exists($unused)+' unknown='+[IO.Directory]::Exists($unknown)+' historical='+(Test-WorkloadEvidenceOutputs $fixture $archived))
    foreach($redirect in @('root','artifacts','artifacts/build','artifacts/build/evidence-artifacts')){
        $case=Join-Path $fixture ('junction-'+$redirect.Replace('/','-'));$external=Join-Path $case 'outside';$local=Join-Path $case 'repository'
        $link=if($redirect -eq 'root'){$local}else{Join-Path $local $redirect}
        $suffix=if($redirect -eq 'root'){'artifacts/build/evidence-artifacts'}elseif($redirect -eq 'artifacts'){'build/evidence-artifacts'}elseif($redirect -eq 'artifacts/build'){'evidence-artifacts'}else{''}
        $externalArchive=if($suffix){Join-Path $external $suffix}else{$external}
        $witness=Join-Path (Join-Path $externalArchive ('A'*64)) 'outside.bin'
        foreach($path in @($link,$external,$witness)){Assert-Evidence ([IO.Path]::GetFullPath($path).StartsWith($fixture+'\',[StringComparison]::OrdinalIgnoreCase)) 'junction sample paths remain in owned fixture'}
        foreach($parent in @((Split-Path -Parent $witness),(Split-Path -Parent $link))){[IO.Directory]::CreateDirectory($parent)|Out-Null}
        [IO.File]::WriteAllText($witness,'owned external witness')
        New-Item -ItemType Junction -Path $link -Target $external|Out-Null
        Remove-UnusedWorkloadArtifacts $local @() -Collect -RetainedRecords @()
        Assert-Evidence ([IO.File]::Exists($witness)) ('reparse ancestor refuses external deletion: '+$redirect)
    }
    $priorStrategy=$env:JUEMINGR_STRATEGY_ONLY;$priorFoundation=$env:JUEMINGR_FOUNDATION_STRATEGY_ONLY
    try {
        $env:JUEMINGR_STRATEGY_ONLY='facing';$env:JUEMINGR_FOUNDATION_STRATEGY_ONLY='1'
        $cleared=@(Invoke-WorkloadProcess 'only-contamination' $stderrExe @('only'))
        Assert-Evidence ($LASTEXITCODE -eq 0 -and $env:JUEMINGR_STRATEGY_ONLY -ceq 'facing' -and $env:JUEMINGR_FOUNDATION_STRATEGY_ONLY -ceq '1') 'formal child clears only pollution and restores parent environment'
    } finally {$env:JUEMINGR_STRATEGY_ONLY=$priorStrategy;$env:JUEMINGR_FOUNDATION_STRATEGY_ONLY=$priorFoundation}
    $captured=@(& { Invoke-WorkloadProcess 'stderr-success' $stderrExe @('0') } 2>&1)
    Assert-Evidence (($captured | Out-String).Contains('ordinary native information')) 'successful native stderr is retained under package capture'
    Assert-Evidence ($ErrorActionPreference -ceq 'Stop') 'success restores caller error preference'
    $failed=$false
    try { $null=@(& { Invoke-WorkloadProcess 'stderr-failure' $stderrExe @('7') } 2>&1) }
    catch { $failed=$_.Exception.Message.Contains('failed with exit 7') }
    Assert-Evidence ($failed -and $ErrorActionPreference -ceq 'Stop') 'native stderr cannot hide exit 7 and failure restores preference'
    $LASTEXITCODE=0; $failed=$false
    try { $null=@(& { Invoke-WorkloadProcess 'not-an-executable' $exe @() } 2>&1) }
    catch { $failed=$true }
    Assert-Evidence ($failed -and $ErrorActionPreference -ceq 'Stop') 'launch failure cannot reuse an earlier zero exit and restores preference'
    Write-Output 'PASS: real child checkpoint/retirement/qualification boundaries; evidence identity, changed bytes/options, missing/failed/partial/feedback receipts, old schema, stderr capture, real nonzero exit and launch failure.'
} finally {
    Stop-WorkloadReadWindow
    $resolved=[IO.Path]::GetFullPath($fixture);$temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if (-not $resolved.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($resolved).StartsWith('JueMingR-evidence-',[StringComparison]::Ordinal)) {throw 'Unsafe fixture cleanup.'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
