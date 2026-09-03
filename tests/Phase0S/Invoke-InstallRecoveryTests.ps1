[CmdletBinding()]
param(
    [switch] $Run,
    [string] $RepositoryRoot
)

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
    $supportSource = Join-Path $RepositoryRoot 'scripts\phase0s\Phase0S.ScriptSupport.ps1'
    if (-not [System.IO.File]::Exists($supportSource)) {
        throw 'The Phase 0-S package fixture requires Phase0S.ScriptSupport.ps1.'
    }
    $supportDestination = Join-Path $PackageRoot 'Phase0S.ScriptSupport.ps1'
    Copy-Item -LiteralPath $supportSource -Destination $supportDestination
    . $supportDestination

    $sourceCommit = (& git -C $RepositoryRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $sourceCommit -notmatch '^[0-9a-f]{40}$') {
        throw 'The controlled package fixture could not obtain a source commit identity.'
    }
    $packageId = 'phase0s-' + $sourceCommit
    $targetIdentity = Get-Phase0SAssemblyIdentity -AssemblyPath $TerrariaIdentityInput
    $runtimeManifest = @(
        'schemaVersion=2',
        ('packageId=' + $packageId),
        ('sourceCommit=' + $sourceCommit),
        'targetAssemblySimpleName=Terraria',
        'targetAssemblyVersion=1.4.5.8',
        ('targetAssemblyMvid=' + $targetIdentity.mvid),
        ('targetAssemblySha256=' + $targetIdentity.sha256),
        'reLogicAssemblySimpleName=ReLogic',
        'reLogicAssemblyVersion=1.0.0.0',
        'reLogicAssemblyPublicKeyToken=null',
        'reLogicAssemblyMvid=ee258be9-88a4-423d-b3ce-84b6c35b141a',
        'reLogicResourceName=Terraria.Libraries.ReLogic.ReLogic.dll',
        'reLogicResourceSha256=E1C5DCCEFFF5FD1C789FF712BABFA1A305FCED0D03C96EF30F2C14D99AA0AF29',
        'targetTypeName=Terraria.Main',
        'targetMethodName=Update',
        'targetMethodMetadataToken=0x06000001',
        'targetMethodIsStatic=false',
        'targetMethodReturnType=System.Void',
        'targetMethodParameterCount=1',
        'targetMethodParameterType=Microsoft.Xna.Framework.GameTime',
        'hostAssemblySimpleName=JueMingR.TerrariaHost',
        'hostAssemblyVersion=0.0.0.0',
        'hostAssemblyMvid=00000000-0000-0000-0000-000000000000',
        ('hostAssemblySha256=' + ('0' * 64)),
        'harmonyAssemblySimpleName=0Harmony',
        'harmonyAssemblyVersion=2.4.2.0',
        'harmonyAssemblyMvid=024a0e6e-c8c2-437e-ad04-7b6279389c23',
        'harmonyAssemblySha256=7B9E756306FA3D7620E02A857C8927A6AB04973F9BD8A77D3866700A6DEAC55C',
        'patchOwner=JueMingR.Phase0S.MainUpdate',
        'evidenceFileName=phase-0-s-evidence.log'
    ) -join [Environment]::NewLine
    Assert-Phase0SCondition -Condition ((@($runtimeManifest -split [Environment]::NewLine)).Count -eq 30) -Message 'The controlled package runtime manifest must contain exactly 30 lines.'
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
        target = $targetIdentity
        payload = $payloadEntries
    }
    $manifestPath = Join-Path $PackageRoot 'phase-0-s-package.manifest.json'
    [System.IO.File]::WriteAllText(
        $manifestPath,
        (ConvertTo-Phase0SCanonicalPackageManifestText -Manifest $manifest),
        (New-Object System.Text.UTF8Encoding($false)))
    $writtenPackageManifest = [System.IO.File]::ReadAllText($manifestPath, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
    $runtimePackageId = @($runtimeManifest -split [Environment]::NewLine | Where-Object { $_.StartsWith('packageId=', [System.StringComparison]::Ordinal) })[0].Substring('packageId='.Length)
    Assert-Phase0SCondition -Condition ([int] $writtenPackageManifest.schemaVersion -eq 1 -and [string] $writtenPackageManifest.packageId -ceq $packageId -and $runtimePackageId -ceq $packageId -and [System.IO.File]::Exists($supportDestination) -and -not [System.IO.File]::Exists((Join-Path $payloadRoot 'Phase0S.ScriptSupport.ps1'))) -Message 'The controlled package must use package manifest schema 1, share one package id, and keep its support script outside payload.'

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

function Assert-Phase0SCompactJsonResult {
    param(
        [Parameter(Mandatory = $true)][pscustomobject] $Result,
        [Parameter(Mandatory = $true)][string] $Operation,
        [Parameter(Mandatory = $true)][int] $ExpectedExitCode,
        [Parameter(Mandatory = $true)][string] $ExpectedCode,
        [Parameter(Mandatory = $true)][string] $ExpectedStatus,
        [Parameter(Mandatory = $true)][string] $TargetDirectory
    )

    Assert-Phase0SCondition -Condition ($Result.exitCode -eq $ExpectedExitCode) -Message "${Operation}/${ExpectedCode}: expected exit $ExpectedExitCode, actual $($Result.exitCode)."
    Assert-Phase0SCondition -Condition ($Result.output.Count -eq 1) -Message "${Operation}/${ExpectedCode}: expected exactly one JSON output line."
    $line = [string] $Result.output[0]
    $resultObject = $line | ConvertFrom-Json
    $actualProperties = @($resultObject.PSObject.Properties.Name | Sort-Object)
    $expectedProperties = @('code', 'exitCode', 'object', 'operation', 'packageId', 'schemaVersion', 'sha256', 'status')
    Assert-Phase0SCondition -Condition ((@($actualProperties) -join '|') -ceq ($expectedProperties -join '|')) -Message "${Operation}/${ExpectedCode}: result JSON properties differ from the fixed contract."
    Assert-Phase0SCondition -Condition ($resultObject.schemaVersion -eq 1 -and [string] $resultObject.operation -ceq $Operation -and [string] $resultObject.status -ceq $ExpectedStatus -and [int] $resultObject.exitCode -eq $ExpectedExitCode -and [string] $resultObject.code -ceq $ExpectedCode) -Message "${Operation}/${ExpectedCode}: fixed result values do not match."
    $expectedObjects = @{
        CONFIG_EXISTS = 'Terraria.exe.config'
        BOOTSTRAP_CONFLICT = 'bootstrap'
        WORK_PATH_CONFLICT = 'work-paths'
        TERRARIA_IDENTITY_MISMATCH = 'Terraria.exe'
        INSTALL_COMPLETE = 'installation'
        RESTORE_COMPLETE = 'owned-files'
        RESTORE_NOOP = 'owned-files'
        OWNERSHIP_UNPROVEN = 'owned-files'
    }
    $expectedObject = $expectedObjects[$ExpectedCode]
    Assert-Phase0SCondition -Condition (-not [string]::IsNullOrEmpty($expectedObject) -and [string] $resultObject.object -ceq $expectedObject) -Message "${Operation}/${ExpectedCode}: result object is not the fixed logical name."
    switch ($ExpectedCode) {
        'CONFIG_EXISTS' {
            Assert-Phase0SCondition -Condition ($null -eq $resultObject.packageId -and [string] $resultObject.sha256 -match '^[0-9A-F]{64}$') -Message "${Operation}/${ExpectedCode}: packageId must be JSON null and sha256 must be uppercase SHA-256."
        }
        { $_ -in @('BOOTSTRAP_CONFLICT', 'WORK_PATH_CONFLICT', 'TERRARIA_IDENTITY_MISMATCH') } {
            Assert-Phase0SCondition -Condition ($null -eq $resultObject.packageId -and $null -eq $resultObject.sha256) -Message "${Operation}/${ExpectedCode}: packageId and sha256 must be JSON null."
        }
        { $_ -in @('INSTALL_COMPLETE', 'RESTORE_COMPLETE', 'RESTORE_NOOP', 'OWNERSHIP_UNPROVEN') } {
            Assert-Phase0SCondition -Condition ([string] $resultObject.packageId -match '^phase0s-[0-9a-f]{40}$' -and $null -eq $resultObject.sha256) -Message "${Operation}/${ExpectedCode}: packageId or sha256 null semantics differ from the result contract."
        }
        default {
            throw "No null-semantics contract is defined for $ExpectedCode."
        }
    }
    Assert-Phase0SCondition -Condition ($line.IndexOf([System.IO.Path]::GetFullPath($TargetDirectory), [System.StringComparison]::OrdinalIgnoreCase) -lt 0 -and $line.IndexOf([Environment]::UserName, [System.StringComparison]::OrdinalIgnoreCase) -lt 0 -and $actualProperties -notcontains 'message' -and $actualProperties -notcontains 'stack') -Message "${Operation}/${ExpectedCode}: result JSON leaked a path, username, message, or stack."
}

function Assert-Phase0SReceiptMatchesPackageManifest {
    param(
        [Parameter(Mandatory = $true)][string] $PackageRoot,
        [Parameter(Mandatory = $true)][string] $TargetDirectory
    )

    $packageManifest = [System.IO.File]::ReadAllBytes((Join-Path $PackageRoot 'phase-0-s-package.manifest.json'))
    $receipt = [System.IO.File]::ReadAllBytes((Join-Path $TargetDirectory 'JueMingR.Validation\phase-0-s-install-manifest.json'))
    $identical = $packageManifest.Length -eq $receipt.Length
    for ($index = 0; $identical -and $index -lt $packageManifest.Length; $index++) {
        $identical = $packageManifest[$index] -eq $receipt[$index]
    }
    Assert-Phase0SCondition -Condition $identical -Message 'The install receipt is not a byte-for-byte copy of the package manifest.'
}

function Write-Phase0SEvidence {
    param(
        [Parameter(Mandatory = $true)][string] $TargetDirectory,
        [Parameter(Mandatory = $true)][string] $PackageId,
        [ValidateSet('prefix', 'complete', 'primary', 'primary-cleanup', 'wrong-package', 'out-of-order', 'unknown')]
        [string] $Kind
    )

    $events = @('TERRARIA_ASSEMBLY_READY', 'HARMONY_READY', 'HOOK_INSTALLED', 'MAIN_UPDATE_POSTFIX_FIRED', 'RUNTIME_HANDOFF_COMPLETE')
    $eventCount = if ($Kind -eq 'prefix') { 3 } elseif ($Kind -eq 'primary' -or $Kind -eq 'primary-cleanup') { 2 } else { 5 }
    $lines = New-Object System.Collections.Generic.List[string]
    for ($index = 0; $index -lt $eventCount; $index++) {
        $number = $index + 1
        $eventPackageId = if ($Kind -eq 'wrong-package') { 'phase0s-0000000000000000000000000000000000000000' } else { $PackageId }
        $eventName = if ($Kind -eq 'unknown' -and $index -eq 0) { 'UNKNOWN_EVENT' } else { $events[$index] }
        $eventNumber = if ($Kind -eq 'out-of-order' -and $index -eq 1) { '03' } else { $number.ToString('D2') }
        $lines.Add(('PHASE0S|1|{0}|{1}|{2}|{3}|1' -f $eventPackageId, $eventNumber, $eventName, [DateTime]::UtcNow.AddSeconds($index).ToString('o')))
    }
    if ($Kind -eq 'primary' -or $Kind -eq 'primary-cleanup') {
        $lines.Add(('PHASE0S|1|{0}|ERROR|PATCH|PATCH_FAILED|FileNotFoundException|ReLogic, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null' -f $PackageId))
    }
    if ($Kind -eq 'primary-cleanup') {
        $lines.Add(('PHASE0S|1|{0}|ERROR|PATCH_CLEANUP|CLEANUP_FAILED|FileNotFoundException' -f $PackageId))
    }
    [System.IO.File]::WriteAllLines((Join-Path $TargetDirectory 'JueMingR.Validation\phase-0-s-evidence.log'), $lines, (New-Object System.Text.UTF8Encoding($false)))
}

function Assert-Phase0SExitAndNoWrite {
    param(
        [Parameter(Mandatory = $true)][pscustomobject] $Result,
        [Parameter(Mandatory = $true)][int] $ExpectedExitCode,
        [Parameter(Mandatory = $true)][object[]] $Before,
        [Parameter(Mandatory = $true)][string] $Target,
        [Parameter(Mandatory = $true)][string] $Scenario,
        [Parameter(Mandatory = $true)][string] $Operation,
        [Parameter(Mandatory = $true)][string] $ExpectedCode,
        [Parameter(Mandatory = $true)][string] $ExpectedStatus
    )

    Assert-Phase0SCompactJsonResult -Result $Result -Operation $Operation -ExpectedExitCode $ExpectedExitCode -ExpectedCode $ExpectedCode -ExpectedStatus $ExpectedStatus -TargetDirectory $Target
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
    $supportScript = Join-Path $RepositoryRoot 'scripts\phase0s\Phase0S.ScriptSupport.ps1'
    if (-not [System.IO.File]::Exists($installScript) -or -not [System.IO.File]::Exists($restoreScript) -or -not [System.IO.File]::Exists($supportScript)) {
        throw 'Phase 0-S install, restore, and support production scripts are required before behavior tests can run.'
    }

    $root = New-Phase0STestRoot
    try {
        $packageRoot = Join-Path $root 'controlled-package'
        $packageManifest = New-Phase0SControlledPackageFixture -RepositoryRoot $RepositoryRoot -PackageRoot $packageRoot -TerrariaIdentityInput $terrariaIdentityInput

        $existingConfig = New-Phase0STargetDirectory -Root $root -TerrariaIdentityInput $terrariaIdentityInput -Name 'existing-config'
        [System.IO.File]::WriteAllText((Join-Path $existingConfig 'Terraria.exe.config'), 'external-config', (New-Object System.Text.UTF8Encoding($false)))
        $before = Get-Phase0STreeSnapshot -Root $existingConfig
        Assert-Phase0SExitAndNoWrite -Result (Invoke-Phase0SPackageScript -PackageRoot $packageRoot -ScriptName 'Install-Phase0S.ps1' -TerrariaDirectory $existingConfig) -ExpectedExitCode 10 -Before $before -Target $existingConfig -Scenario 'existing config' -Operation 'install' -ExpectedCode 'CONFIG_EXISTS' -ExpectedStatus 'conflict'

        $missingExeConfig = Join-Path $root 'config-short-circuit-missing-exe'
        [System.IO.Directory]::CreateDirectory($missingExeConfig) | Out-Null
        [System.IO.File]::WriteAllText((Join-Path $missingExeConfig 'Terraria.exe.config'), 'external-config', (New-Object System.Text.UTF8Encoding($false)))
        $before = Get-Phase0STreeSnapshot -Root $missingExeConfig
        Assert-Phase0SExitAndNoWrite -Result (Invoke-Phase0SPackageScript -PackageRoot $packageRoot -ScriptName 'Install-Phase0S.ps1' -TerrariaDirectory $missingExeConfig) -ExpectedExitCode 10 -Before $before -Target $missingExeConfig -Scenario 'config short-circuit missing Terraria.exe' -Operation 'install' -ExpectedCode 'CONFIG_EXISTS' -ExpectedStatus 'conflict'

        $invalidExeConfig = New-Phase0STargetDirectory -Root $root -TerrariaIdentityInput $terrariaIdentityInput -Name 'config-short-circuit-invalid-exe'
        [System.IO.File]::WriteAllText((Join-Path $invalidExeConfig 'Terraria.exe'), 'not-a-terraria-assembly', (New-Object System.Text.UTF8Encoding($false)))
        [System.IO.File]::WriteAllText((Join-Path $invalidExeConfig 'Terraria.exe.config'), 'external-config', (New-Object System.Text.UTF8Encoding($false)))
        $before = Get-Phase0STreeSnapshot -Root $invalidExeConfig
        Assert-Phase0SExitAndNoWrite -Result (Invoke-Phase0SPackageScript -PackageRoot $packageRoot -ScriptName 'Install-Phase0S.ps1' -TerrariaDirectory $invalidExeConfig) -ExpectedExitCode 10 -Before $before -Target $invalidExeConfig -Scenario 'config short-circuit invalid Terraria.exe' -Operation 'install' -ExpectedCode 'CONFIG_EXISTS' -ExpectedStatus 'conflict'

        $invalidPackageRoot = Join-Path $root 'invalid-package'
        Copy-Item -LiteralPath $packageRoot -Destination $invalidPackageRoot -Recurse
        [System.IO.File]::WriteAllText((Join-Path $invalidPackageRoot 'phase-0-s-package.manifest.json'), '{', (New-Object System.Text.UTF8Encoding($false)))
        $invalidPackageConfig = New-Phase0STargetDirectory -Root $root -TerrariaIdentityInput $terrariaIdentityInput -Name 'config-short-circuit-invalid-package'
        [System.IO.File]::WriteAllText((Join-Path $invalidPackageConfig 'Terraria.exe.config'), 'external-config', (New-Object System.Text.UTF8Encoding($false)))
        $before = Get-Phase0STreeSnapshot -Root $invalidPackageConfig
        Assert-Phase0SExitAndNoWrite -Result (Invoke-Phase0SPackageScript -PackageRoot $invalidPackageRoot -ScriptName 'Install-Phase0S.ps1' -TerrariaDirectory $invalidPackageConfig) -ExpectedExitCode 10 -Before $before -Target $invalidPackageConfig -Scenario 'config short-circuit invalid package' -Operation 'install' -ExpectedCode 'CONFIG_EXISTS' -ExpectedStatus 'conflict'

        $allConflictConfig = New-Phase0STargetDirectory -Root $root -TerrariaIdentityInput $terrariaIdentityInput -Name 'config-short-circuit-all-conflicts'
        [System.IO.File]::WriteAllText((Join-Path $allConflictConfig 'Terraria.exe'), 'not-a-terraria-assembly', (New-Object System.Text.UTF8Encoding($false)))
        [System.IO.File]::WriteAllText((Join-Path $allConflictConfig 'Terraria.exe.config'), 'external-config', (New-Object System.Text.UTF8Encoding($false)))
        [System.IO.File]::WriteAllText((Join-Path $allConflictConfig 'JueMingR.Bootstrap.dll'), 'external-bootstrap', (New-Object System.Text.UTF8Encoding($false)))
        [System.IO.Directory]::CreateDirectory((Join-Path $allConflictConfig 'JueMingR.Validation')) | Out-Null
        [System.IO.File]::WriteAllText((Join-Path $allConflictConfig 'Terraria.exe.config.phase0s-temp'), 'temp', (New-Object System.Text.UTF8Encoding($false)))
        [System.IO.File]::WriteAllText((Join-Path $allConflictConfig 'JueMingR.Bootstrap.dll.phase0s-temp'), 'temp', (New-Object System.Text.UTF8Encoding($false)))
        [System.IO.Directory]::CreateDirectory((Join-Path $allConflictConfig 'JueMingR.Validation.phase0s-stage')) | Out-Null
        $before = Get-Phase0STreeSnapshot -Root $allConflictConfig
        Assert-Phase0SExitAndNoWrite -Result (Invoke-Phase0SPackageScript -PackageRoot $invalidPackageRoot -ScriptName 'Install-Phase0S.ps1' -TerrariaDirectory $allConflictConfig) -ExpectedExitCode 10 -Before $before -Target $allConflictConfig -Scenario 'config short-circuit invalid Terraria, package, and later conflicts' -Operation 'install' -ExpectedCode 'CONFIG_EXISTS' -ExpectedStatus 'conflict'

        $bootstrapConflict = New-Phase0STargetDirectory -Root $root -TerrariaIdentityInput $terrariaIdentityInput -Name 'bootstrap-conflict'
        [System.IO.File]::WriteAllText((Join-Path $bootstrapConflict 'JueMingR.Bootstrap.dll'), 'external-bootstrap', (New-Object System.Text.UTF8Encoding($false)))
        $before = Get-Phase0STreeSnapshot -Root $bootstrapConflict
        Assert-Phase0SExitAndNoWrite -Result (Invoke-Phase0SPackageScript -PackageRoot $packageRoot -ScriptName 'Install-Phase0S.ps1' -TerrariaDirectory $bootstrapConflict) -ExpectedExitCode 11 -Before $before -Target $bootstrapConflict -Scenario 'Bootstrap conflict' -Operation 'install' -ExpectedCode 'BOOTSTRAP_CONFLICT' -ExpectedStatus 'conflict'

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
            Assert-Phase0SExitAndNoWrite -Result (Invoke-Phase0SPackageScript -PackageRoot $packageRoot -ScriptName 'Install-Phase0S.ps1' -TerrariaDirectory $conflictTarget) -ExpectedExitCode 12 -Before $before -Target $conflictTarget -Scenario $conflictName -Operation 'install' -ExpectedCode 'WORK_PATH_CONFLICT' -ExpectedStatus 'conflict'
        }

        $identityMismatch = Join-Path $root 'identity-mismatch'
        [System.IO.Directory]::CreateDirectory($identityMismatch) | Out-Null
        [System.IO.File]::WriteAllText((Join-Path $identityMismatch 'Terraria.exe'), 'not-a-terraria-assembly', (New-Object System.Text.UTF8Encoding($false)))
        $before = Get-Phase0STreeSnapshot -Root $identityMismatch
        Assert-Phase0SExitAndNoWrite -Result (Invoke-Phase0SPackageScript -PackageRoot $packageRoot -ScriptName 'Install-Phase0S.ps1' -TerrariaDirectory $identityMismatch) -ExpectedExitCode 4 -Before $before -Target $identityMismatch -Scenario 'Terraria identity mismatch' -Operation 'install' -ExpectedCode 'TERRARIA_IDENTITY_MISMATCH' -ExpectedStatus 'failure'

        $successTarget = New-Phase0STargetDirectory -Root $root -TerrariaIdentityInput $terrariaIdentityInput -Name 'success'
        $installResult = Invoke-Phase0SPackageScript -PackageRoot $packageRoot -ScriptName 'Install-Phase0S.ps1' -TerrariaDirectory $successTarget
        Assert-Phase0SCompactJsonResult -Result $installResult -Operation 'install' -ExpectedExitCode 0 -ExpectedCode 'INSTALL_COMPLETE' -ExpectedStatus 'success' -TargetDirectory $successTarget
        Assert-Phase0SInstalledLayout -Target $successTarget
        Assert-Phase0SReceiptMatchesPackageManifest -PackageRoot $packageRoot -TargetDirectory $successTarget
        Write-Phase0SEvidence -TargetDirectory $successTarget -PackageId ([string] $packageManifest.packageId) -Kind 'complete'
        $restoreResult = Invoke-Phase0SPackageScript -PackageRoot $packageRoot -ScriptName 'Restore-Phase0S.ps1' -TerrariaDirectory $successTarget
        Assert-Phase0SCompactJsonResult -Result $restoreResult -Operation 'restore' -ExpectedExitCode 0 -ExpectedCode 'RESTORE_COMPLETE' -ExpectedStatus 'success' -TargetDirectory $successTarget
        $afterRestore = Get-Phase0STreeSnapshot -Root $successTarget
        Assert-Phase0STreeSnapshotEqual -Expected (Get-Phase0STreeSnapshot -Root (New-Phase0STargetDirectory -Root $root -TerrariaIdentityInput $terrariaIdentityInput -Name 'restore-baseline')) -Actual $afterRestore -Context 'exact restore'

        $prefixTarget = New-Phase0STargetDirectory -Root $root -TerrariaIdentityInput $terrariaIdentityInput -Name 'evidence-prefix'
        $installResult = Invoke-Phase0SPackageScript -PackageRoot $packageRoot -ScriptName 'Install-Phase0S.ps1' -TerrariaDirectory $prefixTarget
        Assert-Phase0SCompactJsonResult -Result $installResult -Operation 'install' -ExpectedExitCode 0 -ExpectedCode 'INSTALL_COMPLETE' -ExpectedStatus 'success' -TargetDirectory $prefixTarget
        Write-Phase0SEvidence -TargetDirectory $prefixTarget -PackageId ([string] $packageManifest.packageId) -Kind 'prefix'
        $restoreResult = Invoke-Phase0SPackageScript -PackageRoot $packageRoot -ScriptName 'Restore-Phase0S.ps1' -TerrariaDirectory $prefixTarget
        Assert-Phase0SCompactJsonResult -Result $restoreResult -Operation 'restore' -ExpectedExitCode 0 -ExpectedCode 'RESTORE_COMPLETE' -ExpectedStatus 'success' -TargetDirectory $prefixTarget

        foreach ($validFailureEvidence in @('primary', 'primary-cleanup')) {
            $failureTarget = New-Phase0STargetDirectory -Root $root -TerrariaIdentityInput $terrariaIdentityInput -Name ('valid-evidence-' + $validFailureEvidence)
            $installResult = Invoke-Phase0SPackageScript -PackageRoot $packageRoot -ScriptName 'Install-Phase0S.ps1' -TerrariaDirectory $failureTarget
            Assert-Phase0SCompactJsonResult -Result $installResult -Operation 'install' -ExpectedExitCode 0 -ExpectedCode 'INSTALL_COMPLETE' -ExpectedStatus 'success' -TargetDirectory $failureTarget
            Write-Phase0SEvidence -TargetDirectory $failureTarget -PackageId ([string] $packageManifest.packageId) -Kind $validFailureEvidence
            $restoreResult = Invoke-Phase0SPackageScript -PackageRoot $packageRoot -ScriptName 'Restore-Phase0S.ps1' -TerrariaDirectory $failureTarget
            Assert-Phase0SCompactJsonResult -Result $restoreResult -Operation 'restore' -ExpectedExitCode 0 -ExpectedCode 'RESTORE_COMPLETE' -ExpectedStatus 'success' -TargetDirectory $failureTarget
        }

        foreach ($invalidEvidence in @('wrong-package', 'out-of-order', 'unknown')) {
            $invalidEvidenceTarget = New-Phase0STargetDirectory -Root $root -TerrariaIdentityInput $terrariaIdentityInput -Name ('evidence-' + $invalidEvidence)
            $installResult = Invoke-Phase0SPackageScript -PackageRoot $packageRoot -ScriptName 'Install-Phase0S.ps1' -TerrariaDirectory $invalidEvidenceTarget
            Assert-Phase0SCompactJsonResult -Result $installResult -Operation 'install' -ExpectedExitCode 0 -ExpectedCode 'INSTALL_COMPLETE' -ExpectedStatus 'success' -TargetDirectory $invalidEvidenceTarget
            Write-Phase0SEvidence -TargetDirectory $invalidEvidenceTarget -PackageId ([string] $packageManifest.packageId) -Kind $invalidEvidence
            $before = Get-Phase0STreeSnapshot -Root $invalidEvidenceTarget
            Assert-Phase0SExitAndNoWrite -Result (Invoke-Phase0SPackageScript -PackageRoot $packageRoot -ScriptName 'Restore-Phase0S.ps1' -TerrariaDirectory $invalidEvidenceTarget) -ExpectedExitCode 30 -Before $before -Target $invalidEvidenceTarget -Scenario ('invalid evidence ' + $invalidEvidence) -Operation 'restore' -ExpectedCode 'OWNERSHIP_UNPROVEN' -ExpectedStatus 'failure'
        }

        $noopBefore = Get-Phase0STreeSnapshot -Root $successTarget
        Assert-Phase0SExitAndNoWrite -Result (Invoke-Phase0SPackageScript -PackageRoot $packageRoot -ScriptName 'Restore-Phase0S.ps1' -TerrariaDirectory $successTarget) -ExpectedExitCode 0 -Before $noopBefore -Target $successTarget -Scenario 'second restore noop' -Operation 'restore' -ExpectedCode 'RESTORE_NOOP' -ExpectedStatus 'noop'

        foreach ($modification in @('modified-static', 'unknown-sidecar')) {
            $modifiedTarget = New-Phase0STargetDirectory -Root $root -TerrariaIdentityInput $terrariaIdentityInput -Name $modification
            $installResult = Invoke-Phase0SPackageScript -PackageRoot $packageRoot -ScriptName 'Install-Phase0S.ps1' -TerrariaDirectory $modifiedTarget
            Assert-Phase0SCompactJsonResult -Result $installResult -Operation 'install' -ExpectedExitCode 0 -ExpectedCode 'INSTALL_COMPLETE' -ExpectedStatus 'success' -TargetDirectory $modifiedTarget
            if ($modification -eq 'modified-static') {
                [System.IO.File]::AppendAllText((Join-Path $modifiedTarget 'JueMingR.Bootstrap.dll'), 'external-change')
            }
            else {
                [System.IO.File]::WriteAllText((Join-Path $modifiedTarget 'JueMingR.Validation\unknown.txt'), 'external-file', (New-Object System.Text.UTF8Encoding($false)))
            }
            $before = Get-Phase0STreeSnapshot -Root $modifiedTarget
            Assert-Phase0SExitAndNoWrite -Result (Invoke-Phase0SPackageScript -PackageRoot $packageRoot -ScriptName 'Restore-Phase0S.ps1' -TerrariaDirectory $modifiedTarget) -ExpectedExitCode 30 -Before $before -Target $modifiedTarget -Scenario $modification -Operation 'restore' -ExpectedCode 'OWNERSHIP_UNPROVEN' -ExpectedStatus 'failure'
        }
    }
    finally {
        Remove-Phase0STestRoot -Root $root
    }
}

if ($Run) {
    if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
        throw 'The -Run entry point requires -RepositoryRoot.'
    }
    Invoke-Phase0SInstallRecoveryTests -RepositoryRoot $RepositoryRoot
}
