[CmdletBinding()]
param(
    [string] $TerrariaInstallDirectory,
    [string] $XnaReferenceDirectory,
    [string] $DestinationDirectory,
    [string] $ReadOnlyLegacyDirectory,
    [string] $ReproducibilityRoot,
    [switch] $Force,
    [switch] $InspectOnly,
    [switch] $VerifyOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$script:RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$script:BaselinePath = Join-Path $script:RepositoryRoot 'eng\TerrariaReferences.baseline.json'
$script:MarkerName = '.juemingr-reference-set.json'
$script:GeneratorIdentity = 'scripts/prepare-terraria-references.ps1'
$script:Utf8NoBom = New-Object System.Text.UTF8Encoding($false)

if ($null -eq ('JueMingR.PhysicalPathNativeMethods' -as [type])) {
    $assemblyName = New-Object System.Reflection.AssemblyName('JueMingR.PhysicalPath.Dynamic')
    if ($PSVersionTable.PSEdition -eq 'Core') {
        $assemblyBuilder = [System.Reflection.Emit.AssemblyBuilder]::DefineDynamicAssembly(
            $assemblyName,
            [System.Reflection.Emit.AssemblyBuilderAccess]::Run)
    }
    else {
        $assemblyBuilder = [AppDomain]::CurrentDomain.DefineDynamicAssembly(
            $assemblyName,
            [System.Reflection.Emit.AssemblyBuilderAccess]::Run)
    }
    $moduleBuilder = $assemblyBuilder.DefineDynamicModule('JueMingR.PhysicalPath.Dynamic')
    $typeBuilder = $moduleBuilder.DefineType(
        'JueMingR.PhysicalPathNativeMethods',
        [System.Reflection.TypeAttributes] 'Public, Abstract, Sealed')
    $methodAttributes = [System.Reflection.MethodAttributes] 'Public, Static, PinvokeImpl'
    $createFileMethod = $typeBuilder.DefinePInvokeMethod(
        'CreateFile',
        'kernel32.dll',
        'CreateFileW',
        $methodAttributes,
        [System.Reflection.CallingConventions]::Standard,
        [Microsoft.Win32.SafeHandles.SafeFileHandle],
        [Type[]] @([string], [uint32], [uint32], [IntPtr], [uint32], [uint32], [IntPtr]),
        [System.Runtime.InteropServices.CallingConvention]::Winapi,
        [System.Runtime.InteropServices.CharSet]::Unicode)
    $createFileMethod.SetImplementationFlags(
        $createFileMethod.GetMethodImplementationFlags() -bor [System.Reflection.MethodImplAttributes]::PreserveSig)
    $getFinalPathMethod = $typeBuilder.DefinePInvokeMethod(
        'GetFinalPathNameByHandle',
        'kernel32.dll',
        'GetFinalPathNameByHandleW',
        $methodAttributes,
        [System.Reflection.CallingConventions]::Standard,
        [uint32],
        [Type[]] @([Microsoft.Win32.SafeHandles.SafeFileHandle], [System.Text.StringBuilder], [uint32], [uint32]),
        [System.Runtime.InteropServices.CallingConvention]::Winapi,
        [System.Runtime.InteropServices.CharSet]::Unicode)
    $getFinalPathMethod.SetImplementationFlags(
        $getFinalPathMethod.GetMethodImplementationFlags() -bor [System.Reflection.MethodImplAttributes]::PreserveSig)
    $setEnvironmentVariableMethod = $typeBuilder.DefinePInvokeMethod(
        'SetEnvironmentVariable',
        'kernel32.dll',
        'SetEnvironmentVariableW',
        $methodAttributes,
        [System.Reflection.CallingConventions]::Standard,
        [bool],
        [Type[]] @([string], [string]),
        [System.Runtime.InteropServices.CallingConvention]::Winapi,
        [System.Runtime.InteropServices.CharSet]::Unicode)
    $setEnvironmentVariableMethod.SetImplementationFlags(
        $setEnvironmentVariableMethod.GetMethodImplementationFlags() -bor [System.Reflection.MethodImplAttributes]::PreserveSig)
    $null = $typeBuilder.CreateType()
}

function Get-FinalDirectoryPath {
    param([string] $Path)

    $handle = [JueMingR.PhysicalPathNativeMethods]::CreateFile(
        $Path,
        0,
        0x00000007,
        [IntPtr]::Zero,
        3,
        0x02000000,
        [IntPtr]::Zero)
    if ($null -eq $handle -or $handle.IsInvalid) {
        if ($null -ne $handle) { $handle.Dispose() }
        throw 'Could not open a directory handle for physical path resolution.'
    }

    try {
        $buffer = New-Object System.Text.StringBuilder(512)
        $length = [JueMingR.PhysicalPathNativeMethods]::GetFinalPathNameByHandle(
            $handle,
            $buffer,
            [uint32] $buffer.Capacity,
            0)
        if ($length -eq 0) {
            throw 'Could not resolve a directory handle to its physical path.'
        }
        if ($length -ge $buffer.Capacity) {
            $buffer = New-Object System.Text.StringBuilder([int] $length + 1)
            $length = [JueMingR.PhysicalPathNativeMethods]::GetFinalPathNameByHandle(
                $handle,
                $buffer,
                [uint32] $buffer.Capacity,
                0)
            if ($length -eq 0 -or $length -ge $buffer.Capacity) {
                throw 'Could not resolve a directory handle to its complete physical path.'
            }
        }
        return $buffer.ToString()
    }
    finally {
        $handle.Dispose()
    }
}

function Resolve-UnresolvedPath {
    param([string] $Path)

    return [System.IO.Path]::GetFullPath(
        $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path))
}

