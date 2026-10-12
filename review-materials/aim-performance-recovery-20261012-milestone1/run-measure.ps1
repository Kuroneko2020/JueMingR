param([int]$Weapon=1228,[ValidateSet('off','path','aim')][string]$Mode='path',[ValidateSet('still','move')][string]$Motion='still',[ValidateSet('idle','use')][string]$Attack='idle',[string]$Prefix='75a',[int]$Frames=360,[switch]$Count,[string]$Payload)
$ErrorActionPreference='Stop'
$taskRepo=(git rev-parse --show-toplevel).Trim();$taskRoot=Join-Path $taskRepo '.local/aim-performance-recovery-20261012'
if(-not $Payload){$Payload=Join-Path $taskRoot '75a-z/JueMingR-Continuous-Processing-75a1072cba59fbb84f587ec5615b8f32a9cedcb5/payload/JueMingR.Validation'}
$taskName=$Prefix+'-'+$Weapon+'-'+$Mode+'-'+$Motion+'-'+$Attack+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8)
$taskOutput=Join-Path $taskRoot $taskName;$taskLog=$taskOutput+'.log'
$env:JMR_MEASURE_FRAMES=[string]$Frames;$env:JMR_MEASURE_COUNT=if($Count){'1'}else{'0'}
& (Join-Path $taskRoot 'ContinuousCombatMeasure.exe') $taskRepo (Join-Path $taskRepo '.local/aim-root-fix-20261012/delivery/final-8aea410-fixed/NativeWorldTextProbe.exe') $Payload $taskOutput $Weapon $Mode $Motion $Attack *> $taskLog
$taskExit=$LASTEXITCODE
Get-Content -LiteralPath $taskLog -Tail $(if($Count){25}else{3})
Write-Output ('RAW '+$taskLog+' exit='+$taskExit)
if($taskExit -ne 0){exit $taskExit}
