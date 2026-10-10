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
    internal static class NativeCombatQueenBeeChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Action<NPC> before,after;
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        internal static void Boundaries()
        {
            var model=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcQueenBeeMotion",true);var kernel=model.GetMethod("Step",Flags);var distance=model.GetMethod("Distance",Flags);
            for(int i=1;i<=8192;i++)
            {float x=((i*7919)%100007-50003)/31f,y=((i*3571)%90007-45003)/29f;Require((float)distance.Invoke(null,new object[]{x,y})==new Vector2(x,y).Length(),"Native QueenBee distance normalization root index="+i);}
            var n=new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,222,222),Style=43,Active=true,Width=200,Height=120,X=1000,Y=2000,Life=1000,LifeMax=1000,TargetCaptured=true,HasPlayer=true,PlayerActive=true,Target=0,PlayerIndex=0,Direction=1,TimeLeft=750,A0=-1};
            var e=new PredictionEnvironment{PlayerIndex=0,PlayerX=1110,PlayerY=2060,PlayerWidth=20,PlayerHeight=42,Jungle=true,GravityWorldSurface=100,WorldWidth=400,WorldHeight=240};object[] args={n,e,new BoundaryTerrain(true),1,PredictionStop.None,null,0,false};
            Require(!(bool)kernel.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.RandomDecision,"Real server attack selection stops before Next(3).");
            e.Multiplayer=true;args[1]=e;Require((bool)kernel.Invoke(null,args) && ((NpcMotionState)args[0]).A0==-1,"Client observed selection wait has its actual inert motor.");e.Multiplayer=false;args[1]=e;
            n.A0=1;n.Vx=1;n.Vy=2;args[0]=n;Require((bool)kernel.Invoke(null,args) && ((NpcMotionState)args[0]).Vx==n.Vx*.9f,"All20 clear close origins prove native damping independently of emission RNG.");
            args[0]=n;args[2]=new BoundaryTerrain(false);Require(!(bool)kernel.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.RandomDecision,"Blocked origin comparison crosses both raw pursuit signs and is genuine motor randomness.");
            e.PlayerX=2000;args[0]=n;args[1]=e;Require((bool)kernel.Invoke(null,args),"All20 distant blocked origins share one exact raw-component acceleration.");
            n.A0=3;e.PlayerX=1200;e.PlayerY=2300;args[0]=n;args[1]=e;args[2]=new BoundaryTerrain(true);Require((bool)kernel.Invoke(null,args),"Clear ordinary stinger motor remains known across projectile-only RNG.");
            var count=model.GetMethod("Nearby",Flags);var areas=new[]{new MotionRect(1100,2000,20,42),new MotionRect(1150,2000,20,42),new MotionRect(1200,2000,20,42)};e.Players=new PredictionPlayers(areas,3,new[]{0,1,2},areas,new[]{true,false,false},new[]{false,false,true});object[] counts={n,e,1,0,PredictionStop.None};
            Require((bool)count.Invoke(null,counts) && (int)counts[3]==2,"Native active nondead count includes ghosts and excludes actual dead players.");counts[2]=2;Require(!(bool)count.Invoke(null,counts) && (PredictionStop)counts[4]==PredictionStop.MissingDependency,"A future second live/ghost player's unknown motion is not a frozen clock-count fact.");
            n.A0=0;n.A1=1;n.A2=1;n.Vx=6;n.Vy=0;n.HasClosestPlayer=true;n.ClosestPlayerIndex=0;n.ClosestPlayerArea=n.PlayerArea=new MotionRect(1100,2000,20,42);
            e.Players=new PredictionPlayers(areas,3,new[]{0,1,2},areas,new[]{true,true,false},new[]{false,false,true});
            args[0]=n;args[1]=e;args[2]=new BoundaryTerrain(true);args[3]=2;
            Require(!(bool)kernel.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.MissingDependency,"Actual future charge slowdown needs a new multiplayer winner; captured Closest is not that winner.");
            n.A2=0;n.Vx=1;args[0]=n;
            Require((bool)kernel.Invoke(null,args),"The known charge prefix before its real reselect action remains available with two players.");
            Console.WriteLine("PASS QueenBee native8192roots/server-client/20-origin proof/ghost/dead/multiplayer boundaries");
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");long session=(long)Get(host,"Session");var read=source.GetType().GetMethod("Read",Flags);var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);var terrain=(IPredictionTerrain)Get(source,"Terrain");
            var rows=new List<string>{"scene,frame,phase,clock,charges,life,target,selected,published,count,stop,matched,long,cross,actions,random,receiving,error"};var failures=new List<string>();
            var observer=new Harmony("JueMingR.Probe.QueenBeeOriginalActions");var update=typeof(NPC).GetMethod("UpdateNPC",Flags);observer.Patch(update,prefix:new HarmonyMethod(typeof(NativeCombatQueenBeeChecks).GetMethod(nameof(Before),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatQueenBeeChecks).GetMethod(nameof(After),Flags)));
            bool oldGood=Main.getGoodWorld;int totalMatches=0,totalActions=0,totalRandom=0,totalRestored=0;var allPhases=new HashSet<int>();
            try
            {
                for(int scene=0;scene<8;scene++)
                {
                    try
                    {
                        Setup(context,scene);int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),scene==5?5700:1800,2100,NPCID.QueenBee,Start:16,Target:scene==0?255:Main.myPlayer);var actor=Main.npc[slot];Require(actor.active && actor.aiStyle==43,"Real QueenBee defaults enter the locked AI43 family.");
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                        var freezes=new List<NpcTrajectory>();var phases=new Dictionary<NpcTrajectory,string>();var targets=new HashSet<int>();int actions=0,random=0,restored=0,matched=0,longMatches=0,cross=0,published=0,selected=0,receiving=0,retired=0,farActions=0,deadActions=0;bool awaiting=false;double maxError=0,tangent=0;
                        NPC pending=null;NpcMotionState input=default(NpcMotionState),predicted=default(NpcMotionState);bool valid=false;PredictionStop stop=PredictionStop.None;
                        before=n=>
                        {
                            pending=n;valid=false;if(!ReferenceEquals(n,actor) || !n.active)return;
                            input=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] args={n,input,terrain};tracking.Invoke(null,args);predicted=input=(NpcMotionState)args[1];terrain.Reset();valid=NpcMotion.Step(ref predicted,null,0,EnvironmentFor(n),terrain,1,true,out stop);
                            if(!valid)
                            {
                                if(stop==PredictionStop.RandomDecision){random++;awaiting=true;}
                                else Require(stop==PredictionStop.Despawn || stop==PredictionStop.MissingDependency,"Only actual QueenBee dependencies or native random motor end this sampled action: "+stop+" phase="+string.Join("/",n.ai)+" pos="+n.position);
                            }
                        };
                        after=n=>
                        {
                            if(!ReferenceEquals(n,pending) || !ReferenceEquals(n,actor))return;if(!valid){if(stop==PredictionStop.Despawn && !n.active)retired++;return;}
                            double error=Vector2.Distance(new Vector2(predicted.X,predicted.Y),n.position);
                            Require(error<=.08 && Math.Abs(predicted.Vx-n.velocity.X)<=.0001 && Math.Abs(predicted.Vy-n.velocity.Y)<=.0001 && predicted.A0==n.ai[0] && predicted.A1==n.ai[1] && predicted.A2==n.ai[2] && predicted.L0==n.localAI[0] && predicted.Direction==n.direction && predicted.SpriteDirection==n.spriteDirection && predicted.Health.Defense==n.defense && predicted.CanReceive==(!n.dontTakeDamage && !n.immortal),"Original QueenBee action scene="+scene+" error="+error+" phase="+string.Join("/",n.ai)+" predicted="+predicted.A0+"/"+predicted.A1+"/"+predicted.A2+" V="+predicted.Vx.ToString("R")+","+predicted.Vy.ToString("R")+" actualV="+n.velocity+" target="+n.target+" p="+Main.player[n.target].position);
                            actions++;allPhases.Add((int)input.A0);targets.Add(n.target);if(input.A0==4)farActions++;if(input.A0==5)deadActions++;if(awaiting){restored++;awaiting=false;}
                        };
                        for(int frame=0;frame<3200 && actor.active;frame++)
                        {
                            bool changed=false;
                            if(scene==2 && (frame==400 || frame==900 || frame==1400 || frame==1900))
                            {float fraction=frame==400?.74f:frame==900?.49f:frame==1400?.24f:.09f;actor.StrikeNPCNoInteraction(actor.life-(int)(actor.lifeMax*fraction)+actor.defense+20,0,0);Require(actor.active && actor.life<actor.lifeMax*fraction,"Actual receiving hit crosses an Expert charge/count/defense threshold.");changed=true;}
                            if(scene==6 && frame==1200){Main.LocalPlayer.KillMe(Terraria.DataStructures.PlayerDeathReason.ByCustomReason("isolated QueenBee retreat"),1000000,0);Require(Main.LocalPlayer.dead,"Original player death changes the actual retreat branch.");changed=true;}
                            if(scene==7 && frame==600){Main.player[1].Teleport(new Vector2(2700,2400-Main.player[1].height));Main.player[1].ResetFallDamage();changed=true;}
                            if(changed){freezes.Clear();phases.Clear();}
                            NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();if(!actor.active)break;
                            if(!actor.dontTakeDamage)receiving++;bool chosen=(bool)Get(Get(host,"Selection"),"HasTarget") && ReferenceEquals(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Token,actor);if(chosen)selected++;
                            string phase=actor.ai[0]+"/"+actor.ai[1]+"/"+actor.ai[2];var path=cache.Read(0);
                            if(path!=null && ReferenceEquals(path.Identity.Token,actor) && !freezes.Contains(path)){published++;if(frame%5==0 && path.Count>1){freezes.Add(path);phases[path]=phase;}}
                            for(int i=freezes.Count-1;i>=0;i--)
                            {
                                var frozen=freezes[i];int offset=(int)(Main.GameUpdateCount-frozen.CaptureTick);if(offset>=frozen.Count){freezes.RemoveAt(i);phases.Remove(frozen);continue;}if(offset<1)continue;
                                var point=frozen[offset];double error=Vector2.Distance(new Vector2(point.Bounds.X,point.Bounds.Y),actor.position);maxError=Math.Max(maxError,error);matched++;
                                Require(error<=.08 && point.CanReceive==(!actor.dontTakeDamage && !actor.immortal) && point.NoTileCollide==actor.noTileCollide,"Frozen QueenBee scene="+scene+" frame="+frame+" offset="+offset+" error="+error+" predicted="+point.Bounds.X+","+point.Bounds.Y+" actual="+actor.position+" phase="+phase+" target="+actor.target+" stop="+frozen.Stop);
                                if(offset>=30){longMatches++;tangent=Math.Max(tangent,Vector2.Distance(actor.position,new Vector2(frozen[0].Bounds.X+frozen[0].Vx*offset,frozen[0].Bounds.Y+frozen[0].Vy*offset)));}if(phases[frozen]!=phase)cross++;
                            }
                            rows.Add(string.Join(",",scene,frame,actor.ai[0],actor.ai[1],actor.ai[2],actor.life,actor.target,chosen,path!=null,path?.Count??0,path?.Stop.ToString()??"none",matched,longMatches,cross,actions,random,receiving,maxError.ToString("R",CultureInfo.InvariantCulture)));
                        }
                        before=after=null;Require(actions>=300 && receiving>=300,"Natural receiving QueenBee retains meaningful original actions scene="+scene+" actions="+actions);
                        if(scene!=7)Require(selected>0 && published>0 && longMatches>=30 && cross>=30 && tangent>8,"Natural QueenBee known prefixes curve and cross original clocks through long futures scene="+scene+" selected="+selected+" published="+published+" long="+longMatches+" cross="+cross);
                        if(scene<=4)Require(random>0 && restored>0,"Genuine motor selection ends a prefix then the next real sample restores it.");
                        if(scene==5)Require(farActions>100,"Actual distant birth crosses the phase4 return motor.");if(scene==6)Require(deadActions>100 || retired>0,"Actual dead player drives the original phase5 retreat.");if(scene==7)Require(targets.Count>1,"The actual two-player arena changes numbered targets after a legal player teleport.");
                        totalMatches+=matched;totalActions+=actions;totalRandom+=random;totalRestored+=restored;
                        Console.WriteLine("QUEEN-BEE scene="+scene+" actions="+actions+" matched="+matched+" long="+longMatches+" cross="+cross+" random="+random+" restored="+restored+" far="+farActions+" dead="+deadActions+" retired="+retired+" error="+maxError);
                    }
                    catch(Exception failure) when(!(failure is OutOfMemoryException)){before=after=null;failures.Add(scene+": "+failure.Message);Console.WriteLine("QUEEN-BEE FAIL scene="+scene+": "+failure);}
                }
                Require(failures.Count==0 && allPhases.Contains(0) && allPhases.Contains(1) && allPhases.Contains(2) && allPhases.Contains(3) && allPhases.Contains(4) && allPhases.Contains(5),"All natural QueenBee attack/return/retreat phases and scenes remain required: "+string.Join(" | ",failures)+" phases="+string.Join("/",allPhases));
                Console.WriteLine("PASS QueenBee natural scenes=8 matched="+totalMatches+" actions="+totalActions+" random="+totalRandom+" restored="+totalRestored);
            }
            finally{before=after=null;Main.getGoodWorld=oldGood;observer.Unpatch(update,HarmonyPatchType.All,observer.Id);Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"queen-bee-continuous.csv"),rows);}
        }
        private static PredictionEnvironment EnvironmentFor(NPC n)
        {
            var p=Main.player[n.target>=0 && n.target<Main.player.Length?n.target:255];var areas=new List<MotionRect>();var precise=new List<MotionRect>();var slots=new List<int>();var alive=new List<bool>();var dead=new List<bool>();
            for(int i=0;i<255;i++)if(Main.player[i].active){var peer=Main.player[i];var r=peer.Hitbox;areas.Add(new MotionRect(r.X,r.Y,r.Width,r.Height));precise.Add(new MotionRect(peer.position.X,peer.position.Y,peer.width,peer.height));slots.Add(i);alive.Add(!peer.dead && !peer.ghost);dead.Add(peer.dead);}
            return new PredictionEnvironment{Players=new PredictionPlayers(areas.ToArray(),areas.Count,slots.ToArray(),precise.ToArray(),alive.ToArray(),dead.ToArray()),PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerVx=p.velocity.X,PlayerVy=p.velocity.Y,PlayerVelocityCaptured=true,PlayerTimelineActive=p.active && !p.dead && !p.ghost,PlayerDead=p.dead || !p.active,Jungle=p.ZoneJungle,Expert=Main.expertMode,GoodWorld=Main.getGoodWorld,Day=Main.dayTime,Multiplayer=Main.netMode==1,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,GravityWorldSurface=Main.worldSurface,RockLayer=(float)Main.rockLayer};
        }
        private static void Setup(object context,int scene)
        {
            NativeCombatLiveContextChecks.FlightWorld();
            // Native Jungle excludes Underworld. Preserve the real floor150
            // but move UnderworldLayer to200 by extending the actual world,
            // rather than asserting a biome flag in a 240-high hell arena.
            var oldTiles=Main.tile;Main.maxTilesY=400;Main.bottomWorld=Main.maxTilesY*16;Main.tile=new Tile[Main.maxTilesX,Main.maxTilesY];
            for(int x=0;x<Main.maxTilesX;x++)for(int y=0;y<Main.maxTilesY;y++){if(y<oldTiles.GetLength(1))Main.tile[x,y]=oldTiles[x,y];else{var tile=new Tile();tile.active(true);tile.type=TileID.Stone;Main.tile[x,y]=tile;}}
            Main.Map=new Terraria.Map.WorldMap(Main.maxTilesX,Main.maxTilesY);NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.GameMode=scene==0?0:1;Main.dayTime=scene==4;Main.time=1800;Main.getGoodWorld=scene==3;
            if(Main.ItemDropSolver==null){Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);}for(int i=0;i<Main.item.Length;i++)Main.item[i].whoAmI=i;
            if(scene!=4)for(int x=0;x<Main.maxTilesX;x++)for(int y=150;y<170;y++)Main.tile[x,y].type=TileID.JungleGrass;
            if(scene==4)for(int y=105;y<150;y++){Main.tile[95,y].active(true);Main.tile[95,y].type=TileID.Stone;}
            for(int i=0;i<255;i++)Main.player[i].active=false;
            PlayerAt(Main.myPlayer,1100);if(scene==7)PlayerAt(1,1700);
            Main.PlayerSceneMetrics.Reset();Main.LocalPlayer.UpdateSceneMetrics();Main.LocalPlayer.UpdateBiomes();Require(Main.LocalPlayer.ZoneJungle==(scene!=4),"Native biome scan establishes the real QueenBee enrage premise.");
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions(path:false,mouseCenter:true,clearLine:false,radius:50));
        }
        private static void PlayerAt(int index,float x)
        {
            var p=Main.player[index]=new Player{whoAmI=index,active=true,isControlledByFilm=true,releaseJump=true};p.position=new Vector2(x,2400-p.height);p.statLife=p.statLifeMax=p.statLifeMax2=5000;p.immune=true;p.immuneTime=1000000;p.ResetFallDamage();for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
        }
        private sealed class BoundaryTerrain:IPredictionTerrain
        {
            private readonly bool clear;internal BoundaryTerrain(bool value){clear=value;}
            public bool CanHit(MotionRect source,MotionRect target,out bool result,out PredictionStop stop){result=clear;stop=PredictionStop.None;return true;}
            public bool Solid(MotionRect area,out bool result,out PredictionStop stop){result=false;stop=PredictionStop.None;return true;}
            public bool Tile(int x,int y,out PredictionTile tile,out PredictionStop stop){tile=default(PredictionTile);stop=PredictionStop.None;return true;}
            public bool Move(ref NpcMotionState state,PredictionEnvironment environment,out PredictionStop stop){stop=PredictionStop.None;return true;}
            public bool Unchanged{get{return true;}}public void Reset(){}
        }
    }
}
