[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$control=Join-Path $repository 'scripts/Aim-Diagnostics-Control.ps1'
$root=[IO.Path]::GetFullPath($OutputDirectory)
if([IO.Directory]::Exists($root)){throw 'Fresh isolated fixture output required.'}
[IO.Directory]::CreateDirectory($root)|Out-Null
[IO.File]::WriteAllBytes((Join-Path $root 'Terraria.exe'),[byte[]]@())
& $control -Action Arm -GameDirectory $root
$arm=Join-Path $root 'JueMingRData/logs/aim-diagnostics/arm.txt'
if([IO.File]::ReadAllText($arm) -notmatch '^[0-9a-f]{32}$'){throw 'One-use arm token malformed.'}
$refused=$false;try{& $control -Action Arm -GameDirectory $root}catch{$refused=$true}
if(-not $refused){throw 'Duplicate arm must not overwrite token.'}
$session=Join-Path $root 'JueMingRData/logs/aim-diagnostics/20261002-fixture'
[IO.Directory]::CreateDirectory((Join-Path $session 'host'))|Out-Null
& $control -Action Mark -GameDirectory $root -SessionDirectory $session -Note 'fixture only'
if(-not [IO.File]::ReadAllText((Join-Path $session 'mark.txt')).Contains('fixture only')){throw 'Marker annotation missing.'}
[IO.File]::WriteAllText((Join-Path $session 'host/manifest.tsv'),'fixture readiness only')
$dead=Join-Path $session 'worker-dead';[IO.Directory]::CreateDirectory($dead)|Out-Null
[IO.File]::WriteAllText((Join-Path $dead 'session.tsv'),"process`t2147483646`n")
$watch=[Diagnostics.Stopwatch]::StartNew();& $control -Action Stop -GameDirectory $root -SessionDirectory $session
if($watch.Elapsed.TotalSeconds -gt 5){throw 'Known dead generation must not block stop indefinitely.'}
# A live PID with an older generation's start time/manifest is deliberately
# reused in the fixture. Stop must wait for that live process, never kill it.
$helperDirectory=Join-Path $root 'payload';[IO.Directory]::CreateDirectory($helperDirectory)|Out-Null
$helperSource=Join-Path $helperDirectory 'Wait.cs';$helper=Join-Path $helperDirectory 'JueMingR.PredictionWorker.exe'
[IO.File]::WriteAllText($helperSource,'class Wait { static void Main() { System.Threading.Thread.Sleep(1800); } }')
& (Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe') /nologo /target:exe (('/out:'+ $helper)) $helperSource
if($LASTEXITCODE -ne 0){throw 'BCL helper compilation failed.'}
$live=Start-Process -FilePath $helper -PassThru -WindowStyle Hidden
$old=Join-Path $session 'worker-old-pid';[IO.Directory]::CreateDirectory($old)|Out-Null
[IO.File]::WriteAllText((Join-Path $old 'session.tsv'),"process`t"+$live.Id+"`nprocessStartUtc`t"+[DateTime]::UtcNow.AddHours(-1).ToString('o')+"`n")
[IO.File]::WriteAllText((Join-Path $old 'manifest.tsv'),'old generation fixture')
$watch.Restart();& $control -Action Stop -GameDirectory $root -SessionDirectory $session
if($watch.Elapsed.TotalMilliseconds -lt 1000 -or -not $live.HasExited){throw 'Reused live PID must not inherit an old completed identity.'}
$outside=Join-Path $root 'outside';[IO.Directory]::CreateDirectory($outside)|Out-Null
$refused=$false;try{& $control -Action Mark -GameDirectory $root -SessionDirectory $outside}catch{$refused=$true}
if(-not $refused){throw 'Outside directory must be refused.'}
$linked=Join-Path $root 'JueMingRData/logs/aim-diagnostics/20261003-link'
New-Item -ItemType Junction -Path $linked -Target $outside|Out-Null
$refused=$false;try{& $control -Action Mark -GameDirectory $root -SessionDirectory $linked}catch{$refused=$true}
if(-not $refused -or [IO.File]::Exists((Join-Path $outside 'mark.txt'))){throw 'Redirected session must be refused before writing.'}
foreach($path in @('scripts/Aim-Diagnostics-Control.ps1','scripts/build.ps1','scripts/build-phase0s-validation-package.ps1','scripts/phase0s/Phase0S.ScriptSupport.ps1','scripts/test-workload-regressions.ps1','scripts/test-world-object-text.ps1','scripts/workload/Workload.Evidence.ps1')){
 $tokens=$null;$errors=$null;[Management.Automation.Language.Parser]::ParseFile((Join-Path $repository $path),[ref]$tokens,[ref]$errors)|Out-Null
 if($errors.Count){throw ($path+': '+($errors -join ';'))}
}
Write-Output 'PASS isolated arm/duplicate/mark/stop/dead-worker/outside/junction controls and changed PowerShell parser checks.'
