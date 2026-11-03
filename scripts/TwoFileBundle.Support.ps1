# One BCL-only container build, consumed by the normal package entry and the
# isolated CLR mechanism check. InputDirectory contains complete internal bytes.
function New-JmrTwoFileBundle {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)][string]$RepositoryRoot,
          [Parameter(Mandatory=$true)][string]$InputDirectory,
          [Parameter(Mandatory=$true)][string]$WorkDirectory,
          [Parameter(Mandatory=$true)][string]$StandardDirectory,
          [Parameter(Mandatory=$true)][string]$SourceCommit,
          [ValidateSet('Release','Debug')][string]$Configuration='Release')
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $names=@('0Harmony.dll','Harmony-2.4.2-LICENSE.txt','JueMingR.Features.dll','JueMingR.Infrastructure.dll','JueMingR.Platform.dll','JueMingR.PredictionWorker.exe','JueMingR.PredictionWorker.exe.config','JueMingR.TerrariaHost.dll','THIRD-PARTY-NOTICES.md','phase-0-s-runtime.manifest')
    $utf8=New-Object Text.UTF8Encoding($false,$true)
    [IO.Directory]::CreateDirectory($WorkDirectory)|Out-Null
    [IO.Directory]::CreateDirectory($StandardDirectory)|Out-Null
    if(@(Get-ChildItem -LiteralPath $StandardDirectory -Force).Count -ne 0){throw 'The standard two-file directory must be empty.'}
    $indexText="schemaVersion=1`n"
    $members=@(foreach($name in $names){
        $path=Join-Path $InputDirectory $name
        $item=Get-Item -LiteralPath $path -Force
        if($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'A bundle input is not an ordinary file.'}
        $hash=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        $indexText += $name+'|'+$item.Length.ToString([Globalization.CultureInfo]::InvariantCulture)+'|'+$hash+"`n"
        [pscustomobject][ordered]@{path=$name;length=[long]$item.Length;sha256=$hash}
    })
    $indexPath=Join-Path $WorkDirectory 'bundle.index'
    [IO.File]::WriteAllText($indexPath,$indexText,$utf8)
    $payloadId=(Get-FileHash -LiteralPath $indexPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $relative='JueMingR.Runtime/'+$payloadId
    $entryIdentity='JueMingR, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null'
    $bindingLines=@(foreach($name in @('0Harmony.dll','JueMingR.Features.dll','JueMingR.Infrastructure.dll','JueMingR.Platform.dll','JueMingR.TerrariaHost.dll')){
        $identity=[Reflection.AssemblyName]::GetAssemblyName((Join-Path $InputDirectory $name))
        '      <dependentAssembly><assemblyIdentity name="'+$identity.Name+'" culture="neutral" publicKeyToken="null" /><codeBase version="'+$identity.Version+'" href="'+$relative+'/'+$name+'" /></dependentAssembly>'
    })
    $config=@('<?xml version="1.0" encoding="utf-8"?>','<configuration>','  <runtime>',('    <appDomainManagerAssembly value="'+$entryIdentity+'" />'),'    <appDomainManagerType value="JueMingR.Bootstrap.Phase0SAppDomainManager" />','    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">')+$bindingLines+@('    </assemblyBinding>','  </runtime>','</configuration>')
    [IO.File]::WriteAllText((Join-Path $WorkDirectory 'Terraria.exe.config'),(($config -join "`n")+"`n"),$utf8)
    $payloadPath=Join-Path $WorkDirectory 'payload.zip'
    $stream=[IO.File]::Open($payloadPath,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try{
        $zip=New-Object IO.Compression.ZipArchive($stream,[IO.Compression.ZipArchiveMode]::Create,$true)
        try{foreach($name in $names){
            $entry=$zip.CreateEntry($name,[IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime=[DateTimeOffset]::new(2000,1,1,0,0,0,[TimeSpan]::Zero)
            $source=[IO.File]::OpenRead((Join-Path $InputDirectory $name));$destination=$entry.Open()
            try{$source.CopyTo($destination)}finally{$destination.Dispose();$source.Dispose()}
        }}finally{$zip.Dispose()}
    }finally{$stream.Dispose()}
    $buildRoot=Join-Path $WorkDirectory 'container-build'
    $build=@(& dotnet.exe build (Join-Path $RepositoryRoot 'src/JueMingR.Bootstrap/JueMingR.Bootstrap.csproj') --configuration $Configuration --nologo -p:Platform=x86 "-p:SourceRevisionId=$SourceCommit" "-p:JueMingRBundleDirectory=$WorkDirectory" "-p:JueMingRBuildRoot=$buildRoot" 2>&1)
    $build|ForEach-Object{Write-Host $_}
    if($LASTEXITCODE -ne 0){throw 'The final bundle container did not build.'}
    $entryOutput=Join-Path $buildRoot ('bin/JueMingR.Bootstrap/x86/'+$Configuration+'/net472/JueMingR.dll')
    if([Reflection.AssemblyName]::GetAssemblyName($entryOutput).FullName -cne $entryIdentity){throw 'The actual entry identity differs from its generated configuration.'}
    [IO.File]::Copy($entryOutput,(Join-Path $StandardDirectory 'JueMingR.dll'))
    [IO.File]::Copy((Join-Path $WorkDirectory 'Terraria.exe.config'),(Join-Path $StandardDirectory 'Terraria.exe.config'))
    $files=@(foreach($name in @('JueMingR.dll','Terraria.exe.config')){
        $path=Join-Path $StandardDirectory $name
        [pscustomobject][ordered]@{path=$name;length=[long](Get-Item -LiteralPath $path).Length;sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
    })
    return [pscustomobject][ordered]@{payloadId=$payloadId;indexResourceName='JueMingR.Bundle.index';containerResourceName='JueMingR.Bundle.payload.zip';configResourceName='JueMingR.Bundle.config';bootstrapAssemblyName=$entryIdentity;files=$files;members=$members}
}

function New-JmrTwoFileZip {
    param([string]$StandardDirectory,[string]$ZipPath)
    $stream=[IO.File]::Open($ZipPath,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try{
        $archive=New-Object IO.Compression.ZipArchive($stream,[IO.Compression.ZipArchiveMode]::Create,$true)
        try{foreach($name in @('JueMingR.dll','Terraria.exe.config')){
            $entry=$archive.CreateEntry($name,[IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime=[DateTimeOffset]::new(2000,1,1,0,0,0,[TimeSpan]::Zero)
            $source=[IO.File]::OpenRead((Join-Path $StandardDirectory $name));$destination=$entry.Open()
            try{$source.CopyTo($destination)}finally{$destination.Dispose();$source.Dispose()}
        }}finally{$archive.Dispose()}
    }finally{$stream.Dispose()}
}

function Write-JmrRuntimeManifest {
    param([string]$Internal,[string]$SourceCommit,[string]$PackageId,$HostIdentity,$HarmonyIdentity)
    if($null -eq $HostIdentity){$HostIdentity=Get-Phase0SAssemblyFileIdentity (Join-Path $Internal 'JueMingR.TerrariaHost.dll')}
    if($null -eq $HarmonyIdentity){$HarmonyIdentity=Get-Phase0SAssemblyFileIdentity (Join-Path $Internal '0Harmony.dll')}
    # The normal builder already inspected the exact source files and verified
    # every copied byte. Framework reflection-only binding forbids loading that
    # same identity from the second path; reuse its MVID only after exact hash
    # and file-name identity verification, never a simple-name cache lookup.
    foreach($pair in @(@('JueMingR.TerrariaHost.dll',$HostIdentity),@('0Harmony.dll',$HarmonyIdentity))){
        $path=Join-Path $Internal $pair[0];$identity=$pair[1]
        if((Get-Phase0SFileSha256 $path) -cne $identity.sha256 -or [Reflection.AssemblyName]::GetAssemblyName($path).FullName -cne $identity.fullName){throw 'Copied runtime bytes differ from their previously inspected identity.'}
    }
$runtimeLines=@(
    'schemaVersion=2',('packageId='+$packageId),('sourceCommit='+$sourceCommit),
    'targetAssemblySimpleName=Terraria','targetAssemblyVersion=1.4.5.8',
    'targetAssemblyMvid=2c29f6c3-4bd9-4add-9c58-da159804e083',
    'targetAssemblySha256=960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3',
    'reLogicAssemblySimpleName=ReLogic','reLogicAssemblyVersion=1.0.0.0','reLogicAssemblyPublicKeyToken=null',
    'reLogicAssemblyMvid=ee258be9-88a4-423d-b3ce-84b6c35b141a','reLogicResourceName=Terraria.Libraries.ReLogic.ReLogic.dll',
    'reLogicResourceSha256=E1C5DCCEFFF5FD1C789FF712BABFA1A305FCED0D03C96EF30F2C14D99AA0AF29',
    'targetTypeName=Terraria.Main','targetMethodName=Update','targetMethodMetadataToken=0x06000D16',
    'targetMethodIsStatic=false','targetMethodReturnType=System.Void','targetMethodParameterCount=1',
    'targetMethodParameterType=Microsoft.Xna.Framework.GameTime','hostAssemblySimpleName=JueMingR.TerrariaHost',
    'hostAssemblyVersion=0.0.0.0',('hostAssemblyMvid='+$hostIdentity.mvid),('hostAssemblySha256='+$hostIdentity.sha256),
    'harmonyAssemblySimpleName=0Harmony','harmonyAssemblyVersion=2.4.2.0',
    ('harmonyAssemblyMvid='+$harmonyIdentity.mvid),('harmonyAssemblySha256='+$harmonyIdentity.sha256),
    'patchOwner=JueMingR.Phase0S.MainUpdate','evidenceFileName=phase-0-s-evidence.log')
[IO.File]::WriteAllText((Join-Path $internal 'phase-0-s-runtime.manifest'),(($runtimeLines -join "`n")+"`n"),(New-Object Text.UTF8Encoding($false)))
Read-Phase0SRuntimeManifest (Join-Path $internal 'phase-0-s-runtime.manifest')|Out-Null
}
