[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [string] $TerrariaInstallDirectory,
    [string] $XnaReferenceDirectory,
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$script:RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
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

function Get-GitLines {
    param(
        [string] $WorkingDirectory,
        [string[]] $Arguments
    )

    Push-Location $WorkingDirectory
    try {
        $output = & git @Arguments 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "git command failed: git $($Arguments -join ' ')"
        }

        return @($output)
    }
    finally {
        Pop-Location
    }
}

function Remove-ValidatedTemporaryRoot {
    param([string] $Path)

    if (-not [System.IO.Directory]::Exists($Path)) {
        return
    }

    $systemTemp = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    $resolved = [System.IO.Path]::GetFullPath($Path)
    $leaf = [System.IO.Path]::GetFileName($resolved)
    if (-not $resolved.StartsWith($systemTemp, [StringComparison]::OrdinalIgnoreCase) -or
        -not $leaf.StartsWith('JueMingR-Repro-', [StringComparison]::Ordinal)) {
        throw "Refusing to delete an unexpected temporary path: $resolved"
    }

    [System.IO.Directory]::Delete($resolved, $true)
}

function Invoke-CleanCloneBuild {
    param(
        [string] $CloneRoot,
        [string] $Commit,
        [string] $ConfigurationName,
        [string] $TerrariaSource,
        [string] $XnaSource
    )

    Invoke-External -FilePath 'git' -Arguments @(
        'clone',
        '--no-local',
        '--no-hardlinks',
        '--quiet',
        '--no-checkout',
        $script:RepositoryRoot,
        $CloneRoot
    ) -FailureMessage 'Temporary clean clone creation failed'
    Invoke-External -FilePath 'git' -Arguments @('-C', $CloneRoot, 'checkout', '--detach', '--quiet', $Commit) -FailureMessage 'Temporary clone checkout failed'

    $references = Join-Path $CloneRoot 'external\TerrariaRefs'
    if ([System.IO.Directory]::Exists($references)) {
        throw 'A clean clone unexpectedly inherited external/TerrariaRefs.'
    }

    $prepareParameters = @{
        DestinationDirectory = $references
    }
    if (-not [string]::IsNullOrWhiteSpace($TerrariaSource)) {
        $prepareParameters.TerrariaInstallDirectory = $TerrariaSource
    }
    if (-not [string]::IsNullOrWhiteSpace($XnaSource)) {
        $prepareParameters.XnaReferenceDirectory = $XnaSource
    }

    & (Join-Path $CloneRoot 'scripts\prepare-terraria-references.ps1') @prepareParameters

    $buildOutput = Join-Path $CloneRoot 'artifacts\build'
    & (Join-Path $CloneRoot 'scripts\build.ps1') `
        -Configuration $ConfigurationName `
        -TerrariaReferencesDirectory $references `
        -OutputDirectory $buildOutput `
        -RequireClean

    $recordPath = Join-Path $buildOutput ("$ConfigurationName\build-record.json")
    if (-not [System.IO.File]::Exists($recordPath)) {
        throw 'A clean clone build did not produce its build record.'
    }

    $record = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
    if ($record.source.commit -ne $Commit -or $record.source.clean -ne $true) {
        throw 'A clean clone build record does not identify the requested clean commit.'
    }

    $workRoot = Join-Path $buildOutput "$ConfigurationName\work"
    $outputs = New-Object System.Collections.Generic.List[object]
    foreach ($entry in $record.outputs) {
        $relativePath = [string] $entry.path
        $physicalPath = Join-Path $workRoot $relativePath.Replace('/', '\')
        if (-not [System.IO.File]::Exists($physicalPath)) {
            throw "Declared output is missing from a clean clone: $relativePath"
        }

        $actualHash = (Get-FileHash -LiteralPath $physicalPath -Algorithm SHA256).Hash.ToUpperInvariant()
        if ($actualHash -ne $entry.sha256) {
            throw "Build record output hash does not match the physical file: $relativePath"
        }

        $extension = [System.IO.Path]::GetExtension($physicalPath)
        if ($extension -ne '.dll' -and $extension -ne '.exe' -and $extension -ne '.pdb') {
            throw "Build record declares an unsupported reproducibility output: $relativePath"
        }

        $outputs.Add([ordered]@{
            path = $relativePath
            size = [int64] $entry.size
            sha256 = $actualHash
        })
    }

    foreach ($file in Get-ChildItem -LiteralPath $workRoot -File -Recurse) {
        if ($file.Name -eq 'Terraria.exe' -or
            $file.Name -eq 'ReLogic.dll' -or
            $file.Name -like 'Microsoft.Xna.Framework*.dll' -or
            $file.Name -eq '0Harmony.dll') {
            throw "A clean clone output contains a forbidden game/runtime binary: $($file.Name)"
        }
    }

    return [ordered]@{
        record = $record
        outputs = @($outputs.ToArray())
    }
}

Push-Location $script:RepositoryRoot
$temporaryRoot = $null
$result = $null
$started = [DateTime]::UtcNow
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $status = @(Get-GitLines -WorkingDirectory $script:RepositoryRoot -Arguments @('status', '--porcelain=v1', '--untracked-files=all'))
    if ($status.Count -ne 0) {
        throw 'Reproducibility verification requires a clean committed source tree.'
    }

    $commitLines = @(Get-GitLines -WorkingDirectory $script:RepositoryRoot -Arguments @('rev-parse', 'HEAD'))
    $commit = $commitLines[0].Trim()
    if ([string]::IsNullOrWhiteSpace($commit)) {
        throw 'Reproducibility verification requires an existing source commit.'
    }

    $temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('JueMingR-Repro-' + [Guid]::NewGuid().ToString('N'))
    [System.IO.Directory]::CreateDirectory($temporaryRoot) | Out-Null
    $cloneA = Join-Path $temporaryRoot 'clone-a'
    $cloneB = Join-Path $temporaryRoot 'clone-b-with-a-different-path'

    $first = Invoke-CleanCloneBuild -CloneRoot $cloneA -Commit $commit -ConfigurationName $Configuration -TerrariaSource $TerrariaInstallDirectory -XnaSource $XnaReferenceDirectory
    $second = Invoke-CleanCloneBuild -CloneRoot $cloneB -Commit $commit -ConfigurationName $Configuration -TerrariaSource $TerrariaInstallDirectory -XnaSource $XnaReferenceDirectory

    if ($first.record.build.sdk -ne $second.record.build.sdk -or
        $first.record.build.developerPack -ne $second.record.build.developerPack -or
        $first.record.references.baselineSha256 -ne $second.record.references.baselineSha256) {
        throw 'The two clean builds did not use identical SDK, targeting pack, and reference baseline identities.'
    }

    $firstByPath = @{}
    foreach ($entry in $first.outputs) { $firstByPath[[string] $entry.path] = $entry }
    $secondByPath = @{}
    foreach ($entry in $second.outputs) { $secondByPath[[string] $entry.path] = $entry }
    $allPaths = @($firstByPath.Keys + $secondByPath.Keys | Sort-Object -Unique)
    $comparisons = New-Object System.Collections.Generic.List[object]
    $differenceCount = 0
    foreach ($path in $allPaths) {
        $firstEntry = $firstByPath[$path]
        $secondEntry = $secondByPath[$path]
        $matches = $null -ne $firstEntry -and
            $null -ne $secondEntry -and
            $firstEntry.size -eq $secondEntry.size -and
            $firstEntry.sha256 -eq $secondEntry.sha256
        if (-not $matches) {
            $differenceCount++
        }

        $comparisons.Add([ordered]@{
            path = $path
            firstSha256 = if ($null -eq $firstEntry) { '' } else { $firstEntry.sha256 }
            secondSha256 = if ($null -eq $secondEntry) { '' } else { $secondEntry.sha256 }
            matches = $matches
        })
        Write-Output ("{0}: {1}" -f $(if ($matches) { 'MATCH' } else { 'DIFFERENT' }), $path)
    }

    if ($differenceCount -ne 0) {
        throw "Reproducibility verification found $differenceCount differing declared output(s)."
    }

    $stopwatch.Stop()
    $result = [ordered]@{
        schemaVersion = 1
        sourceCommit = $commit
        configuration = $Configuration
        sdk = $first.record.build.sdk
        msbuild = $first.record.build.msbuild
        developerPack = $first.record.build.developerPack
        referenceProfileId = $first.record.references.profileId
        referenceBaselineSha256 = $first.record.references.baselineSha256
        independentCloneCount = 2
        inheritedReferenceDirectory = $false
        declaredOutputCount = $allPaths.Count
        differenceCount = $differenceCount
        comparisons = @($comparisons.ToArray())
        forbiddenGameFilesInOutputs = 0
        startedUtc = $started.ToString('o')
        endedUtc = [DateTime]::UtcNow.ToString('o')
        elapsedMilliseconds = $stopwatch.ElapsedMilliseconds
        temporaryClones = 'cleaned before summary was written'
    }
}
finally {
    if ($stopwatch.IsRunning) {
        $stopwatch.Stop()
    }

    if ($null -ne $temporaryRoot) {
        Remove-ValidatedTemporaryRoot -Path $temporaryRoot
    }

    Pop-Location
}

if ($null -eq $result) {
    throw 'Reproducibility verification did not produce a result.'
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $script:RepositoryRoot 'artifacts\reproducibility'
}
$summaryRoot = Resolve-UnresolvedPath -Path (Join-Path $OutputDirectory $Configuration)
if ($summaryRoot -eq $script:RepositoryRoot -or $summaryRoot.Length -le 3) {
    throw 'Reproducibility summary directory is unsafe.'
}
[System.IO.Directory]::CreateDirectory($summaryRoot) | Out-Null
$summaryPath = Join-Path $summaryRoot 'reproducibility-summary.json'
[System.IO.File]::WriteAllText(
    $summaryPath,
    (($result | ConvertTo-Json -Depth 10) + [Environment]::NewLine),
    $script:Utf8NoBom)

Write-Output ("PASS: two independent clean clones produced {0} byte-identical declared DLL/EXE/PDB outputs; differences=0." -f $result.declaredOutputCount)
Write-Output ("Reproducibility summary: {0}" -f $summaryPath)
