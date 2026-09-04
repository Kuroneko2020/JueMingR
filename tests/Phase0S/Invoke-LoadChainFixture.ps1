[CmdletBinding()]
param(
    [switch] $Run,
    [string] $RepositoryRoot
)

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
    $buildOutput = @(& dotnet.exe build $projectPath --configuration Debug --nologo -p:Platform=x86 2>&1)
    foreach ($line in $buildOutput) {
        Write-Host $line
    }
    if ($LASTEXITCODE -ne 0) {
        throw 'The Phase 0-S fake Terraria fixture did not build.'
    }

    $fixtureExe = Join-Path $RepositoryRoot 'tests\Phase0SFixtureTerraria\bin\x86\Debug\net472\Terraria.exe'
    if (-not [System.IO.File]::Exists($fixtureExe)) {
        throw "The fake Terraria fixture executable is missing: $fixtureExe"
    }
    return $fixtureExe
}

function Get-Phase0SFixtureDriverExecutable {
    param([Parameter(Mandatory = $true)][string] $RepositoryRoot)

    $projectPath = Join-Path $RepositoryRoot 'tests\Phase0SFixtureTerraria\Phase0SFixtureTerraria.csproj'
    $outputRoot = Join-Path $RepositoryRoot 'artifacts\phase0s-fixture-driver\bin\'
    $objectRoot = Join-Path $RepositoryRoot 'artifacts\phase0s-fixture-driver\obj\'
    $buildOutput = @(& dotnet.exe build $projectPath --configuration Debug --nologo -p:Platform=x86 -p:AssemblyName=Phase0SFixtureDriver -p:EmbedReLogicResource=false -p:BaseOutputPath=$outputRoot -p:BaseIntermediateOutputPath=$objectRoot 2>&1)
    foreach ($line in $buildOutput) {
        Write-Host $line
    }
    if ($LASTEXITCODE -ne 0) {
        throw 'The Phase 0-S fixture driver did not build.'
    }

    $driverExe = Join-Path $outputRoot 'x86\Debug\net472\Phase0SFixtureDriver.exe'
    if (-not [System.IO.File]::Exists($driverExe)) {
        throw "The Phase 0-S fixture driver is missing: $driverExe"
    }
    return $driverExe
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

    foreach ($xnaFileName in @('Microsoft.Xna.Framework.dll', 'Microsoft.Xna.Framework.Graphics.dll')) {
        $xnaPath = Join-Path $RepositoryRoot ('external\TerrariaRefs\' + $xnaFileName)
        [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($xnaPath) | Out-Null
    }

    $target = Get-Phase0SReflectionIdentity -AssemblyPath $FixtureExe
    $hostIdentity = Get-Phase0SReflectionIdentity -AssemblyPath $HostAssembly
    $targetAssembly = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($FixtureExe)
    $targetType = $targetAssembly.GetType('Terraria.Main', $true)
    $updateMethods = @($targetType.GetMethods(
        [System.Reflection.BindingFlags]'Instance, Public, NonPublic, DeclaredOnly') | Where-Object {
            $parameters = @($_.GetParameters())
            $_.Name -ceq 'Update' -and
            -not $_.IsStatic -and
            -not $_.IsGenericMethod -and
            $_.ReturnType.FullName -ceq 'System.Void' -and
            $parameters.Count -eq 1 -and
            $parameters[0].ParameterType.FullName -ceq 'Microsoft.Xna.Framework.GameTime'
        })
    if ($updateMethods.Count -ne 1) {
        throw 'The fake Terraria fixture does not expose exactly one Main.Update(GameTime) target.'
    }
    $updateMethod = $updateMethods[0]
    if ($SourceCommit -notmatch '^[0-9a-f]{40}$') {
        throw 'The fixture runtime manifest requires a lowercase 40-character source commit.'
    }
    $targetHash = if ($UseWrongTargetHash) { ('0' * 64) } else { $target.sha256 }
    $lines = @(
        'schemaVersion=2',
        ('packageId=' + $PackageId),
        ('sourceCommit=' + $SourceCommit),
        ('targetAssemblySimpleName=' + $target.simpleName),
        ('targetAssemblyVersion=' + $target.version),
        ('targetAssemblyMvid=' + $target.mvid),
        ('targetAssemblySha256=' + $targetHash),
        'reLogicAssemblySimpleName=ReLogic',
        'reLogicAssemblyVersion=1.0.0.0',
        'reLogicAssemblyPublicKeyToken=null',
        'reLogicAssemblyMvid=ee258be9-88a4-423d-b3ce-84b6c35b141a',
        'reLogicResourceName=Terraria.Libraries.ReLogic.ReLogic.dll',
        'reLogicResourceSha256=E1C5DCCEFFF5FD1C789FF712BABFA1A305FCED0D03C96EF30F2C14D99AA0AF29',
        'targetTypeName=Terraria.Main',
        'targetMethodName=Update',
        ('targetMethodMetadataToken=0x{0:X8}' -f $updateMethod.MetadataToken),
        'targetMethodIsStatic=false',
        'targetMethodReturnType=System.Void',
        'targetMethodParameterCount=1',
        'targetMethodParameterType=Microsoft.Xna.Framework.GameTime',
        ('hostAssemblySimpleName=' + $hostIdentity.simpleName),
        ('hostAssemblyVersion=' + $hostIdentity.version),
        ('hostAssemblyMvid=' + $hostIdentity.mvid),
        ('hostAssemblySha256=' + $hostIdentity.sha256),
        'harmonyAssemblySimpleName=0Harmony',
        'harmonyAssemblyVersion=2.4.2.0',
        'harmonyAssemblyMvid=024a0e6e-c8c2-437e-ad04-7b6279389c23',
        'harmonyAssemblySha256=7B9E756306FA3D7620E02A857C8927A6AB04973F9BD8A77D3866700A6DEAC55C',
        'patchOwner=JueMingR.Phase0S.MainUpdate',
        'evidenceFileName=phase-0-s-evidence.log'
    )
    [System.IO.File]::WriteAllLines($ManifestPath, $lines, (New-Object System.Text.UTF8Encoding($false)))
    Assert-Phase0SCondition -Condition (([System.IO.File]::ReadAllLines($ManifestPath)).Count -eq 30) -Message 'The fixture runtime manifest must contain exactly 30 lines.'
}

function New-Phase0SFixtureConfig {
    param(
        [Parameter(Mandatory = $true)][string] $ConfigPath,
        [switch] $WithoutManager
    )

    $managerLines = if ($WithoutManager) { '' } else {
@'
    <appDomainManagerAssembly value="JueMingR.Bootstrap, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null" />
    <appDomainManagerType value="JueMingR.Bootstrap.Phase0SAppDomainManager" />
'@
    }
    $config = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <runtime>
$managerLines
    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
      <probing privatePath="JueMingR.Validation" />
    </assemblyBinding>
  </runtime>
</configuration>
"@
    [System.IO.File]::WriteAllText($ConfigPath, $config, (New-Object System.Text.UTF8Encoding($false)))
}

function New-Phase0SDriverRunDirectory {
    param(
        [Parameter(Mandatory = $true)][string] $Root,
        [Parameter(Mandatory = $true)][string] $Name,
        [Parameter(Mandatory = $true)][string] $FixtureExe,
        [Parameter(Mandatory = $true)][string] $DriverExe,
        [Parameter(Mandatory = $true)][string] $ReLogicPath,
        [Parameter(Mandatory = $true)][hashtable] $ProductionOutputs,
        [Parameter(Mandatory = $true)][string] $HarmonyPath,
        [Parameter(Mandatory = $true)][string] $PackageId,
        [Parameter(Mandatory = $true)][string] $SourceCommit
    )

    $runRoot = Join-Path $Root $Name
    $sidecar = Join-Path $runRoot 'JueMingR.Validation'
    [System.IO.Directory]::CreateDirectory($sidecar) | Out-Null
    Copy-Item -LiteralPath $FixtureExe -Destination (Join-Path $runRoot 'Terraria.exe')
    $runDriverExe = Join-Path $runRoot 'Phase0SFixtureDriver.exe'
    Copy-Item -LiteralPath $DriverExe -Destination $runDriverExe
    Copy-Item -LiteralPath $ReLogicPath -Destination (Join-Path $runRoot 'fixture-relogic.bin')
    Copy-Item -LiteralPath $ProductionOutputs['Bootstrap'] -Destination (Join-Path $runRoot 'JueMingR.Bootstrap.dll')
    foreach ($namePart in @('Host', 'Platform', 'Features', 'Infrastructure')) {
        Copy-Item -LiteralPath $ProductionOutputs[$namePart] -Destination (Join-Path $sidecar (Split-Path -Leaf $ProductionOutputs[$namePart]))
    }
    Copy-Item -LiteralPath $HarmonyPath -Destination (Join-Path $sidecar '0Harmony.dll')
    New-Phase0SFixtureRuntimeManifest -FixtureExe $FixtureExe -HostAssembly $ProductionOutputs['Host'] -ManifestPath (Join-Path $sidecar 'phase-0-s-runtime.manifest') -PackageId $PackageId -SourceCommit $SourceCommit
    New-Phase0SFixtureConfig -ConfigPath ($runDriverExe + '.config') -WithoutManager
    return [pscustomobject]@{
        exePath = $runDriverExe
        evidencePath = Join-Path $sidecar 'phase-0-s-evidence.log'
        packageId = $PackageId
    }
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
    New-Phase0SFixtureRuntimeManifest -FixtureExe $FixtureExe -HostAssembly $ProductionOutputs['Host'] -ManifestPath (Join-Path $sidecar 'phase-0-s-runtime.manifest') -PackageId $PackageId -SourceCommit $SourceCommit -UseWrongTargetHash:$UseWrongTargetHash
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
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        Set-Location -LiteralPath (Split-Path -Parent $FixtureExe)
        $ErrorActionPreference = 'Continue'
        $output = @(& $FixtureExe $Mode $EvidencePath $PackageId 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
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

function Assert-Phase0SSourceContract {
    param([Parameter(Mandatory = $true)][string] $RepositoryRoot)

    $hostPath = Join-Path $RepositoryRoot 'src\JueMingR.TerrariaHost\Phase0SLoadChainHost.cs'
    $bootstrapPath = Join-Path $RepositoryRoot 'src\JueMingR.Bootstrap\Phase0SAppDomainManager.cs'
    $hostText = [System.IO.File]::ReadAllText($hostPath)
    $bootstrapText = [System.IO.File]::ReadAllText($bootstrapPath)
    $patchCalls = [regex]::Matches($hostText, 'harmony\.Patch\(')
    if ($patchCalls.Count -ne 2 -or
        [regex]::Matches($hostText, 'new HarmonyMethod\(postfixMethod\)').Count -ne 1 -or
        [regex]::Matches($hostText, 'new HarmonyMethod\(drawSetupPostfixMethod\)').Count -ne 1 -or
        $hostText -notmatch '(?s)private static void Postfix\(\).*?Interlocked\.CompareExchange\(ref postfixGate, 1, 0\) == 0.*?context\.UpdateRuntime\(\);' -or
        $hostText -notmatch '(?s)private static void DrawSetupPostfix\(List<GameInterfaceLayer> ____gameInterfaceLayers\).*?InsertBiomeLayer\(____gameInterfaceLayers\);' -or
        $hostText -match 'OneTimeDiagnosticPrefix|Phase0SDiagnosticSentinel|MAIN_INITIALIZE_POSTFIX_FIRED' -or
        $hostText -match 'Task\.Run|new\s+Thread\s*\(|new\s+(?:System\.Threading\.)?Timer\s*\(|ConcurrentQueue|Queue<') {
        throw 'The production Host source does not match the single Update postfix, single draw setup postfix, one-shot evidence, no-diagnostic/no-background contract.'
    }

    if ([regex]::Matches($bootstrapText, 'ThreadPool\.QueueUserWorkItem\(').Count -ne 1 -or
        $bootstrapText -match 'Task\.Run|new\s+Thread\s*\(|new\s+(?:System\.Threading\.)?Timer\s*\(|Thread\.Sleep|SpinWait|ConcurrentQueue|Queue<' -or
        $bootstrapText -notmatch '(?s)Waiting\s*,\s*Queued\s*,\s*Installing\s*,\s*Installed\s*,\s*Failed' -or
        $bootstrapText -notmatch '(?s)state = InstallState\.Installing;\s*if \(assemblyLoadHandlerDepth != 0\).*?Install\(request\.TargetAssembly, request\.ReLogicAssembly\)' -or
        $bootstrapText -notmatch 'ReferenceEquals\(AppDomain\.CurrentDomain, request\.AppDomain\)' -or
        $bootstrapText -notmatch 'ReferenceEquals\(targetAssembly, request\.TargetAssembly\)' -or
        $bootstrapText -notmatch 'ReferenceEquals\(reLogicAssembly, request\.ReLogicAssembly\)') {
        throw 'The Bootstrap source does not match the one-work-item, callback-outside, exact-object lifecycle contract.'
    }

    foreach ($removedPath in @(
        'src\JueMingR.Bootstrap\Phase0SDiagnosticSentinel.cs',
        'src\JueMingR.TerrariaHost\Phase0SDiagnosticSentinel.cs')) {
        if ([System.IO.File]::Exists((Join-Path $RepositoryRoot $removedPath))) {
            throw "Removed diagnostic source still exists: $removedPath"
        }
    }
}

function Invoke-Phase0SLoadChainFixtureTests {
    param([Parameter(Mandatory = $true)][string] $RepositoryRoot)

    Assert-Phase0SSourceContract -RepositoryRoot $RepositoryRoot
    $buildResult = Invoke-Phase0SWindowsPowerShell -ScriptPath (Join-Path $RepositoryRoot 'scripts\build.ps1') -Arguments @('-Configuration', 'Debug')
    foreach ($line in $buildResult.output) {
        Write-Host $line
    }
    if ($buildResult.exitCode -ne 0) {
        throw "The Phase 0-S production Debug build failed with exit $($buildResult.exitCode)."
    }
    $sourceCommit = (& git -C $RepositoryRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $sourceCommit -notmatch '^[0-9a-f]{40}$') {
        throw 'The fixture harness requires a source commit identity.'
    }
    $fixtureExe = Get-Phase0SFixtureExecutable -RepositoryRoot $RepositoryRoot
    $driverExe = Get-Phase0SFixtureDriverExecutable -RepositoryRoot $RepositoryRoot
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
    $reLogicPath = Join-Path $RepositoryRoot 'external\TerrariaRefs\ReLogic.dll'
    if (-not [System.IO.File]::Exists($reLogicPath)) {
        throw 'The fixed ReLogic test input is unavailable.'
    }

    $root = New-Phase0STestRoot
    try {
        $success = New-Phase0SFixtureRunDirectory -Root $root -Name 'success' -FixtureExe $fixtureExe -ProductionOutputs $productionOutputs -HarmonyPath $harmonyPath -PackageId ('phase0s-fixture-' + [Guid]::NewGuid().ToString('N')) -SourceCommit $sourceCommit
        $successResult = Invoke-Phase0SFixtureExe -FixtureExe $success.exePath -Mode 'expect-handoff' -EvidencePath $success.evidencePath -PackageId $success.packageId
        foreach ($line in $successResult.output) {
            Write-Host $line
        }
        Assert-Phase0SCondition -Condition ($successResult.exitCode -eq 0) -Message "load-chain success fixture: expected exit 0, actual $($successResult.exitCode)."

        foreach ($driverMode in @(
            'driver-relogic-then-terraria',
            'driver-terraria-then-relogic',
            'driver-both-before-subscription',
            'driver-duplicate-scan',
            'driver-update-before-install',
            'driver-worker-failure',
            'driver-wrong-relogic',
            'driver-two-relogic',
            'driver-relogic-never')) {
            $driverRun = New-Phase0SDriverRunDirectory -Root $root -Name $driverMode -FixtureExe $fixtureExe -DriverExe $driverExe -ReLogicPath $reLogicPath -ProductionOutputs $productionOutputs -HarmonyPath $harmonyPath -PackageId ('phase0s-fixture-' + [Guid]::NewGuid().ToString('N')) -SourceCommit $sourceCommit
            $driverResult = Invoke-Phase0SFixtureExe -FixtureExe $driverRun.exePath -Mode $driverMode -EvidencePath $driverRun.evidencePath -PackageId $driverRun.packageId
            foreach ($line in $driverResult.output) {
                Write-Host $line
            }
            Assert-Phase0SCondition -Condition ($driverResult.exitCode -eq 0) -Message "$driverMode fixture: expected exit 0, actual $($driverResult.exitCode)."
        }

        $wrongHash = New-Phase0SFixtureRunDirectory -Root $root -Name 'wrong-target-hash' -FixtureExe $fixtureExe -ProductionOutputs $productionOutputs -HarmonyPath $harmonyPath -PackageId ('phase0s-fixture-' + [Guid]::NewGuid().ToString('N')) -SourceCommit $sourceCommit -UseWrongTargetHash
        $wrongHashResult = Invoke-Phase0SFixtureExe -FixtureExe $wrongHash.exePath -Mode 'expect-no-handoff' -EvidencePath $wrongHash.evidencePath -PackageId $wrongHash.packageId
        foreach ($line in $wrongHashResult.output) {
            Write-Host $line
        }
        Assert-Phase0SCondition -Condition ($wrongHashResult.exitCode -eq 0) -Message "wrong target hash fixture: expected exit 0, actual $($wrongHashResult.exitCode)."
        Assert-Phase0SNoSuccessEvents -EvidencePath $wrongHash.evidencePath
    }
    finally {
        Remove-Phase0STestRoot -Root $root
    }
}

if ($Run) {
    if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
        throw 'The -Run entry point requires -RepositoryRoot.'
    }
    Invoke-Phase0SLoadChainFixtureTests -RepositoryRoot $RepositoryRoot
}
