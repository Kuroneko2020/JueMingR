[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $TerrariaExePath,
    [Parameter(Mandatory = $true)]
    [string] $XnaGameAssemblyPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$terrariaSource = [System.IO.Path]::GetFullPath($TerrariaExePath)
$xnaSource = [System.IO.Path]::GetFullPath($XnaGameAssemblyPath)
$powershellPath = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$identifier = [Guid]::NewGuid().ToString('N')
$worktreeA = Join-Path ([System.IO.Path]::GetTempPath()) ("JueMingR-Repro-$identifier-a")
$worktreeB = Join-Path ([System.IO.Path]::GetTempPath()) ("JueMingR-Repro-$identifier-b-with-a-different-path")
$createdWorktrees = New-Object System.Collections.Generic.List[string]

function Invoke-Git {
    param([string[]] $Arguments)

    $output = @(& git -C $repositoryRoot @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw ('Git command failed: ' + ($output -join [Environment]::NewLine))
    }
    return $output
}

function Invoke-WorktreeBuild {
    param([string] $Worktree)

    & $powershellPath -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
        -File (Join-Path $Worktree 'scripts\prepare-terraria-references.ps1') `
        -TerrariaExePath $terrariaSource `
        -XnaGameAssemblyPath $xnaSource
    if ($LASTEXITCODE -ne 0) {
        throw "Reference preparation failed in worktree: $Worktree."
    }

    & $powershellPath -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
        -File (Join-Path $Worktree 'scripts\build.ps1') `
        -Configuration Release `
        -RequireClean
    if ($LASTEXITCODE -ne 0) {
        throw "Release build failed in worktree: $Worktree."
    }

    $recordPath = Join-Path $Worktree 'artifacts\build\Release\build-record.json'
    if (-not [System.IO.File]::Exists($recordPath)) {
        throw "Build record is missing in worktree: $Worktree."
    }

    return Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
}

if (-not [System.IO.File]::Exists($terrariaSource) -or
    -not [System.IO.File]::Exists($xnaSource)) {
    throw 'Both explicit legal reference source files must exist.'
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

    $summaryDirectory = Join-Path $repositoryRoot 'artifacts\reproducibility'
    [System.IO.Directory]::CreateDirectory($summaryDirectory) | Out-Null
    $summaryPath = Join-Path $summaryDirectory 'reproducibility-summary.json'
    $summary = [ordered]@{
        schemaVersion = 1
        commit = $commit
        declaredOutputCount = $outputsA.Count
        differenceCount = 0
        outputs = @($outputsA | ForEach-Object {
            [ordered]@{ path = [string] $_.path; sha256 = [string] $_.sha256 }
        })
    }
    $summaryJson = ($summary | ConvertTo-Json -Depth 5) + [Environment]::NewLine
    [System.IO.File]::WriteAllText($summaryPath, $summaryJson, (New-Object System.Text.UTF8Encoding($false)))
    Write-Output ("PASS: two TEMP worktrees produced {0} byte-identical declared outputs; differences=0." -f $outputsA.Count)
    Write-Output ("Reproducibility summary: {0}" -f $summaryPath)
}
finally {
    $cleanupFailures = New-Object System.Collections.Generic.List[string]
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
