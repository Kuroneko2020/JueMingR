[CmdletBinding()]
param(
    [string] $TerrariaExePath,
    [string] $XnaGameAssemblyPath,
    [string] $XnaFrameworkAssemblyPath,
    [string] $XnaGraphicsAssemblyPath,
    [switch] $VerifyOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$baselinePath = Join-Path $repositoryRoot 'eng\TerrariaReferences.baseline.json'
$referencesDirectory = Join-Path $repositoryRoot 'external\TerrariaRefs'

function Read-Baseline {
    if (-not [System.IO.File]::Exists($baselinePath)) {
        throw 'The Terraria reference baseline is missing.'
    }

    $baseline = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
    if ([int] $baseline.schemaVersion -ne 1 -or @($baseline.files).Count -ne 5) {
        throw 'The Terraria reference baseline has an unsupported shape.'
    }

    return $baseline
}

function Get-BaselineEntry {
    param([object] $Baseline, [string] $LogicalName)

    $matches = @($Baseline.files | Where-Object { $_.logicalName -ceq $LogicalName })
    if ($matches.Count -ne 1) {
        throw "The baseline must contain exactly one entry for $LogicalName."
    }

    return $matches[0]
}

function Get-PublicKeyTokenText {
    param([System.Reflection.AssemblyName] $AssemblyName)

    $token = $AssemblyName.GetPublicKeyToken()
    if ($null -eq $token -or $token.Length -eq 0) {
        return ''
    }

    return (($token | ForEach-Object { $_.ToString('x2') }) -join '')
}

function Assert-ReferenceMatchesBaseline {
    param([string] $Path, [object] $Expected)

    if (-not [System.IO.File]::Exists($Path)) {
        throw "Required reference is missing: $($Expected.logicalName)."
    }

    $assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName($Path)
    $fileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($Path).FileVersion
    $token = Get-PublicKeyTokenText -AssemblyName $assemblyName
    $hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()

    if ($assemblyName.Name -cne [string] $Expected.assemblySimpleName -or
        $assemblyName.Version.ToString() -cne [string] $Expected.assemblyVersion -or
        $fileVersion -cne [string] $Expected.fileVersion -or
        $token -cne [string] $Expected.publicKeyToken -or
        $hash -cne [string] $Expected.sha256) {
        throw "Reference identity does not match the baseline: $($Expected.logicalName)."
    }
}

function Assert-ReferenceSet {
    param([object] $Baseline, [string] $Directory)

    if (-not [System.IO.Directory]::Exists($Directory)) {
        throw 'The prepared Terraria reference directory is missing.'
    }

    foreach ($expected in @($Baseline.files)) {
        Assert-ReferenceMatchesBaseline `
            -Path (Join-Path $Directory ([string] $expected.logicalName)) `
            -Expected $expected
    }
}

function Export-EmbeddedReLogic {
    param([string] $TerrariaExe, [string] $Destination, [string] $ResourceName)

    $assembly = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($TerrariaExe)
    $matches = @($assembly.GetManifestResourceNames() | Where-Object { $_ -ceq $ResourceName })
    if ($matches.Count -ne 1) {
        throw "Embedded ReLogic resource is missing or ambiguous: $ResourceName."
    }

    $input = $assembly.GetManifestResourceStream($ResourceName)
    if ($null -eq $input) {
        throw "Embedded ReLogic resource could not be opened: $ResourceName."
    }

    try {
        $output = [System.IO.File]::Open(
            $Destination,
            [System.IO.FileMode]::CreateNew,
            [System.IO.FileAccess]::Write,
            [System.IO.FileShare]::None)
        try {
            $input.CopyTo($output)
        }
        finally {
            $output.Dispose()
        }
    }
    finally {
        $input.Dispose()
    }
}

function Assert-OwnedReferencePath {
    param([string] $Path)
    # Publication owns only the fixed reference directory and its five files /
    # receipt. Never follow a link or remove unrelated files from external.
    $full = [IO.Path]::GetFullPath($Path)
    $owned = [IO.Path]::GetFullPath($referencesDirectory)
    if ($full -cne $owned -and -not $full.StartsWith($owned + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Reference publication escaped its owned directory.'
    }
    $current = $full
    while ($current -and $current -cne $repositoryRoot) {
        if ((Test-Path -LiteralPath $current) -and
            ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Reference publication refuses reparse paths.'
        }
        $current = Split-Path -Parent $current
    }
}

function Assert-ExplicitSources {
    Assert-ReferenceMatchesBaseline -Path $terrariaSource -Expected $terrariaExpected
    Assert-ReferenceMatchesBaseline -Path $xnaGameSource -Expected $xnaGameExpected
    Assert-ReferenceMatchesBaseline -Path $xnaFrameworkSource -Expected $xnaFrameworkExpected
    Assert-ReferenceMatchesBaseline -Path $xnaGraphicsSource -Expected $xnaGraphicsExpected
}

function Publish-ReferenceReceipt {
    # Only the real prepare producer can attest the explicit original source.
    # VerifyOnly deliberately checks compile references without making this
    # stronger Content/source claim. Revalidate source bytes before publishing.
    Assert-ExplicitSources
    Assert-ReferenceSet -Baseline $baseline -Directory $referencesDirectory
    $receiptPath = Join-Path $referencesDirectory '.juemingr-reference-set.json'
    Assert-OwnedReferencePath $receiptPath
    $receipt = [ordered]@{
        schemaVersion = 1
        generator = 'scripts/prepare-terraria-references.ps1'
        profileId = [string] $baseline.profileId
        baselineSha256 = (Get-FileHash -LiteralPath $baselinePath -Algorithm SHA256).Hash
        preparedAtUtc = [DateTime]::UtcNow.ToString('o')
        source = [ordered]@{
            terrariaInstallDirectory = Split-Path -Parent $terrariaSource
            terrariaExePath = $terrariaSource
            xnaGameAssemblyPath = $xnaGameSource
            xnaFrameworkAssemblyPath = $xnaFrameworkSource
            xnaGraphicsAssemblyPath = $xnaGraphicsSource
        }
        sourceHashesUnchanged = $true
        files = @($baseline.files)
    }
    $temporary = Join-Path $referencesDirectory ('.reference-receipt.' + [Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        $stream = [IO.File]::Open($temporary, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        try {
            $bytes = (New-Object Text.UTF8Encoding($false)).GetBytes(($receipt | ConvertTo-Json -Depth 12))
            $stream.Write($bytes, 0, $bytes.Length)
            $stream.Flush($true)
        } finally { $stream.Dispose() }
        $written = Get-Content -LiteralPath $temporary -Raw | ConvertFrom-Json
        if ($written.profileId -cne $baseline.profileId -or @($written.files).Count -ne 5 -or
            $written.source.terrariaInstallDirectory -cne (Split-Path -Parent $terrariaSource)) {
            throw 'Prepared reference receipt did not round-trip.'
        }
        Assert-ExplicitSources
        Assert-ReferenceSet -Baseline $baseline -Directory $referencesDirectory
        if ([IO.File]::Exists($receiptPath)) { [IO.File]::Replace($temporary, $receiptPath, [NullString]::Value) }
        else { [IO.File]::Move($temporary, $receiptPath) }
    } finally {
        if ([IO.File]::Exists($temporary)) { [IO.File]::Delete($temporary) }
    }
}

$baseline = Read-Baseline
if ($VerifyOnly) {
    Assert-ReferenceSet -Baseline $baseline -Directory $referencesDirectory
    Write-Output 'PASS: the five local compile references match the baseline; VerifyOnly does not verify or repair original Content provenance.'
    exit 0
}

if ([string]::IsNullOrWhiteSpace($TerrariaExePath) -or
    [string]::IsNullOrWhiteSpace($XnaGameAssemblyPath) -or
    [string]::IsNullOrWhiteSpace($XnaFrameworkAssemblyPath) -or
    [string]::IsNullOrWhiteSpace($XnaGraphicsAssemblyPath)) {
    throw 'TerrariaExePath and all three XNA assembly paths are required unless VerifyOnly is used.'
}

$terrariaSource = [System.IO.Path]::GetFullPath($TerrariaExePath)
if ([IO.Path]::GetFileName($terrariaSource) -cne 'Terraria.exe') {
    throw 'The explicit original source must be named Terraria.exe for native Content provenance.'
}
$xnaGameSource = [System.IO.Path]::GetFullPath($XnaGameAssemblyPath)
$xnaFrameworkSource = [System.IO.Path]::GetFullPath($XnaFrameworkAssemblyPath)
$xnaGraphicsSource = [System.IO.Path]::GetFullPath($XnaGraphicsAssemblyPath)
$terrariaExpected = Get-BaselineEntry -Baseline $baseline -LogicalName 'Terraria.exe'
$reLogicExpected = Get-BaselineEntry -Baseline $baseline -LogicalName 'ReLogic.dll'
$xnaGameExpected = Get-BaselineEntry -Baseline $baseline -LogicalName 'Microsoft.Xna.Framework.Game.dll'
$xnaFrameworkExpected = Get-BaselineEntry -Baseline $baseline -LogicalName 'Microsoft.Xna.Framework.dll'
$xnaGraphicsExpected = Get-BaselineEntry -Baseline $baseline -LogicalName 'Microsoft.Xna.Framework.Graphics.dll'

Assert-ReferenceMatchesBaseline -Path $terrariaSource -Expected $terrariaExpected
Assert-ReferenceMatchesBaseline -Path $xnaGameSource -Expected $xnaGameExpected
Assert-ReferenceMatchesBaseline -Path $xnaFrameworkSource -Expected $xnaFrameworkExpected
Assert-ReferenceMatchesBaseline -Path $xnaGraphicsSource -Expected $xnaGraphicsExpected

$existingSetIsValid = $false
try {
    Assert-ReferenceSet -Baseline $baseline -Directory $referencesDirectory
    $existingSetIsValid = $true
}
catch {
    $existingSetIsValid = $false
}

if ($existingSetIsValid) {
    Publish-ReferenceReceipt
    Write-Output 'PASS: the fixed local Terraria reference set is already up to date.'
    exit 0
}

$externalDirectory = Split-Path -Parent $referencesDirectory
Assert-OwnedReferencePath $referencesDirectory
[System.IO.Directory]::CreateDirectory($externalDirectory) | Out-Null
$stagingDirectory = Join-Path $externalDirectory ('.TerrariaRefs.stage.' + [Guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($stagingDirectory) | Out-Null

try {
    [System.IO.File]::Copy($terrariaSource, (Join-Path $stagingDirectory 'Terraria.exe'), $false)
    [System.IO.File]::Copy($xnaGameSource, (Join-Path $stagingDirectory 'Microsoft.Xna.Framework.Game.dll'), $false)
    [System.IO.File]::Copy($xnaFrameworkSource, (Join-Path $stagingDirectory 'Microsoft.Xna.Framework.dll'), $false)
    [System.IO.File]::Copy($xnaGraphicsSource, (Join-Path $stagingDirectory 'Microsoft.Xna.Framework.Graphics.dll'), $false)
    # Extract the exact embedded dependency for compilation; never execute the game to resolve it.
    Export-EmbeddedReLogic `
        -TerrariaExe $terrariaSource `
        -Destination (Join-Path $stagingDirectory 'ReLogic.dll') `
        -ResourceName ([string] $reLogicExpected.embeddedResourceName)
    Assert-ReferenceSet -Baseline $baseline -Directory $stagingDirectory

    Assert-ExplicitSources
    Assert-OwnedReferencePath $referencesDirectory
    [IO.Directory]::CreateDirectory($referencesDirectory) | Out-Null
    $receiptPath = Join-Path $referencesDirectory '.juemingr-reference-set.json'
    Assert-OwnedReferencePath $receiptPath
    # Retire provenance before a partial set can be observed; a failed publish
    # remains visibly incomplete and a later prepare can safely finish it.
    if ([IO.File]::Exists($receiptPath)) { [IO.File]::Delete($receiptPath) }
    foreach ($expected in @($baseline.files)) {
        $destination = Join-Path $referencesDirectory ([string] $expected.logicalName)
        Assert-OwnedReferencePath $destination
        $staged = Join-Path $stagingDirectory ([string] $expected.logicalName)
        if ([IO.File]::Exists($destination)) { [IO.File]::Replace($staged, $destination, [NullString]::Value) }
        else { [IO.File]::Move($staged, $destination) }
    }
    Publish-ReferenceReceipt
}
finally {
    if ($null -ne $stagingDirectory -and [System.IO.Directory]::Exists($stagingDirectory)) {
        $stagingFull = [IO.Path]::GetFullPath($stagingDirectory)
        if ((Split-Path -Parent $stagingFull) -cne [IO.Path]::GetFullPath($externalDirectory) -or
            (Split-Path -Leaf $stagingFull) -notlike '.TerrariaRefs.stage.*') { throw 'Unsafe reference staging cleanup target.' }
        Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
    }
}

Assert-ReferenceSet -Baseline $baseline -Directory $referencesDirectory
Write-Output 'PASS: prepared five verified local compile references in external/TerrariaRefs.'
