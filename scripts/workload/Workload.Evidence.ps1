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
    $rows = @($Identity.inputs | Where-Object { $_ -notmatch '^(docs/|AGENTS\.md:|README(?:\.[^/:]+)?:|LICENSE:|THIRD-PARTY-NOTICES\.md:)' })
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
    return $Evidence.schemaVersion -eq 1 -and $Evidence.status -ceq 'PASS' -and
        $Evidence.inputFingerprint -ceq $Fingerprint -and $Evidence.name -ceq $Name -and
        $Evidence.signature -ceq $Signature -and $Evidence.executionId -match '^[0-9a-f]{32}$' -and
        $Evidence.sourceCommit -match '^[0-9a-f]{40}$' -and $Evidence.sourceFingerprint -match '^[0-9A-F]{64}$' -and @($Evidence.outputs).Count -gt 0
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
    $actual = @(Get-ChildItem -LiteralPath (Join-Path $WorkRoot 'bin') -Recurse -File | Where-Object { $_.Extension -in @('.dll','.exe','.pdb') })
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
        $found = @($Workload.results | Where-Object { $_.name -ceq $name -and $_.result -ceq 'PASS' -and $_.disposition -in @('EXECUTED','REUSED') })
        if ($found.Count -ne 1) { return $false }
    }
    return $names.Count -eq $Required.Count
}
function Invoke-WorkloadProcess {
    param([string] $Name, [string] $Executable, [string[]] $Arguments)
    if (-not [IO.File]::Exists($Executable)) { throw ('Missing check executable: ' + $Name) }
    & $Executable @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0) { throw ("Workload check $Name failed with exit $LASTEXITCODE.") }
}
function Get-WorkloadEvidenceOutputs {
    param([string] $Root, $Record, [string] $Executable)
    $paths = @($Record.outputs | ForEach-Object { Join-Path (Join-Path $Root 'artifacts/build/Debug/work') $_.path })
    $paths += $Executable
    if ($Executable.StartsWith((Join-Path $Root 'artifacts/build/Debug/checks'), [StringComparison]::OrdinalIgnoreCase)) {
        $paths += @(Get-ChildItem -LiteralPath (Split-Path -Parent $Executable) -File | Where-Object { $_.Extension -in @('.exe','.dll','.pdb') } | ForEach-Object {$_.FullName})
    }
    foreach ($path in @($paths | Sort-Object -Unique)) {
        [ordered]@{path=$path; length=(Get-Item -LiteralPath $path).Length; sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
    }
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
        'WorkloadCpu'=@('world-host','shared-host'); 'InformationCpu'=@('information','shared-host'); 'GuidanceCpu'=@('guidance','shared-host');
        'ShortFeedbackCpu'=@('shared-host','storage-host','quick-items-host','coin-deposit-host','recovery-host','processing-host','about-host','tools-host','fishing-host');
        'ToolsCpu'=@('tools-host'); 'ToolsCadence'=@('tools-host'); 'ToolsExecutionCpu'=@('tools-host'); 'ToolsWorkload'=@('tools-host');
        'PageCompositionCpu'=@('pages-host'); 'FishingCpu'=@('fishing-host'); 'BackgroundCpu'=@('fishing-host','shared-host'); 'F5AutomationCpu'=@('fishing-host','shared-host');
        'AboutCpu'=@('about-host'); 'BrowserCpu'=@('browser-host'); 'QuickItemsCpu'=@('quick-items-host'); 'CoinDepositCpu'=@('coin-deposit-host');
        'RecoveryCpu'=@('recovery-host'); 'ProcessingCpu'=@('processing-host'); 'DeathCpu'=@('death-host'); 'FootprintsCpu'=@('footprints-host'); 'ExplorationCpu'=@('map-host')
    }
    foreach ($scope in $scopes.Keys) {
        if (@($scopes[$scope] | Where-Object {$Groups -contains $_}).Count -gt 0) { $plan.Add(@{name='native-'+$scope; executable=$native; arguments=@($Root,'--cpu',(Join-Path $ChecksRoot $scope),$scope); project='NativeWorldTextProbe'}) }
    }
    if ($Groups -contains 'shared-host' -or $Groups -contains 'storage-host') {
        foreach ($name in @('Routing','Evidence')) {
            $plan.Add(@{name='workload-'+$name; executable=(Get-Command powershell.exe).Source; arguments=@('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $Root ('tests/Workload/Invoke-Workload'+$name+'Checks.ps1'))); project=''})
        }
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
        $required = @($plan | ForEach-Object {$_.name})
        if (-not (Test-WorkloadCoverage $Record.workload $required $inputIdentity.fingerprint)) { return $false }
        $cache = Read-WorkloadJson (Join-Path $Root 'artifacts/build/workload-evidence.json')
        if ($null -eq $cache -or $cache.schemaVersion -ne 1 -or $cache.inputFingerprint -cne $inputIdentity.fingerprint) { return $false }
        foreach ($check in $plan) {
            $found = @($cache.results | Where-Object {$_.name -ceq $check.name})
            $receipt = @($Record.workload.results | Where-Object {$_.name -ceq $check.name})
            if ($found.Count -ne 1 -or $receipt.Count -ne 1 -or $receipt[0].executionId -cne $found[0].executionId -or
                -not (Test-WorkloadEvidence $found[0] $inputIdentity.fingerprint $check.name (Get-WorkloadHash (@($check.executable)+@($check.arguments)))) -or
                -not (Test-WorkloadEvidenceOutputs $Root $found[0].outputs)) { return $false }
        }
        return $true
    } catch { return $false }
}
