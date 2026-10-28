<#
.SYNOPSIS
Collect this one-shot blueprint diagnostic only after the owner has exited Terraria.
.DESCRIPTION
Stage B seals the selected run before invoking the original package Restore/Install.
It never starts Terraria, consumes/recreates an arm, or searches historical runs.
PrepareOnly and CollectOnly accept synthetic inputs exclusively below collector-checks.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $StageADeliveryPath,
    [Parameter(Mandatory = $true)][string] $OutputDirectory,
    [Parameter(Mandatory = $true)][string] $OwnerFeedback,
    [switch] $OwnerTestingComplete,
    [string] $RunId,
    [ValidateSet('StageB', 'PrepareOnly', 'CollectOnly')][string] $Mode = 'StageB'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$script:Utf8 = New-Object System.Text.UTF8Encoding($false, $true)
$script:RawLimit = 1048576L
$script:ZipLimit = 2097152L
$script:RepositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$script:CheckRoot = [IO.Path]::GetFullPath((Join-Path $script:RepositoryRoot '.local/blueprint-materials-build-20261026/autobuild-diagnostic-20261028/collector-checks'))
$script:Isolated = $Mode -ne 'StageB'

function Get-Value {
    param([AllowNull()][object] $Object, [string] $Name, [AllowNull()][object] $Default = $null)
    if ($null -eq $Object) { return $Default }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $Default }
    return $property.Value
}

function Get-FullPath {
    param([string] $Path)
    if ([string]::IsNullOrWhiteSpace($Path) -or -not [IO.Path]::IsPathRooted($Path)) { throw 'An explicit absolute path is required.' }
    return [IO.Path]::GetFullPath($Path).TrimEnd([char[]] '\/')
}

