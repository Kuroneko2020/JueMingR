[CmdletBinding()]
param(
    [string] $TerrariaInstallDirectory,
    [string] $XnaReferenceDirectory,
    [string] $DestinationDirectory,
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

function Resolve-UnresolvedPath {
    param([string] $Path)

    return [System.IO.Path]::GetFullPath(
        $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path))
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
        $actualValue = [string] $Actual[$property]
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
    param([string] $ExplicitDirectory)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitDirectory)) {
        $directory = Resolve-UnresolvedPath -Path $ExplicitDirectory
        $exe = Join-Path $directory 'Terraria.exe'
        if (-not [System.IO.File]::Exists($exe)) {
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

    $steamRoot = [System.IO.Path]::GetFullPath(([string] $steamKey.SteamPath).Replace('/', '\'))
    $libraries = New-Object System.Collections.Generic.List[string]
    $libraries.Add($steamRoot)
    $libraryFile = Join-Path $steamRoot 'steamapps\libraryfolders.vdf'
    if ([System.IO.File]::Exists($libraryFile)) {
        $libraryText = [System.IO.File]::ReadAllText($libraryFile)
        foreach ($match in [regex]::Matches($libraryText, '(?im)^\s*"path"\s+"([^"]+)"')) {
            $path = $match.Groups[1].Value.Replace('\\', '\')
            if (-not [string]::IsNullOrWhiteSpace($path)) {
                $libraries.Add([System.IO.Path]::GetFullPath($path))
            }
        }
    }

    foreach ($library in $libraries | Select-Object -Unique) {
        $manifestPath = Join-Path $library 'steamapps\appmanifest_105600.acf'
        if (-not [System.IO.File]::Exists($manifestPath)) {
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

        $directory = Join-Path $library ('steamapps\common\' + $installDir)
        $exe = Join-Path $directory 'Terraria.exe'
        if ([System.IO.File]::Exists($exe)) {
            return [ordered]@{
                directory = [System.IO.Path]::GetFullPath($directory)
                exe = [System.IO.Path]::GetFullPath($exe)
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
        [object] $Expected
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitDirectory)) {
        $directory = Resolve-UnresolvedPath -Path $ExplicitDirectory
        $path = Join-Path $directory $Expected.logicalName
        if (-not [System.IO.File]::Exists($path)) {
            throw "The specified XNA reference directory is missing $($Expected.logicalName)."
        }

        return [ordered]@{ directory = $directory; path = $path }
    }

    $xnaKey = Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\XNA\Framework\v4.0' -ErrorAction SilentlyContinue
    if ($null -eq $xnaKey -or $xnaKey.Installed -ne 1 -or $xnaKey.Refresh1Installed -ne 1) {
        throw 'Microsoft XNA Framework 4.0 Refresh installation evidence was not found.'
    }

    $assemblyRoot = Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_32\Microsoft.Xna.Framework.Game'
    if (-not [System.IO.Directory]::Exists($assemblyRoot)) {
        throw 'Microsoft XNA Framework Game GAC_32 directory was not found.'
    }

    foreach ($candidate in Get-ChildItem -LiteralPath $assemblyRoot -Filter $Expected.logicalName -File -Recurse) {
        try {
            $metadata = Get-AssemblyMetadata -Path $candidate.FullName -LogicalName $Expected.logicalName -SourceCategory $Expected.sourceCategory
            Assert-MetadataMatchesBaseline -Actual $metadata -Expected $Expected
            return [ordered]@{ directory = $candidate.DirectoryName; path = $candidate.FullName }
        }
        catch {
            continue
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

    $bytes = [System.IO.File]::ReadAllBytes($Destination)
    $reLogicAssembly = [System.Reflection.Assembly]::ReflectionOnlyLoad($bytes)
    if (-not $reLogicAssembly.ReflectionOnly) {
        throw 'Extracted ReLogic metadata inspection did not use the reflection-only context.'
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

if ($InspectOnly -and $VerifyOnly) {
    throw 'InspectOnly and VerifyOnly cannot be used together.'
}

$baseline = Get-Baseline
$baselineHash = (Get-FileHash -LiteralPath $script:BaselinePath -Algorithm SHA256).Hash.ToUpperInvariant()
if ([string]::IsNullOrWhiteSpace($DestinationDirectory)) {
    $DestinationDirectory = Join-Path $script:RepositoryRoot 'external\TerrariaRefs'
}

$destination = Resolve-UnresolvedPath -Path $DestinationDirectory
if ($VerifyOnly) {
    $verified = Test-PreparedReferenceDirectory -Directory $destination -Baseline $baseline -BaselineHash $baselineHash
    Write-Output ("PASS: verified {0} local compile references against profile {1}." -f $verified.Count, $baseline.profileId)
    return
}

$terraria = Resolve-TerrariaSource -ExplicitDirectory $TerrariaInstallDirectory
$xnaExpected = Get-BaselineEntry -Baseline $baseline -LogicalName 'Microsoft.Xna.Framework.Game.dll'
$xna = Resolve-XnaSource -ExplicitDirectory $XnaReferenceDirectory -Expected $xnaExpected
$terrariaHashBefore = (Get-FileHash -LiteralPath $terraria.exe -Algorithm SHA256).Hash.ToUpperInvariant()
$xnaHashBefore = (Get-FileHash -LiteralPath $xna.path -Algorithm SHA256).Hash.ToUpperInvariant()
$temporaryDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ('JueMingR-TerrariaRefs-' + [Guid]::NewGuid().ToString('N'))
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

            $existingMarkerPath = Join-Path $destination $script:MarkerName
            if (-not [System.IO.File]::Exists($existingMarkerPath)) {
                throw '-Force refused: the existing destination has no generator marker.'
            }

            $existingMarker = Get-Content -LiteralPath $existingMarkerPath -Raw | ConvertFrom-Json
            if ($existingMarker.generator -ne $script:GeneratorIdentity) {
                throw '-Force refused: the existing destination was not generated by this script.'
            }
        }
    }

    $destinationParent = [System.IO.Path]::GetDirectoryName($destination)
    if ([string]::IsNullOrWhiteSpace($destinationParent) -or $destination -eq $script:RepositoryRoot) {
        throw 'DestinationDirectory must be a dedicated child directory, not the repository root.'
    }

    [System.IO.Directory]::CreateDirectory($destinationParent) | Out-Null
    $stagingDirectory = Join-Path $destinationParent ('.JueMingR-TerrariaRefs-staging-' + [Guid]::NewGuid().ToString('N'))
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
            terrariaInstallDirectory = $terraria.directory
            terrariaChannel = $terraria.channel
            steamAppId = $terraria.appId
            steamStateFlags = $terraria.stateFlags
            steamBuildId = $terraria.buildId
            xnaReferenceDirectory = $xna.directory
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
