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
    # Execute the real specialist launcher against inert recording subprocesses.
    # This proves dispatch only; actual NpcSync behavior is tested separately.
    $mini=Join-Path $root 'specialist';$scripts=Join-Path $mini 'scripts'
    [IO.Directory]::CreateDirectory((Join-Path $scripts 'workload'))|Out-Null
    [IO.Directory]::CreateDirectory((Join-Path $mini 'artifacts/build/Debug'))|Out-Null
    $content=Join-Path $mini 'content';[IO.Directory]::CreateDirectory((Join-Path $content 'Fonts'))|Out-Null
    [IO.File]::WriteAllText((Join-Path $content 'Fonts/Mouse_Text.xnb'),'inert dispatcher input')
    [IO.File]::WriteAllText((Join-Path $mini 'artifacts/build/Debug/build-record.json'),'{}')
    Copy-Item -LiteralPath (Join-Path $repository 'scripts/test-world-object-text.ps1') -Destination (Join-Path $scripts 'test-world-object-text.ps1')
    $recorder=Join-Path $mini 'recorder.exe'
    Add-Type -TypeDefinition @'
using System;using System.IO;class SpecialistRecorder{
 static void Main(string[] args){Directory.CreateDirectory(args[2]);File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"scope.txt"),args[3]+Environment.NewLine);}
}
'@ -OutputAssembly $recorder -OutputType ConsoleApplication
    [IO.File]::WriteAllText((Join-Path $scripts 'build.ps1'),@'
param([string]$Configuration,[switch]$PrepareOnly,[string]$ApprovedRetainedAssets)
if(-not $PrepareOnly -or $Configuration -cne 'Debug'){throw 'unexpected build mode'}
Add-Content (Join-Path $PSScriptRoot '../build-calls.txt') 'Debug PrepareOnly'
$global:LASTEXITCODE=0
'@)
    [IO.File]::WriteAllText((Join-Path $scripts 'workload/Workload.Support.ps1'),@'
function Initialize-WorkloadWorkspace {param($Root,$Path);return $null}
function Get-WorkloadIdentity {param($Root);return @{fingerprint='dispatch-only'}}
function Get-WorkloadEvidenceInput {param($Root,$Identity);return @{inputs=@()}}
function Ensure-WorkloadFixture {param($Root,$Project,$Inputs);return (Join-Path $Root 'recorder.exe')}
function global:git {$global:LASTEXITCODE=0}
'@)
    $null=@(& powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $scripts 'test-world-object-text.ps1') -ContentDirectory $content -OutputDirectory (Join-Path $mini 'out') -Scope NpcSync 2>&1)
    Assert-PackageCheck ($LASTEXITCODE -eq 0) 'real specialist launcher runs recorded one-scope child'
    $scopes=@(Get-Content (Join-Path $mini 'scope.txt'));$builds=@(Get-Content (Join-Path $mini 'build-calls.txt'))
    Assert-PackageCheck ($scopes.Count -eq 1 -and $scopes[0] -ceq 'NpcSync' -and $builds.Count -eq 1) 'one preparation and one declared scope, no full matrix or duplicate scope'
    $savedPreference=$ErrorActionPreference
    try{
        $ErrorActionPreference='Continue'
        $null=@(& powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $scripts 'test-world-object-text.ps1') -ContentDirectory $content -OutputDirectory (Join-Path $mini 'invalid') -Scope NotKnown 2>&1)
        $unknownExit=$LASTEXITCODE
    }finally{$ErrorActionPreference=$savedPreference}
    Assert-PackageCheck ($unknownExit -ne 0 -and @(Get-Content (Join-Path $mini 'scope.txt')).Count -eq 1 -and @(Get-Content (Join-Path $mini 'build-calls.txt')).Count -eq 1) 'unknown specialist scope fails before subprocess dispatch'
    # Real build script -> recording MSBuild/structure subprocess -> recording
    # runner, in a miniature owned Git tree. No product sources or fake game ABI.
    $buildMini=Join-Path $root 'build-dispatch';$buildScripts=Join-Path $buildMini 'scripts'
    [IO.Directory]::CreateDirectory((Join-Path $buildScripts 'workload'))|Out-Null
    [IO.Directory]::CreateDirectory((Join-Path $buildMini 'eng'))|Out-Null
    [IO.Directory]::CreateDirectory((Join-Path $buildMini 'external'))|Out-Null
    foreach($name in @('build.ps1')){Copy-Item (Join-Path $repository ('scripts/'+$name)) (Join-Path $buildScripts $name)}
    foreach($file in Get-ChildItem (Join-Path $repository 'scripts/workload') -File){Copy-Item $file.FullName (Join-Path $buildScripts ('workload/'+$file.Name))}
    foreach($name in @('TerrariaReferences.baseline.json','Harmony.baseline.json')){Copy-Item (Join-Path $repository ('eng/'+$name)) (Join-Path $buildMini ('eng/'+$name))}
    foreach($name in @('TerrariaRefs','Harmony')){New-Item -ItemType Junction -Path (Join-Path $buildMini ('external/'+$name)) -Target (Join-Path $repository ('external/'+$name))|Out-Null}
    [IO.File]::WriteAllText((Join-Path $buildMini 'JueMingR.sln'),'recorded compile input')
    [IO.File]::WriteAllText((Join-Path $buildMini '.gitignore'),"artifacts/`n")
    foreach($name in @('prepare-terraria-references.ps1','prepare-harmony.ps1')){[IO.File]::WriteAllText((Join-Path $buildScripts $name),'param([switch]$VerifyOnly)')}
    $buildRecorder=Join-Path $buildMini 'build-recorder.exe'
    Add-Type -TypeDefinition @'
