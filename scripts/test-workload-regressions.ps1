[CmdletBinding()]
param(
    [string] $Baseline,
    [ValidateSet('Related','Full','Feedback')][string] $Mode = 'Related',
    [switch] $Rerun
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'workload/Workload.Support.ps1')
& (Join-Path $PSScriptRoot 'prepare-terraria-references.ps1') -VerifyOnly | Out-Host
& (Join-Path $PSScriptRoot 'prepare-harmony.ps1') -VerifyOnly | Out-Host
if ((& dotnet.exe --version).Trim() -cne '10.0.203' -or $LASTEXITCODE -ne 0) { throw 'Locked SDK unavailable.' }
$identity = Get-WorkloadIdentity $repositoryRoot
$inputIdentity = Get-WorkloadEvidenceInput $repositoryRoot $identity
$recordPath = Join-Path $repositoryRoot 'artifacts/build/Debug/build-record.json'
$record = Read-WorkloadJson $recordPath
if (-not (Test-WorkloadBuildMatch $repositoryRoot $record $identity)) { throw 'Debug detection binaries do not match current executable inputs/SDK/output hashes.' }
# A supplied baseline is useful for feedback, but cannot shrink final obligations.
$changes = Get-WorkloadChanges $repositoryRoot $(if ($Mode -ceq 'Feedback') { $Baseline } else { '' })
if ($Mode -ceq 'Feedback' -and [string]::IsNullOrWhiteSpace($Baseline)) { throw 'Feedback requires an explicit comparison baseline; it cannot authorize delivery.' }
if ($Mode -cne 'Feedback' -and $Baseline) {
    $extra = Get-WorkloadChanges $repositoryRoot $Baseline
    if ($extra.reason) { throw $extra.reason }
    $changes.paths = @($changes.paths + $extra.paths | Sort-Object -Unique)
}
$route = Get-WorkloadRoute $changes.paths
if ($changes.reason -or $route.unknown.Count -gt 0) { throw ($changes.reason + ' Unclassified paths: ' + ($route.unknown -join ', ')) }
if ($Mode -ceq 'Full') { $route = Get-WorkloadRoute @('scripts/build.ps1') }
$checksRoot = Join-Path $repositoryRoot 'artifacts/build/Debug/checks'
$architecture = Join-Path $repositoryRoot 'artifacts/build/Debug/work/bin/JueMingR.ArchitectureTests/x86/Debug/net472/JueMingR.ArchitectureTests.exe'
$catalog = @(& $architecture --list-checks)
if ($LASTEXITCODE -ne 0 -or $catalog.Count -eq 0) { throw 'The business check catalogue is unavailable.' }
$plan = @(Get-WorkloadPlan $repositoryRoot $checksRoot $architecture $catalog $route.groups)
$required = @($plan | ForEach-Object { $_.name })
$knownNames = @((Get-WorkloadPlan $repositoryRoot $checksRoot $architecture $catalog (Get-WorkloadRoute @('scripts/build.ps1')).groups) | ForEach-Object {$_.name})
if (@($required | Sort-Object -Unique).Count -ne $required.Count) { throw 'Duplicate check identity in plan.' }
$cachePath = Join-Path $repositoryRoot 'artifacts/build/workload-evidence.json'
$cache = Read-WorkloadJson $cachePath
$entries = @{}
if ($null -ne $cache -and $null -ne $cache.PSObject.Properties['schemaVersion'] -and $cache.schemaVersion -eq 2) {
    foreach ($entry in $cache.results) { if ($knownNames -contains $entry.name) {$entries[$entry.name] = $entry} }
} elseif ($null -ne $cache) { Write-Host 'INVALIDATED: executable input/dependency/environment set changed or old evidence schema.' }
$results = New-Object 'System.Collections.Generic.List[object]'
$pending = @{}
$liveChecks = @{}
$built = @{}
$currentCheck = 'planning'
function Save-Evidence {
    Write-WorkloadJson $cachePath ([ordered]@{ schemaVersion=2; inputFingerprint=$inputIdentity.fingerprint; results=@($entries.Values | Sort-Object name) })
}
function Assert-StableInputs {
    $now = Get-WorkloadIdentity $repositoryRoot
    if ($now.commit -cne $identity.commit -or $now.fingerprint -cne $identity.fingerprint -or
        (Get-WorkloadEvidenceInput $repositoryRoot $now).fingerprint -cne $inputIdentity.fingerprint -or
        -not (Test-WorkloadBuildMatch $repositoryRoot $record $now)) { throw 'Inputs or detection outputs changed during validation.' }
    foreach ($live in $liveChecks.Values) {
        if (-not (Test-WorkloadLiveArtifacts $live.outputs $live.exact $live.absent)) { throw 'Execution artifacts or runtime configuration changed during validation.' }
    }
}
function Ensure-Fixture([string] $Project) {
    if (-not $Project -or $built.ContainsKey($Project)) { return }
    & dotnet.exe build (Join-Path $repositoryRoot ('tests/' + $Project + '/' + $Project + '.csproj')) --configuration Debug --nologo -p:Platform=x86 "-p:JueMingRBuildRoot=$checksRoot" | Out-Host
    if ($LASTEXITCODE -ne 0) { throw ('Workload fixture build failed: ' + $Project) }
    $built[$Project] = $true
}
Write-Host ('Selection: mode=' + $Mode + '; final/feedback baseline=' + $changes.baseline + '; changed paths=' + $changes.paths.Count + '; groups=' + ($route.groups -join ', '))
Write-Host ('Required checks: ' + ($required -join ', '))
try {
    foreach ($check in $plan) {
        $currentCheck = $check.name
        $signature = Get-WorkloadHash (@($check.executable) + @($check.arguments))
        $old = if ($entries.ContainsKey($check.name)) { $entries[$check.name] } else { $null }
        $reuse = -not $Rerun -and (Test-WorkloadReusable $repositoryRoot $old $inputIdentity $check.name $signature)
        if ($reuse) {
            $absent=@($old.outputs | ForEach-Object {Split-Path -Parent $_.livePath} | Sort-Object -Unique | Where-Object {-not [IO.Directory]::Exists($_)})
            $liveChecks[$check.name]=@{outputs=$old.outputs;absent=$absent;exact=($old.detectionCommit -ceq $record.commit -and $old.allInputFingerprint -ceq $inputIdentity.fingerprint)}
            Write-Host ('REUSED ' + $check.name + ' from ' + $old.sourceCommit + '/' + $old.executionId + ' original-ms=' + $old.milliseconds)
            $results.Add([ordered]@{name=$check.name; result='PASS'; disposition='REUSED'; milliseconds=0; originalMilliseconds=$old.milliseconds; executionId=$old.executionId; sourceCommit=$old.sourceCommit})
            continue
        }
        if ($null -ne $old) { Write-Host ('INVALIDATED ' + $check.name + ': result/signature/artifact mismatch or explicit rerun.') }
        # Retire an old success before starting; failure/cancellation cannot fall
        # back to it on the next invocation. Other valid successes stay usable.
        $entries.Remove($check.name); Save-Evidence
        Ensure-Fixture $check.project
        # Capture the actual inputs to the process before launch, then require
        # the same bytes/configuration/set after it and at the whole-run exit.
        $outputs = @(Get-WorkloadEvidenceOutputs $repositoryRoot $record $check.executable)
        $liveOutputs=@($outputs | ForEach-Object {[pscustomobject]@{path=$_.path;livePath=$_.path;length=$_.length;sha256=$_.sha256}})
        $liveChecks[$check.name]=@{outputs=$liveOutputs;absent=@();exact=$true}
        $clock = [Diagnostics.Stopwatch]::StartNew()
        Invoke-WorkloadProcess $check.name $check.executable $check.arguments
        if (-not (Test-WorkloadLiveArtifacts $liveOutputs $true)) {throw ('Execution artifacts changed during check: '+$check.name)}
        $outputs = @(Save-WorkloadArtifacts $repositoryRoot $outputs)
        $entry = [ordered]@{schemaVersion=2; name=$check.name; status='PASS'; inputFingerprint=(Get-WorkloadCheckFingerprint $inputIdentity $check.name); signature=$signature;
            allInputFingerprint=$inputIdentity.fingerprint; inputs=$inputIdentity.inputs;
            executionId=[Guid]::NewGuid().ToString('N'); executedUtc=[DateTime]::UtcNow.ToString('o'); sourceCommit=$identity.commit; sourceFingerprint=$identity.fingerprint;
            detectionCommit=$record.commit; outputs=$outputs; milliseconds=$clock.ElapsedMilliseconds}
        $pending[$check.name] = $entry
        $results.Add([ordered]@{name=$check.name; result='PASS'; disposition='EXECUTED'; milliseconds=$entry.milliseconds; originalMilliseconds=$entry.milliseconds; executionId=$entry.executionId; sourceCommit=$identity.commit})
        Write-Host ('EXECUTED ' + $check.name + ' ms=' + $entry.milliseconds)
    }
    Assert-StableInputs
    foreach ($name in $pending.Keys) { $entries[$name]=$pending[$name] }
    foreach ($item in $results) {
        if (-not (Test-WorkloadEvidenceOutputs $repositoryRoot $entries[$item.name].outputs)) { throw ('Check artifacts changed: ' + $item.name) }
    }
    Save-Evidence
    Remove-UnusedWorkloadArtifacts $repositoryRoot @($entries.Values)
    $result = [ordered]@{ status=$(if ($Mode -ceq 'Feedback') {'FEEDBACK'} else {'PASS'}); mode=$Mode; commit=$identity.commit; sourceFingerprint=$identity.fingerprint;
        inputFingerprint=$inputIdentity.fingerprint; baseline=$changes.baseline; requestedBaseline=$Baseline; changedPaths=$changes.paths; groups=$route.groups;
        runtime='.NET Framework 4.7.2 target / x86'; requiredChecks=$required; checkCount=$results.Count; results=@($results.ToArray());
        notSelected=@((Get-WorkloadPlan $repositoryRoot $checksRoot $architecture $catalog (Get-WorkloadRoute @('scripts/build.ps1')).groups) | Where-Object { $required -notcontains $_.name } | ForEach-Object {$_.name});
        evidenceFile='artifacts/build/workload-evidence.json'; evidenceSha256=(Get-FileHash -LiteralPath $cachePath -Algorithm SHA256).Hash;
        slowGraphics='separate-risk-triggered-entry' }
    if ($Mode -cne 'Feedback' -and -not (Test-WorkloadCoverage ($result | ConvertTo-Json -Depth 10 | ConvertFrom-Json) $required $inputIdentity.fingerprint)) { throw 'Incomplete final coverage.' }
    $record.workload=$result; Write-WorkloadJson $recordPath $record
    Write-Host ('Coverage: executed=' + @($results | Where-Object {$_.disposition -ceq 'EXECUTED'}).Count + '; reused=' + @($results | Where-Object {$_.disposition -ceq 'REUSED'}).Count + '; not-selected=' + $result.notSelected.Count + '; status=' + $result.status)
    Write-Output $result
} catch {
    # Successful siblings are useful only if the entire observed input set
    # stayed fixed. An input race discards this invocation's pending evidence.
    try { Assert-StableInputs; foreach ($name in $pending.Keys) {$entries[$name]=$pending[$name]}; Save-Evidence } catch { }
    $failure=[ordered]@{status='FAILED'; mode=$Mode; commit=$identity.commit; sourceFingerprint=$identity.fingerprint; failedCheck=$currentCheck; reason=$_.Exception.Message; completedResults=@($results.ToArray()); remaining='not covered after failure'}
    $record.workload=$failure; Write-WorkloadJson $recordPath $record
    $_.Exception.Data['workload']=$failure
    throw
}
