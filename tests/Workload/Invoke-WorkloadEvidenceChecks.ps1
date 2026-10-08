[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $root 'scripts/workload/Workload.Support.ps1')
function Assert-Evidence([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw ('Evidence contract: ' + $Message) }
}
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
foreach ($path in @('scripts/phase0s/Install-Phase0S.ps1','scripts/phase0s/Restore-Phase0S.ps1','scripts/phase0s/Phase0S.ScriptSupport.ps1','scripts/workload/Workload.Support.ps1','scripts/workload/Workload.Evidence.ps1','scripts/test-workload-regressions.ps1','tests/NativeWorldTextProbe/NativeChecks.cs','tests/NativeWorldTextProbe/NativeToolExecutionChecks.cs','environment:runtime')) {
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
    Assert-Evidence ($leafRoute.groups -contains $pair.group -and $leafRoute.groups -notcontains 'shared-host') ('precise leaf route: '+$pair.path)
    $old=[pscustomobject]@{inputs=@($pair.path+':A')};$new=[pscustomobject]@{inputs=@($pair.path+':B')}
    Assert-Evidence ((Get-WorkloadCheckFingerprint $old $pair.consumer) -cne (Get-WorkloadCheckFingerprint $new $pair.consumer)) 'new leaf invalidates actual consumer'
    Assert-Evidence ((Get-WorkloadCheckFingerprint $old 'native-FishingCpu') -ceq (Get-WorkloadCheckFingerprint $new 'native-FishingCpu')) 'new leaf does not claim unrelated fishing dependency'
    $leafPlan=@(Get-WorkloadPlan $root 'checks' 'architecture.exe' @() @($pair.group) | ForEach-Object {$_.name})
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
    Assert-Evidence (Test-WorkloadEvidenceOutputs $fixture $repaired) 'fresh matching execution repairs a damaged archive slot'
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
    $support=Join-Path $root 'scripts/workload/Workload.Evidence.ps1'
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
    Save-WorkloadApplicabilityPointer $fixture $sourceLock $locked $appPath
    Assert-Evidence ((Read-WorkloadApplicabilityPointer $fixture $sourceLock $locked) -ceq $appPath) 'finite pointer survives absence of a Debug build record before compilation'
    $pointerPath=Join-Path $fixture 'artifacts/build/workload-checkpoints/current-applicability.json'
    $pointerOriginal=[IO.File]::ReadAllText($pointerPath)
    [IO.File]::WriteAllText($pointerPath,'{partial')
    $rejected=$false;try{$null=Read-WorkloadApplicabilityPointer $fixture $sourceLock $locked}catch{$rejected=$true}
    Assert-Evidence $rejected 'damaged adopted pointer cannot silently revert to full execution'
    [IO.File]::WriteAllText($pointerPath,$pointerOriginal,(New-Object Text.UTF8Encoding($false)))
    $rejected=$false;try{$null=Read-WorkloadApplicabilityPointer $fixture $sourceLock $different}catch{$rejected=$true}
    Assert-Evidence $rejected 'changed candidate inputs require bounded qualification review'
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
    [IO.File]::WriteAllText((Join-Path $unused 'owned.bin'),'discardable sample')
    [IO.File]::WriteAllText((Join-Path $unknown 'owned.bin'),'uncertain sample')
    $unknownRecord=Join-Path $fixture 'artifacts/build/workload-checkpoints/unknown.pending'
    [IO.File]::WriteAllText($unknownRecord,('{"outputs":["'+$unknown.Replace('\','\\')+'\\owned.bin"'))
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
    Remove-UnusedWorkloadArtifacts $fixture @() -Collect -RetainedRecords @($appPath)
    Assert-Evidence (-not [IO.Directory]::Exists($unused) -and [IO.Directory]::Exists($unknown) -and (Test-WorkloadEvidenceOutputs $fixture $archived)) ('explicit collection: unused='+[IO.Directory]::Exists($unused)+' unknown='+[IO.Directory]::Exists($unknown)+' historical='+(Test-WorkloadEvidenceOutputs $fixture $archived))
    foreach($redirect in @('root','artifacts','artifacts/build','artifacts/build/evidence-artifacts')){
        $case=Join-Path $fixture ('junction-'+$redirect.Replace('/','-'));$external=Join-Path $case 'outside';$local=Join-Path $case 'repository'
        $link=if($redirect -eq 'root'){$local}else{Join-Path $local $redirect}
        $suffix=if($redirect -eq 'root'){'artifacts/build/evidence-artifacts'}elseif($redirect -eq 'artifacts'){'build/evidence-artifacts'}elseif($redirect -eq 'artifacts/build'){'evidence-artifacts'}else{''}
        $externalArchive=if($suffix){Join-Path $external $suffix}else{$external}
        $witness=Join-Path (Join-Path $externalArchive ('A'*64)) 'outside.bin'
        foreach($path in @($link,$external,$witness)){Assert-Evidence ([IO.Path]::GetFullPath($path).StartsWith($fixture+'\',[StringComparison]::OrdinalIgnoreCase)) 'junction sample paths remain in owned fixture'}
        New-Item -ItemType Directory -Path (Split-Path -Parent $witness),(Split-Path -Parent $link)|Out-Null
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
    $resolved=[IO.Path]::GetFullPath($fixture);$temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if (-not $resolved.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($resolved).StartsWith('JueMingR-evidence-',[StringComparison]::Ordinal)) {throw 'Unsafe fixture cleanup.'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