using System;using System.IO;class BuildDispatchRecorder{
 static void Main(string[] args){if(args[0]=="--version"){Console.WriteLine("10.0.203");return;}
 if(args[0]=="build"){string root=null;foreach(string a in args)if(a.StartsWith("-p:JueMingRBuildRoot="))root=a.Substring(21);
 string dir=Path.Combine(root,"bin/JueMingR.ArchitectureTests/x86/Debug/net472");Directory.CreateDirectory(dir);
 File.Copy(System.Reflection.Assembly.GetExecutingAssembly().Location,Path.Combine(dir,"JueMingR.ArchitectureTests.exe"));}
 Console.WriteLine("recorded build/structure dispatch only");}
}
'@ -OutputAssembly $buildRecorder -OutputType ConsoleApplication
    [IO.File]::WriteAllText((Join-Path $buildScripts 'test-workload-regressions.ps1'),@'
param($Baseline,$Mode,[switch]$Rerun,$ApplicabilityRecord,$ApprovedRetainedAssets)
if(-not $ApprovedRetainedAssets){throw 'retained declaration was not forwarded'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$record=Get-Content (Join-Path $root 'artifacts/build/Debug/build-record.json') -Raw|ConvertFrom-Json
if($record.clean -ne $false -or $record.workspace.releaseEligible -ne $false){throw 'restricted identity lost'}
[IO.File]::WriteAllText((Join-Path $root 'artifacts/build/runner-argument.txt'),$ApprovedRetainedAssets)
[pscustomobject]@{status='FEEDBACK';checkCount=0;productChecks='NOT_EXECUTED';deliveryEligible=$false}
'@)
    & git -C $buildMini init -q
    & git -C $buildMini -c user.name=Fixture -c user.email=fixture@example.invalid commit --allow-empty -m fixture -q
    $note=Join-Path $buildMini 'note.txt';[IO.File]::WriteAllText($note,'self-owned retained asset')
    $assets=Join-Path $root 'build-assets.json'
    [ordered]@{schema='retained-workspace-assets-1';authorization='owned dispatch fixture';files=@([ordered]@{path='note.txt';length=(Get-Item $note).Length;sha256=(Get-FileHash $note).Hash;tracked=$false})}|ConvertTo-Json -Depth 5|Set-Content $assets -Encoding UTF8
    $buildHarness=Join-Path $root 'build-harness.ps1'
    [IO.File]::WriteAllText($buildHarness,@'
param($Root,$Assets)
function global:Get-Command { [CmdletBinding()]param([string]$Name)
 if($Name -ceq 'dotnet.exe'){return [pscustomobject]@{Source=(Join-Path $Root 'build-recorder.exe')}}
 return Microsoft.PowerShell.Core\Get-Command $Name -ErrorAction Stop
}
& (Join-Path $Root 'scripts/build.ps1') -Configuration Debug -WorkloadMode Feedback -WorkloadBaseline HEAD -ApprovedRetainedAssets $Assets
'@)
    $output=@(& powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $buildHarness -Root $buildMini -Assets $assets 2>&1)
    Assert-PackageCheck ($LASTEXITCODE -eq 0 -and [IO.File]::ReadAllText((Join-Path $buildMini 'artifacts/build/runner-argument.txt')) -ceq $assets) ('formal restricted build forwards exact declaration to runner: '+($output|Out-String))
    Write-Output 'PASS: archive identity, public failure, independent suite and specialist one-scope dispatch. No product or game executed.'
} finally {
    $absolute=[IO.Path]::GetFullPath($root); $temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if (-not $absolute.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($absolute).StartsWith('JueMingR-package-check-')) {throw 'Unsafe fixture cleanup.'}
    foreach($name in @('TerrariaRefs','Harmony')){
        $link=Join-Path $absolute ('build-dispatch/external/'+$name)
        if([IO.Directory]::Exists($link)){
            if(-not $link.StartsWith($absolute+'\',[StringComparison]::OrdinalIgnoreCase) -or -not ((Get-Item $link -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Unexpected fixture link boundary.'}
            [IO.Directory]::Delete($link,$false)
        }
    }
    Remove-Item -LiteralPath $absolute -Recurse -Force
}
