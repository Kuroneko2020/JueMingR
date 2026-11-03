[CmdletBinding()]
param([Parameter(Mandatory=$true)][string] $SetupExecutable,[Parameter(Mandatory=$true)][string] $OutputRoot)
$ErrorActionPreference='Stop'
function Invoke-ClearCli([string] $Directory){
    # Redirect explicitly: Windows PowerShell does not reliably wait/capture a WinExe
    # launched with &, even though that executable writes JSON to the attached console.
    $start=New-Object Diagnostics.ProcessStartInfo
    $start.FileName=[IO.Path]::GetFullPath($SetupExecutable)
    $start.Arguments='--clear-config --game-dir "'+$Directory+'" --json'
    $start.UseShellExecute=$false;$start.CreateNoWindow=$true
    $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    $process=[Diagnostics.Process]::Start($start)
    try{$stdout=$process.StandardOutput.ReadToEnd();$stderr=$process.StandardError.ReadToEnd();$process.WaitForExit();return [pscustomobject]@{exitCode=$process.ExitCode;stdout=$stdout;stderr=$stderr}}
    finally{$process.Dispose()}
}
$repositoryRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$OutputRoot=[IO.Path]::GetFullPath($OutputRoot)
if(-not $OutputRoot.StartsWith((Join-Path $repositoryRoot '.local')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $OutputRoot)){throw 'Use a new isolated .local output root.'}
[void][IO.Directory]::CreateDirectory($OutputRoot)
$build=Join-Path $OutputRoot 'build'
& dotnet.exe build (Join-Path $PSScriptRoot 'SetupServiceChecks.csproj') -c Release --nologo "-p:JueMingRBuildRoot=$build"
if($LASTEXITCODE -ne 0){throw 'Setup checks did not compile.'}
$checks=Join-Path $build 'bin/SetupServiceChecks/Release/net472/SetupServiceChecks.exe'
& $checks --extract ([IO.Path]::GetFullPath($SetupExecutable)) $OutputRoot
if($LASTEXITCODE -ne 0){throw 'Setup pair could not be extracted.'}
& $checks (Join-Path $OutputRoot 'targets') (Join-Path $repositoryRoot 'external/TerrariaRefs/Terraria.exe') (Join-Path $OutputRoot 'JueMingR.dll') (Join-Path $OutputRoot 'Terraria.exe.config') --ui-increment | Tee-Object -FilePath (Join-Path $OutputRoot 'narrow-checks.txt')
if($LASTEXITCODE -ne 0){throw 'Discovery/preference reset checks failed.'}
$cli=Join-Path $OutputRoot 'cli-target'
[void][IO.Directory]::CreateDirectory($cli)
[IO.File]::Copy((Join-Path $repositoryRoot 'external/TerrariaRefs/Terraria.exe'),(Join-Path $cli 'Terraria.exe'))
$config=Join-Path $cli 'JueMingRData/config'
[void][IO.Directory]::CreateDirectory($config)
[IO.File]::WriteAllText((Join-Path $config 'hotkeys.json'),'test-preference')
$result=Invoke-ClearCli $cli
if($result.exitCode -ne 0 -or ($result.stdout|ConvertFrom-Json).status -ne 'success' -or (Test-Path -LiteralPath (Join-Path $config 'hotkeys.json'))){throw ('Real CLI preference clear failed: '+$result.stdout+$result.stderr)}
$result.stdout | Set-Content -LiteralPath (Join-Path $OutputRoot 'cli-result.json') -Encoding UTF8
$linked=Join-Path $OutputRoot 'linked-config'
[void][IO.Directory]::CreateDirectory($linked)
[IO.File]::Copy((Join-Path $repositoryRoot 'external/TerrariaRefs/Terraria.exe'),(Join-Path $linked 'Terraria.exe'))
[void][IO.Directory]::CreateDirectory((Join-Path $linked 'JueMingRData'))
[IO.File]::WriteAllText((Join-Path $config 'ui.json'),'preserve-linked-target')
[void](New-Item -ItemType Junction -Path (Join-Path $linked 'JueMingRData/config') -Target $config)
$rejected=Invoke-ClearCli $linked
if($rejected.exitCode -eq 0 -or ($rejected.stdout|ConvertFrom-Json).status -ne 'failure' -or [IO.File]::ReadAllText((Join-Path $config 'ui.json')) -cne 'preserve-linked-target'){throw 'Linked preference path must be rejected before deletion.'}
$rejected.stdout | Set-Content -LiteralPath (Join-Path $OutputRoot 'junction-result.json') -Encoding UTF8
[ordered]@{status='PASS';narrowChecks=12;cliClear='PASS';ancestorJunction='REJECTED';gameStarted=$false;realGameTargetMutated=$false;setupSha256=(Get-FileHash -LiteralPath $SetupExecutable).Hash}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $OutputRoot 'result.json') -Encoding UTF8
