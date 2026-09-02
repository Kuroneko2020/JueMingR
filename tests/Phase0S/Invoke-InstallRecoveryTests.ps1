[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

. (Join-Path $PSScriptRoot 'TestSupport.ps1')

function Get-Phase0SAssemblyIdentity {
    param([Parameter(Mandatory = $true)][string] $AssemblyPath)

    $assembly = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($AssemblyPath)
    return [ordered]@{
        simpleName = $assembly.GetName().Name
        version = $assembly.GetName().Version.ToString()
        mvid = $assembly.ManifestModule.ModuleVersionId.ToString()
        sha256 = Get-Phase0SFileSha256 -Path $AssemblyPath
    }
}

function New-Phase0SControlledPackageFixture {
    param(
        [Parameter(Mandatory = $true)][string] $RepositoryRoot,
        [Parameter(Mandatory = $true)][string] $PackageRoot,
        [Parameter(Mandatory = $true)][string] $TerrariaIdentityInput
    )

    [System.IO.Directory]::CreateDirectory($PackageRoot) | Out-Null
    $payloadRoot = Join-Path $PackageRoot 'payload'
    [System.IO.Directory]::CreateDirectory((Join-Path $payloadRoot 'JueMingR.Validation')) | Out-Null

    $sourceCommit = (& git -C $RepositoryRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $sourceCommit -notmatch '^[0-9a-f]{40}$') {
        throw 'The controlled package fixture could not obtain a source commit identity.'
    }
    $packageId = 'phase0s-' + $sourceCommit
    $runtimeManifest = @(
        'schemaVersion=1',
        'packageId=' + $packageId,
        'sourceCommit=' + $sourceCommit,
        'targetAssemblySimpleName=Terraria',
        'targetAssemblyVersion=1.4.5.8',
        'targetAssemblyMvid=00000000-0000-0000-0000-000000000000',
        'targetAssemblySha256=' + (Get-Phase0SFileSha256 -Path $TerrariaIdentityInput),
        'targetTypeName=Terraria.Main',
        'targetMethodName=Initialize',
        'targetMethodMetadataToken=0x06000001',
        'targetMethodIsStatic=false',
        'targetMethodReturnType=System.Void',
        'targetMethodParameterCount=0',
        'hostAssemblySimpleName=JueMingR.TerrariaHost',
        'hostAssemblyVersion=0.0.0.0',
        'hostAssemblyMvid=00000000-0000-0000-0000-000000000000',
        'hostAssemblySha256=' + ('0' * 64),
        'harmonyAssemblySimpleName=0Harmony',
        'harmonyAssemblyVersion=2.4.2.0',
        'harmonyAssemblyMvid=024a0e6e-c8c2-437e-ad04-7b6279389c23',
        'harmonyAssemblySha256=7B9E756306FA3D7620E02A857C8927A6AB04973F9BD8A77D3866700A6DEAC55C',
        'patchOwner=JueMingR.Phase0S.MainInitialize',
        'evidenceFileName=phase-0-s-evidence.log'
    ) -join [Environment]::NewLine
    $payloadContents = [ordered]@{
        'Terraria.exe.config' = '<configuration><runtime /></configuration>' + [Environment]::NewLine
        'JueMingR.Bootstrap.dll' = 'phase0s-controlled-bootstrap-fixture'
        'JueMingR.Validation/JueMingR.TerrariaHost.dll' = 'phase0s-controlled-host-fixture'
        'JueMingR.Validation/JueMingR.Platform.dll' = 'phase0s-controlled-platform-fixture'
        'JueMingR.Validation/JueMingR.Features.dll' = 'phase0s-controlled-features-fixture'
        'JueMingR.Validation/JueMingR.Infrastructure.dll' = 'phase0s-controlled-infrastructure-fixture'
        'JueMingR.Validation/0Harmony.dll' = 'phase0s-controlled-harmony-fixture'
        'JueMingR.Validation/phase-0-s-runtime.manifest' = $runtimeManifest + [Environment]::NewLine
    }
    foreach ($relativePath in $payloadContents.Keys) {
        $destination = Join-Path $payloadRoot $relativePath
        $directory = Split-Path -Parent $destination
        [System.IO.Directory]::CreateDirectory($directory) | Out-Null
        [System.IO.File]::WriteAllText($destination, $payloadContents[$relativePath], (New-Object System.Text.UTF8Encoding($false)))
    }

    $payloadEntries = @($payloadContents.Keys | Sort-Object | ForEach-Object {
        $path = Join-Path $payloadRoot $_
        [ordered]@{
            installRelativePath = $_
            length = [int64](Get-Item -LiteralPath $path).Length
            sha256 = Get-Phase0SFileSha256 -Path $path
        }
    })
    $manifest = [ordered]@{
        schemaVersion = 1
        packageId = $packageId
        sourceCommit = $sourceCommit
        target = Get-Phase0SAssemblyIdentity -AssemblyPath $TerrariaIdentityInput
        payload = $payloadEntries
    }
    $manifestPath = Join-Path $PackageRoot 'phase-0-s-package.manifest.json'
    [System.IO.File]::WriteAllText(
        $manifestPath,
        (($manifest | ConvertTo-Json -Depth 6) + [Environment]::NewLine),
        (New-Object System.Text.UTF8Encoding($false)))

    foreach ($scriptName in @('Install-Phase0S.ps1', 'Restore-Phase0S.ps1')) {
        $sourceScript = Join-Path $RepositoryRoot ('scripts\phase0s\' + $scriptName)
        Copy-Item -LiteralPath $sourceScript -Destination (Join-Path $PackageRoot $scriptName)
    }
    return $manifest
}

function New-Phase0STargetDirectory {
    param(
        [Parameter(Mandatory = $true)][string] $Root,
        [Parameter(Mandatory = $true)][string] $TerrariaIdentityInput,
        [Parameter(Mandatory = $true)][string] $Name
    )

    $target = Join-Path $Root $Name
    [System.IO.Directory]::CreateDirectory($target) | Out-Null
    Copy-Item -LiteralPath $TerrariaIdentityInput -Destination (Join-Path $target 'Terraria.exe')
    return $target
}

function Invoke-Phase0SPackageScript {
    param(
        [Parameter(Mandatory = $true)][string] $PackageRoot,
        [Parameter(Mandatory = $true)][string] $ScriptName,
        [Parameter(Mandatory = $true)][string] $TerrariaDirectory
    )

    return Invoke-Phase0SWindowsPowerShell -ScriptPath (Join-Path $PackageRoot $ScriptName) -Arguments @('-TerrariaDirectory', $TerrariaDirectory)
}

function Assert-Phase0SExitAndNoWrite {
    param(
        [Parameter(Mandatory = $true)][pscustomobject] $Result,
        [Parameter(Mandatory = $true)][int] $ExpectedExitCode,
        [Parameter(Mandatory = $true)][object[]] $Before,
        [Parameter(Mandatory = $true)][string] $Target,
        [Parameter(Mandatory = $true)][string] $Scenario
    )

    Assert-Phase0SCondition -Condition ($Result.exitCode -eq $ExpectedExitCode) -Message "${Scenario}: expected exit $ExpectedExitCode, actual $($Result.exitCode)."
    Assert-Phase0STreeSnapshotEqual -Expected $Before -Actual (Get-Phase0STreeSnapshot -Root $Target) -Context $Scenario
}

function Assert-Phase0SInstalledLayout {
    param([Parameter(Mandatory = $true)][string] $Target)

    $required = @(
        'Terraria.exe',
        'Terraria.exe.config',
        'JueMingR.Bootstrap.dll',
        'JueMingR.Validation',
        'JueMingR.Validation\phase-0-s-install-manifest.json',
        'JueMingR.Validation\JueMingR.TerrariaHost.dll',
        'JueMingR.Validation\JueMingR.Platform.dll',
        'JueMingR.Validation\JueMingR.Features.dll',
        'JueMingR.Validation\JueMingR.Infrastructure.dll',
        'JueMingR.Validation\0Harmony.dll',
        'JueMingR.Validation\phase-0-s-runtime.manifest'
    )
    $actual = @(Get-Phase0STreeSnapshot -Root $Target | ForEach-Object { $_.path })
    foreach ($path in $required) {
        Assert-Phase0SCondition -Condition ($actual -contains $path) -Message "Successful install is missing fixed layout entry: $path"
    }
    foreach ($path in $actual) {
        Assert-Phase0SCondition -Condition ($required -contains $path -or $path -eq 'JueMingR.Validation') -Message "Successful install created an unexpected target entry: $path"
    }
}

function Invoke-Phase0SInstallRecoveryTests {
    param([Parameter(Mandatory = $true)][string] $RepositoryRoot)

    $terrariaIdentityInput = Join-Path $RepositoryRoot 'external\TerrariaRefs\Terraria.exe'
    if (-not [System.IO.File]::Exists($terrariaIdentityInput)) {
        throw 'The legal local Terraria.exe compile-time identity input is unavailable.'
    }

    $installScript = Join-Path $RepositoryRoot 'scripts\phase0s\Install-Phase0S.ps1'
    $restoreScript = Join-Path $RepositoryRoot 'scripts\phase0s\Restore-Phase0S.ps1'
    if (-not [System.IO.File]::Exists($installScript) -or -not [System.IO.File]::Exists($restoreScript)) {
        throw 'Phase 0-S install/recovery production scripts are required before behavior tests can run.'
    }

    $root = New-Phase0STestRoot
    try {
        $packageRoot = Join-Path $root 'controlled-package'
        New-Phase0SControlledPackageFixture -RepositoryRoot $RepositoryRoot -PackageRoot $packageRoot -TerrariaIdentityInput $terrariaIdentityInput | Out-Null

        $existingConfig = New-Phase0STargetDirectory -Root $root -TerrariaIdentityInput $terrariaIdentityInput -Name 'existing-config'
        [System.IO.File]::WriteAllText((Join-Path $existingConfig 'Terraria.exe.config'), 'external-config', (New-Object System.Text.UTF8Encoding($false)))
        $before = Get-Phase0STreeSnapshot -Root $existingConfig
        Assert-Phase0SExitAndNoWrite -Result (Invoke-Phase0SPackageScript -PackageRoot $packageRoot -ScriptName 'Install-Phase0S.ps1' -TerrariaDirectory $existingConfig) -ExpectedExitCode 10 -Before $before -Target $existingConfig -Scenario 'existing config'

        $bootstrapConflict = New-Phase0STargetDirectory -Root $root -TerrariaIdentityInput $terrariaIdentityInput -Name 'bootstrap-conflict'
        [System.IO.File]::WriteAllText((Join-Path $bootstrapConflict 'JueMingR.Bootstrap.dll'), 'external-bootstrap', (New-Object System.Text.UTF8Encoding($false)))
        $before = Get-Phase0STreeSnapshot -Root $bootstrapConflict
        Assert-Phase0SExitAndNoWrite -Result (Invoke-Phase0SPackageScript -PackageRoot $packageRoot -ScriptName 'Install-Phase0S.ps1' -TerrariaDirectory $bootstrapConflict) -ExpectedExitCode 11 -Before $before -Target $bootstrapConflict -Scenario 'Bootstrap conflict'

        foreach ($conflictName in @('sidecar-conflict', 'stage-conflict', 'config-temp-conflict', 'bootstrap-temp-conflict')) {
            $conflictTarget = New-Phase0STargetDirectory -Root $root -TerrariaIdentityInput $terrariaIdentityInput -Name $conflictName
            $conflictPath = Join-Path $conflictTarget ($(if ($conflictName -eq 'sidecar-conflict') { 'JueMingR.Validation' } elseif ($conflictName -eq 'stage-conflict') { 'JueMingR.Validation.phase0s-stage' } elseif ($conflictName -eq 'config-temp-conflict') { 'Terraria.exe.config.phase0s-temp' } else { 'JueMingR.Bootstrap.dll.phase0s-temp' }))
            if ($conflictPath.EndsWith('Validation')) {
                [System.IO.Directory]::CreateDirectory($conflictPath) | Out-Null
            }
            else {
                [System.IO.File]::WriteAllText($conflictPath, 'conflict', (New-Object System.Text.UTF8Encoding($false)))
            }
            $before = Get-Phase0STreeSnapshot -Root $conflictTarget
            Assert-Phase0SExitAndNoWrite -Result (Invoke-Phase0SPackageScript -PackageRoot $packageRoot -ScriptName 'Install-Phase0S.ps1' -TerrariaDirectory $conflictTarget) -ExpectedExitCode 12 -Before $before -Target $conflictTarget -Scenario $conflictName
        }

        $identityMismatch = Join-Path $root 'identity-mismatch'
        [System.IO.Directory]::CreateDirectory($identityMismatch) | Out-Null
        [System.IO.File]::WriteAllText((Join-Path $identityMismatch 'Terraria.exe'), 'not-a-terraria-assembly', (New-Object System.Text.UTF8Encoding($false)))
        $before = Get-Phase0STreeSnapshot -Root $identityMismatch
        Assert-Phase0SExitAndNoWrite -Result (Invoke-Phase0SPackageScript -PackageRoot $packageRoot -ScriptName 'Install-Phase0S.ps1' -TerrariaDirectory $identityMismatch) -ExpectedExitCode 4 -Before $before -Target $identityMismatch -Scenario 'Terraria identity mismatch'

        $successTarget = New-Phase0STargetDirectory -Root $root -TerrariaIdentityInput $terrariaIdentityInput -Name 'success'
        $installResult = Invoke-Phase0SPackageScript -PackageRoot $packageRoot -ScriptName 'Install-Phase0S.ps1' -TerrariaDirectory $successTarget
        Assert-Phase0SCondition -Condition ($installResult.exitCode -eq 0) -Message "successful install: expected exit 0, actual $($installResult.exitCode)."
        Assert-Phase0SInstalledLayout -Target $successTarget

        $restoreResult = Invoke-Phase0SPackageScript -PackageRoot $packageRoot -ScriptName 'Restore-Phase0S.ps1' -TerrariaDirectory $successTarget
        Assert-Phase0SCondition -Condition ($restoreResult.exitCode -eq 0) -Message "exact restore: expected exit 0, actual $($restoreResult.exitCode)."
        $afterRestore = Get-Phase0STreeSnapshot -Root $successTarget
        Assert-Phase0STreeSnapshotEqual -Expected (Get-Phase0STreeSnapshot -Root (New-Phase0STargetDirectory -Root $root -TerrariaIdentityInput $terrariaIdentityInput -Name 'restore-baseline')) -Actual $afterRestore -Context 'exact restore'

        $noopBefore = Get-Phase0STreeSnapshot -Root $successTarget
        Assert-Phase0SExitAndNoWrite -Result (Invoke-Phase0SPackageScript -PackageRoot $packageRoot -ScriptName 'Restore-Phase0S.ps1' -TerrariaDirectory $successTarget) -ExpectedExitCode 0 -Before $noopBefore -Target $successTarget -Scenario 'second restore noop'

        foreach ($modification in @('modified-static', 'unknown-sidecar')) {
            $modifiedTarget = New-Phase0STargetDirectory -Root $root -TerrariaIdentityInput $terrariaIdentityInput -Name $modification
            $installResult = Invoke-Phase0SPackageScript -PackageRoot $packageRoot -ScriptName 'Install-Phase0S.ps1' -TerrariaDirectory $modifiedTarget
            Assert-Phase0SCondition -Condition ($installResult.exitCode -eq 0) -Message "$modification setup install failed with exit $($installResult.exitCode)."
            if ($modification -eq 'modified-static') {
                [System.IO.File]::AppendAllText((Join-Path $modifiedTarget 'JueMingR.Bootstrap.dll'), 'external-change')
            }
            else {
                [System.IO.File]::WriteAllText((Join-Path $modifiedTarget 'JueMingR.Validation\unknown.txt'), 'external-file', (New-Object System.Text.UTF8Encoding($false)))
            }
            $before = Get-Phase0STreeSnapshot -Root $modifiedTarget
            Assert-Phase0SExitAndNoWrite -Result (Invoke-Phase0SPackageScript -PackageRoot $packageRoot -ScriptName 'Restore-Phase0S.ps1' -TerrariaDirectory $modifiedTarget) -ExpectedExitCode 30 -Before $before -Target $modifiedTarget -Scenario $modification
        }
    }
    finally {
        Remove-Phase0STestRoot -Root $root
    }
}
