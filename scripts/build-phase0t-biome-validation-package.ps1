[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

& (Join-Path $PSScriptRoot 'build-phase0s-validation-package.ps1') `
    -OutputDirectory $OutputDirectory `
    -Profile Phase0TBiome
