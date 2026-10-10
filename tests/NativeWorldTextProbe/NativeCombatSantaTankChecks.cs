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
    internal static class NativeCombatSantaTankChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Action<NPC> before,after;
        private static Action<Player,double,int> hurt;
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static void Hurt(Player __instance,double __result,int Damage){if(__result>0)hurt?.Invoke(__instance,__result,Damage);}
        internal static void Boundaries()
        {
            var type=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcSantaTankMotion",true);var known=type.GetMethod("Known",Flags);
            Require((bool)known.Invoke(null,new object[]{new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,346,346),Style=61}}),"Santa-NK1 uses its complete AI61 motor.");
            Require(!(bool)known.Invoke(null,new object[]{new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,347,347),Style=61}}),"Other event IDs do not borrow AI61.");
            Console.WriteLine("PASS exact AI61 member and adjacent-mechanism boundary");
        }
        internal static void Lifecycle(object context)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");long session=(long)Get(host,"Session");var read=source.GetType().GetMethod("Read",Flags);
            var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);
            var action=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcSantaTankMotion",true).GetMethod("Step",Flags);var terrain=(IPredictionTerrain)Get(source,"Terrain");int cases=0;
            foreach(float lifeRatio in new[]{1f,.75f,.5f,.25f,.24f})foreach(int variant in new[]{0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15})
            {
                World();var p=Main.LocalPlayer;var actor=Main.npc[16]=new NPC{whoAmI=16};actor.SetDefaults(346);actor.active=true;actor.target=Main.myPlayer;
                actor.life=(int)(actor.lifeMax*lifeRatio);actor.position=new Vector2(1200.25f,2400-actor.height);actor.direction=actor.spriteDirection=-1;actor.velocity=new Vector2(-2.375f,variant==10?-.2f:0);
                if(variant==1)actor.ai[1]=299;
                if(variant==2 || variant==3 || variant==4){actor.ai[0]=1;actor.ai[1]=variant==3?240:variant==4?239:0;}
                if(variant==5 || variant==6){Main.dayTime=true;actor.velocity.X=variant==5?0:2.375f;}
                if(variant==7)p.position.X=actor.Center.X+49.75f-p.width*.5f;
                if(variant==8){p.position.X=actor.position.X+actor.width*.5f-p.width*.5f;p.position.Y=2420;}
                if(variant==9 || variant==10 || variant==14)for(int x=74;x<84;x++){Main.tile[x,149].active(true);Main.tile[x,149].type=1;if(variant==14)Main.tile[x,149].halfBrick(true);}
                if(variant==11)actor.StrikeNPCNoInteraction(1,0,0);
                if(variant==12){actor.AddBuff(BuffID.Confused,600);actor.confused=true;}
                if(variant==13)actor.target=255;
                if(variant==15){Main.netMode=1;actor.ai[1]=299;}
                terrain.Reset();var state=(NpcMotionState)read.Invoke(null,new object[]{actor,session});object[] tracked={actor,state,terrain};tracking.Invoke(null,tracked);state=(NpcMotionState)tracked[1];
                object[] args={state,Environment(p),terrain,0,PredictionStop.None,new[]{state},1,variant==12};Require((bool)action.Invoke(null,args),"Known native AI61 scalar action continues ratio="+lifeRatio+" variant="+variant+" stop="+args[4]);state=(NpcMotionState)args[0];actor.AI();
                Require(state.X==actor.position.X && state.Y==actor.position.Y && state.Vx==actor.velocity.X && state.Vy==actor.velocity.Y && state.A0==actor.ai[0] && state.A1==actor.ai[1] && state.Direction==actor.direction && state.NoGravity==actor.noGravity && state.NoTileCollide==actor.noTileCollide && state.TimeLeft==actor.timeLeft,
                    "Original AI61 scalar ratio="+lifeRatio+" variant="+variant+" V="+state.Vx.ToString("R")+","+state.Vy.ToString("R")+"/"+actor.velocity.X.ToString("R")+","+actor.velocity.Y.ToString("R")+" phase="+state.A0+","+state.A1+"/"+actor.ai[0]+","+actor.ai[1]+" lifetime="+state.TimeLeft+"/"+actor.timeLeft+" XY="+state.X+","+state.Y+"/"+actor.position+" facing="+state.Direction+"/"+actor.direction+" noGravity="+state.NoGravity+"/"+actor.noGravity+" noTile="+state.NoTileCollide+"/"+actor.noTileCollide);cases++;
            }
            Console.WriteLine("PASS original AI61 scalar receiving-phase/life/terrain/day/target matrix cases="+cases);
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");Require(GetOptional(source,"Native")==null,"AI61 default prediction is the synchronous scalar producer.");
            var read=source.GetType().GetMethod("Read",Flags);var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);long session=(long)Get(host,"Session");var terrain=(IPredictionTerrain)Get(source,"Terrain");
            var oracle=new Harmony("JueMingR.Tests.SantaNativeSlotAction");oracle.Patch(typeof(NPC).GetMethod("UpdateNPC",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSantaTankChecks),nameof(Before)),postfix:new HarmonyMethod(typeof(NativeCombatSantaTankChecks),nameof(After)));
            oracle.Patch(typeof(Player).GetMethod("Hurt",Flags,null,new[]{typeof(Terraria.DataStructures.PlayerDeathReason),typeof(int),typeof(int),typeof(bool),typeof(bool),typeof(bool),typeof(int),typeof(bool)},null),postfix:new HarmonyMethod(typeof(NativeCombatSantaTankChecks),nameof(Hurt)));
            try
            {
            var rows=new List<string>{"scene,frame,life,a0,a1,x,y,vx,vy,selected,published,count,stop,matched,longMatched,cross"};var failures=new List<string>();int total=0,totalLong=0,totalCross=0;
            foreach(int scene in new[]{0,1,2,3,4,5})
            {
                try
                {
                    World();Main.snowMoon=true;Main.remixWorld=scene==5;var p=Main.LocalPlayer;p.position=new Vector2(1700,2400-p.height);p.ResetFallDamage();p.isControlledByFilm=true;p.releaseJump=true;p.immune=true;p.immuneTime=1000000;p.statLife=p.statLifeMax=p.statLifeMax2=5000;for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
                    if(scene==3)for(int x=100;x<138;x++)for(int y=147;y<150;y++){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}
                    int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1400,2400,346,Start:16,Target:Main.myPlayer);var actor=Main.npc[slot];
                    NpcMotionState predictedAction=default(NpcMotionState),beforeAction=default(NpcMotionState);bool valid=false;int actionMatches=0;
                    hurt=(player,damage,requested)=>{if(ReferenceEquals(player,p))Console.WriteLine("SANTA PLAYER_HURT scene="+scene+" tick="+Main.GameUpdateCount+" damage="+damage+" requested="+requested+" hp="+player.statLife+" dead="+player.dead+" position="+player.position+" velocity="+player.velocity+" fall="+player.fallStart+","+player.fallStart2+" immune="+player.immune+","+player.immuneTime);};
                    before=n=>
                    {
                        valid=false;if(!ReferenceEquals(n,actor) || !n.active)return;
                        terrain.Reset();predictedAction=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] facts={n,predictedAction,terrain};tracking.Invoke(null,facts);predictedAction=(NpcMotionState)facts[1];var group=new[]{predictedAction};var e=Environment(p);e.PlayerTimelineActive=true;PredictionStop stop;
                        beforeAction=predictedAction;valid=NpcMotion.Step(ref predictedAction,group,1,e,terrain,1,true,out stop);Require(valid || stop==PredictionStop.Despawn,"Current original AI61 dependencies scene="+scene+" stop="+stop);
                    };
                    after=n=>
                    {
                        if(!ReferenceEquals(n,actor) || !valid)return;
                        double error=Vector2.Distance(n.position,new Vector2(predictedAction.X,predictedAction.Y));
                        if(error>.002 || Math.Abs(predictedAction.Vy-n.velocity.Y)>.0001)
                        {var box=new MotionRect(beforeAction.Bounds.CenterX-40,beforeAction.Y+beforeAction.Height-20,80,20);bool solid;PredictionStop queryStop;bool known=terrain.Solid(box,out solid,out queryStop);Console.WriteLine("SANTA FOOT-FIRST-DIFF tick="+Main.GameUpdateCount+" before="+beforeAction.X+","+beforeAction.Y+" pv="+beforeAction.Vx+","+beforeAction.Vy+" foot="+box.X.ToString("R")+","+box.Y.ToString("R")+","+(box.Y+box.Height).ToString("R")+" host="+known+":"+solid+":"+queryStop+" native="+Collision.SolidCollision(new Vector2(box.X,box.Y),80,20));}
                        Require(error<=.002 && Math.Abs(predictedAction.Vx-n.velocity.X)<=.0001 && Math.Abs(predictedAction.Vy-n.velocity.Y)<=.0001 && predictedAction.A0==n.ai[0] && predictedAction.A1==n.ai[1] && predictedAction.Direction==n.direction,"Current original AI61 slot scene="+scene+" error="+error+" predicted="+predictedAction.X+","+predictedAction.Y+" pv="+predictedAction.Vx+","+predictedAction.Vy+" actual="+n.position+" av="+n.velocity+" phase="+n.ai[0]+","+n.ai[1]+" direction="+predictedAction.Direction+"/"+n.direction+" player="+p.position+" pv="+p.velocity);actionMatches++;
                    };
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));var frozen=new List<Frozen>();int selected=0,published=0,matches=0,longMatches=0,cross=0,changes=0,afterHit=0;bool hit=false,retired=false;float phase=actor.ai[0];double maxError=0,tangent=0;
                    for(int frame=0;frame<1800;frame++)
                    {
                        if(frame%240==0 && !(scene>=4 && frame>=1200)){p.controlRight=frame%480==0;p.controlLeft=!p.controlRight;frozen.Clear();}
                        if(frame==700)
                        {
                            float ratio=scene==1?.70f:scene==2?.45f:scene==3?.20f:.90f;
                            actor.StrikeNPCNoInteraction(actor.life-(int)(actor.lifeMax*ratio)+actor.defense+10,0,0);Require(actor.active && actor.life>0 && actor.life<actor.lifeMax*ratio,"Real native Santa hit crosses the requested life band without killing it.");hit=true;frozen.Clear();
                        }
                        if((scene==4 || scene==5) && frame==1200){Main.dayTime=true;p.controlLeft=p.controlRight=false;frozen.Clear();}
                        if(scene>=4 && frame==1500)
                        {
                            Require(actor.active && actor.timeLeft>10 && Math.Abs(actor.Center.X-p.Center.X)<50,"Native daytime near-player pause is kept alive by CheckActive, rather than retiring on a fixed day deadline.");
                            Console.WriteLine("SANTA DAY-NEAR scene="+scene+" timeLeft="+actor.timeLeft+" centerDistance="+Math.Abs(actor.Center.X-p.Center.X)+" actor="+actor.position+" player="+p.position);
                            p.Teleport(new Vector2(640,p.position.Y));p.ResetFallDamage();frozen.Clear();
                            Console.WriteLine("SANTA DAY-FAR-NEW-SAMPLE scene="+scene+" timeLeft="+actor.timeLeft+" centerDistance="+Math.Abs(actor.Center.X-p.Center.X)+" actor="+actor.position+" player="+p.position);
                        }
                        NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();if(!actor.active){retired=true;Console.WriteLine("SANTA RETIRED scene="+scene+" frame="+frame+" timeLeft="+actor.timeLeft+" centerDistance="+Math.Abs(actor.Center.X-p.Center.X));break;}
                        if(phase!=actor.ai[0]){phase=actor.ai[0];changes++;}
                        bool has=(bool)Get(Get(host,"Selection"),"HasTarget") && ReferenceEquals(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Token,actor);if(has)selected++;
                        var path=cache.Read(0);if(path!=null && ReferenceEquals(path.Identity.Token,actor)){Require(has && path.CaptureTick==Main.GameUpdateCount,"AI61 actual selection and publication share the current sample.");published++;if(frame%5==0 && path.Count>1)frozen.Add(new Frozen(path,actor.ai[0]));}
                        for(int i=frozen.Count-1;i>=0;i--)
                        {
                            var item=frozen[i];int offset=(int)((long)Main.GameUpdateCount-item.Path.CaptureTick);if(offset>=item.Path.Count){frozen.RemoveAt(i);continue;}if(offset<=0)continue;
                            var point=item.Path[offset];double error=Vector2.Distance(actor.position,new Vector2(point.Bounds.X,point.Bounds.Y));maxError=Math.Max(maxError,error);matches++;if(offset>=30){longMatches++;tangent=Math.Max(tangent,Vector2.Distance(actor.position,new Vector2(item.Path[0].Bounds.X+item.Path[0].Vx*offset,item.Path[0].Bounds.Y+item.Path[0].Vy*offset)));}if(item.Phase!=actor.ai[0])cross++;if(hit)afterHit++;
                            Require(error<=.08 && point.NoTileCollide==actor.noTileCollide && point.CanReceive==(!actor.dontTakeDamage && !actor.immortal),"Frozen original AI61 scene="+scene+" frame="+frame+" offset="+offset+" error="+error.ToString("R",CultureInfo.InvariantCulture)+" predicted="+point.Bounds.X+","+point.Bounds.Y+" actual="+actor.position+" av="+actor.velocity+" phase="+actor.ai[0]+","+actor.ai[1]+" direction="+actor.direction+" target="+actor.target+" targetRect="+actor.targetRect+" player="+p.position+" pv="+p.velocity+" hp="+p.statLife+" dead="+p.dead+" immune="+p.immune+","+p.immuneTime+" fall="+p.fallStart+","+p.fallStart2+" stop="+item.Path.Stop);
                        }
                        rows.Add(string.Join(",",scene,frame,actor.life,actor.ai[0],actor.ai[1],actor.position.X,actor.position.Y,actor.velocity.X,actor.velocity.Y,has,path!=null,path?.Count??0,path?.Stop.ToString()??"none",matches,longMatches,cross));
                    }
                    Require(selected>0 && published>0 && matches>=30 && longMatches>=30 && cross>=30 && changes>=3 && tangent>2,"Natural AI61 receiving pauses and resumed chase have real curved long forecasts scene="+scene+" matched="+matches+" long="+longMatches+" cross="+cross+" changes="+changes+" tangent="+tangent);
                    Require(hit && afterHit>=30,"AI61 real-hit recovery has newly sampled futures.");if(scene==4 || scene==5)Require(retired,"Actual day exit, including remix, reaches native retirement.");
                    Require(actionMatches>=300,"Current AI61 actions are separately checked against actual native slots.");
                    total+=matches;totalLong+=longMatches;totalCross+=cross;Console.WriteLine("PASS original AI61 natural scene="+scene+" matched="+matches+" long="+longMatches+" cross="+cross+" maxError="+maxError.ToString("R",CultureInfo.InvariantCulture));
                }
                catch(Exception error){failures.Add("scene"+scene+":"+error.Message);Console.WriteLine("FAIL original AI61 natural scene="+scene+" "+error);}
                finally{before=after=null;hurt=null;}
            }
            Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"santa-tank-scenes.csv"),rows);Require(failures.Count==0,"AI61 complete natural matrix: "+string.Join(" | ",failures));Console.WriteLine("PASS AI61 complete six-scene coverage matched="+total+" long="+totalLong+" cross="+totalCross);
            }
            finally{before=after=null;hurt=null;oracle.UnpatchAll(oracle.Id);}
        }
        private sealed class Frozen
        {internal readonly NpcTrajectory Path;internal readonly float Phase;internal Frozen(NpcTrajectory path,float phase){Path=path;Phase=phase;}}
        private static void World()
        {NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.dayTime=false;Main.remixWorld=false;Main.Map=new Terraria.Map.WorldMap(Main.maxTilesX,Main.maxTilesY);Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true};Main.LocalPlayer.position=new Vector2(1100,2400-Main.LocalPlayer.height);}
        private static PredictionEnvironment Environment(Player p)
        {return new PredictionEnvironment{Players=new PredictionPlayers(new[]{new MotionRect((int)p.position.X,(int)p.position.Y,p.width,p.height)},1,new[]{p.whoAmI}),PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerVx=p.velocity.X,PlayerVy=p.velocity.Y,PlayerVelocityCaptured=true,GravityWorldSurface=Main.worldSurface,WorldSurface=(float)Main.worldSurface,Day=Main.dayTime,Expert=Main.expertMode,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,Multiplayer=Main.netMode==1,Remix=Main.remixWorld};}
    }
}
