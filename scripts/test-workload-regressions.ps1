[CmdletBinding()]
param([string] $Baseline)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'workload/Workload.Support.ps1')
& (Join-Path $PSScriptRoot 'prepare-terraria-references.ps1') -VerifyOnly | Out-Host
& (Join-Path $PSScriptRoot 'prepare-harmony.ps1') -VerifyOnly | Out-Host
$identity = Get-WorkloadIdentity $repositoryRoot
$recordPath = Join-Path $repositoryRoot 'artifacts/build/Debug/build-record.json'
if (-not [IO.File]::Exists($recordPath)) { throw 'A matching Debug detection build is required; run scripts/build.ps1.' }
$record = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
if (-not (Test-WorkloadBuildMatch $repositoryRoot $record $identity)) { throw 'Debug detection binaries do not match current source/SDK/output hashes.' }
if ((& dotnet.exe --version).Trim() -cne '10.0.203' -or $LASTEXITCODE -ne 0) { throw 'Locked SDK unavailable.' }
$changes = Get-WorkloadChanges $repositoryRoot $Baseline
$route = Get-WorkloadRoute $changes.paths
$results = New-Object 'System.Collections.Generic.List[object]'
$checksRoot = Join-Path $repositoryRoot 'artifacts/build/Debug/checks'
[IO.Directory]::CreateDirectory($checksRoot) | Out-Null
function Invoke-WorkloadCheck {
    param([string] $Name, [string] $Executable, [string[]] $Arguments)
    $script:currentCheck = $Name
    if (-not [IO.File]::Exists($Executable)) { throw ('Missing check executable: ' + $Name) }
    $clock = [Diagnostics.Stopwatch]::StartNew()
    & $Executable @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0) { throw ("Workload check $Name failed with exit $LASTEXITCODE.") }
    $results.Add([ordered]@{ name = $Name; result = 'PASS'; milliseconds = $clock.ElapsedMilliseconds })
}
function Build-WorkloadFixture {
    param([string] $Project)
    $script:currentCheck = 'compile-' + $Project
    # Consume the already compiled Features/Host outputs. This does not invoke build.ps1.
    & dotnet.exe build (Join-Path $repositoryRoot ('tests/' + $Project + '/' + $Project + '.csproj')) --configuration Debug --nologo -p:Platform=x86 "-p:JueMingRBuildRoot=$checksRoot" | Out-Host
    if ($LASTEXITCODE -ne 0) { throw ('Workload fixture build failed: ' + $Project) }
    $name = if ($Project -ceq 'Phase0SFixtureTerraria') { 'Terraria' } else { $Project }
    return Join-Path $checksRoot ('bin/' + $Project + '/x86/Debug/net472/' + $name + '.exe')
}
try {
$architecture = Join-Path $repositoryRoot 'artifacts/build/Debug/work/bin/JueMingR.ArchitectureTests/x86/Debug/net472/JueMingR.ArchitectureTests.exe'
Invoke-WorkloadCheck 'core-records-selection' $architecture @('--workload-core', $repositoryRoot)
$fixture = Build-WorkloadFixture 'Phase0SFixtureTerraria'
foreach ($mode in @('notes-input', 'entity-style', 'world-targets-style')) { Invoke-WorkloadCheck ('core-' + $mode) $fixture @($mode) }
$native = Build-WorkloadFixture 'NativeWorldTextProbe'
Invoke-WorkloadCheck 'core-native-host' $native @($repositoryRoot, '--cpu', (Join-Path $checksRoot 'native-cpu'), 'WorkloadCpu')
if ($route.groups -contains 'shared-host') { Invoke-WorkloadCheck 'information-native-host' $native @($repositoryRoot, '--cpu', (Join-Path $checksRoot 'information-cpu'), 'InformationCpu') }
if ($route.groups -contains 'shared-host') { Invoke-WorkloadCheck 'guidance-native-host' $native @($repositoryRoot, '--cpu', (Join-Path $checksRoot 'guidance-cpu'), 'GuidanceCpu') }
$modes = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
if ($route.groups -contains 'notes-host') { [void]$modes.Add('hotkeys-popup'); [void]$modes.Add('focus-input') }
if ($route.groups -contains 'world-host') { foreach ($mode in @('world-targets-observation', 'world-targets-projection', 'entity-observation')) { [void]$modes.Add($mode) } }
if ($route.groups -contains 'shared-host') { foreach ($mode in @('f5-cpu', 'focus-input', 'hotkeys-popup', 'entity-observation', 'entity-projection', 'entity-preferences', 'entity-controls', 'world-targets-observation', 'world-targets-projection', 'items-safety', 'information-defaults')) { [void]$modes.Add($mode) } }
foreach ($mode in @($modes | Sort-Object)) { Invoke-WorkloadCheck $mode $fixture @($mode) }
if ($route.groups -contains 'storage-host') { Invoke-WorkloadCheck 'storage-host' $architecture @('--workload-storage', $repositoryRoot) }
if (@($changes.paths | Where-Object { $_ -match '^scripts/(workload/|test-workload-regressions\.ps1|build\.ps1)|^tests/Workload/' }).Count -gt 0) {
    Invoke-WorkloadCheck 'routing-contract' (Get-Command powershell.exe).Source @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $repositoryRoot 'tests/Workload/Invoke-WorkloadRoutingChecks.ps1'))
}
if ($results.Count -lt 5) { throw 'Zero/incomplete workload execution cannot pass.' }
$script:currentCheck = 'final-source-and-route-validation'
if ($identity.fingerprint -cne (Get-WorkloadIdentity $repositoryRoot).fingerprint) { throw 'Source changed during workload execution.' }
if ($changes.reason -or $route.unknown.Count -gt 0) { throw ($changes.reason + ' Unclassified paths: ' + ($route.unknown -join ', ')) }
$result = [ordered]@{ status = 'PASS'; commit = $identity.commit; sourceFingerprint = $identity.fingerprint;
    baseline = $changes.baseline; changedPaths = $changes.paths; groups = $route.groups; runtime = '.NET Framework 4.7.2 target / x86';
    checkCount = $results.Count; results = @($results.ToArray()); detectionOutputs = $record.outputs; slowGraphics = 'separate-risk-triggered-entry' }
$record.workload = $result
[IO.File]::WriteAllText($recordPath, ($record | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding($false)))
Write-Output $result
} catch {
    $failure = [ordered]@{ status = 'FAILED'; commit = $identity.commit; sourceFingerprint = $identity.fingerprint;
        baseline = $changes.baseline; changedPaths = $changes.paths; groups = $route.groups; failedCheck = $script:currentCheck;
        reason = $_.Exception.Message; checkCount = $results.Count; completedResults = @($results.ToArray()); remaining = 'not executed after failure' }
    $record.workload = $failure
    [IO.File]::WriteAllText($recordPath, ($record | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding($false)))
    $_.Exception.Data['workload'] = $failure
    throw
}
