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
internal static class RunNaturalStateCount
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
            Console.WriteLine("GAME MVID="+game.ManifestModule.ModuleVersionId+" SHA256="+Hash(game.Location));Console.WriteLine("PROBE SHA256="+Hash(probe.Location)+" scope=NaturalCpuState:no-draw mode="+mode+"; release runtime, debug-only measurement counters unused");
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
            graphics.GetType().GetMethod("EnsureNativeAssets",F).Invoke(graphics,null);
            Exception runFailure=null;
            try{Install();Invoke("NativeCombatNaturalImpactChecks","RunCpu",context,graphics,output);}
            catch(Exception error){runFailure=error;throw;}
            finally
            {
                var errors=new List<Exception>();
                try{if(audit!=null)audit.UnpatchAll(audit.Id);}catch(Exception error){errors.Add(error);}
                finally{depth=0;Array.Clear(stack,0,stack.Length);audit=null;}
                try{Write();}catch(Exception error){errors.Add(error);}
                if(errors.Count!=0){if(runFailure!=null)errors.Insert(0,runFailure);throw new AggregateException("State observation cleanup/output failed.",errors);}
            }
        }
    }
    struct Frame
    {
        internal bool B,C,D,E,Returned,Success;
        internal PredictionPlayerMotion Before,Fluid,Border,Item;
    }
    struct Counts
    {
        internal uint Tick;
        internal int Case,Frame;internal bool StepFailed;
        internal int Calls,Complete,Unobserved,Failed,MissingTail,Bc,Bd,De,Pressed,Zero,PressedZero,PressedZeroBc,PressedZeroBd;
    }
    struct Difference
    {
        internal uint Tick;internal int Case,Frame;internal PredictionPlayerMotion B,C,D,E;
    }
    // One isolated player update thread. No per-call allocation or formatted
    // logging; the stack also keeps nested scopes from observing one another.
    static readonly Frame[] stack=new Frame[16];
    static readonly Counts[] rows=new Counts[2048];
    static readonly Difference[] differences=new Difference[16];
    [ThreadStatic]static int depth;
    static int rowCount,differenceCount,overflow;
    static int ownerThread,otherThreadCalls,caseIndex=-1,sceneFrame;
    static Guid worldId;
    static Counts current;
    static Harmony audit;
    static void Enter(out int __state)
    {
        if(System.Threading.Thread.CurrentThread.ManagedThreadId!=ownerThread){System.Threading.Interlocked.Increment(ref otherThreadCalls);__state=-1;return;}
        if(depth>=stack.Length)throw new InvalidOperationException("State observer nesting exceeds its finite stack.");
        __state=depth;stack[depth++]=default(Frame);
    }
    static void Returned(bool __result){if(depth>0){var f=stack[depth-1];f.Returned=true;f.Success=__result;stack[depth-1]=f;}}
    static Exception Leave(int __state,Exception __exception)
    {
        if(__state<0)return __exception;
        try
        {
            var f=stack[__state];current.Calls++;
            if(__exception!=null || !f.Returned || !f.Success){current.Failed++;return __exception;}
            if(!f.B){current.Unobserved++;return __exception;}
            if(!f.C || !f.D || !f.E){current.MissingTail++;return __exception;}
            current.Complete++;
            bool bc=f.Before.SameSample(f.Fluid),bd=f.Before.SameSample(f.Border),de=f.Border.SameSample(f.Item);
            if(bc)current.Bc++;if(bd)current.Bd++;if(de)current.De++;
            bool pressed=f.Before.Left || f.Before.Right;
            bool zero=f.Before.Vx==0 && f.Before.Vy==0 && f.Border.Vx==0 && f.Border.Vy==0;
            if(pressed)current.Pressed++;if(zero)current.Zero++;
            if(pressed && zero){current.PressedZero++;if(bc)current.PressedZeroBc++;if(bd)current.PressedZeroBd++;}
            if((!bc || !bd || !de) && differenceCount<differences.Length)
                differences[differenceCount++]=new Difference{Tick=Terraria.Main.GameUpdateCount,Case=current.Case,Frame=current.Frame,B=f.Before,C=f.Fluid,D=f.Border,E=f.Item};
            return __exception;
        }
        finally{stack[__state]=default(Frame);depth=__state;}
    }
    // p is a typed VALUE copy of the business ref parameter. Only our
    // Harmony __state uses out; no __args or business out/ref is bound.
    static void Horizontal(PredictionPlayerMotion p)
    {
        if(depth==0)return;var f=stack[depth-1];
        if(!f.B){f.B=true;f.Before=p;stack[depth-1]=f;}
    }
    static void BeforeBorder(PredictionPlayerMotion p)
    {
        if(depth==0)return;var f=stack[depth-1];
        if(f.B && !f.C){f.C=true;f.Fluid=p;stack[depth-1]=f;}
    }
    static void BeforeItem(PredictionPlayerMotion p,out bool __state)
    {
        __state=false;if(depth==0)return;var f=stack[depth-1];
        if(f.B && f.C && !f.D){f.D=true;f.Border=p;stack[depth-1]=f;__state=true;}
    }
    static void AfterItem(PredictionPlayerMotion p,bool __state)
    {
        if(!__state || depth==0)return;var f=stack[depth-1];f.E=true;f.Item=p;stack[depth-1]=f;
    }
    static void StepBefore()
    {
        var id=Terraria.Main.ActiveWorldFileData.UniqueId;
        if(id!=worldId){worldId=id;caseIndex++;sceneFrame=0;}
        current=new Counts{Case=caseIndex,Frame=sceneFrame++};
    }
    static Exception StepAfter(Exception __exception)
    {
        current.StepFailed=__exception!=null;
        current.Tick=Terraria.Main.GameUpdateCount;
        if(rowCount<rows.Length)rows[rowCount++]=current;else overflow++;
        return __exception;
    }
    static MethodInfo Method(Type type,string name,Type[] signature)
    {
        var method=type.GetMethod(name,F,null,signature,null);
        if(method==null || method.GetMethodBody()==null)throw new MissingMethodException(type.FullName,name);
        Console.WriteLine("STATE target="+type.FullName+"."+name+" MVID="+type.Assembly.ManifestModule.ModuleVersionId+" ILbytes="+method.GetMethodBody().GetILAsByteArray().Length);
        return method;
    }
    static HarmonyMethod Hook(string name){return new HarmonyMethod(typeof(RunNaturalStateCount).GetMethod(name,F));}
    static void Install()
    {
        var features=Assembly.Load("JueMingR.Features");
        var rolling=features.GetType("JueMingR.Features.Combat.RollingNpcPrediction",true);
        var continuation=features.GetType("JueMingR.Features.Combat.PlayerMotionContinuation",true);
        var horizontal=features.GetType("JueMingR.Features.Combat.PlayerHorizontalMotion",true);
        var motion=typeof(PredictionPlayerMotion);var byref=motion.MakeByRefType();
        ownerThread=System.Threading.Thread.CurrentThread.ManagedThreadId;
        audit=new Harmony("JueMingR.Private.NaturalStateCount");
        var advance=Method(rolling,"AdvancePlayer",new[]{byref,typeof(PredictionEnvironment),typeof(IPredictionTerrain),typeof(PredictionStop).MakeByRefType()});
        audit.Patch(advance,prefix:Hook("Enter"),postfix:Hook("Returned"),finalizer:Hook("Leave"));
        audit.Patch(Method(horizontal,"Step",new[]{byref}),prefix:Hook("Horizontal"));
        audit.Patch(Method(continuation,"ClampBorders",new[]{byref,typeof(PredictionStop).MakeByRefType()}),prefix:Hook("BeforeBorder"));
        audit.Patch(Method(continuation,"CompleteItemCheck",new[]{byref}),prefix:Hook("BeforeItem"),postfix:Hook("AfterItem"));
        var step=probe.GetType("NativeWorldTextProbe.NativeCombatNaturalImpactChecks",true).GetMethod("CpuStep",F);
        if(step==null)throw new MissingMethodException("CpuStep");
        audit.Patch(step,prefix:Hook("StepBefore"),finalizer:Hook("StepAfter"));
        Console.WriteLine("STATE diagnostic only; same RunCpu sequence, no timing admission. B=Horizontal entry after three prefixes; C=ClampBorders entry after AfterFluid; D=CompleteItemCheck entry after borders; E=ItemCheck exit. Full SameSample, no normalized clocks. No Horizontal/handled/settled is unobserved, not stable. Exceptions clear scope; missing tails/row overflow remain explicit.");
    }
    static string FirstDifference(PredictionPlayerMotion a,PredictionPlayerMotion b)
    {
        // Reflection and string formatting occur only after the native window.
        foreach(var field in typeof(PredictionPlayerMotion).GetFields(BindingFlags.Public|BindingFlags.Instance))
        {
            object x=field.GetValue(a),y=field.GetValue(b);
            bool same=field.Name=="PlayerToken"?ReferenceEquals(x,y):field.FieldType==typeof(float)?(float)x==(float)y:Equals(x,y);
            if(!same)return field.Name+"="+x+" -> "+y;
        }
        return "<none>";
    }
    static void Write()
    {
        var lines=new List<string>(rowCount+1){"case,frame,tick,stepFailed,calls,completeBCDE,unobservedNoHorizontal,failed,missingTail,fullBC,fullBD,fullDE,pressed,zeroBeforeAndBorder,pressedZero,pressedZeroFullBC,pressedZeroFullBD"};
        for(int i=0;i<rowCount;i++){var r=rows[i];lines.Add(string.Join(",",r.Case,r.Frame,r.Tick,r.StepFailed,r.Calls,r.Complete,r.Unobserved,r.Failed,r.MissingTail,r.Bc,r.Bd,r.De,r.Pressed,r.Zero,r.PressedZero,r.PressedZeroBc,r.PressedZeroBd));}
        File.WriteAllLines(Path.Combine(output,"natural-state-counts.csv"),lines);
        var details=new List<string>{"Diagnostic only, unchanged full RunCpu frames. Counts are observations, not cache equivalence or performance admission. Exact SameSample includes action clocks. Prefix/postfix callers may already be inlined; target identity logging does not prove complete observer coverage. Complete counts require all four boundaries; complex/hover/handled/existing settled without Horizontal are unobserved. Case increments only when the existing fixture replaces world UniqueId; frame is every actual CpuStep, including failures and zero-call/budget-truncated frames. Join original CPU rows by case+frame, never GUC alone. Phases remain idle0..179/attack180..269/switch270..273/restore274..299/release300+. No missing-frame removal or phase assertion. Row overflow="+overflow+" unobservedOtherThreadCalls="+otherThreadCalls};
        for(int i=0;i<differenceCount;i++){var d=differences[i];details.Add("case="+d.Case+" frame="+d.Frame+" tick="+d.Tick+" firstBC="+FirstDifference(d.B,d.C)+" firstBD="+FirstDifference(d.B,d.D)+" firstDE="+FirstDifference(d.D,d.E));}
        File.WriteAllLines(Path.Combine(output,"natural-state-differences.txt"),details);
        Console.WriteLine("STATE rows="+rowCount+" boundedDifferences="+differenceCount+" overflow="+overflow+"; no blanket PASS");
    }
}

