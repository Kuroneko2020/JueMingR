using System.Collections.Generic;
using JueMingR.Platform.Combat;
using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Runtime.ExceptionServices;
internal static class RunNaturalCpuCount
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    static Assembly probe;static string output,content;static string mode;
    static bool printed;
    static string Safe(Func<string> read){try{string v=read();return v==null?"<null>":v.Substring(0,Math.Min(2400,v.Length));}catch{return "<getter failed>";}}
    static void Dump(Exception final)
    {
        if(printed)return;printed=true;
        try{Console.Error.WriteLine("FINAL type="+Safe(()=>final.GetType().FullName)+" message="+Safe(()=>final.Message)+" stack="+Safe(()=>final.StackTrace));}catch{}
    }
    static object Invoke(string type,string method,params object[] args){return probe.GetType("NativeWorldTextProbe."+type,true).GetMethod(method,F).Invoke(null,args);}
    static string Hash(string path){using(var f=File.OpenRead(path))using(var s=SHA256.Create())return BitConverter.ToString(s.ComputeHash(f)).Replace("-","");}
    static int Main(string[] args)
    {
        try
        {

            AppDomain.CurrentDomain.UnhandledException+=(sender,e)=>{var error=e.ExceptionObject as Exception;if(error!=null)Dump(error);};
            string repo=Path.GetFullPath(args[0]),payload=Path.GetFullPath(args[2]);output=Path.GetFullPath(args[3]);content=Path.GetFullPath(args[4]);mode=args.Length>5?args[5]:"all";Directory.CreateDirectory(output);
            AppDomain.CurrentDomain.AssemblyResolve+=(sender,e)=>{string name=new AssemblyName(e.Name).Name;string path=Path.Combine(payload,name+".dll");return (name.StartsWith("JueMingR.")||name=="0Harmony")&&File.Exists(path)?Assembly.LoadFrom(path):null;};
            foreach(string name in new[]{"0Harmony","JueMingR.Platform","JueMingR.Features","JueMingR.Infrastructure","JueMingR.TerrariaHost"})
            {
                var a=Assembly.LoadFrom(Path.Combine(payload,name+".dll"));var d=(DebuggableAttribute)Attribute.GetCustomAttribute(a,typeof(DebuggableAttribute));if(name.StartsWith("JueMingR.")&&d!=null&&d.IsJITOptimizerDisabled)throw new Exception("Unoptimized candidate");
                Console.WriteLine("LOAD "+name+" MVID="+a.ManifestModule.ModuleVersionId+" SHA256="+Hash(a.Location)+" location="+a.Location);
            }
            probe=Assembly.LoadFrom(Path.GetFullPath(args[1]));var program=probe.GetType("NativeWorldTextProbe.Program",true);program.GetProperty("Repository",F).SetValue(null,repo,null);program.GetProperty("ProductionConfiguration",F).SetValue(null,"Release",null);program.GetField("references",F).SetValue(null,Path.Combine(repo,"external","TerrariaRefs"));
            AppDomain.CurrentDomain.AssemblyResolve+=(ResolveEventHandler)Delegate.CreateDelegate(typeof(ResolveEventHandler),program.GetMethod("Resolve",F));
            var game=Assembly.LoadFrom(Path.Combine(repo,"external","TerrariaRefs","Terraria.exe"));program.GetField("game",F).SetValue(null,game);string user=Path.Combine(repo,".local","ia-"+Guid.NewGuid().ToString("N").Substring(0,8));game.GetType("Terraria.Program",true).GetField("SavePath",F).SetValue(null,user);Directory.CreateDirectory(user);Console.WriteLine("ISOLATED SavePath="+user+" max-path-safe private root");
            Console.WriteLine("GAME MVID="+game.ManifestModule.ModuleVersionId+" SHA256="+Hash(game.Location));Console.WriteLine("PROBE SHA256="+Hash(probe.Location)+" scope=NaturalCpu:no-draw mode="+mode+"; release runtime, debug-only measurement counters unused");
            Environment.SetEnvironmentVariable("JUEMINGR_AIM_LIGHT_OFF","1");
            Invoke("NativeQuickItemChecks","Run",(Action<object>)Exercise,false,false,false,true,true,Path.Combine(payload,"JueMingR.TerrariaHost.dll"),null,null,false);return 0;
        }
        catch(Exception e){Dump(e);return 1;}
    }
    [MethodImpl(MethodImplOptions.NoInlining)]static void Exercise(object context)
    {
        Invoke("NativeCombatAimChecks","Initialize");
        using(var graphics=(IDisposable)Activator.CreateInstance(probe.GetType("NativeWorldTextProbe.ProbeGraphics",true),F,null,new object[]{content,false},null))
        {
            // The captured original LegacyNPCProfile constructor requires
            // Main.Assets. Reuse the probe's original asset-service initializer
            // backed by this complete, locked Content directory, without stubs.
            graphics.GetType().GetMethod("EnsureNativeAssets",F).Invoke(graphics,null);
            InstallCounts(context);
            try{Invoke("NativeCombatNaturalImpactChecks","RunCpu",context,graphics,output);}finally{WriteCounts();}
        }
    }
    struct Pose {internal float X,Y,Vx,Vy;}
    struct Counts {internal uint Tick;internal int Cells,Reads,Moves,StillPressed;internal bool Left,Right,Exceeded;}
    static int cells,reads,moves,stillPressed;
    static readonly List<Counts> rows=new List<Counts>(2048);
    static Func<bool> exceeded;static Harmony audit;
    static void CellBefore(){cells++;}
    static void ReadBefore(){reads++;}
    static void PlayerBefore(PredictionPlayerMotion p,out Pose __state){__state=new Pose{X=p.X,Y=p.Y,Vx=p.Vx,Vy=p.Vy};}
    static void PlayerAfter(PredictionPlayerMotion p,Pose __state,bool __result)
    {
        moves++;
        if(__result && (p.Left||p.Right) && p.Vx==0 && p.Vy==0 && __state.Vx==0 && __state.Vy==0 && p.X==__state.X && p.Y==__state.Y)stillPressed++;
    }
    static void StepBefore(){cells=reads=moves=stillPressed=0;}
    static void StepAfter(){var p=Terraria.Main.LocalPlayer;rows.Add(new Counts{Tick=Terraria.Main.GameUpdateCount,Cells=cells,Reads=reads,Moves=moves,StillPressed=stillPressed,Left=p.controlLeft,Right=p.controlRight,Exceeded=exceeded()});}
    static void InstallCounts(object context)
    {
        var host=context.GetType().GetProperty("CombatObservation",F).GetValue(context,null);var work=host.GetType().GetField("PredictionWork",F).GetValue(host);exceeded=(Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>),work,work.GetType().GetProperty("Exceeded",F).GetGetMethod(true));
        audit=new Harmony("JueMingR.Private.NaturalCpuCounters");var terrain=context.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);
        foreach(string name in new[]{"CellAt","Read"}){var target=terrain.GetMethod(name,F);Console.WriteLine("COUNTER target="+target.DeclaringType.FullName+"."+name+" ILbytes="+target.GetMethodBody().GetILAsByteArray().Length+"; entry counts can omit already-inlined callers");audit.Patch(target,prefix:new HarmonyMethod(typeof(RunNaturalCpuCount).GetMethod(name=="Read"?"ReadBefore":"CellBefore",F)));}
        var move=Assembly.Load("JueMingR.Features").GetType("JueMingR.Features.Combat.RollingNpcPrediction",true).GetMethod("AdvancePlayer",F);
        audit.Patch(move,prefix:new HarmonyMethod(typeof(RunNaturalCpuCount).GetMethod("PlayerBefore",F)),postfix:new HarmonyMethod(typeof(RunNaturalCpuCount).GetMethod("PlayerAfter",F)));
        var step=probe.GetType("NativeWorldTextProbe.NativeCombatNaturalImpactChecks",true).GetMethod("CpuStep",F);
        audit.Patch(step,prefix:new HarmonyMethod(typeof(RunNaturalCpuCount).GetMethod("StepBefore",F)),postfix:new HarmonyMethod(typeof(RunNaturalCpuCount).GetMethod("StepAfter",F)));
        Console.WriteLine("COUNTER no business ref/out/__args bindings; private __state only. No per-cell timestamps; diagnostic time is NOT admission. Pose/zeroV is a clue, not a complete fixed-point proof.");
    }
    static void WriteCounts()
    {
        if(audit!=null)audit.UnpatchAll(audit.Id);
        var lines=new List<string>(rows.Count+1){"tick,cellAtEntries,nativeCellReads,advancePlayerReturns,pressedSamePoseZeroVelocity,realLeft,realRight,budgetExceeded"};long c=0,r=0,m=0,s=0;int e=0;
        foreach(var row in rows){lines.Add(string.Join(",",row.Tick,row.Cells,row.Reads,row.Moves,row.StillPressed,row.Left,row.Right,row.Exceeded));c+=row.Cells;r+=row.Reads;m+=row.Moves;s+=row.StillPressed;if(row.Exceeded)e++;}
        File.WriteAllLines(Path.Combine(output,"natural-cpu-counts.csv"),lines);Console.WriteLine("COUNTER samples="+rows.Count+" cellEntries="+c+" nativeReads="+r+" playerReturns="+m+" futurePressedSamePoseZeroV="+s+" budgetExceeded="+e+"; retain truncated frames, no timing admission");
    }
}
