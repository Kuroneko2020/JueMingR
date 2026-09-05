[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $TerrariaExePath,
    [Parameter(Mandatory = $true)]
    [string] $XnaGameAssemblyPath,
    [Parameter(Mandatory = $true)]
    [string] $XnaFrameworkAssemblyPath,
    [Parameter(Mandatory = $true)]
    [string] $XnaGraphicsAssemblyPath,
    [Parameter(Mandatory = $true)]
    [string] $HarmonyPackagePath,
    [switch] $VerifyPhase0TBiomePackage,
    [switch] $VerifyPhase0UF5UIPackage
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
if ($VerifyPhase0TBiomePackage -and $VerifyPhase0UF5UIPackage) { throw 'Select one validation package profile.' }

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$terrariaSource = [System.IO.Path]::GetFullPath($TerrariaExePath)
$xnaSource = [System.IO.Path]::GetFullPath($XnaGameAssemblyPath)
$xnaFrameworkSource = [System.IO.Path]::GetFullPath($XnaFrameworkAssemblyPath)
$xnaGraphicsSource = [System.IO.Path]::GetFullPath($XnaGraphicsAssemblyPath)
$harmonyPackageSource = [System.IO.Path]::GetFullPath($HarmonyPackagePath)
$powershellPath = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$identifier = [Guid]::NewGuid().ToString('N')
$worktreeA = Join-Path ([System.IO.Path]::GetTempPath()) ("JueMingR-Repro-$identifier-a")
$worktreeB = Join-Path ([System.IO.Path]::GetTempPath()) ("JueMingR-Repro-$identifier-b-with-a-different-path")
$packageOutputA = Join-Path ([System.IO.Path]::GetTempPath()) ("JueMingR-Repro-$identifier-package-a")
$packageOutputB = Join-Path ([System.IO.Path]::GetTempPath()) ("JueMingR-Repro-$identifier-package-b")
$createdWorktrees = New-Object System.Collections.Generic.List[string]
$packageOutputRoots = New-Object System.Collections.Generic.List[string]

function Invoke-Git {
    param([string[]] $Arguments)

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = @(& git -C $repositoryRoot @Arguments 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    if ($exitCode -ne 0) {
        throw ('Git command failed: ' + ($output -join [Environment]::NewLine))
    }
    return $output
}

function Invoke-WorktreeBuild {
    param([string] $Worktree)

    $prepareOutput = @(& $powershellPath -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
        -File (Join-Path $Worktree 'scripts\prepare-terraria-references.ps1') `
        -TerrariaExePath $terrariaSource `
        -XnaGameAssemblyPath $xnaSource `
        -XnaFrameworkAssemblyPath $xnaFrameworkSource `
        -XnaGraphicsAssemblyPath $xnaGraphicsSource)
    if ($LASTEXITCODE -ne 0) {
        throw ('Reference preparation failed: ' + ($prepareOutput -join [Environment]::NewLine))
    }

    $harmonyPrepareOutput = @(& $powershellPath -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
        -File (Join-Path $Worktree 'scripts\prepare-harmony.ps1') `
        -HarmonyPackagePath $harmonyPackageSource)
    if ($LASTEXITCODE -ne 0) {
        throw ('Harmony preparation failed: ' + ($harmonyPrepareOutput -join [Environment]::NewLine))
    }

    $buildOutput = @(& $powershellPath -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
        -File (Join-Path $Worktree 'scripts\build.ps1') `
        -Configuration Release `
        -RequireClean)
    if ($LASTEXITCODE -ne 0) {
        throw ('Release build failed: ' + ($buildOutput -join [Environment]::NewLine))
    }

    $recordPath = Join-Path $Worktree 'artifacts\build\Release\build-record.json'
    if (-not [System.IO.File]::Exists($recordPath)) {
        throw "Build record is missing in worktree: $Worktree."
    }

    return Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
}

function Invoke-WorktreeValidationPackage {
    param(
        [Parameter(Mandatory = $true)][string] $Worktree,
        [Parameter(Mandatory = $true)][string] $OutputDirectory,
        [Parameter(Mandatory = $true)][string] $Commit
    )

    $packageOutput = @(& $powershellPath -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
        -File (Join-Path $Worktree 'scripts\build-phase0s-validation-package.ps1') `
        -OutputDirectory $OutputDirectory -Profile $(if ($VerifyPhase0UF5UIPackage) { 'Phase0UF5UI' } else { 'Phase0TBiome' }))
    if ($LASTEXITCODE -ne 0) {
        throw ('Validation package build failed: ' + ($packageOutput -join [Environment]::NewLine))
    }

    $zipName = $(if ($VerifyPhase0UF5UIPackage) { 'JueMingR-Phase0U-F5UI-' } else { 'JueMingR-Phase0T-Biome-' }) + $Commit + '.zip'
    $zipPath = Join-Path $OutputDirectory $zipName
    if (-not [System.IO.File]::Exists($zipPath)) {
        throw 'The selected candidate ZIP is missing.'
    }

    $zip = Get-Item -LiteralPath $zipPath -Force -ErrorAction Stop
    return [pscustomobject][ordered]@{
        fileName = $zipName
        length = [int64] $zip.Length
        sha256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToUpperInvariant()
    }
}

if (-not [System.IO.File]::Exists($terrariaSource) -or
    -not [System.IO.File]::Exists($xnaSource) -or
    -not [System.IO.File]::Exists($xnaFrameworkSource) -or
    -not [System.IO.File]::Exists($xnaGraphicsSource) -or
    -not [System.IO.File]::Exists($harmonyPackageSource)) {
    throw 'All explicit Terraria, XNA, and Harmony package source files must exist.'
}
if (-not [System.IO.File]::Exists($powershellPath)) {
    throw 'Windows PowerShell 5.1 is unavailable.'
}

$statusLines = @(Invoke-Git -Arguments @('status', '--porcelain=v1', '--untracked-files=all'))
if ($statusLines.Count -ne 0) {
    throw 'Reproducibility verification requires a clean source commit.'
}
$commit = [string] (Invoke-Git -Arguments @('rev-parse', 'HEAD') | Select-Object -First 1)
$commit = $commit.Trim()

try {
    Invoke-Git -Arguments @('worktree', 'add', '--detach', $worktreeA, $commit) | Out-Null
    $createdWorktrees.Add($worktreeA)
    Invoke-Git -Arguments @('worktree', 'add', '--detach', $worktreeB, $commit) | Out-Null
    $createdWorktrees.Add($worktreeB)

    $recordA = Invoke-WorktreeBuild -Worktree $worktreeA
    $recordB = Invoke-WorktreeBuild -Worktree $worktreeB
    $referencesA = @($recordA.references)
    $referencesB = @($recordB.references)
    if ($referencesA.Count -ne 6 -or $referencesB.Count -ne 6) {
        throw 'Each worktree build record must contain exactly six compile inputs.'
    }
    $referencesByNameB = @{}
    foreach ($reference in $referencesB) {
        $referencesByNameB[[string] $reference.logicalName] = $reference
    }
    foreach ($reference in $referencesA) {
        $logicalName = [string] $reference.logicalName
        if (-not $referencesByNameB.ContainsKey($logicalName) -or
            [string] $referencesByNameB[$logicalName].sha256 -cne [string] $reference.sha256 -or
            [string] $referencesByNameB[$logicalName].assemblySimpleName -cne [string] $reference.assemblySimpleName -or
            [string] $referencesByNameB[$logicalName].assemblyVersion -cne [string] $reference.assemblyVersion -or
            [string] $referencesByNameB[$logicalName].mvid -cne [string] $reference.mvid) {
            throw ('Compile input differences: ' + $logicalName)
        }
    }

    $outputsA = @($recordA.outputs)
    $outputsB = @($recordB.outputs)
    if ($outputsA.Count -ne $outputsB.Count) {
        throw 'The two worktrees produced different declared output counts.'
    }

    $byPathB = @{}
    foreach ($output in $outputsB) {
        $byPathB[[string] $output.path] = $output
    }
    $differences = New-Object System.Collections.Generic.List[string]
    foreach ($output in $outputsA) {
        $path = [string] $output.path
        if (-not $byPathB.ContainsKey($path) -or
            [string] $byPathB[$path].sha256 -cne [string] $output.sha256 -or
            [int64] $byPathB[$path].length -ne [int64] $output.length) {
            $differences.Add($path)
        }
    }
    if ($differences.Count -ne 0) {
        throw ('Declared output differences: ' + ($differences -join ', '))
    }

    $packageSummary = $null
    if ($VerifyPhase0TBiomePackage -or $VerifyPhase0UF5UIPackage) {
        $packageOutputRoots.Add($packageOutputA)
        $packageOutputRoots.Add($packageOutputB)
        $packageA = Invoke-WorktreeValidationPackage -Worktree $worktreeA -OutputDirectory $packageOutputA -Commit $commit
        $packageB = Invoke-WorktreeValidationPackage -Worktree $worktreeB -OutputDirectory $packageOutputB -Commit $commit
        if ($packageA.fileName -cne $packageB.fileName -or
            $packageA.length -ne $packageB.length -or
            $packageA.sha256 -cne $packageB.sha256) {
            throw 'The two worktrees produced different candidate ZIP bytes.'
        }
        $packageSummary = [ordered]@{
            fileName = $packageA.fileName
            length = $packageA.length
            sha256 = $packageA.sha256
            differenceCount = 0
        }
    }

    $summaryDirectory = Join-Path $repositoryRoot 'artifacts\reproducibility'
    [System.IO.Directory]::CreateDirectory($summaryDirectory) | Out-Null
    $summaryPath = Join-Path $summaryDirectory 'reproducibility-summary.json'
    $summary = [ordered]@{
        schemaVersion = 2
        commit = $commit
        declaredOutputCount = $outputsA.Count
        differenceCount = 0
        outputs = @($outputsA | ForEach-Object {
            [ordered]@{ path = [string] $_.path; sha256 = [string] $_.sha256 }
        })
    }
    if ($null -ne $packageSummary) {
        if ($VerifyPhase0UF5UIPackage) { $summary.phase0UF5UIPackage = $packageSummary }
        else { $summary.phase0TBiomePackage = $packageSummary }
    }
    $summaryJson = ($summary | ConvertTo-Json -Depth 5) + [Environment]::NewLine
    [System.IO.File]::WriteAllText($summaryPath, $summaryJson, (New-Object System.Text.UTF8Encoding($false)))
    Write-Output ("PASS: two TEMP worktrees produced {0} byte-identical declared outputs; differences=0." -f $outputsA.Count)
    if ($null -ne $packageSummary) {
        Write-Output ("PASS: two TEMP worktrees produced byte-identical selected candidate ZIPs; SHA-256={0}." -f $packageSummary.sha256)
    }
    Write-Output ("Reproducibility summary: {0}" -f $summaryPath)
}
finally {
    $cleanupFailures = New-Object System.Collections.Generic.List[string]
    $tempPrefix = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    foreach ($outputRoot in @($packageOutputRoots)) {
        try {
            $fullOutputRoot = [System.IO.Path]::GetFullPath($outputRoot)
            $expectedLeafPrefix = 'JueMingR-Repro-' + $identifier + '-package-'
            if (-not $fullOutputRoot.StartsWith($tempPrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
                -not (Split-Path -Leaf $fullOutputRoot).StartsWith($expectedLeafPrefix, [System.StringComparison]::Ordinal)) {
                throw 'Package output cleanup target is outside the fixed TEMP scope.'
            }
            if ([System.IO.Directory]::Exists($fullOutputRoot)) {
                Remove-Item -LiteralPath $fullOutputRoot -Recurse -Force
            }
        }
        catch {
            $cleanupFailures.Add($outputRoot)
        }
    }
    foreach ($worktree in @($createdWorktrees)) {
        & git -C $repositoryRoot worktree remove --force $worktree 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            $cleanupFailures.Add($worktree)
        }
    }
    & git -C $repositoryRoot worktree prune 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        $cleanupFailures.Add('worktree prune')
    }
    if ($cleanupFailures.Count -ne 0) {
        throw ('Could not clean reproducibility worktrees: ' + ($cleanupFailures -join ', '))
    }
}
