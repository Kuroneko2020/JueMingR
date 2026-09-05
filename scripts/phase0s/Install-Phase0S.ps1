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
    [Console]::Out.WriteLine('{"schemaVersion":1,"operation":"install","status":"failure","code":"PACKAGE_INVALID","exitCode":3,"packageId":null,"object":"package","sha256":null}')
    exit 3
}

try {
    $targetRoot = Get-Phase0SValidatedTerrariaDirectory -TerrariaDirectory $TerrariaDirectory
}
catch {
    Write-Phase0SResultAndExit -Operation 'install' -Status 'failure' -Code 'INVALID_ARGUMENT' -ExitCode 2 -PackageId $null -Object 'target' -Sha256 $null
}

$paths = Get-Phase0SControlledPaths -TerrariaDirectory $targetRoot
$configState = Get-Phase0SPathState -Path $paths.configFinal
# Existing config is a conflict, not a backup-and-overwrite input for this validation installer.
if ($configState.exists) {
    $configHash = $null
    if ($configState.readable -and -not $configState.isDirectory -and -not $configState.isReparsePoint) {
        try {
            $configHash = Get-Phase0SFileSha256 -Path $paths.configFinal
        }
        catch {
            $configHash = $null
        }
    }
    Write-Phase0SResultAndExit -Operation 'install' -Status 'conflict' -Code 'CONFIG_EXISTS' -ExitCode 10 -PackageId $null -Object 'Terraria.exe.config' -Sha256 $configHash
}

$terrariaExe = Join-Path $targetRoot 'Terraria.exe'
if (-not (Test-Phase0STerrariaIdentity -Path $terrariaExe)) {
    Write-Phase0SResultAndExit -Operation 'install' -Status 'failure' -Code 'TERRARIA_IDENTITY_MISMATCH' -ExitCode 4 -PackageId $null -Object 'Terraria.exe' -Sha256 $null
}

$runningProcesses = @(Get-Process -Name 'Terraria' -ErrorAction SilentlyContinue)
if ($runningProcesses.Count -ne 0) {
    Write-Phase0SResultAndExit -Operation 'install' -Status 'conflict' -Code 'TERRARIA_RUNNING' -ExitCode 5 -PackageId $null -Object 'process' -Sha256 $null
}

if ((Get-Phase0SPathState -Path $paths.bootstrapFinal).exists) {
    Write-Phase0SResultAndExit -Operation 'install' -Status 'conflict' -Code 'BOOTSTRAP_CONFLICT' -ExitCode 11 -PackageId $null -Object 'bootstrap' -Sha256 $null
}
foreach ($path in @($paths.sidecarFinal, $paths.sidecarStage, $paths.configTemp, $paths.bootstrapTemp)) {
    if ((Get-Phase0SPathState -Path $path).exists) {
        Write-Phase0SResultAndExit -Operation 'install' -Status 'conflict' -Code 'WORK_PATH_CONFLICT' -ExitCode 12 -PackageId $null -Object 'work-paths' -Sha256 $null
    }
}

try {
    $package = Read-Phase0SPackage -PackageRoot $PSScriptRoot
}
catch {
    Write-Phase0SResultAndExit -Operation 'install' -Status 'failure' -Code 'PACKAGE_INVALID' -ExitCode 3 -PackageId $null -Object 'package' -Sha256 $null
}

