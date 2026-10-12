using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Terraria;
using GameMain=Terraria.Main;
using Terraria.GameInput;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using JueMingR.Features.Combat;
using HarmonyLib;

internal static class ContinuousCombatMeasure
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    static Assembly probe; static string output, payload; static int weapon, frames;static bool moving,use;static string mode;
    static object Get(object o,string n){var p=o.GetType().GetProperty(n,F);return p!=null?p.GetValue(o,null):o.GetType().GetField(n,F).GetValue(o);}
    static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,F).Invoke(o,a);}
    static object Invoke(string t,string n,params object[] a){return probe.GetType("NativeWorldTextProbe."+t,true).GetMethod(n,F).Invoke(null,a);}
    static string Hash(string p){using(var f=File.OpenRead(p))using(var s=SHA256.Create())return BitConverter.ToString(s.ComputeHash(f)).Replace("-","");}
    static bool Skip(){return false;}
    static Dictionary<string,long> counts=new Dictionary<string,long>(),ticks=new Dictionary<string,long>();
    static void Count(MethodBase __originalMethod,out long __state){__state=Stopwatch.GetTimestamp();string k=__originalMethod.DeclaringType.Name+"."+__originalMethod.Name;long n;counts.TryGetValue(k,out n);counts[k]=n+1;}
    static void CountEnd(MethodBase __originalMethod,long __state){string k=__originalMethod.DeclaringType.Name+"."+__originalMethod.Name;long n;ticks.TryGetValue(k,out n);ticks[k]=n+Stopwatch.GetTimestamp()-__state;}
    static void InstallCounts(Harmony h){foreach(var a in AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name.StartsWith("JueMingR.")))foreach(var t in a.GetTypes())foreach(var m in t.GetMethods(F))if(new[]{"HostCombatObservation.UpdateSample","HostAttackAim.ResolveSelection","HostAttackAim.PreparePlan","HostAttackAim.ReadFlight","NpcPredictionSource.Prepare","NpcPredictionSource.PrepareAction","RollingNpcPrediction.Prepare","NpcMotion.Step","NpcMotion.NeedsPlayerMotion","NpcMotion.StructuredModel","NpcMotion.CurrentPlayerPremise","NpcHealth.Step","AttackIntercept.Solve","HostMeleeAttack.SolvePrepared","HostMeleeAttack.KnownSpearFamily","HostMeleeAttack.SpearDerived","PredictionTerrain.get_Unchanged","HostAttackObstacles.get_Unchanged","HostProjectileEnvironment.Capture","PlayerMotionContinuation.Advance"}.Contains(t.Name+"."+m.Name))h.Patch(m,prefix:new HarmonyMethod(typeof(ContinuousCombatMeasure).GetMethod("Count",F)),postfix:new HarmonyMethod(typeof(ContinuousCombatMeasure).GetMethod("CountEnd",F)));}
    static int Main(string[] a)
    {
        try {
            if(a.Length!=8)throw new ArgumentException("repository fixedProbe payload output weapon off|path|aim still|move idle|use");
            string repo=Path.GetFullPath(a[0]);payload=Path.GetFullPath(a[2]);output=Path.GetFullPath(a[3]);weapon=int.Parse(a[4]);mode=a[5];moving=a[6]=="move";use=a[7]=="use";
            frames=int.Parse(Environment.GetEnvironmentVariable("JMR_MEASURE_FRAMES")??"360");Directory.CreateDirectory(output);
            AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{string n=new AssemblyName(e.Name).Name;string p=Path.Combine(payload,n+".dll");return (n.StartsWith("JueMingR.")||n=="0Harmony")&&File.Exists(p)?Assembly.LoadFrom(p):null;};
            Assembly.LoadFrom(Path.Combine(payload,"0Harmony.dll"));foreach(string n in new[]{"JueMingR.Platform","JueMingR.Features","JueMingR.Infrastructure","JueMingR.TerrariaHost"}){
                var ass=Assembly.LoadFrom(Path.Combine(payload,n+".dll"));var d=(DebuggableAttribute)Attribute.GetCustomAttribute(ass,typeof(DebuggableAttribute));if(d!=null&&d.IsJITOptimizerDisabled)throw new Exception("Unoptimized candidate");
                Console.WriteLine("LOAD "+n+" source="+((AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(ass,typeof(AssemblyInformationalVersionAttribute))).InformationalVersion+" MVID="+ass.ManifestModule.ModuleVersionId+" SHA256="+Hash(ass.Location)+" location="+ass.Location);
            }
            probe=Assembly.LoadFrom(Path.GetFullPath(a[1]));var program=probe.GetType("NativeWorldTextProbe.Program",true);program.GetProperty("Repository",F).SetValue(null,repo,null);program.GetProperty("ProductionConfiguration",F).SetValue(null,"Release",null);program.GetField("references",F).SetValue(null,Path.Combine(repo,"external","TerrariaRefs"));
            AppDomain.CurrentDomain.AssemblyResolve+=(ResolveEventHandler)Delegate.CreateDelegate(typeof(ResolveEventHandler),program.GetMethod("Resolve",F));
            var game=Assembly.LoadFrom(Path.Combine(repo,"external","TerrariaRefs","Terraria.exe"));program.GetField("game",F).SetValue(null,game);game.GetType("Terraria.Program",true).GetField("SavePath",F).SetValue(null,Path.Combine(output,"u"));
            Console.WriteLine("GAME MVID="+game.ManifestModule.ModuleVersionId+" SHA256="+Hash(game.Location));Environment.SetEnvironmentVariable("JUEMINGR_AIM_LIGHT_OFF","1");AppDomain.MonitoringIsEnabled=true;
            Invoke("NativeQuickItemChecks","Run",(Action<object>)Exercise,false,false,false,true,true,Path.Combine(payload,"JueMingR.TerrariaHost.dll"),null,null,false);return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    [MethodImpl(MethodImplOptions.NoInlining)] static void Exercise(object context)
    {
        Invoke("NativeCombatAimChecks","Initialize");var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var input=Get(context,"Input");var attack=Get(combat,"Attack");
        if(Environment.GetEnvironmentVariable("JMR_MEASURE_BASIC")=="1"){Invoke("NativeCombatBasicAimChecks","Run",context);return;}
        var h=new Harmony("JueMingR.Measure.NativeOutlets");foreach(string n in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})h.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(n,F),prefix:new HarmonyMethod(typeof(ContinuousCombatMeasure).GetMethod("Skip",F)));
        if(Environment.GetEnvironmentVariable("JMR_MEASURE_COUNT")=="1")InstallCounts(h);
        try{
            Invoke("NativeCombatObservationChecks","Save",host,new ObservationOptions());Invoke("NativeCombatCadenceChecks","Save",combat,new CombatOptions());
            var p=(Player)Invoke("NativeToolExecutionChecks","Reset",context,Get(context,"Tools"),input,weapon,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.ResetFallDamage();p.releaseUseItem=true;p.statMana=p.statManaMax=p.statManaMax2=200;p.inventory[54].SetDefaults(3104);p.inventory[54].stack=1;GameMain.screenPosition=new Vector2(480,400);GameMain.rand=new Terraria.Utilities.UnifiedRandom(7193);
            var target=(NPC)Invoke("NativeCombatControlPreviewChecks","Dummy",2,55,40);Invoke("NativeCombatObservationChecks","Save",host,new ObservationOptions(false,mode!="off",true,false,true,50,true,mode=="aim"));
            bool weather=Environment.GetEnvironmentVariable("JMR_MEASURE_WEATHER")=="1";int windChanges=0;var weatherElapsed=TimeSpan.FromSeconds(1.0/60);var weatherTotal=TimeSpan.Zero;
            if(weather){GameMain.windSpeedCurrent=.05f;GameMain.windSpeedTarget=.65f;}
            int warm=use&&Environment.GetEnvironmentVariable("JMR_MEASURE_COLD")!="1"?240:60;int exitStart=use?frames-180:frames;
            int preview=0,path=0,live=0,moved=0;float minX=p.position.X,maxX=p.position.X;var samples=new double[frames,12];
            for(int i=-warm;i<frames;i++){
                bool held=use && i<exitStart && (i>=0 || warm>60) && (i+warm)%75<60;
                long start=Stopwatch.GetTimestamp();Invoke("NativeToolExecutionChecks","Sample",context,input,new Vector2(560,580),held);Call(combat,"Sample");Call(host,"SampleMouse");
                if(weather){float previousWind=GameMain.windSpeedCurrent;weatherTotal+=weatherElapsed;GameMain.instance.UpdateWeather(new GameTime(weatherTotal,weatherElapsed),0);if(i>=0 && previousWind!=GameMain.windSpeedCurrent)windChanges++;}
                if(moving){bool right=((i+warm)/36)%2==0;PlayerInput.Triggers.Current.Right=right;PlayerInput.Triggers.Current.Left=!right;GameMain.oldKeyState=GameMain.keyState;GameMain.keyState=new KeyboardState(right?Keys.D:Keys.A);}
                Invoke("NativeQuickItemChecks","BeginWorldStep");float x=p.position.X;p.Update(0);for(int n=0;n<GameMain.npc.Length;n++)if(GameMain.npc[n].active)GameMain.npc[n].UpdateNPC(n);
                for(int q=0;q<GameMain.maxProjectiles;q++)if(GameMain.projectile[q].active){GameMain.ProjectileUpdateLoopIndex=q;GameMain.projectile[q].Update(q);}GameMain.ProjectileUpdateLoopIndex=-1;
                long completed=Stopwatch.GetTimestamp();Call(context,"UpdateRuntime");Call(context,"UpdateShell");long end=Stopwatch.GetTimestamp();
                if(i==-1){GC.Collect();GC.WaitForPendingFinalizers();allocated=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;for(int g=0;g<3;g++)gc[g]=GC.CollectionCount(g);counts.Clear();ticks.Clear();}
                if(i>=0){bool pv=Get(attack,"ExpectedPreview")!=null;bool pa=Call(Get(Get(host,"Prediction"),"Cache"),"Read",0)!=null;int active=0;for(int q=0;q<GameMain.maxProjectiles;q++)if(GameMain.projectile[q].active)active++;if(pv)preview++;if(pa)path++;if(active>0)live++;if(x!=p.position.X)moved++;minX=Math.Min(minX,p.position.X);maxX=Math.Max(maxX,p.position.X);samples[i,0]=i;samples[i,1]=(end-start)*1000.0/Stopwatch.Frequency;samples[i,2]=(end-completed)*1000.0/Stopwatch.Frequency;samples[i,3]=p.position.X;samples[i,4]=p.position.Y;samples[i,5]=p.velocity.X;samples[i,6]=p.velocity.Y;samples[i,7]=active;samples[i,8]=pv?1:0;samples[i,9]=pa?1:0;samples[i,10]=GameMain.GameUpdateCount;samples[i,11]=use?(i>=exitStart?3:held?1:2):0;}
            }
            long alloc=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-allocated;int[] gcEnd=Enumerable.Range(0,3).Select(g=>GC.CollectionCount(g)-gc[g]).ToArray();var times=Enumerable.Range(0,frames).Select(i=>samples[i,1]).OrderBy(v=>v).ToArray();var rows=new List<string>{"frame,totalMs,completedMs,x,y,vx,vy,live,preview,path,tick,phase"};for(int i=0;i<frames;i++)rows.Add(string.Join(",",Enumerable.Range(0,12).Select(j=>samples[i,j].ToString("R",System.Globalization.CultureInfo.InvariantCulture))));
            Console.WriteLine("MEASURE weapon="+weapon+" mode="+mode+" moving="+moving+" use="+use+" warm="+warm+" n="+frames+" avg="+times.Average()+" p95="+times[(int)(frames*.95)-1]+" p99="+times[(int)(frames*.99)-1]+" max="+times.Last()+" allocated="+alloc+" gc="+string.Join("/",gcEnd)+" preview="+preview+" path="+path+" live="+live+" moved="+moved+" xRange="+minX+".."+maxX+" targetActive="+target.active+" exitLive="+samples[frames-1,7]);
            if(use)foreach(bool exiting in new[]{false,true}){var segment=Enumerable.Range(0,frames).Where(i=>(i>=exitStart)==exiting).Select(i=>samples[i,1]).OrderBy(v=>v).ToArray();Console.WriteLine("SEGMENT "+(exiting?"exit":"attack-cycles")+" n="+segment.Length+" mean="+segment.Average()+" p95="+segment[(int)(segment.Length*.95)-1]+" p99="+segment[(int)(segment.Length*.99)-1]+" max="+segment.Last());}
            Console.WriteLine("ENVIRONMENT naturalUpdateWeather="+weather+" windChanges="+windChanges+" windFinal="+GameMain.windSpeedCurrent+" target="+GameMain.windSpeedTarget+" (selected native stages, not full Main.DoUpdate)");
            File.WriteAllLines(Path.Combine(output,"frames.csv"),rows);if(moving&&moved<frames*.8)throw new Exception("Player did not continuously move");if(!target.active)throw new Exception("Natural placed dummy retired");
            foreach(var row in counts.OrderByDescending(r=>ticks[r.Key]))Console.WriteLine("COUNT "+row.Key+" n="+row.Value+" inclusiveMs="+Ms(ticks[row.Key])+" (nested, do not sum; diagnostic run only)");
        }finally{h.UnpatchAll(h.Id);Invoke("NativeCombatObservationChecks","Save",host,new ObservationOptions());Invoke("NativeCombatCadenceChecks","Save",combat,new CombatOptions());}
    }
    static long allocated;static int[] gc=new int[3];static string Ms(long v){return (v*1000.0/Stopwatch.Frequency).ToString("R",System.Globalization.CultureInfo.InvariantCulture);}
}
