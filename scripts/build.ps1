[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',
    [string] $TerrariaReferencesDirectory,
    [string] $OutputDirectory,
    [switch] $SkipArchitectureTests,
    [switch] $RequireClean,
    [switch] $NoRestore
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$script:RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$script:BuildMarkerName = '.juemingr-phase0r-build-output'
$script:BuildMarkerValue = 'scripts/build.ps1 schema 1'
$script:Utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Resolve-UnresolvedPath {
    param([string] $Path)

    return [System.IO.Path]::GetFullPath(
        $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path))
}

function Invoke-External {
    param(
        [string] $FilePath,
        [string[]] $Arguments,
        [string] $FailureMessage
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage (exit $LASTEXITCODE)."
    }
}

function Get-TextSha256 {
    param([string] $Text)

    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
        return ([System.BitConverter]::ToString($algorithm.ComputeHash($bytes))).Replace('-', '')
    }
    finally {
        $algorithm.Dispose()
    }
}

function Get-NormalizedTextFileSha256 {
    param([string] $Path)

    $text = [System.IO.File]::ReadAllText($Path)
    $normalized = $text.Replace("`r`n", "`n").Replace("`r", "`n")
    return Get-TextSha256 -Text $normalized
}

function Get-GitOutput {
    param([string[]] $Arguments)

    $output = & git -c core.safecrlf=false @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git command failed: git $($Arguments -join ' ')"
    }

    return @($output)
}

