Set-StrictMode -Version 2.0

$script:Phase0SUtf8NoBom = New-Object System.Text.UTF8Encoding($false, $true)
$script:Phase0SExpectedPayloadPaths = @(
    'JueMingR.Bootstrap.dll',
    'JueMingR.Validation/0Harmony.dll',
    'JueMingR.Validation/JueMingR.Features.dll',
    'JueMingR.Validation/JueMingR.Infrastructure.dll',
    'JueMingR.Validation/JueMingR.Platform.dll',
    'JueMingR.Validation/JueMingR.TerrariaHost.dll',
    'JueMingR.Validation/phase-0-s-runtime.manifest',
    'Terraria.exe.config'
)

function ConvertTo-Phase0SJsonString {
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string] $Value)

    $builder = New-Object System.Text.StringBuilder
    [void] $builder.Append('"')
    foreach ($character in $Value.ToCharArray()) {
        $code = [int] $character
        switch ($character) {
            '"' { [void] $builder.Append('\"'); continue }
            '\' { [void] $builder.Append('\\'); continue }
            "`b" { [void] $builder.Append('\b'); continue }
            "`f" { [void] $builder.Append('\f'); continue }
            "`n" { [void] $builder.Append('\n'); continue }
            "`r" { [void] $builder.Append('\r'); continue }
            "`t" { [void] $builder.Append('\t'); continue }
        }
        if ($code -lt 0x20) {
            [void] $builder.Append(('\u{0:x4}' -f $code))
        }
        else {
            [void] $builder.Append($character)
        }
    }
    [void] $builder.Append('"')
    return $builder.ToString()
}

function ConvertTo-Phase0SCanonicalPackageManifestText {
    param([Parameter(Mandatory = $true)][object] $Manifest)

    $payload = @($Manifest.payload)
    $builder = New-Object System.Text.StringBuilder
    [void] $builder.Append('{"schemaVersion":1,"packageId":')
    [void] $builder.Append((ConvertTo-Phase0SJsonString -Value ([string] $Manifest.packageId)))
    [void] $builder.Append(',"sourceCommit":')
    [void] $builder.Append((ConvertTo-Phase0SJsonString -Value ([string] $Manifest.sourceCommit)))
    [void] $builder.Append(',"target":{"simpleName":')
    [void] $builder.Append((ConvertTo-Phase0SJsonString -Value ([string] $Manifest.target.simpleName)))
    [void] $builder.Append(',"version":')
    [void] $builder.Append((ConvertTo-Phase0SJsonString -Value ([string] $Manifest.target.version)))
    [void] $builder.Append(',"mvid":')
    [void] $builder.Append((ConvertTo-Phase0SJsonString -Value ([string] $Manifest.target.mvid)))
    [void] $builder.Append(',"sha256":')
    [void] $builder.Append((ConvertTo-Phase0SJsonString -Value ([string] $Manifest.target.sha256)))
    [void] $builder.Append('},"payload":[')
    for ($index = 0; $index -lt $payload.Count; $index++) {
        if ($index -ne 0) {
            [void] $builder.Append(',')
        }
        [void] $builder.Append('{"installRelativePath":')
        [void] $builder.Append((ConvertTo-Phase0SJsonString -Value ([string] $payload[$index].installRelativePath)))
        [void] $builder.Append(',"length":')
        [void] $builder.Append(([System.Convert]::ToInt64($payload[$index].length)).ToString([System.Globalization.CultureInfo]::InvariantCulture))
        [void] $builder.Append(',"sha256":')
        [void] $builder.Append((ConvertTo-Phase0SJsonString -Value ([string] $payload[$index].sha256)))
        [void] $builder.Append('}')
    }
    [void] $builder.Append(']}')
    [void] $builder.Append([Environment]::NewLine)
    return $builder.ToString()
}

function Write-Phase0SResultAndExit {
    param(
        [Parameter(Mandatory = $true)][ValidateSet('install', 'restore')][string] $Operation,
        [Parameter(Mandatory = $true)][ValidateSet('success', 'noop', 'conflict', 'failure')][string] $Status,
        [Parameter(Mandatory = $true)][string] $Code,
        [Parameter(Mandatory = $true)][int] $ExitCode,
        [AllowNull()][object] $PackageId,
        [AllowNull()][string] $Object,
        [AllowNull()][object] $Sha256
    )

    $result = [ordered]@{
        schemaVersion = 1
        operation = $Operation
        status = $Status
        code = $Code
        exitCode = $ExitCode
        packageId = $PackageId
        object = $Object
        sha256 = $Sha256
    }
    [Console]::Out.WriteLine(($result | ConvertTo-Json -Compress))
    exit $ExitCode
}

function Get-Phase0SFileSha256 {
    param([Parameter(Mandatory = $true)][string] $Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256 -ErrorAction Stop).Hash.ToUpperInvariant()
}

