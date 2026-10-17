using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

// Private incident carrier only. Existing probe leaves own their assertions,
// native consumers, observations and cleanup. This is not a timing platform.
internal static class RunIncidentRegression
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    static Assembly probe,game,loadedHost,harmonyAssembly;
    static string payload,output,content,leaf;

    static object Invoke(string type,string method,params object[] args)
    {return probe.GetType("NativeWorldTextProbe."+type,true).GetMethod(method,F).Invoke(null,args);}
    static object Get(object value,string name)
    {
        var type=value.GetType();var property=type.GetProperty(name,F);
        if(property!=null)return property.GetValue(value,null);
        var field=type.GetField(name,F);if(field==null)throw new MissingFieldException(type.FullName,name);
        return field.GetValue(value);
    }
    static string Hash(string path)
    {using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");}
    static bool GraphicsLeaf {get{return leaf=="shape-visual" || leaf=="sealed-child-draw";}}
    static void RequireLeaf(string value)
    {
        switch(value)
        {
            case "basic-aim":case "candidate-demand":case "observation":case "geometry":
            case "shape-visual":case "sealed-child-draw":case "spears":return;
            default:throw new ArgumentException("Unknown incident regression leaf: "+value);
        }
    }
    static Assembly LoadPayload(string name)
    {
        string path=Path.GetFullPath(Path.Combine(payload,name+".dll"));
        if(!File.Exists(path))throw new FileNotFoundException("Incomplete explicit candidate payload",path);
        var assembly=Assembly.LoadFrom(path);
        if(!string.Equals(Path.GetFullPath(assembly.Location),path,StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Candidate bound outside explicit payload: "+name+" at "+assembly.Location);
        var debug=(DebuggableAttribute)Attribute.GetCustomAttribute(assembly,typeof(DebuggableAttribute));
        if(name.StartsWith("JueMingR.",StringComparison.Ordinal) && debug!=null && debug.IsJITOptimizerDisabled)
            throw new InvalidOperationException("Unoptimized candidate: "+name);
        var version=(AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(assembly,typeof(AssemblyInformationalVersionAttribute));
        Console.WriteLine("LOAD "+name+" source="+(version==null?"<unavailable>":version.InformationalVersion)+" MVID="+assembly.ManifestModule.ModuleVersionId+" SHA256="+Hash(assembly.Location)+" location="+assembly.Location);
        return assembly;
    }
    static int Main(string[] args)
    {
        try
        {
            if(args.Length!=6)throw new ArgumentException("repository fixedProbe payload output Content basic-aim|candidate-demand|observation|geometry|shape-visual|sealed-child-draw|spears");
            string repo=Path.GetFullPath(args[0]),probePath=Path.GetFullPath(args[1]);
            payload=Path.GetFullPath(args[2]);output=Path.GetFullPath(args[3]);content=Path.GetFullPath(args[4]);leaf=args[5];RequireLeaf(leaf);
            if(GraphicsLeaf && !Directory.Exists(content))throw new DirectoryNotFoundException("Original Content required for actual Draw: "+content);
            Directory.CreateDirectory(output);
            AppDomain.CurrentDomain.AssemblyResolve+=(sender,eventArgs)=>
            {
                string name=new AssemblyName(eventArgs.Name).Name;string path=Path.Combine(payload,name+".dll");
                return (name.StartsWith("JueMingR.",StringComparison.Ordinal) || name=="0Harmony") && File.Exists(path)?Assembly.LoadFrom(path):null;
            };
            harmonyAssembly=LoadPayload("0Harmony");
            foreach(string name in new[]{"JueMingR.Platform","JueMingR.Features","JueMingR.Infrastructure","JueMingR.TerrariaHost"})
            {var assembly=LoadPayload(name);if(name=="JueMingR.TerrariaHost")loadedHost=assembly;}
            // Bind the actual four Release modules before the Debug probe's
            // typed methods are JITted; never borrow its adjacent Debug output.
            probe=Assembly.LoadFrom(probePath);var program=probe.GetType("NativeWorldTextProbe.Program",true);
            program.GetProperty("Repository",F).SetValue(null,repo,null);
            program.GetProperty("ProductionConfiguration",F).SetValue(null,"Release",null);
            program.GetField("references",F).SetValue(null,Path.Combine(repo,"external","TerrariaRefs"));
            AppDomain.CurrentDomain.AssemblyResolve+=(ResolveEventHandler)Delegate.CreateDelegate(typeof(ResolveEventHandler),program.GetMethod("Resolve",F));
            game=Assembly.LoadFrom(Path.Combine(repo,"external","TerrariaRefs","Terraria.exe"));program.GetField("game",F).SetValue(null,game);
            // Match the existing natural runner's short, process-private save
            // root. Long evidence-directory names must not alter native I/O.
            string user=Path.Combine(repo,".local","ir-"+Guid.NewGuid().ToString("N").Substring(0,8));Directory.CreateDirectory(user);
            game.GetType("Terraria.Program",true).GetField("SavePath",F).SetValue(null,user);
            Console.WriteLine("ISOLATED SavePath="+user);
            Console.WriteLine("GAME MVID="+game.ManifestModule.ModuleVersionId+" SHA256="+Hash(game.Location)+" location="+game.Location);
            Console.WriteLine("PROBE MVID="+probe.ManifestModule.ModuleVersionId+" SHA256="+Hash(probe.Location)+" location="+probe.Location);
            Console.WriteLine("INCIDENT leaf="+leaf+" actualDraw="+GraphicsLeaf+" Content="+content+"; functional regression, no FPS/timing claim");
            // Explicit leaf arguments own the existing Usage selector; an
            // inherited item subset must not silently trim that obligation.
            Environment.SetEnvironmentVariable("JMR_USAGE_ORIGINAL_FEEDBACK",leaf=="basic-aim"?"basic-aim":leaf=="spears"?"spears":leaf=="sealed-child-draw"?"seams":null);
            Environment.SetEnvironmentVariable("JMR_USAGE_ITEM",null);
            Environment.SetEnvironmentVariable("JUEMINGR_AIM_LIGHT_OFF","1");
            Invoke("NativeQuickItemChecks","Run",(Action<object>)Exercise,false,false,false,true,true,Path.Combine(payload,"JueMingR.TerrariaHost.dll"),null,null,false);
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }

    // Reuse only the established unrelated achievement isolation. Native
    // Update/ItemCheck/Shoot/NPC/projectile/Damage and business parameters are
    // untouched; no FirstChance, out/__args observer or timing hooks are added.
    static object AchievementOutlets()
    {
        var harmonyType=harmonyAssembly.GetType("HarmonyLib.Harmony",true);
        var methodType=harmonyAssembly.GetType("HarmonyLib.HarmonyMethod",true);
        var audit=Activator.CreateInstance(harmonyType,new object[]{"JueMingR.Tests.IncidentRegressionOutlets"});
        var patch=harmonyType.GetMethod("Patch",F);var skip=probe.GetType("NativeWorldTextProbe.NativeCombatCadenceChecks",true).GetMethod("SkipAchievement",F);
        if(patch==null || skip==null)throw new MissingMethodException("Established achievement isolation seam unavailable");
        try
        {
            foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})
            {
                var original=game.GetType("Terraria.GameContent.Achievements.AchievementsHelper",true).GetMethod(name,F);
                if(original==null)throw new MissingMethodException("Terraria achievement outlet "+name);
                var arguments=new object[patch.GetParameters().Length];arguments[0]=original;arguments[1]=Activator.CreateInstance(methodType,new object[]{skip});
                patch.Invoke(audit,arguments);
            }
            return audit;
        }
        catch{harmonyType.GetMethod("UnpatchAll",F).Invoke(audit,new object[]{"JueMingR.Tests.IncidentRegressionOutlets"});throw;}
    }
    [MethodImpl(MethodImplOptions.NoInlining)]static void Exercise(object context)
    {
        Invoke("NativeCombatAimChecks","Initialize");
        var observation=Get(context,"CombatObservation");
        if(!ReferenceEquals(observation.GetType().Assembly,loadedHost))throw new InvalidOperationException("Context observation is not the explicitly loaded candidate Host");
        Console.WriteLine("CONTEXT Host MVID="+observation.GetType().Assembly.ManifestModule.ModuleVersionId+" location="+observation.GetType().Assembly.Location);
        object audit=AchievementOutlets();
        try
        {
            if(GraphicsLeaf)
            {
                using(var graphics=(IDisposable)Activator.CreateInstance(probe.GetType("NativeWorldTextProbe.ProbeGraphics",true),F,null,new object[]{content,false},null))
                {
                    graphics.GetType().GetMethod("EnsureNativeAssets",F).Invoke(graphics,null);
                    if(leaf=="shape-visual")
                    {
                        // Same real world-layer readiness fixture as the
                        // existing Visual.World mother; shape tests own pixels.
                        var status=observation.GetType().GetField("LayerStatus",F);
                        status.SetValue(observation,Enum.Parse(status.FieldType,"Ready"));
                        Invoke("NativeCombatShapeVisualChecks","Run",context,graphics,output);
                    }
                    else Invoke("NativeCombatUsageChecks","Run",context,graphics,output);
                }
                return;
            }
            switch(leaf)
            {
                case "basic-aim":case "spears":Invoke("NativeCombatUsageChecks","Run",context,null,output);break;
                case "candidate-demand":Invoke("NativeCombatCandidateDemandChecks","Run",context);break;
                case "observation":Invoke("NativeCombatObservationChecks","Run",context);break;
                case "geometry":Invoke("NativeCombatGeometryChecks","Run",context);break;
                default:throw new InvalidOperationException("Unsupported dispatched leaf: "+leaf);
            }
        }
        finally{audit.GetType().GetMethod("UnpatchAll",F).Invoke(audit,new object[]{"JueMingR.Tests.IncidentRegressionOutlets"});}
    }
}
