[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, ValueFromPipeline = $false)]
    [string] $TerrariaDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

try {
    . (Join-Path $PSScriptRoot 'Phase0S.ScriptSupport.ps1')
}
catch {
    [Console]::Out.WriteLine('{"schemaVersion":1,"operation":"restore","status":"failure","code":"PACKAGE_INVALID","exitCode":3,"packageId":null,"object":"package","sha256":null}')
    exit 3
}

try {
    $targetRoot = Get-Phase0SValidatedTerrariaDirectory -TerrariaDirectory $TerrariaDirectory
}
catch {
    Write-Phase0SResultAndExit -Operation 'restore' -Status 'failure' -Code 'INVALID_ARGUMENT' -ExitCode 2 -PackageId $null -Object 'target' -Sha256 $null
}

try {
    $package = Read-Phase0SPackage -PackageRoot $PSScriptRoot
}
catch {
    Write-Phase0SResultAndExit -Operation 'restore' -Status 'failure' -Code 'PACKAGE_INVALID' -ExitCode 3 -PackageId $null -Object 'package' -Sha256 $null
}

$ownership = Test-Phase0SRestoreOwnership -TerrariaDirectory $targetRoot -Package $package
if (-not $ownership.valid) {
    Write-Phase0SResultAndExit -Operation 'restore' -Status 'failure' -Code 'OWNERSHIP_UNPROVEN' -ExitCode 30 -PackageId $package.packageId -Object 'owned-files' -Sha256 $null
}
if ($ownership.noop) {
    Write-Phase0SResultAndExit -Operation 'restore' -Status 'noop' -Code 'RESTORE_NOOP' -ExitCode 0 -PackageId $package.packageId -Object 'owned-files' -Sha256 $null
}

try {
    $secondPackage = Read-Phase0SPackage -PackageRoot $PSScriptRoot
    if ($secondPackage.manifestText -cne $package.manifestText -or $secondPackage.packageId -cne $package.packageId) {
        throw 'Package identity changed during restore preflight.'
    }
    $secondOwnership = Test-Phase0SRestoreOwnership -TerrariaDirectory $targetRoot -Package $secondPackage
    if (-not $secondOwnership.valid -or $secondOwnership.noop) {
        throw 'Target ownership changed during restore preflight.'
    }
}
catch {
    Write-Phase0SResultAndExit -Operation 'restore' -Status 'failure' -Code 'OWNERSHIP_UNPROVEN' -ExitCode 30 -PackageId $package.packageId -Object 'owned-files' -Sha256 $null
}

try {
    Remove-Phase0SOwnedObjects -TerrariaDirectory $targetRoot -Package $secondPackage
}
catch {
    Write-Phase0SResultAndExit -Operation 'restore' -Status 'failure' -Code 'RESTORE_DELETE_FAILED' -ExitCode 31 -PackageId $package.packageId -Object 'owned-files' -Sha256 $null
}

Write-Phase0SResultAndExit -Operation 'restore' -Status 'success' -Code 'RESTORE_COMPLETE' -ExitCode 0 -PackageId $package.packageId -Object 'owned-files' -Sha256 $null