function Get-Phase0SPathState {
    param([Parameter(Mandatory = $true)][string] $Path)

    try {
        $attributes = [System.IO.File]::GetAttributes($Path)
        return [pscustomobject][ordered]@{
            exists = $true
            readable = $true
            attributes = $attributes
            isDirectory = (($attributes -band [System.IO.FileAttributes]::Directory) -ne 0)
            isReparsePoint = (($attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0)
        }
    }
    catch [System.IO.FileNotFoundException] {
        return [pscustomobject][ordered]@{ exists = $false; readable = $true; attributes = $null; isDirectory = $false; isReparsePoint = $false }
    }
    catch [System.IO.DirectoryNotFoundException] {
        return [pscustomobject][ordered]@{ exists = $false; readable = $true; attributes = $null; isDirectory = $false; isReparsePoint = $false }
    }
    catch {
        return [pscustomobject][ordered]@{ exists = $true; readable = $false; attributes = $null; isDirectory = $false; isReparsePoint = $false }
    }
}

function Test-Phase0SOrdinaryFile {
    param([Parameter(Mandatory = $true)][string] $Path)

    $state = Get-Phase0SPathState -Path $Path
    return $state.exists -and $state.readable -and -not $state.isDirectory -and -not $state.isReparsePoint
}

function Test-Phase0SOrdinaryDirectory {
    param([Parameter(Mandatory = $true)][string] $Path)

    $state = Get-Phase0SPathState -Path $Path
    return $state.exists -and $state.readable -and $state.isDirectory -and -not $state.isReparsePoint
}

function Get-Phase0SStrictUtf8Text {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [int64] $MaximumLength = 1048576
    )

    if (-not (Test-Phase0SOrdinaryFile -Path $Path)) {
        throw 'The required text input is not an ordinary file.'
    }
    $length = (Get-Item -LiteralPath $Path -Force -ErrorAction Stop).Length
    if ($length -lt 0 -or $length -gt $MaximumLength) {
        throw 'The required text input is outside its size limit.'
    }
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        throw 'UTF-8 BOM is not permitted.'
    }
    return $script:Phase0SUtf8NoBom.GetString($bytes)
}

function Assert-Phase0SExactProperties {
    param(
        [Parameter(Mandatory = $true)][object] $Object,
        [Parameter(Mandatory = $true)][string[]] $ExpectedNames
    )

    if ($null -eq $Object) {
        throw 'A required JSON object is null.'
    }
    $actualNames = @($Object.PSObject.Properties | ForEach-Object { $_.Name })
    if ($actualNames.Count -ne $ExpectedNames.Count) {
        throw 'A JSON object has an unexpected property count.'
    }
    for ($index = 0; $index -lt $ExpectedNames.Count; $index++) {
        if ($actualNames[$index] -cne $ExpectedNames[$index]) {
            throw 'A JSON object has unexpected or out-of-order properties.'
        }
    }
}

function Test-Phase0SIntegralJsonNumber {
    param([AllowNull()][object] $Value)

    if ($null -eq $Value) {
        return $false
    }
    return $Value -is [byte] -or $Value -is [sbyte] -or $Value -is [int16] -or
        $Value -is [uint16] -or $Value -is [int32] -or $Value -is [uint32] -or
        $Value -is [int64] -or $Value -is [uint64]
}

