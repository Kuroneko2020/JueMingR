[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$StandardDirectory,
      [Parameter(Mandatory=$true)][string]$SetupExecutable)
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2.0
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $repository 'scripts/phase0s/PackageVerification.Support.ps1')
$root=Join-Path ([IO.Path]::GetTempPath()) ('JueMingR-pair-consumer-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($root)|Out-Null
$count=0
try {
    $pair=Join-Path $root 'pair';[IO.Directory]::CreateDirectory($pair)|Out-Null
    foreach($name in @('JueMingR.dll','Terraria.exe.config')){[IO.File]::Copy((Join-Path $StandardDirectory $name),(Join-Path $pair $name))}
    $identity=Read-TwoFilePackage -PackageRoot $pair
    $files=@(foreach($name in @('JueMingR.dll','Terraria.exe.config')){$path=Join-Path $pair $name;[pscustomobject]@{path=$name;length=(Get-Item $path).Length;sha256=(Get-FileHash $path).Hash}})
    function New-ConsumerRecord([string]$Setup){
        return [pscustomobject]@{distributionFormat='two-file-1';packageId=$identity.packageId;sourceCommit=$identity.sourceCommit;
            packageDirectory=[pscustomobject]@{name='';files=$files};bundle=[pscustomobject]@{payloadId=$identity.payloadId;bootstrapAssemblyName=$identity.assemblyName;files=$files};
            setup=[pscustomobject]@{length=(Get-Item $Setup).Length;sha256=(Get-FileHash $Setup).Hash;resources=@($files|ForEach-Object{[pscustomobject]@{name=('JueMingR.Setup.Payload.'+$_.path);path=$_.path;length=$_.length;sha256=$_.sha256}})}}
    }
    $record=New-ConsumerRecord $SetupExecutable
    $null=Read-TwoFilePackage -PackageRoot $pair -Record $record -SetupPath $SetupExecutable;$count++
    [IO.File]::AppendAllText((Join-Path $pair 'Terraria.exe.config'),' ')
    $failed=$false;try{$null=Read-TwoFilePackage -PackageRoot $pair}catch{$failed=$_.Exception.Message -like '*pair mismatch*'}
    if(-not $failed){throw 'Mismatched DLL/config was accepted.'};$count++
    [IO.File]::Copy((Join-Path $StandardDirectory 'Terraria.exe.config'),(Join-Path $pair 'Terraria.exe.config'),$true)
    $record.bundle.files=@($files[0],$files[0])
    $failed=$false;try{$null=Read-TwoFilePackage -PackageRoot $pair -Record $record -SetupPath $SetupExecutable}catch{$failed=$true}
    if(-not $failed){throw 'Duplicated channel identity was accepted.'};$count++

    # A tiny inert EXE supplies deliberately different embedded config bytes.
    # It is never executed; a fresh metadata-only reader avoids assembly cache
    # identity reuse and proves actual resource mismatch rather than log text.
    $source=Join-Path $root 'fixture.cs';[IO.File]::WriteAllText($source,'internal static class Entry { static void Main() {} }')
    $wrongConfig=Join-Path $root 'wrong.config';[IO.File]::WriteAllText($wrongConfig,'different new config')
    $wrongSetup=Join-Path $root 'JueMingR.Setup.exe'
    $compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe'
    $null=& $compiler /nologo /target:winexe /platform:x86 ('/out:'+$wrongSetup) ('/resource:'+(Join-Path $pair 'JueMingR.dll')+',JueMingR.Setup.Payload.JueMingR.dll') ('/resource:'+$wrongConfig+',JueMingR.Setup.Payload.Terraria.exe.config') $source
    if($LASTEXITCODE -ne 0){throw 'Cannot build inert resource counterexample.'}
    $wrongRecord=New-ConsumerRecord $wrongSetup;$recordPath=Join-Path $root 'record.json'
    [IO.File]::WriteAllText($recordPath,($wrongRecord|ConvertTo-Json -Depth 8))
    $child=Join-Path $root 'reader.ps1'
    [IO.File]::WriteAllText($child,@'
param($Support,$Pair,$Record,$Setup)
$ErrorActionPreference='Stop';Set-StrictMode -Version 2.0
. $Support
try {$null=Read-TwoFilePackage $Pair (Get-Content $Record -Raw|ConvertFrom-Json) $Setup;exit 9}
catch {if($_.Exception.Message -like '*different bytes*'){Write-Output 'EXPECTED_SETUP_MISMATCH';exit 0};throw}
'@)
    $output=@(& powershell.exe -NoProfile -NonInteractive -File $child -Support (Join-Path $repository 'scripts/phase0s/PackageVerification.Support.ps1') -Pair $pair -Record $recordPath -Setup $wrongSetup)
    if($LASTEXITCODE -ne 0 -or $output -notcontains 'EXPECTED_SETUP_MISMATCH'){throw 'Different embedded Setup config was not rejected.'};$count++
    [ordered]@{status='PASS';checks=$count;scope='real-container-pair-and-standalone-setup-resources';payloadId=$identity.payloadId;gameExecuted=$false;installation='NOT_EXECUTED'}|ConvertTo-Json -Compress
} finally {
    $absolute=[IO.Path]::GetFullPath($root);$temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if(-not $absolute.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($absolute).StartsWith('JueMingR-pair-consumer-')){throw 'Unsafe consumer fixture cleanup.'}
    Remove-Item -LiteralPath $absolute -Recurse -Force
}
