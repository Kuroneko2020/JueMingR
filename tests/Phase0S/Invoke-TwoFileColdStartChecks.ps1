[CmdletBinding()]
param([switch]$Run,[string]$RepositoryRoot,[string]$NativeContentDirectory)
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2
if(-not $Run){throw 'Use -Run for the isolated real CLR mechanism check.'}
if(-not $RepositoryRoot){$RepositoryRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))}
. (Join-Path $RepositoryRoot 'scripts/phase0s/Phase0S.ScriptSupport.ps1')
. (Join-Path $RepositoryRoot 'scripts/TwoFileBundle.Support.ps1')
$source=(& git -C $RepositoryRoot rev-parse HEAD).Trim()
$root=Join-Path $RepositoryRoot ('artifacts/two-file-cold-start/'+[DateTime]::UtcNow.ToString('yyyyMMddTHHmmss')+'-'+[Guid]::NewGuid().ToString('N'))
$build=Join-Path $root 'build'
$logs=Join-Path $root 'logs';[IO.Directory]::CreateDirectory($logs)|Out-Null
$scenarioRoot=Join-Path ([IO.Path]::GetTempPath()) ('JmrCold-'+[Guid]::NewGuid().ToString('N').Substring(0,12))
[IO.Directory]::CreateDirectory($scenarioRoot)|Out-Null
foreach($project in @('src/JueMingR.TerrariaHost/JueMingR.TerrariaHost.csproj','tests/TwoFileColdStart/TwoFileColdStart.csproj')){
    & dotnet.exe build (Join-Path $RepositoryRoot $project) --configuration Release --nologo -p:Platform=x86 "-p:SourceRevisionId=$source" "-p:JueMingRBuildRoot=$build"
    if($LASTEXITCODE -ne 0){throw 'Cold-start component build failed.'}
}
$internal=Join-Path $root 'internal';[IO.Directory]::CreateDirectory($internal)|Out-Null
foreach($project in @('JueMingR.Platform','JueMingR.Features','JueMingR.Infrastructure','JueMingR.TerrariaHost','JueMingR.PredictionWorker')){
    $name=$project+$(if($project -eq 'JueMingR.PredictionWorker'){'.exe'}else{'.dll'})
    [IO.File]::Copy((Join-Path $build ('bin/'+$project+'/x86/Release/net472/'+$name)),(Join-Path $internal $name))
    if($name.EndsWith('.exe')){[IO.File]::Copy((Join-Path $build ('bin/'+$project+'/x86/Release/net472/'+$name+'.config')),(Join-Path $internal ($name+'.config')))}
}
[IO.File]::Copy((Join-Path $RepositoryRoot 'external/Harmony/0Harmony.dll'),(Join-Path $internal '0Harmony.dll'))
[IO.File]::Copy((Join-Path $RepositoryRoot 'external/Harmony/LICENSE'),(Join-Path $internal 'Harmony-2.4.2-LICENSE.txt'))
[IO.File]::Copy((Join-Path $RepositoryRoot 'THIRD-PARTY-NOTICES.md'),(Join-Path $internal 'THIRD-PARTY-NOTICES.md'))
Write-JmrRuntimeManifest -Internal $internal -SourceCommit $source -PackageId ('cold-start-'+$source)
$standard=Join-Path $root 'standard'
$bundle=New-JmrTwoFileBundle -RepositoryRoot $RepositoryRoot -InputDirectory $internal -WorkDirectory (Join-Path $root 'bundle') -StandardDirectory $standard -SourceCommit $source
$poisonSource=Join-Path $root 'poison.cs'
[IO.File]::WriteAllText($poisonSource,'[assembly:System.Reflection.AssemblyVersion("1.0.0.0")] public class ForeignHarmonyMarker {}')
$poison=Join-Path $root '0Harmony.dll'
& (Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe') /nologo /target:library "/out:$poison" $poisonSource
if($LASTEXITCODE -ne 0){throw 'The conflicting same-simple-name test assembly did not compile.'}
function New-ColdRoot([string]$Name){
    $directory=Join-Path $scenarioRoot $Name;[IO.Directory]::CreateDirectory($directory)|Out-Null
    foreach($file in @('JueMingR.dll','Terraria.exe.config')){[IO.File]::Copy((Join-Path $standard $file),(Join-Path $directory $file))}
    [IO.File]::Copy((Join-Path $standard 'Terraria.exe.config'),(Join-Path $directory 'ColdStartDriver.exe.config'))
    [IO.File]::Copy((Join-Path $RepositoryRoot 'external/TerrariaRefs/Terraria.exe'),(Join-Path $directory 'Terraria.exe'))
    [IO.File]::Copy((Join-Path $build 'bin/TwoFileColdStart/x86/Release/net472/ColdStartDriver.exe'),(Join-Path $directory 'ColdStartDriver.exe'))
    [IO.File]::Copy($poison,(Join-Path $directory '0Harmony.dll'))
    if(Test-Path -LiteralPath (Join-Path $directory 'JueMingR.Runtime')){throw 'A cold scenario already contains the private runtime.'}
    return $directory
}
function Invoke-ColdDriver([string]$Directory,[string]$Name,[string]$Argument,[string]$ExpectedFailure=''){
    $stdout=Join-Path $logs ($Name+'.stdout.txt');$stderr=Join-Path $logs ($Name+'.stderr.txt')
    $process=Start-Process -FilePath (Join-Path $Directory 'ColdStartDriver.exe') -ArgumentList $Argument -WorkingDirectory $Directory -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
    $null=$process.Handle
    if(-not $process.WaitForExit(150000)){$process.Kill();throw 'The owned cold-start test process exceeded its deadline.'}
    $process.WaitForExit()
    $process.Refresh()
    Get-Content -LiteralPath $stdout -Encoding UTF8|Write-Host
    if($ExpectedFailure){
        if($process.ExitCode -eq 0){throw 'An invalid pair unexpectedly activated.'}
        $errorPath=Join-Path $Directory 'JueMingRData/logs/bootstrap-error.txt'
        if(-not (Test-Path -LiteralPath $errorPath) -or [IO.File]::ReadAllText($errorPath).IndexOf($ExpectedFailure,[StringComparison]::Ordinal) -lt 0){throw 'Preparation failure did not retain its actual bounded reason.'}
        Write-Host ('PASS bounded preparation failure: '+[IO.File]::ReadAllText($errorPath))
    }elseif($process.ExitCode -ne 0){Get-Content -LiteralPath $stderr -Encoding UTF8|Write-Host;throw ('Cold-start scenario failed: '+$Name)}
}
$mismatch=New-ColdRoot 'mismatch'
[IO.File]::AppendAllText((Join-Path $mismatch 'ColdStartDriver.exe.config'),"`n<!-- valid XML, wrong pair bytes -->`n")
Invoke-ColdDriver $mismatch 'mismatched-pair' '--binding-only' 'different payloads'
$blocked=New-ColdRoot 'blocked'
[IO.File]::WriteAllText((Join-Path $blocked 'JueMingR.Runtime'),'owned test obstruction')
Invoke-ColdDriver $blocked 'blocked-preparation' '--binding-only' 'bundle directory is a file'
$cold=New-ColdRoot 'cold'
Invoke-ColdDriver $cold 'cold-worker' '--worker'
$payload=Join-Path $cold ('JueMingR.Runtime/'+$bundle.payloadId)
$before=@(foreach($member in $bundle.members){(Get-Item -LiteralPath (Join-Path $payload $member.path)).LastWriteTimeUtc.Ticks})
Invoke-ColdDriver $cold 'valid-cache-reuse' '--binding-only'
$after=@(foreach($member in $bundle.members){(Get-Item -LiteralPath (Join-Path $payload $member.path)).LastWriteTimeUtc.Ticks})
if(($before -join '|') -cne ($after -join '|')){throw 'Valid cache reuse rewrote program members.'}
[IO.File]::WriteAllText((Join-Path $payload 'JueMingR.Platform.dll'),'corrupt current owned member')
Invoke-ColdDriver $cold 'damaged-cache-repair' '--binding-only'
if((Get-FileHash -LiteralPath (Join-Path $payload 'JueMingR.Platform.dll') -Algorithm SHA256).Hash -cne ($bundle.members|Where-Object path -eq 'JueMingR.Platform.dll').sha256){throw 'The container did not repair its current payload.'}
# A second complete pair must select its own directory even with the old cache
# intact. The notice-only variant changes no production gameplay or fixture gate.
[IO.File]::AppendAllText((Join-Path $internal 'THIRD-PARTY-NOTICES.md'),"`nCold-start mechanism test variant B.`n")
$updatedStandard=Join-Path $root 'updated-standard'
$updated=New-JmrTwoFileBundle -RepositoryRoot $RepositoryRoot -InputDirectory $internal -WorkDirectory (Join-Path $root 'updated-bundle') -StandardDirectory $updatedStandard -SourceCommit $source
if($updated.payloadId -ceq $bundle.payloadId){throw 'The independently indexed variant did not change identity.'}
foreach($file in @('JueMingR.dll','Terraria.exe.config')){[IO.File]::Copy((Join-Path $updatedStandard $file),(Join-Path $cold $file),$true)}
[IO.File]::Copy((Join-Path $updatedStandard 'Terraria.exe.config'),(Join-Path $cold 'ColdStartDriver.exe.config'),$true)
Invoke-ColdDriver $cold 'paired-update-selects-current' '--binding-only'
$updatedEvidence=Join-Path $cold ('JueMingR.Runtime/'+$updated.payloadId+'/phase-0-s-evidence.log')
if(-not (Test-Path -LiteralPath $updatedEvidence) -or -not (Test-Path -LiteralPath $payload)){throw 'Paired update failed its exact current-cache selection.'}
if($NativeContentDirectory){
    $native=New-ColdRoot 'native'
    New-Item -ItemType Junction -Path (Join-Path $native 'Content') -Target ([IO.Path]::GetFullPath($NativeContentDirectory))|Out-Null
    $nativeLibrary=Join-Path (Split-Path -Parent ([IO.Path]::GetFullPath($NativeContentDirectory))) 'ReLogic.Native.dll'
    if(-not (Test-Path -LiteralPath $nativeLibrary -PathType Leaf)){throw 'The original native IME dependency is required by original initialization.'}
    [IO.File]::Copy($nativeLibrary,(Join-Path $native 'ReLogic.Native.dll'))
    Invoke-ColdDriver $native 'native-original-update' '--native'
}
[ordered]@{status='PASS';scope='production-clr-cold-bootstrap';sourceCommit=$source;workspaceDirty=([bool](& git -C $RepositoryRoot status --porcelain));payloadId=$bundle.payloadId;evidenceRoot=$root;scenarioRoot=$scenarioRoot;rootSameNameForeignDependency='0Harmony, Version=1.0.0.0';cacheReuse='PASS';cacheRepair='PASS';worker='READY_AND_CLOSED';nativeOriginalUpdate=$(if($NativeContentDirectory){'PASS'}else{'NOT_EXECUTED'});ownerGame='NOT_EXECUTED'}|ConvertTo-Json -Depth 4
