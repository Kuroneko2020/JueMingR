[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

. (Join-Path $PSScriptRoot 'TestSupport.ps1')

function Get-Phase0SReflectionIdentity {
    param([Parameter(Mandatory = $true)][string] $AssemblyPath)

    $assembly = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($AssemblyPath)
    return [ordered]@{
        simpleName = $assembly.GetName().Name
        version = $assembly.GetName().Version.ToString()
        mvid = $assembly.ManifestModule.ModuleVersionId.ToString()
        sha256 = Get-Phase0SFileSha256 -Path $AssemblyPath
    }
}

function Get-Phase0SFixtureExecutable {
    param([Parameter(Mandatory = $true)][string] $RepositoryRoot)

    $projectPath = Join-Path $RepositoryRoot 'tests\Phase0SFixtureTerraria\Phase0SFixtureTerraria.csproj'
    & dotnet.exe build $projectPath --configuration Debug --nologo -p:Platform=x86
    if ($LASTEXITCODE -ne 0) {
        throw 'The Phase 0-S fake Terraria fixture did not build.'
    }

    $fixtureExe = Join-Path $RepositoryRoot 'tests\Phase0SFixtureTerraria\bin\x86\Debug\net472\Terraria.exe'
    if (-not [System.IO.File]::Exists($fixtureExe)) {
        throw "The fake Terraria fixture executable is missing: $fixtureExe"
    }
    return $fixtureExe
}

function Get-Phase0SProductionOutput {
    param(
        [Parameter(Mandatory = $true)][string] $RepositoryRoot,
        [Parameter(Mandatory = $true)][string] $ProjectName,
        [Parameter(Mandatory = $true)][string] $FileName
    )

    $path = Join-Path $RepositoryRoot ('artifacts\build\Debug\work\bin\' + $ProjectName + '\x86\Debug\net472\' + $FileName)
    if (-not [System.IO.File]::Exists($path)) {
        throw "The Phase 0-S production build did not produce $ProjectName/$FileName."
    }
    return $path
}

function New-Phase0SFixtureRuntimeManifest {
    param(
        [Parameter(Mandatory = $true)][string] $FixtureExe,
        [Parameter(Mandatory = $true)][string] $HostAssembly,
        [Parameter(Mandatory = $true)][string] $ManifestPath,
        [Parameter(Mandatory = $true)][string] $PackageId,
        [Parameter(Mandatory = $true)][string] $SourceCommit,
        [switch] $UseWrongTargetHash
    )

    $target = Get-Phase0SReflectionIdentity -AssemblyPath $FixtureExe
    $host = Get-Phase0SReflectionIdentity -AssemblyPath $HostAssembly
    $targetAssembly = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($FixtureExe)
    $initializeMethod = $targetAssembly.GetType('Terraria.Main', $true).GetMethod(
        'Initialize',
        [System.Reflection.BindingFlags]'Instance, NonPublic, DeclaredOnly')
    if ($null -eq $initializeMethod -or $initializeMethod.IsStatic -or
        $initializeMethod.ReturnType.FullName -cne 'System.Void' -or
        $initializeMethod.GetParameters().Count -ne 0) {
        throw 'The fake Terraria fixture does not expose the required parameterless protected Main.Initialize target.'
    }
    if ($SourceCommit -notmatch '^[0-9a-f]{40}$') {
        throw 'The fixture runtime manifest requires a lowercase 40-character source commit.'
    }
    $targetHash = if ($UseWrongTargetHash) { ('0' * 64) } else { $target.sha256 }
    $lines = @(
        'schemaVersion=1',
        'packageId=' + $PackageId,
        'sourceCommit=' + $SourceCommit,
        'targetAssemblySimpleName=' + $target.simpleName,
        'targetAssemblyVersion=' + $target.version,
        'targetAssemblyMvid=' + $target.mvid,
        'targetAssemblySha256=' + $targetHash,
        'targetTypeName=Terraria.Main',
        'targetMethodName=Initialize',
        ('targetMethodMetadataToken=0x{0:X8}' -f $initializeMethod.MetadataToken),
        'targetMethodIsStatic=false',
        'targetMethodReturnType=System.Void',
        'targetMethodParameterCount=0',
        'hostAssemblySimpleName=' + $host.simpleName,
        'hostAssemblyVersion=' + $host.version,
        'hostAssemblyMvid=' + $host.mvid,
        'hostAssemblySha256=' + $host.sha256,
        'harmonyAssemblySimpleName=0Harmony',
        'harmonyAssemblyVersion=2.4.2.0',
        'harmonyAssemblyMvid=024a0e6e-c8c2-437e-ad04-7b6279389c23',
        'harmonyAssemblySha256=7B9E756306FA3D7620E02A857C8927A6AB04973F9BD8A77D3866700A6DEAC55C',
        'patchOwner=JueMingR.Phase0S.MainInitialize',
        'evidenceFileName=phase-0-s-evidence.log'
    )
    [System.IO.File]::WriteAllLines($ManifestPath, $lines, (New-Object System.Text.UTF8Encoding($false)))
}

function New-Phase0SFixtureConfig {
    param([Parameter(Mandatory = $true)][string] $ConfigPath)

    $config = @'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <runtime>
    <appDomainManagerAssembly value="JueMingR.Bootstrap, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null" />
    <appDomainManagerType value="JueMingR.Bootstrap.Phase0SAppDomainManager" />
    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
      <probing privatePath="JueMingR.Validation" />
    </assemblyBinding>
  </runtime>
</configuration>
'@
    [System.IO.File]::WriteAllText($ConfigPath, $config, (New-Object System.Text.UTF8Encoding($false)))
}

function New-Phase0SFixtureRunDirectory {
    param(
        [Parameter(Mandatory = $true)][string] $Root,
        [Parameter(Mandatory = $true)][string] $Name,
        [Parameter(Mandatory = $true)][string] $FixtureExe,
        [Parameter(Mandatory = $true)][hashtable] $ProductionOutputs,
        [Parameter(Mandatory = $true)][string] $HarmonyPath,
        [Parameter(Mandatory = $true)][string] $PackageId,
        [Parameter(Mandatory = $true)][string] $SourceCommit,
        [switch] $UseWrongTargetHash
    )

    $runRoot = Join-Path $Root $Name
    $sidecar = Join-Path $runRoot 'JueMingR.Validation'
    [System.IO.Directory]::CreateDirectory($sidecar) | Out-Null
    $runFixtureExe = Join-Path $runRoot 'Terraria.exe'
    Copy-Item -LiteralPath $FixtureExe -Destination $runFixtureExe
    Copy-Item -LiteralPath $ProductionOutputs['Bootstrap'] -Destination (Join-Path $runRoot 'JueMingR.Bootstrap.dll')
    foreach ($name in @('Host', 'Platform', 'Features', 'Infrastructure')) {
        Copy-Item -LiteralPath $ProductionOutputs[$name] -Destination (Join-Path $sidecar (Split-Path -Leaf $ProductionOutputs[$name]))
    }
    Copy-Item -LiteralPath $HarmonyPath -Destination (Join-Path $sidecar '0Harmony.dll')
    New-Phase0SFixtureRuntimeManifest -FixtureExe $runFixtureExe -HostAssembly (Join-Path $sidecar 'JueMingR.TerrariaHost.dll') -ManifestPath (Join-Path $sidecar 'phase-0-s-runtime.manifest') -PackageId $PackageId -SourceCommit $SourceCommit -UseWrongTargetHash:$UseWrongTargetHash
    New-Phase0SFixtureConfig -ConfigPath (Join-Path $runRoot 'Terraria.exe.config')
    return [pscustomobject]@{
        exePath = $runFixtureExe
        evidencePath = Join-Path $sidecar 'phase-0-s-evidence.log'
        packageId = $PackageId
    }
}

function Invoke-Phase0SFixtureExe {
    param(
        [Parameter(Mandatory = $true)][string] $FixtureExe,
        [Parameter(Mandatory = $true)][string] $Mode,
        [Parameter(Mandatory = $true)][string] $EvidencePath,
        [Parameter(Mandatory = $true)][string] $PackageId
    )

    $previousLocation = Get-Location
    try {
        Set-Location -LiteralPath (Split-Path -Parent $FixtureExe)
        $output = @(& $FixtureExe $Mode $EvidencePath $PackageId 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        Set-Location -LiteralPath $previousLocation.Path
    }
    return [pscustomobject]@{ exitCode = $exitCode; output = @($output) }
}

function Assert-Phase0SNoSuccessEvents {
    param([Parameter(Mandatory = $true)][string] $EvidencePath)

    if (-not [System.IO.File]::Exists($EvidencePath)) {
        return
    }
    foreach ($line in [System.IO.File]::ReadAllLines($EvidencePath)) {
        if ($line -match '^PHASE0S\|1\|[^|]+\|0[1-5]\|') {
            throw 'The wrong-target-hash fixture emitted a success event instead of failing closed.'
        }
    }
}

function Invoke-Phase0SLoadChainFixtureTests {
    param([Parameter(Mandatory = $true)][string] $RepositoryRoot)

    & (Join-Path $RepositoryRoot 'scripts\build.ps1') -Configuration Debug
    if ($LASTEXITCODE -ne 0) {
        throw 'The Phase 0-S production Debug build failed.'
    }
    $sourceCommit = (& git -C $RepositoryRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $sourceCommit -notmatch '^[0-9a-f]{40}$') {
        throw 'The fixture harness requires a source commit identity.'
    }
    $fixtureExe = Get-Phase0SFixtureExecutable -RepositoryRoot $RepositoryRoot
    $productionOutputs = @{
        Bootstrap = Get-Phase0SProductionOutput -RepositoryRoot $RepositoryRoot -ProjectName 'JueMingR.Bootstrap' -FileName 'JueMingR.Bootstrap.dll'
        Host = Get-Phase0SProductionOutput -RepositoryRoot $RepositoryRoot -ProjectName 'JueMingR.TerrariaHost' -FileName 'JueMingR.TerrariaHost.dll'
        Platform = Get-Phase0SProductionOutput -RepositoryRoot $RepositoryRoot -ProjectName 'JueMingR.Platform' -FileName 'JueMingR.Platform.dll'
        Features = Get-Phase0SProductionOutput -RepositoryRoot $RepositoryRoot -ProjectName 'JueMingR.Features' -FileName 'JueMingR.Features.dll'
        Infrastructure = Get-Phase0SProductionOutput -RepositoryRoot $RepositoryRoot -ProjectName 'JueMingR.Infrastructure' -FileName 'JueMingR.Infrastructure.dll'
    }
    $harmonyPath = Join-Path $RepositoryRoot 'external\Harmony\0Harmony.dll'
    if (-not [System.IO.File]::Exists($harmonyPath)) {
        throw 'The prepared official Harmony 2.4.2 asset is unavailable.'
    }

    $root = New-Phase0STestRoot
    try {
        $success = New-Phase0SFixtureRunDirectory -Root $root -Name 'success' -FixtureExe $fixtureExe -ProductionOutputs $productionOutputs -HarmonyPath $harmonyPath -PackageId ('phase0s-fixture-' + [Guid]::NewGuid().ToString('N')) -SourceCommit $sourceCommit
        $successResult = Invoke-Phase0SFixtureExe -FixtureExe $success.exePath -Mode 'expect-handoff' -EvidencePath $success.evidencePath -PackageId $success.packageId
        Assert-Phase0SCondition -Condition ($successResult.exitCode -eq 0) -Message "load-chain success fixture: expected exit 0, actual $($successResult.exitCode)."

        $wrongHash = New-Phase0SFixtureRunDirectory -Root $root -Name 'wrong-target-hash' -FixtureExe $fixtureExe -ProductionOutputs $productionOutputs -HarmonyPath $harmonyPath -PackageId ('phase0s-fixture-' + [Guid]::NewGuid().ToString('N')) -SourceCommit $sourceCommit -UseWrongTargetHash
        $wrongHashResult = Invoke-Phase0SFixtureExe -FixtureExe $wrongHash.exePath -Mode 'expect-no-handoff' -EvidencePath $wrongHash.evidencePath -PackageId $wrongHash.packageId
        Assert-Phase0SCondition -Condition ($wrongHashResult.exitCode -eq 0) -Message "wrong target hash fixture: expected exit 0, actual $($wrongHashResult.exitCode)."
        Assert-Phase0SNoSuccessEvents -EvidencePath $wrongHash.evidencePath
    }
    finally {
        Remove-Phase0STestRoot -Root $root
    }
}
