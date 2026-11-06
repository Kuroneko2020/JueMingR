[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ProbePath)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('JMR-owned-child-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
try{
    [IO.Directory]::CreateDirectory($fixture)|Out-Null
    $driver=Join-Path $fixture 'ChildController.cs'
    # This controller invokes the actual Probe subprocess owners. It does not
    # copy their environment preparation or replace native child assertions.
    [IO.File]::WriteAllText($driver,@'
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
static class ChildController {
const BindingFlags Flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
static Assembly probe;
static object Call(string type,string method,params object[] args){return probe.GetType("NativeWorldTextProbe."+type,true).GetMethod(method,Flags).Invoke(null,args);}
static void Receive(Process child,string output) {
using(child){var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();if(!child.WaitForExit(60000)){child.Kill();throw new Exception("Owned child timed out.");}string log=stdout.Result+stderr.Result;File.WriteAllText(output,log);Console.Write(log);if(child.ExitCode!=0)throw new Exception("Actual owned child failed: "+child.ExitCode);if(log.Contains("JMR-LEAF-V1|") || log.Contains("JMR-ASSERTION-V1|"))throw new Exception("Internal child signed parent formal context.");}
}
static int Main(string[] args){try {
probe=Assembly.LoadFrom(Path.GetFullPath(args[0]));
probe.GetType("NativeWorldTextProbe.Program",true).GetProperty("Repository",Flags).GetSetMethod(true).Invoke(null,new object[]{args[1]});
string prior=Environment.GetEnvironmentVariable("JUEMINGR_WORKLOAD_SCOPE_CONTEXT");
string assertion=Environment.GetEnvironmentVariable("JUEMINGR_WORKLOAD_ASSERTION_CONTEXT");
try {
Environment.SetEnvironmentVariable("JUEMINGR_WORKLOAD_SCOPE_CONTEXT","native-NpcStrategy|owned-controller");
Environment.SetEnvironmentVariable("JUEMINGR_WORKLOAD_ASSERTION_CONTEXT","parent-assertion");
string strategy=Path.Combine(args[2],"strategy");Directory.CreateDirectory(strategy);
Receive((Process)Call("NativeStrategyScopes","StartChild",new[]{args[1],"--cpu",strategy},"facing"),Path.Combine(args[2],"strategy.log"));
Environment.SetEnvironmentVariable("JUEMINGR_WORKLOAD_SCOPE_CONTEXT","native-JourneyCpu|owned-controller");
string journey=Path.Combine(args[2],"journey-start");Directory.CreateDirectory(journey);
Receive((Process)Call("NativeJourneyChecks","StartChild","unused",journey,"NpcPresentationOriginal"),Path.Combine(args[2],"journey-start.log"));
Environment.SetEnvironmentVariable("JUEMINGR_WORKLOAD_SCOPE_CONTEXT","native-NpcPrivateSafety|owned-controller");
string original=Path.Combine(args[2],"private-original");Directory.CreateDirectory(original);
Call("NativeCombatPrivateImageChecks","OriginalProcess",original);
if(!File.Exists(Path.Combine(original,"presentation-original.txt")))throw new Exception("PrivateSafety original child did not produce its native comparison.");
if(Environment.GetEnvironmentVariable("JUEMINGR_WORKLOAD_SCOPE_CONTEXT")!="native-NpcPrivateSafety|owned-controller" || Environment.GetEnvironmentVariable("JUEMINGR_WORKLOAD_ASSERTION_CONTEXT")!="parent-assertion")throw new Exception("Owned child changed parent context.");
Console.WriteLine("PASS actual Strategy child/native assertions, actual Journey StartChild chain and PrivateSafety original comparison child; no parent protocol signing.");
}finally{Environment.SetEnvironmentVariable("JUEMINGR_WORKLOAD_SCOPE_CONTEXT",prior);Environment.SetEnvironmentVariable("JUEMINGR_WORKLOAD_ASSERTION_CONTEXT",assertion);}
return 0;
}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
'@)
    $exe=Join-Path $fixture 'ChildController.exe';$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe'
    & $compiler /nologo /target:exe /platform:x86 ('/out:'+$exe) $driver
    if($LASTEXITCODE -ne 0){throw 'Owned child controller compilation failed.'}
    & $exe ([IO.Path]::GetFullPath($ProbePath)) $root $fixture
    if($LASTEXITCODE -ne 0){throw ('Actual Probe child regression failed; original evidence retained at '+$fixture)}
    Write-Output ('Actual child evidence='+$fixture+'; caller owns retention; no installation or owner game used.')
}catch{
    Write-Output ('Retained exact owned fixture='+$fixture)
    throw
}
