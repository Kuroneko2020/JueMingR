[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ProbePath,[string]$OriginalProbePath,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $root 'scripts/workload/Workload.Support.ps1')
$ProbePath=[IO.Path]::GetFullPath($ProbePath)
$output=[IO.Path]::GetFullPath($OutputDirectory)
if([IO.Directory]::Exists($output)){throw 'Preserve previous host guard evidence; output must be new.'}
[IO.Directory]::CreateDirectory($output)|Out-Null
# A private copy of the real fixed game keeps its real ABI and MVID. One
# appended byte only makes the source SHA wrong; original references are read-only.
$badRoot=Join-Path $output 'wrong-sha-root'
foreach($name in @('TerrariaRefs','Harmony')){
    $target=Join-Path $badRoot ('external/'+$name);[IO.Directory]::CreateDirectory($target)|Out-Null
    Get-ChildItem -LiteralPath (Join-Path $root ('external/'+$name)) -File | Copy-Item -Destination $target
}
$badGame=Join-Path $badRoot 'external/TerrariaRefs/Terraria.exe'
$stream=[IO.File]::Open($badGame,[IO.FileMode]::Append,[IO.FileAccess]::Write)
try{$stream.WriteByte(0)}finally{$stream.Dispose()}
$identity=Get-WorkloadIdentity $root
$inputs=[ordered]@{inputs=@('host-guard-real-game-sha:'+(Get-FileHash $badGame).Hash);fingerprint=(Get-WorkloadHash @('host-guard-real-game-sha:'+(Get-FileHash $badGame).Hash))}
$batch=New-WorkloadCheckpointBatch $output $identity $inputs @('native-MovementJumpCpu')
$results=New-Object 'System.Collections.Generic.List[object]'
function Invoke-GuardCase([string]$Label,[string]$Executable,[string]$Repository,[string]$WantedKind){
    $check=[pscustomobject]@{name='native-MovementJumpCpu';executable=$Executable;arguments=@($Repository,'--cpu',(Join-Path $output $Label),'MovementJumpCpu')}
    $attempt=Start-WorkloadCheckpoint $output $batch $check (Get-WorkloadHash (@($Executable)+@($check.arguments)))
    $actual=$null
    try{Invoke-WorkloadProcess $check.name $check.executable $check.arguments -Checkpoint $attempt;$actual='PASS'}catch{$actual=[string]$_.Exception.Data['WorkloadFailureKind'];if(-not $actual){throw}}
    if($actual -cne $WantedKind){throw ('Host guard case '+$Label+' expected '+$WantedKind+' but got '+$actual)}
    if($WantedKind -ceq 'HostException' -and $attempt.hostExceptionType -cne 'System.IO.InvalidDataException'){throw 'The real identity guard must retain its fatal InvalidDataException cause.'}
    $results.Add([ordered]@{label=$Label;result=$actual;exitCode=$attempt.exitCode;exceptionType=$attempt.hostExceptionType;executionId=$attempt.executionId;checkpointPath=$attempt.path;probeSha256=(Get-FileHash $Executable).Hash})
}
if($OriginalProbePath){Invoke-GuardCase 'before-wrong-sha' ([IO.Path]::GetFullPath($OriginalProbePath)) $badRoot 'BusinessAssertion'}
Invoke-GuardCase 'after-wrong-sha' $ProbePath $badRoot 'HostException'
Invoke-GuardCase 'after-valid-native-jump' $ProbePath $root 'PASS'
Write-WorkloadJson (Join-Path $output 'summary.json') ([ordered]@{status='PASS';scope='real NativeWorldTextProbe identity guard and normal native MovementJumpCpu; no whole-game startup';sourceIdentity=$identity;realReferenceSha256=(Get-FileHash (Join-Path $root 'external/TerrariaRefs/Terraria.exe')).Hash;privateWrongSha256=(Get-FileHash $badGame).Hash;results=@($results.ToArray());qualification='NOT_EVALUATED; isolated check receipts are not delivery evidence'})
Write-Output 'PASS: real host wrong-SHA fatal classification and valid native identity; original game/reference files unchanged.'