function Get-CanonicalDirectoryPath {
    param(
        [string] $Path,
        [string] $Label,
        [switch] $RequireAbsolute
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "$Label must be a non-empty directory path."
    }
    if ($Path.StartsWith('\\?\', [StringComparison]::Ordinal) -or
        $Path.StartsWith('\\.\', [StringComparison]::Ordinal)) {
        throw "$Label may not use an extended or device path prefix."
    }
    if ($RequireAbsolute) {
        $isDriveAbsolute = $Path -match '^[A-Za-z]:[\\/]'
        $isUncAbsolute = $Path -match '^[\\/]{2}[^\\/]+[\\/]+[^\\/]+'
        if (-not $isDriveAbsolute -and -not $isUncAbsolute) {
            throw "$Label must be a fully qualified absolute directory path."
        }
    }

    try {
        $fullPath = if ($RequireAbsolute) {
            [System.IO.Path]::GetFullPath($Path)
        }
        else {
            Resolve-UnresolvedPath -Path $Path
        }
    }
    catch {
        throw "$Label could not be canonicalized as a directory path."
    }

    $current = $fullPath
    $missingSegments = New-Object System.Collections.Generic.List[string]
    while (-not [System.IO.Directory]::Exists($current)) {
        if ([System.IO.File]::Exists($current)) {
            throw "$Label resolves to a file, not a directory."
        }

        $trimmedCurrent = $current.TrimEnd([char[]] @(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar))
        $leaf = [System.IO.Path]::GetFileName($trimmedCurrent)
        $parent = [System.IO.Path]::GetDirectoryName($trimmedCurrent)
        if ([string]::IsNullOrWhiteSpace($leaf) -or
            [string]::IsNullOrWhiteSpace($parent) -or
            [string]::Equals($parent, $current, [StringComparison]::OrdinalIgnoreCase)) {
            throw "$Label has no resolvable existing directory ancestor."
        }

        $missingSegments.Insert(0, $leaf)
        $current = $parent
    }

    try {
        $physicalPath = Get-FinalDirectoryPath -Path $current
    }
    catch {
        throw "$Label could not be resolved to a physical directory path."
    }

    if ($physicalPath.StartsWith('\\?\UNC\', [StringComparison]::OrdinalIgnoreCase)) {
        $physicalPath = '\\' + $physicalPath.Substring(8)
    }
    elseif ($physicalPath.StartsWith('\\?\', [StringComparison]::OrdinalIgnoreCase)) {
        $physicalPath = $physicalPath.Substring(4)
    }
    else {
        throw "$Label resolved to an unsupported physical path form."
    }

    foreach ($segment in $missingSegments) {
        $physicalPath = [System.IO.Path]::Combine($physicalPath, $segment)
    }
    $physicalPath = [System.IO.Path]::GetFullPath($physicalPath)
    $root = [System.IO.Path]::GetPathRoot($physicalPath)
    if ([string]::Equals($physicalPath, $root, [StringComparison]::OrdinalIgnoreCase)) {
        return $physicalPath
    }

    return $physicalPath.TrimEnd([char[]] @(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar))
}

$script:RepositoryRoot = Get-CanonicalDirectoryPath -Path $script:RepositoryRoot -Label 'repository root' -RequireAbsolute
$script:BaselinePath = Join-Path $script:RepositoryRoot 'eng\TerrariaReferences.baseline.json'

function Test-PathWithinOrEqual {
    param(
        [string] $Candidate,
        [string] $Parent
    )

    if ([string]::Equals($Candidate, $Parent, [StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    $parentPrefix = $Parent.TrimEnd([char[]] @(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)) + [System.IO.Path]::DirectorySeparatorChar
    return $Candidate.StartsWith($parentPrefix, [StringComparison]::OrdinalIgnoreCase)
}

function Assert-PathTreesDisjoint {
    param(
        [string] $Candidate,
        [string] $CandidateLabel,
        [string] $Protected,
        [string] $ProtectedLabel
    )

    if ((Test-PathWithinOrEqual -Candidate $Candidate -Parent $Protected) -or
        (Test-PathWithinOrEqual -Candidate $Protected -Parent $Candidate)) {
        throw "$CandidateLabel and $ProtectedLabel must be disjoint directory trees."
    }
}

function Assert-LegalSourceCandidateBoundaries {
    param(
        [string] $CandidateDirectory,
        [string] $CandidateLabel,
        [string] $DestinationDirectory
    )

    Assert-PathTreesDisjoint -Candidate $CandidateDirectory -CandidateLabel $CandidateLabel -Protected $script:RepositoryRoot -ProtectedLabel 'repository root'
    Assert-PathTreesDisjoint -Candidate $CandidateDirectory -CandidateLabel $CandidateLabel -Protected $script:LegacyRoot -ProtectedLabel 'read-only Legacy root'
    Assert-PathTreesDisjoint -Candidate $CandidateDirectory -CandidateLabel $CandidateLabel -Protected $DestinationDirectory -ProtectedLabel 'DestinationDirectory'
}

function Test-RegularSourceFileExists {
    param(
        [string] $Path,
        [string] $Label
    )

    if (-not [System.IO.File]::Exists($Path)) {
        return $false
    }

    $item = Get-Item -LiteralPath $Path -Force
    if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "$Label may not be a reparse point."
    }

    return $true
}

function Get-ReadOnlyLegacyRoot {
    param(
        [string] $ExplicitDirectory,
        [string] $ExplicitReproducibilityRoot
    )

    $siblingCandidate = Join-Path ([System.IO.Path]::GetDirectoryName($script:RepositoryRoot)) 'JueMingZ'
    if ([System.IO.Directory]::Exists($siblingCandidate)) {
        $siblingRoot = Get-CanonicalDirectoryPath -Path $siblingCandidate -Label 'read-only sibling Legacy root' -RequireAbsolute
        if (-not [string]::IsNullOrWhiteSpace($ExplicitDirectory)) {
            $explicitRoot = Get-CanonicalDirectoryPath -Path $ExplicitDirectory -Label 'ReadOnlyLegacyDirectory' -RequireAbsolute
            if (-not [string]::Equals($explicitRoot, $siblingRoot, [StringComparison]::OrdinalIgnoreCase)) {
                throw 'ReadOnlyLegacyDirectory may not replace the required sibling ../JueMingZ repository.'
            }
        }
        return $siblingRoot
    }

    if ([string]::IsNullOrWhiteSpace($ExplicitDirectory) -or
        -not [System.IO.Directory]::Exists($ExplicitDirectory)) {
        throw 'The required sibling Legacy repository ../JueMingZ is missing; an existing explicit read-only Legacy root is required for an isolated clean clone.'
    }
    if ([string]::IsNullOrWhiteSpace($ExplicitReproducibilityRoot) -or
        -not [System.IO.Directory]::Exists($ExplicitReproducibilityRoot)) {
        throw 'An explicit read-only Legacy root is accepted only for a verifier-owned reproducibility clone.'
    }
    $reproducibilityRoot = Get-CanonicalDirectoryPath -Path $ExplicitReproducibilityRoot -Label 'ReproducibilityRoot' -RequireAbsolute
    $repositoryParent = Get-CanonicalDirectoryPath `
        -Path ([System.IO.Path]::GetDirectoryName($script:RepositoryRoot)) `
        -Label 'repository parent' `
        -RequireAbsolute
    $systemTempRoot = Get-CanonicalDirectoryPath -Path ([System.IO.Path]::GetTempPath()) -Label 'system TEMP root' -RequireAbsolute
    $reproducibilityParent = Get-CanonicalDirectoryPath `
        -Path ([System.IO.Path]::GetDirectoryName($reproducibilityRoot)) `
        -Label 'reproducibility root parent' `
        -RequireAbsolute
    $repositoryLeaf = [System.IO.Path]::GetFileName($script:RepositoryRoot)
    if (-not [string]::Equals($repositoryParent, $reproducibilityRoot, [StringComparison]::OrdinalIgnoreCase) -or
        -not [string]::Equals($reproducibilityParent, $systemTempRoot, [StringComparison]::OrdinalIgnoreCase) -or
        [System.IO.Path]::GetFileName($reproducibilityRoot) -notmatch '^JueMingR-Repro-[0-9a-f]{32}$' -or
        ($repositoryLeaf -cne 'clone-a' -and $repositoryLeaf -cne 'clone-b-with-a-different-path')) {
        throw 'The explicit Legacy exception is limited to the two verifier-owned clones under a system TEMP reproducibility root.'
    }
    $explicitRoot = Get-CanonicalDirectoryPath -Path $ExplicitDirectory -Label 'ReadOnlyLegacyDirectory' -RequireAbsolute
    if (-not [string]::Equals([System.IO.Path]::GetFileName($explicitRoot), 'JueMingZ', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'An explicit read-only Legacy root must identify the JueMingZ directory.'
    }
    return $explicitRoot
}

$script:LegacyRoot = Get-ReadOnlyLegacyRoot `
    -ExplicitDirectory $ReadOnlyLegacyDirectory `
    -ExplicitReproducibilityRoot $ReproducibilityRoot
Assert-PathTreesDisjoint -Candidate $script:RepositoryRoot -CandidateLabel 'repository root' -Protected $script:LegacyRoot -ProtectedLabel 'read-only Legacy root'

function Get-RequiredMarkerDirectory {
    param(
        [object] $Source,
        [string] $PropertyName
    )

    $property = $Source.PSObject.Properties[$PropertyName]
    if ($null -eq $property -or -not ($property.Value -is [string]) -or
        [string]::IsNullOrWhiteSpace([string] $property.Value)) {
        throw "Prepared Terraria reference marker source.$PropertyName must be a non-empty absolute path."
    }

    return Get-CanonicalDirectoryPath -Path ([string] $property.Value) -Label ("marker source." + $PropertyName) -RequireAbsolute
}

function Get-ValidatedMarkerSourceDirectories {
    param(
        [object] $Marker,
        [object] $Baseline
    )

    $sourceProperty = $Marker.PSObject.Properties['source']
    if ($null -eq $sourceProperty -or $null -eq $sourceProperty.Value) {
        throw 'Prepared Terraria reference marker source metadata is missing.'
    }

    $unchangedProperty = $Marker.PSObject.Properties['sourceHashesUnchanged']
    if ($null -eq $unchangedProperty -or
        -not ($unchangedProperty.Value -is [bool]) -or
        $unchangedProperty.Value -ne $true) {
        throw 'Prepared Terraria reference marker must record sourceHashesUnchanged as boolean true.'
    }

    $source = $sourceProperty.Value
    $terrariaDirectory = Get-RequiredMarkerDirectory -Source $source -PropertyName 'terrariaInstallDirectory'
    $xnaDirectory = Get-RequiredMarkerDirectory -Source $source -PropertyName 'xnaReferenceDirectory'
    $channelProperty = $source.PSObject.Properties['terrariaChannel']
    $channel = if ($null -eq $channelProperty) { '' } else { [string] $channelProperty.Value }
    if ($channel -ne 'Steam' -and $channel -ne 'Explicit legal local installation') {
        throw 'Prepared Terraria reference marker has an unsupported source channel.'
    }

    if ($channel -eq 'Steam') {
        $expected = $Baseline.terrariaChannelEvidence
        foreach ($item in @(
            @{ Name = 'steamAppId'; Expected = [string] $expected.appId },
            @{ Name = 'steamStateFlags'; Expected = [string] $expected.stateFlags },
            @{ Name = 'steamBuildId'; Expected = [string] $expected.buildId })) {
            $property = $source.PSObject.Properties[$item.Name]
            if ($null -eq $property -or
                -not [string]::Equals([string] $property.Value, $item.Expected, [StringComparison]::Ordinal)) {
                throw "Prepared Terraria reference marker Steam evidence does not match the approved baseline: $($item.Name)."
            }
        }
    }

    return [ordered]@{
        terraria = $terrariaDirectory
        xna = $xnaDirectory
    }
}

function Assert-RecordedSourceFilesMatchBaseline {
    param(
        [object] $MarkerSources,
        [object] $Baseline
    )

    if (-not [System.IO.Directory]::Exists($MarkerSources.terraria) -or
        -not [System.IO.Directory]::Exists($MarkerSources.xna)) {
        throw 'Prepared Terraria reference marker source directories must still exist.'
    }

    $terrariaExpected = Get-BaselineEntry -Baseline $Baseline -LogicalName 'Terraria.exe'
    $xnaExpected = Get-BaselineEntry -Baseline $Baseline -LogicalName 'Microsoft.Xna.Framework.Game.dll'
    $sourceChecks = @(
        @{ Path = (Join-Path $MarkerSources.terraria 'Terraria.exe'); Expected = $terrariaExpected.sha256; Label = 'Terraria.exe' },
        @{ Path = (Join-Path $MarkerSources.xna 'Microsoft.Xna.Framework.Game.dll'); Expected = $xnaExpected.sha256; Label = 'Microsoft.Xna.Framework.Game.dll' }
    )
    foreach ($check in $sourceChecks) {
        if (-not [System.IO.File]::Exists($check.Path)) {
            throw "Prepared reference marker source file is missing: $($check.Label)"
        }
        $actualHash = (Get-FileHash -LiteralPath $check.Path -Algorithm SHA256).Hash.ToUpperInvariant()
        if (-not [string]::Equals($actualHash, [string] $check.Expected, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Prepared reference marker source file no longer matches the baseline: $($check.Label)"
        }
    }
}

function Get-NormalizedTextFileSha256 {
    param([string] $Path)

    $text = [System.IO.File]::ReadAllText($Path)
    $normalized = $text.Replace("`r`n", "`n").Replace("`r", "`n")
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = $script:Utf8NoBom.GetBytes($normalized)
        return ([System.BitConverter]::ToString($algorithm.ComputeHash($bytes))).Replace('-', '')
    }
    finally {
        $algorithm.Dispose()
    }
}

function Get-PublicKeyTokenText {
    param([System.Reflection.AssemblyName] $AssemblyName)

    $token = $AssemblyName.GetPublicKeyToken()
    if ($null -eq $token -or $token.Length -eq 0) {
        return ''
    }

    return (($token | ForEach-Object { $_.ToString('x2') }) -join '')
}

function Get-PeMetadata {
    param([string] $Path)

    $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
    $reader = New-Object System.IO.BinaryReader($stream)
    try {
        $stream.Position = 0x3c
        $peOffset = $reader.ReadInt32()
        $stream.Position = $peOffset
        if ($reader.ReadUInt32() -ne 0x00004550) {
            throw "Reference is not a PE file: $Path"
        }

        $machineValue = $reader.ReadUInt16()
        $sectionCount = $reader.ReadUInt16()
        $stream.Position += 12
        $optionalHeaderSize = $reader.ReadUInt16()
        $stream.Position += 2
        $optionalHeaderOffset = $stream.Position
        $magic = $reader.ReadUInt16()
        if ($magic -eq 0x10b) {
            $peFormat = 'PE32'
            $dataDirectoryOffset = $optionalHeaderOffset + 96
        }
        elseif ($magic -eq 0x20b) {
            $peFormat = 'PE32+'
            $dataDirectoryOffset = $optionalHeaderOffset + 112
        }
        else {
            throw "Reference has an unsupported PE optional header: $Path"
        }

        $stream.Position = $dataDirectoryOffset + (14 * 8)
        $clrRva = $reader.ReadUInt32()
        $clrSize = $reader.ReadUInt32()
        if ($clrRva -eq 0 -or $clrSize -eq 0) {
            throw "Reference has no CLR header: $Path"
        }

        $sectionTableOffset = $optionalHeaderOffset + $optionalHeaderSize
        $clrOffset = $null
        for ($index = 0; $index -lt $sectionCount; $index++) {
            $stream.Position = $sectionTableOffset + ($index * 40) + 8
            $virtualSize = $reader.ReadUInt32()
            $virtualAddress = $reader.ReadUInt32()
            $rawSize = $reader.ReadUInt32()
            $rawOffset = $reader.ReadUInt32()
            $mappedSize = [Math]::Max([int64] $virtualSize, [int64] $rawSize)
            if ($clrRva -ge $virtualAddress -and $clrRva -lt ($virtualAddress + $mappedSize)) {
                $clrOffset = $rawOffset + ($clrRva - $virtualAddress)
                break
            }
        }

        if ($null -eq $clrOffset) {
            throw "Reference CLR header is not mapped by a PE section: $Path"
        }

        $stream.Position = $clrOffset + 16
        $corFlags = $reader.ReadUInt32()
        $machine = switch ($machineValue) {
            0x014c { 'I386' }
            0x8664 { 'AMD64' }
            0x01c4 { 'ARMv7' }
            0xaa64 { 'ARM64' }
            default { ('0x{0:x4}' -f $machineValue) }
        }

        $flagNames = New-Object System.Collections.Generic.List[string]
        if (($corFlags -band 0x00000001) -ne 0) { $flagNames.Add('ILOnly') }
        if (($corFlags -band 0x00000002) -ne 0) { $flagNames.Add('Requires32Bit') }
        if (($corFlags -band 0x00000008) -ne 0) { $flagNames.Add('StrongNameSigned') }
        if (($corFlags -band 0x00000010) -ne 0) { $flagNames.Add('NativeEntryPoint') }
        if (($corFlags -band 0x00010000) -ne 0) { $flagNames.Add('TrackDebugData') }
        if (($corFlags -band 0x00020000) -ne 0) { $flagNames.Add('Prefers32Bit') }

        return [ordered]@{
            peMachine = $machine
            peFormat = $peFormat
            clrFlags = @($flagNames.ToArray())
            requires32Bit = (($corFlags -band 0x00000002) -ne 0)
        }
    }
    finally {
        $reader.Dispose()
        $stream.Dispose()
    }
}

function Get-AssemblyMetadata {
    param(
        [string] $Path,
        [string] $LogicalName,
        [string] $SourceCategory
    )

    if (-not [System.IO.File]::Exists($Path)) {
        throw "Required reference file is missing: $LogicalName"
    }

    $assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName($Path)
    $pe = Get-PeMetadata -Path $Path
    return [ordered]@{
        logicalName = $LogicalName
        assemblySimpleName = $assemblyName.Name
        assemblyVersion = $assemblyName.Version.ToString()
        fileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($Path).FileVersion
        publicKeyToken = Get-PublicKeyTokenText -AssemblyName $assemblyName
        peMachine = $pe.peMachine
        peFormat = $pe.peFormat
        clrFlags = @($pe.clrFlags)
        requires32Bit = $pe.requires32Bit
        sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
        sourceCategory = $SourceCategory
    }
}

function Assert-MetadataMatchesBaseline {
    param(
        [object] $Actual,
        [object] $Expected
    )

    $properties = @(
        'logicalName',
        'assemblySimpleName',
        'assemblyVersion',
        'fileVersion',
        'publicKeyToken',
        'peMachine',
        'peFormat',
        'requires32Bit',
        'sha256',
        'sourceCategory'
    )
    foreach ($property in $properties) {
        $actualValue = if ($Actual -is [System.Collections.IDictionary]) {
            [string] $Actual[$property]
        }
        else {
            $actualProperty = $Actual.PSObject.Properties[$property]
            if ($null -eq $actualProperty) { '' } else { [string] $actualProperty.Value }
        }
        $expectedValue = [string] $Expected.$property
        if (-not [string]::Equals($actualValue, $expectedValue, [StringComparison]::Ordinal)) {
            throw "Reference identity mismatch for $($Expected.logicalName): $property expected '$expectedValue', actual '$actualValue'."
        }
    }

    $actualFlags = @($Actual.clrFlags) -join ','
    $expectedFlags = @($Expected.clrFlags) -join ','
    if (-not [string]::Equals($actualFlags, $expectedFlags, [StringComparison]::Ordinal)) {
        throw "Reference identity mismatch for $($Expected.logicalName): clrFlags expected '$expectedFlags', actual '$actualFlags'."
    }

    if ($Expected.redistributableByJueMingR -ne $false -or $Expected.copyLocal -ne $false) {
        throw "Baseline redistribution/copy-local policy is invalid for $($Expected.logicalName)."
    }
}

function Get-Baseline {
    if (-not [System.IO.File]::Exists($script:BaselinePath)) {
        throw "Reference baseline is missing: eng/TerrariaReferences.baseline.json"
    }

    $baseline = Get-Content -LiteralPath $script:BaselinePath -Raw | ConvertFrom-Json
    if ($baseline.schemaVersion -ne 1 -or $baseline.files.Count -ne 3) {
        throw 'Reference baseline has an unsupported schema or file count.'
    }

    return $baseline
}

function Get-BaselineEntry {
    param(
        [object] $Baseline,
        [string] $LogicalName
    )

    $entries = @($Baseline.files | Where-Object { $_.logicalName -eq $LogicalName })
    if ($entries.Count -ne 1) {
        throw "Reference baseline must contain exactly one entry for $LogicalName."
    }

    return $entries[0]
}

function Get-VdfValue {
    param(
        [string] $Text,
        [string] $Name
    )

    $match = [regex]::Match($Text, '(?im)^\s*"' + [regex]::Escape($Name) + '"\s+"([^"]*)"')
    if (-not $match.Success) {
        return $null
    }

    return $match.Groups[1].Value
}

function Resolve-TerrariaSource {
    param(
        [string] $ExplicitDirectory,
        [string] $DestinationDirectory
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitDirectory)) {
        $directory = Get-CanonicalDirectoryPath -Path $ExplicitDirectory -Label 'Terraria source candidate'
        Assert-LegalSourceCandidateBoundaries `
            -CandidateDirectory $directory `
            -CandidateLabel 'Terraria source candidate' `
            -DestinationDirectory $DestinationDirectory
        $exe = Join-Path $directory 'Terraria.exe'
        if (-not (Test-RegularSourceFileExists -Path $exe -Label 'Terraria.exe source candidate')) {
            throw 'The specified Terraria installation directory does not contain Terraria.exe.'
        }

        return [ordered]@{
            directory = $directory
            exe = $exe
            channel = 'Explicit legal local installation'
            appId = ''
            stateFlags = ''
            buildId = ''
            appManifest = ''
        }
    }

    $steamKey = Get-ItemProperty -LiteralPath 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue
    if ($null -eq $steamKey -or [string]::IsNullOrWhiteSpace([string] $steamKey.SteamPath)) {
        throw 'Terraria installation was not specified and the current-user Steam root was not found.'
    }

    $steamRoot = Get-CanonicalDirectoryPath `
        -Path ([string] $steamKey.SteamPath).Replace('/', '\') `
        -Label 'Steam source candidate' `
        -RequireAbsolute
    Assert-LegalSourceCandidateBoundaries `
        -CandidateDirectory $steamRoot `
        -CandidateLabel 'Steam source candidate' `
        -DestinationDirectory $DestinationDirectory
    $libraries = New-Object System.Collections.Generic.List[string]
    $libraries.Add($steamRoot)
    $libraryFile = Join-Path $steamRoot 'steamapps\libraryfolders.vdf'
    if (Test-RegularSourceFileExists -Path $libraryFile -Label 'Steam libraryfolders.vdf source candidate') {
        $libraryText = [System.IO.File]::ReadAllText($libraryFile)
        foreach ($match in [regex]::Matches($libraryText, '(?im)^\s*"path"\s+"([^"]+)"')) {
            $path = $match.Groups[1].Value.Replace('\\', '\')
            if (-not [string]::IsNullOrWhiteSpace($path)) {
                $library = Get-CanonicalDirectoryPath -Path $path -Label 'Steam library source candidate' -RequireAbsolute
                Assert-LegalSourceCandidateBoundaries `
                    -CandidateDirectory $library `
                    -CandidateLabel 'Steam library source candidate' `
                    -DestinationDirectory $DestinationDirectory
                $libraries.Add($library)
            }
        }
    }

    foreach ($library in $libraries | Select-Object -Unique) {
        $library = Get-CanonicalDirectoryPath -Path $library -Label 'Steam library source candidate' -RequireAbsolute
        Assert-LegalSourceCandidateBoundaries `
            -CandidateDirectory $library `
            -CandidateLabel 'Steam library source candidate' `
            -DestinationDirectory $DestinationDirectory
        $manifestPath = Join-Path $library 'steamapps\appmanifest_105600.acf'
        if (-not (Test-RegularSourceFileExists -Path $manifestPath -Label 'Steam app manifest source candidate')) {
            continue
        }

        $manifestText = [System.IO.File]::ReadAllText($manifestPath)
        $installDir = Get-VdfValue -Text $manifestText -Name 'installdir'
        $stateFlags = Get-VdfValue -Text $manifestText -Name 'StateFlags'
        $buildId = Get-VdfValue -Text $manifestText -Name 'buildid'
        $parsedStateFlags = 0
        if ([string]::IsNullOrWhiteSpace($installDir) -or
            -not [int]::TryParse($stateFlags, [ref] $parsedStateFlags) -or
            (($parsedStateFlags -band 4) -eq 0)) {
            continue
        }

        $directory = Get-CanonicalDirectoryPath `
            -Path (Join-Path $library ('steamapps\common\' + $installDir)) `
            -Label 'Terraria Steam installation source candidate' `
            -RequireAbsolute
        Assert-LegalSourceCandidateBoundaries `
            -CandidateDirectory $directory `
            -CandidateLabel 'Terraria Steam installation source candidate' `
            -DestinationDirectory $DestinationDirectory
        $exe = Join-Path $directory 'Terraria.exe'
        if (Test-RegularSourceFileExists -Path $exe -Label 'Terraria.exe source candidate') {
            return [ordered]@{
                directory = $directory
                exe = $exe
                channel = 'Steam'
                appId = '105600'
                stateFlags = $stateFlags
                buildId = $buildId
                appManifest = [System.IO.Path]::GetFullPath($manifestPath)
            }
        }
    }

    throw 'A complete Steam Terraria installation for app 105600 was not found in registered Steam libraries.'
}

function Resolve-XnaSource {
    param(
        [string] $ExplicitDirectory,
        [object] $Expected,
        [string] $DestinationDirectory
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitDirectory)) {
        $directory = Get-CanonicalDirectoryPath -Path $ExplicitDirectory -Label 'XNA source candidate'
        Assert-LegalSourceCandidateBoundaries `
            -CandidateDirectory $directory `
            -CandidateLabel 'XNA source candidate' `
            -DestinationDirectory $DestinationDirectory
        $path = Join-Path $directory $Expected.logicalName
        if (-not (Test-RegularSourceFileExists -Path $path -Label 'XNA source candidate')) {
            throw "The specified XNA reference directory is missing $($Expected.logicalName)."
        }

        return [ordered]@{ directory = $directory; path = $path }
    }

    $xnaKey = Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\XNA\Framework\v4.0' -ErrorAction SilentlyContinue
    if ($null -eq $xnaKey -or $xnaKey.Installed -ne 1 -or $xnaKey.Refresh1Installed -ne 1) {
        throw 'Microsoft XNA Framework 4.0 Refresh installation evidence was not found.'
    }

    $windowsRoot = [Environment]::GetFolderPath([Environment+SpecialFolder]::Windows)
    if ([string]::IsNullOrWhiteSpace($windowsRoot)) {
        throw 'The Windows system directory could not be resolved.'
    }
    $assemblyRoot = Get-CanonicalDirectoryPath `
        -Path (Join-Path $windowsRoot 'Microsoft.NET\assembly\GAC_32\Microsoft.Xna.Framework.Game') `
        -Label 'XNA GAC source candidate' `
        -RequireAbsolute
    Assert-LegalSourceCandidateBoundaries `
        -CandidateDirectory $assemblyRoot `
        -CandidateLabel 'XNA GAC source candidate' `
        -DestinationDirectory $DestinationDirectory
    if (-not [System.IO.Directory]::Exists($assemblyRoot)) {
        throw 'Microsoft XNA Framework Game GAC_32 directory was not found.'
    }

    $pendingDirectories = New-Object 'System.Collections.Generic.Stack[string]'
    $pendingDirectories.Push($assemblyRoot)
    while ($pendingDirectories.Count -gt 0) {
        $currentDirectory = Get-CanonicalDirectoryPath `
            -Path $pendingDirectories.Pop() `
            -Label 'XNA GAC candidate directory' `
            -RequireAbsolute
        Assert-LegalSourceCandidateBoundaries `
            -CandidateDirectory $currentDirectory `
            -CandidateLabel 'XNA GAC candidate directory' `
            -DestinationDirectory $DestinationDirectory
        $currentItem = Get-Item -LiteralPath $currentDirectory -Force
        if (($currentItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'An XNA GAC candidate directory may not be a reparse point.'
        }

        foreach ($entry in @($currentItem.EnumerateFileSystemInfos() | Sort-Object -Property Name)) {
            if (($entry.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw 'The XNA GAC candidate tree may not contain reparse points.'
            }
            if (($entry.Attributes -band [System.IO.FileAttributes]::Directory) -ne 0) {
                $pendingDirectories.Push($entry.FullName)
                continue
            }
            if (-not [string]::Equals($entry.Name, [string] $Expected.logicalName, [StringComparison]::OrdinalIgnoreCase)) {
                continue
            }

            try {
                $metadata = Get-AssemblyMetadata -Path $entry.FullName -LogicalName $Expected.logicalName -SourceCategory $Expected.sourceCategory
                Assert-MetadataMatchesBaseline -Actual $metadata -Expected $Expected
                return [ordered]@{ directory = $currentDirectory; path = $entry.FullName }
            }
            catch {
                continue
            }
        }
    }

    throw 'No installed GAC_32 XNA Game assembly matches the approved reference baseline.'
}

function Export-EmbeddedReLogic {
    param(
        [string] $TerrariaExe,
        [string] $Destination,
        [string] $ResourceName
    )

    $assembly = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($TerrariaExe)
    if (-not $assembly.ReflectionOnly) {
        throw 'Terraria metadata inspection did not use the reflection-only context.'
    }

    $matchingResources = @($assembly.GetManifestResourceNames() | Where-Object { $_ -eq $ResourceName })
    if ($matchingResources.Count -ne 1) {
        throw "Embedded ReLogic resource is missing or ambiguous: $ResourceName"
    }

    $resourceStream = $assembly.GetManifestResourceStream($ResourceName)
    if ($null -eq $resourceStream) {
        throw "Embedded ReLogic resource could not be opened: $ResourceName"
    }

    try {
        $output = [System.IO.File]::Open($Destination, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        try {
            $resourceStream.CopyTo($output)
        }
        finally {
            $output.Dispose()
        }
    }
    finally {
        $resourceStream.Dispose()
    }
}

function Test-PreparedReferenceDirectory {
    param(
        [string] $Directory,
        [object] $Baseline,
        [string] $BaselineHash
    )

    if (-not [System.IO.Directory]::Exists($Directory)) {
        throw "Prepared Terraria reference directory is missing: $Directory"
    }

    $markerPath = Join-Path $Directory $script:MarkerName
    if (-not [System.IO.File]::Exists($markerPath)) {
        throw "Prepared Terraria reference marker is missing: $script:MarkerName"
    }

    $marker = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
    if ($marker.schemaVersion -ne 1 -or
        $marker.generator -ne $script:GeneratorIdentity -or
        $marker.profileId -ne $Baseline.profileId -or
        $marker.baselineSha256 -ne $BaselineHash) {
        throw 'Prepared Terraria reference marker does not match the approved baseline.'
    }

    $preparedDirectory = Get-CanonicalDirectoryPath -Path $Directory -Label 'prepared reference directory'
    if (Test-PathWithinOrEqual -Candidate $script:RepositoryRoot -Parent $preparedDirectory) {
        throw 'Prepared reference directory may not equal or contain the repository root.'
    }
    $markerSources = Get-ValidatedMarkerSourceDirectories -Marker $marker -Baseline $Baseline
    Assert-PathTreesDisjoint -Candidate $markerSources.terraria -CandidateLabel 'Terraria source directory' -Protected $script:RepositoryRoot -ProtectedLabel 'repository root'
    Assert-PathTreesDisjoint -Candidate $markerSources.xna -CandidateLabel 'XNA source directory' -Protected $script:RepositoryRoot -ProtectedLabel 'repository root'
    Assert-PathTreesDisjoint -Candidate $markerSources.terraria -CandidateLabel 'Terraria source directory' -Protected $script:LegacyRoot -ProtectedLabel 'read-only Legacy root'
    Assert-PathTreesDisjoint -Candidate $markerSources.xna -CandidateLabel 'XNA source directory' -Protected $script:LegacyRoot -ProtectedLabel 'read-only Legacy root'
    Assert-PathTreesDisjoint -Candidate $preparedDirectory -CandidateLabel 'prepared reference directory' -Protected $markerSources.terraria -ProtectedLabel 'Terraria source directory'
    Assert-PathTreesDisjoint -Candidate $preparedDirectory -CandidateLabel 'prepared reference directory' -Protected $markerSources.xna -ProtectedLabel 'XNA source directory'
    Assert-RecordedSourceFilesMatchBaseline -MarkerSources $markerSources -Baseline $Baseline

    $metadata = New-Object System.Collections.Generic.List[object]
    foreach ($expected in $Baseline.files) {
        $path = Join-Path $Directory $expected.logicalName
        $actual = Get-AssemblyMetadata -Path $path -LogicalName $expected.logicalName -SourceCategory $expected.sourceCategory
        Assert-MetadataMatchesBaseline -Actual $actual -Expected $expected
        $metadata.Add($actual)
    }

    $expectedNames = @($Baseline.files | ForEach-Object { $_.logicalName })
    foreach ($binary in Get-ChildItem -LiteralPath $Directory -File | Where-Object { $_.Extension -eq '.dll' -or $_.Extension -eq '.exe' }) {
        if ($expectedNames -notcontains $binary.Name) {
            throw "Prepared Terraria reference directory contains an unexpected binary: $($binary.Name)"
        }
    }

    return $metadata
}

function Assert-ForceReplaceablePreparedDirectory {
    param(
        [string] $Directory,
        [object] $Baseline,
        [string] $BaselineHash,
        [string] $CurrentTerrariaSource,
        [string] $CurrentXnaSource
    )

    $allowedNames = @($script:MarkerName) + @($Baseline.files | ForEach-Object { [string] $_.logicalName })
    $directoryInfo = New-Object System.IO.DirectoryInfo -ArgumentList $Directory
    foreach ($entry in $directoryInfo.EnumerateFileSystemInfos()) {
        if (($entry.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 -or
            ($entry.Attributes -band [System.IO.FileAttributes]::Directory) -ne 0 -or
            $allowedNames -notcontains $entry.Name) {
            throw "-Force refused: existing destination contains an unapproved or non-file entry: $($entry.Name)"
        }
    }

    $markerPath = Join-Path $Directory $script:MarkerName
    if (-not [System.IO.File]::Exists($markerPath)) {
        throw '-Force refused: the existing destination has no generator marker.'
    }
    $marker = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
    if ($marker.schemaVersion -ne 1 -or
        $marker.generator -ne $script:GeneratorIdentity -or
        $marker.profileId -ne $Baseline.profileId -or
        $marker.baselineSha256 -ne $BaselineHash) {
        throw '-Force refused: the existing destination marker identity is not approved.'
    }

    $markerSources = Get-ValidatedMarkerSourceDirectories -Marker $marker -Baseline $Baseline
    if (-not [string]::Equals($markerSources.terraria, $CurrentTerrariaSource, [StringComparison]::OrdinalIgnoreCase) -or
        -not [string]::Equals($markerSources.xna, $CurrentXnaSource, [StringComparison]::OrdinalIgnoreCase)) {
        throw '-Force refused: the existing destination marker does not identify the current legal sources.'
    }
    Assert-PathTreesDisjoint -Candidate $markerSources.terraria -CandidateLabel 'marker Terraria source directory' -Protected $script:RepositoryRoot -ProtectedLabel 'repository root'
    Assert-PathTreesDisjoint -Candidate $markerSources.xna -CandidateLabel 'marker XNA source directory' -Protected $script:RepositoryRoot -ProtectedLabel 'repository root'
    Assert-PathTreesDisjoint -Candidate $markerSources.terraria -CandidateLabel 'marker Terraria source directory' -Protected $script:LegacyRoot -ProtectedLabel 'read-only Legacy root'
    Assert-PathTreesDisjoint -Candidate $markerSources.xna -CandidateLabel 'marker XNA source directory' -Protected $script:LegacyRoot -ProtectedLabel 'read-only Legacy root'
    Assert-PathTreesDisjoint -Candidate $Directory -CandidateLabel 'existing DestinationDirectory' -Protected $markerSources.terraria -ProtectedLabel 'marker Terraria source directory'
    Assert-PathTreesDisjoint -Candidate $Directory -CandidateLabel 'existing DestinationDirectory' -Protected $markerSources.xna -ProtectedLabel 'marker XNA source directory'
    Assert-RecordedSourceFilesMatchBaseline -MarkerSources $markerSources -Baseline $Baseline

    $markerFiles = @($marker.files)
    if ($markerFiles.Count -ne $Baseline.files.Count) {
        throw '-Force refused: the existing destination marker does not contain the complete baseline identity set.'
    }
    foreach ($expected in $Baseline.files) {
        $matchingFiles = @($markerFiles | Where-Object { $_.logicalName -eq $expected.logicalName })
        if ($matchingFiles.Count -ne 1) {
            throw "-Force refused: the existing destination marker has an invalid identity count for $($expected.logicalName)."
        }
        $matchingFile = $matchingFiles | Select-Object -First 1
        Assert-MetadataMatchesBaseline -Actual $matchingFile -Expected $expected
    }
}

if ($InspectOnly -and $VerifyOnly) {
    throw 'InspectOnly and VerifyOnly cannot be used together.'
}

$baseline = Get-Baseline
$baselineHash = Get-NormalizedTextFileSha256 -Path $script:BaselinePath
if ([string]::IsNullOrWhiteSpace($DestinationDirectory)) {
    $DestinationDirectory = Join-Path $script:RepositoryRoot 'external\TerrariaRefs'
}

$destination = Get-CanonicalDirectoryPath -Path $DestinationDirectory -Label 'DestinationDirectory'
Assert-PathTreesDisjoint -Candidate $destination -CandidateLabel 'DestinationDirectory' -Protected $script:LegacyRoot -ProtectedLabel 'read-only Legacy root'
if (Test-PathWithinOrEqual -Candidate $script:RepositoryRoot -Parent $destination) {
    throw 'DestinationDirectory may not equal or contain the repository root.'
}
if ($VerifyOnly) {
    $verified = Test-PreparedReferenceDirectory -Directory $destination -Baseline $baseline -BaselineHash $baselineHash
    Write-Output ("PASS: verified {0} local compile references against profile {1}." -f $verified.Count, $baseline.profileId)
    return
}

$terraria = Resolve-TerrariaSource -ExplicitDirectory $TerrariaInstallDirectory -DestinationDirectory $destination
$xnaExpected = Get-BaselineEntry -Baseline $baseline -LogicalName 'Microsoft.Xna.Framework.Game.dll'
$xna = Resolve-XnaSource -ExplicitDirectory $XnaReferenceDirectory -Expected $xnaExpected -DestinationDirectory $destination
$terrariaSourceDirectory = Get-CanonicalDirectoryPath -Path $terraria.directory -Label 'Terraria source directory' -RequireAbsolute
$xnaSourceDirectory = Get-CanonicalDirectoryPath -Path $xna.directory -Label 'XNA source directory' -RequireAbsolute
Assert-PathTreesDisjoint -Candidate $destination -CandidateLabel 'DestinationDirectory' -Protected $terrariaSourceDirectory -ProtectedLabel 'Terraria source directory'
Assert-PathTreesDisjoint -Candidate $destination -CandidateLabel 'DestinationDirectory' -Protected $xnaSourceDirectory -ProtectedLabel 'XNA source directory'
Assert-PathTreesDisjoint -Candidate $terrariaSourceDirectory -CandidateLabel 'Terraria source directory' -Protected $script:RepositoryRoot -ProtectedLabel 'repository root'
Assert-PathTreesDisjoint -Candidate $xnaSourceDirectory -CandidateLabel 'XNA source directory' -Protected $script:RepositoryRoot -ProtectedLabel 'repository root'
Assert-PathTreesDisjoint -Candidate $terrariaSourceDirectory -CandidateLabel 'Terraria source directory' -Protected $script:LegacyRoot -ProtectedLabel 'read-only Legacy root'
Assert-PathTreesDisjoint -Candidate $xnaSourceDirectory -CandidateLabel 'XNA source directory' -Protected $script:LegacyRoot -ProtectedLabel 'read-only Legacy root'
$terrariaHashBefore = (Get-FileHash -LiteralPath $terraria.exe -Algorithm SHA256).Hash.ToUpperInvariant()
$xnaHashBefore = (Get-FileHash -LiteralPath $xna.path -Algorithm SHA256).Hash.ToUpperInvariant()
$temporaryDirectory = Get-CanonicalDirectoryPath `
    -Path (Join-Path ([System.IO.Path]::GetTempPath()) ('JueMingR-TerrariaRefs-' + [Guid]::NewGuid().ToString('N'))) `
    -Label 'temporary preparation directory'
Assert-PathTreesDisjoint -Candidate $temporaryDirectory -CandidateLabel 'temporary preparation directory' -Protected $terrariaSourceDirectory -ProtectedLabel 'Terraria source directory'
Assert-PathTreesDisjoint -Candidate $temporaryDirectory -CandidateLabel 'temporary preparation directory' -Protected $xnaSourceDirectory -ProtectedLabel 'XNA source directory'
Assert-PathTreesDisjoint -Candidate $temporaryDirectory -CandidateLabel 'temporary preparation directory' -Protected $destination -ProtectedLabel 'DestinationDirectory'
Assert-PathTreesDisjoint -Candidate $temporaryDirectory -CandidateLabel 'temporary preparation directory' -Protected $script:RepositoryRoot -ProtectedLabel 'repository root'
Assert-PathTreesDisjoint -Candidate $temporaryDirectory -CandidateLabel 'temporary preparation directory' -Protected $script:LegacyRoot -ProtectedLabel 'read-only Legacy root'
[System.IO.Directory]::CreateDirectory($temporaryDirectory) | Out-Null
$stagingDirectory = $null
$backupDirectory = $null
try {
    $terrariaDestination = Join-Path $temporaryDirectory 'Terraria.exe'
    [System.IO.File]::Copy($terraria.exe, $terrariaDestination, $false)

    $reLogicExpected = Get-BaselineEntry -Baseline $baseline -LogicalName 'ReLogic.dll'
    $reLogicDestination = Join-Path $temporaryDirectory 'ReLogic.dll'
    Export-EmbeddedReLogic -TerrariaExe $terraria.exe -Destination $reLogicDestination -ResourceName $reLogicExpected.embeddedResourceName

    $xnaDestination = Join-Path $temporaryDirectory 'Microsoft.Xna.Framework.Game.dll'
    [System.IO.File]::Copy($xna.path, $xnaDestination, $false)

    $preparedMetadata = New-Object System.Collections.Generic.List[object]
    foreach ($expected in $baseline.files) {
        $actual = Get-AssemblyMetadata -Path (Join-Path $temporaryDirectory $expected.logicalName) -LogicalName $expected.logicalName -SourceCategory $expected.sourceCategory
        Assert-MetadataMatchesBaseline -Actual $actual -Expected $expected
        $preparedMetadata.Add($actual)
    }

    $terrariaHashAfter = (Get-FileHash -LiteralPath $terraria.exe -Algorithm SHA256).Hash.ToUpperInvariant()
    $xnaHashAfter = (Get-FileHash -LiteralPath $xna.path -Algorithm SHA256).Hash.ToUpperInvariant()
    if ($terrariaHashAfter -ne $terrariaHashBefore -or $xnaHashAfter -ne $xnaHashBefore) {
        throw 'A legal source reference changed while it was being inspected.'
    }

    Write-Output ("PASS: inspected legal compile inputs for profile {0}; 3 files match the baseline." -f $baseline.profileId)
    if ($InspectOnly) {
        return
    }

    if ([System.IO.Directory]::Exists($destination)) {
        try {
            $null = Test-PreparedReferenceDirectory -Directory $destination -Baseline $baseline -BaselineHash $baselineHash
            Write-Output 'PASS: destination already contains the complete verified reference set; no files changed.'
            return
        }
        catch {
            if (-not $Force) {
                throw "Destination exists but is not a complete verified reference set. Remove it manually or use -Force only if it was generated by this script. Detail: $($_.Exception.Message)"
            }

            Assert-ForceReplaceablePreparedDirectory `
                -Directory $destination `
                -Baseline $baseline `
                -BaselineHash $baselineHash `
                -CurrentTerrariaSource $terrariaSourceDirectory `
                -CurrentXnaSource $xnaSourceDirectory
        }
    }

    $destinationParent = [System.IO.Path]::GetDirectoryName($destination)
    if ([string]::IsNullOrWhiteSpace($destinationParent) -or $destination -eq $script:RepositoryRoot) {
        throw 'DestinationDirectory must be a dedicated child directory, not the repository root.'
    }

    $stagingDirectory = Join-Path $destinationParent ('.JueMingR-TerrariaRefs-staging-' + [Guid]::NewGuid().ToString('N'))
    $stagingDirectory = Get-CanonicalDirectoryPath -Path $stagingDirectory -Label 'reference staging directory'
    Assert-PathTreesDisjoint -Candidate $stagingDirectory -CandidateLabel 'reference staging directory' -Protected $script:LegacyRoot -ProtectedLabel 'read-only Legacy root'
    [System.IO.Directory]::CreateDirectory($destinationParent) | Out-Null
    [System.IO.Directory]::CreateDirectory($stagingDirectory) | Out-Null
    foreach ($expected in $baseline.files) {
        [System.IO.File]::Copy(
            (Join-Path $temporaryDirectory $expected.logicalName),
            (Join-Path $stagingDirectory $expected.logicalName),
            $false)
    }

    $marker = [ordered]@{
        schemaVersion = 1
        generator = $script:GeneratorIdentity
        profileId = $baseline.profileId
        baselineSha256 = $baselineHash
        preparedAtUtc = [DateTime]::UtcNow.ToString('o')
        source = [ordered]@{
            terrariaInstallDirectory = $terrariaSourceDirectory
            terrariaChannel = $terraria.channel
            steamAppId = $terraria.appId
            steamStateFlags = $terraria.stateFlags
            steamBuildId = $terraria.buildId
            xnaReferenceDirectory = $xnaSourceDirectory
        }
        sourceHashesUnchanged = $true
        files = @($preparedMetadata.ToArray())
    }
    [System.IO.File]::WriteAllText(
        (Join-Path $stagingDirectory $script:MarkerName),
        (($marker | ConvertTo-Json -Depth 10) + [Environment]::NewLine),
        $script:Utf8NoBom)

    $null = Test-PreparedReferenceDirectory -Directory $stagingDirectory -Baseline $baseline -BaselineHash $baselineHash
    if ([System.IO.Directory]::Exists($destination)) {
        $backupDirectory = Join-Path $destinationParent ('.JueMingR-TerrariaRefs-backup-' + [Guid]::NewGuid().ToString('N'))
        $backupDirectory = Get-CanonicalDirectoryPath -Path $backupDirectory -Label 'reference backup directory'
        Assert-PathTreesDisjoint -Candidate $backupDirectory -CandidateLabel 'reference backup directory' -Protected $script:LegacyRoot -ProtectedLabel 'read-only Legacy root'
        [System.IO.Directory]::Move($destination, $backupDirectory)
    }

    try {
        [System.IO.Directory]::Move($stagingDirectory, $destination)
        $stagingDirectory = $null
        $null = Test-PreparedReferenceDirectory -Directory $destination -Baseline $baseline -BaselineHash $baselineHash
        if ($null -ne $backupDirectory -and [System.IO.Directory]::Exists($backupDirectory)) {
            [System.IO.Directory]::Delete($backupDirectory, $true)
            $backupDirectory = $null
        }
    }
    catch {
        if (-not [System.IO.Directory]::Exists($destination) -and
            $null -ne $backupDirectory -and
            [System.IO.Directory]::Exists($backupDirectory)) {
            [System.IO.Directory]::Move($backupDirectory, $destination)
            $backupDirectory = $null
        }

        throw
    }

    Write-Output ("PASS: prepared 3 verified local compile references at the requested destination. Profile: {0}." -f $baseline.profileId)
    Write-Output 'The files remain local, are not redistributable by JueMingR, and do not establish runtime support.'
}
finally {
    if ([System.IO.Directory]::Exists($temporaryDirectory)) {
        [System.IO.Directory]::Delete($temporaryDirectory, $true)
    }

    if ($null -ne $stagingDirectory -and [System.IO.Directory]::Exists($stagingDirectory)) {
        [System.IO.Directory]::Delete($stagingDirectory, $true)
    }

    if ($null -ne $backupDirectory -and [System.IO.Directory]::Exists($backupDirectory)) {
        if (-not [System.IO.Directory]::Exists($destination)) {
            [System.IO.Directory]::Move($backupDirectory, $destination)
        }
    }
}