function Test-Within {
    param([string] $Path, [string] $Root)
    return $Path.StartsWith($Root.TrimEnd([char[]] '\/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
}

function Assert-NoReparse {
    param([string] $Path)
    # Check every existing ancestor, so a contained-looking path cannot follow a junction.
    $current = Get-FullPath $Path
    while ($current) {
        if (Test-Path -LiteralPath $current) {
            if (((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Reparse paths are not allowed for diagnostic collection.' }
        }
        $parent = [IO.Path]::GetDirectoryName($current)
        if ($parent -ceq $current) { break }
        $current = $parent
    }
}

function Get-Hash {
    param([string] $Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Read-SmallJson {
    param([string] $Path, [long] $MaximumLength = 65536)
    Assert-NoReparse $Path
    $item = Get-Item -LiteralPath $Path -Force
    if ($item.PSIsContainer -or $item.Length -gt $MaximumLength) { throw 'JSON input is not a bounded ordinary file.' }
    return ($script:Utf8.GetString([IO.File]::ReadAllBytes($Path)) | ConvertFrom-Json -ErrorAction Stop)
}

function Write-Text {
    param([string] $Path, [string] $Text)
    $bytes = $script:Utf8.GetBytes($Text)
    $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $stream.Write($bytes, 0, $bytes.Length) } finally { $stream.Dispose() }
}

function Write-Json {
    param([string] $Path, [object] $Value)
    Write-Text $Path (($Value | ConvertTo-Json -Depth 30) + [Environment]::NewLine)
}

function Copy-Sealed {
    param([string] $Source, [string] $Destination, [long] $PackageLimit = 65536)
    Assert-NoReparse $Source
    if (-not (Test-Path -LiteralPath $Source -PathType Leaf)) {
        if (Test-Path -LiteralPath $Source) { throw 'A known diagnostic file path is occupied by a directory; preserve it for manual inspection.' }
        return [pscustomobject]@{ status = 'missing'; bytes = $null; sha256 = $null; packaged = $false }
    }
    # Preserve first, then hash both copies. A copy failure or changed source blocks Restore.
    $before = Get-Item -LiteralPath $Source -Force
    $inputStream = [IO.File]::Open($Source, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        $outputStream = [IO.File]::Open($Destination, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose() }
    } finally { $inputStream.Dispose() }
    $after = Get-Item -LiteralPath $Source -Force
    $sourceHash = Get-Hash $Source
    $copyHash = Get-Hash $Destination
    if ($before.Length -ne $after.Length -or $before.LastWriteTimeUtc -ne $after.LastWriteTimeUtc -or $sourceHash -cne $copyHash) { throw 'A source changed while sealing; installation recovery has not been authorized by this copy.' }
    return [pscustomobject]@{ status = 'preserved'; bytes = [long] $after.Length; sha256 = $copyHash; packaged = $after.Length -le $PackageLimit }
}

function Assert-Hash {
    param([string] $Path, [string] $Expected, [long] $ExpectedBytes = -1)
    if ($Expected -cnotmatch '^[0-9A-F]{64}$') { throw 'The delivery receipt requires an uppercase SHA-256.' }
    Assert-NoReparse $Path
    $item = Get-Item -LiteralPath $Path -Force
    if ($item.PSIsContainer -or ($ExpectedBytes -ge 0 -and $item.Length -ne $ExpectedBytes) -or (Get-Hash $Path) -cne $Expected) { throw 'A file no longer matches the stage A delivery identity.' }
}

function Assert-Exited {
    $processes = @(Get-Process -Name 'Terraria', 'TerrariaServer', 'JueMingR.PredictionWorker' -ErrorAction SilentlyContinue)
    if ($processes.Count -ne 0) { throw ('Exit Terraria and its related processes first. Still running: ' + (($processes | ForEach-Object { $_.ProcessName + ':' + $_.Id }) -join ', ')) }
}

function Assert-PackageTools {
    param([object] $Identity)
    # Bind the invoked official tools to the retained, hashed original ZIP, not a lookalike root.
    $archive = [IO.Compression.ZipFile]::OpenRead([string] $Identity.zipPath)
    try {
        $directoryName = Split-Path -Leaf ([string] $Identity.packageRoot)
        foreach ($name in @('Install-Phase0S.ps1', 'Restore-Phase0S.ps1', 'Phase0S.ScriptSupport.ps1', 'phase-0-s-package.manifest.json')) {
            $entry = $archive.GetEntry($directoryName + '/' + $name)
            if ($null -eq $entry -or $entry.Length -gt 262144) { throw 'The retained package ZIP is missing a bounded official input.' }
            $stream = $entry.Open()
            $sha = [Security.Cryptography.SHA256]::Create()
            try { $archiveHash = ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '') } finally { $sha.Dispose(); $stream.Dispose() }
            Assert-Hash (Join-Path ([string] $Identity.packageRoot) $name) $archiveHash $entry.Length
        }
    } finally { $archive.Dispose() }
}

function Read-InstalledIdentity {
    param([object] $Expected, [string] $Target, [switch] $Payload)
    $receiptPath = Join-Path $Target 'JueMingR.Validation/phase-0-s-install-manifest.json'
    $runtimePath = Join-Path $Target 'JueMingR.Validation/phase-0-s-runtime.manifest'
    $hostPath = Join-Path $Target 'JueMingR.Validation/JueMingR.TerrariaHost.dll'
    Assert-NoReparse $receiptPath
    Assert-NoReparse $runtimePath
    Assert-NoReparse $hostPath
    if (-not $script:Isolated) {
        if (-not (Test-Phase0SFilesEqualBytes -FirstPath $Expected.manifestPath -SecondPath $receiptPath)) { throw 'Installed receipt differs from the exact package.' }
        $runtime = Read-Phase0SRuntimeManifest $runtimePath
        if ($runtime.packageId -cne $Expected.packageId -or $runtime.sourceCommit -cne $Expected.sourceCommit -or (Get-Hash $hostPath) -cne $runtime.hostAssemblySha256 -or (Get-Phase0SAssemblyMvid $hostPath) -cne $runtime.hostAssemblyMvid) { throw 'Installed runtime/Host identity differs from the package.' }
        if ($Payload) {
            foreach ($entry in $Expected.payload) {
                $path = Resolve-Phase0SContainedPath -Root $Target -RelativePath $entry.installRelativePath
                Assert-NoReparse $path
                if (-not (Test-Phase0SFileMatchesPayloadEntry $path $entry)) { throw 'Restored ordinary payload readback failed.' }
            }
        }
        return [pscustomobject]@{ packageId = $runtime.packageId; sourceCommit = $runtime.sourceCommit; hostSha256 = Get-Hash $hostPath; hostMvid = $runtime.hostAssemblyMvid; receiptSha256 = Get-Hash $receiptPath; runtimeSha256 = Get-Hash $runtimePath; payloadReadback = [bool] $Payload }
    }
    $receipt = Read-SmallJson $receiptPath
    if ($receipt.packageId -cne $Expected.packageId -or $receipt.sourceCommit -cne $Expected.sourceCommit) { throw 'Synthetic receipt identity differs.' }
    return [pscustomobject]@{ packageId = $receipt.packageId; sourceCommit = $receipt.sourceCommit; hostSha256 = Get-Hash $hostPath; hostMvid = $Expected.hostMvid; receiptSha256 = Get-Hash $receiptPath; runtimeSha256 = Get-Hash $runtimePath; payloadReadback = $false }
}

function Invoke-Official {
    param([string] $ScriptPath, [string] $Target, [string] $Operation, [string] $PackageId, [string] $ResultPath)
    Assert-Exited
    $powershell = Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe'
    $lines = @(& $powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $ScriptPath -TerrariaDirectory $Target 2>&1)
    $exitCode = $LASTEXITCODE
    $text = ($lines | ForEach-Object { [string] $_ }) -join [Environment]::NewLine
    if ($text.Length -gt 8192) { throw 'Official tool returned unexpected oversized output; inspect it locally.' }
    Write-Text $ResultPath ($text + [Environment]::NewLine)
    $result = $text | ConvertFrom-Json -ErrorAction Stop
    if ($exitCode -ne 0 -or $result.exitCode -ne 0 -or $result.operation -cne $Operation -or $result.packageId -cne $PackageId -or $result.status -cne 'success') { throw ('Official ' + $Operation + ' did not complete: ' + [string] $result.code) }
    return $result
}

function Format-Observed {
    param([AllowNull()][object] $Value)
    if ($null -eq $Value) { return 'unknown / 未观测' }
    return ($Value | ConvertTo-Json -Depth 12 -Compress)
}

function Convert-Row {
    param([AllowNull()][object] $Row)
    if ($null -eq $Row) { return $null }
    $names = ([string] (Get-Value $Row 'columns' '')).Split(',')
    $values = @(Get-Value $Row 'values' @())
    if ($names.Count -ne $values.Count) { return $null }
    $result = [ordered]@{}
    for ($index = 0; $index -lt $names.Count; $index++) { $result[$names[$index]] = $values[$index] }
    return [pscustomobject] $result
}

function Get-CounterRows {
    param([string[]] $Names, [object[]] $Values)
    for ($index = 0; $index -lt $Names.Count; $index++) {
        $value = if ($index -lt $Values.Count) { $Values[$index] } else { $null }
        [pscustomobject]@{ name = $Names[$index]; count = $value; index = $index }
    }
}

function New-Summary {
    param([AllowNull()][object] $Trace, [object] $RawState, [string] $Selection, [string] $RecoveryStatus)
    $lines = New-Object 'System.Collections.Generic.List[string]'
    $lines.Add('# 本次有限观察')
    $lines.Add('')
    $lines.Add('所有者反馈：' + $OwnerFeedback)
    $lines.Add('')
    $lines.Add('选取依据：' + $Selection + '；原件状态：' + $RawState.status + '；原件字节：' + (Format-Observed $RawState.bytes))
    $lines.Add('普通安装：' + $RecoveryStatus + '。自动放置仍待分析。')
    $lines.Add('')
    if ($null -eq $Trace) {
        $lines.Add('未取得可解析且身份匹配的本次记录，无法确认实际使用时段或最先失去进展的阶段。缺失、截断、容量违规及身份问题见 01_IDENTITY.json；未把隔离探针当作玩家记录。')
    } else {
        $coverage = Get-Value $Trace 'coverage'
        $totals = Get-Value $Trace 'totals'
        $dictionary = Get-Value $Trace 'dictionary'
        $seconds = @(Get-Value $Trace 'seconds' @())
        $stages = @(Get-CounterRows @(Get-Value $dictionary 'stages' @()) @(Get-Value $totals 'stageCalls' @()))
        $reasons = @(Get-CounterRows @(Get-Value $dictionary 'reasons' @()) @(Get-Value $totals 'reasonOccurrences' @()))
        $epochs = @(Get-Value $totals 'reasonObservedOuterEpochs' @())
        $hasEpochs = $epochs.Count -eq $reasons.Count -and $reasons.Count -gt 0
        foreach ($reason in $reasons) { $reason | Add-Member -NotePropertyName observedOuterEpochs -NotePropertyValue $(if ($hasEpochs) { $epochs[$reason.index] } else { $null }) }
        $lines.Add('调用、独立目标、更新等待与墙钟时长分别保留。缺 outer 的间隔不分配给业务原因；reason 是原接点发生量，不能一律视作独立目标或等待更新。')
        $lines.Add('')
        $lines.Add('1. 使用与窗口：`' + (Format-Observed $coverage) + '`。开关/scene/移动/静止为实际观察更新量；未观察间隔中的玩家状态未知。所有者描述与具体秒桶的对应仍为 unknown，不能补造时间。')
        $lines.Add('2. 阶段到达（括号内为最后到达所在秒桶；精确事件见 raw）：')
        foreach ($stage in $stages) {
            $last = @($seconds | Where-Object { $calls = @(Get-Value $_ 'stageCalls' @()); $stage.index -lt $calls.Count -and $calls[$stage.index] -gt 0 } | Select-Object -Last 1)
            $lastSecond = if ($last.Count -eq 0) { 'not_reached' } else { [string] $last[0].second }
            $lines.Add('   - ' + $stage.name + ': ' + (Format-Observed $stage.count) + ' (' + $lastSecond + ')')
        }
        $waitReasons = @($reasons | Where-Object { $_.count -gt 0 -and ($_.name -match 'Wait|Gate|Blocked|BudgetEmpty|Rejected|Missing|NotReady' -or $_.name -in @('NoPlayer', 'NoCandidate', 'NoPrepared', 'SourceFiltered', 'SourceExhausted', 'SourceNotCurrent', 'ForwardZeroBudget', 'ToolsOccupied', 'OtherTool', 'OwnershipDenied', 'Pending', 'Yield', 'Invalid', 'ManualInput', 'NoConsumption', 'UnknownOutcome', 'ReadUnavailable')) })
        $lines.Add('3. 主要等待/阻挡原因（最多六项）：observedOuterEpochs 是两次实际 outer 到达间同原因去重的观测区间数；occurrences 是原接点调用/步数。两者均不等于墙钟时长，多个原因可覆盖同一区间。')
        if ($hasEpochs) { $waitReasons = @($waitReasons | Sort-Object observedOuterEpochs -Descending | Select-Object -First 6) }
        else { $lines.Add('   - 原件缺 reasonObservedOuterEpochs，无法按已观察更新区间排序；该量保持 unknown，以下只列实际发生量。'); $waitReasons = @($waitReasons | Select-Object -First 6) }
        foreach ($reason in $waitReasons) { $lines.Add('   - ' + $reason.name + ': observedOuterEpochs=' + (Format-Observed $reason.observedOuterEpochs) + '; occurrences=' + (Format-Observed $reason.count)) }
        $lines.Add('4. 重置/扫描/恢复：')
        foreach ($name in @('NearReset', 'ForwardVisit', 'ForwardZeroBudget', 'RetryRequested', 'RetryDue', 'WakeBlocked', 'WakePerformed')) {
            $match = @($reasons | Where-Object { $_.name -ceq $name })
            $lines.Add('   - ' + $name + ': ' + $(if ($match.Count) { Format-Observed $match[0].count } else { 'unknown / 未观测' }))
        }
        $stationary = @($seconds | Where-Object { $_.observed -and $_.stationaryUpdates -gt 0 })
        $lines.Add('   - 出现静止观测的秒桶: ' + (($stationary | ForEach-Object { $_.second }) -join ', ') + '；是否“静止仍不推进”须对同一桶的扫描量/游标与输入门核对。')
        $lastSources = @($seconds | Where-Object { @(Get-Value $_ 'sources' @()).Count -gt 0 } | Select-Object -Last 1)
        if ($lastSources.Count) { foreach ($source in @($lastSources[0].sources)) { $lines.Add('   - 最后source桶 ' + $lastSources[0].second + ': `' + (Format-Observed (Convert-Row $source)) + '`') } }
        $lines.Add('5. 原版结果/耗材/方向与目标（同 flight/token/generation 比较；回调或耗材变化不证明匹配成功）：')
        foreach ($target in @(Get-Value $Trace 'targets' @())) { $lines.Add('   - `' + (Format-Observed (Convert-Row $target)) + '`') }
        foreach ($material in @(Get-Value $Trace 'materials' @())) { $lines.Add('   - 材料采样: `' + (Format-Observed (Convert-Row $material)) + '`') }
        $omissions = [ordered]@{}
        foreach ($name in @('eventDistinctOccurrences', 'eventRetained', 'eventOverwritten', 'eventRepeatedSuppressed', 'observerContentionDropped', 'targetDetailReplaced', 'optionalTargetsOmitted', 'optionalEventsOmitted', 'optionalSourcesOmitted')) { $omissions[$name] = Get-Value $totals $name }
        $lines.Add('6. 观察缺口：`' + (Format-Observed ([pscustomobject] $omissions)) + '`；停止：`' + (Format-Observed (Get-Value $Trace 'stop')) + '`。')
        $lines.Add('')
        $pipeline = @('Outer', 'BlueprintUpdate', 'Advance', 'Discover', 'Prepare', 'ToolsAdmit', 'ItemBegin', 'Consumer', 'Checked', 'Match')
        $reached = $null; $firstMissing = $null
        foreach ($name in $pipeline) {
            $counter = @($stages | Where-Object { $_.name -ceq $name })
            if ($counter.Count -eq 0 -or $null -eq $counter[0].count) { break }
            if ($counter[0].count -gt 0) { $reached = $name } elseif ($null -ne $reached) { $firstMissing = $name; break }
        }
        if ($null -ne $firstMissing) { $lines.Add('有限初判：窗口内最早未到达的已记录接缝为 ' + $firstMissing + '，前一到达接缝为 ' + $reached + '。这只定位观测边界，不能把无需求/等待或分支未走当作根因；具体拒绝、秒桶与事件 seq 由原件和 HOOKS 解释。') }
        else { $lines.Add('有限初判：累计到达量不足以唯一确定停摆起点；需对最后进展后的秒桶与事件 seq 查看。不能由累计非零推断后半窗口仍在进展。') }
        $lines.Add('同 ordinal 的 240 material tick 等待、deadline 到期及 wake 条件只按保留 source/事件原始时钟判断；快照不足时保持 unknown，不自动补采或修复。')
    }
    return ($lines -join [Environment]::NewLine) + [Environment]::NewLine
}

function New-VerifiedZip {
    param([string] $Directory, [string[]] $Names, [string] $ZipPath)
    $sums = foreach ($name in $Names) { (Get-Hash (Join-Path $Directory $name)) + '  ' + $name }
    Write-Text (Join-Path $Directory 'SHA256SUMS.txt') (($sums -join "`n") + "`n")
    $allNames = @($Names) + @('SHA256SUMS.txt')
    $stream = [IO.File]::Open($ZipPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    try {
        $archive = New-Object IO.Compression.ZipArchive($stream, [IO.Compression.ZipArchiveMode]::Create, $true)
        try {
            foreach ($name in $allNames) {
                $entry = $archive.CreateEntry($name, [IO.Compression.CompressionLevel]::Optimal)
                $source = [IO.File]::OpenRead((Join-Path $Directory $name))
                $destination = $entry.Open()
                try { $source.CopyTo($destination) } finally { $destination.Dispose(); $source.Dispose() }
            }
        } finally { $archive.Dispose() }
    } finally { $stream.Dispose() }
    if ((Get-Item -LiteralPath $ZipPath).Length -gt $script:ZipLimit) { throw 'Final ZIP exceeds 2 MiB; keep the preserved original and report the affected delivery.' }
    $archive = [IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        if ($archive.Entries.Count -ne $allNames.Count) { throw 'ZIP entry count differs.' }
        foreach ($name in $allNames) {
            $entry = $archive.GetEntry($name)
            if ($null -eq $entry -or $entry.Length -ne (Get-Item -LiteralPath (Join-Path $Directory $name)).Length) { throw 'ZIP entry length differs.' }
            $stream = $entry.Open()
            $sha = [Security.Cryptography.SHA256]::Create()
            try { $hash = ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '') } finally { $sha.Dispose(); $stream.Dispose() }
            if ($hash -cne (Get-Hash (Join-Path $Directory $name))) { throw 'ZIP decompression/hash verification failed.' }
        }
    } finally { $archive.Dispose() }
}

Assert-Exited
if (-not $OwnerTestingComplete -and $Mode -ne 'PrepareOnly') { throw 'Stage B requires the owner testing-complete instruction (-OwnerTestingComplete).' }
if ($OwnerFeedback.Length -gt 4096) { throw 'Keep owner feedback to the short observation requested by this task.' }
$deliveryPath = Get-FullPath $StageADeliveryPath
$outputRoot = Get-FullPath $OutputDirectory
if ($script:Isolated -and (-not (Test-Within $deliveryPath $script:CheckRoot) -or -not (Test-Within $outputRoot $script:CheckRoot))) { throw 'Check modes cannot read or write outside collector-checks.' }
Assert-NoReparse $deliveryPath
Assert-NoReparse $outputRoot
$delivery = Read-SmallJson $deliveryPath
if ($delivery.schema -cne 'jmr-blueprint-autobuild-stage-a-v1' -or $delivery.baselineCommit -notmatch '^[0-9a-f]{40}$') { throw 'Unsupported stage A delivery receipt.' }
if ($script:Isolated -and -not [bool] (Get-Value $delivery 'isolationOnly' $false)) { throw 'Check modes require a receipt marked isolationOnly=true.' }
if (-not $script:Isolated -and [bool] (Get-Value $delivery 'isolationOnly' $false)) { throw 'Synthetic evidence cannot execute real Stage B.' }
$target = Get-FullPath ([string] $delivery.target.terrariaDirectory)
$logRoot = Get-FullPath ([string] $delivery.capture.logRoot)
$expectedLogRoot = Join-Path $target 'JueMingRData/logs/blueprint-autobuild-diagnostic'
if ($logRoot -ine (Get-FullPath $expectedLogRoot) -or (Get-FullPath ([string] $delivery.capture.armPath)) -ine (Join-Path $logRoot 'arm.txt')) { throw 'Capture paths do not belong to the exact stage A target.' }
$paths = @($deliveryPath, $outputRoot, $target, $logRoot, [string] $delivery.capture.armPath, [string] $delivery.source.diffPath, [string] $delivery.source.hooksPath)
foreach ($identity in @($delivery.diagnostic, $delivery.ordinary)) {
    if ($identity.sourceCommit -notmatch '^[0-9a-f]{40}$' -or [string]::IsNullOrWhiteSpace($identity.packageId)) { throw 'Package source identity is missing.' }
    $paths += @([string] $identity.packageRoot, [string] $identity.zipPath)
}
foreach ($path in $paths) {
    $full = Get-FullPath $path
    if ($script:Isolated -and -not (Test-Within $full $script:CheckRoot)) { throw 'Check modes cannot read or write outside collector-checks.' }
    Assert-NoReparse $full
}
foreach ($root in @($target, [string] $delivery.diagnostic.packageRoot, [string] $delivery.ordinary.packageRoot)) { if ((Test-Within $outputRoot $root) -or $outputRoot -ieq (Get-FullPath $root)) { throw 'Evidence output must be separate from the game and package roots.' } }
if (Test-Path -LiteralPath $outputRoot) { throw 'Choose a new evidence output directory; existing evidence is never overwritten.' }
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach ($identity in @($delivery.diagnostic, $delivery.ordinary)) { Assert-Hash ([string] $identity.zipPath) ([string] $identity.zipSha256) ([long] $identity.zipBytes) }
Assert-Hash ([string] $delivery.source.diffPath) ([string] $delivery.source.diffSha256)
Assert-Hash ([string] $delivery.source.hooksPath) ([string] $delivery.source.hooksSha256)
if (-not $script:Isolated) {
    . (Join-Path $PSScriptRoot '../phase0s/Phase0S.ScriptSupport.ps1')
    foreach ($identity in @($delivery.diagnostic, $delivery.ordinary)) { Assert-PackageTools $identity }
    $diagnosticPackage = Read-Phase0SPackage ([string] $delivery.diagnostic.packageRoot)
    $ordinaryPackage = Read-Phase0SPackage ([string] $delivery.ordinary.packageRoot)
    if (-not (Test-Phase0STerrariaIdentity (Join-Path $target 'Terraria.exe'))) { throw 'The actual target Terraria identity changed.' }
    foreach ($pair in @(@($diagnosticPackage, $delivery.diagnostic), @($ordinaryPackage, $delivery.ordinary))) {
        if ($pair[0].packageId -cne $pair[1].packageId -or $pair[0].sourceCommit -cne $pair[1].sourceCommit) { throw 'Retained package differs from stage A.' }
    }
} else {
    $diagnosticPackage = $delivery.diagnostic
    $ordinaryPackage = $delivery.ordinary
}
$installed = Read-InstalledIdentity $diagnosticPackage $target
foreach ($name in @('hostSha256', 'hostMvid', 'receiptSha256', 'runtimeSha256')) {
    if ($installed.$name -cne $delivery.target.$name) { throw ('Installed identity no longer matches stage A: ' + $name) }
}
if ($installed.hostSha256 -cne $delivery.diagnostic.hostSha256 -or $installed.hostMvid -cne $delivery.diagnostic.hostMvid) { throw 'Stage A target and diagnostic Host identity disagree.' }
if ($Mode -eq 'PrepareOnly') { Write-Output '{"status":"prepared-isolated-only","recovery":"not-executed","realPlayerEvidence":false}'; return }

[void] [IO.Directory]::CreateDirectory($outputRoot)
$sealedRoot = Join-Path $outputRoot 'preserved'
$packageRoot = Join-Path $outputRoot 'evidence'
foreach ($root in @($sealedRoot, $packageRoot)) { [void] [IO.Directory]::CreateDirectory($root) }
foreach ($name in @('raw', 'source', 'delivery')) { [void] [IO.Directory]::CreateDirectory((Join-Path $packageRoot $name)) }
$files = New-Object 'System.Collections.Generic.List[string]'
$recovery = [ordered]@{ status = 'pending'; reason = 'Evidence must be sealed before recovery.'; restore = $null; install = $null; readback = $null }
try {
    $deliveryState = Copy-Sealed $deliveryPath (Join-Path $sealedRoot 'stage-a-delivery.json')
    $states = [ordered]@{}
    $states.arm = Copy-Sealed (Join-Path $logRoot 'arm.txt') (Join-Path $sealedRoot 'arm.txt') 4096
    $states.claim = Copy-Sealed (Join-Path $logRoot 'claim-status.json') (Join-Path $sealedRoot 'claim-status.json') 16384
    $claim = $null
    if ($states.claim.status -eq 'preserved' -and $states.claim.packaged) {
        try { $claim = Read-SmallJson (Join-Path $sealedRoot 'claim-status.json') 16384 } catch { $states.claim.status = 'invalid-json-preserved' }
    }
    $selection = 'No run was inferred; claim status is missing or unusable.'
    if ([string]::IsNullOrWhiteSpace($RunId)) {
        if ($null -ne $claim -and (Get-Value $claim 'status') -ceq 'claimed' -and (Get-Value $claim 'hostSha256') -ceq $installed.hostSha256) { $RunId = [string] (Get-Value $claim 'runId'); $selection = 'Exact run ID from this Host claim-status.json.' }
    } else { $selection = 'Explicit run ID; raw Host identity is checked separately.' }
    $trace = $null
    $rawState = [pscustomobject]@{ status = 'not-selected'; bytes = $null; sha256 = $null; packaged = $false }
    if (-not [string]::IsNullOrWhiteSpace($RunId)) {
        if ($RunId -cnotmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,95}$') { throw 'Unsafe run ID; no historical directory search is performed.' }
        $states.used = Copy-Sealed (Join-Path $logRoot ('used-' + $RunId + '.txt')) (Join-Path $sealedRoot ('used-' + $RunId + '.txt')) 4096
        $states.exportFailure = Copy-Sealed (Join-Path $logRoot ('export-failure-' + $RunId + '.json')) (Join-Path $sealedRoot ('export-failure-' + $RunId + '.json')) 4096
        $rawState = Copy-Sealed (Join-Path $logRoot ('run-' + $RunId + '/trace.json')) (Join-Path $sealedRoot 'trace.json') $script:RawLimit
        if ($rawState.status -eq 'preserved' -and $rawState.packaged) {
            try {
                $candidate = Read-SmallJson (Join-Path $sealedRoot 'trace.json') $script:RawLimit
                if ($candidate.schema -cne 'jmr-blueprint-autobuild-diag-v1' -or $candidate.run.runId -cne $RunId -or $candidate.run.hostSha256 -cne $installed.hostSha256 -or $candidate.run.hostMvid -cne $installed.hostMvid -or $candidate.run.packageId -cne $diagnosticPackage.packageId) { $rawState.status = 'identity-mismatch-preserved' } else { $trace = $candidate }
            } catch { $rawState.status = 'invalid-or-truncated-json-preserved' }
        } elseif ($rawState.status -eq 'preserved') { $rawState.status = 'over-limit-local-only' }
    }
    $diagnosticBytes = if ($null -eq $rawState.bytes) { 0L } else { [long] $rawState.bytes }
    foreach ($name in @('arm', 'claim', 'used', 'exportFailure')) { if ($states.Contains($name) -and $null -ne $states[$name].bytes) { $diagnosticBytes += [long] $states[$name].bytes } }
    $rawState | Add-Member -NotePropertyName allDiagnosticBytes -NotePropertyValue $diagnosticBytes
    if ($diagnosticBytes -gt $script:RawLimit) { $rawState.status = 'raw-total-over-limit-local-only'; $rawState.packaged = $false; $trace = $null }
    foreach ($name in @('phase-0-s-install-manifest.json', 'phase-0-s-runtime.manifest', 'phase-0-s-evidence.log')) {
        $states[$name] = Copy-Sealed (Join-Path $target ('JueMingR.Validation/' + $name)) (Join-Path $sealedRoot $name) 131072
    }
    $sourceDiff = Copy-Sealed ([string] $delivery.source.diffPath) (Join-Path $sealedRoot 'diagnostic.diff') 524288
    $hooks = Copy-Sealed ([string] $delivery.source.hooksPath) (Join-Path $sealedRoot 'HOOKS.md') 65536
    Write-Json (Join-Path $sealedRoot 'seal.json') ([ordered]@{ stageA = $deliveryState; runId = $RunId; selection = $selection; raw = $rawState; files = $states; sourceDiff = $sourceDiff; hooks = $hooks })

    # No user data backup is restored. Only the official exact-package tools may change installation.
    if ($Mode -eq 'StageB') {
        try {
            if ($states.arm.status -eq 'preserved') {
                if ($states.arm.sha256 -ceq $delivery.capture.armSha256) {
                    # A known unconsumed token is retired only after its exact bytes have been sealed.
                    $armPath = Join-Path $logRoot 'arm.txt'
                    Assert-Hash $armPath ([string] $delivery.capture.armSha256)
                    $retiredName = 'unused-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmss') + '-' + [Guid]::NewGuid().ToString('N') + '.txt'
                    [IO.File]::Move($armPath, (Join-Path $logRoot $retiredName))
                    $states.arm | Add-Member -NotePropertyName retirement -NotePropertyValue $retiredName
                } else { $states.arm | Add-Member -NotePropertyName retirement -NotePropertyValue 'unknown-arm-left-untouched' }
            }
            $recovery.restore = Invoke-Official (Join-Path ([string] $delivery.diagnostic.packageRoot) 'Restore-Phase0S.ps1') $target 'restore' $diagnosticPackage.packageId (Join-Path $sealedRoot 'restore-result.json')
            $recovery.install = Invoke-Official (Join-Path ([string] $delivery.ordinary.packageRoot) 'Install-Phase0S.ps1') $target 'install' $ordinaryPackage.packageId (Join-Path $sealedRoot 'install-result.json')
            $recovery.readback = Read-InstalledIdentity $ordinaryPackage $target -Payload
            $recovery.status = 'ordinary-restored'
            $recovery.reason = 'Official Restore/Install completed and ordinary payload/receipt/runtime were read back once.'
        } catch { $recovery.status = 'pending'; $recovery.reason = $_.Exception.Message }
    } else { $recovery.status = 'not-executed-isolated-check'; $recovery.reason = 'Synthetic collection only; no Restore or Install was invoked.' }
    Write-Json (Join-Path $sealedRoot 'recovery.json') $recovery

    if ($rawState.packaged -and $rawState.bytes -ne $null) {
        [IO.File]::Copy((Join-Path $sealedRoot 'trace.json'), (Join-Path $packageRoot 'raw/trace.json'), $false)
        $files.Add('raw/trace.json')
    } else {
        Write-Text (Join-Path $packageRoot 'raw/TRACE_NOT_PACKAGED.md') ('Original trace status: ' + $rawState.status + '. Preserved local originals, where present, were not truncated. See 01_IDENTITY.json.' + [Environment]::NewLine)
        $files.Add('raw/TRACE_NOT_PACKAGED.md')
    }
    foreach ($pair in @(@('diagnostic.diff', $sourceDiff), @('HOOKS.md', $hooks))) {
        $name = [string] $pair[0]
        $state = $pair[1]
        if ($state.packaged) {
            [IO.File]::Copy((Join-Path $sealedRoot $name), (Join-Path $packageRoot ('source/' + $name)), $false)
            $files.Add('source/' + $name)
        } else {
            # Oversized source detail must not crowd out trace/identity or be silently truncated.
            $omittedName = 'source/' + $name + '.omitted.md'
            Write-Text (Join-Path $packageRoot $omittedName) ('Source detail exceeds the task small-text allowance and is preserved locally unchanged. Bytes=' + $state.bytes + '; SHA256=' + $state.sha256 + '. Fixed source link: ' + (Format-Observed (Get-Value $delivery.diagnostic 'sourceUrl')) + '. This evidence delivery has an explicit source-detail gap.' + [Environment]::NewLine)
            $files.Add($omittedName)
        }
    }
    foreach ($name in @('arm.txt', 'claim-status.json', ('used-' + $RunId + '.txt'), ('export-failure-' + $RunId + '.json'), 'phase-0-s-install-manifest.json', 'phase-0-s-runtime.manifest', 'recovery.json')) {
        $path = Join-Path $sealedRoot $name
        if ((Test-Path -LiteralPath $path -PathType Leaf) -and (Get-Item -LiteralPath $path).Length -le 131072) { [IO.File]::Copy($path, (Join-Path $packageRoot ('delivery/' + $name)), $false); $files.Add('delivery/' + $name) }
    }
    # Full formal evidence is sealed locally; the ZIP carries only its hash and bounded identity.
    $identity = [ordered]@{
        schema = 'jmr-blueprint-autobuild-evidence-v1'; realPlayerEvidence = (-not $script:Isolated -and $null -ne $rawState.bytes); usableRuntimeObservation = $null -ne $trace
        baselineCommit = $delivery.baselineCommit
        diagnostic = [ordered]@{ sourceCommit = $delivery.diagnostic.sourceCommit; sourceUrl = Get-Value $delivery.diagnostic 'sourceUrl'; packageId = $delivery.diagnostic.packageId; zipSha256 = $delivery.diagnostic.zipSha256; zipBytes = $delivery.diagnostic.zipBytes; hostSha256 = $delivery.diagnostic.hostSha256; hostMvid = $delivery.diagnostic.hostMvid; build = $delivery.diagnostic.build }
        ordinary = [ordered]@{ sourceCommit = $delivery.ordinary.sourceCommit; packageId = $delivery.ordinary.packageId; zipSha256 = $delivery.ordinary.zipSha256; zipBytes = $delivery.ordinary.zipBytes }
        stageAReceiptSha256 = $deliveryState.sha256; installedBeforeRestore = $installed
        sourceFiles = [ordered]@{ diff = $sourceDiff; hooks = $hooks }
        runId = $RunId; selection = $selection; armAndClaimFiles = $states; raw = $rawState
        runtimeObserved = Get-Value $trace 'run'; coverage = Get-Value $trace 'coverage'; stop = Get-Value $trace 'stop'
        fileVerificationAndRuntimeObservationAreSeparate = $true; recovery = $recovery
        limits = [ordered]@{ rawBytes = $script:RawLimit; zipBytes = $script:ZipLimit }
    }
    Write-Json (Join-Path $packageRoot '01_IDENTITY.json') $identity
    $files.Add('01_IDENTITY.json')
    Write-Text (Join-Path $packageRoot '02_SUMMARY.md') (New-Summary $trace $rawState $selection $recovery.status)
    $files.Add('02_SUMMARY.md')
    $exitText = if ($recovery.status -eq 'ordinary-restored') { '现场证据已封存，普通包实际恢复完成。' } elseif ($script:Isolated) { '隔离检查：未执行安装恢复。' } else { '采集结束，实际安装恢复待完成。' }
    $sourceText = if ($sourceDiff.packaged -and $hooks.packaged) { '诊断增量与接点映射已收录。' } else { '存在源码详单省略；原件在本地保留，source/*.omitted.md 说明哈希和固定链接，未宣称完整。' }
    $readme = @('本次自动放置一次性诊断证据', '', ('所有者反馈：' + $OwnerFeedback), ('run ID：' + $RunId + '；原件：' + $rawState.status), ($exitText + ' ' + $recovery.reason), ('现场数据：' + $(if ($script:Isolated) { '合成隔离检查，不是玩家实机记录。' } else { '本次精确目录中实际取得的记录；是否可用见身份和覆盖字段。' })), $sourceText, '', '01_IDENTITY.json 区分阶段 A/文件核验/运行观测与恢复；02_SUMMARY.md 保留量化值和未知；raw 保留原始字节；source 为诊断增量及接点映射；delivery 为必要小凭据。', '自动放置仍待分析；本包不证明修复、性能、多人或所有者验收。') -join [Environment]::NewLine
    Write-Text (Join-Path $packageRoot '00_README.md') ($readme + [Environment]::NewLine)
    $files.Add('00_README.md')
    $zipRunId = if ([string]::IsNullOrWhiteSpace($RunId)) { 'no-run-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmss') } else { $RunId }
    $zipPath = Join-Path $outputRoot ('JMR_BLUEPRINT_AUTOBUILD_EVIDENCE_' + $zipRunId + '.zip')
    New-VerifiedZip $packageRoot $files.ToArray() $zipPath
    Write-Output (([ordered]@{ status = 'collected'; zipPath = $zipPath; zipBytes = (Get-Item -LiteralPath $zipPath).Length; zipSha256 = Get-Hash $zipPath; rawStatus = $rawState.status; rawSha256 = $rawState.sha256; recovery = $recovery.status; realPlayerEvidence = (-not $script:Isolated -and $null -ne $rawState.bytes) } | ConvertTo-Json -Compress))
} catch {
    # Preserve all partial evidence; never clean user data or retry recovery after an uncertain failure.
    $failurePath = Join-Path $outputRoot 'COLLECTION_FAILED.json'
    if (-not (Test-Path -LiteralPath $failurePath)) { Write-Json $failurePath ([ordered]@{ status = 'failed'; reason = $_.Exception.Message; recovery = $recovery }) }
    throw
}
