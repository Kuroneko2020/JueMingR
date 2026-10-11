using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // AI8 motor-only oracles. Actual native attacks/RNG still execute once in
    // the original action. Unobserved teleport choices are never manufactured.
    internal static class NativeCombatCasterChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly hostAssembly;
        private static Action<NPC> before,after;
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static Type Model{get{return typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcCasterMotion",true);}}
        private static bool Motor(ref NpcMotionState state,PredictionEnvironment e,NpcMotionState[] group,out PredictionStop stop)
        {object[] args={state,e,null,1,PredictionStop.None,group,group.Length,false};bool ok=(bool)Model.GetMethod("Step",Flags).Invoke(null,args);state=(NpcMotionState)args[0];stop=(PredictionStop)args[4];return ok;}
        internal static void Boundaries()
        {
            var n=new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,24,24),Style=8,Active=true,A0=600,Vx=.105f,Width=21,Height=41,CasterRelationsCaptured=true,CasterRelationsComplete=true};
            PredictionStop stop;var server=new PredictionEnvironment();
            Require(Motor(ref n,server,new[]{n},out stop) && n.Vx==0,"Native strict deadband must stop the real body, not leave a creeping trend.");
            n.A0=649;Require(!Motor(ref n,server,new[]{n},out stop) && stop==PredictionStop.RandomDestination,"Server stops before its actual unknown650 destination consumer.");
            n.A0=649;Require(Motor(ref n,new PredictionEnvironment{Multiplayer=true},new[]{n},out stop) && n.A0==650,"Client650 is not a random destination consumer.");
            n.A0=1;n.A2=100;n.A3=149;n.NetOffsetX=3;n.NetOffsetY=-2;
            Require(Motor(ref n,server,new[]{n},out stop) && n.X==1598 && n.Y==2343 && n.NetOffsetX==0 && n.NetOffsetY==0 && n.NewSegment,"Already observed destination uses integer half width and clears the native offset.");
            n.A2=n.A3=0;n.A0=648;
            var wall=new NpcMotionState{Identity=new NpcIdentity(1,new object(),8,1,113,113),Active=true,Life=0};
            Require(!Motor(ref n,new PredictionEnvironment{GoodWorld=true},new[]{n,wall},out stop) && stop==PredictionStop.RandomDestination,"Active Life0 Wall still accelerates this exact decision action.");
            n.A0=648;wall.Identity=new NpcIdentity(1,new object(),200,1,113,113);
            Require(Motor(ref n,new PredictionEnvironment{GoodWorld=true},new[]{n,wall},out stop) && n.A0==649,"Native AnyNPCs excludes dummy200.");
            Console.WriteLine("PASS AI8 six finite consumer boundaries (not a native whole-world certificate)");
        }
        internal static void Lifecycle(object context)
        {
            int actions=0,stops=0,zeroes=0;
            foreach(int type in new[]{24,29,32,45,172,281,282,283,284,285,286,533,693})
            {
                Setup(context);var actor=Birth(type);actor.AI();
                if(type==24)
                {
                    int life=actor.life;actor.StrikeNPCNoInteraction(1,1,1);
                    Require(actor.life<life && actor.justHit && actor.velocity.X>0,"Original positive Strike creates the observed damping input.");
                }
                int limit=type==24?60:3;
                for(int frame=0;frame<limit;frame++)
                {
                    var observed=Capture(context,actor);var expected=observed;var e=EnvironmentFor(actor);var group=Group(context);
                    PredictionStop stop;bool ok=Motor(ref expected,e,group,out stop);float priorV=actor.velocity.X;
                    actor.AI();actions++;
                    if(!ok){Require(stop==PredictionStop.RandomDestination,"Only the actual server RNG consumer may stop this finite AI oracle.");stops++;continue;}
                    Require(expected.X==actor.position.X && expected.Y==actor.position.Y && expected.Vx==actor.velocity.X && expected.Vy==actor.velocity.Y && expected.A0==actor.ai[0] && expected.Alpha==actor.alpha && expected.NetOffsetX==actor.netOffset.X && expected.NetOffsetY==actor.netOffset.Y,"Original AI8 motor type="+type+" frame="+frame+" V="+actor.velocity+" expected="+expected.Vx.ToString("R")+","+expected.Vy.ToString("R")+" clock="+expected.A0+"/"+actor.ai[0]);
                    if(type==24 && priorV!=0 && actor.velocity.X==0)zeroes++;
                }
            }
            // Real NPC.NewNPC arguments establish the original RedHat marker;
            // no manual solver-only ai clock or forged global bool is used.
            foreach(int variant in new[]{0,1,2})
            {
                Setup(context);int type=variant==0?24:32;
                if(variant==0){Main.getGoodWorld=true;NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),2100,2300,113,Start:8);}
                else
                {
                    NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),2100,2300,35,Start:8,ai3:variant==1?1:0);
                    if(variant==2)NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),2300,2300,35,Start:30,ai3:1);
                }
                var actor=Birth(type);var expected=Capture(context,actor);PredictionStop stop;
                Require(Motor(ref expected,EnvironmentFor(actor),Group(context),out stop),"Observed modifier starts in a finite known window.");actor.AI();
                Require(expected.A0==actor.ai[0] && expected.L3==actor.localAI[3] && actor.ai[0]==(variant==2?501:502),"Original Wall/lowest-slot RedHat modifies the real clock, not an unconditional accelerated flag.");actions++;
            }
            Require(zeroes==1,"Actual knockback damps through the strict native stop.");
            Console.WriteLine("PASS AI8 original lifecycle actions="+actions+" strictStops="+zeroes+" unknownConsumers="+stops);
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            int leaf=-1;string selector=Environment.GetEnvironmentVariable("JUEMINGR_CASTER_SCENE");if(!string.IsNullOrEmpty(selector))Require(int.TryParse(selector,out leaf) && leaf>=0 && leaf<4,"Caster selector0..3.");
            Console.WriteLine("CASTER actual leaf="+(leaf<0?"0,1,2,3":leaf.ToString())+" client/server/bodyclock; attack emissions not certified");
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var terrain=(IPredictionTerrain)Get(source,"Terrain");
            var audit=new Harmony("JueMingR.Probe.Caster");audit.Patch(typeof(NPC).GetMethod("UpdateNPC",Flags),prefix:new HarmonyMethod(typeof(NativeCombatCasterChecks),nameof(Before)),postfix:new HarmonyMethod(typeof(NativeCombatCasterChecks),nameof(After)));
            var rows=new List<string>{"scene,frame,clock,selected,actions,matches,long,random,jumps,fresh,restored,max"};
            try
            {
                for(int scene=0;scene<4;scene++)
                {
                    if(leaf>=0 && leaf!=scene)continue;Setup(context);
                    if(scene==2)NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),2100,2300,35,Start:8,ai3:1);
                    if(scene==3){Main.getGoodWorld=true;NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),2100,2300,113,Start:8);}
                    var actor=Birth(scene==2?32:24);if(scene==1)Main.netMode=1;
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                    var freezes=new List<NpcTrajectory>();NpcMotionState expected=default(NpcMotionState);PredictionStop stop=PredictionStop.None;bool ok=false;
                    int selected=0,actions=0,matches=0,longMatches=0,random=0,jumps=0,fresh=0,restored=0;double maximum=0;bool hit=false;long hitTick=-1;NpcTrajectory priorHit=null;
                    before=n=>
                    {
                        if(!ReferenceEquals(n,actor))return;expected=Capture(context,n);var group=Group(context);terrain.Reset();
                        ok=NpcMotion.Step(ref expected,group,group.Length,EnvironmentFor(n),terrain,1,true,out stop);
                        Require(ok || stop==PredictionStop.RandomDestination,"Actual AI8 prefix unknown boundary="+stop+" clock="+n.ai[0]);
                    };
                    after=n=>
                    {
                        if(!ReferenceEquals(n,actor))return;actions++;
                        if(!ok){random++;Require(n.ai[0]==1,"The refused server action actually consumes destination selection.");return;}
                        double error=Vector2.Distance(n.position,new Vector2(expected.X,expected.Y));
                        Require(error<=.08 && Math.Abs(expected.Vx-n.velocity.X)<=.0001 && Math.Abs(expected.Vy-n.velocity.Y)<=.0001 && expected.A0==n.ai[0],"Actual AI8 full action scene="+scene+" error="+error.ToString("R")+" clock="+expected.A0+"/"+n.ai[0]+" V="+expected.Vx+","+expected.Vy+"/"+n.velocity);
                        if(expected.NewSegment)jumps++;
                    };
                    // Real server Strike resets AI8 to400. Scene0 needs250 subsequent
                    // actions to consume650; keep that event and its observed recovery.
                    int actionLimit=scene==0?300:240;
                    for(int frame=0;frame<actionLimit;frame++)
                    {
                        if(scene==0 && frame==35){priorHit=cache.Read(0);int life=actor.life;actor.StrikeNPCNoInteraction(1,1,1);Require(actor.life<life && actor.justHit,"Real positive receive precedes a new sample.");Require(actor.ai[0]==400,"Original server AI8 receiving resets the actual teleport clock to400.");hit=true;hitTick=(long)Main.GameUpdateCount;freezes.Clear();}
                        NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();Require(actor.active,"Actual caster remains a receiving actor in this bounded local window.");
                        var path=cache.Read(0);if(path!=null && ReferenceEquals(path.Identity.Token,actor))
                        {
                            selected++;if(freezes.Count==0 || !ReferenceEquals(freezes[freezes.Count-1],path))freezes.Add(path);
                            if(hit && path.CaptureTick>hitTick){fresh++;Require(!ReferenceEquals(path,priorHit),"Receiving withdraws the production pre-hit path.");}
                            if(scene==2 || scene==3)
                            {
                                var acquired=(NpcMotionState[])Get(source,"states");bool closed=false;
                                foreach(var row in acquired)if(ReferenceEquals(row.Identity.Token,actor) && row.CasterRelationsCaptured && row.CasterRelationsComplete){closed=true;break;}
                                Require(closed,"Real Source acquisition, not the current-action oracle's local snapshot, closes the required global-query roles.");
                            }
                        }
                        if(hit && Main.GameUpdateCount>hitTick){var prepared=GetOptional(Get(host,"World"),"preparedPath") as NpcTrajectory;Require(path==null || !ReferenceEquals(path,priorHit),"Old NoNewHits publication expires after actual receiving.");Require(prepared==null || ReferenceEquals(prepared,path),"Prepared consumer uses current publication.");}
                        foreach(var frozen in freezes)
                        {
                            int age=(int)((long)Main.GameUpdateCount-frozen.CaptureTick);if(age<=0 || age>=frozen.Count)continue;
                            var point=frozen[age];double error=Vector2.Distance(new Vector2(point.Bounds.X,point.Bounds.Y),actor.position);maximum=Math.Max(maximum,error);
                            Require(error<=.08 && Math.Abs(point.Vx-actor.velocity.X)<=.0001 && Math.Abs(point.Vy-actor.velocity.Y)<=.0001,"Frozen AI8 scene="+scene+" frame="+frame+" offset="+age+" error="+error.ToString("R")+" nativeClock="+actor.ai[0]+" stop="+frozen.Stop);matches++;if(age>=30){longMatches++;if(hit && frozen.CaptureTick>hitTick)restored++;}
                        }
                        rows.Add(string.Join(",",scene,frame,actor.ai[0],selected,actions,matches,longMatches,random,jumps,fresh,restored,maximum.ToString("R",CultureInfo.InvariantCulture)));
                    }
                    Require(actions==actionLimit && selected>100 && matches>100 && longMatches>30,"Finite true body windows remain useful before their actual unknown consumer.");
                    if(scene==1)Require(actor.ai[0]>=740 && random==0,"Real client crosses650 and keeps240 original actions without a fictitious destination boundary.");
                    else Require(random>0,"The corresponding server window reaches its real destination-selection action.");
                    if(scene==0)Require(hit && fresh>0 && restored>30,"Actual knockback has useful long recovery after withdrawing the old condition.");
                    Console.WriteLine("PASS Caster scene="+scene+" actions="+actions+" selected="+selected+" matched="+matches+" long="+longMatches+" random="+random+" observedJumps="+jumps+" fresh="+fresh+" restored="+restored+" max="+maximum.ToString("R",CultureInfo.InvariantCulture));before=after=null;
                }
            }
            finally{before=after=null;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"caster-scenes.csv"),rows);}
        }
        private static void Setup(object context){typeof(NativeCombatMartianTurretChecks).GetMethod("Setup",Flags).Invoke(null,new[]{context});hostAssembly=Get(Get(context,"CombatObservation"),"Prediction").GetType().Assembly;}
        private static NPC Birth(int type)
        {int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1800,2300,type,Start:16);Require(slot>=0 && slot<Main.maxNPCs && Main.npc[slot].active && Main.npc[slot].aiStyle==8,"Original AI8 birth type="+type);var actor=Main.npc[slot];Require((bool)hostAssembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true).GetMethod("Receives",Flags).Invoke(null,new object[]{actor,true}),"Actual shared CombatSelection accepts the born caster, independent of contact damage.");return actor;}
        private static NpcMotionState Capture(object context,NPC actor)
        {var n=(NpcMotionState)typeof(NativeCombatMartianTurretChecks).GetMethod("Capture",Flags).Invoke(null,new[]{context,(object)actor});n.CasterRelationsCaptured=n.CasterRelationsComplete=true;return n;}
        private static NpcMotionState[] Group(object context)
        {var rows=new List<NpcMotionState>();for(int slot=0;slot<Main.maxNPCs;slot++){var n=Main.npc[slot];if(n!=null && n.active)rows.Add(Capture(context,n));}return rows.ToArray();}
        private static PredictionEnvironment EnvironmentFor(NPC actor)
        {return (PredictionEnvironment)typeof(NativeCombatMartianTurretChecks).GetMethod("EnvironmentFor",Flags).Invoke(null,new object[]{actor});}
    }
}
