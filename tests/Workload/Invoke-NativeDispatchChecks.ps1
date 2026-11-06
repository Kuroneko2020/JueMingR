[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2.0
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $root 'scripts/workload/Workload.Support.ps1')
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('JueMingR-dispatch-'+[Guid]::NewGuid().ToString('N'))
$checks=0
function Assert-Dispatch([bool]$Pass,[string]$Reason){if(!$Pass){throw ('Native dispatch: '+$Reason)};$script:checks++}
$innerKeys=@('JMR_PIRANHA_MOTOR_SCENE','JMR_PHOENIX_ORACLE_ONLY','JMR_ATTACHED_SHOT_ONLY','JMR_PHANTASM_BOUNDARY_TRACE','JMR_BETSY_FLAME_ORACLE_ONLY','JMR_ARC_SWING_LEAF','JMR_ARC_SWING_ITEM','JMR_THORN_LEAF','JMR_THORN_ITEM')
$saved=@{};foreach($key in @('JUEMINGR_KING_SLIME','JUEMINGR_PHASED_FLIGHT','JUEMINGR_NPC_LEGAL_COVERAGE','JUEMINGR_UNKNOWN_ONLY','JUEMINGR_WORKLOAD_SCOPE_CONTEXT','JMR_WEAPON_EFFECT_ORACLE_ONLY')+$innerKeys){$saved[$key]=[Environment]::GetEnvironmentVariable($key)}
try{
    [IO.Directory]::CreateDirectory($fixture)|Out-Null
    $driver=Join-Path $fixture 'Driver.cs'
    # Compile the actual boundary class. These small mechanism scenes are not
    # claimed as Terraria leaf execution; the real Probe has separate evidence.
    [IO.File]::WriteAllText($driver,@'
using System;
namespace NativeWorldTextProbe {
static class Driver {
static readonly string[] inner={"JMR_PIRANHA_MOTOR_SCENE","JMR_PHOENIX_ORACLE_ONLY","JMR_ATTACHED_SHOT_ONLY","JMR_PHANTASM_BOUNDARY_TRACE","JMR_BETSY_FLAME_ORACLE_ONLY","JMR_ARC_SWING_LEAF","JMR_ARC_SWING_ITEM","JMR_THORN_LEAF","JMR_THORN_ITEM"};
static void Wait(string[] args) {
System.IO.File.WriteAllText(System.IO.Path.Combine(args[2],"cancel.pid"),System.Diagnostics.Process.GetCurrentProcess().Id.ToString());
while(!System.IO.File.Exists(System.IO.Path.Combine(args[2],"cancel.stop")))System.Threading.Thread.Sleep(10);
}
static int Main(string[] args) {
try {
if(args.Length==1 && args[0]=="child") {
if(Environment.GetEnvironmentVariable("JUEMINGR_WORKLOAD_SCOPE_CONTEXT")!=null || Environment.GetEnvironmentVariable("JUEMINGR_WORKLOAD_ASSERTION_CONTEXT")!=null)throw new Exception("Parent formal context leaked into owned child.");
Console.WriteLine("owned-child-context-retired");return 0;
}
if(args[1]=="owned-child") {
var start=new System.Diagnostics.ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,"child") { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true };
NativeScopeDispatch.PrepareOwnedChild(start);
using(var child=System.Diagnostics.Process.Start(start)){Console.Write(child.StandardOutput.ReadToEnd());child.WaitForExit();if(child.ExitCode!=0)throw new Exception("Owned child failed.");}
return 0;
}
NativeScopeDispatch.Begin(args[0],args[3]);
string scope=args[3];
string family=scope=="CombatEffectsNailCpu"?"nail":scope=="CombatEffectsBetsyFlameCpu"?"betsy-flame":scope=="CombatEffectsArcSwingCpu"?"arc-swing":scope=="CombatEffectsHomingEffectCpu"?"homing-effect":scope=="CombatEffectsJoustingLanceCpu"?"jousting-lance":scope=="CombatEffectsWhipStarCpu"?"whip-star":scope=="CombatEffectsThornCpu"?"thorn":null;
string leaf=family!=null?"effects-"+family:scope=="NpcCaster"?"caster":scope=="NpcSnowman"?"snowman":scope=="RuntimeFailureCpu"?"runtime-failure-initial":scope=="JourneyCloseCpu"?"journey-close":scope=="CombatShotBirthOrderCpu"?"combat-shot-birth-order":scope=="RuntimeFailureBuildCpu"?"runtime-failure-build":scope=="RuntimeFailureBuildManualCpu"?"runtime-failure-build-manual":scope=="RuntimeFailureBuildWorldCpu"?"runtime-failure-build-world":scope=="RuntimeFailureBuildReceiptCpu"?"runtime-failure-build-receipt":scope=="DirectionalControl"?"directional-control":scope=="NpcSelectedContinuation"?"npc-selected-continuation":scope=="TerrainLine"?"terrain-line":"worker-fields";
if(Environment.GetEnvironmentVariable("JUEMINGR_KING_SLIME")!=null || Environment.GetEnvironmentVariable("JUEMINGR_PHASED_FLIGHT")!=null || Environment.GetEnvironmentVariable("JUEMINGR_NPC_LEGAL_COVERAGE")!=null || Environment.GetEnvironmentVariable("JMR_WEAPON_EFFECT_ORACLE_ONLY")!=null)throw new Exception("Inherited selector reached the boundary.");
if(args[1]=="wrong-leaf")leaf="king-slime";
if(family!=null) {
bool failed=false;
try { NativeScopeDispatch.RunExclusiveEffectFamily(family,()=>{
foreach(string key in inner)if(Environment.GetEnvironmentVariable(key)!=null)throw new Exception("Inherited inner shortcut reached a complete family: "+key);
if(Environment.GetEnvironmentVariable("JMR_WEAPON_EFFECT_ORACLE_ONLY")!=family)throw new Exception("Selected actual family missing.");
NativeScopeDispatch.EnterSelected(leaf);
if(args[1]=="inner-fail")throw new Exception("Controlled family assertion failure.");
if(args[1]=="cancel-wait")Wait(args);
}); } catch { if(args[1]!="inner-fail")throw;failed=true; }
foreach(string key in inner)if(Environment.GetEnvironmentVariable(key)!="inner-parent")throw new Exception("Inner selector not restored: "+key);
if(Environment.GetEnvironmentVariable("JMR_WEAPON_EFFECT_ORACLE_ONLY")!=null)throw new Exception("Family selector not restored after actual boundary clear.");
if(failed)return 9;
} else NativeScopeDispatch.Enter(leaf);
if(args[1]=="cancel-wait" && family==null) {
Wait(args);return 0;
}
if(args[1]=="fail")return 9;
if(args[1]!="no-completion")NativeScopeDispatch.Complete();
if(args[1]=="duplicate")NativeScopeDispatch.Complete();
return 0;
} catch(Exception e) { Console.Error.WriteLine(e.Message); return 7; }
}
}}
'@)
    $exe=Join-Path $fixture 'NativeWorldTextProbe.exe';$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe'
    & $compiler /nologo /target:exe /platform:x86 ('/out:'+$exe) $driver (Join-Path $root 'tests/NativeWorldTextProbe/NativeScopeDispatch.cs')
    if($LASTEXITCODE -ne 0){throw 'Actual native scope boundary compilation failed.'}
    foreach($key in $innerKeys){[Environment]::SetEnvironmentVariable($key,'inner-parent')}
    foreach($scope in @('NpcCaster','NpcSnowman','NpcWorkerFields','RuntimeFailureCpu','JourneyCloseCpu','CombatShotBirthOrderCpu','CombatEffectsNailCpu','CombatEffectsCpu','CombatEffectsBetsyFlameCpu','CombatEffectsArcSwingCpu','CombatEffectsHomingEffectCpu','CombatEffectsJoustingLanceCpu','CombatEffectsWhipStarCpu','CombatEffectsThornCpu','RuntimeFailureBuildCpu','RuntimeFailureBuildManualCpu','RuntimeFailureBuildWorldCpu','RuntimeFailureBuildReceiptCpu','DirectionalControl','NpcSelectedContinuation','TerrainLine')){
        $env:JUEMINGR_KING_SLIME='king-parent';$env:JUEMINGR_PHASED_FLIGHT='phase-parent';$env:JUEMINGR_NPC_LEGAL_COVERAGE='legal-parent';$env:JUEMINGR_WORKLOAD_SCOPE_CONTEXT='parent-context';$env:JMR_WEAPON_EFFECT_ORACLE_ONLY='phoenix'
        Invoke-WorkloadProcess ('native-'+$scope) $exe @($root,'ok',$fixture,$scope)
        Assert-Dispatch ($env:JUEMINGR_KING_SLIME -ceq 'king-parent' -and $env:JUEMINGR_PHASED_FLIGHT -ceq 'phase-parent' -and $env:JUEMINGR_NPC_LEGAL_COVERAGE -ceq 'legal-parent' -and $env:JUEMINGR_WORKLOAD_SCOPE_CONTEXT -ceq 'parent-context' -and $env:JMR_WEAPON_EFFECT_ORACLE_ONLY -ceq 'phoenix') ('actual child protocol and parent environment restored '+$scope)
    }
    foreach($kind in @('wrong-leaf','no-completion','duplicate','fail')){
        $rejected=$false;try{Invoke-WorkloadProcess 'native-NpcCaster' $exe @($root,$kind,$fixture,'NpcCaster')}catch{$rejected=$true}
        Assert-Dispatch ($rejected -and $env:JUEMINGR_KING_SLIME -ceq 'king-parent' -and $env:JUEMINGR_WORKLOAD_SCOPE_CONTEXT -ceq 'parent-context' -and $env:JMR_WEAPON_EFFECT_ORACLE_ONLY -ceq 'phoenix') ('wrong/missing/duplicate/failed completion rejected with parent restored '+$kind)
    }
    $rejected=$false;try{Invoke-WorkloadProcess 'native-NpcSnowman' $exe @($root,'ok',$fixture,'NpcCaster')}catch{$rejected=$true}
    Assert-Dispatch $rejected 'formal name and actual scope mismatch rejected'
    $rejected=$false;try{Invoke-WorkloadProcess 'native-RuntimeFailureCpu' $exe @($root,'ok',$fixture,'JourneyCloseCpu')}catch{$rejected=$true}
    Assert-Dispatch $rejected 'CPU scope prefix admission retains exact formal request equality'
    $rejected=$false;try{Invoke-WorkloadProcess 'native-CombatEffectsNailCpu' $exe @($root,'wrong-leaf',$fixture,'CombatEffectsNailCpu')}catch{$rejected=$true}
    Assert-Dispatch ($rejected -and $env:JMR_WEAPON_EFFECT_ORACLE_ONLY -ceq 'phoenix') 'actual family dispatch refuses a different leaf and restores the real inherited weapon selector'
    $rejected=$false;try{Invoke-WorkloadProcess 'native-CombatEffectsBetsyFlameCpu' $exe @($root,'inner-fail',$fixture,'CombatEffectsBetsyFlameCpu')}catch{$rejected=$true}
    Assert-Dispatch ($rejected -and $env:JMR_BETSY_FLAME_ORACLE_ONLY -ceq 'inner-parent' -and $env:JMR_ARC_SWING_LEAF -ceq 'inner-parent' -and $env:JMR_ARC_SWING_ITEM -ceq 'inner-parent' -and $env:JMR_THORN_LEAF -ceq 'inner-parent' -and $env:JMR_THORN_ITEM -ceq 'inner-parent' -and $env:JMR_WEAPON_EFFECT_ORACLE_ONLY -ceq 'phoenix') 'real complete-family environment boundary restores observed shortening keys after callback failure'
    $pipeline=[PowerShell]::Create();$pidPath=Join-Path $fixture 'cancel.pid';$stopPath=Join-Path $fixture 'cancel.stop'
    try{
        $support=Join-Path $root 'scripts/workload/Workload.Support.ps1'
        [void]$pipeline.AddScript('param($support,$exe,$arguments) $ErrorActionPreference="Stop"; . $support; Invoke-WorkloadProcess "native-CombatEffectsNailCpu" $exe $arguments').AddArgument($support).AddArgument($exe).AddArgument(@($root,'cancel-wait',$fixture,'CombatEffectsNailCpu'))
        $async=$pipeline.BeginInvoke();$deadline=[DateTime]::UtcNow.AddSeconds(5)
        while(-not [IO.File]::Exists($pidPath)){if([DateTime]::UtcNow -gt $deadline){throw 'Known family cancellation child did not start.'};Start-Sleep -Milliseconds 10}
        $pipeline.Stop();try{$null=$pipeline.EndInvoke($async)}catch{}
        Assert-Dispatch ($env:JMR_WEAPON_EFFECT_ORACLE_ONLY -ceq 'phoenix' -and $env:JUEMINGR_WORKLOAD_SCOPE_CONTEXT -ceq 'parent-context') 'actual pipeline cancellation restores weapon selector and parent scope without a completion protocol'
    }finally{
        if([IO.File]::Exists($pidPath)){
            $owned=Get-Process -Id ([int][IO.File]::ReadAllText($pidPath)) -ErrorAction SilentlyContinue
            if($null -ne $owned){if(![string]::Equals($owned.MainModule.FileName,$exe,[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected family cancellation child identity.'};[IO.File]::WriteAllText($stopPath,'finish only owned fixture');if(!$owned.WaitForExit(3000)){throw 'Owned family cancellation child did not finish.'}}
        }
        $pipeline.Dispose()
    }
    $env:JUEMINGR_UNKNOWN_ONLY='1';$rejected=$false;try{Get-WorkloadClearedEnvironment|Out-Null}catch{$rejected=$true}
    Assert-Dispatch ($rejected -and $env:JUEMINGR_UNKNOWN_ONLY -ceq '1') 'unknown suffix fails before child execution and preserves parent'
    Remove-Item Env:JUEMINGR_UNKNOWN_ONLY
    Invoke-WorkloadProcess 'native-ChildContextMechanism' $exe @($root,'owned-child',$fixture,'ChildContextMechanism')
    Assert-Dispatch ($env:JUEMINGR_WORKLOAD_SCOPE_CONTEXT -ceq 'parent-context') 'actual owned subprocess retires parent protocol context and parent state survives'
    Write-Output ('PASS native dispatch mechanism checks='+$checks+'; actual boundary class, runner protocol and process environment; no Terraria execution.')
}finally{
    foreach($key in $saved.Keys){[Environment]::SetEnvironmentVariable($key,$saved[$key])}
    $full=[IO.Path]::GetFullPath($fixture);$temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if(!$full.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -or (Split-Path -Leaf $full) -notlike 'JueMingR-dispatch-*'){throw 'Unsafe dispatch fixture cleanup.'}
    if([IO.Directory]::Exists($full)){Remove-Item -LiteralPath $full -Recurse -Force}
}
