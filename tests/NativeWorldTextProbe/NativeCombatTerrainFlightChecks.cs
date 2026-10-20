using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatTerrainFlightChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        // Locked UpdateWindyDayState reads/writes only static weather facts.
        // The isolated fixture has no initialized game Main; use the same
        // constructor-free receiver pattern as the existing weather oracle.
        private static readonly Main weatherOracle=WeatherOracle();
        private static Main WeatherOracle()
        {var receiver=(Main)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Main));GC.SuppressFinalize(receiver);return receiver;}
        internal static void Boundaries()
        {
            var type=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcTerrainFlightMotion",true);
            MethodInfo known=type.GetMethod("Known",Flags),action=type.GetMethod("Step",Flags);
            foreach(int member in new[]{541,542,543,544,545,628})
            {
                var state=new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,member,member),Style=member==541?102:member==628?119:103};
                Require((bool)known.Invoke(null,new object[]{state}),"Exact terrain family member "+member);
                var other=state;other.Identity=new NpcIdentity(1,new object(),16,1,224,224);
                Require(!(bool)known.Invoke(null,new object[]{other}),"Shared terrain style cannot admit unrelated member "+member);
                object[] args={state,new PredictionEnvironment(),null,false,PredictionStop.None};
                Require(!(bool)action.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.MissingDependency,"Required sampled facts cannot default to zero/false "+member);
            }
            Console.WriteLine("PASS terrain exact family / explicit required facts boundaries");
        }
        internal static void Lifecycle(object context)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");long session=(long)Get(host,"Session");
            MethodInfo read=source.GetType().GetMethod("Read",Flags),action=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcTerrainFlightMotion",true).GetMethod("Step",Flags);
            MethodInfo capture=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);
            var terrain=(IPredictionTerrain)Get(source,"Terrain");int oldMode=Main.netMode,cases=0;
            try
            {
                NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.netMode=1;
                Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true};var player=Main.LocalPlayer;player.position=new Vector2(1100,2300);
                foreach(int member in new[]{541,542,543,544,545})foreach(int variant in new[]{0,1,2,3})
                {
                    PrepareTerrain(member,variant);
                    var actor=Main.npc[16]=new NPC{whoAmI=16};actor.SetDefaults(member);actor.active=true;actor.target=Main.myPlayer;actor.direction=1;actor.directionY=1;
                    actor.position=new Vector2(1700,member==541?2300:2260);actor.velocity=new Vector2(-2,variant==2?-.98f:.5f);actor.oldVelocity=new Vector2(-4,2);actor.collideX=variant==3;actor.collideY=variant==3;
                    actor.ai[0]=member==541?(variant==0?0:variant==1?53:-40):0;actor.ai[2]=member==541?0:29;actor.life=variant==2?actor.lifeMax/4:actor.lifeMax;
                    player.velocity=new Vector2(variant==1?-4:2,variant==2?-2:0);terrain.Reset();var state=(NpcMotionState)read.Invoke(null,new object[]{actor,session});
                    object[] captured={actor,state,terrain};capture.Invoke(null,captured);state=(NpcMotionState)captured[1];
                    var environment=new PredictionEnvironment{PlayerIndex=Main.myPlayer,PlayerX=player.Center.X,PlayerY=player.Center.Y,PlayerWidth=player.width,PlayerHeight=player.height,PlayerVelocityCaptured=true,PlayerVx=player.velocity.X,PlayerVy=player.velocity.Y,Multiplayer=true};
                    actor.AI();object[] args={state,environment,terrain,false,PredictionStop.None};
                    Require((bool)action.Invoke(null,args),"Terrain motor native scalar action "+member+" variant="+variant+" stop="+args[4]);state=(NpcMotionState)args[0];
                    Require(Math.Abs(state.Vx-actor.velocity.X)<.00001 && Math.Abs(state.Vy-actor.velocity.Y)<.00001 && state.A0==actor.ai[0] && state.A1==actor.ai[1] && state.A2==actor.ai[2] && state.L0==actor.localAI[0] && state.L1==actor.localAI[1] && state.L2==actor.localAI[2] && state.Direction==actor.direction && state.DirectionY==actor.directionY,
                        "Terrain native motor/clock/facing exact action "+member+" variant="+variant+" predictedV="+state.Vx+","+state.Vy+" actualV="+actor.velocity+" predictedA="+state.A0+","+state.A1+","+state.A2+" actualA="+actor.ai[0]+","+actor.ai[1]+","+actor.ai[2]);cases++;
                }
                // The server's action54 reads the real player's X velocity
                // before deciding near placement versus deterministic -200.
                Main.netMode=0;
                foreach(float velocity in new[]{-12f,12f})
                {
                    Projectile.ClearAll();var actor=Main.npc[16]=new NPC{whoAmI=16};actor.SetDefaults(541);actor.active=true;actor.target=Main.myPlayer;actor.direction=-1;actor.position=new Vector2(1700,2300);actor.velocity=new Vector2(-2,.5f);actor.ai[0]=53;
                    player.position=new Vector2(1100,2300);player.velocity=new Vector2(velocity,0);terrain.Reset();var state=(NpcMotionState)read.Invoke(null,new object[]{actor,session});
                    object[] captured={actor,state,terrain};capture.Invoke(null,captured);state=(NpcMotionState)captured[1];
                    var environment=new PredictionEnvironment{PlayerIndex=Main.myPlayer,PlayerX=player.Center.X,PlayerY=player.Center.Y,PlayerWidth=player.width,PlayerHeight=player.height,PlayerVelocityCaptured=true,PlayerVx=velocity};
                    actor.AI();object[] args={state,environment,terrain,false,PredictionStop.None};Require((bool)action.Invoke(null,args),"Elemental server action54 motor continues through attack placement RNG.");state=(NpcMotionState)args[0];
                    Require(state.A0==actor.ai[0] && state.A0==(velocity<0?-200:54) && state.Vx==actor.velocity.X && state.Vy==actor.velocity.Y,"Original server action54 uses real velocity for near/far cooldown branch.");cases++;
                }
                WindyOracle(read,action,session);
                Console.WriteLine("PASS original terrain scalar motor/clock/collision boundaries cases="+cases);
            }
            finally{Main.netMode=oldMode;}
        }
        private static void WindyOracle(MethodInfo read,MethodInfo action,long session)
        {
            var windy=typeof(Main).GetMethod("UpdateWindyDayState",Flags);var retained=typeof(Main).GetField("_shouldUseWindyDayMusic",Flags);
            float oldWind=Main.windSpeedTarget,oldCloud=Main.cloudAlpha;double oldTime=Main.time;bool oldDay=Main.dayTime,oldRetained=(bool)retained.GetValue(null);
            try
            {
                Main.dayTime=true;Main.time=20000;Main.cloudAlpha=0;
                foreach(bool seed in new[]{false,true})
                {
                    Main.windSpeedTarget=seed?.5f:.2f;windy.Invoke(weatherOracle,null);
                    Main.windSpeedTarget=.37f;windy.Invoke(weatherOracle,null);
                    Require(Main.IsItAHappyWindyDay==seed,"Original happy-wind hysteresis cannot be inferred from .37 wind alone.");
                    var actor=Main.npc[16]=new NPC{whoAmI=16};actor.SetDefaults(628);actor.active=true;actor.target=Main.myPlayer;actor.timeLeft=123;actor.velocity=new Vector2(2,3);
                    var state=(NpcMotionState)read.Invoke(null,new object[]{actor,session});actor.AI();object[] args={state,new PredictionEnvironment{HappyWindyCaptured=true,HappyWindy=Main.IsItAHappyWindyDay},null,false,PredictionStop.None};
                    Require((bool)action.Invoke(null,args),"Dandelion uses the observed retained wind fact.");state=(NpcMotionState)args[0];
                    Require(state.TimeLeft==actor.timeLeft && state.Vx==actor.velocity.X && state.Vy==actor.velocity.Y,"Original Dandelion lifetime clamp/body retains actual velocity.");
                }
                Console.WriteLine("PASS original happy-wind hysteresis / Dandelion body-lifetime oracle");
            }
            finally{Main.windSpeedTarget=oldWind;Main.cloudAlpha=oldCloud;Main.time=oldTime;Main.dayTime=oldDay;retained.SetValue(null,oldRetained);}
        }
        // Full original update schedule and production selection/publication.
        // Frozen paths cross pursuit/jump/cooldown, rather than being replaced
        // by fresh static NPCs every time their real direction changes.
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");Require(GetOptional(source,"Native")==null,"Terrain controllers use the default rolling producer.");
            var rows=new List<string>{"scene,frame,type,tick,phase,x,y,vx,vy,playerVx,playerVy,selected,published,count,stop,matched,longMatches,maxError,jumps,transitions,playerX,playerY,ai2,wet"};
            var failures=new List<string>();int scenes=0,totalMatches=0,totalLong=0;
            foreach(int type in new[]{541,542,543,544,545,628})foreach(int variant in type==628?new[]{0,1,2}:new[]{0,1,2,3})
            {
                string scene=type+"-terrain-"+variant;scenes++;
                try
                {
                    NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.GameMode=0;Main.remixWorld=variant==3;Main.dayTime=type==628 && variant!=0;Main.time=20000;Main.cloudAlpha=0;Main.windSpeedTarget=Main.windSpeedCurrent=variant==2?.37f:variant==0?.2f:.5f;
                    typeof(Main).GetMethod("UpdateWindyDayState",Flags).Invoke(weatherOracle,null);
                    PrepareTerrain(type,variant);
                    Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};var p=Main.LocalPlayer;
                    // Keep the upward-player counterexample and pure-water branch,
                    // but leave >150 approach room when the native terrain exit occurs.
                    p.position=new Vector2(type==541 && variant==3?400:type>=542 && type<=545 && variant>=2?900:1100,2400-p.height);p.fallStart=p.fallStart2=(int)(p.position.Y/16);p.statLife=p.statLifeMax=p.statLifeMax2=5000;p.creativeGodMode=true;p.immune=true;p.immuneTime=100000;
                    for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=100000;for(int i=0;i<3;i++)p.armor[i].SetDefaults(696+i);
                    if(variant==2){p.position.Y-=100;p.velocity=new Vector2(2,-6);}
                    int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1700,type>=542 && type<=545?2272:2300,type,Start:16,Target:Main.myPlayer);var actor=Main.npc[slot];actor.velocity=new Vector2(-2,.5f);actor.timeLeft=750;
                    if(type==541){actor.ai[0]=variant==0?0:variant==3?53:-100;if(variant==2)actor.life=actor.lifeMax/4;}
                    if(type>=542 && type<=545)actor.ai[2]=29;
                    if(type==628)actor.velocity=new Vector2(2,1);
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                    var captures=new List<NpcTrajectory>();int matches=0,longMatches=0,published=0,selectedCount=0,jumps=0,transitions=0;double maximum=0;float lastClock=actor.ai[0],lastJump=actor.ai[2];
                    for(int frame=0;frame<240 && actor.active;frame++)
                    {
                        NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();if(!actor.active)break;
                        if(actor.ai[2]<0 && lastJump>=0)jumps++;if(Math.Sign(actor.ai[0])!=Math.Sign(lastClock))transitions++;lastClock=actor.ai[0];lastJump=actor.ai[2];
                        var selected=(NpcIdentity)Get(Get(host,"Selection"),"Target");bool has=(bool)Get(Get(host,"Selection"),"HasTarget") && ReferenceEquals(selected.Token,actor);if(has)selectedCount++;
                        var path=NativeCombatPresentationChecks.CurrentPath(host);if(path!=null){Require(has && path.Identity.Equals(selected) && path.SampleTick==Main.GameUpdateCount,"Fresh selected terrain path "+scene);published++;if(published==1 || frame%15==0)captures.Add(path);}
                        for(int i=captures.Count-1;i>=0;i--)
                        {
                            var frozen=captures[i];int offset=(int)((long)Main.GameUpdateCount-frozen.SampleTick);if(offset>=frozen.Count){captures.RemoveAt(i);continue;}if(offset<=0)continue;
                            var point=frozen[offset];double error=Vector2.Distance(new Vector2(point.Bounds.X,point.Bounds.Y),actor.position);maximum=Math.Max(maximum,error);matches++;if(offset>=30)longMatches++;
                            Require(point.NoTileCollide==actor.noTileCollide && error<=.08,"Frozen native terrain motor "+scene+" offset="+offset+" error="+error.ToString("R",CultureInfo.InvariantCulture)+" predicted="+point.Bounds.X+","+point.Bounds.Y+" pv="+point.Vx+","+point.Vy+" actual="+actor.position+" av="+actor.velocity+" ai="+actor.ai[0]+","+actor.ai[1]+","+actor.ai[2]+" player="+p.position+" playerV="+p.velocity+" stop="+frozen.Stop);
                        }
                        rows.Add(string.Join(",",scene,frame,type,Main.GameUpdateCount,actor.ai[0],actor.position.X,actor.position.Y,actor.velocity.X,actor.velocity.Y,p.velocity.X,p.velocity.Y,has,path!=null,path?.Count??0,path?.Stop.ToString()??"none",matches,longMatches,maximum,jumps,transitions,p.position.X,p.position.Y,actor.ai[2],actor.wet));
                    }
                    Require(selectedCount>0 && published>0 && longMatches>=30,"Continuing terrain mechanism must publish and match >=30futureactions "+scene+" selected="+selectedCount+" published="+published+" matched="+matches+" long="+longMatches);
                    if(type==541)Require(transitions>0,"Elemental natural attack/cooldown transition "+scene);
                    if(type>=542 && type<=545 && variant!=1)Require(jumps>0,"Sandshark natural leaving-terrain jump "+scene);
                    totalMatches+=matches;totalLong+=longMatches;Console.WriteLine("TERRAIN scene="+scene+" matched="+matches+" long="+longMatches+" maxError="+maximum+" jumps="+jumps+" transitions="+transitions);
                }
                catch(InvalidOperationException failure){failures.Add(scene+": "+failure.Message);Console.WriteLine("TERRAIN FAIL "+scene+": "+failure.Message);}
            }
            Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"terrain-flight-continuous.csv"),rows);
            Require(failures.Count==0,"Native terrain natural scenes failed="+failures.Count+": "+string.Join(" | ",failures));
            Console.WriteLine("PASS original terrain default continuous scenes="+scenes+" matched="+totalMatches+" long="+totalLong);
        }
        private static void PrepareTerrain(int type,int variant)
        {
            if(type>=542 && type<=545)
            {
                ushort tileType=variant==1?(ushort)TileID.Stone:(ushort)(type==542?TileID.Sand:type==543?TileID.Ebonsand:type==544?TileID.Crimsand:TileID.Pearlsand);
                for(int x=75;x<=120;x++)for(int y=136;y<=150;y++){Main.tile[x,y].active(true);Main.tile[x,y].type=tileType;}
                if(variant==3)for(int x=75;x<=120;x++)for(int y=136;y<150;y++){Main.tile[x,y].active(false);Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(0);}
            }
            if(type==541 && variant==2)for(int x=80;x<115;x++)for(int y=143;y<150;y++){Main.tile[x,y].active(false);Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(0);}
        }
        internal static void Umbrella(object context,Action step,string output)
        {
            object source=Get(Get(context,"CombatObservation"),"Prediction");var read=source.GetType().GetMethod("ReadPlayer",Flags);var terrain=(IPredictionTerrain)Activator.CreateInstance(Get(source,"Terrain").GetType(),true);
            var rows=new List<string>{"item,case,frame,animation,predictedAnimation,x,y,vx,vy,predictedX,predictedY,predictedVy,error"};int matches=0;
            try
            {
                foreach(int item in new[]{946,4707})foreach(int variant in new[]{0,1,2,3,4})
                {
                    NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.GameMode=0;Main.remixWorld=false;
                    Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};var p=Main.LocalPlayer;p.inventory[0].SetDefaults(item);p.selectedItemState.Select(0);
                    p.position=new Vector2(1100,1800);p.velocity=new Vector2(0,4);p.fallStart=p.fallStart2=(int)(p.position.Y/16);p.statLife=p.statLifeMax=p.statLifeMax2=5000;p.creativeGodMode=true;
                    if(variant==2)p.controlDown=true;
                    if(variant==3){p.gravDir=-1;p.velocity.Y=-4;p.AddBuff(BuffID.Gravitation,600);}
                    if(variant==4){p.inventory[0].autoReuse=true;p.autoReuseAllWeapons=true;}
                    step();Require(p.itemAnimation==0,"Original umbrella starts in idle hold.");
                    if(variant==1 || variant==4){p.controlUseItem=true;p.releaseUseItem=true;}
                    var motion=(PredictionPlayerMotion)read.Invoke(null,new object[]{p});Require(motion.UmbrellaHold && !motion.Complex,"Known umbrella motor remains an ordinary supported player premise.");
                    var environment=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface};int sawAttack=0,sawIdleAfter=0;
                    for(int frame=0;frame<52;frame++)
                    {
                        // Both consume identical already sampled held controls.
                        // The prediction advances no ItemCheck or live player.
                        PredictionStop stop;terrain.Reset();Require(PlayerMotionContinuation.Advance(ref motion,environment,terrain,out stop),"Umbrella owned continuation item="+item+" case="+variant+" stop="+stop);step();
                        if(p.itemAnimation>0)sawAttack++;else if(sawAttack>0)sawIdleAfter++;
                        double error=Vector2.Distance(p.position,new Vector2(motion.X,motion.Y));
                        rows.Add(string.Join(",",item,variant,frame,p.itemAnimation,motion.UmbrellaAnimation,p.position.X,p.position.Y,p.velocity.X,p.velocity.Y,motion.X,motion.Y,motion.Vy,error));
                        Require(error<=.08 && Math.Abs(p.velocity.Y-motion.Vy)<=.0001 && p.itemAnimation==motion.UmbrellaAnimation,"Original umbrella movement-before-item-tail item="+item+" case="+variant+" frame="+frame+" error="+error+" actualV="+p.velocity+" predictedV="+motion.Vx+","+motion.Vy+" actualAnimation="+p.itemAnimation+" predictedAnimation="+motion.UmbrellaAnimation);matches++;
                    }
                    if(variant==1)Require(sawAttack>0 && sawIdleAfter>0,"Original first-click attack returns to idle hold cap.");
                    if(variant==4)Require(sawAttack>22,"Native autoReuse observes multiple attacks without simulated item/attack actions.");
                }
                // Pulley skips only the hold tail. This original ItemCheck
                // oracle does not substitute fake rope movement for the
                // separately retained existing natural rope evidence.
                var pulley=Main.LocalPlayer;pulley.inventory[0].SetDefaults(946);pulley.itemAnimation=0;pulley.controlUseItem=false;pulley.pulley=true;pulley.gravDir=1;pulley.velocity.Y=4;
                var tail=(PredictionPlayerMotion)read.Invoke(null,new object[]{pulley});PlayerMotionContinuation.CompleteItemCheck(ref tail);pulley.ItemCheck();
                Require(pulley.velocity.Y==tail.Vy && tail.Vy==4,"Original pulley umbrella bypasses hold cap.");
                foreach(bool click in new[]{false,true})
                {
                    pulley.pulley=false;pulley.itemAnimation=0;pulley.controlUseItem=click;pulley.releaseUseItem=true;pulley.velocity.Y=4;
                    tail=(PredictionPlayerMotion)read.Invoke(null,new object[]{pulley});Vector2 position=pulley.position;
                    PlayerMotionContinuation.CompleteItemCheck(ref tail);pulley.ItemCheck();
                    Require(pulley.position==position && tail.X==position.X && tail.Y==position.Y && tail.Vy==pulley.velocity.Y && tail.UmbrellaAnimation==pulley.itemAnimation,
                        "Already-completed player step performs only original ItemCheck tail, idle/first-click="+click);
                }
                Console.WriteLine("PASS original umbrella idle / first-click / attack-to-idle / Down / reverse / continuous reuse player updates matches="+matches+" plus pulley ItemCheck tail oracle");
            }
            finally{Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"umbrella-player-continuation.csv"),rows);}
        }
    }
}
