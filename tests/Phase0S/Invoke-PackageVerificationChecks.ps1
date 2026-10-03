[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $repository 'scripts/phase0s/PackageVerification.Support.ps1')
function Assert-PackageCheck([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw ('Package verification: ' + $Message) }
}
$root = Join-Path ([IO.Path]::GetTempPath()) ('JueMingR-package-check-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($root) | Out-Null
try {
    # Tiny inert files test archive identity, not a fake Terraria or product DLL.
    $zipPath = Join-Path $root 'fixture.zip'
    $archive = [IO.Compression.ZipFile]::Open($zipPath, 'Create')
    try {
        foreach ($name in @('fixture/a.txt','fixture/sub/b.txt')) {
            $writer = New-Object IO.StreamWriter($archive.CreateEntry($name).Open())
            try { $writer.Write('proof') } finally { $writer.Dispose() }
        }
    } finally { $archive.Dispose() }
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $hash = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes('proof'))).Replace('-','') } finally { $sha.Dispose() }
    $record = [pscustomobject]@{packageDirectory=[pscustomobject]@{name='fixture';files=@(
        [pscustomobject]@{path='a.txt';length=5;sha256=$hash},
        [pscustomobject]@{path='sub/b.txt';length=5;sha256=$hash})}}
    Assert-PackageCheck ((Test-RecordedPackageArchive $zipPath $record) -eq 2) 'every recorded file is checked'
    $record.packageDirectory.files[0].sha256='0'*64
    $failed=$false; try { $null=Test-RecordedPackageArchive $zipPath $record } catch { $failed=$true }
    Assert-PackageCheck $failed 'changed member bytes fail even with a valid ZIP'
    $record.packageDirectory.files[0].sha256=$hash
    $record.packageDirectory.files[1].path='a.txt'
    $failed=$false; try { $null=Test-RecordedPackageArchive $zipPath $record } catch { $failed=$true }
    Assert-PackageCheck $failed 'duplicate record members fail'
    $record.packageDirectory.files[1].path='sub/b.txt'
    foreach ($bad in @('fixture/../escape.txt','fixture/a.txt','other/extra.txt')) {
        $copy=Join-Path $root ([Guid]::NewGuid().ToString('N')+'.zip'); [IO.File]::Copy($zipPath,$copy)
        $archive=[IO.Compression.ZipFile]::Open($copy,'Update')
        try { $null=$archive.CreateEntry($bad) } finally {$archive.Dispose()}
        $failed=$false; try { $null=Test-RecordedPackageArchive $copy $record } catch {$failed=$true}
        Assert-PackageCheck $failed ('unsafe, duplicate or extra member rejected: '+$bad)
    }
    # The public entry must reject the wrong expected hash with a real nonzero
    # process exit, before extraction or any product/build invocation.
    $recordPath=Join-Path $root 'record.json'
    [IO.File]::WriteAllText($recordPath,($record|ConvertTo-Json -Depth 5))
    $output = @(& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repository 'scripts/verify-existing-package.ps1') -ZipPath $zipPath -BuildRecordPath $recordPath -ExpectedSourceCommit ('a'*40) -ExpectedZipSha256 ('0'*64) 2>&1)
    Assert-PackageCheck ($LASTEXITCODE -ne 0 -and ($output|Out-String).Contains('ZIP_IDENTITY_MISMATCH')) 'real public-entry failure propagates'
    # Observe dispatch in a separate PowerShell, without running either expensive
    # suite. A first-suite failure must not hide the independent recovery result.
    $dispatchLog=Join-Path $root 'dispatch.txt'
    $harness=Join-Path $root 'dispatch.ps1'
    $entry=(Join-Path $repository 'scripts/test-phase0s.ps1').Replace("'","''")
    $logLiteral=$dispatchLog.Replace("'","''")
    $harnessText=@'
param([string] $Scope)
function global:powershell.exe {
    $suite=($args | Where-Object { $_ -like '*Invoke-*Tests.ps1' -or $_ -like '*Invoke-LoadChainFixture.ps1' })
    Add-Content -LiteralPath '__LOG__' -Value ([IO.Path]::GetFileName($suite))
    $global:LASTEXITCODE=0
    if ($suite -like '*Invoke-LoadChainFixture.ps1') {$global:LASTEXITCODE=13}
}
& '__ENTRY__' -Scope $Scope
exit $LASTEXITCODE
'@
    [IO.File]::WriteAllText($harness,$harnessText.Replace('__LOG__',$logLiteral).Replace('__ENTRY__',$entry))
    # Windows PowerShell turns redirected native stderr into ErrorRecords;
    # the deliberately failed child must be judged by its exit, not stop us.
    $savedPreference=$ErrorActionPreference
    try {
        $ErrorActionPreference='Continue'
        $null=@(& powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $harness -Scope AllSynthetic 2>&1)
        $aggregateExit=$LASTEXITCODE
    } finally {$ErrorActionPreference=$savedPreference}
    Assert-PackageCheck ($aggregateExit -ne 0) 'aggregate preserves failed synthetic result'
    $calls=@(Get-Content -LiteralPath $dispatchLog)
    Assert-PackageCheck ($calls.Count -eq 2 -and $calls[0] -ceq 'Invoke-LoadChainFixture.ps1' -and $calls[1] -ceq 'Invoke-InstallRecoveryTests.ps1') 'recovery still dispatched after synthetic failure'
    [IO.File]::WriteAllText($dispatchLog,'')
    $null=@(& powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $harness -Scope installrecovery 2>&1)
    $calls=@(Get-Content -LiteralPath $dispatchLog)
    Assert-PackageCheck ($LASTEXITCODE -eq 0 -and $calls.Count -eq 1 -and $calls[0] -ceq 'Invoke-InstallRecoveryTests.ps1') 'recovery can run without legacy synthetic precondition'
    $savedPreference=$ErrorActionPreference
    try {
        $ErrorActionPreference='Continue'
        $output=@(& powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $repository 'scripts/test-phase0s.ps1') -Scope packageintegrity -ZipPath $zipPath -BuildRecordPath $recordPath -ExpectedSourceCommit ('a'*40) -ExpectedZipSha256 ('0'*64) 2>&1)
        $scopeExit=$LASTEXITCODE
    } finally {$ErrorActionPreference=$savedPreference}
    Assert-PackageCheck ($scopeExit -ne 0 -and ($output|Out-String).Contains('ZIP_IDENTITY_MISMATCH')) 'lowercase package scope reaches identity verifier'
    Write-Output 'PASS: archive members, content, duplicate/traversal/extra rejection, public nonzero exit and independent suite dispatch. No product or game executed.'
} finally {
    $absolute=[IO.Path]::GetFullPath($root); $temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if (-not $absolute.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($absolute).StartsWith('JueMingR-package-check-')) {throw 'Unsafe fixture cleanup.'}
    Remove-Item -LiteralPath $absolute -Recurse -Force
}
