<#
.SYNOPSIS
Narrow synthetic checks for the one-shot blueprint recorder, without Terraria.
.DESCRIPTION
Links only the three diagnostic recorder sources into a local net472/x86 Release
program. Its controllable clock proves lifecycle/capacity boundaries, not elapsed
real gameplay, FPS, native consumers, or ordinary/diagnostic package isolation.
All inputs, build output, failures and raw traces stay below this task check root.
#>
[CmdletBinding()]
param(
    [string] $OutputDirectory,
    [ValidateSet('All', 'TargetAssociation')][string] $Scope = 'All'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$actualRepositoryRoot = (& git -C $repositoryRoot rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0 -or [IO.Path]::GetFullPath($actualRepositoryRoot).TrimEnd('\') -ine $repositoryRoot.TrimEnd('\')) { throw 'Run this check from the actual JueMingR repository.' }
$checkRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot '.local/blueprint-materials-build-20261026/autobuild-diagnostic-20261028/recorder-checks'))
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $checkRoot ('r-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8)) }
if (-not [IO.Path]::IsPathRooted($OutputDirectory)) { throw 'OutputDirectory must be an explicit absolute path.' }
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\')
if (-not $outputRoot.StartsWith($checkRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Recorder checks may only write below the dedicated recorder-checks root.' }
$ancestor = $outputRoot
while ($ancestor) {
    if ((Test-Path -LiteralPath $ancestor) -and ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Recorder check output may not traverse reparse points.' }
    $ancestor = [IO.Path]::GetDirectoryName($ancestor)
}
if (Test-Path -LiteralPath $outputRoot) { throw 'Use a fresh output directory; old evidence is never overwritten or cleaned.' }
$sourceNames = @('BlueprintBuildTrace.cs', 'BlueprintBuildTraceData.cs', 'BlueprintBuildTraceExport.cs')
$sources = foreach ($name in $sourceNames) {
    $path = Join-Path $repositoryRoot ('src/JueMingR.TerrariaHost/Blueprint/' + $name)
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw ('Recorder source is missing: ' + $name) }
    [ordered]@{ name = $name; path = $path; sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
}
$utf8 = New-Object Text.UTF8Encoding($false)
[void] [IO.Directory]::CreateDirectory($outputRoot)
$projectRoot = Join-Path $outputRoot 'project'
$buildRoot = Join-Path $outputRoot 'build'
$runsRoot = Join-Path $outputRoot 'runs'
[void] [IO.Directory]::CreateDirectory($projectRoot)
$projectPath = Join-Path $projectRoot 'BlueprintBuildTraceChecks.csproj'
$harnessPath = Join-Path $projectRoot 'Program.cs'
$compileItems = foreach ($source in $sources) { '    <Compile Include="' + [Security.SecurityElement]::Escape([string] $source.path) + '" Link="' + $source.name + '" />' }
$project = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <PlatformTarget>x86</PlatformTarget>
    <OutputType>Exe</OutputType>
    <AssemblyName>BlueprintBuildTraceChecks</AssemblyName>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <DefineConstants>JMR_BLUEPRINT_DIAGNOSTIC;JMR_BLUEPRINT_DIAGNOSTIC_TEST</DefineConstants>
    <LangVersion>7.3</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <!-- The linked DTOs intentionally have fields this BCL-only producer never sets. -->
    <NoWarn>CS0649</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="System.Web.Extensions" />
    <Compile Include="Program.cs" />
__COMPILE_ITEMS__
  </ItemGroup>
</Project>
'@
[IO.File]::WriteAllText($projectPath, $project.Replace('__COMPILE_ITEMS__', ($compileItems -join [Environment]::NewLine)), $utf8)
$harness = @'
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using JueMingR.TerrariaHost.Blueprint;

internal static class Program
{
    private static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=2097152,RecursionLimit=40};
    private static readonly List<object> Results=new List<object>();
    private static string root,hostFile,hostSha;
    private sealed class Clock
    {
        internal long Now,Reads;
        internal long Read(){Interlocked.Increment(ref Reads);return Interlocked.Read(ref Now);}
    }
    private sealed class Fixture
    {
        internal string Root;
        internal BlueprintBuildTrace Trace;
        internal Clock Clock;
    }
    private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    private static string Hash(string path)
    {using(var input=File.OpenRead(path))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(input)).Replace("-","");}
    private static Dictionary<string,object> Object(object value){return (Dictionary<string,object>)value;}
    private static object[] Array(object value){return ((IEnumerable)value).Cast<object>().ToArray();}
    private static long Number(object value){return Convert.ToInt64(value,CultureInfo.InvariantCulture);}
    private static Dictionary<string,object> ReadJson(string path){return Object(Json.DeserializeObject(File.ReadAllText(path,new UTF8Encoding(false,true))));}
    private static Dictionary<string,object> Row(object value)
    {
        var row=Object(value);var names=((string)row["columns"]).Split(',');var values=Array(row["values"]);
        Require(names.Length==values.Length,"Row columns/values length mismatch.");
        var result=new Dictionary<string,object>();for(int i=0;i<names.Length;i++)result.Add(names[i],values[i]);return result;
    }
    private static BuildTraceOuter World(bool present=true)
    {return new BuildTraceOuter{WorldPresent=present,WorldId=7,NetMode=0,PlayerIndex=0,Frame=10,UpdateTick=20,GameUpdateCount=30,X=100,Y=200,Direction=1};}
    private static Fixture New(string name,string arm)
    {
        var fixture=new Fixture{Root=Path.Combine(root,name),Clock=new Clock()};Directory.CreateDirectory(fixture.Root);
        if(arm!=null)File.WriteAllText(Path.Combine(fixture.Root,"arm.txt"),arm,new UTF8Encoding(false,true));
        fixture.Trace=new BlueprintBuildTrace(fixture.Root,hostFile,"synthetic-recorder-check",fixture.Clock.Read,useTimer:false);
        BlueprintBuildTrace.BindForTest(fixture.Trace);return fixture;
    }
    private static void SettleClaim(Fixture fixture)
    {
        fixture.Trace.Outer(World());
        Require(SpinWait.SpinUntil(()=>{fixture.Trace.Outer(World());return fixture.Trace.Accepting || fixture.Trace.Finished;},5000),"Claim did not reach a bounded terminal/start state.");
    }
    private static string TracePath(Fixture fixture){return Path.Combine(fixture.Root,"run-"+fixture.Trace.RunId,"trace.json");}
    private static Dictionary<string,object> Exported(Fixture fixture)
    {
        string path=TracePath(fixture);
        Require(SpinWait.SpinUntil(()=>
        {
            if(!File.Exists(path))return false;
            try{using(File.Open(path,FileMode.Open,FileAccess.Read,FileShare.None))return true;}catch(IOException){return false;}
        },5000),"A complete trace was not exported within the check deadline.");
        var trace=ReadJson(path);Require((string)trace["schema"]=="jmr-blueprint-autobuild-diag-v1","Wrong raw trace schema.");
        ValidateShape(trace);
        var run=Object(trace["run"]);Require((string)run["hostSha256"]==hostSha && (string)run["runId"]==fixture.Trace.RunId,"Trace Host/run identity mismatch.");
        long rawBytes=Directory.GetFiles(fixture.Root,"*",SearchOption.AllDirectories).Sum(p=>new FileInfo(p).Length);
        Require(rawBytes<=1048576,"Total raw recorder output exceeded 1 MiB.");
        return trace;
    }
    private static void ValidateShape(Dictionary<string,object> trace)
    {
        var dictionary=Object(trace["dictionary"]);int stages=Array(dictionary["stages"]).Length,reasons=Array(dictionary["reasons"]).Length;
        var totals=Object(trace["totals"]);
        Require(Array(totals["stageCalls"]).Length==stages && Array(totals["reasonOccurrences"]).Length==reasons && Array(totals["reasonObservedOuterEpochs"]).Length==reasons,"Totals/dictionary dimensions differ.");
        foreach(var secondValue in Array(trace["seconds"]))
        {
            var second=Object(secondValue);
            Require(Array(second["stageCalls"]).Length==stages && Array(second["stage_last_seen_age_ms"]).Length==stages && Array(second["reasonOccurrences"]).Length==reasons && Array(second["reasonObservedOuterEpochs"]).Length==reasons,"Second/dictionary dimensions differ.");
            if(second["blueprint"]!=null)Row(second["blueprint"]);foreach(var source in Array(second["sources"]))Row(source);
        }
        foreach(string name in new[]{"events","targets","materials"})foreach(var row in Array(trace[name]))Row(row);
    }
    private static string SnapshotFiles(string directory)
    {return string.Join("|",Directory.GetFiles(directory,"*",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>p.Substring(directory.Length)+":"+new FileInfo(p).Length+":"+Hash(p)));}
    private static string SnapshotArrays(BlueprintBuildTrace trace)
    {
        var text=new StringBuilder();foreach(var field in typeof(BlueprintBuildTrace).GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Where(f=>f.FieldType.IsArray).OrderBy(f=>f.Name,StringComparer.Ordinal))
        {text.Append(field.Name).Append(':');AppendValue(text,field.GetValue(trace));}
        using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-","");
    }
    private static void AppendValue(StringBuilder text,object value)
    {
        if(value==null){text.Append("null;");return;}var type=value.GetType();
        if(type.IsArray){foreach(var item in (IEnumerable)value)AppendValue(text,item);return;}
        if(type.IsPrimitive || type.IsEnum || value is string){text.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append(';');return;}
        foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).OrderBy(f=>f.Name,StringComparer.Ordinal))AppendValue(text,field.GetValue(value));
    }
    private static void Case(string name,Action action){action();Results.Add(new{ name,status="PASS"});Console.WriteLine("PASS "+name);}

    private static void ArmStartAndRestart()
    {
        var fixture=New("arm-start-restart","hostSha256="+hostSha+"\n");string arm=Path.Combine(fixture.Root,"arm.txt");string armHash=Hash(arm);
        fixture.Clock.Now=3600000;for(int i=0;i<10;i++)fixture.Trace.Outer(World(false));
        Require(!fixture.Trace.Accepting && fixture.Trace.RunId==null && Hash(arm)==armHash,"Menu observations consumed or started the arm.");
        SettleClaim(fixture);Require(fixture.Trace.Accepting && BlueprintBuildTrace.Active,"A valid first world did not start the recorder.");
        Require(!File.Exists(arm),"The exact arm was not atomically consumed.");
        Require(Hash(Path.Combine(fixture.Root,"used-"+fixture.Trace.RunId+".txt"))==armHash,"Consumed arm bytes changed.");
        fixture.Trace.Blueprint(new BuildTraceBlueprint{State=0,SceneCount=0});
        Require(fixture.Trace.Accepting,"AutoBuild off/no scene prevented observation.");
        fixture.Clock.Now+=1250;fixture.Trace.Stop("normal-process-exit",true);
        var trace=Exported(fixture);var coverage=Object(trace["coverage"]);
        Require(Number(coverage["autoEnabledObservedUpdates"])==0 && Number(coverage["sceneObservedUpdates"])==0,"Off/no scene was misrepresented as active work.");
        Require(Number(coverage["elapsedMs"])==1250 && Array(trace["seconds"]).Length==2,"Menu time was included or early exit was padded to 120 seconds.");
        string originalRun=fixture.Trace.RunId,files=SnapshotFiles(fixture.Root);
        var restarted=new Fixture{Root=fixture.Root,Clock=new Clock()};restarted.Trace=new BlueprintBuildTrace(restarted.Root,hostFile,"synthetic-recorder-check",restarted.Clock.Read,useTimer:false);
        BlueprintBuildTrace.BindForTest(restarted.Trace);SettleClaim(restarted);
        Require(restarted.Trace.Finished && !restarted.Trace.Accepting,"Ordinary restart resumed an already used arm.");
        Require(Directory.GetDirectories(fixture.Root,"run-*").Length==1 && SnapshotFiles(fixture.Root)==files,"Restart changed earlier evidence or created a second trace.");
        Require(!File.Exists(Path.Combine(fixture.Root,"arm.txt")),"Restart recreated an arm.");
    }
    private static void MissingAndWrongArm()
    {
        var missing=New("missing-arm",null);SettleClaim(missing);
        Require(missing.Trace.Finished && !missing.Trace.Accepting,"Missing arm started recording.");
        Require((string)ReadJson(Path.Combine(missing.Root,"claim-status.json"))["status"]=="missing-arm","Missing arm status was not explicit.");
        Require(Directory.GetDirectories(missing.Root,"run-*").Length==0,"Missing arm created a trace.");
        var wrong=New("wrong-host","hostSha256="+new string('F',64)+"\n");string before=Hash(Path.Combine(wrong.Root,"arm.txt"));SettleClaim(wrong);
        Require(wrong.Trace.Finished && !wrong.Trace.Accepting,"Wrong Host arm started recording.");
        Require((string)ReadJson(Path.Combine(wrong.Root,"claim-status.json"))["status"]=="wrong-host" && Hash(Path.Combine(wrong.Root,"arm.txt"))==before,"Wrong Host arm was consumed or not explained.");
        var invalid=New("oversized-arm",new string('x',129));SettleClaim(invalid);
        Require(invalid.Trace.Finished && (string)ReadJson(Path.Combine(invalid.Root,"claim-status.json"))["status"]=="invalid-arm","Oversized arm was not rejected.");
    }
    private static void PendingWorldRetirement()
    {
        foreach(bool changedWorld in new[]{false,true})
        {
            var fixture=New(changedWorld?"pending-world-change":"pending-world-exit","hostSha256="+hostSha+"\n");string arm=Path.Combine(fixture.Root,"arm.txt"),before=Hash(arm);
            using(var entered=new ManualResetEvent(false))using(var release=new ManualResetEvent(false))using(var completed=new ManualResetEvent(false))
            {
                fixture.Trace.BeforeClaimForTest=()=>{entered.Set();Require(release.WaitOne(5000),"The check did not release its blocked claim.");};
                fixture.Trace.AfterClaimForTest=()=>completed.Set();
                fixture.Trace.Outer(World());Require(entered.WaitOne(5000),"The deterministic before-claim barrier was not reached.");
                try
                {
                    var next=World(!changedWorld?false:true);if(changedWorld)next.WorldId=8;
                    fixture.Trace.Outer(next);Require(fixture.Trace.Finished && !fixture.Trace.Accepting,"Leaving/changing world while claim was pending did not retire the window.");
                }
                finally{release.Set();}
                Require(completed.WaitOne(5000),"Claim finally did not reach its deterministic completion barrier.");
            }
            for(int i=0;i<100;i++)fixture.Trace.Outer(World());
            Require(fixture.Trace.Finished && !fixture.Trace.Accepting && Hash(arm)==before,"A late claim reopened the same world or consumed its arm.");
            Require(Directory.GetFiles(fixture.Root,"used-*.txt").Length==0 && Directory.GetDirectories(fixture.Root,"run-*").Length==0,"A retired pending window emitted a run.");
            Require((string)ReadJson(Path.Combine(fixture.Root,"claim-status.json"))["status"]=="session-ended-before-claim","Pending retirement status was not explicit.");
        }
    }
    private static Fixture CapacityAndDeadline()
    {
        var fixture=New("capacity-deadline","hostSha256="+hostSha+"\n");SettleClaim(fixture);Require(fixture.Trace.Accepting,"Capacity fixture did not start.");
        for(int i=1;i<=700;i++)BlueprintBuildTrace.Semantic(BuildTraceStage.Discover,BuildTraceReason.NoCandidate,i,long.MaxValue-i,long.MinValue+i,i*7,flight:i,token:i,generation:i);
        BlueprintBuildTrace.Semantic(BuildTraceStage.Discover,BuildTraceReason.NoCandidate,700,long.MaxValue-700,long.MinValue+700,4900,flight:700,token:700,generation:700);
        for(int i=0;i<16;i++){fixture.Trace.Target(new BuildTraceTarget{Session=1,Instance=1,Range=1,Ordinal=i,SupportStamp=long.MaxValue,RangeVersion=long.MaxValue,Catalog=long.MaxValue,Flight=long.MaxValue,Token=long.MaxValue,NativeGeneration=long.MaxValue});fixture.Trace.Supply(100+i,false,i);}
        fixture.Trace.InventoryBegin();for(int i=0;i<16;i++)fixture.Trace.InventorySlot(100+i,100,i%2==0);fixture.Trace.InventoryEnd(77);
        for(int second=0;second<120;second++)
        {
            fixture.Clock.Now=second*1000L;if(second==10 || second==11)continue;
            var outer=World();outer.Frame=1000+second;outer.GameUpdateCount=(uint)(2000+second);outer.UpdateTick=3000+second;if(second%2==0)outer.VelocityX=1;
            if(second>0)fixture.Trace.Outer(outer);
            fixture.Trace.Blueprint(new BuildTraceBlueprint{State=1,SceneCount=1,MaterialTick=(ulong)second,ProjectionMatchTick=(ulong)(second+1)});
            BlueprintBuildTrace.Hit(BuildTraceStage.BlueprintUpdate);BlueprintBuildTrace.Hit(BuildTraceStage.Advance,BuildTraceReason.NoCandidate);
            foreach(BuildTraceReason reason in Enum.GetValues(typeof(BuildTraceReason)))if(reason!=BuildTraceReason.Count)BlueprintBuildTrace.Count(reason,60000000000000000L);
            Require(BlueprintBuildTrace.Gate(true,BuildTraceReason.InputGate) && !BlueprintBuildTrace.Gate(false,BuildTraceReason.InterfaceGate),"Observed gate altered its business bool.");
            for(int slot=0;slot<4;slot++)fixture.Trace.Source(slot,new BuildTraceSource{Instance=long.MaxValue,Version=long.MaxValue,Wake=long.MaxValue,NearVersion=long.MaxValue,NearWake=long.MaxValue,NearPlayer=long.MaxValue,NearSupply=long.MaxValue,NearCursor=second,ForwardCursor=second,FrontierCursor=second,RetryAt=ulong.MaxValue,MaterialTick=(ulong)second},4);
        }
        Require(fixture.Trace.Accepting,"Event saturation ended the summary window.");fixture.Clock.Now=119999;fixture.Trace.CheckDeadline();Require(fixture.Trace.Accepting,"Recorder stopped before 120000 monotonic milliseconds.");
        fixture.Clock.Now=120000;fixture.Trace.CheckDeadline();Require(fixture.Trace.Finished && !fixture.Trace.Accepting,"Recorder did not stop at 120000 monotonic milliseconds.");
        var trace=Exported(fixture);var coverage=Object(trace["coverage"]);var totals=Object(trace["totals"]);var seconds=Array(trace["seconds"]);var events=Array(trace["events"]);
        Require(Number(coverage["elapsedMs"])==120000 && seconds.Length==120,"Deadline did not preserve exactly 120 bounded nominal buckets.");
        Require(Number(totals["eventDistinctOccurrences"])==700 && Number(totals["eventRetained"])==512 && Number(totals["eventOverwritten"])==188 && Number(totals["eventRepeatedSuppressed"])==1,"Bounded semantic event accounting differs.");
        Require(events.Length==512 && Number(Row(events[127])["seq"])==128 && Number(Row(events[128])["seq"])==317 && Number(Row(events[511])["seq"])==700,"Head/tail event ordering or coverage is wrong.");
        Require((bool)Object(seconds[119])["observed"] && Number(Object(seconds[119])["outerCalls"])>0,"Summary did not continue after event saturation.");
        foreach(int gap in new[]{10,11}){var bucket=Object(seconds[gap]);Require(!(bool)bucket["observed"] && (bool)bucket["coverage_gap"] && bucket["outer"]==null && bucket["blueprint"]==null,"A missing outer bucket reused an old snapshot.");}
        Require(Number(coverage["coverageGapBuckets"])==2,"Gap coverage differs.");
        Require(Array(trace["targets"]).Length<=8 && Array(trace["materials"]).Length==8 && Number(totals["targetDetailReplaced"])==8,"Target/material snapshot bound differs.");
        var reasons=Array(Object(trace["dictionary"])["reasons"]);int noCandidate=System.Array.IndexOf(reasons,"NoCandidate");
        Require(Number(Array(totals["reasonObservedOuterEpochs"])[noCandidate])<Number(Array(totals["reasonOccurrences"])[noCandidate]),"Reason callbacks and observed outer epochs were conflated.");
        Require((string)Object(trace["stop"])["reason"]=="time-limit","Deadline stop reason missing.");return fixture;
    }
    private static void StoppedWorkAndOnceExport(Fixture fixture)
    {
        BlueprintBuildTrace.BindForTest(fixture.Trace);long clockReads=fixture.Clock.Reads;string arrays=SnapshotArrays(fixture.Trace),files=SnapshotFiles(fixture.Root);
        for(int i=0;i<1000;i++)
        {
            fixture.Trace.Outer(World());fixture.Trace.CheckDeadline();fixture.Trace.Stop("must-not-export-again",true);
            BlueprintBuildTrace.Hit(BuildTraceStage.Advance);BlueprintBuildTrace.Count(BuildTraceReason.NoCandidate);BlueprintBuildTrace.Semantic(BuildTraceStage.Consumer,BuildTraceReason.Entered,i);
            Require(BlueprintBuildTrace.Gate(true,BuildTraceReason.InputGate) && !BlueprintBuildTrace.Gate(false,BuildTraceReason.InputGate),"Inactive gate changed its bool.");
            Require(!fixture.Trace.NeedSources && fixture.Trace.Target(new BuildTraceTarget{Ordinal=i})==-1,"Stopped observer accepted a new source/target.");
            fixture.Trace.Blueprint(new BuildTraceBlueprint{State=1,SceneCount=1});fixture.Trace.Source(0,new BuildTraceSource{NearCursor=i},1);fixture.Trace.EmptySources();
            fixture.Trace.Supply(i,false,i);fixture.Trace.InventoryBegin();fixture.Trace.InventorySlot(i,100,true);fixture.Trace.InventoryEnd(999);fixture.Trace.PrepareMaterial(i,true);
        }
        Require(fixture.Clock.Reads==clockReads,"Inactive producer read the observation clock.");
        Require(SnapshotArrays(fixture.Trace)==arrays,"Inactive producer mutated accepted recorder arrays.");
        Require(SnapshotFiles(fixture.Root)==files && Directory.GetFiles(fixture.Root,"export-failure-*.json").Length==0,"Stopped observer exported again or changed raw bytes.");
    }
    private static void RotatedTargetAssociation()
    {
        var fixture=New("rotated-target-association","hostSha256="+hostSha+"\n");SettleClaim(fixture);
        for(int ordinal=0;ordinal<16;ordinal++)
        {
            fixture.Clock.Now=ordinal+1;
            int slot=fixture.Trace.Target(new BuildTraceTarget{Session=1,Instance=2,Range=3,Ordinal=ordinal,Flight=ordinal+1,Token=ordinal+1,ChoiceAt=-1,CanPlaceResult=-1});
            Require(slot>=0 && slot<8,"Target recorder did not return a bounded detail slot.");
            BlueprintBuildTrace.Semantic(BuildTraceStage.Prepare,BuildTraceReason.Prepared,a:ordinal,flight:ordinal+1,token:ordinal+1,target:slot);
        }
        int refreshed=fixture.Trace.Target(new BuildTraceTarget{Session=1,Instance=2,Range=3,Ordinal=15,Flight=16,Token=16,ChoiceAt=-1,CanPlaceResult=-1});
        BlueprintBuildTrace.Semantic(BuildTraceStage.Prepare,BuildTraceReason.Prepared,a:15,b:1,flight:16,token:16,target:refreshed);
        fixture.Clock.Now=100;fixture.Trace.Stop("target-association-check",true);
        var trace=Exported(fixture);var events=Array(trace["events"]);var targets=Array(trace["targets"]);
        Require(events.Length==17 && targets.Length==8,"Early events or bounded target details were unexpectedly lost.");
        var ids=new HashSet<long>();long lastTargetId=-1;
        for(int index=0;index<events.Length;index++)
        {
            var value=Row(events[index]);long ordinal=Number(value["a"]),detailId=Number(value["targetDetailId"]);int slot=(int)Number(value["targetDetailSlot"]);
            Require(detailId>0,"An observed target event lost its stable identity.");
            if(index<16)Require(ids.Add(detailId),"Independent targets reused a stable detail identity.");
            if(ordinal<8)
            {
                Require(slot==-1,"A retained early event falsely points at a reused target slot.");
                Require(!targets.Any(t=>Number(Row(t)["detailId"])==detailId),"An evicted target identity was silently rebound to a later detail.");
            }
            else
            {
                Require(slot>=0 && slot<targets.Length,"A retained current detail unexpectedly lost its slot.");var target=Row(targets[slot]);
                Require(Number(target["detailId"])==detailId && Number(target["ordinal"])==ordinal,"An event linked to another target's detail after rotation.");
            }
            if(index==15)lastTargetId=detailId;
            if(index==16)Require(detailId==lastTargetId,"Refreshing a retained target changed its stable association.");
        }
        Require(Number(Object(trace["totals"])["targetDetailReplaced"])==8,"Target rotation count differs.");
    }
    private static void SessionEnded()
    {
        var fixture=New("session-ended","hostSha256="+hostSha+"\n");SettleClaim(fixture);fixture.Clock.Now=1750;fixture.Trace.Outer(World(false));
        Require(fixture.Trace.Finished,"World exit did not stop the observer.");var trace=Exported(fixture);
        Require((string)Object(trace["stop"])["reason"]=="session-ended-or-changed" && Number(Object(trace["stop"])["elapsedMs"])==1750,"World exit reason/elapsed time differs.");
    }
    private static int Main(string[] args)
    {
        root=Path.GetFullPath(args[0]);hostFile=typeof(Program).Assembly.Location;hostSha=Hash(hostFile);Directory.CreateDirectory(root);
        string scope=args.Length>1?args[1]:"All";
        try
        {
            if(scope=="All")
            {
                Case("menu preserves arm; first world starts with AutoBuild off/no scene; early stop and restart do not extend it",ArmStartAndRestart);
                Case("missing, wrong Host and oversized arms are explicit and never consumed",MissingAndWrongArm);
                Case("pending claim cannot reopen/consume after world exit/change, even on return to the initial world",PendingWorldRetirement);
            }
            Case("more than 8 targets: retained early events keep stable identity and mark evicted details unknown",RotatedTargetAssociation);
            Fixture capacity=null;Case("fake 120s window: event saturation, bounded summaries/details, gaps, ordered coverage and <=1MiB raw",()=>capacity=CapacityAndDeadline());
            if(scope=="All")
            {
                Case("inactive producers accept no data, read no clock, mutate no arrays and do not re-export",()=>StoppedWorkAndOnceExport(capacity));
                Case("world/session exit ends the one window early",SessionEnded);
            }
            var result=new{schema="jmr-blueprint-autobuild-recorder-checks-v1",status="PASS",scope,syntheticOnly=true,clock="controllable monotonic milliseconds; no real 120s wait",hostSha256=hostSha,cases=Results.ToArray(),boundaries="recorder/BCL only; native consumer, package compile isolation, game/FPS/multiplayer/owner acceptance NOT_EVALUATED"};
            File.WriteAllText(Path.Combine(root,"checks.json"),Json.Serialize(result),new UTF8Encoding(false,true));return 0;
        }
        catch(Exception error)
        {
            File.WriteAllText(Path.Combine(root,"failure.json"),Json.Serialize(new{status="FAIL",syntheticOnly=true,type=error.GetType().FullName,message=error.Message,passed=Results.ToArray()}),new UTF8Encoding(false,true));Console.Error.WriteLine(error);return 1;
        }
    }
}
'@
[IO.File]::WriteAllText($harnessPath, $harness, $utf8)
$inputs = [ordered]@{ schema = 'jmr-blueprint-autobuild-recorder-check-inputs-v1'; scope = $Scope; syntheticOnly = $true; configuration = 'Release'; targetFramework = 'net472'; platform = 'x86'; defines = @('JMR_BLUEPRINT_DIAGNOSTIC', 'JMR_BLUEPRINT_DIAGNOSTIC_TEST'); sources = $sources; harnessSha256 = (Get-FileHash -LiteralPath $harnessPath -Algorithm SHA256).Hash; projectSha256 = (Get-FileHash -LiteralPath $projectPath -Algorithm SHA256).Hash; scriptSha256 = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash }
[IO.File]::WriteAllText((Join-Path $outputRoot 'inputs.json'), ($inputs | ConvertTo-Json -Depth 8), $utf8)
$checkPhase = 'build'
try {
    $dotnet = (Get-Command dotnet -ErrorAction Stop).Source
    $sdkVersion = (& $dotnet --version).Trim()
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -cne '10.0.203') { throw 'Use the repository pinned .NET SDK 10.0.203.' }
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { $buildOutput = @(& $dotnet build $projectPath --configuration Release --nologo ('-p:JueMingRBuildRoot=' + $buildRoot) -p:Platform=x86 2>&1); $buildExit = $LASTEXITCODE }
    finally { $ErrorActionPreference = $previousPreference }
    [IO.File]::WriteAllText((Join-Path $outputRoot 'build.log'), (($buildOutput | ForEach-Object { [string] $_ }) -join [Environment]::NewLine), $utf8)
    if ($buildExit -ne 0) { throw 'Recorder-only net472 build failed; original build.log is retained.' }
    foreach ($source in $sources) { if ((Get-FileHash -LiteralPath $source.path -Algorithm SHA256).Hash -cne $source.sha256) { throw 'Recorder input changed during build; this result cannot certify mixed inputs.' } }
    $executables = @(Get-ChildItem -LiteralPath (Join-Path $buildRoot 'bin/BlueprintBuildTraceChecks') -Filter 'BlueprintBuildTraceChecks.exe' -Recurse -File)
    if ($executables.Count -ne 1) { throw 'The isolated recorder build did not yield one exact executable.' }
    # Framework startup still has a short-path boundary on this machine. Only
    # the exact declared check executable/config are copied inside this root.
    $probe = Join-Path $outputRoot 'probe.exe'
    [IO.File]::Copy($executables[0].FullName, $probe, $false)
    [IO.File]::Copy(($executables[0].FullName + '.config'), ($probe + '.config'), $false)
    $checkPhase = 'execution'
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { $runOutput = @(& $probe $runsRoot $Scope 2>&1); $runExit = $LASTEXITCODE }
    finally { $ErrorActionPreference = $previousPreference }
    [IO.File]::WriteAllText((Join-Path $outputRoot 'run.log'), (($runOutput | ForEach-Object { [string] $_ }) -join [Environment]::NewLine), $utf8)
    if ($runExit -ne 0) {
        if (Test-Path -LiteralPath (Join-Path $runsRoot 'failure.json')) { throw ('Recorder synthetic assertion failed (exit ' + $runExit + '); original failure.json and raw files are retained.') }
        throw ('Recorder check host failed before producing an assertion result (exit ' + $runExit + '); business assertions NOT_REACHED. Original build/run logs are retained.')
    }
    foreach ($source in $sources) { if ((Get-FileHash -LiteralPath $source.path -Algorithm SHA256).Hash -cne $source.sha256) { throw 'Recorder input changed during execution; retain the old evidence and check the actual revision.' } }
    $checksPath = Join-Path $runsRoot 'checks.json'
    $checks = [IO.File]::ReadAllText($checksPath, $utf8) | ConvertFrom-Json
    if ($checks.status -cne 'PASS' -or $checks.syntheticOnly -ne $true) { throw 'The isolated result is missing its synthetic scope.' }
    $result = [ordered]@{ schema = 'jmr-blueprint-autobuild-recorder-check-result-v1'; status = 'PASS'; scope = $Scope; syntheticOnly = $true; outputDirectory = $outputRoot; sdk = $sdkVersion; executableSha256 = (Get-FileHash -LiteralPath $probe -Algorithm SHA256).Hash; checksSha256 = (Get-FileHash -LiteralPath $checksPath -Algorithm SHA256).Hash; inputFileSha256 = (Get-FileHash -LiteralPath (Join-Path $outputRoot 'inputs.json') -Algorithm SHA256).Hash; cases = $checks.cases; real120Seconds = 'NOT_EXECUTED'; gameAndPublicBuild = 'NOT_TOUCHED' }
    [IO.File]::WriteAllText((Join-Path $outputRoot 'result.json'), ($result | ConvertTo-Json -Depth 8), $utf8)
    Write-Output ($result | ConvertTo-Json -Depth 8)
} catch {
    $failure = [ordered]@{ status = 'FAIL'; phase = $checkPhase; syntheticOnly = $true; reason = $_.Exception.Message; originalInputs = 'inputs.json'; outputDirectory = $outputRoot; gameAndPublicBuild = 'NOT_TOUCHED' }
    [IO.File]::WriteAllText((Join-Path $outputRoot 'result.json'), ($failure | ConvertTo-Json -Depth 5), $utf8)
    throw
}
