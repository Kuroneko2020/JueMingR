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
foreach ($path in @('scripts/phase0s/Install-Phase0S.ps1','scripts/phase0s/Restore-Phase0S.ps1','scripts/phase0s/Phase0S.ScriptSupport.ps1','scripts/workload/Workload.Support.ps1','scripts/workload/Workload.Evidence.ps1','scripts/test-workload-regressions.ps1','tests/NativeWorldTextProbe/NativeChecks.cs','environment:runtime')) {
    $old = [pscustomobject]@{inputs=@($path+':A')}; $new = [pscustomobject]@{inputs=@($path+':B')}
    Assert-Evidence ((Get-WorkloadCheckFingerprint $old 'native-FishingCpu') -cne (Get-WorkloadCheckFingerprint $new 'native-FishingCpu')) ('shared inputs remain strict: ' + $path)
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
    Add-Type -TypeDefinition 'using System; public static class WorkloadStderrReceiver { public static int Main(string[] args) { Console.Error.WriteLine("ordinary native information"); return int.Parse(args[0]); } }' -OutputAssembly $stderrExe -OutputType ConsoleApplication
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
    Write-Output 'PASS: evidence identity, changed bytes/options, missing/failed/partial/feedback receipts, old schema, stderr capture, real nonzero exit and launch failure.'
} finally {
    $resolved=[IO.Path]::GetFullPath($fixture);$temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if (-not $resolved.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($resolved).StartsWith('JueMingR-evidence-',[StringComparison]::Ordinal)) {throw 'Unsafe fixture cleanup.'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
