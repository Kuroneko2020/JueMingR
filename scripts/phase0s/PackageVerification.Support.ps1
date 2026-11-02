# Standalone archive checks; no build, installer, game or package script execution.
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
function Assert-PackageMemberPath([string] $Path) {
    if ([string]::IsNullOrWhiteSpace($Path) -or $Path -match '[\\:]|^/|//|(^|/)\.\.?(/|$)' -or $Path.EndsWith('/')) {
        throw 'Unsafe package member path.'
    }
}

function Get-PackageBytesHash([byte[]] $Bytes) {
    $sha=[Security.Cryptography.SHA256]::Create()
    try {return [BitConverter]::ToString($sha.ComputeHash($Bytes)).Replace('-','')} finally {$sha.Dispose()}
}
function Read-PackageResource($Assembly,[string] $Name) {
    $stream=$Assembly.GetManifestResourceStream($Name)
    if($null -eq $stream){throw ('Missing embedded package resource: '+$Name)}
    $memory=New-Object IO.MemoryStream
    try {$stream.CopyTo($memory);return ,$memory.ToArray()} finally {$memory.Dispose();$stream.Dispose()}
}
function Read-PackageMetadataAssembly([byte[]] $Bytes) {
    # Framework reflection-only contexts reject loading identical bytes twice.
    # Reuse only this exact content identity, never a simple assembly name.
    if(-not (Get-Variable -Name PackageMetadataCache -Scope Script -ErrorAction SilentlyContinue)){$script:PackageMetadataCache=@{}}
    $hash=Get-PackageBytesHash $Bytes
    if(-not $script:PackageMetadataCache.ContainsKey($hash)){$script:PackageMetadataCache[$hash]=[Reflection.Assembly]::ReflectionOnlyLoad($Bytes)}
    return $script:PackageMetadataCache[$hash]
}
function Read-TwoFilePackage {
    param([string] $PackageRoot,$Record,[string] $SetupPath)
    # Reflection-only metadata/resource reads never execute code supplied by an
    # archive. The standard pair, embedded config and indexed payload must agree.
    $dll=Join-Path $PackageRoot 'JueMingR.dll';$config=Join-Path $PackageRoot 'Terraria.exe.config'
    foreach($path in @($dll,$config)){
        $item=Get-Item -LiteralPath $path -Force -ErrorAction Stop
        if($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Package pair must be ordinary files.'}
    }
    $assembly=Read-PackageMetadataAssembly ([IO.File]::ReadAllBytes($dll))
    if($assembly.GetName().Name -cne 'JueMingR'){throw 'Unexpected container assembly identity.'}
    $index=Read-PackageResource $assembly 'JueMingR.Bundle.index'
    $payloadId=(Get-PackageBytesHash $index).ToLowerInvariant()
    $configBytes=[IO.File]::ReadAllBytes($config)
    if((Get-PackageBytesHash $configBytes) -cne (Get-PackageBytesHash (Read-PackageResource $assembly 'JueMingR.Bundle.config'))){throw 'Container/config pair mismatch.'}
    $xml=New-Object Xml.XmlDocument;$xml.XmlResolver=$null;$xml.LoadXml([Text.Encoding]::UTF8.GetString($configBytes))
    if($xml.configuration.runtime.appDomainManagerAssembly.value -cne $assembly.FullName -or
        $xml.configuration.runtime.appDomainManagerType.value -cne 'JueMingR.Bootstrap.Phase0SAppDomainManager'){throw 'Config entry identity mismatch.'}
    $expected=New-Object 'System.Collections.Generic.Dictionary[string,object]' ([StringComparer]::Ordinal)
    $lines=[Text.Encoding]::UTF8.GetString($index).Split([char]10)
    if($lines[0] -cne 'schemaVersion=1' -or $lines[$lines.Length-1] -cne ''){throw 'Invalid container index schema.'}
    foreach($line in $lines[1..($lines.Length-2)]){
        $fields=$line.Split('|');if($fields.Count -ne 3){throw 'Invalid container index member.'}
        Assert-PackageMemberPath $fields[0]
        if($fields[0].Contains('/') -or $expected.ContainsKey($fields[0]) -or $fields[1] -cnotmatch '^[0-9]+$' -or $fields[2] -cnotmatch '^[0-9A-F]{64}$'){throw 'Unsafe container index member.'}
        $expected.Add($fields[0],[pscustomobject]@{path=$fields[0];length=[long]$fields[1];sha256=$fields[2]})
    }
    foreach($name in @('JueMingR.TerrariaHost.dll','JueMingR.Platform.dll','JueMingR.Features.dll','JueMingR.Infrastructure.dll','0Harmony.dll','JueMingR.PredictionWorker.exe','JueMingR.PredictionWorker.exe.config','phase-0-s-runtime.manifest')){if(-not $expected.ContainsKey($name)){throw ('Container missing required member: '+$name)}}
    if(@($expected.Keys | Where-Object {$_ -match '^(Terraria|ReLogic|Microsoft\.Xna)'}).Count){throw 'Game assemblies must not be bundled.'}
    $requiredNames=@('0Harmony.dll','Harmony-2.4.2-LICENSE.txt','JueMingR.Features.dll','JueMingR.Infrastructure.dll','JueMingR.Platform.dll','JueMingR.PredictionWorker.exe','JueMingR.PredictionWorker.exe.config','JueMingR.TerrariaHost.dll','THIRD-PARTY-NOTICES.md','phase-0-s-runtime.manifest')
    if((@($expected.Keys | Sort-Object) -join '|') -cne (@($requiredNames | Sort-Object) -join '|')){throw 'Container must carry the complete approved internal member set.'}
    $payload=Read-PackageResource $assembly 'JueMingR.Bundle.payload.zip'
    $memory=New-Object IO.MemoryStream(,$payload);$zip=New-Object IO.Compression.ZipArchive($memory,[IO.Compression.ZipArchiveMode]::Read)
    $seen=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    $runtimeText=$null
    try {
        foreach($entry in $zip.Entries){
            if(-not $seen.Add($entry.FullName) -or -not $expected.ContainsKey($entry.FullName) -or $entry.Length -ne $expected[$entry.FullName].length){throw 'Container member set/length mismatch.'}
            $stream=$entry.Open();$bytes=New-Object IO.MemoryStream
            try {
                $stream.CopyTo($bytes);if((Get-PackageBytesHash $bytes.ToArray()) -cne $expected[$entry.FullName].sha256){throw 'Container member hash mismatch.'}
                if($entry.FullName -ceq 'phase-0-s-runtime.manifest'){$runtimeText=[Text.Encoding]::UTF8.GetString($bytes.ToArray())}
            } finally {$bytes.Dispose();$stream.Dispose()}
        }
        if($seen.Count -ne $expected.Count){throw 'Container missing indexed members.'}
    } finally {$zip.Dispose();$memory.Dispose()}
    $ns=New-Object Xml.XmlNamespaceManager($xml.NameTable);$ns.AddNamespace('asm','urn:schemas-microsoft-com:asm.v1')
    $bindings=@($xml.SelectNodes('//asm:codeBase',$ns))
    if($bindings.Count -ne 5){throw 'Config must bind all five private assemblies.'}
    foreach($binding in $bindings){
        if(-not ([string]$binding.href).StartsWith(('JueMingR.Runtime/'+$payloadId+'/'),[StringComparison]::Ordinal)){throw 'Config binding selects a different payload.'}
    }
    $runtime=@{}
    foreach($line in $runtimeText.Split([char]10)){$pair=$line.TrimEnd([char]13).Split(@([char]'='),2);if($pair.Count -eq 2){if($runtime.ContainsKey($pair[0])){throw 'Duplicate runtime identity.'};$runtime[$pair[0]]=$pair[1]}}
    if($runtime.sourceCommit -cnotmatch '^[0-9a-f]{40}$' -or [string]::IsNullOrWhiteSpace($runtime.packageId)){throw 'Runtime source/package identity missing.'}
    if($null -ne $Record){
        if($Record.distributionFormat -cne 'two-file-1' -or $Record.bundle.payloadId -cne $payloadId -or $Record.bundle.bootstrapAssemblyName -cne $assembly.FullName -or
           $Record.sourceCommit -cne $runtime.sourceCommit -or $Record.packageId -cne $runtime.packageId){throw 'Bundle build-record mismatch.'}
        foreach($file in $Record.packageDirectory.files){$bytes=[IO.File]::ReadAllBytes((Join-Path $PackageRoot $file.path));if($bytes.Length -ne $file.length -or (Get-PackageBytesHash $bytes) -cne $file.sha256){throw 'Standard pair record mismatch.'}}
        if(@($Record.bundle.files).Count -ne 2 -or @($Record.setup.resources).Count -ne 2){throw 'Channel member record mismatch.'}
        foreach($file in $Record.packageDirectory.files){
            $bundleEntry=@($Record.bundle.files | Where-Object {$_.path -ceq $file.path})
            $setupEntry=@($Record.setup.resources | Where-Object {$_.path -ceq $file.path})
            if($bundleEntry.Count -ne 1 -or $setupEntry.Count -ne 1 -or $bundleEntry[0].length -ne $file.length -or $bundleEntry[0].sha256 -cne $file.sha256 -or
                $setupEntry[0].length -ne $file.length -or $setupEntry[0].sha256 -cne $file.sha256 -or $setupEntry[0].name -cne ('JueMingR.Setup.Payload.'+$file.path)){throw 'Channel member record differs from standard bytes.'}
        }
        if([string]::IsNullOrWhiteSpace($SetupPath)){throw 'Two-channel verification requires the standalone Setup executable.'}
        $setupItem=Get-Item -LiteralPath $SetupPath -Force
        if($setupItem.PSIsContainer -or ($setupItem.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Setup must be an ordinary file.'}
        $setupBytes=[IO.File]::ReadAllBytes($SetupPath)
        if($setupBytes.Length -ne $Record.setup.length -or (Get-PackageBytesHash $setupBytes) -cne $Record.setup.sha256){throw 'Setup build-record mismatch.'}
        $setup=Read-PackageMetadataAssembly $setupBytes
        foreach($name in @('JueMingR.dll','Terraria.exe.config')){
            if((Get-PackageBytesHash (Read-PackageResource $setup ('JueMingR.Setup.Payload.'+$name))) -cne (Get-PackageBytesHash ([IO.File]::ReadAllBytes((Join-Path $PackageRoot $name))))){throw 'Setup and manual channel carry different bytes.'}
        }
    }
    return [pscustomobject]@{root=$PackageRoot;distributionFormat='two-file-1';packageId=$runtime.packageId;sourceCommit=$runtime.sourceCommit;payloadId=$payloadId;payload=@($expected.Values);assemblyName=$assembly.FullName;runtime=$runtime;runtimeRelativePath=('JueMingR.Runtime/'+$payloadId)}
}
function Test-RecordedPackageArchive {
    param([string] $ZipPath, $Record)
    $root=[string]$Record.packageDirectory.name
    $twoFile=$null -ne $Record.PSObject.Properties['distributionFormat'] -and $Record.distributionFormat -ceq 'two-file-1'
    if ($twoFile) {
        if ($root -cne '' -or @($Record.packageDirectory.files).Count -ne 2 -or
            (@($Record.packageDirectory.files.path | Sort-Object) -join '|') -cne 'JueMingR.dll|Terraria.exe.config') {throw 'Two-file archive must contain exactly the standard pair at its root.'}
    } else {
        Assert-PackageMemberPath $root
        if ($root.Contains('/')) {throw 'Package must have one root.'}
    }
    $prefix=if($twoFile){''}else{$root+'/'}
    $expected=New-Object 'System.Collections.Generic.Dictionary[string,object]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $Record.packageDirectory.files) {
        Assert-PackageMemberPath ([string]$file.path)
        if ($file.sha256 -cnotmatch '^[0-9A-F]{64}$' -or $file.length -lt 0 -or $expected.ContainsKey($prefix+$file.path)) {throw 'Invalid or duplicate recorded member.'}
        $expected.Add($prefix+$file.path,$file)
    }
    if ($expected.Count -eq 0) {throw 'Empty package record.'}
    $seen=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    $archive=[IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        foreach ($entry in $archive.Entries) {
            # The existing builder writes files only; accepting directory/link
            # entries would widen its exact recorded archive contract.
            Assert-PackageMemberPath $entry.FullName
            if (-not $seen.Add($entry.FullName) -or -not $expected.ContainsKey($entry.FullName) -or
                (($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000) {throw 'Unknown, duplicate or symbolic package member.'}
            $file=$expected[$entry.FullName]
            if ($entry.FullName -cne ($prefix+$file.path) -or $entry.Length -ne $file.length) {throw 'Package member identity mismatch.'}
            $stream=$entry.Open(); $sha=[Security.Cryptography.SHA256]::Create()
            try { $hash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') }
            finally {$sha.Dispose();$stream.Dispose()}
            if ($hash -cne $file.sha256) {throw 'Package member hash mismatch.'}
        }
        if ($seen.Count -ne $expected.Count) {throw 'Missing recorded package members.'}
        return $seen.Count
    } finally {$archive.Dispose()}
}
