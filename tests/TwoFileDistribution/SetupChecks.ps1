[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $SetupExecutable,
    [string] $OutputRoot
)
$ErrorActionPreference = 'Stop'
function ConvertTo-SetupProcessArgument {
    param([AllowEmptyString()][string] $Value)
    # ProcessStartInfo.Arguments works on Framework and Core. Quote each argv item using
    # Windows CRT rules: double backslashes only before quotes and the closing quote.
    $quoted = [Text.StringBuilder]::new()
    [void]$quoted.Append([char]34)
    $slashes = 0
    foreach ($character in $Value.ToCharArray()) {
        if ($character -eq [char]92) { $slashes++; continue }
        if ($character -eq [char]34) {
            [void]$quoted.Append([char]92, (2 * $slashes + 1))
        }
        else { [void]$quoted.Append([char]92, $slashes) }
        [void]$quoted.Append($character)
        $slashes = 0
    }
    [void]$quoted.Append([char]92, (2 * $slashes))
    [void]$quoted.Append([char]34)
    return $quoted.ToString()
}
function Invoke-SetupCli {
    param([string] $Executable, [string[]] $Arguments)
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $Executable
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.CreateNoWindow = $true
    $start.Arguments = (($Arguments | ForEach-Object { ConvertTo-SetupProcessArgument -Value $_ }) -join ' ')
    $process = [Diagnostics.Process]::Start($start)
    try { $stdout = $process.StandardOutput.ReadToEnd(); $stderr = $process.StandardError.ReadToEnd(); $process.WaitForExit(); return [pscustomobject]@{ exitCode = $process.ExitCode; stdout = $stdout; stderr = $stderr } }
    finally { $process.Dispose() }
}
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $repositoryRoot ('.local/two-file-distribution-20261102/setup/checks-' + [Guid]::NewGuid().ToString('N')) }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot '.local')) + [IO.Path]::DirectorySeparatorChar
if (-not $OutputRoot.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Setup checks must use an isolated repository .local output root.' }
if (Test-Path -LiteralPath $OutputRoot) { throw 'Setup checks require a new isolated output root.' }
[void][IO.Directory]::CreateDirectory($OutputRoot)
$standalone = Join-Path $OutputRoot 'standalone'
[void][IO.Directory]::CreateDirectory($standalone)
$testExe = Join-Path $standalone 'JueMingR-Setup.exe'
[IO.File]::Copy([IO.Path]::GetFullPath($SetupExecutable), $testExe)
$build = Join-Path $OutputRoot 'build'
& dotnet build (Join-Path $PSScriptRoot 'SetupServiceChecks.csproj') -c Release ('-p:JueMingRBuildRoot=' + $build) --nologo
if ($LASTEXITCODE -ne 0) { throw 'Setup service test compile failed.' }
$checks = Join-Path $build 'bin/SetupServiceChecks/Release/net472/SetupServiceChecks.exe'
& $checks --extract $testExe $OutputRoot
if ($LASTEXITCODE -ne 0) { throw 'Final Setup is missing frozen payload.' }
$infoCall = Invoke-SetupCli -Executable $testExe -Arguments @('--payload-info', '--json')
if ($infoCall.exitCode -ne 0) { throw ('Standalone payload verification failed: ' + $infoCall.stdout + $infoCall.stderr) }
$info = $infoCall.stdout | ConvertFrom-Json
if ($info.schema -ne 'JueMingR.Setup.Payload.1' -or $info.dll.sha256 -cne (Get-FileHash -LiteralPath (Join-Path $OutputRoot 'JueMingR.dll')).Hash -or $info.config.sha256 -cne (Get-FileHash -LiteralPath (Join-Path $OutputRoot 'Terraria.exe.config')).Hash) { throw 'CLI embedded identity mismatch.' }
& $checks (Join-Path $OutputRoot 'targets') (Join-Path $repositoryRoot 'external/TerrariaRefs/Terraria.exe') (Join-Path $OutputRoot 'JueMingR.dll') (Join-Path $OutputRoot 'Terraria.exe.config') | Tee-Object -FilePath (Join-Path $OutputRoot 'service-checks.log')
if ($LASTEXITCODE -ne 0) { throw 'Setup service checks failed.' }
$cliTarget = Join-Path $OutputRoot 'cli target 中文'
[void][IO.Directory]::CreateDirectory($cliTarget)
[IO.File]::Copy((Join-Path $repositoryRoot 'external/TerrariaRefs/Terraria.exe'), (Join-Path $cliTarget 'Terraria.exe'))
$call = Invoke-SetupCli -Executable $testExe -Arguments @('--install', '--game-dir', $cliTarget, '--json')
$result = $call.stdout
if ($call.exitCode -ne 0 -or ($result | ConvertFrom-Json).status -ne 'success') { throw ('Standalone CLI install failed: ' + $result) }
foreach ($name in @('JueMingR.dll', 'Terraria.exe.config')) { if ((Get-FileHash -LiteralPath (Join-Path $cliTarget $name)).Hash -cne (Get-FileHash -LiteralPath (Join-Path $OutputRoot $name)).Hash) { throw 'CLI installed bytes mismatch.' } }
$call = Invoke-SetupCli -Executable $testExe -Arguments @('--uninstall', '--game-dir', $cliTarget, '--json')
$result = $call.stdout
if ($call.exitCode -ne 0 -or ($result | ConvertFrom-Json).status -ne 'success') { throw ('Standalone CLI removal failed: ' + $result) }
$call = Invoke-SetupCli -Executable $testExe -Arguments @('--install', '--game-dir', (Join-Path $OutputRoot 'missing'), '--json')
if ($call.exitCode -eq 0 -or ($call.stdout | ConvertFrom-Json).status -ne 'failure') { throw 'CLI failed to return a real failure exit code.' }
$junction = Join-Path $OutputRoot 'linked-game'
[void](New-Item -ItemType Junction -Path $junction -Target $cliTarget)
$call = Invoke-SetupCli -Executable $testExe -Arguments @('--install', '--game-dir', $junction, '--json')
if ($call.exitCode -eq 0 -or ($call.stdout | ConvertFrom-Json).status -ne 'failure' -or (Test-Path -LiteralPath (Join-Path $cliTarget 'JueMingR.dll'))) { throw 'Reparse target was not rejected before mutation.' }
[ordered]@{ schema = 'JueMingR.Setup.Checks.1'; status = 'PASS'; setupSha256 = (Get-FileHash -LiteralPath $testExe).Hash; outputRoot = $OutputRoot; gui = 'not covered by filesystem checks'; gameStarted = $false } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputRoot 'result.json') -Encoding UTF8
Get-Content -LiteralPath (Join-Path $OutputRoot 'result.json')
