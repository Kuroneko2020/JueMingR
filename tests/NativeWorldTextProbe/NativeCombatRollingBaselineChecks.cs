using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.DataStructures;
using Terraria.Utilities;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using static NativeWorldTextProbe.NativeCombatAttackMechanismChecks;

namespace NativeWorldTextProbe
{
    // A bounded baseline on the original Host seam. Timers are inclusive and
    // overlap: they must not be added to one another or subtracted from Draw.
    // Normal NPC life/immunity is never extended to keep a window alive.
    internal static partial class NativeCombatRollingBaselineChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static readonly string[] Names={"selection","source","capture","terrain","publish","prepare","draw","sharedHook","runtime","instrumentedShell"};
        private static readonly Dictionary<MethodBase,int> Methods=new Dictionary<MethodBase,int>();
        private static readonly double[] Times=new double[10];
        private static readonly long[] Bytes=new long[10];
        private static readonly int[] Calls=new int[10];
        private static readonly List<string> Hits=new List<string>();
        private static readonly List<string> Births=new List<string>();
        private static void Birth(IEntitySource __0,int __result)
        {
            if(__result<0 || __result>=Main.maxProjectiles)return;
            var p=Main.projectile[__result];var n=(__0 as EntitySource_Parent)?.Entity as NPC;
            if(light){if(p.type==55 && Matches(bActor,n))lightHornetBirths++;return;}
            Births.Add(Csv(phase,frame,Main.GameUpdateCount,__0.GetType().Name,p.type,__result,(uint)p.key,n?.whoAmI,n?.type,n?.ai[1],ReferenceEquals(n,Main.npc[bSlot]),Main.LocalPlayer.HeldItem.type));
        }
        private static Projectile damageSource;
        private static Func<long> allocated;
        private static int frame; private static string phase; private static int aSlot,bSlot;
        private static NpcIdentity aActor,bActor;
        internal static string Phase=>phase;
        internal static void QualityFrame(string scene,int index,int slot){phase=scene;frame=index;bSlot=slot;aSlot=-1;aActor=default(NpcIdentity);bActor=Actor(Main.npc[slot]);}
        private static Exception ProjectileFault(Projectile __instance,Exception __exception)
        {if(__exception!=null)Console.WriteLine("ORIGINAL-PROJECTILE-FAULT phase="+phase+" frame="+frame+" type="+__instance.type+" owner="+__instance.owner+" pos="+__instance.position+" ai="+string.Join("/",__instance.ai)+" error="+__exception);return __exception;}
        private static void FirstChance(object sender,System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {if(e.Exception is NullReferenceException)Console.WriteLine("BASELINE-FIRST-CHANCE phase="+phase+" frame="+frame+" "+new StackTrace(e.Exception,true));}
        private struct Stamp {internal long Time,Bytes;internal int G0,G1,G2;}
        private static readonly int[] sourceGc=new int[3];
        private static void Before(MethodBase __originalMethod,out Stamp __state)
        {int index=Methods[__originalMethod];__state=new Stamp{Time=Stopwatch.GetTimestamp(),Bytes=allocated==null?0:allocated()};Calls[index]++;if(index==1){__state.G0=GC.CollectionCount(0);__state.G1=GC.CollectionCount(1);__state.G2=GC.CollectionCount(2);}}
        private static void After(MethodBase __originalMethod,Stamp __state)
        {int index=Methods[__originalMethod];Times[index]+=Ms(Stopwatch.GetTimestamp()-__state.Time);if(allocated!=null)Bytes[index]+=allocated()-__state.Bytes;if(index==1){sourceGc[0]+=GC.CollectionCount(0)-__state.G0;sourceGc[1]+=GC.CollectionCount(1)-__state.G1;sourceGc[2]+=GC.CollectionCount(2)-__state.G2;}}
        private static void Damage(Projectile __instance,out Projectile __state){__state=damageSource;damageSource=__instance;}
        private static void DamageEnd(Projectile __state){damageSource=__state;}
        private static void Hit(NPC __instance,int __result)
        {
            if(light && __result>0){LightHit(__instance,__result);return;}
            if(__result>0)Hits.Add(Csv(phase,frame,Main.GameUpdateCount,damageSource?.type,__instance.whoAmI,__instance.type,__result,__instance.life,
                Matches(aActor,__instance)?"A":Matches(bActor,__instance)?"B":"other",__instance.generation,Token(__instance),__instance.netID));
        }
        internal static void Run(object context,NpcPredictionCache cache,Action step,string output,ProbeGraphics graphics)
        {
            bool cpuOnly=Environment.GetEnvironmentVariable("JUEMINGR_ROLLING_CPU_ONLY")=="1" && Environment.GetEnvironmentVariable("JUEMINGR_NPC_LIVE_CONTEXT")=="rolling-candidate";
            if(graphics==null && !cpuOnly)throw new InvalidOperationException("Baseline requires actual XNA Draw, no zero substitute.");
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var native=Get(source,"Native");var selection=Get(host,"Selection");var layer=Get(host,"World");
            bool candidate=Environment.GetEnvironmentVariable("JUEMINGR_NPC_LIVE_CONTEXT")=="rolling-candidate";
            light=candidate && Environment.GetEnvironmentVariable("JUEMINGR_ROLLING_LIGHT")=="1";
            if(light)LightInitialize(context,host,selection,layer);
            var runtime=new List<string>{"host="+host.GetType().Assembly.Location,"hostMvid="+host.GetType().Assembly.ManifestModule.ModuleVersionId,"hostVersion="+host.GetType().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion,"clockFrequency="+Stopwatch.Frequency,"processorCount="+Environment.ProcessorCount,"os="+Environment.OSVersion,"allocation=GetAllocatedBytesForCurrentThread or NA","timers=inclusive; Draw excludes screenshot/readback; step includes paced wait"};
            File.WriteAllLines(Path.Combine(output,"rolling-identity.txt"),runtime);
            Methods.Clear();Hits.Clear();Hits.Add("phase,frame,tick,projectileType,victimSlot,victimType,damage,life,role,victimGeneration,victimToken,victimNetId");
            Births.Clear();Births.Add("phase,frame,tick,sourceType,projectileType,slot,key,npcSlot,npcType,npcAi1,intendedBSource,heldItem");
            var allocation=typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",BindingFlags.Public|BindingFlags.Static);
            allocated=allocation==null?null:(Func<long>)Delegate.CreateDelegate(typeof(Func<long>),allocation);
            var rows=new List<string>{"phase,frame,tick,legal,selectedB,identityOk,published,count,capture,strokes,pathStrokes,text,life,playerLife,playerDead,activeNPC,activeProjectile,hitCount,requests,received,hostMs,shellMs,renderMs,stepWallMs,hostBytes,drawBytes,gc0,gc1,gc2,intendedBEligible,actualSlot,sceneRestart,cacheRequired,nativeFailed,nativeReason,workerState,readyMs,npcAi0,npcAi1,npcAi2,npcAi3,hostPathFailed,"+string.Join(",",Names.SelectMany(n=>new[]{n+"Ms",n+"Calls"}))};
            rows[0]+=",sourceBytes,sourceGc0,sourceGc1,sourceGc2";
            var errors=new List<string>();var summary=new List<string>{"phase,executed,legal,selected,published,longestBlank,firstShown,future60,future120,naturalEnd,unexecuted,firstLegal,responseUpdates"};
            var actors=new List<string>{"phase,frame,tick,aToken,aSlot,aGeneration,aType,aNetId,aMatches,aActive,aLife,bToken,bSlot,bGeneration,bType,bNetId,bMatches,bActive,bLife,selectedToken,selectedSlot,selectedGeneration,selectedType,selectedNetId,selectedB,published,pathTick,pathCount,worldLayerText,pathStrokes,sourceCalls,publishCalls"};
            NpcTrajectory fixedPath=null;Vector2 fixedCamera=Vector2.Zero;
            var fixedCosts=new List<string>{"repetition,frame,count,strokes,text,prepareMs,layerDrawMs,renderWrapperMs,prepareBytes,drawBytes"};
            var helperCosts=new List<string>{"phase,cpuMs,executed"};
            bool evidenceComplete=false,requireHornetBirth=false;int sceneCount=0;
            using(var timing=new NativeCombatRollingTimingChecks(source,output))
            using(var quality=candidate && Environment.GetEnvironmentVariable("JUEMINGR_ROLLING_QUALITY")=="1"?new NativeCombatRollingQualityChecks(source,output):null)
            using(var observer=new HarmonyScope())
            {
                if(!light)Watch(observer.Harmony,selection.GetType(),"Update",0);Watch(observer.Harmony,source.GetType(),"Prepare",1);
                if(native!=null){Watch(observer.Harmony,native.GetType(),"Capture",2);Watch(observer.Harmony,host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeTerrainSnapshot"),"Capture",3);}
                if(!light)Watch(observer.Harmony,cache.GetType(),"Publish",4);Watch(observer.Harmony,layer.GetType(),"Prepare",5);Watch(observer.Harmony,layer.GetType(),"Draw",6);
                Watch(observer.Harmony,context.GetType(),"UpdateRuntime",8);Watch(observer.Harmony,context.GetType(),"UpdateShell",9);
                var hooks=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.CombatGeometryHooks");
                if(!light)foreach(var method in hooks.GetMethods(Flags).Where(m=>m.IsStatic && m.IsPrivate && (m.Name.StartsWith("Before",StringComparison.Ordinal)||m.Name.StartsWith("End",StringComparison.Ordinal)||m.Name=="PlayerHurt"||m.Name=="NpcReset")))Watch(observer.Harmony,method,7);
                observer.Harmony.Patch(typeof(Projectile).GetMethod("Damage_PVE_Inner",Flags),prefix:Hook(nameof(Damage)),finalizer:Hook(nameof(DamageEnd)));
                observer.Harmony.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags),postfix:Hook(nameof(Hit)));
                observer.Harmony.Patch(typeof(Projectile).GetMethod("Update",Flags),finalizer:Hook(nameof(ProjectileFault)));
                observer.Harmony.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters()[1].ParameterType==typeof(float)),postfix:Hook(nameof(Birth)));
                try
                {
                    if(Environment.GetEnvironmentVariable("JUEMINGR_ROLLING_DIAGNOSE")=="1")AppDomain.CurrentDomain.FirstChanceException+=FirstChance;
                    NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.hardMode=true;
                    Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);
                    // Main normally creates this concrete original recorder;
                    // the isolated container must retain normal fatal loot.
                    Main.BestiaryTracker=new Terraria.GameContent.Bestiary.BestiaryUnlocksTracker();
                    Main.BestiaryDB=new Terraria.GameContent.Bestiary.BestiaryDatabase();
                    var bestiary=new Terraria.GameContent.Bestiary.BestiaryDatabaseNPCsPopulator();bestiary.Populate(Main.BestiaryDB);
                    ContentSamples.RebuildBestiarySortingIDsByBestiaryDatabaseContents(Main.BestiaryDB);
                    Main.BestiaryDB.Merge(Main.ItemDropsDB);bestiary.AddDropOverrides(Main.BestiaryDB);
                    Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};
                    var player=Main.LocalPlayer;player.position=new Vector2(1100,2400-player.height);player.fallStart=player.fallStart2=(int)(player.position.Y/16);player.statLife=player.statLifeMax=player.statLifeMax2=500;
                    for(int i=0;i<3;i++)player.armor[i].SetDefaults(696+i);
                    typeof(Main).GetField("_rngs",Flags).SetValue(null,new Dictionary<string,UnifiedRandom>{{"UpdatePlayers",new UnifiedRandom(531)},{"UpdateNPCs",new UnifiedRandom(879)},{"UpdateProjectiles",new UnifiedRandom(171)}});
                    Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();
                    // Warm the original environment through real idle updates.
                    // This startup axis is separate from normal combat costs;
                    // r1/r2 preserve the cold-start interruption evidence.
                    phase="startup-idle";NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,radius:0));
                    if(candidate && Environment.GetEnvironmentVariable("JUEMINGR_ROLLING_BOUNDARIES")=="1")
                        NativeCombatRollingBoundaryChecks.Run(context,host,cache,step,output);
                    var startup=Stopwatch.StartNew();int startupUpdates=0;
                    while(!candidate && startup.Elapsed.TotalSeconds<70)
                    {
                        NativeCombatModeledImpactChecks.SampleMouse(context,new Vector2(5000,1000));step();startupUpdates++;
                        var owner=Get(native,"Worker");if(owner!=null && (double)Get(owner,"ReadyMilliseconds")>0)break;
                    }
                    File.WriteAllText(Path.Combine(output,"rolling-startup.txt"),"idle real updates="+startupUpdates+" elapsedMs="+startup.Elapsed.TotalMilliseconds.ToString("R",CultureInfo.InvariantCulture));
                    var readyOwner=candidate?null:Get(native,"Worker");if(!candidate && (readyOwner==null || (double)Get(readyOwner,"ReadyMilliseconds")==0))throw new InvalidOperationException("Original environment did not become Ready in bounded idle preparation.");
                    aSlot=Spawn(NPCID.GiantTortoise,1400);bSlot=Spawn(NPCID.Derpling,1900);aActor=Actor(Main.npc[aSlot]);bActor=Actor(Main.npc[bSlot]);
                    string[] phases={"off","idle","stable","guardian-A-select-B","gun-A-select-B","gun-B","hornet-open","hornet-wall","dense","guardian-selected-tortoise","off-final"};
                    int[] durations={60,60,180,180,180,180,360,180,120,180,60};
                    if(light){phases=new[]{"off","idle","stable","guardian-A-select-B","gun-A-select-B","gun-B","hornet-open","hornet-wall","dense","guardian-selected-tortoise","off-final","switch-return","broom-dense","player-water","reopen-off","reopen"};durations=new[]{120,120,360,360,360,360,720,360,240,360,120,180,240,240,60,180};}
                    for(int axis=0;axis<phases.Length;axis++)
                    {
                        phase=phases[axis];bool enabled=phase!="off" && phase!="off-final" && phase!="reopen-off",idle=axis==1,selectedTortoise=phase=="guardian-selected-tortoise";
                        string filter=Environment.GetEnvironmentVariable("JUEMINGR_ROLLING_PHASES");
                        if(!string.IsNullOrWhiteSpace(filter) && !filter.Split(',').Select(value=>value.Trim()).Contains(phase))continue;
                        sceneCount++;
                        if(light && (phase=="hornet-open" || phase=="hornet-wall" || phase=="broom-dense"))requireHornetBirth=true;
                        quality?.ChangeScene(phase,axis==7);
                        bool restart=player.dead || !Main.npc[bSlot].active;
                        if(light && !Matches(bActor,Main.npc[bSlot]))restart=true;
                        if(player.dead || restart && (axis<6 || light))
                        {
                            // A naturally ended encounter remains in its previous
                            // denominator. The next phase starts a new normal-life
                            // encounter, never heals/resurrects the previous actor.
                            Main.player[Main.myPlayer]=player=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};
                            player.position=new Vector2(1100,2400-player.height);player.fallStart=player.fallStart2=(int)(player.position.Y/16);player.statLife=player.statLifeMax=player.statLifeMax2=500;
                            if(axis<6 || light){bSlot=Spawn(NPCID.Derpling,1900);bActor=Actor(Main.npc[bSlot]);}
                        }
                        if(axis==3 || axis==4 || light && phase=="switch-return")
                        {
                            // B can reuse a naturally dead A's slot. A live slot
                            // alone must never establish independent identity.
                            if(aSlot<0 || !Matches(aActor,Main.npc[aSlot]) || !Main.npc[aSlot].active){aSlot=Spawn(NPCID.GiantTortoise,1400);aActor=Actor(Main.npc[aSlot]);}
                            AssertIndependentActors(Main.npc[aSlot],Main.npc[bSlot]);
                        }
                        if(axis==5){NPC.ClearAll();Projectile.ClearAll();aSlot=-1;aActor=default(NpcIdentity);bSlot=Spawn(NPCID.Derpling,1400);bActor=Actor(Main.npc[bSlot]);restart=true;}
                        if(axis==6){NPC.ClearAll();Projectile.ClearAll();aSlot=-1;aActor=default(NpcIdentity);bSlot=Spawn(NPCID.MossHornet,1500);bActor=Actor(Main.npc[bSlot]);Main.npc[bSlot].position=new Vector2(1500,2150);}
                        if(axis==7)for(int y=132;y<150;y++){Main.tile[82,y].active(true);Main.tile[82,y].type=1;}
                        if(axis==8)for(int i=0;i<16;i++)Spawn(NPCID.BlueSlime,2800+i*24);
                        if(selectedTortoise){NPC.ClearAll();Projectile.ClearAll();aSlot=-1;aActor=default(NpcIdentity);bSlot=Spawn(NPCID.GiantTortoise,1400);bActor=Actor(Main.npc[bSlot]);restart=true;}
                        if(light)LightScene(phase,player);
                        for(int i=0;i<3;i++)player.armor[i].SetDefaults(axis==3 || selectedTortoise?3381+i:696+i);
                        player.inventory[0].TurnToAir();player.controlUseItem=false;
                        if(axis==4 || axis==5){player.inventory[0].SetDefaults(ItemID.Minishark);player.inventory[54].SetDefaults(ItemID.MusketBall);player.inventory[54].stack=999;player.selectedItemState.Select(0);player.selectedItemState.Update();player.releaseUseItem=true;}
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(path:enabled,collision:enabled,mouseCenter:true,clearLine:false,radius:idle?0:25));
                        int legal=0,selected=0,shown=0,blank=0,longest=0,first=-1,firstLegal=-1,f60=0,f120=0,executed=0,phaseHitStart=light?lightHitCount:Hits.Count;bool naturalEnd=false;
                        var child=readyOwner==null?null:(Process)Get(readyOwner,"child");double helperBefore=child==null?0:child.TotalProcessorTime.TotalMilliseconds;
                        for(frame=0;frame<durations[axis];frame++)
                        {
                            var expected=light && phase=="switch-return" && frame>=60 && frame<120?aActor:bActor;
                            var target=Main.npc[expected.Slot];
                            if(light)player.controlUp=phase=="broom-dense" && frame<60;
                            if(!Matches(expected,target))throw new InvalidOperationException("Intended B instance changed during encounter.");
                            player.controlUseItem=axis==4 || axis==5;
                            bool negativeSelection=Environment.GetEnvironmentVariable("JUEMINGR_ROLLING_SELECTION_NEGATIVE")==phase;
                            if(light)LightMouse(idle || negativeSelection?new Vector2(5000,1000):target.Center);else NativeCombatModeledImpactChecks.SampleMouse(context,idle || negativeSelection?new Vector2(5000,1000):target.Center);
                            // The physical mouse is also the original weapon's
                            // aim input; Host selection alone cannot prove fire.
                            var aim=axis==4 && Main.npc[aSlot].active?Main.npc[aSlot].Center:target.Center;
                            Main.mouseX=(int)(aim.X-Main.screenPosition.X);Main.mouseY=(int)(aim.Y-Main.screenPosition.Y);
                            Array.Clear(Times,0,Times.Length);Array.Clear(Bytes,0,Bytes.Length);Array.Clear(Calls,0,Calls.Length);
                            Array.Clear(sourceGc,0,sourceGc.Length);
                            long bytes=allocated==null?0:allocated();int g0=GC.CollectionCount(0),g1=GC.CollectionCount(1),g2=GC.CollectionCount(2),hitBefore=light?lightHitCount:Hits.Count;
                            timing.Begin();long begin=Stopwatch.GetTimestamp();step();double wall=Ms(Stopwatch.GetTimestamp()-begin);timing.End(phase,frame);
                            bool eligible=enabled && !idle && target.active && target.life>0 && !target.dontTakeDamage && !target.friendly && !target.immortal && !player.dead;
                            var key=light?lightTarget():(NpcIdentity)Get(selection,"Target");bool valid=light?lightHasTarget():(bool)Get(selection,"HasTarget");bool picked=valid && Matches(expected,key);
                            var path=cache.Read(0);bool identity=path==null || valid && path.Identity.Equals(key) && path.SampleTick==Main.GameUpdateCount;
                            if(!identity)throw new InvalidOperationException("Baseline consumer has stale/wrong identity.");
                            // CPU telemetry must consume the current projection after Update/Prepare; it is not a GPU Draw.
                            if(graphics==null)NativeCombatPresentationChecks.Project(layer);
                            // GPU counts are read after the actual Draw which projects and consumes events.
                            bytes=allocated==null?0:allocated();begin=Stopwatch.GetTimestamp();if(graphics!=null)graphics.Render(light?lightDraw:()=>Call(layer,"Draw"),Main.GameViewMatrix.ZoomMatrix);double drawMs=graphics==null?double.NaN:Ms(Stopwatch.GetTimestamp()-begin);long drawBytes=allocated==null?0:allocated()-bytes;
                            bool published=path!=null;int strokes=light?lightStrokes():(int)Get(layer,"StrokeCount"),events=light?lightEvents():(int)Get(layer,"eventEnd");
                            var a=aSlot<0?null:Main.npc[aSlot];
                            if(!light)actors.Add(Csv(phase,frame,Main.GameUpdateCount,Token(aActor.Token),aActor.Slot,aActor.Generation,aActor.Type,aActor.NetId,Matches(aActor,a),a?.active,a?.life,
                                Token(bActor.Token),bActor.Slot,bActor.Generation,bActor.Type,bActor.NetId,Matches(bActor,target),target.active,target.life,Token(key.Token),key.Slot,key.Generation,key.Type,key.NetId,picked,published,path?.CaptureTick??-1,path?.Count??0,Get(layer,"pathText")!=null,strokes-events,Calls[1],Calls[4]));
                            if(fixedPath==null && picked && path!=null && path.Count==121){fixedPath=path;fixedCamera=Main.screenPosition;}
                            executed++;if(eligible)
                            {
                                legal++;if(firstLegal<0)firstLegal=frame;if(picked)selected++;
                                if(picked && published){shown++;blank=0;if(first<0)first=frame;if(path.Count>=61)f60++;if(path.Count>=121)f120++;}
                                else longest=Math.Max(longest,++blank);
                            }
                            else blank=0;
                            if(light)LightRecord(target,expected,key,eligible,picked,path,strokes-events,lightText()!=null,wall,drawMs,GC.CollectionCount(0)-g0,GC.CollectionCount(1)-g1,GC.CollectionCount(2)-g2,lightHitCount-hitBefore);
                            else
                            {
                            string costs=string.Join(",",Enumerable.Range(0,Names.Length).SelectMany(i=>new[]{Times[i].ToString("R",CultureInfo.InvariantCulture),Calls[i].ToString(CultureInfo.InvariantCulture)}));
                            var worker=native==null?null:Get(native,"Worker");
                            rows.Add(Csv(phase,frame,Main.GameUpdateCount,valid,picked,identity,published,path?.Count??0,path?.CaptureTick??-1,strokes,strokes-events,Get(layer,"pathText")!=null,target.life,player.statLife,player.dead,Main.npc.Count(n=>n.active),Main.projectile.Count(p=>p.active),Hits.Count-hitBefore,native==null?0:Get(native,"Requests"),NativeCombatProductionPredictionChecks.LastReceived,Times[8],Times[9],drawMs,wall,allocated==null?(object)"NA":Bytes[8],allocated==null?(object)"NA":drawBytes,GC.CollectionCount(0)-g0,GC.CollectionCount(1)-g1,GC.CollectionCount(2)-g2,eligible,valid?key.Slot:-1,restart,cache.Required,native==null?false:Get(native,"Failed"),native==null?"rolling":Get(native,"Reason"),worker==null?-1:Get(worker,"State"),worker==null?0:Get(worker,"ReadyMilliseconds"),target.ai[0],target.ai[1],target.ai[2],target.ai[3],Get(host,"pathFailed"))+","+costs);
                            rows[rows.Count-1]+=","+Bytes[1]+","+sourceGc[0]+","+sourceGc[1]+","+sourceGc[2];
                            }
                            quality?.Observe();if(picked)quality?.Capture(phase,frame,path);
                            if(!target.active || player.dead){naturalEnd=true;break;}
                        }
                        summary.Add(Csv(phase,executed,legal,selected,shown,longest,first,f60,f120,naturalEnd,durations[axis]-executed,firstLegal,first<0?-1:first-firstLegal+1));
                        helperCosts.Add(Csv(phase,child==null?0:child.TotalProcessorTime.TotalMilliseconds-helperBefore,executed));
                        Console.WriteLine("ROLLING "+phase+" legal="+legal+" selected="+selected+" shown="+shown+" longest="+longest+" hits="+(light?lightHitCount:Hits.Count-1)+" naturalEnd="+naturalEnd);
                        // Acquisition belongs to availability: an eligible B
                        // that is never selected must not erase the denominator.
                        if(enabled && !idle && legal>0 && (shown<legal*.95 || longest>3 || first<0 || first-firstLegal+1>3 || f60<shown*.95))errors.Add(phase+": acquisition/availability/length gate RED");
                        // Gear/phase labels cannot substitute an original
                        // Damage->Strike receipt on the required live instance.
                        if(axis==3 || axis==4 || axis==5 || selectedTortoise)
                        {
                            string role=axis==4?"A":"B",projectile=axis==4 || axis==5?"14":"623";
                            if(light?!LightRequiredHit(phaseHitStart,axis==4?"A":"B",axis==4 || axis==5?14:623):!Hits.Skip(phaseHitStart).Any(row=>{var fields=row.Replace("\"","").Split(',');return fields[3]==projectile && fields[8]==role;}))
                                errors.Add(phase+": required real victim hit missing");
                        }
                    }
                    if(sceneCount==0)throw new InvalidOperationException("Rolling phase filter selected no known scene; zero-world-update evidence is invalid.");
                    Console.WriteLine("ROLLING scenes="+sceneCount+" completed-updates="+(light?lightRowCount:rows.Count-1));
                    if(light){if(requireHornetBirth && lightHornetBirths==0)throw new InvalidOperationException("Selected hornet scenes require actual selected hornet projectile55 birth.");LightEnd(host,cache,step);evidenceComplete=true;}
                    else
                    {
                    // Presentation-only replay of an actual immutable 121-point
                    // result. No world update, Source or acceptance is faked;
                    // these rows never enter the continuous denominator.
                    if(quality!=null)NativeCombatRollingMechanismChecks.Run(context,host,cache,step,quality,output);
                    if(fixedPath==null)throw new InvalidOperationException("No real 121-point output for equal-output Draw baseline.");
                    string replay=Environment.GetEnvironmentVariable("JUEMINGR_ROLLING_FIXED_PATH");
                    if(candidate && replay!=null)
                    {
                        fixedPath=FrozenPresentation(replay,fixedPath.Identity);
                        // Center the frozen input, not this candidate's much
                        // earlier first publication. This retains all 128 real
                        // strokes instead of clipping the old path offscreen.
                        fixedCamera=new Vector2(fixedPath[0].Bounds.CenterX-Main.screenWidth/2,fixedPath[0].Bounds.CenterY-Main.screenHeight/2);
                    }
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true));Main.screenPosition=fixedCamera;cache.Demand(0,30,120);cache.Publish(fixedPath);
                    var fixedPoints=new List<string>{"offset,x,y,width,height,newSegment"};
                    for(int i=0;i<fixedPath.Count;i++){var p=fixedPath[i];fixedPoints.Add(Csv(p.TickOffset,p.Bounds.X,p.Bounds.Y,p.Bounds.Width,p.Bounds.Height,p.NewSegment));}
                    File.WriteAllLines(Path.Combine(output,"rolling-fixed-path.csv"),fixedPoints);
                    for(int repetition=0;graphics!=null && repetition<3;repetition++)for(int i=-30;i<120;i++)
                    {
                        Array.Clear(Times,0,Times.Length);Array.Clear(Bytes,0,Bytes.Length);Array.Clear(Calls,0,Calls.Length);
                        Call(layer,"Prepare");long begin=Stopwatch.GetTimestamp();graphics.Render(()=>Call(layer,"Draw"),Main.GameViewMatrix.ZoomMatrix);double render=Ms(Stopwatch.GetTimestamp()-begin);
                        if(replay!=null && ((int)Get(layer,"StrokeCount")!=128 || (string)Get(layer,"pathText")!="NPC 路径：近似 · 未来约 2.0 秒 · 随机代表路线 · 假设玩家延续当前输入"))throw new InvalidOperationException("Frozen presentation mismatch: strokes="+Get(layer,"StrokeCount")+" text="+Get(layer,"pathText")+" camera="+fixedCamera+" viewport="+Main.screenWidth+"x"+Main.screenHeight);
                        if(i>=0)fixedCosts.Add(Csv(repetition,i,fixedPath.Count,Get(layer,"StrokeCount"),Get(layer,"pathText")!=null,Times[5],Times[6],render,allocated==null?(object)"NA":Bytes[5],allocated==null?(object)"NA":Bytes[6]));
                    }
                    File.WriteAllText(Path.Combine(output,"rolling-fixed-identity.txt"),"source="+(replay??"current real output")+"\ncamera="+fixedCamera+"\ntext="+Get(layer,"pathText")+"\ncount="+fixedPath.Count+"\nstrokes="+Get(layer,"StrokeCount"));
                    evidenceComplete=true;
                    }
                }
                finally
                {if(light)LightWrite(output,evidenceComplete);AppDomain.CurrentDomain.FirstChanceException-=FirstChance;File.WriteAllLines(Path.Combine(output,"rolling-actors.csv"),actors);File.WriteAllLines(Path.Combine(output,"rolling-updates.csv"),rows);File.WriteAllLines(Path.Combine(output,"rolling-summary.csv"),summary);File.WriteAllLines(Path.Combine(output,"rolling-hits.csv"),Hits);File.WriteAllLines(Path.Combine(output,"rolling-births.csv"),Births);File.WriteAllLines(Path.Combine(output,"rolling-fixed-costs.csv"),fixedCosts);File.WriteAllLines(Path.Combine(output,"rolling-helper-costs.csv"),helperCosts);File.WriteAllLines(Path.Combine(output,"rolling-red.txt"),errors);File.WriteAllText(Path.Combine(output,"rolling-status.txt"),evidenceComplete?"completed baseline, not a candidate PASS":"tool interrupted; do not count as product RED");damageSource=null;}
            }
            if(Environment.GetEnvironmentVariable("JUEMINGR_ROLLING_EXPECT_SELECTION_REJECTION")=="1")
            {
                string negative=Environment.GetEnvironmentVariable("JUEMINGR_ROLLING_SELECTION_NEGATIVE");
                if(errors.Count!=1 || errors[0]!=negative+": acquisition/availability/length gate RED")throw new InvalidOperationException("The real selection-negative scene must be rejected by its availability gate alone.");
                Console.WriteLine("PASS availability gate rejected eligible-but-unselected "+negative+"; raw missing interval retained.");return;
            }
            if(errors.Count>0)throw new InvalidOperationException("Frozen f8 rolling contract RED: "+string.Join("; ",errors));
        }
        private static int Spawn(int type,int x)=>NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),x,2400,type,Start:16,Target:Main.myPlayer);
        private static NpcIdentity Actor(NPC n)=>new NpcIdentity(0,n,n.whoAmI,n.generation,n.type,n.netID);
        private static bool Matches(NpcIdentity actor,NPC n)=>n!=null && ReferenceEquals(actor.Token,n) && actor.Slot==n.whoAmI && actor.Generation==n.generation && actor.Type==n.type && actor.NetId==n.netID;
        private static bool Matches(NpcIdentity actor,NpcIdentity n)=>ReferenceEquals(actor.Token,n.Token) && actor.Slot==n.Slot && actor.Generation==n.Generation && actor.Type==n.Type && actor.NetId==n.NetId;
        private static int Token(object value)=>value==null?0:System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
        private static void AssertIndependentActors(NPC a,NPC b)
        {
            if(ReferenceEquals(a,b) || a.whoAmI==b.whoAmI)
                throw new InvalidOperationException("Independent A/B encounter aliased: A slot="+a.whoAmI+" generation="+a.generation+" type="+a.type+"; B slot="+b.whoAmI+" generation="+b.generation+" type="+b.type);
        }
        private static NpcTrajectory FrozenPresentation(string path,NpcIdentity identity)
        {
            // Presentation-only input frozen by the accepted f8 baseline. It
            // cannot enter Source, selection or a continuous quality ledger.
            byte[] bytes=File.ReadAllBytes(path);string hash;
            using(var sha=System.Security.Cryptography.SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");
            if(hash!="C82435C257D9B6B5FF6B62DB8398EE67C1A248677B6FC2088D14C864AC0DE5AF")throw new InvalidOperationException("Frozen Draw input changed.");
            string[] lines=File.ReadAllLines(path);if(lines.Length!=122)throw new InvalidOperationException("Frozen Draw point count.");
            var points=new NpcTrajectoryPoint[121];
            for(int i=0;i<points.Length;i++)
            {
                string[] fields=lines[i+1].Replace("\"","").Split(',');
                if(fields.Length!=6 || int.Parse(fields[0],CultureInfo.InvariantCulture)!=i)throw new InvalidOperationException("Frozen Draw point order.");
                var n=new NpcMotionState{X=float.Parse(fields[1],CultureInfo.InvariantCulture),Y=float.Parse(fields[2],CultureInfo.InvariantCulture),Width=int.Parse(fields[3],CultureInfo.InvariantCulture),Height=int.Parse(fields[4],CultureInfo.InvariantCulture),NewSegment=bool.Parse(fields[5])};
                points[i]=new NpcTrajectoryPoint(i,n);
            }
            // NativePredictionResult at f8 supplies these presentation flags;
            // retain the same text and dashed geometry for equal output cost.
            return new NpcTrajectory(identity,(long)Main.GameUpdateCount,1,PredictionAssumption.NoNewHits|PredictionAssumption.RandomRepresentative|PredictionAssumption.LocalTerrain|PredictionAssumption.HeldPlayerControls,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static void Watch(Harmony h,Type type,string name,int index){foreach(var m in type.GetMethods(Flags).Where(m=>m.Name==name))Watch(h,m,index);}
        private static void Watch(Harmony h,MethodInfo method,int index){Methods[method]=index;h.Patch(method,prefix:Hook(nameof(Before)),postfix:Hook(nameof(After)));}
        private static HarmonyMethod Hook(string name)=>new HarmonyMethod(typeof(NativeCombatRollingBaselineChecks).GetMethod(name,Flags));
        private static void Call(object value,string name)=>value.GetType().GetMethod(name,Flags).Invoke(value,null);
        private static double Ms(long ticks)=>ticks*1000.0/Stopwatch.Frequency;
        private sealed class HarmonyScope:IDisposable{internal readonly Harmony Harmony=new Harmony("JueMingR.Tests.RollingBaseline");public void Dispose(){Harmony.UnpatchAll(Harmony.Id);}}
    }
}
