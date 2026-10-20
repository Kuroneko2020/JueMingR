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
    internal static class NativeCombatKingSlimeChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Action<NPC> before,after;
        private static Action<NPC> checkBefore,checkAfter;
        private static Action<int,int,PredictionStop> terrainFailure;
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static void CheckBefore(NPC __instance){checkBefore?.Invoke(__instance);}
        private static void CheckAfter(NPC __instance){checkAfter?.Invoke(__instance);}
        private static void TerrainFailure(int x,int y,PredictionStop stop,bool __result){if(!__result)terrainFailure?.Invoke(x,y,stop);}
        internal static void Boundaries()
        {
            var old=new Player[255];for(int i=0;i<255;i++){old[i]=Main.player[i];Main.player[i]=new Player{whoAmI=i};}
            var closest=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcKingSlimeMotion",true).GetMethod("ClosestBottom",Flags);
            int cases=0;
            try
            {
                // Exercise the ORIGINAL public FindClosest inputs, including
                // nondead ghosts and the first-active fallback when all dead.
                foreach(bool dead in new[]{false,true})foreach(bool ghost in new[]{false,true})foreach(bool tie in new[]{false,true})
                {
                    Main.player[3].active=Main.player[7].active=true;Main.player[3].dead=Main.player[7].dead=dead;Main.player[3].ghost=ghost;
                    Main.player[3].position=new Vector2(102.25f,205.5f);Main.player[7].position=tie?Main.player[3].position:new Vector2(502.5f,705.25f);
                    var position=new Vector2(302.5f,405.75f);int winner=Player.FindClosest(position,99,93);var p=Main.player[winner];
                    var areas=new[]{new MotionRect(Main.player[3].position.X,Main.player[3].position.Y,Main.player[3].width,Main.player[3].height),new MotionRect(Main.player[7].position.X,Main.player[7].position.Y,Main.player[7].width,Main.player[7].height)};
                    var e=new PredictionEnvironment{Players=new PredictionPlayers(areas,2,new[]{3,7},areas,new[]{!dead && !ghost,!dead},new[]{dead,dead}),PlayerIndex=winner,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerTimelineActive=!dead};
                    var n=new NpcMotionState{X=position.X,Y=position.Y,Width=99,Height=93};object[] args={n,e,0,PredictionStop.None};
                    Require((bool)closest.Invoke(null,args),"King current fallback has a complete native candidate set.");n=(NpcMotionState)args[0];Require(n.L1==p.Bottom.X && n.L2==p.Bottom.Y,"Original FindClosest native dead/ghost/tie/first-active semantics.");cases++;
                }
            }
            finally{for(int i=0;i<255;i++)Main.player[i]=old[i];}
            Console.WriteLine("PASS King Slime original FindClosest candidate boundaries="+cases);
        }
        internal static void Lifecycle(object context)
        {
            // Cache construction is a pure original tile/visibility query,
            // not another AI or RNG action. Both models receive the same real
            // tile values; native determines whether a legal choice exists.
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");long session=(long)Get(host,"Session");
            var read=source.GetType().GetMethod("Read",Flags);var terrain=(IPredictionTerrain)Get(source,"Terrain");
            var build=typeof(NPC).GetMethod("BuildKingSlimeTeleportCache",Flags);var size=typeof(NPC).GetField("kingSlimePointCacheSize",Flags);var points=typeof(NPC).GetField("kingSlimePointCache",Flags);
            var model=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcKingSlimeMotion",true).GetMethod("Cache",Flags);
            for(int variant=0;variant<9;variant++)
            {
                Setup(context,0);Main.LocalPlayer.position=new Vector2(1600-Main.LocalPlayer.width*.5f,1600-Main.LocalPlayer.height*.5f);
                int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1200,1600,NPCID.KingSlime,Start:16,Target:Main.myPlayer);var actor=Main.npc[slot];
                var tile=Main.tile[93,100];if(variant!=0){tile.active(true);tile.type=variant==3?TileID.Platforms:TileID.Stone;}
                if(variant==2){Main.tile[107,100].active(true);Main.tile[107,100].type=TileID.Stone;}
                if(variant==4)tile.liquidType(1);
                if(variant==5)tile.inActive(true);
                if(variant>=6){var above=Main.tile[93,99];above.active(true);above.type=TileID.Stone;if(variant==7)above.halfBrick(true);if(variant==8)above.slope(1);}
                bool native=(bool)build.Invoke(actor,new object[]{100,100,10,7});int count=(int)size.GetValue(null);var nativePoints=(Point[])points.GetValue(null);
                var state=(NpcMotionState)read.Invoke(null,new object[]{actor,session});var e=EnvironmentFor(actor);terrain.Reset();object[] args={state,e,terrain,10,7,false,PredictionStop.None};bool valid=(bool)model.Invoke(null,args);bool found=(bool)args[5];PredictionStop stop=(PredictionStop)args[6];
                bool distinct=false;for(int i=1;i<count;i++)if(nativePoints[i]!=nativePoints[0])distinct=true;
                Require(valid==!distinct && (distinct?stop==PredictionStop.RandomDestination:found==native),"Original King legal cache empty/single/multiple/platform/lava/actuation/above-half-slope variant="+variant+" count="+count+" stop="+stop);
                if(valid && found){state=(NpcMotionState)args[0];Require(state.L1==nativePoints[0].X*16+8 && state.L2==nativePoints[0].Y*16,"A singleton native cache has one exact landing point.");}
            }
            Console.WriteLine("PASS King Slime original pure cache/Lava/Line lifecycle cases=9");
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");long session=(long)Get(host,"Session");
            var read=source.GetType().GetMethod("Read",Flags);var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);
            var terrain=(IPredictionTerrain)Activator.CreateInstance(Get(source,"Terrain").GetType(),true);var rows=new List<string>{"scene,frame,hop,clock,teleport,selected,published,count,stop,matched,longMatched,error,actions,random,timeLeft,playerDead,playerRespawn,playerX,playerY"};var failures=new List<string>();
            bool oldGood=Main.getGoodWorld;var observer=new Harmony("JueMingR.Probe.KingSlimeOracle");var update=typeof(NPC).GetMethod("UpdateNPC",Flags);
            observer.Patch(update,prefix:new HarmonyMethod(typeof(NativeCombatKingSlimeChecks).GetMethod(nameof(Before),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatKingSlimeChecks).GetMethod(nameof(After),Flags)));
            var checkActive=typeof(NPC).GetMethod("CheckActive",Flags);var cellAt=terrain.GetType().GetMethod("CellAt",Flags);
            observer.Patch(checkActive,prefix:new HarmonyMethod(typeof(NativeCombatKingSlimeChecks).GetMethod(nameof(CheckBefore),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatKingSlimeChecks).GetMethod(nameof(CheckAfter),Flags)));
            observer.Patch(cellAt,postfix:new HarmonyMethod(typeof(NativeCombatKingSlimeChecks).GetMethod(nameof(TerrainFailure),Flags)));
            int totalActions=0,totalMatches=0,totalRandom=0;
            try
            {
                for(int scene=0;scene<6;scene++)
                {
                    try
                    {
                        Setup(context,scene);int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),scene==5?5700:1800,2400,NPCID.KingSlime,Start:16,Target:Main.myPlayer);var actor=Main.npc[slot];
                        Require(actor.active && actor.aiStyle==15,"Genuine King Slime birth enters original AI15.");
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                        var freezes=new List<NpcTrajectory>();var phases=new Dictionary<NpcTrajectory,int>();var observed=new HashSet<int>();int actions=0,random=0,matched=0,longMatches=0,cross=0,published=0,selectedCount=0,teleports=0,retired=0;double maxError=0,tangent=0;
                        NPC pending=null;NpcMotionState predicted=default(NpcMotionState);bool valid=false;PredictionStop actionStop=PredictionStop.None;
                        var failedCells=new HashSet<string>();int nativeChecks=0,nativeCheckLeft=0;bool nativeCheckActiveBefore=false;
                        checkBefore=n=>{if(!ReferenceEquals(n,actor))return;nativeChecks++;nativeCheckLeft=n.timeLeft;nativeCheckActiveBefore=n.active;};
                        checkAfter=n=>{if(scene==4 && ReferenceEquals(n,actor) && (nativeCheckLeft<=10 || !n.active))Console.WriteLine("KING DEAD-NATIVE-CHECK tick="+Main.GameUpdateCount+" calls="+nativeChecks+" active="+nativeCheckActiveBefore+"->"+n.active+" left="+nativeCheckLeft+"->"+n.timeLeft+" pos="+n.position+" size="+n.width+","+n.height+" phase="+string.Join("/",n.ai));};
                        before=n=>
                        {
                            // The independent action oracle must not reset Source's live cells.
                            terrain.Reset();
                            pending=n;valid=false;if(!ReferenceEquals(n,actor) || !n.active)return;
                            predicted=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] args={n,predicted,terrain};tracking.Invoke(null,args);predicted=(NpcMotionState)args[1];
                            var e=EnvironmentFor(n);terrain.Reset();
                            terrainFailure=scene==4?(Action<int,int,PredictionStop>)((x,y,stop)=>{string key=x+":"+y+":"+n.width+":"+n.height;if(failedCells.Add(key))Console.WriteLine("KING DEAD-TERRAIN-FIRST-CELL x="+x+" y="+y+" stop="+stop+" actor="+n.position+" size="+n.width+","+n.height+" world="+Main.maxTilesX+","+Main.maxTilesY+" stack="+System.Environment.StackTrace.Replace('\r',' ').Replace('\n',' '));}):null;
                            try{valid=NpcMotion.Step(ref predicted,null,0,e,terrain,1,true,out actionStop);}finally{terrainFailure=null;}
                            if(!valid){if(actionStop==PredictionStop.RandomDestination)random++;else {if(scene==4 && (actionStop==PredictionStop.TerrainUnavailable || actionStop==PredictionStop.TerrainLimit))Console.WriteLine("KING DEAD-TERRAIN stop="+actionStop+" position="+n.position+" velocity="+n.velocity+" size="+n.width+","+n.height+" phase="+string.Join("/",n.ai)+" timeLeft="+n.timeLeft);Require(actionStop==PredictionStop.Despawn || actionStop==PredictionStop.MissingDependency || actionStop==PredictionStop.TerrainUnavailable || actionStop==PredictionStop.TerrainLimit,"Explicit King action boundary: "+actionStop);}}
                        };
                        after=n=>
                        {
                            if(!ReferenceEquals(n,pending))return;
                            if(!valid){if(actionStop==PredictionStop.Despawn && !n.active)retired++;return;}
                            double error=Vector2.Distance(new Vector2(predicted.X,predicted.Y),n.position);
                            Require(error<=.08 && Math.Abs(predicted.Vx-n.velocity.X)<=.0001 && Math.Abs(predicted.Vy-n.velocity.Y)<=.0001 && predicted.Width==n.width && predicted.Height==n.height && predicted.Scale==n.scale && predicted.A0==n.ai[0] && predicted.A1==n.ai[1] && predicted.A2==n.ai[2] && predicted.A3==n.ai[3] && predicted.L0==n.localAI[0] && predicted.L1==n.localAI[1] && predicted.L2==n.localAI[2] && predicted.Active==n.active && predicted.Health.DontTakeDamage==n.dontTakeDamage,"Original King action scene="+scene+" error="+error+" predictedV="+predicted.Vx+","+predicted.Vy+" actual="+n.velocity+" phase="+string.Join("/",n.ai)+" local="+string.Join("/",n.localAI));actions++;if(predicted.NewSegment)teleports++;
                        };
                        for(int frame=0;frame<1100 && actor.active;frame++)
                        {
                            if(scene<3 && frame==400){actor.StrikeNPCNoInteraction(actor.life-(int)(actor.lifeMax*.08f)+actor.defense+10,0,0);Require(actor.active && actor.life>0 && actor.life<actor.lifeMax*.1,"Original nonlethal hit reaches every health-based jump clock and resize threshold.");freezes.Clear();}
                            if(scene==4 && frame==400){Main.LocalPlayer.KillMe(Terraria.DataStructures.PlayerDeathReason.ByOther(0),1000000,0);Require(Main.LocalPlayer.dead,"Actual player death establishes King retreat.");freezes.Clear();}
                            NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();if(!actor.active)break;
                            observed.Add((int)actor.ai[1]);bool selected=(bool)Get(Get(host,"Selection"),"HasTarget") && ReferenceEquals(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Token,actor);if(selected)selectedCount++;
                            var path=NativeCombatPresentationChecks.CurrentPath(host);if(path!=null && ReferenceEquals(path.Identity.Token,actor)){Require(selected && path.SampleTick==Main.GameUpdateCount,"Actual King identity owns a fresh production window.");published++;if(frame%5==0 && path.Count>1){freezes.Add(path);phases[path]=(int)actor.ai[1];}}
                            for(int i=freezes.Count-1;i>=0;i--)
                            {
                                var frozen=freezes[i];int offset=(int)((long)Main.GameUpdateCount-frozen.SampleTick);if(offset>=frozen.Count){freezes.RemoveAt(i);continue;}if(offset<=0)continue;
                                var point=frozen[offset];double error=Vector2.Distance(new Vector2(point.Bounds.X,point.Bounds.Y),actor.position);maxError=Math.Max(maxError,error);matched++;
                                Require(error<=.08 && point.CanReceive==(!actor.dontTakeDamage && !actor.immortal),"Frozen original King scene="+scene+" offset="+offset+" error="+error+" phase="+string.Join("/",actor.ai)+" predicted="+point.Bounds.X+","+point.Bounds.Y+" actual="+actor.position+" stop="+frozen.Stop);
                                if(offset>=30){longMatches++;tangent=Math.Max(tangent,Vector2.Distance(actor.position,new Vector2(frozen[0].Bounds.X+frozen[0].Vx*offset,frozen[0].Bounds.Y+frozen[0].Vy*offset)));}if(phases[frozen]!=(int)actor.ai[1])cross++;
                            }
                            rows.Add(string.Join(",",scene,frame,actor.ai[1],actor.ai[0],actor.ai[2],selected,path!=null,path?.Count??0,path?.Stop.ToString()??"none",matched,longMatches,maxError.ToString("R",CultureInfo.InvariantCulture),actions,random,actor.timeLeft,Main.LocalPlayer.dead,Main.LocalPlayer.respawnTimer,Main.LocalPlayer.position.X,Main.LocalPlayer.position.Y));
                        }
                        before=after=null;
                        if(scene==5)Require(!actor.active && retired>0,"Far target follows the original encourage-despawn and exact retirement action.");
                        else
                        {
                            Require(actions>=300 && published>0 && selectedCount>0 && longMatches>=30 && tangent>8 && cross>=30,"Native King has long curved full-production futures across real jump or teleport phases: "+scene+" actions="+actions+" pub="+published+" long="+longMatches+" cross="+cross);
                            if(scene==3)Require(observed.Contains(5) && observed.Contains(6) && teleports>0,"High real player platform naturally reaches shrink, teleport and recovery.");
                            if(scene==4)Require(!actor.active && retired>0,"Actual dead player drives the original retreat retirement: active="+actor.active+" retiredOracle="+retired+" nativeChecks="+nativeChecks+" actor="+actor.position+" phase="+string.Join("/",actor.ai)+" timeLeft="+actor.timeLeft+" dead="+Main.LocalPlayer.dead+" respawn="+Main.LocalPlayer.respawnTimer+" player="+Main.LocalPlayer.position);
                        }
                        totalActions+=actions;totalMatches+=matched;totalRandom+=random;Console.WriteLine("KING scene="+scene+" actions="+actions+" pub="+published+" matched="+matched+" long="+longMatches+" cross="+cross+" random="+random+" teleports="+teleports+" retired="+retired+" error="+maxError+" phases="+string.Join(";",observed));
                    }
                    catch(Exception failure) when(!(failure is OutOfMemoryException)){before=after=checkBefore=checkAfter=null;terrainFailure=null;failures.Add(scene+": "+failure.Message);Console.WriteLine("KING FAIL scene="+scene+": "+failure);}
                }
                Require(failures.Count==0,"Original King Slime failures="+failures.Count+": "+string.Join(" | ",failures));Console.WriteLine("PASS King Slime original natural scenes=6 actions="+totalActions+" matched="+totalMatches+" random="+totalRandom);
            }
            finally{before=after=checkBefore=checkAfter=null;terrainFailure=null;observer.Unpatch(update,HarmonyPatchType.All,observer.Id);observer.Unpatch(checkActive,HarmonyPatchType.All,observer.Id);observer.Unpatch(cellAt,HarmonyPatchType.All,observer.Id);Main.getGoodWorld=oldGood;Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"king-slime-continuous.csv"),rows);}
        }
        private static PredictionEnvironment EnvironmentFor(NPC n)
        {
            var areas=new List<MotionRect>();var exact=new List<MotionRect>();var slots=new List<int>();var alive=new List<bool>();var dead=new List<bool>();
            for(int i=0;i<255;i++)if(Main.player[i].active){var p=Main.player[i];var box=p.Hitbox;areas.Add(new MotionRect(box.X,box.Y,box.Width,box.Height));exact.Add(new MotionRect(p.position.X,p.position.Y,p.width,p.height));slots.Add(i);alive.Add(!p.dead && !p.ghost);dead.Add(p.dead);}
            var target=Main.player[n.target>=0 && n.target<255?n.target:Main.myPlayer];
            return new PredictionEnvironment{Players=new PredictionPlayers(areas.ToArray(),areas.Count,slots.ToArray(),exact.ToArray(),alive.ToArray(),dead.ToArray()),PlayerIndex=target.whoAmI,PlayerX=target.Center.X,PlayerY=target.Center.Y,PlayerWidth=target.width,PlayerHeight=target.height,PlayerVx=target.velocity.X,PlayerVy=target.velocity.Y,PlayerVelocityCaptured=true,PlayerTimelineActive=target.active && !target.dead && !target.ghost,PlayerDead=target.dead || !target.active,Expert=Main.expertMode,GoodWorld=Main.getGoodWorld,Multiplayer=Main.netMode==1,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,GravityWorldSurface=Main.worldSurface,RockLayer=(float)Main.rockLayer};
        }
        private static void Setup(object context,int scene)
        {
            NativeCombatLiveContextChecks.FlightWorld();Main.Map=new Terraria.Map.WorldMap(Main.maxTilesX,Main.maxTilesY);NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.GameMode=scene==0?0:1;Main.dayTime=false;Main.time=1800;Main.getGoodWorld=scene==2;
            if(Main.ItemDropSolver==null){Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);}
            for(int i=0;i<255;i++)Main.player[i].active=false;
            Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};var p=Main.LocalPlayer;p.position=new Vector2(1100,(scene==3?2000:2400)-p.height);p.statLife=p.statLifeMax=p.statLifeMax2=5000;p.creativeGodMode=true;p.immune=true;p.immuneTime=1000000;p.fallStart=p.fallStart2=(int)p.position.Y/16;
            if(scene==3)for(int x=20;x<Main.maxTilesX-20;x++){Main.tile[x,125].active(true);Main.tile[x,125].type=TileID.Platforms;}
            for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions(path:false,mouseCenter:true,clearLine:false,radius:50));
        }
    }
}
