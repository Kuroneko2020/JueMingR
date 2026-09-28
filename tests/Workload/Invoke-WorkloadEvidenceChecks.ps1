[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $root 'scripts/workload/Workload.Support.ps1')
function Assert-Evidence([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw ('Evidence contract: ' + $Message) }
}
# The page-only provider owns arrangement, not the automation execution chain.
$page = Get-WorkloadRoute @('src/JueMingR.TerrariaHost/F5/MiscAutomationPanel.cs')
Assert-Evidence ($page.groups -contains 'pages-host') 'page arrangement must select actual page composition/input checks'
Assert-Evidence ($page.groups -notcontains 'fishing-host' -and $page.groups -notcontains 'storage-host') 'page arrangement must not select unrelated fishing/storage execution'
$shared = Get-WorkloadRoute @('src/JueMingR.Platform/Items/ItemOperationOwnership.cs')
foreach ($group in @('fishing-host','tools-host','quick-items-host','coin-deposit-host','processing-host')) {
    Assert-Evidence ($shared.groups -contains $group) ('shared item provider must include ' + $group)
}
Write-Output 'PASS: local page exclusions and transitive shared item consumers.'
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('JueMingR-evidence-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixture) | Out-Null
try {
    $artifact = Join-Path $fixture 'check.bin'
    [IO.File]::WriteAllText($artifact, 'original')
    $output = [pscustomobject]@{path=$artifact; length=8; sha256=(Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash}
    $evidence = [pscustomobject]@{schemaVersion=1; status='PASS'; name='one'; signature='signature'; inputFingerprint='input';
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
    Write-Output 'PASS: evidence identity, changed bytes/options, missing/failed/partial/feedback receipts, old schema and real nonzero process exit.'
} finally {
    $resolved=[IO.Path]::GetFullPath($fixture);$temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if (-not $resolved.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($resolved).StartsWith('JueMingR-evidence-',[StringComparison]::Ordinal)) {throw 'Unsafe fixture cleanup.'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
