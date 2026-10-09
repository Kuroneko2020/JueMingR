[CmdletBinding()]
param(
    [string] $Baseline,
    [ValidateSet('Related','Full','Feedback')][string] $Mode = 'Related',
    [switch] $Rerun,
    [string] $ApplicabilityRecord,
    [switch] $ClosureOnly,
    [string] $ApprovedRetainedAssets,
    [switch] $PlanOnly
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'workload/Workload.Support.ps1')
$admission=Initialize-WorkloadWorkspace $repositoryRoot $ApprovedRetainedAssets
$identity = Get-WorkloadIdentity $repositoryRoot
$inputIdentity = Get-WorkloadEvidenceInput $repositoryRoot $identity
$recordPath = Join-Path $repositoryRoot 'artifacts/build/Debug/build-record.json'
$record = Read-WorkloadJson $recordPath
# A supplied baseline is useful for feedback, but cannot shrink final obligations.
$changes = Get-WorkloadChanges $repositoryRoot $(if ($Mode -ceq 'Feedback') { $Baseline } else { '' })
if ($Mode -ceq 'Feedback' -and [string]::IsNullOrWhiteSpace($Baseline)) { throw 'Feedback requires an explicit comparison baseline; it cannot authorize delivery.' }
if ($Mode -cne 'Feedback' -and $Baseline) {
    $extra = Get-WorkloadChanges $repositoryRoot $Baseline
    if ($extra.reason) { throw $extra.reason }
    $changes.paths = @($changes.paths + $extra.paths | Sort-Object -Unique)
}
$selectedPaths=@($changes.paths)
if($null -ne $admission){$selectedPaths=@($selectedPaths | Where-Object {$_ -cnotin @($admission.files.path)})}
$route = Get-WorkloadRoute $selectedPaths
if ($changes.reason -or $route.unknown.Count -gt 0) { throw ($changes.reason + ' Unclassified paths: ' + ($route.unknown -join ', ')) }
if ($Mode -ceq 'Full') { $route = Get-WorkloadRoute @('@full') }
$checksRoot = Join-Path $repositoryRoot 'artifacts/build/Debug/checks'
$architecture = Join-Path $repositoryRoot 'artifacts/build/Debug/work/bin/JueMingR.ArchitectureTests/x86/Debug/net472/JueMingR.ArchitectureTests.exe'
$toolOnly=@($route.groups | Where-Object {$_ -notmatch '^(core|workload-tools|package-tools|check:workload-.*)$'}).Count -eq 0
if($toolOnly){
    $catalog=@();$record=[pscustomobject]@{schemaVersion=4;commit=$identity.commit;outputs=@();workload=[pscustomobject]@{status='PENDING'}}
    Write-Host 'TOOL SCOPE: no product compilation or business process required; product delivery NOT_EVALUATED.'
} else {
    & (Join-Path $PSScriptRoot 'prepare-terraria-references.ps1') -VerifyOnly | Out-Host
    & (Join-Path $PSScriptRoot 'prepare-harmony.ps1') -VerifyOnly | Out-Host
    if ((& dotnet.exe --version).Trim() -cne '10.0.203' -or $LASTEXITCODE -ne 0) { throw 'Locked SDK unavailable.' }
    if (-not (Test-WorkloadBuildMatch $repositoryRoot $record $identity $inputIdentity)) {throw 'Debug detection binaries do not match current compile inputs/SDK/output hashes.'}
    $catalog=@(& $architecture --list-checks)
    if($LASTEXITCODE -ne 0 -or $catalog.Count -eq 0){throw 'The business check catalogue is unavailable.'}
}
$recordPath=if($toolOnly){Join-Path $repositoryRoot 'artifacts/build/tool-workload-record.json'}else{$recordPath}
$plan = @(Get-WorkloadPlan $repositoryRoot $checksRoot $architecture $catalog $route.groups)
$required = @($plan | ForEach-Object { $_.name })
if($PlanOnly){
    [ordered]@{status='PLAN_ONLY';mode=$Mode;baseline=$changes.baseline;paths=$selectedPaths;groups=$route.groups;required=$required;productDelivery='NOT_EVALUATED'} | ConvertTo-Json -Depth 6
    return
}
# The ordinary Release/package path may consume the same already locked Debug
# qualification. A missing or changed record must not silently trigger a full
# fallback run or erase the remaining cumulative responsibilities.
if (-not $ApplicabilityRecord) {try{$ApplicabilityRecord=Read-WorkloadApplicabilityPointer $repositoryRoot $identity $inputIdentity}catch{
    if($Mode -cne 'Feedback'){throw}
    Write-Host ('UNAVAILABLE qualification: '+$_.Exception.Message+'; independent new feedback only; delivery blocked.')
}}
if (-not $ApplicabilityRecord -and $record.workload.PSObject.Properties.Name -contains 'applicabilityRecord') {
    $oldPath=$record.workload.applicabilityRecord
    if (-not [IO.File]::Exists($oldPath) -or (Get-FileHash -LiteralPath $oldPath).Hash -cne $record.workload.applicabilitySha256) {throw 'Saved applicability record changed.'}
    $old=Read-WorkloadJson $oldPath
    if($null -eq $old){throw 'Saved applicability record damaged.'}
    if($old.commit -ceq $identity.commit -and $old.sourceFingerprint -ceq $identity.fingerprint -and $old.inputFingerprint -ceq $inputIdentity.fingerprint){$ApplicabilityRecord=$oldPath}else{Write-Host ('STALE build qualification preserved: '+$oldPath+'; not used for current candidate.')}
}
$applicability=$null
if ($ApplicabilityRecord) {
    $applicability=Read-WorkloadApplicability $repositoryRoot $ApplicabilityRecord $identity $inputIdentity $required
    Save-WorkloadApplicabilityPointer $repositoryRoot $identity $inputIdentity $ApplicabilityRecord
    $plan+=@($applicability.additional);$required=@($plan | ForEach-Object {$_.name})
}
$knownNames = @((Get-WorkloadPlan $repositoryRoot $checksRoot $architecture $catalog (Get-WorkloadRoute @('@full')).groups) | ForEach-Object {$_.name})
if ($null -ne $applicability) {$knownNames+=@($applicability.additional | ForEach-Object {$_.name})}
if (@($required | Sort-Object -Unique).Count -ne $required.Count) { throw 'Duplicate check identity in plan.' }
$cachePath = Join-Path $repositoryRoot 'artifacts/build/workload-evidence.json'
# Reuse shared records/archive hashes once inside a controlled read phase.
# Source and live process inputs are freshly confirmed at phase boundaries.
Start-WorkloadReadWindow
$cache = Read-WorkloadJson $cachePath
$entries = @{}
if ($null -ne $cache -and $null -ne $cache.PSObject.Properties['schemaVersion'] -and $cache.schemaVersion -eq 2) {
    foreach ($entry in $cache.results) {
        if($entries.ContainsKey($entry.name)){throw 'Duplicate saved check identity.'}
        # A tooling-only invocation has no product catalogue to retire IDs.
        # Preserve originals outside this plan; never turn NOT_SELECTED into PASS.
        $entries[$entry.name] = $entry
    }
} elseif ($null -ne $cache) { Write-Host 'INVALIDATED: executable input/dependency/environment set changed or old evidence schema.' }
$results = New-Object 'System.Collections.Generic.List[object]'
$pending = @{}
$liveChecks = @{}
$built = @{}
$currentCheck = 'planning'
$batch=New-WorkloadCheckpointBatch $repositoryRoot $identity $inputIdentity $required $applicability
$completedReceipts=New-Object 'System.Collections.Generic.List[object]'
function Save-Evidence {
    Write-WorkloadJson $cachePath ([ordered]@{ schemaVersion=2; inputFingerprint=$inputIdentity.fingerprint; results=@($entries.Values | Sort-Object name) })
}
function Assert-StableInputs {
    $now = Get-WorkloadIdentity $repositoryRoot
    if ($now.commit -cne $identity.commit -or $now.fingerprint -cne $identity.fingerprint -or
        (Get-WorkloadEvidenceInput $repositoryRoot $now).fingerprint -cne $inputIdentity.fingerprint -or
        (-not $toolOnly -and -not (Test-WorkloadBuildMatch $repositoryRoot $record $now $inputIdentity))) { throw 'Inputs or detection outputs changed during validation.' }
    if($null -ne $admission){$current=Read-WorkloadRetainedAssets $repositoryRoot $ApprovedRetainedAssets;if(($current|ConvertTo-Json -Depth 8 -Compress) -cne ($admission|ConvertTo-Json -Depth 8 -Compress)){throw 'Retained workspace declaration changed.'}}
    if ($null -ne $applicability -and ((Get-FileHash -LiteralPath $applicability.path).Hash -cne $applicability.sha256 -or
        (Get-FileHash -LiteralPath $applicability.record.legacyCachePath).Hash -cne $applicability.record.legacyCacheSha256)) {throw 'Fixed applicability/original cache changed during validation.'}
    foreach ($live in $liveChecks.Values) {
        if (-not (Test-WorkloadLiveArtifacts $live.outputs $live.exact $live.absent)) { throw 'Execution artifacts or runtime configuration changed during validation.' }
    }
}
function Ensure-Fixture([string] $Project) {
    if (-not $Project -or $built.ContainsKey($Project)) { return }
    $before=Get-WorkloadFixtureInputFingerprint $repositoryRoot $Project $inputIdentity
    $null=Ensure-WorkloadFixture $repositoryRoot $Project $inputIdentity
    $after=Get-WorkloadFixtureInputFingerprint $repositoryRoot $Project $inputIdentity
    if ($after -cne $before) {throw ('Fixture inputs changed during compilation: '+$Project)}
    $built[$Project] = $after
}
Write-Host ('Selection: mode=' + $Mode + '; final/feedback baseline=' + $changes.baseline + '; changed paths=' + $changes.paths.Count + '; groups=' + ($route.groups -join ', '))
Write-Host ('Required checks: ' + ($required -join ', '))
# Cancellation may bypass catch. Retire aggregate delivery before any child,
# while retaining every original execution receipt and historical cache row.
$running=[ordered]@{status='RUNNING';batchId=$batch.id;requiredChecks=$required;mode=$Mode}
if ($null -ne $applicability) {$running.applicabilityRecord=$applicability.path;$running.applicabilitySha256=$applicability.sha256}
$record | Add-Member -MemberType NoteProperty -Name workload -Value $running -Force
Write-WorkloadJson $recordPath $record
try {
    Assert-StableInputs
    foreach ($check in $plan) {
        $currentCheck = $check.name
        $signature = Get-WorkloadHash (@($check.executable) + @($check.arguments))
        $old = if ($entries.ContainsKey($check.name)) { $entries[$check.name] } else { $null }
        if (-not $Rerun) {
            $restored=Restore-WorkloadCheckpointEvidence $repositoryRoot $identity $inputIdentity $check.name $signature $record.commit
            if ($null -ne $restored) {$old=$restored;$entries[$check.name]=$restored}
        }
        $reuse = -not $Rerun -and (Test-WorkloadReusable $repositoryRoot $old $inputIdentity $check.name $signature)
        if ($reuse) {
            $absent=@($old.outputs | ForEach-Object {Split-Path -Parent $_.livePath} | Sort-Object -Unique | Where-Object {-not [IO.Directory]::Exists($_)})
            $liveChecks[$check.name]=@{outputs=$old.outputs;absent=$absent;exact=($old.detectionCommit -ceq $record.commit -and $old.allInputFingerprint -ceq $inputIdentity.fingerprint)}
            Write-Host ('REUSED ' + $check.name + ' from ' + $old.sourceCommit + '/' + $old.executionId + ' original-ms=' + $old.milliseconds)
            $results.Add([ordered]@{name=$check.name; result='PASS'; disposition='REUSED'; milliseconds=0; originalMilliseconds=$old.milliseconds; executionId=$old.executionId; sourceCommit=$old.sourceCommit})
            continue
        }
        if (-not $Rerun -and $null -ne $applicability) {
            $decision=@($applicability.record.decisions | Where-Object {$_.name -ceq $check.name})
            if ($decision.Count -eq 1 -and $decision[0].action -ceq 'QUALIFIED') {
                $qualified=Get-WorkloadQualifiedOriginal $repositoryRoot $applicability $check $inputIdentity
                if ($null -eq $qualified) {throw ('Approved qualification no longer valid; review this finite obligation: '+$check.name)}
                $entries[$check.name]=$qualified
                $absent=@($qualified.outputs | ForEach-Object {Split-Path -Parent $_.livePath} | Sort-Object -Unique | Where-Object {-not [IO.Directory]::Exists($_)})
                $liveChecks[$check.name]=@{outputs=$qualified.outputs;absent=$absent;exact=$false}
                $results.Add([ordered]@{name=$check.name;result='AWAITING_STABILITY';disposition='QUALIFIED';milliseconds=0;originalMilliseconds=$qualified.milliseconds;executionId=$qualified.executionId;sourceCommit=$qualified.sourceCommit;qualificationId=$applicability.record.qualificationId;requiresCurrent=@($decision[0].requiresCurrent)})
                Write-Host ('QUALIFICATION_PENDING '+$check.name+' original='+$qualified.sourceCommit+'/'+$qualified.executionId)
                continue
            }
        }
        if ($null -ne $old) { Write-Host ('INVALIDATED ' + $check.name + ': result/signature/artifact mismatch or explicit rerun.') }
        # Retire an old success before starting; failure/cancellation cannot fall
        # back to it on the next invocation. Other valid successes stay usable.
        if($ClosureOnly){throw ('Closure refuses execution fallback; missing current evidence: '+$check.name)}
        $attempt=Start-WorkloadCheckpoint $repositoryRoot $batch $check $signature
        $entries.Remove($check.name)
        Stop-WorkloadReadWindow
        Ensure-Fixture $check.project
        Start-WorkloadReadWindow
        $attempt | Add-Member -MemberType NoteProperty -Name fixtureInputFingerprint -Value $(if ($check.project) {$built[$check.project]} else {'prebuilt detection outputs locked by build record'})
        # Capture the actual inputs to the process before launch, then require
        # the same bytes/configuration/set after it and at the whole-run exit.
        $outputs = @(Get-WorkloadEvidenceOutputs $repositoryRoot $record $check.executable $check.arguments)
        $liveOutputs=@($outputs | ForEach-Object {[pscustomobject]@{path=$_.path;livePath=$_.path;length=$_.length;sha256=$_.sha256}})
        $liveChecks[$check.name]=@{outputs=$liveOutputs;absent=@();exact=$true}
        $attempt.beforeOutputs=$liveOutputs
        $clock = [Diagnostics.Stopwatch]::StartNew()
        Invoke-WorkloadProcess $check.name $check.executable $check.arguments -Checkpoint $attempt
        if (-not (Test-WorkloadLiveArtifacts $liveOutputs $true)) {throw ('Execution artifacts changed during check: '+$check.name)}
        $outputs = @(Save-WorkloadArtifacts $repositoryRoot $outputs)
        $attempt.outputs=$outputs;$attempt.milliseconds=$clock.ElapsedMilliseconds
        Write-WorkloadJson $attempt.path $attempt
        $completedReceipts.Add($attempt)
        $entry = [ordered]@{schemaVersion=2; name=$check.name; status='PASS'; inputFingerprint=(Get-WorkloadCheckFingerprint $inputIdentity $check.name); signature=$signature;
            allInputFingerprint=$inputIdentity.fingerprint; inputs=$inputIdentity.inputs;
            executionId=$attempt.executionId; executedUtc=$attempt.endedUtc; startedUtc=$attempt.startedUtc; checkpointPath=$attempt.path; sourceCommit=$identity.commit; sourceFingerprint=$identity.fingerprint;
            detectionCommit=$record.commit; outputs=$outputs; milliseconds=$clock.ElapsedMilliseconds}
        $pending[$check.name] = $entry
        $results.Add([ordered]@{name=$check.name; result='AWAITING_STABILITY'; disposition='EXECUTED'; milliseconds=$entry.milliseconds; originalMilliseconds=$entry.milliseconds; executionId=$entry.executionId; sourceCommit=$identity.commit})
        Write-Host ('EXECUTED ' + $check.name + ' ms=' + $entry.milliseconds)
    }
    Stop-WorkloadReadWindow
    Start-WorkloadReadWindow
    Assert-StableInputs
    Complete-WorkloadCheckpointQualification $repositoryRoot @($completedReceipts.ToArray()) $batch
    foreach ($item in $results) {if ($item.disposition -ceq 'EXECUTED') {$item.result='PASS'}}
    foreach ($item in $results) {
        if ($item.disposition -ceq 'QUALIFIED') {
            foreach ($dependency in $item.requiresCurrent) {
                $proof=@($results | Where-Object {$_.name -ceq $dependency -and $_.result -ceq 'PASS' -and $_.disposition -in @('EXECUTED','REUSED')})
                if ($proof.Count -ne 1) {throw ('Current qualification proof not completed: '+$dependency)}
            }
            $item.result='PASS'
        }
    }
    foreach ($name in $pending.Keys) { $entries[$name]=$pending[$name] }
    foreach ($item in $results) {
        if (-not (Test-WorkloadEvidenceOutputs $repositoryRoot $entries[$item.name].outputs)) { throw ('Check artifacts changed: ' + $item.name) }
    }
    Save-Evidence
    Remove-UnusedWorkloadArtifacts $repositoryRoot @($entries.Values)
    $result = [ordered]@{ status=$(if ($Mode -ceq 'Feedback') {'FEEDBACK'} else {'PASS'}); mode=$Mode; commit=$identity.commit; sourceFingerprint=$identity.fingerprint;
        inputFingerprint=$inputIdentity.fingerprint; baseline=$changes.baseline; requestedBaseline=$Baseline; changedPaths=$changes.paths; groups=$route.groups;
        runtime='.NET Framework 4.7.2 target / x86'; requiredChecks=$required; checkCount=$results.Count; results=@($results.ToArray());
        notSelected=@(@($knownNames)+@($entries.Keys) | Sort-Object -Unique | Where-Object {$required -notcontains $_});
        evidenceFile='artifacts/build/workload-evidence.json'; evidenceSha256=(Get-FileHash -LiteralPath $cachePath -Algorithm SHA256).Hash;
        slowGraphics='separate-risk-triggered-entry' }
    if ($null -ne $applicability) {$result.applicabilityRecord=$applicability.path;$result.applicabilitySha256=$applicability.sha256;$result.qualificationId=$applicability.record.qualificationId}
    if ($Mode -cne 'Feedback' -and -not (Test-WorkloadCoverage ($result | ConvertTo-Json -Depth 10 | ConvertFrom-Json) $required $inputIdentity.fingerprint)) { throw 'Incomplete final coverage.' }
    $record.workload=$result; Write-WorkloadJson $recordPath $record
    Write-Host ('Coverage: executed=' + @($results | Where-Object {$_.disposition -ceq 'EXECUTED'}).Count + '; reused=' + @($results | Where-Object {$_.disposition -ceq 'REUSED'}).Count + '; qualified=' + @($results | Where-Object {$_.disposition -ceq 'QUALIFIED'}).Count + '; not-selected=' + $result.notSelected.Count + '; status=' + $result.status)
    Write-Output $result
} catch {
    # Successful siblings are useful only if the entire observed input set
    # stayed fixed. An input race discards this invocation's pending evidence.
    try { Assert-StableInputs; Complete-WorkloadCheckpointQualification $repositoryRoot @($completedReceipts.ToArray()) $batch; foreach ($name in $pending.Keys) {$entries[$name]=$pending[$name]}; foreach ($item in $results) {if ($item.disposition -ceq 'EXECUTED') {$item.result='PASS'}}; Save-Evidence } catch { }
    $failure=[ordered]@{status='FAILED'; mode=$Mode; commit=$identity.commit; sourceFingerprint=$identity.fingerprint; failedCheck=$currentCheck; reason=$_.Exception.Message; completedResults=@($results.ToArray()); remaining='not covered after failure'}
    if ($null -ne $applicability) {$failure.applicabilityRecord=$applicability.path;$failure.applicabilitySha256=$applicability.sha256}
    $record.workload=$failure; Write-WorkloadJson $recordPath $record
    $_.Exception.Data['workload']=$failure
    throw
} finally {Stop-WorkloadReadWindow}
