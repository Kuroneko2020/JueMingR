[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string] $StandardDirectory,
    [Parameter(Mandatory=$true)][string] $BuildRecordPath,
    [Parameter(Mandatory=$true)][string] $SetupPath
)
$ErrorActionPreference='Stop'
# A fresh metadata-only process for each installer identity: Framework forbids loading
# two byte streams with the same Setup AssemblyName into its reflection-only context.
. (Join-Path $PSScriptRoot 'PackageVerification.Support.ps1')
$record=Get-Content -LiteralPath $BuildRecordPath -Raw -Encoding UTF8 | ConvertFrom-Json
Read-TwoFilePackage -PackageRoot $StandardDirectory -Record $record -SetupPath $SetupPath | ConvertTo-Json -Depth 20
