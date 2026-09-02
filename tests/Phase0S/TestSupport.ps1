Set-StrictMode -Version 2.0

function Get-Phase0SSystemTempRoot {
    return [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\')
}

function Test-Phase0SPathIsInsideSystemTemp {
    param([Parameter(Mandatory = $true)][string] $Path)

    $tempRoot = (Get-Phase0SSystemTempRoot) + '\'
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    return $fullPath.StartsWith($tempRoot, [System.StringComparison]::OrdinalIgnoreCase)
}

function New-Phase0STestRoot {
    $tempRoot = Get-Phase0SSystemTempRoot
    $root = Join-Path $tempRoot ('JueMingR-Phase0S-Test-' + [Guid]::NewGuid().ToString('N'))
    if (-not (Test-Phase0SPathIsInsideSystemTemp -Path $root)) {
        throw 'The generated Phase 0-S test root is not under the system TEMP root.'
    }

    [System.IO.Directory]::CreateDirectory($root) | Out-Null
    $markerPath = Join-Path $root '.phase0s-test-root'
    [System.IO.File]::WriteAllText($markerPath, [Guid]::NewGuid().ToString('N'), (New-Object System.Text.UTF8Encoding($false)))
    return $root
}

function Remove-Phase0STestRoot {
    param([Parameter(Mandatory = $true)][string] $Root)

    $fullRoot = [System.IO.Path]::GetFullPath($Root)
    $markerPath = Join-Path $fullRoot '.phase0s-test-root'
    if (-not (Test-Phase0SPathIsInsideSystemTemp -Path $fullRoot) -or
        -not [System.IO.File]::Exists($markerPath)) {
        throw 'Refusing to remove a directory that is not a marked Phase 0-S system TEMP test root.'
    }

    Remove-Item -LiteralPath $fullRoot -Recurse -Force
}

function Get-Phase0SFileSha256 {
    param([Parameter(Mandatory = $true)][string] $Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Get-Phase0STreeSnapshot {
    param([Parameter(Mandatory = $true)][string] $Root)

    $fullRoot = [System.IO.Path]::GetFullPath($Root)
    if (-not [System.IO.Directory]::Exists($fullRoot)) {
        throw "Snapshot root does not exist: $fullRoot"
    }

    $records = New-Object System.Collections.Generic.List[object]
    foreach ($entry in @(Get-ChildItem -LiteralPath $fullRoot -Force -Recurse | Sort-Object FullName)) {
        $relativePath = $entry.FullName.Substring($fullRoot.Length).TrimStart('\')
        if ($entry.PSIsContainer) {
            $records.Add([pscustomobject][ordered]@{ path = $relativePath; type = 'directory'; length = 0; sha256 = '' })
        }
        else {
            $records.Add([pscustomobject][ordered]@{ path = $relativePath; type = 'file'; length = [int64]$entry.Length; sha256 = Get-Phase0SFileSha256 -Path $entry.FullName })
        }
    }
    return $records.ToArray()
}

function Assert-Phase0STreeSnapshotEqual {
    param(
        [Parameter(Mandatory = $true)][object[]] $Expected,
        [Parameter(Mandatory = $true)][object[]] $Actual,
        [Parameter(Mandatory = $true)][string] $Context
    )

    $expectedJson = @($Expected | ConvertTo-Json -Depth 4 -Compress) -join ''
    $actualJson = @($Actual | ConvertTo-Json -Depth 4 -Compress) -join ''
    if ($expectedJson -cne $actualJson) {
        throw "${Context}: the target tree changed."
    }
}

function Assert-Phase0SCondition {
    param(
        [Parameter(Mandatory = $true)][bool] $Condition,
        [Parameter(Mandatory = $true)][string] $Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Invoke-Phase0SWindowsPowerShell {
    param(
        [Parameter(Mandatory = $true)][string] $ScriptPath,
        [string[]] $Arguments = @()
    )

    $powershellPath = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    if (-not [System.IO.File]::Exists($powershellPath)) {
        throw 'Windows PowerShell 5.1 is unavailable.'
    }
    if (-not [System.IO.File]::Exists($ScriptPath)) {
        throw "Script does not exist: $ScriptPath"
    }

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = @(& $powershellPath -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $ScriptPath @Arguments 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    return [pscustomobject]@{ exitCode = $exitCode; output = @($output) }
}
