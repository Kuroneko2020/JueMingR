[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2.0
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $root 'scripts/workload/Workload.Support.ps1')
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('JueMingR-dispatch-'+[Guid]::NewGuid().ToString('N'))
$checks=0
function Assert-Dispatch([bool]$Pass,[string]$Reason){if(!$Pass){throw ('Native dispatch: '+$Reason)};$script:checks++}
$saved=@{};foreach($key in @('JUEMINGR_KING_SLIME','JUEMINGR_PHASED_FLIGHT','JUEMINGR_NPC_LEGAL_COVERAGE','JUEMINGR_UNKNOWN_ONLY','JUEMINGR_WORKLOAD_SCOPE_CONTEXT')){$saved[$key]=[Environment]::GetEnvironmentVariable($key)}
try{
    [IO.Directory]::CreateDirectory($fixture)|Out-Null
    $driver=Join-Path $fixture 'Driver.cs'
    # Compile the actual boundary class. These small mechanism scenes are not
    # claimed as Terraria leaf execution; the real Probe has separate evidence.
    [IO.File]::WriteAllText($driver,@'
using System;
namespace NativeWorldTextProbe {
static class Driver {
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
string leaf=scope=="NpcCaster"?"caster":scope=="NpcSnowman"?"snowman":"worker-fields";
if(Environment.GetEnvironmentVariable("JUEMINGR_KING_SLIME")!=null || Environment.GetEnvironmentVariable("JUEMINGR_PHASED_FLIGHT")!=null || Environment.GetEnvironmentVariable("JUEMINGR_NPC_LEGAL_COVERAGE")!=null)throw new Exception("Inherited selector reached the boundary.");
if(args[1]=="wrong-leaf")leaf="king-slime";
NativeScopeDispatch.Enter(leaf);
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
    foreach($scope in @('NpcCaster','NpcSnowman','NpcWorkerFields')){
        $env:JUEMINGR_KING_SLIME='king-parent';$env:JUEMINGR_PHASED_FLIGHT='phase-parent';$env:JUEMINGR_NPC_LEGAL_COVERAGE='legal-parent';$env:JUEMINGR_WORKLOAD_SCOPE_CONTEXT='parent-context'
        Invoke-WorkloadProcess ('native-'+$scope) $exe @($root,'ok',$fixture,$scope)
        Assert-Dispatch ($env:JUEMINGR_KING_SLIME -ceq 'king-parent' -and $env:JUEMINGR_PHASED_FLIGHT -ceq 'phase-parent' -and $env:JUEMINGR_NPC_LEGAL_COVERAGE -ceq 'legal-parent' -and $env:JUEMINGR_WORKLOAD_SCOPE_CONTEXT -ceq 'parent-context') ('actual child protocol and parent environment restored '+$scope)
    }
    foreach($kind in @('wrong-leaf','no-completion','duplicate','fail')){
        $rejected=$false;try{Invoke-WorkloadProcess 'native-NpcCaster' $exe @($root,$kind,$fixture,'NpcCaster')}catch{$rejected=$true}
        Assert-Dispatch ($rejected -and $env:JUEMINGR_KING_SLIME -ceq 'king-parent' -and $env:JUEMINGR_WORKLOAD_SCOPE_CONTEXT -ceq 'parent-context') ('wrong/missing/duplicate/failed completion rejected with parent restored '+$kind)
    }
    $rejected=$false;try{Invoke-WorkloadProcess 'native-NpcSnowman' $exe @($root,'ok',$fixture,'NpcCaster')}catch{$rejected=$true}
    Assert-Dispatch $rejected 'formal name and actual scope mismatch rejected'
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