function Get-DirtyIdentity {
    param(
        [string] $Root,
        [string[]] $StatusLines
    )

    $parts = New-Object System.Collections.Generic.List[string]
    $parts.Add('status')
    foreach ($line in $StatusLines) { $parts.Add($line) }
    $parts.Add('working-diff')
    foreach ($line in Get-GitOutput -Arguments @('diff', '--binary', 'HEAD', '--')) { $parts.Add([string] $line) }
    $parts.Add('staged-diff')
    foreach ($line in Get-GitOutput -Arguments @('diff', '--cached', '--binary', 'HEAD', '--')) { $parts.Add([string] $line) }

    foreach ($relativePath in @(Get-GitOutput -Arguments @('-c', 'core.quotePath=false', 'ls-files', '--others', '--exclude-standard')) | Sort-Object) {
        $absolutePath = Join-Path $Root $relativePath
        if ([System.IO.File]::Exists($absolutePath)) {
            $parts.Add(('untracked|{0}|{1}' -f $relativePath.Replace('\', '/'), (Get-FileHash -LiteralPath $absolutePath -Algorithm SHA256).Hash))
        }
    }

    return Get-TextSha256 -Text ($parts -join "`n")
}

function Assert-SafeBuildOutput {
    param(
        [string] $Root,
        [string] $ConfigurationRoot,
        [string] $ReferencesRoot
    )

    $normalizedRoot = $Root.TrimEnd('\') + '\'
    $normalizedConfiguration = $ConfigurationRoot.TrimEnd('\') + '\'
    if ($ConfigurationRoot -eq $Root -or
        $ConfigurationRoot -eq $ReferencesRoot -or
        $Root.StartsWith($normalizedConfiguration, [StringComparison]::OrdinalIgnoreCase) -or
        $ReferencesRoot.StartsWith($normalizedConfiguration, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'OutputDirectory resolves to a protected source, repository, or reference path.'
    }

    if ($ConfigurationRoot.Length -le 3) {
        throw 'OutputDirectory is too broad.'
    }
}

function Remove-OwnedBuildOutput {
    param([string] $ConfigurationRoot)

    if (-not [System.IO.Directory]::Exists($ConfigurationRoot)) {
        return
    }

    $marker = Join-Path $ConfigurationRoot $script:BuildMarkerName
    if (-not [System.IO.File]::Exists($marker) -or
        [System.IO.File]::ReadAllText($marker).Trim() -ne $script:BuildMarkerValue) {
        throw "Refusing to replace an output directory not owned by scripts/build.ps1: $ConfigurationRoot"
    }

    [System.IO.Directory]::Delete($ConfigurationRoot, $true)
}

function Assert-NoForbiddenOutput {
    param([string] $ConfigurationRoot)

    foreach ($file in Get-ChildItem -LiteralPath $ConfigurationRoot -File -Recurse) {
        $name = $file.Name
        if ($name -eq 'Terraria.exe' -or
            $name -eq 'ReLogic.dll' -or
            $name -like 'Microsoft.Xna.Framework*.dll' -or
            $name -eq '0Harmony.dll' -or
            $file.FullName.IndexOf('JueMingZ', [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "Forbidden game, Harmony, or Legacy file found in build output: $name"
        }
    }
}

Push-Location $script:RepositoryRoot
$buildStarted = [DateTime]::UtcNow
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
try {
    if ([string]::IsNullOrWhiteSpace($TerrariaReferencesDirectory)) {
        $TerrariaReferencesDirectory = Join-Path $script:RepositoryRoot 'external\TerrariaRefs'
    }
    if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
        $OutputDirectory = Join-Path $script:RepositoryRoot 'artifacts\build'
    }

    $referencesRoot = Resolve-UnresolvedPath -Path $TerrariaReferencesDirectory
    $outputRoot = Resolve-UnresolvedPath -Path $OutputDirectory
    $configurationRoot = [System.IO.Path]::GetFullPath((Join-Path $outputRoot $Configuration))
    $workRoot = Join-Path $configurationRoot 'work'
    Assert-SafeBuildOutput -Root $script:RepositoryRoot -ConfigurationRoot $configurationRoot -ReferencesRoot $referencesRoot

    $globalJsonPath = Join-Path $script:RepositoryRoot 'global.json'
    $globalJson = Get-Content -LiteralPath $globalJsonPath -Raw | ConvertFrom-Json
    $expectedSdk = [string] $globalJson.sdk.version
    $actualSdk = (& dotnet --version).Trim()
    if ($LASTEXITCODE -ne 0 -or $actualSdk -ne $expectedSdk) {
        throw "global.json requires .NET SDK $expectedSdk, but dotnet selected '$actualSdk'."
    }

    $targetingPackRoot = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2'
    $targetingPackMscorlib = Join-Path $targetingPackRoot 'mscorlib.dll'
    if (-not [System.IO.File]::Exists($targetingPackMscorlib)) {
        throw '.NET Framework 4.7.2 Developer Pack / Targeting Pack is missing.'
    }
    $targetingPackVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($targetingPackMscorlib).FileVersion

    & (Join-Path $PSScriptRoot 'prepare-terraria-references.ps1') -DestinationDirectory $referencesRoot -VerifyOnly

    $trackedFiles = @(Get-GitOutput -Arguments @('-c', 'core.quotePath=false', 'ls-files'))
    foreach ($trackedFile in $trackedFiles) {
        $fileName = [System.IO.Path]::GetFileName([string] $trackedFile)
        if ($fileName -eq 'Terraria.exe' -or
            $fileName -eq 'ReLogic.dll' -or
            $fileName -like 'Microsoft.Xna.Framework*.dll' -or
            $fileName -eq '0Harmony.dll') {
            throw "Git tracks a forbidden game/runtime binary: $trackedFile"
        }
    }

    $commitLines = @(Get-GitOutput -Arguments @('rev-parse', 'HEAD'))
    $commit = $commitLines[0].Trim()
    $statusLines = @(Get-GitOutput -Arguments @('status', '--porcelain=v1', '--untracked-files=all'))
    $isClean = $statusLines.Count -eq 0
    if ($RequireClean -and -not $isClean) {
        throw 'RequireClean was specified, but the Git working tree has tracked or non-ignored untracked changes.'
    }

    $dirtyIdentity = if ($isClean) { '' } else { Get-DirtyIdentity -Root $script:RepositoryRoot -StatusLines $statusLines }
    $sourceRevisionId = if ($isClean) { $commit } else { $commit + '.dirty.' + $dirtyIdentity.Substring(0, 16).ToLowerInvariant() }

    if ($NoRestore) {
        $marker = Join-Path $configurationRoot $script:BuildMarkerName
        if (-not [System.IO.File]::Exists($marker) -or
            [System.IO.File]::ReadAllText($marker).Trim() -ne $script:BuildMarkerValue) {
            throw 'NoRestore requires an existing output root previously created by scripts/build.ps1.'
        }

        $binRoot = Join-Path $workRoot 'bin'
        if ([System.IO.Directory]::Exists($binRoot)) {
            [System.IO.Directory]::Delete($binRoot, $true)
        }
    }
    else {
        Remove-OwnedBuildOutput -ConfigurationRoot $configurationRoot
        [System.IO.Directory]::CreateDirectory($configurationRoot) | Out-Null
        [System.IO.File]::WriteAllText(
            (Join-Path $configurationRoot $script:BuildMarkerName),
            $script:BuildMarkerValue + [Environment]::NewLine,
            $script:Utf8NoBom)
    }

    [System.IO.Directory]::CreateDirectory($workRoot) | Out-Null
    $commonProperties = @(
        '-p:Platform=x86',
        ('-p:JueMingRBuildRoot={0}' -f $workRoot),
        ('-p:TerrariaReferencesDirectory={0}' -f $referencesRoot),
        ('-p:SourceRevisionId={0}' -f $sourceRevisionId)
    )
    if (-not $NoRestore) {
        Invoke-External -FilePath 'dotnet' -Arguments (@('restore', '.\JueMingR.sln', '-nologo') + $commonProperties) -FailureMessage 'Solution restore failed'
    }

    $buildArguments = @('build', '.\JueMingR.sln', '-c', $Configuration, '-nologo', '--no-restore') + $commonProperties
    Invoke-External -FilePath 'dotnet' -Arguments $buildArguments -FailureMessage 'Solution build failed'

    $architectureResult = 'SKIPPED for diagnostics'
    if (-not $SkipArchitectureTests) {
        $architectureExecutables = @(
            Get-ChildItem -LiteralPath (Join-Path $workRoot 'bin\JueMingR.ArchitectureTests') -Filter 'JueMingR.ArchitectureTests.exe' -File -Recurse
        )
        if ($architectureExecutables.Count -ne 1) {
            throw 'Expected exactly one ArchitectureTests executable in the formal build output.'
        }

        Invoke-External -FilePath $architectureExecutables[0].FullName -Arguments @($script:RepositoryRoot) -FailureMessage 'Architecture tests failed'
        $architectureResult = 'PASS'
    }

    Assert-NoForbiddenOutput -ConfigurationRoot $configurationRoot
    $declaredOutputs = New-Object System.Collections.Generic.List[object]
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $workRoot 'bin') -File -Recurse |
        Where-Object { $_.Extension -eq '.dll' -or $_.Extension -eq '.exe' -or $_.Extension -eq '.pdb' } |
        Sort-Object FullName) {
        $relativePath = $file.FullName.Substring($workRoot.Length).TrimStart('\').Replace('\', '/')
        $declaredOutputs.Add([ordered]@{
            path = $relativePath
            size = $file.Length
            sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
        })
    }
    if ($declaredOutputs.Count -eq 0) {
        throw 'The formal build produced no declared DLL, EXE, or PDB outputs.'
    }

    $baselinePath = Join-Path $script:RepositoryRoot 'eng\TerrariaReferences.baseline.json'
    $baseline = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
    $msbuildVersion = ((& dotnet msbuild -version -nologo) | Select-Object -Last 1).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Could not read the locked MSBuild version.' }
    $dotnetPath = (Get-Command dotnet).Source
    $compilerPath = Join-Path ([System.IO.Path]::GetDirectoryName($dotnetPath)) ("sdk\$actualSdk\Roslyn\bincore\csc.dll")
    $compilerVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($compilerPath).FileVersion
    $referenceHashes = @($baseline.files | ForEach-Object {
        [ordered]@{ logicalName = $_.logicalName; sha256 = $_.sha256 }
    })

    $stopwatch.Stop()
    $buildEnded = [DateTime]::UtcNow
    $record = [ordered]@{
        schemaVersion = 1
        source = [ordered]@{
            commit = $commit
            clean = $isClean
            dirtyDiffIdentitySha256 = $dirtyIdentity
            sourceRevisionId = $sourceRevisionId
        }
        build = [ordered]@{
            configuration = $Configuration
            targetFramework = 'net472'
            platformTarget = 'x86'
            sdk = $actualSdk
            msbuild = $msbuildVersion
            compiler = $compilerVersion
            powerShell = $PSVersionTable.PSVersion.ToString()
            developerPack = ".NET Framework 4.7.2 reference assemblies $targetingPackVersion"
            entry = 'scripts/build.ps1'
            effectiveParameters = [ordered]@{
                configuration = $Configuration
                references = 'local directory verified against the tracked baseline; path omitted'
                output = 'ignored local artifacts directory; path omitted'
                skipArchitectureTests = [bool] $SkipArchitectureTests
                requireClean = [bool] $RequireClean
                noRestore = [bool] $NoRestore
            }
            startedUtc = $buildStarted.ToString('o')
            endedUtc = $buildEnded.ToString('o')
            elapsedMilliseconds = $stopwatch.ElapsedMilliseconds
        }
        references = [ordered]@{
            profileId = $baseline.profileId
            baselineSha256 = Get-NormalizedTextFileSha256 -Path $baselinePath
            files = $referenceHashes
        }
        architectureChecks = $architectureResult
        outputs = @($declaredOutputs.ToArray())
        unverifiedAxes = @(
            'Terraria loading',
            'hooks',
            'UI',
            'features',
            'multiplayer',
            'runtime performance',
            'installation and recovery',
            'project-owner game testing',
            'release'
        )
    }
    $recordPath = Join-Path $configurationRoot 'build-record.json'
    [System.IO.File]::WriteAllText(
        $recordPath,
        (($record | ConvertTo-Json -Depth 10) + [Environment]::NewLine),
        $script:Utf8NoBom)

    Write-Output ("PASS: {0} build, architecture checks={1}, declared outputs={2}." -f $Configuration, $architectureResult, $declaredOutputs.Count)
    Write-Output ("Build record: {0}" -f $recordPath)
}
finally {
    if ($stopwatch.IsRunning) {
        $stopwatch.Stop()
    }
    Pop-Location
}
