using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Runtime.ExceptionServices;
internal static class RoomPhaseMeasure
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    static Assembly probe;static string output,content;static string mode;
    static readonly string[] errors=new string[24];static int errorCount;static bool capturing,printed;
    static string Safe(Func<string> read){try{string v=read();return v==null?"<null>":v.Substring(0,Math.Min(2400,v.Length));}catch{return "<getter failed>";}}
    static void FirstChance(object sender,FirstChanceExceptionEventArgs e)
    {
        if(e.Exception.GetType().Name=="CombatPredictionBudgetExceededException"){budgetExceptions++;return;}
        if(capturing)return;capturing=true;
        try{errors[errorCount++%errors.Length]="RAW type="+Safe(()=>e.Exception.GetType().FullName)+" assembly="+Safe(()=>e.Exception.GetType().Assembly.Location)+" message="+Safe(()=>e.Exception.Message)+" stack="+Safe(()=>e.Exception.StackTrace);}
        finally{capturing=false;}
    }
    static void Dump(Exception final)
    {
        if(printed)return;printed=true;
        try{Console.Error.WriteLine("FINAL type="+Safe(()=>final.GetType().FullName)+" message="+Safe(()=>final.Message)+" stack="+Safe(()=>final.StackTrace));for(int i=Math.Max(0,errorCount-errors.Length);i<errorCount;i++)Console.Error.WriteLine(errors[i%errors.Length]);}catch{}
    }
    static object Invoke(string type,string method,params object[] args){return probe.GetType("NativeWorldTextProbe."+type,true).GetMethod(method,F).Invoke(null,args);}
    static string Hash(string path){using(var f=File.OpenRead(path))using(var s=SHA256.Create())return BitConverter.ToString(s.ComputeHash(f)).Replace("-","");}
    static int Main(string[] args)
    {
        try
        {
            AppDomain.CurrentDomain.FirstChanceException+=FirstChance;
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
            Console.WriteLine("GAME MVID="+game.ManifestModule.ModuleVersionId+" SHA256="+Hash(game.Location));Console.WriteLine("PROBE SHA256="+Hash(probe.Location)+" scope=CombatImpactVisual:natural-slime mode="+mode+"; release runtime, debug-only measurement counters unused");
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
            Install(context);
            try{Invoke("NativeCombatNaturalImpactChecks",mode=="reference"?"RunReference":mode=="candidate"?"RunCandidate":"Run",context,graphics,output);}finally{Report();}
        }
    }
    static readonly System.Collections.Generic.Dictionary<MethodBase,int> ids=new System.Collections.Generic.Dictionary<MethodBase,int>();
    static readonly string[] names=new string[20];static readonly int[] calls=new int[20];static readonly long[] total=new long[20],maximum=new long[20];static int used,budgetExceptions;
    static void Before(MethodBase __originalMethod,out long __state){__state=Stopwatch.GetTimestamp();calls[ids[__originalMethod]]++;}
    static Exception After(MethodBase __originalMethod,long __state,Exception __exception){long elapsed=Stopwatch.GetTimestamp()-__state;int id=ids[__originalMethod];total[id]+=elapsed;maximum[id]=Math.Max(maximum[id],elapsed);return __exception;}
    static void Patch(HarmonyLib.Harmony h,Type type,string name)
    {foreach(var m in type.GetMethods(F)){if(m.Name!=name)continue;int id=used++;ids.Add(m,id);names[id]=type.Name+"."+name;h.Patch(m,prefix:new HarmonyLib.HarmonyMethod(typeof(RoomPhaseMeasure).GetMethod("Before",F)),finalizer:new HarmonyLib.HarmonyMethod(typeof(RoomPhaseMeasure).GetMethod("After",F)));}}
    static void Install(object context)
    {
        var host=context.GetType().Assembly;var features=Assembly.Load("JueMingR.Features");var h=new HarmonyLib.Harmony("JueMingR.Private.RoomPhaseMeasure");
        foreach(string entry in new[]{"HostCombatObservation.UpdateSampleCore","NpcPredictionSource.PrepareSample","HostAttackAim.ResolveSelection","HostAttackAim.PreparePlan","HostAttackAim.SolveOnTerrain","HostProjectileEnvironment.Solve","HostAttackAim.ReadFlightCore"}){var pair=entry.Split('.');Patch(h,host.GetType("JueMingR.TerrariaHost.Combat."+pair[0],true),pair[1]);}
        Patch(h,features.GetType("JueMingR.Features.Combat.RollingNpcPrediction",true),"Prepare");
        Patch(h,features.GetType("JueMingR.Features.Combat.AttackIntercept",true),"Solve");
    }
    static void Report(){for(int i=0;i<used;i++)Console.WriteLine("ROOM-PHASE "+names[i]+" calls="+calls[i]+" inclusiveMs="+(total[i]*1000.0/Stopwatch.Frequency).ToString("R")+" maxMs="+(maximum[i]*1000.0/Stopwatch.Frequency).ToString("R"));Console.WriteLine("ROOM-PHASE budgetExceptions="+budgetExceptions+"; nested inclusive times do not add; instrumentation run, not performance admission");}}
