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

$baseline = Read-Baseline
if ($VerifyOnly) {
    Assert-ReferenceSet -Baseline $baseline -Directory $referencesDirectory
    Write-Output 'PASS: the fixed local Terraria reference set matches the baseline.'
    exit 0
}

if ([string]::IsNullOrWhiteSpace($TerrariaExePath) -or
    [string]::IsNullOrWhiteSpace($XnaGameAssemblyPath) -or
    [string]::IsNullOrWhiteSpace($XnaFrameworkAssemblyPath) -or
    [string]::IsNullOrWhiteSpace($XnaGraphicsAssemblyPath)) {
    throw 'TerrariaExePath and all three XNA assembly paths are required unless VerifyOnly is used.'
}

$terrariaSource = [System.IO.Path]::GetFullPath($TerrariaExePath)
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
    Write-Output 'PASS: the fixed local Terraria reference set is already up to date.'
    exit 0
}

$externalDirectory = Split-Path -Parent $referencesDirectory
[System.IO.Directory]::CreateDirectory($externalDirectory) | Out-Null
$stagingDirectory = Join-Path $externalDirectory ('.TerrariaRefs.stage.' + [Guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($stagingDirectory) | Out-Null

try {
    [System.IO.File]::Copy($terrariaSource, (Join-Path $stagingDirectory 'Terraria.exe'), $false)
    [System.IO.File]::Copy($xnaGameSource, (Join-Path $stagingDirectory 'Microsoft.Xna.Framework.Game.dll'), $false)
    [System.IO.File]::Copy($xnaFrameworkSource, (Join-Path $stagingDirectory 'Microsoft.Xna.Framework.dll'), $false)
    [System.IO.File]::Copy($xnaGraphicsSource, (Join-Path $stagingDirectory 'Microsoft.Xna.Framework.Graphics.dll'), $false)
    Export-EmbeddedReLogic `
        -TerrariaExe $terrariaSource `
        -Destination (Join-Path $stagingDirectory 'ReLogic.dll') `
        -ResourceName ([string] $reLogicExpected.embeddedResourceName)
    Assert-ReferenceSet -Baseline $baseline -Directory $stagingDirectory

    if ([System.IO.Directory]::Exists($referencesDirectory)) {
        Remove-Item -LiteralPath $referencesDirectory -Recurse -Force
    }
    Move-Item -LiteralPath $stagingDirectory -Destination $referencesDirectory
    $stagingDirectory = $null
}
finally {
    if ($null -ne $stagingDirectory -and [System.IO.Directory]::Exists($stagingDirectory)) {
        Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
    }
}

Assert-ReferenceSet -Baseline $baseline -Directory $referencesDirectory
Write-Output 'PASS: prepared five verified local compile references in external/TerrariaRefs.'