function Resolve-Phase0SContainedPath {
    param(
        [Parameter(Mandatory = $true)][string] $Root,
        [Parameter(Mandatory = $true)][string] $RelativePath
    )

    $fullRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd('\')
    $candidate = [System.IO.Path]::GetFullPath((Join-Path $fullRoot $RelativePath.Replace('/', '\')))
    $prefix = $fullRoot + '\'
    if (-not $candidate.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'A relative path escaped its fixed root.'
    }
    return $candidate
}

function Get-Phase0SAssemblyMvid {
    param([Parameter(Mandatory = $true)][string] $Path)

    if ($null -ne ('System.Reflection.PortableExecutable.PEReader' -as [type])) {
        $stream = [System.IO.File]::OpenRead($Path)
        try {
            $reader = [System.Reflection.PortableExecutable.PEReader]::new($stream)
            try {
                $metadata = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($reader)
                return $metadata.GetGuid($metadata.GetModuleDefinition().Mvid).ToString('D')
            }
            finally {
                $reader.Dispose()
            }
        }
        finally {
            $stream.Dispose()
        }
    }

    return [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($Path).ManifestModule.ModuleVersionId.ToString('D')
}

function Get-Phase0SPeClrShape {
    param([Parameter(Mandatory = $true)][string] $Path)

    $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
    try {
        $reader = New-Object System.IO.BinaryReader($stream)
        try {
            if ($stream.Length -lt 256) {
                throw 'PE file is too small.'
            }
            $stream.Position = 0x3C
            $peOffset = $reader.ReadInt32()
            if ($peOffset -lt 0 -or ([int64] $peOffset + 24) -gt $stream.Length) {
                throw 'PE header offset is invalid.'
            }
            $stream.Position = $peOffset
            if ($reader.ReadUInt32() -ne 0x00004550) {
                throw 'PE signature is invalid.'
            }
            $machine = $reader.ReadUInt16()
            $sectionCount = $reader.ReadUInt16()
            $stream.Position = $peOffset + 20
            $optionalHeaderSize = $reader.ReadUInt16()
            $optionalHeaderOffset = $peOffset + 24
            if ($optionalHeaderSize -lt 216 -or ([int64] $optionalHeaderOffset + $optionalHeaderSize) -gt $stream.Length) {
                throw 'PE optional header is invalid.'
            }
            $stream.Position = $optionalHeaderOffset
            $magic = $reader.ReadUInt16()
            if ($magic -ne 0x010B) {
                throw 'PE image is not PE32.'
            }
            $stream.Position = $optionalHeaderOffset + 208
            $cliRva = $reader.ReadUInt32()
            $cliSize = $reader.ReadUInt32()
            if ($cliRva -eq 0 -or $cliSize -lt 20) {
                throw 'PE image has no valid CLR header.'
            }

            $sectionTableOffset = $optionalHeaderOffset + $optionalHeaderSize
            $cliOffset = $null
            for ($index = 0; $index -lt $sectionCount; $index++) {
                $sectionOffset = $sectionTableOffset + ($index * 40)
                if (([int64] $sectionOffset + 40) -gt $stream.Length) {
                    throw 'PE section table is truncated.'
                }
                $stream.Position = $sectionOffset + 8
                $virtualSize = $reader.ReadUInt32()
                $virtualAddress = $reader.ReadUInt32()
                $rawSize = $reader.ReadUInt32()
                $rawOffset = $reader.ReadUInt32()
                $mappedSize = [Math]::Max([int64] $virtualSize, [int64] $rawSize)
                if ([int64] $cliRva -ge [int64] $virtualAddress -and [int64] $cliRva -lt ([int64] $virtualAddress + $mappedSize)) {
                    $cliOffset = [int64] $rawOffset + ([int64] $cliRva - [int64] $virtualAddress)
                    break
                }
            }
            if ($null -eq $cliOffset -or ($cliOffset + 20) -gt $stream.Length) {
                throw 'CLR header does not map to an ordinary section.'
            }
            $stream.Position = $cliOffset + 16
            $clrFlags = $reader.ReadUInt32()
            return [pscustomobject][ordered]@{
                machine = $machine
                peMagic = $magic
                clrFlags = $clrFlags
            }
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Get-Phase0SAssemblyFileIdentity {
    param([Parameter(Mandatory = $true)][string] $Path)

    if (-not (Test-Phase0SOrdinaryFile -Path $Path)) {
        throw 'Assembly identity input is not an ordinary file.'
    }
    $name = [System.Reflection.AssemblyName]::GetAssemblyName($Path)
    return [pscustomobject][ordered]@{
        simpleName = $name.Name
        version = $name.Version.ToString()
        mvid = Get-Phase0SAssemblyMvid -Path $Path
        sha256 = Get-Phase0SFileSha256 -Path $Path
        fullName = $name.FullName
    }
}

function Test-Phase0STerrariaIdentity {
    param([Parameter(Mandatory = $true)][string] $Path)

    try {
        $identity = Get-Phase0SAssemblyFileIdentity -Path $Path
        if ($identity.simpleName -cne 'Terraria' -or
            $identity.version -cne '1.4.5.8' -or
            $identity.mvid -cne '2c29f6c3-4bd9-4add-9c58-da159804e083' -or
            $identity.sha256 -cne '960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3') {
            return $false
        }
        $shape = Get-Phase0SPeClrShape -Path $Path
        return $shape.machine -eq 0x014C -and $shape.peMagic -eq 0x010B -and
            (($shape.clrFlags -band 0x1) -ne 0) -and (($shape.clrFlags -band 0x2) -ne 0)
    }
    catch {
        return $false
    }
}

function Read-Phase0SRuntimeManifest {
    param([Parameter(Mandatory = $true)][string] $Path)

    $text = Get-Phase0SStrictUtf8Text -Path $Path -MaximumLength 16384
    if ($text.EndsWith("`r`n", [System.StringComparison]::Ordinal)) {
        $body = $text.Substring(0, $text.Length - 2)
    }
    elseif ($text.EndsWith("`n", [System.StringComparison]::Ordinal)) {
        $body = $text.Substring(0, $text.Length - 1)
    }
    else {
        $body = $text
    }
    $normalizedBody = $body.Replace("`r`n", "`n")
    if ($normalizedBody.IndexOf("`r", [System.StringComparison]::Ordinal) -ge 0) {
        throw 'Runtime manifest uses an invalid line ending.'
    }
    $lines = @($normalizedBody.Split([char] "`n"))
    $expectedKeys = @(
        'schemaVersion',
        'packageId',
        'sourceCommit',
        'targetAssemblySimpleName',
        'targetAssemblyVersion',
        'targetAssemblyMvid',
        'targetAssemblySha256',
        'reLogicAssemblySimpleName',
        'reLogicAssemblyVersion',
        'reLogicAssemblyPublicKeyToken',
        'reLogicAssemblyMvid',
        'reLogicResourceName',
        'reLogicResourceSha256',
        'targetTypeName',
        'targetMethodName',
        'targetMethodMetadataToken',
        'targetMethodIsStatic',
        'targetMethodReturnType',
        'targetMethodParameterCount',
        'targetMethodParameterType',
        'hostAssemblySimpleName',
        'hostAssemblyVersion',
        'hostAssemblyMvid',
        'hostAssemblySha256',
        'harmonyAssemblySimpleName',
        'harmonyAssemblyVersion',
        'harmonyAssemblyMvid',
        'harmonyAssemblySha256',
        'patchOwner',
        'evidenceFileName'
    )
    if ($lines.Count -ne $expectedKeys.Count) {
        throw 'Runtime manifest must contain exactly 30 lines.'
    }

    $values = [ordered]@{}
    for ($index = 0; $index -lt $expectedKeys.Count; $index++) {
        $separator = $lines[$index].IndexOf('=', [System.StringComparison]::Ordinal)
        if ($separator -le 0 -or $lines[$index].IndexOf('=', $separator + 1) -ge 0) {
            throw 'Runtime manifest line shape is invalid.'
        }
        $key = $lines[$index].Substring(0, $separator)
        $value = $lines[$index].Substring($separator + 1)
        if ($key -cne $expectedKeys[$index] -or $value.Length -eq 0) {
            throw 'Runtime manifest keys or values are invalid.'
        }
        $values[$key] = $value
    }

    if ($values.schemaVersion -cne '2' -or
        $values.packageId -notmatch '^[A-Za-z0-9.-]{1,64}$' -or
        $values.sourceCommit -notmatch '^[0-9a-f]{40}$' -or
        $values.targetAssemblySimpleName -cne 'Terraria' -or
        $values.targetAssemblyVersion -cne '1.4.5.8' -or
        $values.targetAssemblySha256 -notmatch '^[0-9A-F]{64}$' -or
        $values.reLogicAssemblySimpleName -cne 'ReLogic' -or
        $values.reLogicAssemblyVersion -cne '1.0.0.0' -or
        $values.reLogicAssemblyPublicKeyToken -cne 'null' -or
        $values.reLogicAssemblyMvid -cne 'ee258be9-88a4-423d-b3ce-84b6c35b141a' -or
        $values.reLogicResourceName -cne 'Terraria.Libraries.ReLogic.ReLogic.dll' -or
        $values.reLogicResourceSha256 -cne 'E1C5DCCEFFF5FD1C789FF712BABFA1A305FCED0D03C96EF30F2C14D99AA0AF29' -or
        $values.targetTypeName -cne 'Terraria.Main' -or
        $values.targetMethodName -cne 'Update' -or
        $values.targetMethodMetadataToken -notmatch '^0x[0-9A-F]{8}$' -or
        $values.targetMethodIsStatic -cne 'false' -or
        $values.targetMethodReturnType -cne 'System.Void' -or
        $values.targetMethodParameterCount -cne '1' -or
        $values.targetMethodParameterType -cne 'Microsoft.Xna.Framework.GameTime' -or
        $values.hostAssemblySimpleName -cne 'JueMingR.TerrariaHost' -or
        $values.hostAssemblyVersion -cne '0.0.0.0' -or
        $values.hostAssemblySha256 -notmatch '^[0-9A-F]{64}$' -or
        $values.harmonyAssemblySimpleName -cne '0Harmony' -or
        $values.harmonyAssemblyVersion -cne '2.4.2.0' -or
        $values.harmonyAssemblyMvid -cne '024a0e6e-c8c2-437e-ad04-7b6279389c23' -or
        $values.harmonyAssemblySha256 -cne '7B9E756306FA3D7620E02A857C8927A6AB04973F9BD8A77D3866700A6DEAC55C' -or
        $values.patchOwner -cne 'JueMingR.Phase0S.MainUpdate' -or
        $values.evidenceFileName -cne 'phase-0-s-evidence.log') {
        throw 'Runtime manifest fixed values are invalid.'
    }
    foreach ($guidValue in @([string] $values.targetAssemblyMvid, [string] $values.reLogicAssemblyMvid, [string] $values.hostAssemblyMvid)) {
        $guid = [Guid]::Empty
        if (-not [Guid]::TryParseExact($guidValue, 'D', [ref] $guid) -or $guid.ToString('D') -cne $guidValue) {
            throw 'Runtime manifest GUID is not canonical.'
        }
    }
    return [pscustomobject] $values
}

function Test-Phase0SPackagePayloadTree {
    param(
        [Parameter(Mandatory = $true)][string] $PayloadRoot,
        [Parameter(Mandatory = $true)][object[]] $PayloadEntries
    )

    if (-not (Test-Phase0SOrdinaryDirectory -Path $PayloadRoot)) {
        throw 'Package payload root is not an ordinary directory.'
    }
    $expectedFiles = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $PayloadEntries) {
        [void] $expectedFiles.Add(([string] $entry.installRelativePath).Replace('/', '\'))
    }
    $expectedDirectories = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    [void] $expectedDirectories.Add('JueMingR.Validation')

    $queue = New-Object 'System.Collections.Generic.Queue[string]'
    $queue.Enqueue($PayloadRoot)
    $seenFiles = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    $seenDirectories = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    while ($queue.Count -gt 0) {
        $directory = $queue.Dequeue()
        foreach ($item in @(Get-ChildItem -LiteralPath $directory -Force -ErrorAction Stop)) {
            if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw 'Package payload contains a reparse point.'
            }
            $relative = $item.FullName.Substring($PayloadRoot.TrimEnd('\').Length).TrimStart('\')
            if ($item.PSIsContainer) {
                if (-not $expectedDirectories.Contains($relative) -or -not $seenDirectories.Add($relative)) {
                    throw 'Package payload contains an unknown or duplicate directory.'
                }
                $queue.Enqueue($item.FullName)
            }
            else {
                if (-not $expectedFiles.Contains($relative) -or -not $seenFiles.Add($relative)) {
                    throw 'Package payload contains an unknown or duplicate file.'
                }
            }
        }
    }
    if ($seenFiles.Count -ne $expectedFiles.Count -or $seenDirectories.Count -ne $expectedDirectories.Count) {
        throw 'Package payload tree is incomplete.'
    }
}

function Read-Phase0SPackage {
    param([Parameter(Mandatory = $true)][string] $PackageRoot)

    $fullPackageRoot = [System.IO.Path]::GetFullPath($PackageRoot)
    if (-not (Test-Phase0SOrdinaryDirectory -Path $fullPackageRoot)) {
        throw 'Package root is not an ordinary directory.'
    }
    $manifestPath = Join-Path $fullPackageRoot 'phase-0-s-package.manifest.json'
    $manifestText = Get-Phase0SStrictUtf8Text -Path $manifestPath -MaximumLength 131072
    try {
        $manifest = $manifestText | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        throw 'Package manifest JSON is invalid.'
    }

    Assert-Phase0SExactProperties -Object $manifest -ExpectedNames @('schemaVersion', 'packageId', 'sourceCommit', 'target', 'payload')
    Assert-Phase0SExactProperties -Object $manifest.target -ExpectedNames @('simpleName', 'version', 'mvid', 'sha256')
    if (-not (Test-Phase0SIntegralJsonNumber -Value $manifest.schemaVersion) -or [int64] $manifest.schemaVersion -ne 1) {
        throw 'Package manifest schemaVersion is invalid.'
    }
    $allowedPackageIds = @(
        ('phase0s-' + [string] $manifest.sourceCommit),
        ('phase0t-biome-' + [string] $manifest.sourceCommit),
        ('phase0u-f5-ui-' + [string] $manifest.sourceCommit)
    )
    if ([string] $manifest.sourceCommit -notmatch '^[0-9a-f]{40}$' -or
        $allowedPackageIds -cnotcontains [string] $manifest.packageId) {
        throw 'Package manifest source identity is invalid.'
    }
    if ([string] $manifest.target.simpleName -cne 'Terraria' -or
        [string] $manifest.target.version -cne '1.4.5.8' -or
        [string] $manifest.target.mvid -cne '2c29f6c3-4bd9-4add-9c58-da159804e083' -or
        [string] $manifest.target.sha256 -cne '960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3') {
        throw 'Package manifest target identity is invalid.'
    }
    $payload = @($manifest.payload)
    if ($payload.Count -ne $script:Phase0SExpectedPayloadPaths.Count) {
        throw 'Package manifest payload count is invalid.'
    }
    $caseInsensitivePaths = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    for ($index = 0; $index -lt $payload.Count; $index++) {
        $entry = $payload[$index]
        Assert-Phase0SExactProperties -Object $entry -ExpectedNames @('installRelativePath', 'length', 'sha256')
        $relativePath = [string] $entry.installRelativePath
        if ($relativePath -cne $script:Phase0SExpectedPayloadPaths[$index] -or
            -not $caseInsensitivePaths.Add($relativePath) -or
            -not (Test-Phase0SIntegralJsonNumber -Value $entry.length) -or
            [int64] $entry.length -lt 0 -or
            [string] $entry.sha256 -notmatch '^[0-9A-F]{64}$') {
            throw 'Package manifest payload entry is invalid.'
        }
    }
    $canonical = ConvertTo-Phase0SCanonicalPackageManifestText -Manifest $manifest
    if ($manifestText -cne $canonical) {
        throw 'Package manifest is not in the canonical byte representation.'
    }

    $payloadRoot = Join-Path $fullPackageRoot 'payload'
    Test-Phase0SPackagePayloadTree -PayloadRoot $payloadRoot -PayloadEntries $payload
    foreach ($entry in $payload) {
        $sourcePath = Resolve-Phase0SContainedPath -Root $payloadRoot -RelativePath ([string] $entry.installRelativePath)
        if (-not (Test-Phase0SOrdinaryFile -Path $sourcePath)) {
            throw 'Package payload file is not an ordinary file.'
        }
        $item = Get-Item -LiteralPath $sourcePath -Force -ErrorAction Stop
        if ([int64] $item.Length -ne [int64] $entry.length -or
            (Get-Phase0SFileSha256 -Path $sourcePath) -cne [string] $entry.sha256) {
            throw 'Package payload identity mismatch.'
        }
    }

    $runtimePath = Join-Path $payloadRoot 'JueMingR.Validation\phase-0-s-runtime.manifest'
    $runtime = Read-Phase0SRuntimeManifest -Path $runtimePath
    if ($runtime.packageId -cne [string] $manifest.packageId -or
        $runtime.sourceCommit -cne [string] $manifest.sourceCommit) {
        throw 'Runtime and package manifest source identities differ.'
    }
    return [pscustomobject][ordered]@{
        root = $fullPackageRoot
        manifestPath = $manifestPath
        manifestText = $manifestText
        manifestBytes = [System.IO.File]::ReadAllBytes($manifestPath)
        packageId = [string] $manifest.packageId
        sourceCommit = [string] $manifest.sourceCommit
        target = $manifest.target
        payload = $payload
        payloadRoot = $payloadRoot
        runtime = $runtime
    }
}

function Get-Phase0SPayloadEntry {
    param(
        [Parameter(Mandatory = $true)][object] $Package,
        [Parameter(Mandatory = $true)][string] $RelativePath
    )

    $matches = @($Package.payload | Where-Object { [string] $_.installRelativePath -ceq $RelativePath })
    if ($matches.Count -ne 1) {
        throw 'A fixed payload entry is missing.'
    }
    return $matches[0]
}

function Test-Phase0SFileMatchesPayloadEntry {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][object] $Entry
    )

    try {
        if (-not (Test-Phase0SOrdinaryFile -Path $Path)) {
            return $false
        }
        $item = Get-Item -LiteralPath $Path -Force -ErrorAction Stop
        return [int64] $item.Length -eq [int64] $Entry.length -and
            (Get-Phase0SFileSha256 -Path $Path) -ceq [string] $Entry.sha256
    }
    catch {
        return $false
    }
}

function Test-Phase0SFilesEqualBytes {
    param(
        [Parameter(Mandatory = $true)][string] $FirstPath,
        [Parameter(Mandatory = $true)][string] $SecondPath
    )

    try {
        if (-not (Test-Phase0SOrdinaryFile -Path $FirstPath) -or -not (Test-Phase0SOrdinaryFile -Path $SecondPath)) {
            return $false
        }
        $first = Get-Item -LiteralPath $FirstPath -Force -ErrorAction Stop
        $second = Get-Item -LiteralPath $SecondPath -Force -ErrorAction Stop
        return $first.Length -eq $second.Length -and
            (Get-Phase0SFileSha256 -Path $FirstPath) -ceq (Get-Phase0SFileSha256 -Path $SecondPath)
    }
    catch {
        return $false
    }
}

function Initialize-Phase0SNativeMethods {
    if ($null -eq ('JueMingR.Phase0S.NativeMethods' -as [type])) {
        Add-Type -Namespace 'JueMingR.Phase0S' -Name 'NativeMethods' -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
public static extern bool CreateDirectory(string path, System.IntPtr securityAttributes);
'@ -ErrorAction Stop | Out-Null
    }
}

function New-Phase0SDirectoryCreateNew {
    param([Parameter(Mandatory = $true)][string] $Path)

    Initialize-Phase0SNativeMethods
    if ([JueMingR.Phase0S.NativeMethods]::CreateDirectory($Path, [IntPtr]::Zero)) {
        return $true
    }
    $errorCode = [System.Runtime.InteropServices.Marshal]::GetLastWin32Error()
    if ($errorCode -eq 80 -or $errorCode -eq 183) {
        return $false
    }
    throw 'The fixed directory could not be created.'
}

function Copy-Phase0SFileCreateNew {
    param(
        [Parameter(Mandatory = $true)][string] $SourcePath,
        [Parameter(Mandatory = $true)][string] $DestinationPath,
        [Parameter(Mandatory = $true)][object] $ExpectedEntry
    )

    if (-not (Test-Phase0SFileMatchesPayloadEntry -Path $SourcePath -Entry $ExpectedEntry)) {
        throw 'Source payload identity changed before copy.'
    }
    $source = [System.IO.File]::Open($SourcePath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
    try {
        $destination = [System.IO.File]::Open($DestinationPath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        try {
            $source.CopyTo($destination)
            $destination.Flush($true)
        }
        finally {
            $destination.Dispose()
        }
    }
    finally {
        $source.Dispose()
    }
    if (-not (Test-Phase0SFileMatchesPayloadEntry -Path $DestinationPath -Entry $ExpectedEntry)) {
        throw 'Copied payload identity mismatch.'
    }
}

function Copy-Phase0SBytesCreateNew {
    param(
        [Parameter(Mandatory = $true)][byte[]] $Bytes,
        [Parameter(Mandatory = $true)][string] $DestinationPath
    )

    $destination = [System.IO.File]::Open($DestinationPath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
    try {
        $destination.Write($Bytes, 0, $Bytes.Length)
        $destination.Flush($true)
    }
    finally {
        $destination.Dispose()
    }
}

function Test-Phase0SEvidenceFile {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $PackageId
    )

    try {
        $text = Get-Phase0SStrictUtf8Text -Path $Path -MaximumLength 65536
        if ($text.EndsWith("`r`n", [System.StringComparison]::Ordinal)) {
            $body = $text.Substring(0, $text.Length - 2)
        }
        elseif ($text.EndsWith("`n", [System.StringComparison]::Ordinal)) {
            $body = $text.Substring(0, $text.Length - 1)
        }
        else {
            $body = $text
        }
        $normalizedBody = $body.Replace("`r`n", "`n")
        if ($normalizedBody.Length -eq 0 -or $normalizedBody.IndexOf("`r", [System.StringComparison]::Ordinal) -ge 0) {
            return $false
        }
        $lines = @($normalizedBody.Split([char] "`n"))
        if ($lines.Count -lt 1 -or $lines.Count -gt 6) {
            return $false
        }
        $eventNames = @(
            'TERRARIA_ASSEMBLY_READY',
            'HARMONY_READY',
            'HOOK_INSTALLED',
            'MAIN_UPDATE_POSTFIX_FIRED',
            'RUNTIME_HANDOFF_COMPLETE'
        )
        $allowedStages = @(
            'BOOTSTRAP_MANIFEST', 'TARGET_IDENTITY', 'READINESS_IDENTITY', 'EVIDENCE_CREATE', 'HOST_LOAD', 'HOST_ENTRY',
            'HOST_VALIDATE', 'HARMONY_LOAD', 'TARGET_METHOD', 'PATCH', 'PATCH_INFO', 'PATCH_CLEANUP',
            'POSTFIX', 'HANDOFF'
        )
        $allowedCodes = @(
            'INVALID_MANIFEST', 'IDENTITY_MISMATCH', 'PRELOADED', 'NOT_UNIQUE', 'APPEND_FAILED',
            'PATCH_FAILED', 'VERIFY_FAILED', 'CLEANUP_FAILED'
        )
        $eventCount = 0
        $primaryErrorSeen = $false
        $primaryPatchSeen = $false
        $cleanupErrorSeen = $false
        foreach ($line in $lines) {
            if ($line.Length -eq 0 -or $line.Trim() -cne $line) {
                return $false
            }
            $fields = @($line.Split('|'))
            if (($fields.Count -ne 7 -and $fields.Count -ne 8) -or $fields[0] -cne 'PHASE0S' -or
                $fields[1] -cne '1' -or $fields[2] -cne $PackageId) {
                return $false
            }
            if ($fields[3] -ceq 'ERROR') {
                $isCleanup = $fields[4] -ceq 'PATCH_CLEANUP' -and $fields[5] -ceq 'CLEANUP_FAILED'
                if ($cleanupErrorSeen -or $eventCount -eq 0 -or
                    $allowedStages -cnotcontains $fields[4] -or
                    $allowedCodes -cnotcontains $fields[5] -or
                    $fields[6] -notmatch '^[A-Za-z][A-Za-z0-9]*$') {
                    return $false
                }
                if ($fields.Count -eq 8 -and
                    ($fields[6] -cne 'FileNotFoundException' -or
                     $fields[7] -cne 'ReLogic, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null')) {
                    return $false
                }
                if ($isCleanup) {
                    if (-not $primaryPatchSeen) {
                        return $false
                    }
                    $cleanupErrorSeen = $true
                }
                elseif ($primaryErrorSeen) {
                    return $false
                }
                else {
                    $primaryErrorSeen = $true
                    $primaryPatchSeen = $fields[4] -ceq 'PATCH' -and $fields[5] -ceq 'PATCH_FAILED'
                }
                continue
            }
            if ($primaryErrorSeen -or $eventCount -ge 5 -or $fields.Count -ne 7) {
                return $false
            }
            $expectedSequence = ($eventCount + 1).ToString('D2', [System.Globalization.CultureInfo]::InvariantCulture)
            if ($fields[3] -cne $expectedSequence -or $fields[4] -cne $eventNames[$eventCount] -or
                $fields[5] -notmatch '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$') {
                return $false
            }
            $timestamp = [DateTimeOffset]::MinValue
            if (-not [DateTimeOffset]::TryParseExact(
                $fields[5],
                'O',
                [System.Globalization.CultureInfo]::InvariantCulture,
                [System.Globalization.DateTimeStyles]::RoundtripKind,
                [ref] $timestamp)) {
                return $false
            }
            $threadId = 0
            if (-not [int]::TryParse($fields[6], [System.Globalization.NumberStyles]::None, [System.Globalization.CultureInfo]::InvariantCulture, [ref] $threadId) -or $threadId -le 0) {
                return $false
            }
            $eventCount++
        }
        return $eventCount -ge 1
    }
    catch {
        return $false
    }
}

function Assert-Phase0SSidecarDirectory {
    param(
        [Parameter(Mandatory = $true)][string] $Directory,
        [Parameter(Mandatory = $true)][object] $Package,
        [switch] $AllowPartialStatic,
        [switch] $AllowEvidence
    )

    if (-not (Test-Phase0SOrdinaryDirectory -Path $Directory)) {
        throw 'Controlled sidecar is not an ordinary directory.'
    }
    $receiptName = 'phase-0-s-install-manifest.json'
    $receiptPath = Join-Path $Directory $receiptName
    if (-not (Test-Phase0SFilesEqualBytes -FirstPath $Package.manifestPath -SecondPath $receiptPath)) {
        throw 'Controlled sidecar receipt does not match the package manifest.'
    }

    $staticNames = @(
        '0Harmony.dll',
        'JueMingR.Features.dll',
        'JueMingR.Infrastructure.dll',
        'JueMingR.Platform.dll',
        'JueMingR.TerrariaHost.dll',
        'phase-0-s-runtime.manifest'
    )
    $allowedNames = @($receiptName) + $staticNames
    if ($AllowEvidence) {
        $allowedNames += 'phase-0-s-evidence.log'
    }
    $items = @(Get-ChildItem -LiteralPath $Directory -Force -ErrorAction Stop)
    foreach ($item in $items) {
        if ($item.PSIsContainer -or ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 -or
            $allowedNames -cnotcontains $item.Name) {
            throw 'Controlled sidecar contains an unknown object.'
        }
    }
    foreach ($staticName in $staticNames) {
        $path = Join-Path $Directory $staticName
        $state = Get-Phase0SPathState -Path $path
        if (-not $state.exists) {
            if (-not $AllowPartialStatic) {
                throw 'Controlled sidecar is missing a static file.'
            }
            continue
        }
        $entry = Get-Phase0SPayloadEntry -Package $Package -RelativePath ('JueMingR.Validation/' + $staticName)
        if (-not (Test-Phase0SFileMatchesPayloadEntry -Path $path -Entry $entry)) {
            throw 'Controlled sidecar static file identity mismatch.'
        }
    }
    $evidencePath = Join-Path $Directory 'phase-0-s-evidence.log'
    if ((Get-Phase0SPathState -Path $evidencePath).exists) {
        if (-not $AllowEvidence -or -not (Test-Phase0SEvidenceFile -Path $evidencePath -PackageId $Package.packageId)) {
            throw 'Controlled sidecar evidence is not attributable to this package.'
        }
    }
}

function Get-Phase0SControlledPaths {
    param([Parameter(Mandatory = $true)][string] $TerrariaDirectory)

    return [pscustomobject][ordered]@{
        configFinal = Join-Path $TerrariaDirectory 'Terraria.exe.config'
        configTemp = Join-Path $TerrariaDirectory 'Terraria.exe.config.phase0s-temp'
        bootstrapFinal = Join-Path $TerrariaDirectory 'JueMingR.Bootstrap.dll'
        bootstrapTemp = Join-Path $TerrariaDirectory 'JueMingR.Bootstrap.dll.phase0s-temp'
        sidecarFinal = Join-Path $TerrariaDirectory 'JueMingR.Validation'
        sidecarStage = Join-Path $TerrariaDirectory 'JueMingR.Validation.phase0s-stage'
    }
}

function Get-Phase0SValidatedTerrariaDirectory {
    param([Parameter(Mandatory = $true)][string] $TerrariaDirectory)

    if ([string]::IsNullOrWhiteSpace($TerrariaDirectory)) {
        throw 'Terraria directory argument is empty.'
    }
    $fullPath = [System.IO.Path]::GetFullPath($TerrariaDirectory)
    if (-not (Test-Phase0SOrdinaryDirectory -Path $fullPath)) {
        throw 'Terraria directory argument is not an ordinary directory.'
    }
    return $fullPath.TrimEnd('\')
}

function Test-Phase0SRestoreOwnership {
    param(
        [Parameter(Mandatory = $true)][string] $TerrariaDirectory,
        [Parameter(Mandatory = $true)][object] $Package
    )

    try {
        $paths = Get-Phase0SControlledPaths -TerrariaDirectory $TerrariaDirectory
        $states = [ordered]@{}
        foreach ($name in @('configFinal', 'configTemp', 'bootstrapFinal', 'bootstrapTemp', 'sidecarFinal', 'sidecarStage')) {
            $states[$name] = Get-Phase0SPathState -Path $paths.$name
        }
        $existsCount = @($states.Values | Where-Object { $_.exists }).Count
        if ($existsCount -eq 0) {
            return [pscustomobject][ordered]@{ valid = $true; noop = $true; paths = $paths }
        }

        if (($states.sidecarFinal.exists -and $states.sidecarStage.exists) -or
            ($states.configFinal.exists -and $states.configTemp.exists) -or
            ($states.bootstrapFinal.exists -and $states.bootstrapTemp.exists)) {
            return [pscustomobject][ordered]@{ valid = $false; noop = $false; paths = $paths }
        }
        if ($states.sidecarStage.exists) {
            if ($states.sidecarFinal.exists -or $states.configFinal.exists -or $states.configTemp.exists -or
                $states.bootstrapFinal.exists -or $states.bootstrapTemp.exists) {
                return [pscustomobject][ordered]@{ valid = $false; noop = $false; paths = $paths }
            }
            Assert-Phase0SSidecarDirectory -Directory $paths.sidecarStage -Package $Package -AllowPartialStatic
            return [pscustomobject][ordered]@{ valid = $true; noop = $false; paths = $paths }
        }
        if (-not $states.sidecarFinal.exists) {
            return [pscustomobject][ordered]@{ valid = $false; noop = $false; paths = $paths }
        }

        Assert-Phase0SSidecarDirectory -Directory $paths.sidecarFinal -Package $Package -AllowEvidence
        $bootstrapEntry = Get-Phase0SPayloadEntry -Package $Package -RelativePath 'JueMingR.Bootstrap.dll'
        $configEntry = Get-Phase0SPayloadEntry -Package $Package -RelativePath 'Terraria.exe.config'
        foreach ($name in @('bootstrapFinal', 'bootstrapTemp')) {
            if ($states[$name].exists -and -not (Test-Phase0SFileMatchesPayloadEntry -Path $paths.$name -Entry $bootstrapEntry)) {
                return [pscustomobject][ordered]@{ valid = $false; noop = $false; paths = $paths }
            }
        }
        foreach ($name in @('configFinal', 'configTemp')) {
            if ($states[$name].exists -and -not (Test-Phase0SFileMatchesPayloadEntry -Path $paths.$name -Entry $configEntry)) {
                return [pscustomobject][ordered]@{ valid = $false; noop = $false; paths = $paths }
            }
        }
        if (($states.configFinal.exists -or $states.configTemp.exists) -and -not $states.bootstrapFinal.exists) {
            return [pscustomobject][ordered]@{ valid = $false; noop = $false; paths = $paths }
        }
        return [pscustomobject][ordered]@{ valid = $true; noop = $false; paths = $paths }
    }
    catch {
        return [pscustomobject][ordered]@{ valid = $false; noop = $false; paths = (Get-Phase0SControlledPaths -TerrariaDirectory $TerrariaDirectory) }
    }
}

function Remove-Phase0SControlledDirectory {
    param(
        [Parameter(Mandatory = $true)][string] $Directory,
        [Parameter(Mandatory = $true)][object] $Package,
        [switch] $AllowPartialStatic,
        [switch] $AllowEvidence
    )

    Assert-Phase0SSidecarDirectory -Directory $Directory -Package $Package -AllowPartialStatic:$AllowPartialStatic -AllowEvidence:$AllowEvidence
    $staticNames = @(
        '0Harmony.dll',
        'JueMingR.Features.dll',
        'JueMingR.Infrastructure.dll',
        'JueMingR.Platform.dll',
        'JueMingR.TerrariaHost.dll',
        'phase-0-s-runtime.manifest'
    )
    $evidencePath = Join-Path $Directory 'phase-0-s-evidence.log'
    if ((Get-Phase0SPathState -Path $evidencePath).exists) {
        if (-not $AllowEvidence -or -not (Test-Phase0SEvidenceFile -Path $evidencePath -PackageId $Package.packageId)) {
            throw 'Evidence identity changed before deletion.'
        }
        [System.IO.File]::Delete($evidencePath)
    }
    foreach ($name in $staticNames) {
        $path = Join-Path $Directory $name
        if ((Get-Phase0SPathState -Path $path).exists) {
            $entry = Get-Phase0SPayloadEntry -Package $Package -RelativePath ('JueMingR.Validation/' + $name)
            if (-not (Test-Phase0SFileMatchesPayloadEntry -Path $path -Entry $entry)) {
                throw 'Static sidecar identity changed before deletion.'
            }
            [System.IO.File]::Delete($path)
        }
    }
    $receiptPath = Join-Path $Directory 'phase-0-s-install-manifest.json'
    if (-not (Test-Phase0SFilesEqualBytes -FirstPath $Package.manifestPath -SecondPath $receiptPath)) {
        throw 'Receipt identity changed before deletion.'
    }
    [System.IO.File]::Delete($receiptPath)
    [System.IO.Directory]::Delete($Directory, $false)
}

function Remove-Phase0SOwnedObjects {
    param(
        [Parameter(Mandatory = $true)][string] $TerrariaDirectory,
        [Parameter(Mandatory = $true)][object] $Package
    )

    $state = Test-Phase0SRestoreOwnership -TerrariaDirectory $TerrariaDirectory -Package $Package
    if (-not $state.valid) {
        throw 'Ownership changed before deletion.'
    }
    if ($state.noop) {
        return
    }
    $paths = $state.paths
    $configEntry = Get-Phase0SPayloadEntry -Package $Package -RelativePath 'Terraria.exe.config'
    $bootstrapEntry = Get-Phase0SPayloadEntry -Package $Package -RelativePath 'JueMingR.Bootstrap.dll'
    foreach ($path in @($paths.configFinal, $paths.configTemp)) {
        if ((Get-Phase0SPathState -Path $path).exists) {
            if (-not (Test-Phase0SFileMatchesPayloadEntry -Path $path -Entry $configEntry)) {
                throw 'Config identity changed before deletion.'
            }
            [System.IO.File]::Delete($path)
        }
    }
    foreach ($path in @($paths.bootstrapFinal, $paths.bootstrapTemp)) {
        if ((Get-Phase0SPathState -Path $path).exists) {
            if (-not (Test-Phase0SFileMatchesPayloadEntry -Path $path -Entry $bootstrapEntry)) {
                throw 'Bootstrap identity changed before deletion.'
            }
            [System.IO.File]::Delete($path)
        }
    }
    if ((Get-Phase0SPathState -Path $paths.sidecarFinal).exists) {
        Remove-Phase0SControlledDirectory -Directory $paths.sidecarFinal -Package $Package -AllowEvidence
    }
    if ((Get-Phase0SPathState -Path $paths.sidecarStage).exists) {
        Remove-Phase0SControlledDirectory -Directory $paths.sidecarStage -Package $Package -AllowPartialStatic
    }
}
