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
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatBrainSwarmChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Action<NPC> before,after;
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static Type Model {get{return typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcBrainSwarmMotion",true);}}
        internal static void Boundaries()
        {
            Console.WriteLine("BRAIN MATH process64="+System.Environment.Is64BitProcess+" Features="+Model.Assembly.ManifestModule.ModuleVersionId+" Platform="+typeof(NpcMotionState).Assembly.ManifestModule.ModuleVersionId+" XNA="+typeof(Vector2).Assembly.Location+" mvid="+typeof(Vector2).Assembly.ManifestModule.ModuleVersionId);
            var action=Model.GetMethod("Step",Flags);var owner=new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,266,266),Style=54,Active=true,Width=160,Height=110,L0=1,BrainRelationsCaptured=true,BrainRelationsComplete=true,BrainBossCaptured=true,BrainBossValid=true};owner.BrainBossIdentity=owner.Identity;
            var child=new NpcMotionState{Identity=new NpcIdentity(1,new object(),24,1,267,267),Style=55,Active=true,Width=20,Height=20,X=1000,Y=1000,BrainRelationsCaptured=true,BrainRelationsComplete=true,BrainBossCaptured=true,BrainBossValid=true,BrainBossIdentity=owner.Identity};
            object[] args={child,new PredictionEnvironment(),null,1,PredictionStop.None,new[]{owner,child},2,false};
            Require((bool)action.Invoke(null,args),"Observed Creeper far orbit has a full deterministic attraction motor.");
            child.X=owner.Bounds.CenterX-child.Width*.5f;child.Y=owner.Bounds.CenterY-child.Height*.5f;args[0]=child;
            Require(!(bool)action.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.RandomDecision,"Server close orbit stops at the actual random charge decision.");
            child.BrainNetMode=1;args[0]=child;Require((bool)action.Invoke(null,args),"Client close orbit has no native random charge decision.");
            child.BrainBossValid=false;args[0]=child;Require(!(bool)action.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.Despawn,"Captured negative global means native retirement, not missing geometry.");
            child.BrainBossValid=true;args[0]=child;args[5]=new[]{child};args[6]=1;Require(!(bool)action.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.MissingDependency,"A missing captured full Brain identity cannot become another owner.");
            foreach(int mode in new[]{0,1,2})
            {
                owner.A0=-2;owner.A3=230;owner.A1=100;owner.A2=120;owner.BrainNetMode=mode;owner.Width=161;owner.Height=111;args=new object[]{owner,new PredictionEnvironment{PlayerX=1000,PlayerY=2000,Crimson=true},null,1,PredictionStop.None,new[]{owner},1,false};
                Require((bool)action.Invoke(null,args),"Observed negative Brain fade advances its actual native mode.");var result=(NpcMotionState)args[0];
                Require(result.A3==(mode==0?255:245) && (mode!=0 || result.X==1520 && result.Y==1865),"Singleplayer25/client-server15 fade and integer half teleport are distinct.");
            }
            foreach(bool good in new[]{false,true})for(int sample=0;sample<8192;sample++)
            {
                child.BrainBossValid=true;child.BrainNetMode=0;child.A0=1;child.JustHit=false;child.Vx=(sample%103-51)*.0713f;child.Vy=(sample%79-39)*.0317f;
                float px=child.Bounds.CenterX+sample%113+1.125f,py=child.Bounds.CenterY-sample%97-.375f;
                Vector2 desired=new Vector2(px-child.Bounds.CenterX,py-child.Bounds.CenterY);desired.Normalize();desired*=good?12f:9f;
                Vector2 velocity=(new Vector2(child.Vx,child.Vy)*(good?49f:99f)+desired)/(good?50f:100f);
                args=new object[]{child,new PredictionEnvironment{Expert=true,GoodWorld=good,PlayerX=px,PlayerY=py},null,1,PredictionStop.None,new[]{owner,child},2,false};
                Require((bool)action.Invoke(null,args),"Known observed Expert Creeper charge.");var result=(NpcMotionState)args[0];
                Require(result.Vx==velocity.X && result.Vy==velocity.Y,"Original XNA Creeper inertia/division field stores sample="+sample+" good="+good+" input="+child.Vx.ToString("R")+","+child.Vy.ToString("R")+" delta="+(px-child.Bounds.CenterX).ToString("R")+","+(py-child.Bounds.CenterY).ToString("R")+" nativeAim="+desired.X.ToString("R")+","+desired.Y.ToString("R")+" predicted="+result.Vx.ToString("R")+","+result.Vy.ToString("R")+" actual="+velocity.X.ToString("R")+","+velocity.Y.ToString("R"));
            }
            GravityBoundaries();
            Console.WriteLine("PASS Brain/Creeper exact relation, native random consumer, three network fade modes and integer teleport boundaries");
        }
        private static void GravityBoundaries()
        {
            PredictionStop stop;var p=new PredictionPlayerMotion{GravityControlCaptured=true,GravitySourceExpected=true,GravityDirection=1,Width=20,Height=40};
            var e=new PredictionEnvironment{GravitySourceCaptured=true,GravitySourceValid=true,GravitySourceX=10,GravitySourceY=20};
            Require(PlayerMotionContinuation.BeginGravity(ref p,e,out stop) && p.ForcedGravityTicks==10 && p.GravityDirection==-1,"Known source refreshes zero to ten before vertical motion.");
            e.GravitySourceValid=false;Require(PlayerMotionContinuation.BeginGravity(ref p,e,out stop) && p.ForcedGravityTicks==9,"Clock decrements once without refresh.");
            for(int i=0;i<9;i++)Require(PlayerMotionContinuation.BeginGravity(ref p,e,out stop),"Known absent-source countdown.");
            Require(p.ForcedGravityTicks==0 && p.GravityDirection==1 && !p.BordersForcedGravity && !p.IgnorePlatforms,"Expiration restores ordinary direction/platform and border facts.");
            e.GravitySourceValid=true;e.GravitySourceX=4010;p.GravityDirection=1;Require(PlayerMotionContinuation.BeginGravity(ref p,e,out stop) && p.ForcedGravityTicks==0,"Native strict4000 distance excludes equality.");
            e.GravitySourceX=4009.9998f;Require(PlayerMotionContinuation.BeginGravity(ref p,e,out stop) && p.ForcedGravityTicks==10,"Native inside4000 refreshes.");
            Require(!PlayerMotionContinuation.BeginGravity(ref p,new PredictionEnvironment(),out stop) && stop==PredictionStop.MissingDependency,"Forced source missing from an attack environment cannot become an ordinary future.");
            var identity=new NpcIdentity(1,new object(),16,1,266,266);var array=new[]{new PredictionGravitySource(true,true,identity,10,20),new PredictionGravitySource(true,false,default(NpcIdentity),0,0),new PredictionGravitySource(true,true,identity,30,40)};
            var sources=new PredictionGravitySources(array,3,p.PlayerToken,p.PlayerIndex,true,identity,10);array[0]=default(PredictionGravitySource);
            var points=new[]{new NpcTrajectoryPoint(0,new NpcMotionState{Width=20,Height=20}),new NpcTrajectoryPoint(1,new NpcMotionState{Width=20,Height=20}),new NpcTrajectoryPoint(2,new NpcMotionState{Width=20,Height=20})};
            var path=new NpcTrajectory(identity,10,1,PredictionAssumption.None,PredictionStop.None,points,3,PredictionStrategy.RollingConditional,gravitySources:sources);NpcTrajectory window;
            Require(sources[0].Valid && path.TryWindow(11,1,2,out window) && window.GravitySources.Count==2 && !window.GravitySources[0].Valid && window.GravitySources[1].X==30 && !window.GravitySources.FirstPlayerStepCompleted,"Gravity prefix is copied, bound and sliced with the same trajectory clock.");
        }
        internal static void Lifecycle(object context)
        {
            var io=new Inputs(context);GravityBinding(io);GravityPlayer(io);int cases=0,random=0;
            foreach(int mode in new[]{0,1,2})foreach(int type in new[]{266,267})foreach(int variant in new[]{0,1,2,3,4,5,6,7,8,9,10,11})
            {
                World(variant%3);Main.netMode=mode;NPC owner=Actor(16,266,new Vector2(1800.125f,1900.25f));owner.localAI[0]=1;owner.target=Main.myPlayer;
                var n=type==266?owner:Actor(24,267,new Vector2(variant==0?1000:1800,1900));NPC.crimsonBoss=16;
                n.target=Main.myPlayer;n.velocity=new Vector2(3.713f,-1.271f);
                if(type==266)
                {n.ai[0]=variant<4?0:variant<7?1:variant<9?2:variant==9?-2:variant==10?-3:-1;n.ai[1]=120;n.ai[2]=130;n.ai[3]=variant==9?240:10;n.alpha=variant==6?254:variant==8?1:120;n.localAI[1]=variant==3?119:12;Actor(30,267,new Vector2(1700,1800));}
                else{n.ai[0]=variant<3?0:1;n.ai[1]=variant==7?5:2;n.justHit=variant==6 || variant==7 || variant==8;n.knockBackResist=variant==8?.8f:0;if(variant==9)n.position.X=2800;if(variant==10)NPC.crimsonBoss=-1;}
                if(variant==11){Main.LocalPlayer.dead=true;Main.LocalPlayer.ZoneCrimson=false;}
                io.Terrain.Reset();var group=io.Group();var state=group[Index(group,n.whoAmI)];object[] args={state,Environment(),io.Terrain,0,PredictionStop.None,group,group.Length,false};bool valid=(bool)Model.GetMethod("Step",Flags).Invoke(null,args);state=(NpcMotionState)args[0];
                if(!valid && ((PredictionStop)args[4]==PredictionStop.RandomDecision || (PredictionStop)args[4]==PredictionStop.RandomDestination || (PredictionStop)args[4]==PredictionStop.UnsupportedMechanism)){random++;continue;}
                n.AI();Require(valid==n.active && state.Active==n.active,"Original Brain/Creeper scalar active mode="+mode+" type="+type+" variant="+variant);
                if(valid)Require(state.X==n.position.X && state.Y==n.position.Y && state.Vx==n.velocity.X && state.Vy==n.velocity.Y && state.A0==n.ai[0] && state.A1==n.ai[1] && state.A3==n.ai[3] && state.L1==n.localAI[1] && state.L3==n.localAI[3] && state.Alpha==n.alpha && state.Health.DontTakeDamage==n.dontTakeDamage,"Original Brain/Creeper action type="+type+" mode="+mode+" variant="+variant+" predicted="+state.X.ToString("R")+","+state.Y.ToString("R")+" V="+state.Vx.ToString("R")+","+state.Vy.ToString("R")+" phase="+state.A0+","+state.A1+","+state.A3+" clock="+state.L1+","+state.L3+" alpha/Damage="+state.Alpha+"/"+state.Health.DontTakeDamage+" actual="+n.position.X.ToString("R")+","+n.position.Y.ToString("R")+" V="+n.velocity.X.ToString("R")+","+n.velocity.Y.ToString("R")+" phase="+string.Join("/",n.ai)+" local="+string.Join("/",n.localAI)+" alpha/Damage="+n.alpha+"/"+n.dontTakeDamage);cases++;
            }
            Main.netMode=0;Console.WriteLine("PASS Brain/Creeper original scalar actions="+cases+" explicit actual random/gravity boundaries="+random);
        }
        private static void GravityBinding(Inputs io)
        {
            var bind=io.Source.GetType().GetMethod("TryBindPlayerGravity",Flags);var identity=new NpcIdentity(1,new object(),16,1,266,266);var token=new object();
            var p=new PredictionPlayerMotion{PlayerToken=token,PlayerIndex=0,GravityControlCaptured=true,GravityDirection=1,Width=20,Height=40};
            var values=new PredictionGravitySource[5];var points=new NpcTrajectoryPoint[5];for(int i=0;i<5;i++){values[i]=new PredictionGravitySource(true,true,identity,100+i,200+i);points[i]=new NpcTrajectoryPoint(i,new NpcMotionState{Width=20,Height=20});}
            foreach(bool beforeNpc in new[]{false,true})foreach(int age in new[]{0,1})
            {
                long sample=100,current=sample+age+(beforeNpc?1:0);var payload=new PredictionGravitySources(values,5,token,0,beforeNpc,identity,sample);
                var path=new NpcTrajectory(identity,sample,1,PredictionAssumption.None,PredictionStop.None,points,5,PredictionStrategy.RollingConditional,gravitySources:payload);
                object[] args={path,current,p,new PredictionEnvironment()};Require((bool)bind.Invoke(null,args),"Valid gravity clock phase/age binding.");var motion=(PredictionPlayerMotion)args[2];var env=(PredictionEnvironment)args[3];
                Require(motion.GravityAction==age+(beforeNpc?1:0) && env.GravitySources[motion.GravityAction].X==100+age+(beforeNpc?1:0),"BeforeNPC / completed-world source action ordinal follows actual GUC, not a restamped capture.");
                NpcTrajectory window;Require(path.TryWindow(sample+1,2,2,out window),"Valid matching immutable window.");args=new object[]{window,current+1,p,new PredictionEnvironment()};Require((bool)bind.Invoke(null,args) && ((PredictionEnvironment)args[3]).GravitySources[((PredictionPlayerMotion)args[2]).GravityAction].X==101+age+(beforeNpc?1:0),"Window offsets source and points together without rebasing capture metadata.");
                var clone=p;clone.PlayerToken=new object();args=new object[]{path,current,clone,new PredictionEnvironment()};
                Require(!(bool)bind.Invoke(null,args) && ((PredictionPlayerMotion)args[2]).GravitySourceExpected,"Same-index cloned player cannot borrow another token's gravity source.");
                args=new object[]{path,current,p,new PredictionEnvironment()};Require((bool)bind.Invoke(null,args),"Fresh real observation binds before pre-roll.");motion=(PredictionPlayerMotion)args[2];env=(PredictionEnvironment)args[3];PredictionStop stop;
                Require(PlayerMotionContinuation.BeginGravity(ref motion,env,out stop) && PlayerMotionContinuation.BeginGravity(ref motion,env,out stop),"Known two-action launch delay consumes the same immutable source prefix.");
                Require(motion.GravityAction==age+(beforeNpc?1:0)+2,"Explicit pre-rolled motion retains its original token and advanced gravity ordinal; no clone/read rebinding.");
                bool refused=false;try{path.Republish(sample+1,3);}catch(InvalidOperationException){refused=true;}Require(refused,"Conditional gravity payload cannot republish a different capture.");
            }
            Console.WriteLine("PASS gravity source phase/age/window binding matrix8 and conditional republish refusal");
        }
        private static void GravityPlayer(Inputs io)
        {
            var rngs=typeof(Main).GetField("_rngs",Flags);var saved=rngs.GetValue(null);var read=io.Source.GetType().GetMethod("ReadPlayer",Flags);int cases=0;
            try
            {
                rngs.SetValue(null,new Dictionary<string,Terraria.Utilities.UnifiedRandom>());World(2);Main.windSpeedCurrent=Main.windSpeedTarget=0;Main.netMode=0;
                var p=Main.LocalPlayer;p.isControlledByFilm=true;p.creativeGodMode=true;p.immune=true;p.immuneTime=100000;p.releaseJump=true;p.ResetFallDamage();
                var owner=Actor(16,266,new Vector2(1800,1900));NPC.brainOfGravity=-1;
                using(Main.SwapRandom("UpdatePlayers"))p.Update(Main.myPlayer);
                var predicted=(PredictionPlayerMotion)read.Invoke(null,new object[]{p});
                for(int frame=0;frame<65;frame++)
                {
                    if(frame==0 || frame==30)NPC.brainOfGravity=owner.whoAmI;if(frame==12 || frame==45)NPC.brainOfGravity=-1;
                    if(frame>=30 && frame<45){owner.position=p.Center-new Vector2(owner.width*.5f,owner.height*.5f)+new Vector2(frame<35?4000:3999.5f,0);}
                    var e=Environment();io.Terrain.Reset();PredictionStop stop;Require(PlayerMotionContinuation.Advance(ref predicted,e,io.Terrain,out stop),"Original player force refresh/countdown continuation frame="+frame+" stop="+stop);
                    using(Main.SwapRandom("UpdatePlayers"))p.Update(Main.myPlayer);
                    double error=Vector2.Distance(p.position,new Vector2(predicted.X,predicted.Y));Require(error<=.002 && predicted.Vx==p.velocity.X && predicted.Vy==p.velocity.Y && predicted.GravityDirection==p.gravDir && predicted.ForcedGravityTicks==p.forcedGravity,"Original Player.Update forced gravity frame="+frame+" error="+error+" clock="+predicted.ForcedGravityTicks+"/"+p.forcedGravity+" gravity="+predicted.GravityDirection+"/"+p.gravDir+" V="+predicted.Vx.ToString("R")+","+predicted.Vy.ToString("R")+" native="+p.velocity.X.ToString("R")+","+p.velocity.Y.ToString("R"));cases++;
                }
                Console.WriteLine("PASS original Player.Update Brain force refresh/range4000/countdown actions="+cases);
            }
            finally{rngs.SetValue(null,saved);NPC.brainOfGravity=-1;}
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            var io=new Inputs(context);var observer=new Harmony("jmr.brain.swarm.native");observer.Patch(typeof(NPC).GetMethod("UpdateNPC",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBrainSwarmChecks),nameof(Before)),postfix:new HarmonyMethod(typeof(NativeCombatBrainSwarmChecks),nameof(After)));
            var failures=new List<string>();var rows=new List<string>{"mode,frame,selected,type,phase,clock,count,stop,matched,long,current,random,cross"};long total=0,totalLong=0;
            try
            {
                foreach(int mode in new[]{0,1,2})
                {
                    try
                    {
                        World(mode);var p=Main.LocalPlayer;p.isControlledByFilm=true;p.releaseJump=true;p.statLife=p.statLifeMax=p.statLifeMax2=5000;p.immune=true;p.immuneTime=1000000;
                        int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1700,2100,266,Start:16,Target:Main.myPlayer);var brain=Main.npc[slot];var freezes=new List<NpcTrajectory>();var phases=new HashSet<int>();var creepPhases=new HashSet<int>();NpcMotionState predicted=default(NpcMotionState);bool valid=false;int current=0,random=0,kills=0,published=0,cross=0;long matched=0,longMatches=0;double max=0;
                        before=n=>
                        {
                            valid=false;if(n.type!=266 && n.type!=267)return;io.Terrain.Reset();var group=io.Group();predicted=group[Index(group,n.whoAmI)];PredictionStop stop;var e=Environment();e.PlayerTimelineActive=true;valid=NpcMotion.Step(ref predicted,group,group.Length,e,io.Terrain,0,true,out stop);
                            Require(valid || stop==PredictionStop.RandomDecision || stop==PredictionStop.RandomDestination || stop==PredictionStop.PhaseBoundary || stop==PredictionStop.Despawn,"Current Brain/swarm original known dependency boundary type="+n.type+" stop="+stop);if(stop==PredictionStop.RandomDecision || stop==PredictionStop.RandomDestination)random++;
                        };
                        after=n=>
                        {
                            if(n.type!=266 && n.type!=267 || !valid || !n.active)return;
                            double error=Vector2.Distance(n.position,new Vector2(predicted.X,predicted.Y));Require(error<=.002 && Math.Abs(predicted.Vx-n.velocity.X)<=.0001 && Math.Abs(predicted.Vy-n.velocity.Y)<=.0001 && predicted.A0==n.ai[0] && predicted.A1==n.ai[1] && predicted.Alpha==n.alpha,"Original Brain/swarm full action type="+n.type+" error="+error+" predictedV="+predicted.Vx+","+predicted.Vy+" actualV="+n.velocity+" phase="+n.ai[0]);current++;
                        };
                        NativeCombatObservationChecks.Save(io.Host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                        for(int frame=0;frame<2600;frame++)
                        {
                            if(frame%120==0){p.controlRight=frame%240==0;p.controlLeft=!p.controlRight;freezes.Clear();}
                            NPC aim=brain.dontTakeDamage?null:brain;for(int i=0;aim==null && i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].type==267 && Main.npc[i].ai[0]!=0)aim=Main.npc[i];
                            if(aim==null)for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].type==267){aim=Main.npc[i];break;}
                            if(aim!=null)NativeCombatModeledImpactChecks.SampleMouse(context,aim.Center);step();phases.Add((int)brain.ai[0]);for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].type==267)creepPhases.Add((int)Main.npc[i].ai[0]);
                            var path=cache.Read(0);bool selected=(bool)Get(Get(io.Host,"Selection"),"HasTarget");if(path!=null && selected && (path.Identity.Type==266 || path.Identity.Type==267)){published++;if(frame%5==0 && path.Count>1)freezes.Add(path);}
                            for(int i=freezes.Count-1;i>=0;i--)
                            {
                                var frozen=freezes[i];int offset=(int)((long)Main.GameUpdateCount-frozen.CaptureTick);var actor=Main.npc[frozen.Identity.Slot];if(offset>=frozen.Count || !actor.active || !ReferenceEquals(frozen.Identity.Token,actor) || frozen.Identity.Generation!=actor.generation){freezes.RemoveAt(i);continue;}if(offset<1)continue;
                                var point=frozen[offset];double error=Vector2.Distance(actor.position,new Vector2(point.Bounds.X,point.Bounds.Y));max=Math.Max(max,error);Require(error<=.08 && point.CanReceive==(!actor.dontTakeDamage && !actor.immortal),"Frozen Brain/swarm mode="+mode+" frame="+frame+" type="+actor.type+" offset="+offset+" error="+error+" phase="+actor.ai[0]+","+actor.ai[1]+" stop="+frozen.Stop);matched++;if(offset>=30)longMatches++;if(point.Phase!=frozen[0].Phase)cross++;
                            }
                            rows.Add(string.Join(",",mode,frame,selected,path?.Identity.Type??0,brain.ai[0],brain.localAI[1],path?.Count??0,path?.Stop.ToString()??"none",matched,longMatches,current,random,cross));
                            if(frame==1400){freezes.Clear();for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].type==267){Main.npc[i].StrikeNPCNoInteraction(1000000,0,0);kills++;}}
                        }
                        Require(published>100 && current>300 && longMatches>100 && phases.Contains(0) && phases.Contains(1) && phases.Contains(2) && creepPhases.Contains(1),"Real birth/observed random charge/Brain teleport have long curved selected recovery mode="+mode+" phases="+string.Join("/",phases)+" long="+longMatches+" current="+current);
                        Require(kills>0 && phases.Contains(-1) && phases.Contains(-2) && phases.Contains(-3) && cross>0,"Actual Creeper strikes open Brain second phase and observed teleports cross their phase boundary.");
                        total+=matched;totalLong+=longMatches;Console.WriteLine("PASS natural Brain/swarm mode="+mode+" matched="+matched+" long="+longMatches+" current="+current+" random="+random+" cross="+cross+" max="+max.ToString("R",CultureInfo.InvariantCulture));
                    }
                    catch(Exception error){failures.Add(mode+":"+error.Message);Console.WriteLine("FAIL natural Brain/swarm mode="+mode+" "+error);}
                    finally{before=after=null;}
                }
            }
            finally{before=after=null;observer.UnpatchAll(observer.Id);Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"brain-swarm-scenes.csv"),rows);}
            Require(failures.Count==0,"Complete Brain/swarm natural matrix: "+string.Join(" | ",failures));Console.WriteLine("PASS Brain/swarm natural matched="+total+" long="+totalLong);
        }
        private static int Index(NpcMotionState[] group,int slot){for(int i=0;i<group.Length;i++)if(group[i].Identity.Slot==slot)return i;throw new InvalidOperationException("Missing original Brain/swarm slot="+slot);}
        private static NPC Actor(int slot,int type,Vector2 position){var n=Main.npc[slot]=new NPC{whoAmI=slot};n.SetDefaults(type);n.active=true;n.target=Main.myPlayer;n.position=position;return n;}
        private sealed class Inputs
        {
            internal readonly object Host,Source;internal readonly IPredictionTerrain Terrain;private readonly long session;private readonly MethodInfo read,tracking;
            internal Inputs(object context){Host=Get(context,"CombatObservation");Source=Get(Host,"Prediction");Terrain=(IPredictionTerrain)Get(Source,"Terrain");session=(long)Get(Host,"Session");read=Source.GetType().GetMethod("Read",Flags);tracking=Source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);}
            internal NpcMotionState[] Group()
            {
                var list=new List<NpcMotionState>();for(int i=0;i<Main.maxNPCs;i++)
                {var n=Main.npc[i];if(!n.active || n.type!=266 && n.type!=267)continue;var state=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] args={n,state,Terrain};tracking.Invoke(null,args);state=(NpcMotionState)args[1];state.BrainRelationsCaptured=state.BrainRelationsComplete=state.BrainBossCaptured=true;state.BrainBossValid=NPC.crimsonBoss>=0 && NPC.crimsonBoss<Main.maxNPCs;state.BrainBossIdentity=state.BrainBossValid?new NpcIdentity(session,Main.npc[NPC.crimsonBoss],NPC.crimsonBoss,Main.npc[NPC.crimsonBoss].generation,Main.npc[NPC.crimsonBoss].type,Main.npc[NPC.crimsonBoss].netID):default(NpcIdentity);list.Add(state);}return list.ToArray();
            }
        }
        private static PredictionEnvironment Environment(){var p=Main.LocalPlayer;return new PredictionEnvironment{Players=new PredictionPlayers(new[]{new MotionRect((int)p.position.X,(int)p.position.Y,p.width,p.height)},1,new[]{p.whoAmI}),PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerDead=p.dead,PlayerVelocityCaptured=true,PlayerVx=p.velocity.X,PlayerVy=p.velocity.Y,Crimson=p.ZoneCrimson,GravitySourceCaptured=true,GravitySourceValid=NPC.brainOfGravity>=0 && NPC.brainOfGravity<Main.maxNPCs,GravitySourceX=NPC.brainOfGravity>=0 && NPC.brainOfGravity<Main.maxNPCs?Main.npc[NPC.brainOfGravity].Center.X:0,GravitySourceY=NPC.brainOfGravity>=0 && NPC.brainOfGravity<Main.maxNPCs?Main.npc[NPC.brainOfGravity].Center.Y:0,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,WorldSurface=(float)Main.worldSurface,Multiplayer=Main.netMode==1,Expert=Main.expertMode,GoodWorld=Main.getGoodWorld};}
        private static void World(int mode)
        {
            typeof(NativeCombatDiveFlightChecks).GetMethod("World",Flags).Invoke(null,null);Main.GameMode=mode==0?0:1;Main.getGoodWorld=mode==2;NPC.crimsonBoss=NPC.brainOfGravity=-1;
            for(int x=30;x<150;x++)for(int y=150;y<180;y++)Main.tile[x,y].type=TileID.Crimstone;
            // A real ceiling keeps the reversed player in this actual Crimson
            // arena; do not change native forced-gravity/top-edge death rules.
            if(mode==2)for(int x=30;x<Main.maxTilesX-30;x++){Main.tile[x,130].active(true);Main.tile[x,130].type=TileID.Crimstone;}
            Main.PlayerSceneMetrics.Reset();Main.LocalPlayer.UpdateSceneMetrics();Main.LocalPlayer.UpdateBiomes();Require(Main.LocalPlayer.ZoneCrimson,"Original biome scan establishes Brain arena.");
            if(Main.ItemDropSolver==null){Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);}for(int i=0;i<Main.item.Length;i++)Main.item[i].whoAmI=i;
        }
    }
}
