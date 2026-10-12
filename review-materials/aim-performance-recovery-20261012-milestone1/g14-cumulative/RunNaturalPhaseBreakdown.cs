using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

// Parent-boundary attribution only. No cell, player-action or future-step
// hooks; no FirstChance subscription. Inclusive times overlap and DO NOT add.
internal static class RunNaturalPhaseBreakdown
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    const int MaxRows=4*480,MaxMethods=32;
    const string AuditId="JueMingR.Private.NaturalPhaseBreakdown";
    static Assembly probe,game,host,features,harmonyAssembly;
    static string payload,output,content;
    static object audit;
    static readonly Row[] rows=new Row[MaxRows];
    static readonly int[] calls=new int[MaxRows*MaxMethods],exceptionExits=new int[MaxRows*MaxMethods],budgetExits=new int[MaxRows*MaxMethods];
    static readonly long[] inclusive=new long[MaxRows*MaxMethods],maximum=new long[MaxRows*MaxMethods];
    static readonly Dictionary<MethodBase,int> ids=new Dictionary<MethodBase,int>();
    static readonly MethodInfo[] methods=new MethodInfo[MaxMethods];
    static readonly string[] names=new string[MaxMethods];
    static readonly long[] outsideCalls=new long[MaxMethods];
    static readonly object[] caseWorlds=new object[4];
    static readonly int[] caseFrames=new int[4];
    static readonly List<string> missing=new List<string>();
    static int methodCount,rowCount,caseIndex=-1,frameIndex,mainThread;
    static int rowOverflow,caseOverflow,nestedSteps;
    static object lastWorld;
    [ThreadStatic]static bool inStep;
    [ThreadStatic]static int activeRow;
    static Func<object> worldFile;
    static Func<uint> worldTick;
    static Func<ulong> budgetTick;
    static Func<bool> budgetExceeded;
    static Func<double> budgetMilliseconds;
    static Exception runFailure;
    static bool reported;

    struct Row
    {
        internal int Case,Frame;
        internal uint BeginTick,EndTick;
        internal long Elapsed;
        internal bool Completed,Failed,StatusCaptured,BudgetCurrent,Exceeded;
        internal ulong BudgetTick;
        internal double BudgetMilliseconds;
        internal string ExceptionType,DiagnosticExceptionType;
    }
    // Harmony's __state is observer-owned. No native inputs, outputs or
    // mutable __args are bound, copied back or changed by these callbacks.
    struct TimerState {internal int Row,Id;internal long Started;internal bool Active;}
    struct StepState {internal int Row;internal long Started;internal bool Active;}

    static object Get(object owner,string name)
    {
        var type=owner.GetType();var property=type.GetProperty(name,F);
        if(property!=null)return property.GetValue(owner,null);
        var field=type.GetField(name,F);if(field==null)throw new MissingFieldException(type.FullName,name);
        return field.GetValue(owner);
    }
    static object Invoke(string type,string method,params object[] args)
    {return probe.GetType("NativeWorldTextProbe."+type,true).GetMethod(method,F).Invoke(null,args);}
    static string Hash(string path)
    {using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");}
    static Assembly LoadPayload(string name)
    {
        string path=Path.GetFullPath(Path.Combine(payload,name+".dll"));
        if(!File.Exists(path))throw new FileNotFoundException("Incomplete explicit payload",path);
        var assembly=Assembly.LoadFrom(path);
        if(!string.Equals(Path.GetFullPath(assembly.Location),path,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Candidate bound outside payload: "+assembly.Location);
        var debug=(DebuggableAttribute)Attribute.GetCustomAttribute(assembly,typeof(DebuggableAttribute));
        if(name.StartsWith("JueMingR.",StringComparison.Ordinal) && debug!=null && debug.IsJITOptimizerDisabled)throw new InvalidOperationException("Unoptimized candidate: "+name);
        var source=(AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(assembly,typeof(AssemblyInformationalVersionAttribute));
        Console.WriteLine("LOAD "+name+" source="+(source==null?"unavailable":source.InformationalVersion)+" MVID="+assembly.ManifestModule.ModuleVersionId+" SHA256="+Hash(assembly.Location)+" location="+assembly.Location);
        return assembly;
    }
    static int Main(string[] args)
    {
        try
        {
            if(args.Length!=6 || args[5]!="all")throw new ArgumentException("repository fixedProbe payload output Content all");
            string repo=Path.GetFullPath(args[0]),probePath=Path.GetFullPath(args[1]);
            payload=Path.GetFullPath(args[2]);output=Path.GetFullPath(args[3]);content=Path.GetFullPath(args[4]);Directory.CreateDirectory(output);
            AppDomain.CurrentDomain.AssemblyResolve+=(sender,e)=>
            {
                string name=new AssemblyName(e.Name).Name;string path=Path.Combine(payload,name+".dll");
                return (name.StartsWith("JueMingR.",StringComparison.Ordinal) || name=="0Harmony") && File.Exists(path)?Assembly.LoadFrom(path):null;
            };
            // BCL-only declarations above prevent typed Platform/Features
            // state from binding before the four actual Release modules.
            harmonyAssembly=LoadPayload("0Harmony");LoadPayload("JueMingR.Platform");features=LoadPayload("JueMingR.Features");LoadPayload("JueMingR.Infrastructure");host=LoadPayload("JueMingR.TerrariaHost");
            probe=Assembly.LoadFrom(probePath);var program=probe.GetType("NativeWorldTextProbe.Program",true);
            program.GetProperty("Repository",F).SetValue(null,repo,null);program.GetProperty("ProductionConfiguration",F).SetValue(null,"Release",null);
            program.GetField("references",F).SetValue(null,Path.Combine(repo,"external","TerrariaRefs"));
            AppDomain.CurrentDomain.AssemblyResolve+=(ResolveEventHandler)Delegate.CreateDelegate(typeof(ResolveEventHandler),program.GetMethod("Resolve",F));
            game=Assembly.LoadFrom(Path.Combine(repo,"external","TerrariaRefs","Terraria.exe"));program.GetField("game",F).SetValue(null,game);
            string user=Path.Combine(repo,".local","ip-"+Guid.NewGuid().ToString("N").Substring(0,8));Directory.CreateDirectory(user);
            game.GetType("Terraria.Program",true).GetField("SavePath",F).SetValue(null,user);
            Console.WriteLine("ISOLATED SavePath="+user);
            Console.WriteLine("GAME MVID="+game.ManifestModule.ModuleVersionId+" SHA256="+Hash(game.Location)+" location="+game.Location);
            Console.WriteLine("PROBE MVID="+probe.ManifestModule.ModuleVersionId+" SHA256="+Hash(probe.Location)+" location="+probe.Location);
            Console.WriteLine("DIAGNOSTIC parent inclusive attribution; not performance admission. Inclusive values overlap and must not be summed. No Draw/GetData/PNG.");
            Environment.SetEnvironmentVariable("JUEMINGR_AIM_LIGHT_OFF","1");
            Invoke("NativeQuickItemChecks","Run",(Action<object>)Exercise,false,false,false,true,true,Path.Combine(payload,"JueMingR.TerrariaHost.dll"),null,null,false);
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }

    // Bind readonly getters once. No per-CpuStep reflection arrays/boxing and
    // no typed game/Platform declaration is needed in this carrier assembly.
    static Func<T> StaticGetter<T>(Type type,string name)
    {
        var property=type.GetProperty(name,F);
        if(property!=null)return (Func<T>)Delegate.CreateDelegate(typeof(Func<T>),property.GetGetMethod(true));
        var field=type.GetField(name,F);if(field==null || !field.IsStatic)throw new MissingFieldException(type.FullName,name);
        var method=new DynamicMethod("PhaseRead_"+name,typeof(T),Type.EmptyTypes,typeof(RunNaturalPhaseBreakdown),true);
        var il=method.GetILGenerator();il.Emit(OpCodes.Ldsfld,field);il.Emit(OpCodes.Ret);
        return (Func<T>)method.CreateDelegate(typeof(Func<T>));
    }
    static Func<T> FieldGetter<T>(object owner,string name)
    {
        var field=owner.GetType().GetField(name,F);if(field==null || field.FieldType!=typeof(T))throw new MissingFieldException(owner.GetType().FullName,name);
        var method=new DynamicMethod("PhaseRead_"+name,typeof(T),new[]{typeof(object)},typeof(RunNaturalPhaseBreakdown),true);
        var il=method.GetILGenerator();il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Castclass,field.DeclaringType);il.Emit(OpCodes.Ldfld,field);il.Emit(OpCodes.Ret);
        return (Func<T>)method.CreateDelegate(typeof(Func<T>),owner);
    }
    static Func<T> PropertyGetter<T>(object owner,string name)
    {
        var property=owner.GetType().GetProperty(name,F);if(property==null)throw new MissingMemberException(owner.GetType().FullName,name);
        return (Func<T>)Delegate.CreateDelegate(typeof(Func<T>),owner,property.GetGetMethod(true));
    }
    static void StepBefore(out StepState __state)
    {
        __state=default(StepState);
        if(inStep){nestedSteps++;return;}
        var world=worldFile();
        if(!ReferenceEquals(lastWorld,world))
        {
            lastWorld=world;caseIndex++;frameIndex=0;
            if(caseIndex<caseWorlds.Length)caseWorlds[caseIndex]=world;else caseOverflow++;
        }
        int frame=frameIndex++;if(caseIndex>=0 && caseIndex<caseFrames.Length)caseFrames[caseIndex]++;
        if(rowCount>=MaxRows){rowOverflow++;return;}
        int row=rowCount++;rows[row].Case=caseIndex;rows[row].Frame=frame;rows[row].BeginTick=worldTick();
        activeRow=row;inStep=true;__state=new StepState{Row=row,Started=Stopwatch.GetTimestamp(),Active=true};
    }
    static Exception StepAfter(StepState __state,Exception __exception)
    {
        if(!__state.Active)return __exception;
        long ended=Stopwatch.GetTimestamp();int row=__state.Row;inStep=false;
        rows[row].Elapsed=ended-__state.Started;
        rows[row].Completed=true;rows[row].Failed=__exception!=null;rows[row].ExceptionType=__exception==null?null:__exception.GetType().FullName;
        try
        {
            rows[row].EndTick=worldTick();rows[row].BudgetTick=budgetTick();rows[row].BudgetCurrent=rows[row].BudgetTick==rows[row].EndTick;
            rows[row].Exceeded=budgetExceeded();rows[row].BudgetMilliseconds=budgetMilliseconds();rows[row].StatusCaptured=true;
        }
        catch(Exception error){rows[row].DiagnosticExceptionType=error.GetType().FullName;}
        return __exception;
    }
    static void Before(MethodBase __originalMethod,out TimerState __state)
    {
        __state=default(TimerState);int id=ids[__originalMethod];
        if(!inStep || Thread.CurrentThread.ManagedThreadId!=mainThread){Interlocked.Increment(ref outsideCalls[id]);return;}
        int row=activeRow;calls[row*MaxMethods+id]++;
        __state=new TimerState{Row=row,Id=id,Started=Stopwatch.GetTimestamp(),Active=true};
    }
    static Exception After(TimerState __state,Exception __exception)
    {
        if(__state.Active)
        {
            long elapsed=Stopwatch.GetTimestamp()-__state.Started;int index=__state.Row*MaxMethods+__state.Id;
            inclusive[index]+=elapsed;if(elapsed>maximum[index])maximum[index]=elapsed;
            if(__exception!=null){exceptionExits[index]++;if(__exception.GetType().Name=="CombatPredictionBudgetExceededException")budgetExits[index]++;}
        }
        return __exception;
    }
    static void Patch(MethodInfo original,string prefix,string finalizer)
    {
        var methodType=harmonyAssembly.GetType("HarmonyLib.HarmonyMethod",true);var patch=audit.GetType().GetMethod("Patch",F);
        if(patch==null)throw new MissingMethodException("Harmony Patch unavailable");
        var args=new object[patch.GetParameters().Length];args[0]=original;
        bool hasPrefix=false,hasFinalizer=false;
        for(int i=0;i<args.Length;i++)
        {
            if(patch.GetParameters()[i].Name=="prefix"){args[i]=Activator.CreateInstance(methodType,new object[]{typeof(RunNaturalPhaseBreakdown).GetMethod(prefix,F)});hasPrefix=true;}
            if(patch.GetParameters()[i].Name=="finalizer"){args[i]=Activator.CreateInstance(methodType,new object[]{typeof(RunNaturalPhaseBreakdown).GetMethod(finalizer,F)});hasFinalizer=true;}
        }
        if(!hasPrefix || !hasFinalizer)throw new MissingMethodException("Harmony requires observer prefix and original-exception finalizer");
        patch.Invoke(audit,args);
    }
    static void BindBoundary(Assembly assembly,string typeName,string name)
    {
        var type=assembly.GetType(typeName,false);int found=0;
        if(type!=null)foreach(var method in type.GetMethods(F))
        {
            if(method.Name!=name)continue;found++;
            if(methodCount>=MaxMethods)throw new InvalidOperationException("Boundary metadata exceeds fixed slots");
            int id=methodCount++;ids.Add(method,id);methods[id]=method;names[id]=type.Name+"."+name;
        }
        if(found==0){string message=typeName+"."+name;missing.Add(message);Console.WriteLine("MISSING requested boundary "+message+"; no substitute search/patch");}
    }
    static void Install(object context)
    {
        var observation=Get(context,"CombatObservation");if(!ReferenceEquals(observation.GetType().Assembly,host))throw new InvalidOperationException("Context is not the explicit candidate Host");
        mainThread=Thread.CurrentThread.ManagedThreadId;var main=game.GetType("Terraria.Main",true);
        worldFile=StaticGetter<object>(main,"ActiveWorldFileData");worldTick=StaticGetter<uint>(main,"GameUpdateCount");
        var work=Get(observation,"PredictionWork");budgetTick=FieldGetter<ulong>(work,"tick");budgetExceeded=PropertyGetter<bool>(work,"Exceeded");budgetMilliseconds=PropertyGetter<double>(work,"UsedMilliseconds");
        audit=Activator.CreateInstance(harmonyAssembly.GetType("HarmonyLib.Harmony",true),new object[]{AuditId});
        var step=probe.GetType("NativeWorldTextProbe.NativeCombatNaturalImpactChecks",true).GetMethod("CpuStep",F);
        if(step==null)throw new MissingMethodException("Actual NaturalCpu.CpuStep unavailable");
        foreach(string entry in new[]{"HostCombatObservation.UpdateSampleCore","NpcPredictionSource.PrepareSample","HostAttackAim.ResolveSelection","HostAttackAim.PreparePlan","HostAttackAim.SolveOnTerrain","HostAttackAim.ReadFlightCore"})
        {var pair=entry.Split('.');BindBoundary(host,"JueMingR.TerrariaHost.Combat."+pair[0],pair[1]);}
        BindBoundary(features,"JueMingR.Features.Combat.RollingNpcPrediction","Prepare");
        BindBoundary(features,"JueMingR.Features.Combat.AttackIntercept","Solve");
        // Freeze the metadata map before any prefix can read it, and open
        // CpuStep recording only after all requested parents are installed.
        for(int id=0;id<methodCount;id++)Patch(methods[id],"Before","After");
        Patch(step,"StepBefore","StepAfter");
    }
    [MethodImpl(MethodImplOptions.NoInlining)]static void Exercise(object context)
    {
        Invoke("NativeCombatAimChecks","Initialize");
        using(var graphics=(IDisposable)Activator.CreateInstance(probe.GetType("NativeWorldTextProbe.ProbeGraphics",true),F,null,new object[]{content,false},null))
        {
            graphics.GetType().GetMethod("EnsureNativeAssets",F).Invoke(graphics,null);
            try{Install(context);Invoke("NativeCombatNaturalImpactChecks","RunCpu",context,graphics,output);}
            catch(Exception failure){runFailure=failure;throw;}
            finally
            {
                var cleanup=new List<Exception>();
                try{if(audit!=null)audit.GetType().GetMethod("UnpatchAll",F).Invoke(audit,new object[]{AuditId});}catch(Exception failure){cleanup.Add(failure);}
                finally{inStep=false;audit=null;}
                try{Write();}catch(Exception failure){cleanup.Add(failure);}
                if(cleanup.Count>0){if(runFailure!=null)cleanup.Insert(0,runFailure);throw new AggregateException("Phase diagnostic cleanup/output failures retained",cleanup);}
            }
        }
    }
    static string Phase(int frame){return frame<180?"idle":frame<270?"attack":frame<274?"switch":frame<300?"restore":"release";}
    static string Number(double value){return value.ToString("R",CultureInfo.InvariantCulture);}
    static string Csv(string value){return "\""+(value??"").Replace("\"","\"\"")+"\"";}
    static void Write()
    {
        if(reported)return;reported=true;
        using(var writer=new StreamWriter(Path.Combine(output,"natural-phase-frames.csv"),false,new UTF8Encoding(false)))
        {
            var header=new StringBuilder("case,moving,enabled,frame,phase,beginTick,endTick,stepCompleted,stepFailed,stepException,cpuStepInclusiveTicks,statusCaptured,diagnosticException,budgetCurrent,budgetTick,budgetExceeded,budgetMs");
            for(int id=0;id<methodCount;id++)header.Append(",m").Append(id).Append("Calls,m").Append(id).Append("InclusiveTicks,m").Append(id).Append("MaxTicks,m").Append(id).Append("ExceptionExits,m").Append(id).Append("BudgetExceptionExits");
            writer.WriteLine(header);
            for(int row=0;row<rowCount;row++)
            {
                var r=rows[row];var line=new StringBuilder();
                line.Append(r.Case).Append(',').Append(r.Case>=2).Append(',').Append((r.Case&1)!=0).Append(',').Append(r.Frame).Append(',').Append(Phase(r.Frame)).Append(',').Append(r.BeginTick).Append(',').Append(r.EndTick).Append(',').Append(r.Completed).Append(',').Append(r.Failed).Append(',').Append(Csv(r.ExceptionType)).Append(',').Append(r.Elapsed).Append(',').Append(r.StatusCaptured).Append(',').Append(Csv(r.DiagnosticExceptionType)).Append(',').Append(r.StatusCaptured?r.BudgetCurrent.ToString():"unavailable").Append(',').Append(r.StatusCaptured?r.BudgetTick.ToString(CultureInfo.InvariantCulture):"unavailable").Append(',').Append(r.StatusCaptured?r.Exceeded.ToString():"unavailable").Append(',').Append(r.StatusCaptured?Number(r.BudgetMilliseconds):"unavailable");
                for(int id=0;id<methodCount;id++){int index=row*MaxMethods+id;line.Append(',').Append(calls[index]).Append(',').Append(inclusive[index]).Append(',').Append(maximum[index]).Append(',').Append(exceptionExits[index]).Append(',').Append(budgetExits[index]);}
                writer.WriteLine(line);
            }
        }
        using(var writer=new StreamWriter(Path.Combine(output,"natural-phase-methods.csv"),false,new UTF8Encoding(false)))
        {
            writer.WriteLine("id,boundary,signature,implementationFlags,scopeOutsideOrOtherThreadCalls");
            for(int id=0;id<methodCount;id++)writer.WriteLine(id+","+Csv(names[id])+","+Csv(methods[id].ToString())+","+Csv(methods[id].GetMethodImplementationFlags().ToString())+","+outsideCalls[id]);
        }
        using(var writer=new StreamWriter(Path.Combine(output,"natural-phase-summary.txt"),false,new UTF8Encoding(false)))
        {
            writer.WriteLine("DIAGNOSTIC ONLY: parent-boundary relative attribution, not a new performance admission time. Inclusive values overlap/nest and DO NOT ADD. Instrumentation can itself trigger the production allowance; retain every exception/budget-truncated/zero-call/incomplete record.");
            writer.WriteLine("Original RunCpu is unchanged: standing OFF, standing ON, moving OFF, moving ON. Case identity follows each actual ActiveWorldFileData object replacement; frame increments at every real CpuStep. Join original natural-cpu-frames.csv by case+frame, never GUC alone. At least360 natural steps per case, actual exit may extend to480; no truncation or new warmup.");
            writer.WriteLine("CpuStep elapsed covers its body and nested probes; budget/status reads occur after its final timestamp. All arrays/slots/getters bound before RunCpu. Initial setup, Reset80, scope-external and other-thread calls are EXCLUDED from per-frame ownership and counted separately. No per-cell/player-step/future-step timestamp, FirstChance or native ref/out/__args observer.");
            writer.WriteLine("Hooks may miss calls already inlined into JITted callers; no-inline metadata does not prove complete coverage. A zero observed count is NOT proof of zero work. Uninstrumented parent residual includes native Player/NPC/projectile/shell/input work, child inline work and observation overhead. Boundary exception exits can count the same propagating exception at multiple nested boundaries; they are NOT distinct exception counts.");
            writer.WriteLine("StopwatchFrequency="+Stopwatch.Frequency+" rows="+rowCount+" cases="+(caseIndex+1)+" rowOverflow="+rowOverflow+" caseOverflow="+caseOverflow+" nestedCpuSteps="+nestedSteps+" requestedMissing="+missing.Count+" runFailed="+(runFailure!=null));
            foreach(string name in missing)writer.WriteLine("MISSING "+name);
            if(runFailure!=null)writer.WriteLine(runFailure.ToString());
            for(int c=0;c<4;c++)
            {
                writer.WriteLine("case="+c+" worldReferenceObserved="+(caseWorlds[c]!=null)+" frames="+caseFrames[c]);
                foreach(string phase in new[]{"idle","attack","switch","restore","release"})
                {
                    for(int id=-1;id<methodCount;id++)
                    {
                        int samples=0,completed=0,failed=0,exits=0,budgets=0;long n=0,ticks=0,max=0;
                        for(int row=0;row<rowCount;row++)
                        {
                            var r=rows[row];if(r.Case!=c || Phase(r.Frame)!=phase)continue;samples++;if(r.Completed)completed++;if(r.Failed)failed++;
                            if(id<0){n++;ticks+=r.Elapsed;if(r.Elapsed>max)max=r.Elapsed;}
                            else{int index=row*MaxMethods+id;n+=calls[index];ticks+=inclusive[index];if(maximum[index]>max)max=maximum[index];exits+=exceptionExits[index];budgets+=budgetExits[index];}
                        }
                        if(samples==0)continue;
                        writer.WriteLine("case="+c+" phase="+phase+" boundary="+(id<0?"CpuStep":names[id])+" n="+samples+" completed="+completed+" stepFailed="+failed+" calls="+n+" inclusiveMs="+Number(ticks*1000.0/Stopwatch.Frequency)+" inclusiveMeanPerFrameMs="+Number(ticks*1000.0/Stopwatch.Frequency/samples)+" maxCallMs="+Number(max*1000.0/Stopwatch.Frequency)+" exceptionBoundaryExits="+exits+" budgetExceptionBoundaryExits="+budgets);
                    }
                }
            }
        }
        Console.WriteLine("PHASE-DIAGNOSTIC rows="+rowCount+" boundaries="+methodCount+" missing="+missing.Count+" failure="+(runFailure!=null)+"; relative inclusive attribution only, not performance admission; inclusive values do not add");
    }
}