$installationStarted = $false
try {
    if (-not (New-Phase0SDirectoryCreateNew -Path $paths.sidecarStage)) {
        Write-Phase0SResultAndExit -Operation 'install' -Status 'conflict' -Code 'WORK_PATH_CONFLICT' -ExitCode 12 -PackageId $package.packageId -Object 'work-paths' -Sha256 $null
    }
    $installationStarted = $true

    $receiptPath = Join-Path $paths.sidecarStage 'phase-0-s-install-manifest.json'
    Copy-Phase0SBytesCreateNew -Bytes $package.manifestBytes -DestinationPath $receiptPath
    if (-not (Test-Phase0SFilesEqualBytes -FirstPath $package.manifestPath -SecondPath $receiptPath)) {
        throw 'Install receipt verification failed.'
    }

    $sidecarRelativePaths = @(
        'JueMingR.Validation/0Harmony.dll',
        'JueMingR.Validation/JueMingR.Features.dll',
        'JueMingR.Validation/JueMingR.Infrastructure.dll',
        'JueMingR.Validation/JueMingR.Platform.dll',
        'JueMingR.Validation/JueMingR.TerrariaHost.dll',
        'JueMingR.Validation/phase-0-s-runtime.manifest'
    )
    foreach ($relativePath in $sidecarRelativePaths) {
        $entry = Get-Phase0SPayloadEntry -Package $package -RelativePath $relativePath
        $sourcePath = Resolve-Phase0SContainedPath -Root $package.payloadRoot -RelativePath $relativePath
        $destinationPath = Join-Path $paths.sidecarStage (Split-Path -Leaf $relativePath)
        Copy-Phase0SFileCreateNew -SourcePath $sourcePath -DestinationPath $destinationPath -ExpectedEntry $entry
    }
    Assert-Phase0SSidecarDirectory -Directory $paths.sidecarStage -Package $package
    if ((Get-Phase0SPathState -Path $paths.sidecarFinal).exists) {
        throw 'Sidecar final path appeared during installation.'
    }
    [System.IO.Directory]::Move($paths.sidecarStage, $paths.sidecarFinal)
    Assert-Phase0SSidecarDirectory -Directory $paths.sidecarFinal -Package $package

    $bootstrapEntry = Get-Phase0SPayloadEntry -Package $package -RelativePath 'JueMingR.Bootstrap.dll'
    $bootstrapSource = Resolve-Phase0SContainedPath -Root $package.payloadRoot -RelativePath 'JueMingR.Bootstrap.dll'
    Copy-Phase0SFileCreateNew -SourcePath $bootstrapSource -DestinationPath $paths.bootstrapTemp -ExpectedEntry $bootstrapEntry
    if ((Get-Phase0SPathState -Path $paths.bootstrapFinal).exists) {
        throw 'Bootstrap final path appeared during installation.'
    }
    [System.IO.File]::Move($paths.bootstrapTemp, $paths.bootstrapFinal)
    if (-not (Test-Phase0SFileMatchesPayloadEntry -Path $paths.bootstrapFinal -Entry $bootstrapEntry)) {
        throw 'Bootstrap final verification failed.'
    }

    # Config activates the chain and is committed last; the preceding moves are not one transaction.
    $configEntry = Get-Phase0SPayloadEntry -Package $package -RelativePath 'Terraria.exe.config'
    $configSource = Resolve-Phase0SContainedPath -Root $package.payloadRoot -RelativePath 'Terraria.exe.config'
    Copy-Phase0SFileCreateNew -SourcePath $configSource -DestinationPath $paths.configTemp -ExpectedEntry $configEntry
    if ((Get-Phase0SPathState -Path $paths.configFinal).exists) {
        throw 'Config final path appeared during installation.'
    }
    [System.IO.File]::Move($paths.configTemp, $paths.configFinal)
}
catch {
    if (-not $installationStarted) {
        Write-Phase0SResultAndExit -Operation 'install' -Status 'failure' -Code 'INSTALL_FAILED_ROLLED_BACK' -ExitCode 20 -PackageId $package.packageId -Object 'installation' -Sha256 $null
    }
    try {
        # Even rollback needs exact ownership; unknown partial content must remain for recovery.
        $ownership = Test-Phase0SRestoreOwnership -TerrariaDirectory $targetRoot -Package $package
        if (-not $ownership.valid) {
            throw 'Rollback ownership is incomplete.'
        }
        Remove-Phase0SOwnedObjects -TerrariaDirectory $targetRoot -Package $package
        $after = Test-Phase0SRestoreOwnership -TerrariaDirectory $targetRoot -Package $package
        if (-not $after.valid -or -not $after.noop) {
            throw 'Rollback did not return to the pre-install target state.'
        }
        Write-Phase0SResultAndExit -Operation 'install' -Status 'failure' -Code 'INSTALL_FAILED_ROLLED_BACK' -ExitCode 20 -PackageId $package.packageId -Object 'installation' -Sha256 $null
    }
    catch {
        Write-Phase0SResultAndExit -Operation 'install' -Status 'failure' -Code 'INSTALL_FAILED_ROLLBACK_INCOMPLETE' -ExitCode 21 -PackageId $package.packageId -Object 'installation' -Sha256 $null
    }
}

Write-Phase0SResultAndExit -Operation 'install' -Status 'success' -Code 'INSTALL_COMPLETE' -ExitCode 0 -PackageId $package.packageId -Object 'installation' -Sha256 $null
