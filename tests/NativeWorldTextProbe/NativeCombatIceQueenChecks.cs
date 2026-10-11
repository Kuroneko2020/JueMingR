using System;
using System.Collections.Generic;
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
    internal static class NativeCombatIceQueenChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Action<NPC> before,after;
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static Type Model {get{return typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcIceQueenMotion",true);}}
        private static bool ModelStep(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,out PredictionStop stop)
        {object[] args={n,e,terrain,1,PredictionStop.None,null,0,false};bool ok=(bool)Model.GetMethod("Step",Flags).Invoke(null,args);n=(NpcMotionState)args[0];stop=(PredictionStop)args[4];return ok;}
        private static NpcMotionState Input(int phase,float clock,float playerX=1500)
        {
            return new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,345,345),Style=60,Active=true,X=1400,Y=1600,Width=130,Height=140,Life=34000,LifeMax=34000,A0=phase,A1=clock,A2=1,Target=0,PlayerIndex=0,ClosestPlayerIndex=0,TargetCaptured=true,HasPlayer=true,HasClosestPlayer=true,PlayerActive=true,ClosestPlayerActive=true,PlayerPreciseAreaCaptured=true,ClosestPlayerPreciseAreaCaptured=true,PlayerPreciseArea=new MotionRect(playerX-10,2000,20,42),ClosestPlayerPreciseArea=new MotionRect(playerX-10,2000,20,42),PlayerArea=new MotionRect(playerX-10,2000,20,42),ClosestPlayerArea=new MotionRect(playerX-10,2000,20,42),PlayerVelocityCaptured=true,ClosestPlayerVelocityCaptured=true,PlayerCenterX=playerX,PlayerCenterY=2021,ClosestPlayerCenterX=playerX,ClosestPlayerCenterY=2021};
        }
        private static PredictionEnvironment Env(float x=1500){return new PredictionEnvironment{PlayerIndex=0,PlayerX=x,PlayerY=2021,PlayerWidth=20,PlayerHeight=42};}
        internal static void Boundaries()
        {
            PredictionStop stop;int cases=0;
            foreach(int phase in new[]{0,1,2})
            {
                int limit=phase==0?800:phase==1?600:500;var n=Input(phase,0);Require(ModelStep(ref n,Env(),null,out stop) && n.A1==0 && n.IceQueenClockLower==1 && n.IceQueenClockUpper==3,"AI60 keeps observed A1 and separately records all possible future clock results.");cases++;
                n=Input(phase,limit-2);Require(!ModelStep(ref n,Env(),null,out stop) && stop==PredictionStop.RandomDecision,"First mixed true mode consumer ends known future phase="+phase);cases++;
                n=Input(phase,limit-3);Require(ModelStep(ref n,Env(),null,out stop),"Strict threshold equality still has one deterministic motor action phase="+phase);cases++;
                n=Input(phase,limit);Require(ModelStep(ref n,new PredictionEnvironment{PlayerIndex=0,PlayerX=1500,PlayerY=2021,PlayerWidth=20,PlayerHeight=42,Multiplayer=true},null,out stop) && !n.IceQueenClockRange && n.A1==limit,"Client does not invent the server random clock.");cases++;
            }
            var far=Input(0,900,3000);Require(ModelStep(ref far,Env(3000),null,out stop) && far.IceQueenClockUpper==903,"Unconsumed phase-zero random clock does not truncate far motor.");cases++;
            far=Input(-1,900,3000);Require(ModelStep(ref far,Env(3000),null,out stop) && far.A0==0 && far.A1==0 && far.A2==0 && far.A3==0,"Far target overwrites every native random mode result.");cases++;
            var near=Input(-1,0,2465);Require(!ModelStep(ref near,Env(2465),null,out stop) && stop==PredictionStop.RandomDecision,"Exactly 1000 pixels still consumes random mode choice.");cases++;
            var day=Input(1,599);day.Vx=0;day.Vy=2;day.HasPlayer=day.HasClosestPlayer=false;Require(ModelStep(ref day,new PredictionEnvironment{Day=true},null,out stop) && day.Vx==-.25f && day.Vy==1.9f && !day.IceQueenClockRange,"Known daylight retreat does not need a player or consume attack clocks.");cases++;
            var invalid=Input(2,0);invalid.IceQueenClockRange=true;invalid.IceQueenClockLower=3;invalid.IceQueenClockUpper=2;Require(!ModelStep(ref invalid,Env(),null,out stop) && stop==PredictionStop.InvalidState,"Invalid future range is rejected.");cases++;
            invalid=Input(1,0);invalid.PlayerPreciseAreaCaptured=invalid.ClosestPlayerPreciseAreaCaptured=false;Require(!ModelStep(ref invalid,Env(),null,out stop) && stop==PredictionStop.MissingDependency,"Raw player top-left cannot be borrowed from integer facing rectangle.");cases++;
            Require(!(bool)Model.GetMethod("Known",Flags).Invoke(null,new object[]{new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,327,327),Style=58}}),"AI60 member contract is separate from AI58.");
            Console.WriteLine("PASS AI60 clock-set and actual motor-consumer boundaries cases="+cases);
        }
        internal static void Lifecycle(object context)
        {
            var io=new Inputs(context);int cases=0;
            foreach(int phase in new[]{0,1,2})for(int variant=0;variant<6;variant++)
            {
                World();var p=Main.LocalPlayer;var n=Actor(new Vector2(1700.125f,1800.25f));n.ai[0]=phase;n.ai[1]=10.25f;n.ai[2]=variant%3-1;n.ai[3]=variant==5?17:-4;n.velocity=new Vector2(variant%2==0?-8.375f:8.375f,variant<3?-1.125f:1.125f);
                n.life=variant==0?n.lifeMax:variant==1?n.lifeMax*3/4:variant==2?n.lifeMax*3/4-1:variant==3?n.lifeMax/2-1:variant==4?n.lifeMax/4-1:n.lifeMax/10-1;
                p.position=new Vector2(variant%2==0?1710.375f:1900.625f,n.position.Y+n.height+(variant%3==0?149.875f:variant%3==1?180.125f:200.125f));Main.netMode=variant==4?1:0;Compare(io,n);cases++;
            }
            foreach(int direction in new[]{-1,0,1}){World();Main.dayTime=true;var n=Actor(new Vector2(1700,1800));n.ai[0]=1;n.ai[1]=599;n.velocity=new Vector2(direction,2.125f);Compare(io,n);cases++;}
            World();var far=Actor(new Vector2(500,1800));far.ai[0]=-1;Compare(io,far);cases++;
            Console.WriteLine("PASS original AI60 scalar actions="+cases+" (server A1 checked against exact +1..3 set, never fabricated)");
        }
        private static void Compare(Inputs io,NPC n)
        {
            io.Terrain.Reset();var predicted=io.Capture(n);var original=predicted;PredictionStop stop;Require(ModelStep(ref predicted,Environment(),io.Terrain,out stop),"Known native AI60 scalar action continues stop="+stop);n.AI();
            bool clock=predicted.IceQueenClockRange?n.ai[1]>=predicted.IceQueenClockLower && n.ai[1]<=predicted.IceQueenClockUpper:n.ai[1]==predicted.A1;
            Require(predicted.Vx==n.velocity.X && predicted.Vy==n.velocity.Y && predicted.A0==n.ai[0] && predicted.A2==n.ai[2] && predicted.A3==n.ai[3] && predicted.Direction==n.direction && predicted.DirectionY==n.directionY && predicted.NoTileCollide==n.noTileCollide && predicted.NoGravity==n.noGravity && clock,
                "Original IceQueen scalar phase="+original.A0+" HP="+n.life+" predV="+predicted.Vx.ToString("R")+","+predicted.Vy.ToString("R")+" actualV="+n.velocity.X.ToString("R")+","+n.velocity.Y.ToString("R")+" ai="+predicted.A0+"/"+predicted.A1+"/"+predicted.A2+"/"+predicted.A3+" vs "+string.Join("/",n.ai)+" range="+predicted.IceQueenClockLower+".."+predicted.IceQueenClockUpper+" facing="+predicted.Direction+","+predicted.DirectionY+" vs "+n.direction+","+n.directionY);
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            int selectedScene=-1;string selector=System.Environment.GetEnvironmentVariable("JMR_ICE_QUEEN_SCENE");
            Require(string.IsNullOrEmpty(selector) || int.TryParse(selector,out selectedScene) && selectedScene>=0 && selectedScene<3,"JMR_ICE_QUEEN_SCENE must be 0, 1 or 2.");
            Console.WriteLine("AI60 actual natural scene set="+(selectedScene<0?"0,1,2":selectedScene.ToString()));
            var io=new Inputs(context);var oracle=new Harmony("JueMingR.Tests.IceQueenOriginalSlot");oracle.Patch(typeof(NPC).GetMethod("UpdateNPC",Flags),prefix:new HarmonyMethod(typeof(NativeCombatIceQueenChecks),nameof(Before)),postfix:new HarmonyMethod(typeof(NativeCombatIceQueenChecks),nameof(After)));
            try
            {
                var rows=new List<string>{"scene,frame,phase,count,stop,matches,long,recovery"};var failures=new List<string>();
                for(int scene=0;scene<3;scene++)try
                {
                    if(selectedScene>=0 && scene!=selectedScene)continue;
                    World();Main.snowMoon=true;Main.dayTime=scene==2;Main.rand=new Terraria.Utilities.UnifiedRandom(6000+scene);var p=Main.LocalPlayer;p.position=new Vector2(2400,2400-p.height);p.ResetFallDamage();p.isControlledByFilm=true;p.releaseJump=true;p.immune=true;p.immuneTime=1000000;p.statLife=p.statLifeMax=p.statLifeMax2=5000;for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
                    NativeCombatObservationChecks.Save(io.Host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),2200,1850,345,Start:16,Target:Main.myPlayer);var actor=Main.npc[slot];
                    var frozen=new List<Frozen>();var phases=new HashSet<int>();var longByPhase=new int[3];int matches=0,longMatches=0,actions=0,boundaries=0,recovery=0,publication=0,hits=0,despawnTails=0,retiredFrame=-1;double maxError=0,tangent=0;bool waiting=false;var predicted=default(NpcMotionState);bool valid=false;PredictionStop lastActionStop=PredictionStop.None;
                    before=n=>{valid=false;if(!ReferenceEquals(n,actor))return;io.Terrain.Reset();predicted=io.Capture(n);var e=Environment();e.PlayerTimelineActive=true;valid=NpcMotion.Step(ref predicted,new[]{predicted},1,e,io.Terrain,1,true,out lastActionStop);if(lastActionStop==PredictionStop.RandomDecision)boundaries++;Require(valid || lastActionStop==PredictionStop.RandomDecision || lastActionStop==PredictionStop.Despawn,"Current original IceQueen action stop="+lastActionStop);};
                    after=n=>{if(!ReferenceEquals(n,actor) || !valid)return;double error=Vector2.Distance(n.position,new Vector2(predicted.X,predicted.Y));bool clock=predicted.IceQueenClockRange?n.ai[1]>=predicted.IceQueenClockLower && n.ai[1]<=predicted.IceQueenClockUpper:n.ai[1]==predicted.A1;Require(error<=.002 && predicted.Vx==n.velocity.X && predicted.Vy==n.velocity.Y && clock,"Current IceQueen full action error="+error+" predV="+predicted.Vx.ToString("R")+","+predicted.Vy.ToString("R")+" actualV="+n.velocity+" phase="+string.Join("/",n.ai));actions++;};
                    int maximum=scene==2?1600:2600;
                    for(int frame=0;frame<maximum && actor.active;frame++)
                    {
                        if(scene!=2 && frame%240==0){p.controlRight=frame%480==0;p.controlLeft=!p.controlRight;frozen.Clear();}
                        // Genuine received damage changes the observed health input. Old
                        // conditional forecasts are retired before that external event.
                        if(scene==1 && (frame==500 || frame==1000 || frame==1500)){frozen.Clear();int old=actor.life;actor.StrikeNPCNoInteraction(actor.lifeMax/4+100,0,0);Require(actor.life<old && actor.active,"Actual IceQueen hit changes health without inventing a new actor.");hits++;}
                        NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();Require(p.active && !p.dead,"Native continuing IceQueen player remains alive.");if(!actor.active){Require(scene==2,"Night actor does not unexpectedly retire.");retiredFrame=frame;Require(lastActionStop==PredictionStop.Despawn && !predicted.Active && actor.timeLeft==predicted.TimeLeft,"Actual day retirement agrees with the current predicted Despawn action and timeLeft.");break;}int phase=(int)actor.ai[0];phases.Add(phase);var path=cache.Read(0);
                        if(path!=null && ReferenceEquals(path.Identity.Token,actor)){publication++;if(path.Stop==PredictionStop.Despawn)despawnTails++;if(path.Stop==PredictionStop.RandomDecision)waiting=true;else if(waiting && path.Count>=30){recovery++;waiting=false;}if(frame%5==0 && path.Count>1)frozen.Add(new Frozen(path,phase));}
                        for(int i=frozen.Count-1;i>=0;i--)
                        {
                            var item=frozen[i];int offset=(int)((long)Main.GameUpdateCount-item.Path.CaptureTick);if(offset>=item.Path.Count){frozen.RemoveAt(i);continue;}if(offset<=0)continue;var point=item.Path[offset];double error=Vector2.Distance(actor.position,new Vector2(point.Bounds.X,point.Bounds.Y));matches++;maxError=Math.Max(maxError,error);
                            if(offset>=30){longMatches++;if(item.Phase>=0 && item.Phase<3)longByPhase[item.Phase]++;tangent=Math.Max(tangent,Vector2.Distance(actor.position,new Vector2(item.Path[0].Bounds.X+item.Path[0].Vx*offset,item.Path[0].Bounds.Y+item.Path[0].Vy*offset)));}
                            Require(error<=.08 && point.NoTileCollide==actor.noTileCollide && point.CanReceive==(!actor.dontTakeDamage && !actor.immortal),"Frozen IceQueen scene="+scene+" frame="+frame+" offset="+offset+" error="+error.ToString("R")+" actual="+actor.position+" V="+actor.velocity+" ai="+string.Join("/",actor.ai)+" PC="+p.position+" pv="+p.velocity+" stop="+item.Path.Stop);
                        }
                        rows.Add(string.Join(",",scene,frame,phase,path?.Count??0,path?.Stop.ToString()??"none",matches,longMatches,recovery));
                    }
                    Require(publication>0 && matches>=30 && longMatches>=30 && tangent>2,"Natural IceQueen keeps curved multi-step motion scene="+scene);
                    if(scene!=2){Require(actions>=300 && actor.active && boundaries>=3 && recovery>=3,"Actual native random branches revoke and recover known long windows scene="+scene);for(int phase=0;phase<3;phase++)Require(phases.Contains(phase) && longByPhase[phase]>=30,"Every actual IceQueen phase has a long known motor segment scene="+scene+" phase="+phase);if(scene==1)Require(hits==3,"All health transitions were actual native hits.");}
                    else Require(!actor.active && retiredFrame>=0 && despawnTails>0,"Original day escape reaches actual retirement with published Despawn tails, rather than a forced timeLeft or a night-duration requirement.");
                    Console.WriteLine("PASS AI60 scene="+scene+" matched="+matches+" long="+longMatches+" actions="+actions+" boundaries="+boundaries+" recovery="+recovery+" despawnTails="+despawnTails+" retiredFrame="+retiredFrame+" finalTimeLeft="+actor.timeLeft+" maxError="+maxError.ToString("R"));
                }
                catch(Exception ex){failures.Add("scene"+scene+":"+ex.Message);Console.WriteLine("FAIL AI60 scene="+scene+" "+ex);}
                finally{before=after=null;}
                Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"ice-queen-scenes.csv"),rows);Require(failures.Count==0,"IceQueen actual scenes: "+string.Join(" | ",failures));
            }
            finally{before=after=null;oracle.UnpatchAll(oracle.Id);}
        }
        private sealed class Frozen {internal readonly NpcTrajectory Path;internal readonly int Phase;internal Frozen(NpcTrajectory p,int phase){Path=p;Phase=phase;}}
        private sealed class Inputs
        {
            internal readonly object Host,Source;internal readonly IPredictionTerrain Terrain;private readonly long session;private readonly MethodInfo read,tracking;
            internal Inputs(object context){Host=Get(context,"CombatObservation");Source=Get(Host,"Prediction");Terrain=(IPredictionTerrain)Get(Source,"Terrain");session=(long)Get(Host,"Session");read=Source.GetType().GetMethod("Read",Flags);tracking=Source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);}
            internal NpcMotionState Capture(NPC n){var state=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] a={n,state,Terrain};tracking.Invoke(null,a);return (NpcMotionState)a[1];}
        }
        private static NPC Actor(Vector2 position){var n=Main.npc[16]=new NPC{whoAmI=16};n.SetDefaults(345);n.active=true;n.target=Main.myPlayer;n.position=position;return n;}
        private static void World(){NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.dayTime=false;Main.remixWorld=false;Main.Map=new Terraria.Map.WorldMap(Main.maxTilesX,Main.maxTilesY);Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true};Main.LocalPlayer.position=new Vector2(2000.125f,2400-Main.LocalPlayer.height);Main.LocalPlayer.ResetFallDamage();}
        private static PredictionEnvironment Environment(){var p=Main.LocalPlayer;return new PredictionEnvironment{Players=new PredictionPlayers(new[]{new MotionRect((int)p.position.X,(int)p.position.Y,p.width,p.height)},1,new[]{p.whoAmI}),PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerVx=p.velocity.X,PlayerVy=p.velocity.Y,PlayerVelocityCaptured=true,GravityWorldSurface=Main.worldSurface,WorldSurface=(float)Main.worldSurface,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,Day=Main.dayTime,Multiplayer=Main.netMode==1,Expert=Main.expertMode};}
    }
}
