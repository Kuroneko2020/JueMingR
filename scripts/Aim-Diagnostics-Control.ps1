[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidateSet('Arm','Mark','Stop','Analyze')][string]$Action,
    [Parameter(Mandatory=$true)][string]$GameDirectory,
    [string]$Note='路径和提示消失',
    [string]$SessionDirectory,
    [string]$OutputDirectory,
    [string]$PythonPath
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2.0
$gameRoot=[IO.Path]::GetFullPath($GameDirectory).TrimEnd('\')
if (-not [IO.File]::Exists((Join-Path $gameRoot 'Terraria.exe'))) { throw '准确游戏根目录必须含 Terraria.exe。' }
$root=Join-Path $gameRoot 'JueMingRData/logs/aim-diagnostics'
for ($item=[IO.DirectoryInfo]$root; $null -ne $item -and $item.FullName.Length -ge $gameRoot.Length; $item=$item.Parent) {
    if ($item.Exists -and ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw '诊断目录不能为重定向目录。' }
}
[IO.Directory]::CreateDirectory($root)|Out-Null
if ($Action -eq 'Arm') {
    foreach ($process in @(Get-Process -Name Terraria -ErrorAction SilentlyContinue)) {
        if ([IO.Path]::GetDirectoryName($process.Path) -ieq $gameRoot) { throw '先完全退出此 Terraria，再准备一次性采集。' }
    }
    $arm=Join-Path $root 'arm.txt'
    if ([IO.File]::Exists($arm)) { throw '已有未消费的启动令牌；不要重复准备。' }
    $stream=[IO.File]::Open($arm,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try { $bytes=[Text.Encoding]::UTF8.GetBytes([Guid]::NewGuid().ToString('N'));$stream.Write($bytes,0,$bytes.Length) } finally {$stream.Dispose()}
    Write-Output '已准备本次采集。现在启动诊断版 Terraria；令牌只消费一次。'
    return
}
if (-not $SessionDirectory) {
    $sessions=@(Get-ChildItem -LiteralPath $root -Directory|Sort-Object Name -Descending)
    if ($sessions.Count -eq 0) { throw '尚无诊断会话。' }
    $SessionDirectory=$sessions[0].FullName
}
$session=[IO.Path]::GetFullPath($SessionDirectory)
if (-not $session.StartsWith($root.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) -or -not [IO.Directory]::Exists($session)) { throw '会话必须位于此游戏的诊断目录。' }
for ($item=[IO.DirectoryInfo]$session; $null -ne $item -and $item.FullName.Length -ge $gameRoot.Length; $item=$item.Parent) {
    if ($item.Exists -and ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw '会话目录不能为重定向目录。' }
}
if ($Action -eq 'Mark' -or $Action -eq 'Stop') {
    $file=Join-Path $session $(if ($Action -eq 'Mark') {'mark.txt'} else {'stop.txt'})
    if ([IO.Directory]::Exists($file)) { throw '控制目标必须为普通文件。' }
    if ([IO.File]::Exists($file) -and ((Get-Item -LiteralPath $file -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw '控制文件不能为链接。' }
    [IO.File]::WriteAllText($file,[DateTime]::UtcNow.ToString('o')+' '+$Note,(New-Object Text.UTF8Encoding($false)))
    Write-Output $(if ($Action -eq 'Mark') {'已添加时间标记；不会重试、重启或改变预测。'} else {'已请求主进程和 worker 停止采集；游戏行为不变。'})
    Write-Output $session
    if ($Action -eq 'Stop') {
        $ending=[Diagnostics.Stopwatch]::StartNew()
        $hostManifest=Join-Path $session 'host/manifest.tsv'
        $pending=@()
        do {
            $pending=@(); $knownWorkerIds=@()
            foreach ($worker in @(Get-ChildItem -LiteralPath $session -Directory|Where-Object {$_.Name -like 'worker-*'})) {
                $workerSession=Join-Path $worker.FullName 'session.tsv'
                if (-not [IO.File]::Exists($workerSession)) { continue }
                $pidRow=@([IO.File]::ReadAllLines($workerSession)|Where-Object {$_ -match '^process\s+([0-9]+)$'})
                if ($pidRow.Count -eq 0) { continue }
                $workerProcessId=[int]($pidRow[0] -split "`t")[1]
                $workerProcess=Get-Process -Id $workerProcessId -ErrorAction SilentlyContinue
                if ($workerProcess -and $workerProcess.ProcessName -eq 'JueMingR.PredictionWorker') {
                    try {
                        $startRow=@([IO.File]::ReadAllLines($workerSession)|Where-Object {$_ -match '^processStartUtc\s+'})
                        $sameStart=$startRow.Count -eq 1 -and ([DateTimeOffset]::Parse(($startRow[0] -split "`t")[1]).UtcDateTime.Ticks -eq $workerProcess.StartTime.ToUniversalTime().Ticks)
                        if ($sameStart -and $workerProcess.Path -and $workerProcess.Path.StartsWith($gameRoot+'\',[StringComparison]::OrdinalIgnoreCase)) {
                            $knownWorkerIds+=$workerProcessId
                            if (-not [IO.File]::Exists((Join-Path $worker.FullName 'manifest.tsv'))) { $pending+=$worker.Name }
                        } else { $pending+=('进程身份或启动时间未确认 '+$workerProcessId) }
                    } catch { $pending+=('进程身份未确认 '+$workerProcessId) }
                }
            }
            # A freshly launched generation may not yet have created session.tsv.
            # Discover same-game processes conservatively; never stop or kill them.
            foreach ($running in @(Get-Process -Name 'JueMingR.PredictionWorker' -ErrorAction SilentlyContinue)) {
                try {
                    if (-not $running.Path) { $pending+=('路径未确认进程 '+$running.Id) }
                    elseif ($running.Path.StartsWith($gameRoot+'\',[StringComparison]::OrdinalIgnoreCase) -and $knownWorkerIds -notcontains $running.Id) { $pending+=('未建清单进程 '+$running.Id) }
                } catch { $pending+=('身份未确认进程 '+$running.Id) }
            }
            if ([IO.File]::Exists($hostManifest) -and $pending.Count -eq 0) { break }
            Start-Sleep -Milliseconds 250
        } while ($ending.Elapsed.TotalSeconds -lt 30)
        if ([IO.File]::Exists($hostManifest) -and $pending.Count -eq 0) { Write-Output 'Host 和当前仍运行 worker 的收尾清单已落盘，可以正常退出游戏。已异常退出且没有清单的 worker 仍为未确认，需保留。' }
        else { Write-Output ('仍在收尾或未确认完成，当前等待 worker: '+($pending -join ',')+'。请等 Host 和仍运行 worker 的 manifest.tsv 出现后再退出；若长时间未出现，保留所有资料并告知代理。') }

    }
    return
}
if (-not $OutputDirectory) { $OutputDirectory=Join-Path $session ('analysis-'+[DateTime]::UtcNow.ToString('yyyyMMddTHHmmss')) }
if (-not $PythonPath) {
    $bundled=Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
    if ([IO.File]::Exists($bundled)) {$PythonPath=$bundled} else {$PythonPath=(Get-Command python.exe -ErrorAction Stop).Source}
}
& $PythonPath (Join-Path $PSScriptRoot 'analyze-aim-diagnostics.py') $session $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw '分析未通过完整性检查；保留原资料并查看输出。' }
