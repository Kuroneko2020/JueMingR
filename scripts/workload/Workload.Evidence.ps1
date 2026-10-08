# Local evidence only: a bounded record for the current executable input set.
# No timestamps, Git labels or an old PASS alone establish applicability.
function Get-WorkloadHash {
    param([string[]] $Rows)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($Rows -join "`n")))).Replace('-', '') }
    finally { $sha.Dispose() }
}
function Get-WorkloadEvidenceInput {
    param([string] $Root, $Identity)
    # Keep exact source bytes, assertions, fixtures, recipes and build options.
    # Documentation is not compiled; package assets remain in the build identity.
    # This deliberately invalidates more than a semantic dependency analyser.
    $rows = @($Identity.inputs | Where-Object { $_ -notmatch '^(docs/|scripts/phase0s/[^:]*Owner-Test-Card[^:]*\.md:|AGENTS\.md:|README(?:\.[^/:]+)?:|LICENSE:|THIRD-PARTY-NOTICES\.md:)' })
    foreach ($directory in @('external/TerrariaRefs','external/Harmony')) {
        $location = Join-Path $Root $directory
        if (-not [IO.Directory]::Exists($location)) { throw ('Missing evidence dependency: ' + $directory) }
        $files = @(Get-ChildItem -LiteralPath $location -File | Sort-Object Name)
        if ($files.Count -eq 0) { throw ('Empty evidence dependency: ' + $directory) }
        foreach ($file in $files) { $rows += $directory + '/' + $file.Name + ':' + (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash }
    }
    $framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319'
    foreach ($name in @('clr.dll','mscorlib.dll')) {
        $rows += 'framework-x86/' + $name + ':' + (Get-FileHash -LiteralPath (Join-Path $framework $name) -Algorithm SHA256).Hash
    }
    $rows += 'sdk:10.0.203;target:net472;x86;detection:Debug'
    $rows += 'os:' + [Environment]::OSVersion.VersionString + ';powershell:' + $PSVersionTable.PSVersion.ToString()
    # Hash only relevant build overrides; do not log unrelated private variables.
    foreach ($item in @(Get-ChildItem Env: | Where-Object { $_.Name -match '^(DOTNET_|MSBUILD|COMPlus_|JueMingR|Configuration$|Platform$|DefineConstants$|TargetFramework$)' } | Sort-Object Name)) {
        $rows += 'environment:' + $item.Name + ':' + (Get-WorkloadHash @($item.Value))
    }
    return [ordered]@{ fingerprint = Get-WorkloadHash $rows; inputs = $rows }
}
function Test-WorkloadEvidence {
    param($Evidence, [string] $Fingerprint, [string] $Name, [string] $Signature)
    if ($null -eq $Evidence) { return $false }
    foreach ($key in @('schemaVersion','status','inputFingerprint','name','signature','executionId','sourceCommit','sourceFingerprint','milliseconds','outputs')) {
        if ($null -eq $Evidence.PSObject.Properties[$key]) { return $false }
    }
    return $Evidence.schemaVersion -eq 2 -and $Evidence.status -ceq 'PASS' -and
        $Evidence.inputFingerprint -ceq $Fingerprint -and $Evidence.name -ceq $Name -and
        $Evidence.signature -ceq $Signature -and $Evidence.executionId -match '^[0-9a-f]{32}$' -and
        $Evidence.sourceCommit -match '^[0-9a-f]{40}$' -and $Evidence.sourceFingerprint -match '^[0-9A-F]{64}$' -and @($Evidence.outputs).Count -gt 0
}
function Get-WorkloadCheckFingerprint {
    param($InputIdentity, [string] $Name)
    # Only reviewed leaf assertion files may be omitted from another CPU check.
    # NativeChecks/CheckCatalog, shared helpers, projects, all production and all
    # recipes remain common inputs. New consumers must update this map in the
    # same change (the dispatcher change itself invalidates all old evidence).
    # Page composition is also used by ToolsVisual, outside this CPU cache.
    $leaves = @{
        'scripts/verify-existing-package.ps1'=@('workload-PackageVerification')
        'scripts/phase0s/PackageVerification.Support.ps1'=@('workload-PackageVerification')
        'tests/Phase0S/Invoke-PackageVerificationChecks.ps1'=@('workload-PackageVerification')
        'tests/NativeWorldTextProbe/NativePageCompositionChecks.cs'=@('native-PageCompositionCpu')
        'tests/NativeWorldTextProbe/NativeBackgroundAutomationChecks.cs'=@('native-BackgroundCpu','native-F5AutomationCpu')
        'tests/NativeWorldTextProbe/NativeToolCadenceChecks.cs'=@('native-ToolsCadence')
        'tests/NativeWorldTextProbe/NativeToolsWorkloadChecks.cs'=@('native-ToolsWorkload')
        'tests/NativeWorldTextProbe/NativeYoyoAdoptionChecks.cs'=@('native-CombatCpu','native-CombatYoyoCausal')
        'tests/NativeWorldTextProbe/NativeCombatFoundationChecks.cs'=@('native-NpcFoundationRules')
        'tests/NativeWorldTextProbe/NativeCombatFoundationContinuousChecks.cs'=@('native-NpcFoundationContinuous','native-NpcStrategyContinuous')
        'tests/NativeWorldTextProbe/NativeCombatStrategyChecks.cs'=@('native-NpcStrategy','native-NpcStrategy-rollchoice')
        'tests/NativeWorldTextProbe/NativeCombatFamilyControlChecks.cs'=@('native-NpcStrategy')
        'tests/NativeWorldTextProbe/NativeCombatFiniteControlChecks.cs'=@('native-NpcStrategy')
        'tests/NativeWorldTextProbe/NativeCombatRetargetChecks.cs'=@('native-NpcStrategy')
        'tests/NativeWorldTextProbe/NativeCombatRollingControlChecks.cs'=@('native-NpcStrategy','native-NpcStrategy-rollchoice')
        'tests/NativeWorldTextProbe/NativeNpcLifetimeChecks.cs'=@('native-NpcStrategy','native-NpcStrategy-rollchoice')
        'tests/NativeWorldTextProbe/NativeOuterInputBoundaryChecks.cs'=@('native-InputBoundary')
        'tests/NativeWorldTextProbe/NativeCombatRunningControlChecks.cs'=@('native-NpcStrategy')
        'tests/NativeWorldTextProbe/NativeCombatStructuralControlChecks.cs'=@('native-NpcStrategy')
        'tests/NativeWorldTextProbe/NativeCombatFighterControlChecks.cs'=@('native-NpcStrategy')
        'tests/NativeWorldTextProbe/NativeCombatPositionControlChecks.cs'=@('native-NpcStrategy')
        'tests/NativeWorldTextProbe/NativeCombatFlyingTailChecks.cs'=@('native-NpcStrategy')
        'tests/NativeWorldTextProbe/NativeCombatEventRetirementChecks.cs'=@('native-NpcEventRetirementCpu','native-NpcEventRetirement')
        'tests/NativeWorldTextProbe/NativeCombatSyncChecks.cs'=@('native-NpcSync')
        'tests/NativeWorldTextProbe/NativeDisplayResponsibilityChecks.cs'=@('native-NpcDisplayIsolation')
        'tests/NativeWorldTextProbe/NativeCombatCloseoutTerrainChecks.cs'=@('native-NpcCloseoutTerrain')
        'tests/Workload/Invoke-WorkloadRoutingChecks.ps1'=@('workload-Routing')
        'tests/Workload/Invoke-WorkloadEvidenceChecks.ps1'=@('workload-Evidence')
    }
    $rows = @($InputIdentity.inputs | Where-Object {
        $path = ($_ -split ':',2)[0]
        -not $leaves.ContainsKey($path) -or $leaves[$path] -contains $Name
    })
    return Get-WorkloadHash $rows
}
function Write-WorkloadJson {
    param([string] $Path, $Value)
    [IO.Directory]::CreateDirectory((Split-Path -Parent $Path)) | Out-Null
    # A cancelled writer must not leave a parseable partial PASS.
    $temporary = $Path + '.pending'
    [IO.File]::WriteAllText($temporary, ($Value | ConvertTo-Json -Depth 12), (New-Object Text.UTF8Encoding($false)))
    Move-Item -LiteralPath $temporary -Destination $Path -Force
}
function Read-WorkloadJson {
    param([string] $Path)
    if (-not [IO.File]::Exists($Path)) { return $null }
    try { return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json }
    catch { return $null }
}
function Get-WorkloadClearedEnvironment {
    # These are fixture selectors, never inherited proof of a full scope.
    $known='^JUEMINGR_(STRATEGY_ONLY|FOUNDATION_(ONLY|STRATEGY_ONLY|FIGHTER_ONLY|GRAVITY_ONLY|NPC_LIQUID_ONLY|PLAYER_LIQUID_ONLY|SINGLE)|SHARED_GEOMETRY_PHASE|ROLLING_CPU_ONLY|ROLLING_PHASES|AIM_LIGHT_SEMANTICS_ONLY)$'
    $selectors=@(Get-ChildItem Env: | Where-Object {$_.Name -match '^JUEMINGR_.*(ONLY|SCOPE|PHASES?|SINGLE)$'})
    foreach ($item in $selectors) { if ($item.Name -notmatch $known) {throw ('Unknown inherited workload selector: '+$item.Name)} }
    if ($env:JMR_INPUT_DIAGNOSTIC -or $env:JMR_INPUT_DIAGNOSTIC_WINDOW) {throw 'Diagnostic process switches are not ordinary workload inputs.'}
    return $selectors
}
function Get-WorkloadFixtureInputFingerprint {
    param([string] $Root, [string] $Project, $InputIdentity)
    # The two existing SDK fixtures use default local .cs plus literal linked
    # files / single-directory globs. Unknown imports/property-based includes
    # require review, rather than pretending this is an MSBuild dependency graph.
    $relative='tests/'+$Project+'/'+$Project+'.csproj'
    $projectPath=Join-Path $Root $relative;$directory=Split-Path -Parent $projectPath
    [xml]$xml=Get-Content -LiteralPath $projectPath -Raw -Encoding UTF8
    if ($xml.SelectNodes('//Import|//ProjectReference').Count -gt 0) {throw 'Unreviewed fixture input propagation.'}
    $files=@($projectPath)
    $patterns=@(('^tests/'+[regex]::Escape($Project)+'/.*\.cs$'),('^'+[regex]::Escape($relative)+'$'))
    $files+=@(Get-ChildItem -LiteralPath $directory -Recurse -File -Filter '*.cs' | Where-Object {$_.FullName -notmatch '[\\/](bin|obj)[\\/]'} | ForEach-Object {$_.FullName})
    foreach ($node in $xml.SelectNodes('//Compile[@Include]|//EmbeddedResource[@Include]')) {
        $include=[string]$node.Include
        if ($include.Contains('$') -or $include.Contains('**')) {throw 'Unreviewed fixture input expression.'}
        $parent=Split-Path -Parent $include
        if ($parent.Contains('*') -or $include.Contains('?')) {throw 'Unreviewed fixture input glob.'}
        $resolvedParent=if ($parent) {[IO.Path]::GetFullPath((Join-Path $directory $parent))} else {$directory}
        $full=Join-Path $resolvedParent (Split-Path -Leaf $include)
        $base=[IO.Path]::GetFullPath($Root).TrimEnd('\')+'\'
        if (-not $full.StartsWith($base,[StringComparison]::OrdinalIgnoreCase)) {throw 'Fixture input escapes repository.'}
        $linked=$full.Substring($base.Length).Replace('\','/')
        $patterns+='^'+[regex]::Escape($linked).Replace('\*','[^/]*')+'$'
        $files+=@(Get-ChildItem -Path $full -File -ErrorAction Stop | ForEach-Object {$_.FullName})
    }
    foreach ($name in @('Directory.Build.props','Directory.Build.targets','global.json')) {
        if ([IO.File]::Exists((Join-Path $Root $name))) {$files+=Join-Path $Root $name;$patterns+='^'+[regex]::Escape($name)+'$'}
    }
    $rows=@($files | Sort-Object -Unique | ForEach-Object {([IO.Path]::GetFullPath($_).Substring([IO.Path]::GetFullPath($Root).TrimEnd('\').Length+1).Replace('\','/'))+':'+(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash})
    $expected=@($InputIdentity.inputs | Where-Object {($_ -split ':',2)[0] -match ($patterns -join '|')})
    if ((@($rows | Sort-Object) -join '|') -cne (@($expected | Sort-Object) -join '|')) {throw 'Fixture compile inputs differ from the locked batch.'}
    return Get-WorkloadHash @($rows | Sort-Object)
}
function New-WorkloadCheckpointBatch {
    param([string] $Root, $Identity, $InputIdentity, [string[]] $Required, $Applicability=$null)
    $id=[Guid]::NewGuid().ToString('N')
    $path=Join-Path $Root ('artifacts/build/workload-checkpoints/'+$id+'/batch.json')
    $value=[ordered]@{batchId=$id;startedUtc=[DateTime]::UtcNow.ToString('o');sourceCommit=$Identity.commit;sourceFingerprint=$Identity.fingerprint;inputFingerprint=$InputIdentity.fingerprint;inputs=$InputIdentity.inputs;required=$Required}
    if ($null -ne $Applicability) {$value.applicabilityRecord=$Applicability.path;$value.applicabilitySha256=$Applicability.sha256}
    Write-WorkloadJson $path $value
    return [pscustomobject]@{id=$id;path=$path;sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
}
function Start-WorkloadCheckpoint {
    param([string] $Root, $Batch, $Check, [string] $Signature)
    if ($Check.name -notmatch '^[A-Za-z0-9_-]+$') {throw 'Invalid checkpoint check name.'}
    $cleared=@(Get-WorkloadClearedEnvironment)
    $environment=@(Get-ChildItem Env: | Where-Object {$_.Name -match '^(DOTNET_|MSBUILD|COMPlus_|JUEMINGR|Configuration$|Platform$|DefineConstants$|TargetFramework$)' -and $_.Name -notmatch '^JUEMINGR_.*(ONLY|SCOPE|PHASES?|SINGLE)$'} | Sort-Object Name | ForEach-Object {$_.Name+':'+(Get-WorkloadHash @($_.Value))})
    $id=[Guid]::NewGuid().ToString('N')
    $path=Join-Path (Split-Path -Parent $Batch.path) ($id+'.json')
    $attempt=[pscustomobject][ordered]@{executionId=$id;name=$Check.name;status='PREPARING';signature=$Signature;executable=$Check.executable;arguments=@($Check.arguments);effectiveEnvironment=$environment;clearedSelectors=@($cleared | ForEach-Object {$_.Name} | Sort-Object);batchPath=$Batch.path;batchSha256=$Batch.sha256;path=$path;startedUtc=$null;endedUtc=$null;exitCode=$null;outputPath=($path+'.log');outputSha256=$null;beforeOutputs=@();outputs=@();milliseconds=0}
    Write-WorkloadJson $path $attempt
    # A tiny per-name retirement marker, not a rewrite of all historical cache.
    # Publish before compilation/launch so cancellation cannot revive old PASS.
    Write-WorkloadJson (Join-Path $Root ('artifacts/build/workload-checkpoints/latest-'+$Check.name+'.json')) ([ordered]@{executionId=$id;path=$path})
    return $attempt
}
function Complete-WorkloadCheckpointQualification {
    param([string] $Root, $Completed, $Batch)
    # Caller must first perform the real batch source/build/live stability check.
    # The completion receipt remains AWAITING; qualification is separate evidence
    # of that later check, never an invented execution or an aggregate PASS.
    if ((Get-FileHash -LiteralPath $Batch.path -Algorithm SHA256).Hash -cne $Batch.sha256) {throw 'Checkpoint batch changed.'}
    foreach ($item in $Completed) {
        $receipt=Read-WorkloadJson $item.path
        if ($null -eq $receipt -or $receipt.status -cne 'EXECUTED_AWAITING_STABILITY' -or $receipt.exitCode -ne 0 -or -not (Test-WorkloadEvidenceOutputs $Root $receipt.outputs) -or
            -not [IO.File]::Exists($receipt.outputPath) -or (Get-FileHash -LiteralPath $receipt.outputPath -Algorithm SHA256).Hash -cne $receipt.outputSha256) {throw 'Incomplete checkpoint cannot qualify.'}
        Write-WorkloadJson ($item.path+'.qualification.json') ([ordered]@{status='STABLE';executionId=$item.executionId;receiptSha256=(Get-FileHash -LiteralPath $item.path -Algorithm SHA256).Hash;batchSha256=$Batch.sha256;qualifiedUtc=[DateTime]::UtcNow.ToString('o')})
    }
}
function Test-WorkloadCheckpointQualified {
    param([string] $Root, $Receipt)
    try {
    if ($null -eq $Receipt -or $null -eq $Receipt.PSObject.Properties['path']) {return $false}
    $latest=Read-WorkloadJson (Join-Path $Root ('artifacts/build/workload-checkpoints/latest-'+$Receipt.name+'.json'))
    $qualified=Read-WorkloadJson ($Receipt.path+'.qualification.json')
    if ($null -eq $latest -or $null -eq $qualified -or $latest.executionId -cne $Receipt.executionId -or $latest.path -cne $Receipt.path -or
        $Receipt.status -cne 'EXECUTED_AWAITING_STABILITY' -or $Receipt.exitCode -ne 0 -or -not $Receipt.startedUtc -or -not $Receipt.endedUtc -or
        $qualified.status -cne 'STABLE' -or $qualified.executionId -cne $Receipt.executionId -or $qualified.batchSha256 -cne $Receipt.batchSha256 -or
        -not [IO.File]::Exists($Receipt.path) -or (Get-FileHash -LiteralPath $Receipt.path -Algorithm SHA256).Hash -cne $qualified.receiptSha256 -or
        -not [IO.File]::Exists($Receipt.batchPath) -or (Get-FileHash -LiteralPath $Receipt.batchPath -Algorithm SHA256).Hash -cne $Receipt.batchSha256 -or
        -not [IO.File]::Exists($Receipt.outputPath) -or (Get-FileHash -LiteralPath $Receipt.outputPath -Algorithm SHA256).Hash -cne $Receipt.outputSha256) {return $false}
    return Test-WorkloadEvidenceOutputs $Root $Receipt.outputs
    } catch {return $false}
}
function Restore-WorkloadCheckpointEvidence {
    param([string] $Root, $Identity, $InputIdentity, [string] $Name, [string] $Signature, [string] $DetectionCommit)
    # The ordinary runner calls this only after its current source/build lock
    # check. A previous completed sibling can then gain a new qualification;
    # its original execution ID, command, times and receipt remain unchanged.
    try {
        $latest=Read-WorkloadJson (Join-Path $Root ('artifacts/build/workload-checkpoints/latest-'+$Name+'.json'))
        if ($null -eq $latest) {return $null}
        $receipt=Read-WorkloadJson $latest.path
        if ($null -eq $receipt -or $receipt.status -cne 'EXECUTED_AWAITING_STABILITY' -or $receipt.executionId -cne $latest.executionId -or
            $receipt.name -cne $Name -or $receipt.signature -cne $Signature -or $receipt.exitCode -ne 0 -or -not $receipt.startedUtc -or -not $receipt.endedUtc) {return $null}
        $originalBatch=Read-WorkloadJson $receipt.batchPath
        if ($null -eq $originalBatch -or $originalBatch.sourceCommit -cne $Identity.commit -or $originalBatch.sourceFingerprint -cne $Identity.fingerprint -or
            $originalBatch.inputFingerprint -cne $InputIdentity.fingerprint -or (Get-WorkloadHash @($originalBatch.inputs)) -cne $InputIdentity.fingerprint -or
            -not (Test-WorkloadLiveArtifacts $receipt.outputs $true) -or @($receipt.beforeOutputs).Count -ne @($receipt.outputs).Count) {return $null}
        foreach ($output in $receipt.outputs) {
            $before=@($receipt.beforeOutputs | Where-Object {$_.livePath -ceq $output.livePath -and $_.sha256 -ceq $output.sha256 -and $_.length -eq $output.length})
            if ($before.Count -ne 1) {return $null}
        }
        Complete-WorkloadCheckpointQualification $Root @($receipt) ([pscustomobject]@{path=$receipt.batchPath;sha256=$receipt.batchSha256})
        if (-not (Test-WorkloadCheckpointQualified $Root $receipt)) {return $null}
        return [pscustomobject][ordered]@{schemaVersion=2;name=$Name;status='PASS';inputFingerprint=(Get-WorkloadCheckFingerprint $InputIdentity $Name);signature=$Signature;allInputFingerprint=$InputIdentity.fingerprint;inputs=$originalBatch.inputs;executionId=$receipt.executionId;executedUtc=$receipt.endedUtc;startedUtc=$receipt.startedUtc;checkpointPath=$receipt.path;sourceCommit=$originalBatch.sourceCommit;sourceFingerprint=$originalBatch.sourceFingerprint;detectionCommit=$DetectionCommit;outputs=$receipt.outputs;milliseconds=$receipt.milliseconds}
    } catch {return $null}
}
function Test-WorkloadOutputs {
    param([string] $WorkRoot, $Outputs)
    if (@($Outputs).Count -eq 0) { return $false }
    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($output in $Outputs) {
        $path = [IO.Path]::GetFullPath((Join-Path $WorkRoot $output.path))
        if (-not $path.StartsWith([IO.Path]::GetFullPath($WorkRoot).TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) -or
            -not $seen.Add($path) -or -not [IO.File]::Exists($path) -or
            (Get-Item -LiteralPath $path).Length -ne $output.length -or
            (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $output.sha256) { return $false }
    }
    $actual = @(Get-ChildItem -LiteralPath (Join-Path $WorkRoot 'bin') -Recurse -File)
    return $actual.Count -eq $seen.Count
}
function Test-WorkloadCoverage {
    param($Workload, [string[]] $Required, [string] $Fingerprint)
    if ($null -eq $Workload) { return $false }
    foreach ($key in @('status','mode','inputFingerprint','requiredChecks','results','checkCount')) {
        if ($null -eq $Workload.PSObject.Properties[$key]) { return $false }
    }
    if ($Workload.status -cne 'PASS' -or $Workload.mode -ceq 'Feedback' -or $Workload.inputFingerprint -cne $Fingerprint) { return $false }
    $names = @($Workload.results | ForEach-Object { $_.name })
    if ($names.Count -ne @($names | Sort-Object -Unique).Count -or $Workload.checkCount -ne $names.Count) { return $false }
    if ((@($Required | Sort-Object) -join '|') -cne (@($Workload.requiredChecks | Sort-Object) -join '|')) { return $false }
    foreach ($name in $Required) {
        $found = @($Workload.results | Where-Object { $_.name -ceq $name -and $_.result -ceq 'PASS' -and $_.disposition -in @('EXECUTED','REUSED','QUALIFIED') })
        if ($found.Count -ne 1) { return $false }
        if ($found[0].disposition -ceq 'QUALIFIED' -and ($null -eq $found[0].PSObject.Properties['qualificationId'] -or $found[0].qualificationId -notmatch '^[0-9a-f]{32}$')) {return $false}
    }
    return $names.Count -eq $Required.Count
}
function Get-WorkloadInputDifferences {
    param($OldInputs, $NewInputs)
    $old=@{};$new=@{}
    foreach ($pair in @(@{rows=$OldInputs;map=$old},@{rows=$NewInputs;map=$new})) {
        foreach ($row in $pair.rows) {
            $index=if ($row.StartsWith('environment:')) {$row.LastIndexOf(':')} else {$row.IndexOf(':')}
            if ($index -le 0) {throw 'Malformed executable input row.'}
            $key=$row.Substring(0,$index)
            if ($pair.map.ContainsKey($key)) {throw 'Duplicate executable input key.'}
            $pair.map[$key]=$row.Substring($index+1)
        }
    }
    foreach ($key in @(@($old.Keys)+@($new.Keys) | Sort-Object -Unique)) {
        $before=if ($old.ContainsKey($key)) {$old[$key]} else {'<absent>'}
        $after=if ($new.ContainsKey($key)) {$new[$key]} else {'<absent>'}
        if ($before -cne $after) {$key+'|'+$before+'|'+$after}
    }
}
function Read-WorkloadApplicability {
    param([string] $Root, [string] $Path, $Identity, $InputIdentity, [string[]] $Required)
    $record=Read-WorkloadJson $Path
    if ($null -eq $record -or $record.schema -cne 'fixed-workload-applicability-1' -or $record.qualificationId -notmatch '^[0-9a-f]{32}$' -or
        $record.commit -cne $Identity.commit -or $record.sourceFingerprint -cne $Identity.fingerprint -or $record.inputFingerprint -cne $InputIdentity.fingerprint) {throw 'Applicability candidate/input lock mismatch.'}
    if ((@($record.requiredChecks | Sort-Object) -join '|') -cne (@($Required | Sort-Object) -join '|') -or
        (@($record.decisions.name | Sort-Object) -join '|') -cne (@($Required | Sort-Object) -join '|')) {throw 'Applicability does not cover the actual cumulative plan.'}
    if (-not [IO.File]::Exists($record.legacyCachePath) -or (Get-FileHash -LiteralPath $record.legacyCachePath).Hash -cne $record.legacyCacheSha256) {throw 'Original applicability cache changed.'}
    $legacy=Read-WorkloadJson $record.legacyCachePath
    if ($null -eq $legacy -or $legacy.schemaVersion -ne 2) {throw 'Original applicability cache invalid.'}
    foreach ($set in $record.inputSets) {
        $sources=@($legacy.results | Where-Object {$_.allInputFingerprint -ceq $set.fingerprint})
        if ($sources.Count -eq 0 -or (Get-WorkloadHash @($sources[0].inputs)) -cne $set.fingerprint -or
            (@(Get-WorkloadInputDifferences $sources[0].inputs $InputIdentity.inputs) -join "`n") -cne (@($set.differences) -join "`n")) {throw 'Unexplained original-to-candidate input difference.'}
        if (@($set.differences | Where-Object {$_ -match '^(environment:|os\||sdk\||framework-x86/|external/)'}).Count -gt 0) {throw 'Changed environment/dependency/symbol inputs are outside fixed source qualification.'}
    }
    $mustExecute=@('workload-Routing','workload-Evidence','workload-PackageVerification','native-InputBoundary','native-NpcRollingCpu','native-NpcRollingSelectionNegative','native-ToolsExecutionCpu','native-NpcWorkerTransport','native-NpcStrategyContinuous')
    foreach ($decision in $record.decisions) {
        if ($decision.action -cnotin @('EXECUTE','QUALIFIED') -or ($mustExecute -contains $decision.name -and $decision.action -cne 'EXECUTE')) {throw 'Invalid fixed applicability action.'}
        if ($decision.action -ceq 'QUALIFIED' -and (-not $decision.reason -or $decision.originalExecutionId -notmatch '^[0-9a-f]{32}$')) {throw 'Qualification requires the original execution and reviewed reason.'}
    }
    $additional=@()
    if ($Required -contains 'native-NpcStrategy') {
        if ((@($record.additionalChecks) -join '|') -cne 'native-NpcStrategy-rollchoice') {throw 'Strategy lifetime delta obligation missing.'}
        $checks=Join-Path $Root 'artifacts/build/Debug/checks'
        $additional=@([pscustomobject]@{name='native-NpcStrategy-rollchoice';executable=(Join-Path $checks 'bin/NativeWorldTextProbe/x86/Debug/net472/NativeWorldTextProbe.exe');arguments=@($Root,'--cpu',(Join-Path $checks 'NpcStrategy-rollchoice'),'NpcStrategy:rollchoice');project='NativeWorldTextProbe'})
    } elseif (@($record.additionalChecks).Count -gt 0) {throw 'Unknown additional applicability scope.'}
    $known=@($Required)+@($additional | ForEach-Object {$_.name})
    foreach ($decision in $record.decisions) {
        foreach ($dependency in $decision.requiresCurrent) {if ($known -notcontains $dependency -or $dependency -ceq $decision.name) {throw 'Unknown/circular current qualification proof.'}}
        if ($decision.name -ceq 'native-NpcStrategy' -and $decision.action -ceq 'QUALIFIED') {
            foreach ($requiredProof in @('native-NpcStrategy-rollchoice','native-NpcStrategyContinuous','native-NpcRollingCpu','native-NpcRollingSelectionNegative')) {
                if ($decision.requiresCurrent -notcontains $requiredProof) {throw 'Full strategy qualification omits its current lifetime delta proof.'}
            }
        }
    }
    return [pscustomobject]@{record=$record;path=[IO.Path]::GetFullPath($Path);sha256=(Get-FileHash -LiteralPath $Path).Hash;legacy=$legacy;additional=$additional}
}
# A single pointer preserves an explicitly adopted finite lock outside the
# Debug root that build owns and replaces. It is not historical qualification.
function Read-WorkloadApplicabilityPointer {
    param([string] $Root,$Identity,$InputIdentity)
    $path=Join-Path $Root 'artifacts/build/workload-checkpoints/current-applicability.json'
    if (-not [IO.File]::Exists($path)) {return $null}
    $value=Read-WorkloadJson $path
    if ($null -eq $value -or $value.commit -cne $Identity.commit -or $value.sourceFingerprint -cne $Identity.fingerprint -or
        $value.inputFingerprint -cne $InputIdentity.fingerprint -or -not [IO.File]::Exists($value.path) -or
        (Get-FileHash -LiteralPath $value.path).Hash -cne $value.sha256) {throw 'Saved finite applicability pointer is damaged/changed; review bounded obligations.'}
    return $value.path
}
function Save-WorkloadApplicabilityPointer {
    param([string] $Root,$Identity,$InputIdentity,[string] $Path)
    Write-WorkloadJson (Join-Path $Root 'artifacts/build/workload-checkpoints/current-applicability.json') ([ordered]@{
        path=[IO.Path]::GetFullPath($Path);sha256=(Get-FileHash -LiteralPath $Path).Hash;commit=$Identity.commit;sourceFingerprint=$Identity.fingerprint;inputFingerprint=$InputIdentity.fingerprint
    })
}
function Get-WorkloadQualifiedOriginal {
    param([string] $Root, $Applicability, $Check, $InputIdentity)
    try {
        $decision=@($Applicability.record.decisions | Where-Object {$_.name -ceq $Check.name -and $_.action -ceq 'QUALIFIED'})
        if ($decision.Count -ne 1) {return $null}
        $found=@($Applicability.legacy.results | Where-Object {$_.name -ceq $Check.name -and $_.executionId -ceq $decision[0].originalExecutionId})
        if ($found.Count -ne 1) {return $null};$original=$found[0]
        $signature=Get-WorkloadHash (@($Check.executable)+@($Check.arguments))
        if (-not (Test-WorkloadEvidence $original $original.inputFingerprint $Check.name $signature) -or
            (Get-WorkloadCheckFingerprint $original $Check.name) -cne $original.inputFingerprint -or
            @($Applicability.record.inputSets | Where-Object {$_.fingerprint -ceq $original.allInputFingerprint}).Count -ne 1 -or
            -not (Test-WorkloadLatestAttempt $Root $original $Check.name $signature) -or
            -not (Test-WorkloadEvidenceOutputs $Root $original.outputs)) {return $null}
        $absent=@($original.outputs | ForEach-Object {Split-Path -Parent $_.livePath} | Sort-Object -Unique | Where-Object {-not [IO.Directory]::Exists($_)})
        if (-not (Test-WorkloadLiveArtifacts $original.outputs $false $absent)) {return $null}
        return $original
    } catch {return $null}
}
function Test-WorkloadLatestAttempt {
    param([string] $Root,$Evidence,[string] $Name,[string] $Signature)
    # Both direct reuse and cross-candidate qualification honor the same
    # retirement marker. A matching stable success is not a blanket veto;
    # later incomplete/failed/corrupt/different attempts still retire old PASS.
    try {
        $latestPath=Join-Path $Root ('artifacts/build/workload-checkpoints/latest-'+$Name+'.json')
        $hasCheckpoint=$null -ne $Evidence.PSObject.Properties['checkpointPath']
        if (-not [IO.File]::Exists($latestPath)) {return -not $hasCheckpoint}
        $latest=Read-WorkloadJson $latestPath
        if ($null -eq $latest -or $null -eq $latest.PSObject.Properties['path'] -or $null -eq $latest.PSObject.Properties['executionId']) {return $false}
        if ($hasCheckpoint -and $latest.path -cne $Evidence.checkpointPath) {return $false}
        $receipt=Read-WorkloadJson $latest.path
        return $latest.executionId -ceq $Evidence.executionId -and (Test-WorkloadCheckpointQualified $Root $receipt) -and
            $receipt.name -ceq $Name -and $receipt.signature -ceq $Signature
    } catch {return $false}
}
function Invoke-WorkloadProcess {
    param([string] $Name, [string] $Executable, [string[]] $Arguments, $Checkpoint=$null)
    if (-not [IO.File]::Exists($Executable)) { throw ('Missing check executable: ' + $Name) }
    $previousPreference = $ErrorActionPreference
    $exitCode = $null
    $onlyVariables = @(Get-WorkloadClearedEnvironment)
    $writer=$null
    if ($null -ne $Checkpoint) {
        $Checkpoint.clearedSelectors=@($onlyVariables | ForEach-Object {$_.Name} | Sort-Object)
        $Checkpoint.effectiveEnvironment=@(Get-ChildItem Env: | Where-Object {$_.Name -match '^(DOTNET_|MSBUILD|COMPlus_|JUEMINGR|Configuration$|Platform$|DefineConstants$|TargetFramework$)' -and $_.Name -notmatch '^JUEMINGR_.*(ONLY|SCOPE|PHASES?|SINGLE)$'} | Sort-Object Name | ForEach-Object {$_.Name+':'+(Get-WorkloadHash @($_.Value))})
        $Checkpoint.status='RUNNING';$Checkpoint.startedUtc=[DateTime]::UtcNow.ToString('o')
        Write-WorkloadJson $Checkpoint.path $Checkpoint
        $writer=New-Object IO.StreamWriter($Checkpoint.outputPath,$false,(New-Object Text.UTF8Encoding($false)))
    }
    try {
        foreach ($variable in $onlyVariables) { Remove-Item -LiteralPath ('Env:'+$variable.Name) }
        # Windows PowerShell can promote ordinary native stderr to an error
        # under the package entry's 2>&1 capture. Keep that output, but decide
        # success only from this invocation's exit, never a stale prior value.
        $ErrorActionPreference = 'Continue'
        $global:LASTEXITCODE = $null
        & $Executable @Arguments 2>&1 | ForEach-Object {
            if ($null -ne $writer) {$writer.WriteLine([string]$_);$writer.Flush()}
            if ($_ -is [Management.Automation.ErrorRecord]) {Write-Error -ErrorRecord $_ -ErrorAction Continue} else {$_ | Out-Host}
        }
        $exitCode = $global:LASTEXITCODE
    } finally { if ($null -ne $writer) {$writer.Dispose()};foreach ($variable in $onlyVariables) { Set-Item -LiteralPath ('Env:'+$variable.Name) -Value $variable.Value }; $ErrorActionPreference = $previousPreference }
    if ($null -ne $Checkpoint) {
        $Checkpoint.endedUtc=[DateTime]::UtcNow.ToString('o');$Checkpoint.exitCode=$exitCode
        $Checkpoint.status=if ($null -ne $exitCode -and $exitCode -eq 0) {'EXECUTED_AWAITING_STABILITY'} else {'FAILED'}
        $Checkpoint.outputSha256=(Get-FileHash -LiteralPath $Checkpoint.outputPath -Algorithm SHA256).Hash
        Write-WorkloadJson $Checkpoint.path $Checkpoint
    }
    if ($null -eq $exitCode) { throw ("Workload check $Name did not produce a process exit code.") }
    if ($exitCode -ne 0) { throw ("Workload check $Name failed with exit $exitCode.") }
}
function Get-WorkloadEvidenceOutputs {
    param([string] $Root, $Record, [string] $Executable, [string[]] $Arguments=@())
    $paths = @($Record.outputs | ForEach-Object { Join-Path (Join-Path $Root 'artifacts/build/Debug/work') $_.path })
    $paths += $Executable
    $paths += @($Arguments | Where-Object {[IO.File]::Exists($_)})
    if ($Executable.StartsWith((Join-Path $Root 'artifacts/build/Debug/checks'), [StringComparison]::OrdinalIgnoreCase)) {
        $paths += @(Get-ChildItem -LiteralPath (Split-Path -Parent $Executable) -File | ForEach-Object {$_.FullName})
    }
    foreach ($path in @($paths | Sort-Object -Unique)) {
        [ordered]@{path=$path; length=(Get-Item -LiteralPath $path).Length; sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
    }
}
function Save-WorkloadArtifacts {
    param([string] $Root, $Outputs)
    # Content-addressed snapshots preserve the actual original execution bytes
    # when a later compile changes only an unrelated leaf test or Git metadata.
    # No old binary is relabelled as the current build or shipped in a package.
    $key = Get-WorkloadHash @($Outputs | ForEach-Object {$_.path+':'+$_.sha256})
    $directory = Join-Path $Root ('artifacts/build/evidence-artifacts/'+$key)
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    $index=0
    foreach ($output in $Outputs) {
        $path=Join-Path $directory ([string]$index+'-'+[IO.Path]::GetFileName($output.path)); $index++
        if (-not [IO.File]::Exists($path) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $output.sha256) {
            # A failed archive must recover after a real successful rerun. Copy
            # only current, already checked output into this owned hash slot.
            $temporary=$path+'.pending'
            [IO.File]::Copy($output.path,$temporary,$true)
            if ((Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash -cne $output.sha256) {throw 'Execution artifact changed while archiving.'}
            Move-Item -LiteralPath $temporary -Destination $path -Force
        }
        [ordered]@{path=$path;livePath=$output.path;length=$output.length;sha256=$output.sha256}
    }
}
function Remove-UnusedWorkloadArtifacts {
    param([string] $Root, $Entries, [switch] $Collect, [AllowEmptyCollection()][string[]] $RetainedRecords=@())
    # A regular build lacks the external task-retention list. It neither
    # deletes archives nor scans all historical recovery records each time.
    if(-not $Collect){return}
    if(-not $PSBoundParameters.ContainsKey('RetainedRecords')){throw 'Explicit collection requires a declared retention list (including an explicitly empty list).'}
    $repository=[IO.Path]::GetFullPath($Root).TrimEnd('\')+'\'
    $base=[IO.Path]::GetFullPath((Join-Path $Root 'artifacts/build/evidence-artifacts')).TrimEnd('\')+'\'
    if (-not [IO.Directory]::Exists($base)) { return }
    # FullName is lexical on Windows: a junction above a hash directory can
    # otherwise make an apparently local deletion affect external files.
    $rootPath=$repository.TrimEnd('\')
    for($ancestor=Get-Item -LiteralPath $base;$null -ne $ancestor -and $ancestor.FullName.Length -ge $rootPath.Length;$ancestor=$ancestor.Parent){
        if($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint){return}
    }
    foreach($record in $RetainedRecords){
        if(-not $record){throw 'Empty retention record path.'}
        $path=[IO.Path]::GetFullPath($record)
        if(-not $path.StartsWith($repository,[StringComparison]::OrdinalIgnoreCase) -or -not [IO.File]::Exists($path)){throw ('Missing/outside retention record: '+$record)}
        $file=Get-Item -LiteralPath $path
        for($ancestor=$file;$null -ne $ancestor -and $ancestor.FullName.Length -ge $rootPath.Length;$ancestor=$(if($ancestor -is [IO.FileInfo]){$ancestor.Directory}else{$ancestor.Parent})){
            if($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint){throw ('Reparse retention record: '+$record)}
        }
        # Prove readability before any deletion, even if JSON is incomplete.
        $null=[IO.File]::ReadAllText($path)
    }
    $used=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach($entry in $Entries){foreach($output in $entry.outputs){[void]$used.Add([IO.Path]::GetFullPath((Split-Path -Parent $output.path)))}}
    $queue=New-Object 'System.Collections.Generic.Queue[string]'
    $seen=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    $records=@($RetainedRecords)+@(Join-Path $Root 'artifacts/build/workload-evidence.json')+@(Join-Path $Root 'artifacts/build/Debug/build-record.json')+@(Join-Path $Root 'artifacts/build/Release/build-record.json')
    $checkpoints=Join-Path $Root 'artifacts/build/workload-checkpoints'
    if([IO.Directory]::Exists($checkpoints)){
        $records+=@(Get-ChildItem -LiteralPath $checkpoints -Recurse -File | Where-Object {$_.Extension -eq '.json' -or $_.Name -like '*.pending'} | ForEach-Object {$_.FullName})
    }
    foreach($record in $records){$queue.Enqueue($record)}
    while($queue.Count){
        $path=[IO.Path]::GetFullPath($queue.Dequeue())
        if(-not $path.StartsWith($repository,[StringComparison]::OrdinalIgnoreCase) -or -not $seen.Add($path) -or -not [IO.File]::Exists($path)){continue}
        $file=Get-Item -LiteralPath $path
        if($file.Attributes -band [IO.FileAttributes]::ReparsePoint){continue}
        $raw=[IO.File]::ReadAllText($path)
        # Corrupt/incomplete recovery records still protect each named archive.
        # Unknown associations protect that object, not every archive forever.
        foreach($match in [regex]::Matches($raw,'(?i)evidence-artifacts[\\/]+([0-9a-f]{64})')){[void]$used.Add((Join-Path $base $match.Groups[1].Value))}
        $value=Read-WorkloadJson $path
        if($null -eq $value){continue}
        $nodes=New-Object 'System.Collections.Generic.Queue[object]';$nodes.Enqueue($value)
        while($nodes.Count){
            $node=$nodes.Dequeue()
            if($node -is [string]){
                if([IO.Path]::IsPathRooted($node) -and $node -match '\.json(?:\.pending)?$'){$queue.Enqueue($node)}
            } elseif($node -is [System.Collections.IEnumerable] -and $node -isnot [pscustomobject]){
                foreach($item in $node){if($null -ne $item){$nodes.Enqueue($item)}}
            } elseif($node -is [pscustomobject]) {foreach($property in $node.PSObject.Properties){if($null -ne $property.Value){$nodes.Enqueue($property.Value)}}}
        }
    }
    foreach ($directory in @(Get-ChildItem -LiteralPath $base -Directory)) {
        if ($directory.Name -notmatch '^[0-9A-F]{64}$' -or -not $directory.FullName.StartsWith($base,[StringComparison]::OrdinalIgnoreCase) -or
            $directory.Attributes -band [IO.FileAttributes]::ReparsePoint) {continue}
        # Normal build does not own private retained task records. Explicit
        # collection supplies those references; no automatic historical purge.
        if (-not $used.Contains($directory.FullName)) {
            # Owned snapshots are flat files. Unknown nested/reparse content
            # is not ours to follow or recursively delete.
            $members=@(Get-ChildItem -LiteralPath $directory.FullName -Force)
            if(@($members | Where-Object {$_.PSIsContainer -or $_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count){continue}
            Remove-Item -LiteralPath $directory.FullName -Recurse -Force
        }
    }
}
function Test-WorkloadLiveArtifacts {
    param($Outputs, [bool] $SameInputs, [string[]] $MayBeAbsent = @())
    $directories=@($Outputs | ForEach-Object {Split-Path -Parent $_.livePath} | Sort-Object -Unique)
    foreach ($directory in $directories) {
        # A fresh compile can remove an unneeded fixture directory. Its original
        # archived execution remains evidence; it is not a current executable.
        if (-not [IO.Directory]::Exists($directory)) { if ($MayBeAbsent -notcontains $directory) {return $false}; continue }
        $expected=@($Outputs | Where-Object {(Split-Path -Parent $_.livePath) -ceq $directory})
        # System executables are individual environment inputs, not an owned
        # fixture directory whose entire OS file set belongs to this check.
        if ($directory -notmatch '[\\/]artifacts[\\/]build[\\/]Debug[\\/]') {
            foreach ($output in $expected) { if ((Get-FileHash -LiteralPath $output.livePath -Algorithm SHA256).Hash -cne $output.sha256) {return $false} }
            continue
        }
        $actual=@(Get-ChildItem -LiteralPath $directory -File)
        if ((@($actual.Name | Sort-Object) -join '|') -cne (@($expected | ForEach-Object {[IO.Path]::GetFileName($_.livePath)} | Sort-Object) -join '|')) {return $false}
        foreach ($output in $expected) {
            # Cross-input reuse is a source proof for the archived run, not a
            # claim that rebuilt assemblies have identical metadata. Runtime
            # configuration is never exempted from live validation.
            if ($SameInputs -or [IO.Path]::GetExtension($output.livePath) -notin @('.dll','.exe','.pdb')) {
                if ((Get-FileHash -LiteralPath $output.livePath -Algorithm SHA256).Hash -cne $output.sha256) {return $false}
            }
        }
    }
    return $true
}
function Test-WorkloadReusable {
    param([string] $Root, $Evidence, $InputIdentity, [string] $Name, [string] $Signature)
    if (-not (Test-WorkloadEvidence $Evidence (Get-WorkloadCheckFingerprint $InputIdentity $Name) $Name $Signature)) {return $false}
    if (-not (Test-WorkloadLatestAttempt $Root $Evidence $Name $Signature)) {return $false}
    if ($null -eq $Evidence.PSObject.Properties['allInputFingerprint'] -or $null -eq $Evidence.PSObject.Properties['inputs'] -or
        (Get-WorkloadHash @($Evidence.inputs)) -cne $Evidence.allInputFingerprint -or
        (Get-WorkloadCheckFingerprint $Evidence $Name) -cne $Evidence.inputFingerprint) {return $false}
    $debug=Read-WorkloadJson (Join-Path $Root 'artifacts/build/Debug/build-record.json')
    $sameBuild=$null -ne $debug -and $Evidence.detectionCommit -ceq $debug.commit -and $Evidence.allInputFingerprint -ceq $InputIdentity.fingerprint
    $absent=@($Evidence.outputs | ForEach-Object {Split-Path -Parent $_.livePath} | Sort-Object -Unique | Where-Object {-not [IO.Directory]::Exists($_)})
    return (Test-WorkloadEvidenceOutputs $Root $Evidence.outputs) -and (Test-WorkloadLiveArtifacts $Evidence.outputs $sameBuild $absent)
}
function Test-WorkloadEvidenceOutputs {
    param([string] $Root, $Outputs)
    if (@($Outputs).Count -eq 0) { return $false }
    foreach ($output in $Outputs) {
        if (-not [IO.File]::Exists($output.path) -or (Get-Item -LiteralPath $output.path).Length -ne $output.length -or
            (Get-FileHash -LiteralPath $output.path -Algorithm SHA256).Hash -cne $output.sha256) { return $false }
    }
    return $true
}
function Get-WorkloadPlan {
    param([string] $Root, [string] $ChecksRoot, [string] $Architecture, [string[]] $Catalog, [string[]] $Groups)
    $plan = New-Object 'System.Collections.Generic.List[object]'
    foreach ($line in $Catalog) {
        $parts = $line.Split('|')
        if ($parts.Count -ne 2) { throw 'Invalid business catalogue row.' }
        if ($Groups -contains $parts[1]) { $plan.Add(@{name=$parts[0]; executable=$Architecture; arguments=@('--check',$parts[0],$Root); project=''}) }
    }
    $fixture = Join-Path $ChecksRoot 'bin/Phase0SFixtureTerraria/x86/Debug/net472/Terraria.exe'
    $native = Join-Path $ChecksRoot 'bin/NativeWorldTextProbe/x86/Debug/net472/NativeWorldTextProbe.exe'
    $modes = [ordered]@{
        'notes-input'=@('notes-host'); 'entity-style'=@('style-host'); 'world-targets-style'=@('style-host');
        'focus-input'=@('notes-host','shared-host','pages-host'); 'hotkeys-popup'=@('notes-host','shared-host','pages-host');
        'f5-cpu'=@('about-host','shared-host','pages-host'); 'death-popup'=@('death-host');
        'footprints-popup'=@('footprints-host'); 'map-popup'=@('map-host');
        'world-targets-observation'=@('world-host','shared-host'); 'world-targets-projection'=@('world-host','shared-host');
        'entity-observation'=@('world-host','shared-host'); 'entity-projection'=@('shared-host');
        'entity-preferences'=@('shared-host'); 'entity-controls'=@('shared-host');
        'items-safety'=@('shared-host'); 'information-defaults'=@('shared-host')
    }
    foreach ($mode in $modes.Keys) {
        if (@($modes[$mode] | Where-Object {$Groups -contains $_}).Count -gt 0) { $plan.Add(@{name='fixture-'+$mode; executable=$fixture; arguments=@($mode); project='Phase0SFixtureTerraria'}) }
    }
    $scopes = [ordered]@{
        'NpcRollingCpu'=@('combat-host');
        'NpcRollingSelectionNegative'=@('combat-host');
        'NpcBasicMotion'=@('combat-host');
        'NpcFoundationRules'=@('combat-host'); 'NpcFoundationContinuous'=@('combat-host'); 'NpcPlayerPolicy'=@('combat-host'); 'NpcStrategy'=@('combat-host'); 'NpcStrategyContinuous'=@('combat-host'); 'NpcEventRetirementCpu'=@('combat-host');
        # Ordinary delivery retains the real shared selection/terrain and
        # marker consumer seams; detailed phase matrices stay bounded probes.
        'NpcSync'=@('combat-host'); 'NpcLocalFailure'=@('combat-host'); 'NpcDisplayIsolation'=@('combat-host'); 'NpcCloseoutTerrain'=@('combat-host');
        'NpcSharedGeometry'=@('combat-host'); 'NpcTargetMarker'=@('combat-host');
        'NpcSamplePresentation'=@('combat-host'); 'NpcFiniteFlight'=@('combat-host'); 'CombatYoyoCausal'=@('combat-host');
        'NpcWorkerIntegration'=@('combat-host'); 'NpcSnapshot'=@('combat-host'); 'NpcWorkerPreparation'=@('combat-host');
        'NpcDiagnosticsOff'=@('combat-host'); 'NpcPostDelivery'=@('combat-host'); 'NpcGuardianQuery'=@('combat-host'); 'NpcModeledImpact'=@('combat-host');
        'NpcLegalCoverage'=@('combat-host'); 'NpcWorkerTransport'=@('combat-host'); 'NpcMenuPreparation'=@('combat-host'); 'NpcSessionCapacity'=@('combat-host'); 'NpcProduction'=@('combat-host'); 'NpcLongCoverage'=@('combat-host');
        'WorkloadCpu'=@('world-host','shared-host'); 'InformationCpu'=@('information','shared-host'); 'GuidanceCpu'=@('guidance','shared-host');
        'ShortFeedbackCpu'=@('shared-host','storage-host','quick-items-host','coin-deposit-host','recovery-host','processing-host','about-host','tools-host','fishing-host','combat-host');
        'CombatCpu'=@('combat-host'); 'CombatFacingCpu'=@('combat-host'); 'CombatHitsCpu'=@('combat-host'); 'CombatReportCpu'=@('combat-host'); 'CombatUiCpu'=@('combat-host'); 'CombatObservationCpu'=@('combat-host'); 'CombatCosts'=@('combat-host');
        'InputBoundary'=@('input-boundary','shared-host');
        'ToolsCpu'=@('tools-host'); 'ToolsCadence'=@('tools-host'); 'ToolsExecutionCpu'=@('tools-host'); 'ToolsWorkload'=@('tools-host');
        'PageCompositionCpu'=@('pages-host'); 'FishingCpu'=@('fishing-host'); 'BackgroundCpu'=@('fishing-host','shared-host'); 'F5AutomationCpu'=@('fishing-host','shared-host');
        'AboutCpu'=@('about-host'); 'BrowserCpu'=@('browser-host'); 'QuickItemsCpu'=@('quick-items-host'); 'CoinDepositCpu'=@('coin-deposit-host');
        'RecoveryCpu'=@('recovery-host'); 'ProcessingCpu'=@('processing-host'); 'DeathCpu'=@('death-host'); 'FootprintsCpu'=@('footprints-host'); 'ExplorationCpu'=@('map-host')
    }
    foreach ($scope in $scopes.Keys) {
        if (@($scopes[$scope] | Where-Object {$Groups -contains $_}).Count -gt 0) { $plan.Add(@{name='native-'+$scope; executable=$native; arguments=@($Root,'--cpu',(Join-Path $ChecksRoot $scope),$scope); project='NativeWorldTextProbe'}) }
    }
    if ($Groups -contains 'combat-host') {
        # Integration above creates and authenticates this exact shared layout.
        # The separate process binds the private image before native fixture JIT.
        $plan.Add(@{name='native-NpcPrivateSafety'; executable=$native; arguments=@($Root,(Join-Path $ChecksRoot 'prediction-worker-layout'),(Join-Path $ChecksRoot 'NpcPrivateSafety'),'NpcPrivateSafety'); project='NativeWorldTextProbe'})
    }
    if ($Groups -contains 'shared-host' -or $Groups -contains 'storage-host') {
        foreach ($name in @('Routing','Evidence')) {
            $plan.Add(@{name='workload-'+$name; executable=(Get-Command powershell.exe).Source; arguments=@('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $Root ('tests/Workload/Invoke-Workload'+$name+'Checks.ps1'))); project=''})
        }
    }
    if ($Groups -contains 'package-tools' -or $Groups -contains 'shared-host' -or $Groups -contains 'storage-host') {
        $plan.Add(@{name='workload-PackageVerification'; executable=(Get-Command powershell.exe).Source; arguments=@('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $Root 'tests/Phase0S/Invoke-PackageVerificationChecks.ps1')); project=''})
    }
    return $plan.ToArray()
}
function Test-WorkloadDelivery {
    param([string] $Root, $Record)
    try {
        if ($null -eq $Record -or $Record.schemaVersion -ne 4 -or -not $Record.clean -or $Record.configuration -cne 'Release' -or $Record.sdk -cne '10.0.203') { return $false }
        $identity = Get-WorkloadIdentity $Root
        $inputIdentity = Get-WorkloadEvidenceInput $Root $identity
        if ($Record.commit -cne $identity.commit -or $Record.sourceFingerprint -cne $identity.fingerprint -or $Record.inputFingerprint -cne $inputIdentity.fingerprint -or
            -not (Test-WorkloadOutputs (Join-Path $Root 'artifacts/build/Release/work') $Record.outputs)) { return $false }
        $changes = Get-WorkloadChanges $Root ''
        if ($Record.workload.requestedBaseline) {
            $extra = Get-WorkloadChanges $Root $Record.workload.requestedBaseline
            if ($extra.reason) { return $false }
            $changes.paths = @($changes.paths + $extra.paths | Sort-Object -Unique)
        }
        $route = Get-WorkloadRoute $changes.paths
        if ($changes.reason -or $route.unknown.Count -gt 0) { return $false }
        if ($Record.workload.mode -ceq 'Full') { $route = Get-WorkloadRoute @('scripts/build.ps1') }
        $debug = Read-WorkloadJson (Join-Path $Root 'artifacts/build/Debug/build-record.json')
        if (-not (Test-WorkloadBuildMatch $Root $debug $identity)) { return $false }
        $architecture = Join-Path $Root 'artifacts/build/Debug/work/bin/JueMingR.ArchitectureTests/x86/Debug/net472/JueMingR.ArchitectureTests.exe'
        $catalog = @(& $architecture --list-checks)
        if ($LASTEXITCODE -ne 0 -or $catalog.Count -eq 0) { return $false }
        $plan = @(Get-WorkloadPlan $Root (Join-Path $Root 'artifacts/build/Debug/checks') $architecture $catalog $route.groups)
        $applicability=$null
        if ($Record.workload.PSObject.Properties.Name -contains 'applicabilityRecord') {
            if (-not [IO.File]::Exists($Record.workload.applicabilityRecord) -or (Get-FileHash -LiteralPath $Record.workload.applicabilityRecord).Hash -cne $Record.workload.applicabilitySha256) {return $false}
            $applicability=Read-WorkloadApplicability $Root $Record.workload.applicabilityRecord $identity $inputIdentity @($plan | ForEach-Object {$_.name})
            $plan+=@($applicability.additional)
        } elseif (@($Record.workload.results | Where-Object {$_.disposition -ceq 'QUALIFIED'}).Count -gt 0) {return $false}
        $required = @($plan | ForEach-Object {$_.name})
        if (-not (Test-WorkloadCoverage $Record.workload $required $inputIdentity.fingerprint)) { return $false }
        $cache = Read-WorkloadJson (Join-Path $Root 'artifacts/build/workload-evidence.json')
        if ($null -eq $cache -or $cache.schemaVersion -ne 2) { return $false }
        foreach ($check in $plan) {
            $found = @($cache.results | Where-Object {$_.name -ceq $check.name})
            $receipt = @($Record.workload.results | Where-Object {$_.name -ceq $check.name})
            if ($receipt.Count -eq 1 -and $receipt[0].disposition -ceq 'QUALIFIED') {
                if ($null -eq $applicability -or $receipt[0].qualificationId -cne $applicability.record.qualificationId) {return $false}
                $original=Get-WorkloadQualifiedOriginal $Root $applicability $check $inputIdentity
                if ($null -eq $original -or $found.Count -ne 1 -or $receipt[0].executionId -cne $original.executionId -or $found[0].executionId -cne $original.executionId -or
                    $receipt[0].sourceCommit -cne $original.sourceCommit -or $found[0].sourceCommit -cne $original.sourceCommit -or
                    $found[0].sourceFingerprint -cne $original.sourceFingerprint -or $found[0].status -cne $original.status -or
                    (Get-WorkloadHash @($found[0].inputs)) -cne $original.allInputFingerprint -or
                    $found[0].inputFingerprint -cne $original.inputFingerprint -or $found[0].allInputFingerprint -cne $original.allInputFingerprint -or
                    $found[0].signature -cne $original.signature -or $found[0].executedUtc -cne $original.executedUtc -or
                    ($found[0].outputs | ConvertTo-Json -Depth 5 -Compress) -cne ($original.outputs | ConvertTo-Json -Depth 5 -Compress)) {return $false}
                $decision=@($applicability.record.decisions | Where-Object {$_.name -ceq $check.name})[0]
                foreach ($dependency in $decision.requiresCurrent) {
                    if (@($Record.workload.results | Where-Object {$_.name -ceq $dependency -and $_.result -ceq 'PASS' -and $_.disposition -in @('EXECUTED','REUSED')}).Count -ne 1) {return $false}
                }
                continue
            }
            if ($found.Count -ne 1 -or $receipt.Count -ne 1 -or $receipt[0].executionId -cne $found[0].executionId -or
                -not (Test-WorkloadReusable $Root $found[0] $inputIdentity $check.name (Get-WorkloadHash (@($check.executable)+@($check.arguments))))) { return $false }
        }
        return $true
    } catch { return $false }
}
