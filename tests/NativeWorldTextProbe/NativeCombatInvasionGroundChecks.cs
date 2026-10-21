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
using Terraria.GameContent.Events;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatInvasionGroundChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Action<NPC> beforeAction,afterAction;
        private struct HurtSample
        {internal int Life,FallStart,FallStart2,ImmuneTime;internal bool Dead,Immune;internal Vector2 Position,Velocity;}
        private static Action<Player,double,Terraria.DataStructures.PlayerDeathReason,int,int,HurtSample> receivedHit;
        private static void Before(NPC __instance){beforeAction?.Invoke(__instance);}
        private static void After(NPC __instance){afterAction?.Invoke(__instance);}
        private static void BeforeHurt(Player __instance,out HurtSample __state)
        {__state=new HurtSample{Life=__instance.statLife,Dead=__instance.dead,Immune=__instance.immune,ImmuneTime=__instance.immuneTime,FallStart=__instance.fallStart,FallStart2=__instance.fallStart2,Position=__instance.position,Velocity=__instance.velocity};}
        private static void Hurt(Player __instance,double __result,Terraria.DataStructures.PlayerDeathReason damageSource,int Damage,int hitDirection,HurtSample __state)
        {if(__result>0)receivedHit?.Invoke(__instance,__result,damageSource,Damage,hitDirection,__state);}
        private static readonly int[] Members={552,553,554,555,556,557,561,562,563,566,567,568,569,570,571,572,573,576,577,620};
        internal static void Boundaries()
        {
            var kernel=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcInvasionGroundMotion",true);
            var known=kernel.GetMethod("Known",Flags);foreach(int type in Members)Require((bool)known.Invoke(null,new object[]{new NpcMotionState{Identity=new NpcIdentity(1,new object(),0,1,type,type),Style=107}}),"Every locked AI107 member routes through the full ground action: "+type);
            Require(!(bool)known.Invoke(null,new object[]{new NpcMotionState{Identity=new NpcIdentity(1,new object(),0,1,564,564),Style=107}}),"Dark Mage AI106 is not a walker merely because its nearby IDs share the event.");
            var n=new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,552,552),Style=107,Active=true,Target=255};object[] args={n,new PredictionEnvironment(),null,1,PredictionStop.None,null,0,false};
            Require(!(bool)kernel.GetMethod("Step",Flags).Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.MissingDependency,"An incomplete DD2 target relation is not an empty crystal/player search.");
            Console.WriteLine("PASS AI107 exact complete member set and missing target closure boundary");
        }
        internal static void Lifecycle(object context)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");long session=(long)Get(host,"Session");
            var read=source.GetType().GetMethod("Read",Flags);var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);
            var action=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcInvasionGroundMotion",true).GetMethod("Step",Flags);var terrain=(IPredictionTerrain)Get(source,"Terrain");int cases=0;
            CenterVisibility(terrain);
            Require(DD2Event.EnemiesShouldChasePlayers,"Locked DD2 chase getter is true.");
            foreach(int member in Members)foreach(int variant in member==620?new[]{0,1,2,3,4,5,6,7,8,9,10}:new[]{0,1,2,3,4,5,6,7})
            {
                NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.dayTime=variant==7;
                Main.Map=new Terraria.Map.WorldMap(Main.maxTilesX,Main.maxTilesY);
                Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true};var p=Main.LocalPlayer;p.position=new Vector2(1100,2358);
                var actor=Main.npc[16]=new NPC{whoAmI=16};actor.SetDefaults(member);actor.active=true;actor.target=Main.myPlayer;actor.position=new Vector2(1200,2400-actor.height);actor.direction=actor.spriteDirection=-1;actor.directionY=1;actor.velocity=new Vector2(variant==3?0:-1,0);actor.oldPosition=actor.position;
                actor.localAI[3]=variant==0?0:variant==1?59:variant==4?109:120;
                if(variant==2){actor.ai[0]=member==572 || member==573?1:30;actor.ai[1]=member==576 || member==577?2:40;}
                if(variant==3)actor.ai[3]=29;
                if(variant==4 && member==620){actor.wet=true;actor.velocity.Y=-1;for(int x=70;x<86;x++)for(int y=142;y<150;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(0);}}
                if(variant==5){actor.StrikeNPCNoInteraction(1,0,0);Require(actor.active && actor.justHit,"Real scalar fixture hit remains nonlethal.");}
                if(variant==6)for(int y=130;y<150;y++){Main.tile[73,y].active(true);Main.tile[73,y].type=1;}
                if(variant==7){p.position.Y=2000;for(int x=72;x<76;x++)for(int y=146;y<150;y++){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}}
                if(variant>=8)
                {
                    actor.position.X=1071.462f;actor.oldPosition=actor.position;actor.velocity.X=-.007986699f;actor.ai[0]=3;actor.ai[1]=1;
                    p.position.X=1078.8f;actor.oldTarget=actor.target;
                    if(variant==9)p.aggro=-1;if(variant==10){p.aggro=-1;p.itemAnimation=1;}
                }
                terrain.Reset();var group=Capture(read,tracking,terrain,session);int index=Find(group,actor);var state=group[index];var e=Environment(p,false);
                object[] args={state,e,terrain,0,PredictionStop.None,group,group.Length,false};Require((bool)action.Invoke(null,args),"Known native AI107 scalar action continues member="+member+" variant="+variant+" stop="+args[4]);state=(NpcMotionState)args[0];actor.AI();
                Require(state.X==actor.position.X && state.Y==actor.position.Y && state.Vx==actor.velocity.X && state.Vy==actor.velocity.Y && state.A0==actor.ai[0] && state.A1==actor.ai[1] && state.A2==actor.ai[2] && state.A3==actor.ai[3] && state.L2==actor.localAI[2] && state.L3==actor.localAI[3] && state.NoTileCollide==actor.noTileCollide && state.NoGravity==actor.noGravity && state.Health.DontTakeDamage==actor.dontTakeDamage && state.Direction==actor.direction && state.SpriteDirection==actor.spriteDirection && state.Alpha==actor.alpha,
                    "Original AI107 full motor member="+member+" variant="+variant+" XY="+state.X.ToString("R")+","+state.Y.ToString("R")+"/"+actor.position+" V="+state.Vx.ToString("R")+","+state.Vy.ToString("R")+"/"+actor.velocity+" A="+state.A0+","+state.A1+","+state.A2+","+state.A3+"/"+string.Join(",",actor.ai)+" L23="+state.L2+","+state.L3+"/"+actor.localAI[2]+","+actor.localAI[3]+" noTile="+state.NoTileCollide+"/"+actor.noTileCollide+" noGravity="+state.NoGravity+"/"+actor.noGravity+" facing="+state.Direction+","+state.SpriteDirection+"/"+actor.direction+","+actor.spriteDirection+" alpha="+state.Alpha+"/"+actor.alpha);cases++;
            }
            Console.WriteLine("PASS original AI107 walker/attack/stuck/portal/wet/day scalar matrix cases="+cases);
        }
        private static void CenterVisibility(IPredictionTerrain terrain)
        {
            NativeCombatLiveContextChecks.FlightWorld();int cases=0;
            foreach(bool wall in new[]{false,true})
            {
                for(int y=130;y<149;y++){Main.tile[73,y].active(wall);Main.tile[73,y].type=1;}
                foreach(float fraction in new[]{0f,.25f,.75f})foreach(int shift in new[]{0,15,16})
                {
                    var a=new Vector2(1100+shift+fraction,2300+fraction);var b=new Vector2(1300+fraction,2350+shift+fraction);
                    bool native=Collision.CanHit(a,0,0,b,0,0);Require(native==Collision.CanHit(a,1,1,b,1,1),"Native zero-center and nonempty integer-half representation agree.");
                    terrain.Reset();bool clear;PredictionStop stop;Require(terrain.CanHit(new MotionRect(a.X,a.Y,1,1),new MotionRect(b.X,b.Y,1,1),out clear,out stop) && clear==native,"AI107 center visibility acquires the exact native finite terrain path.");cases++;
                }
            }
            Console.WriteLine("PASS AI107 native center visibility representation cases="+cases);
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");Require(GetOptional(source,"Native")==null,"AI107 remains on the default synchronous scalar producer.");
            var read=source.GetType().GetMethod("Read",Flags);var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);var terrain=(IPredictionTerrain)Activator.CreateInstance(Get(source,"Terrain").GetType(),true);long session=(long)Get(host,"Session");
            var oracle=new Harmony("JueMingR.Tests.Ground107NativeAction");oracle.Patch(typeof(NPC).GetMethod("UpdateNPC",Flags),prefix:new HarmonyMethod(typeof(NativeCombatInvasionGroundChecks),nameof(Before)),postfix:new HarmonyMethod(typeof(NativeCombatInvasionGroundChecks),nameof(After)));
            oracle.Patch(typeof(Player).GetMethod("Hurt",Flags,null,new[]{typeof(Terraria.DataStructures.PlayerDeathReason),typeof(int),typeof(int),typeof(bool),typeof(bool),typeof(bool),typeof(int),typeof(bool)},null),prefix:new HarmonyMethod(typeof(NativeCombatInvasionGroundChecks),nameof(BeforeHurt)),postfix:new HarmonyMethod(typeof(NativeCombatInvasionGroundChecks),nameof(Hurt)));
            try
            {
            var rows=new List<string>{"scene,frame,type,a0,a1,a3,l3,x,y,vx,vy,wet,noTile,selected,published,count,stop,matched,longMatched,cross,phaseChanges,playerX,playerY,direction,target,attackCross"};var failures=new List<string>();int scenes=0,total=0,totalLong=0,totalCross=0;
            foreach(int member in Members)foreach(int mode in member==620?new[]{0,1,2,3}:new[]{0})
            {
                string scene="invasion-ground-"+member+"-"+mode;scenes++;
                try
                {
                    NPC actor;string birth;
                    if(member!=620)
                    {
                        int slot=NativeCombatDd2CoverageChecks.Birth(member,source.GetType().Assembly,out birth);actor=Main.npc[slot];
                        // Keep the real event/gate birth, then place the real
                        // player within native's 200px targeting distance.
                        // The old 1100-action lane never reached firing range;
                        // hurt cooldown alone cannot certify an attack phase.
                        var p=Main.LocalPlayer;p.isControlledByFilm=true;p.position.X=actor.Center.X+140;p.velocity=Vector2.Zero;p.controlLeft=p.controlRight=p.controlUp=p.controlDown=p.controlJump=false;p.releaseJump=true;p.ResetFallDamage();p.immune=true;p.immuneTime=1000000;
                        for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
                    }
                    else
                    {
                        NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();DD2Event.ResetProgressEntirely();Main.netMode=0;Main.dayTime=mode==3;Main.Map=new Terraria.Map.WorldMap(Main.maxTilesX,Main.maxTilesY);
                        Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};var p=Main.LocalPlayer;p.position=new Vector2(1100,2400-p.height);p.immune=true;p.immuneTime=1000000;p.statLife=p.statLifeMax=p.statLifeMax2=5000;for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
                        if(mode==1)for(int x=72;x<98;x++)for(int y=142;y<150;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(0);}
                        if(mode==2){p.position.Y=2200;for(int x=68;x<72;x++)for(int y=140;y<150;y++){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}}
                        // Native Player.Spawn resets fall clocks at its final
                        // spawn position. New Player alone has zero clocks.
                        p.ResetFallDamage();
                        int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1300,2400,620,Start:16,Target:Main.myPlayer);actor=Main.npc[slot];birth="OriginalGoblinSharkBirth";
                    }
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));var frozen=new List<Frozen>();int selected=0,published=0,matches=0,longMatches=0,cross=0,changes=0,attackCross=0,afterHit=0;float lastA0=actor.ai[0],lastA1=actor.ai[1];bool hit=false,sawWet=false,sawDry=false,sawAttack=false;double maximum=0;
                    NpcMotionState predictedAction=default(NpcMotionState);bool actionValid=false;int actionMatches=0;
                    receivedHit=(p,damage,reason,requested,direction,old)=>
                    {
                        if(!ReferenceEquals(p,Main.LocalPlayer))return;
                        Console.WriteLine("GROUND107 PLAYER_HURT scene="+scene+" tick="+Main.GameUpdateCount+" reason="+reason+" damage="+damage+" requested="+requested+" hp="+old.Life+"->"+p.statLife+" dead="+old.Dead+"->"+p.dead+" pos="+old.Position+"->"+p.position+" velocity="+old.Velocity+"->"+p.velocity+" fall="+old.FallStart+","+old.FallStart2+"->"+p.fallStart+","+p.fallStart2+" immune="+old.Immune+","+old.ImmuneTime+"->"+p.immune+","+p.immuneTime+" direction="+direction+" respawn="+p.respawnTimer);
                    };
                    beforeAction=n=>
                    {
                            // The independent action oracle must not reset Source's live cells.
                            terrain.Reset();
                        actionValid=false;if(!ReferenceEquals(n,actor) || !n.active)return;
                        terrain.Reset();var group=Capture(read,tracking,terrain,session);predictedAction=group[Find(group,actor)];var e=Environment(Main.LocalPlayer,true);PredictionStop stop;
                        actionValid=NpcMotion.Step(ref predictedAction,group,group.Length,e,terrain,1,true,out stop);
                        Require(actionValid || stop==PredictionStop.Despawn,"Original current AI107 action has all captured dependencies scene="+scene+" stop="+stop);
                    };
                    afterAction=n=>
                    {
                        if(!ReferenceEquals(n,actor) || !actionValid)return;
                        double error=Vector2.Distance(n.position,new Vector2(predictedAction.X,predictedAction.Y));
                        Require(error<=.002 && Math.Abs(predictedAction.Vx-n.velocity.X)<=.0001 && Math.Abs(predictedAction.Vy-n.velocity.Y)<=.0001 && predictedAction.A0==n.ai[0] && predictedAction.A1==n.ai[1] && predictedAction.Direction==n.direction && predictedAction.NoTileCollide==n.noTileCollide,"Current original AI107 slot action scene="+scene+" error="+error+" predicted="+predictedAction.X+","+predictedAction.Y+" predictedV="+predictedAction.Vx+","+predictedAction.Vy+" actual="+n.position+" actualV="+n.velocity+" predictedA="+predictedAction.A0+","+predictedAction.A1+" actualA="+n.ai[0]+","+n.ai[1]+" facing="+predictedAction.Direction+"/"+n.direction+" pc="+Main.LocalPlayer.position);
                        actionMatches++;
                    };
                    string closure=null;
                    for(int frame=0;frame<1100 && actor.active;frame++)
                    {
                        // A real native hit establishes recovery. Old frozen
                        // NoNewHits predictions are retired at this new premise.
                        int hitFrame=member==566 || member==567?200:member==572 || member==573?20:60;
                        if(frame==hitFrame){actor.StrikeNPCNoInteraction(1,0,0);Require(actor.active && actor.justHit,"Original nonlethal walker hit.");frozen.Clear();hit=true;}
                        NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();if(!actor.active)break;
                        sawWet|=actor.wet;sawDry|=!actor.wet;sawAttack|=actor.ai[0]>0;
                        if(actor.ai[0]!=lastA0 || actor.ai[1]!=lastA1){changes++;lastA0=actor.ai[0];lastA1=actor.ai[1];}
                        string currentClosure="";for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].type==548)currentClosure+=i+":"+Main.npc[i].generation+":"+Main.npc[i].ai[1]+";";
                        if(closure!=null && closure!=currentClosure)frozen.Clear();closure=currentClosure;
                        bool has=(bool)Get(Get(host,"Selection"),"HasTarget") && ReferenceEquals(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Token,actor);if(has)selected++;
                        var path=NativeCombatPresentationChecks.CurrentPath(host);if(path!=null && ReferenceEquals(path.Identity.Token,actor)){Require(has && path.SampleTick==Main.GameUpdateCount,"AI107 selection and published timeline share the same sample.");published++;if(frame%5==0 && path.Count>1)frozen.Add(new Frozen(path,actor.ai[0],actor.ai[1]));}
                        for(int i=frozen.Count-1;i>=0;i--)
                        {
                            var item=frozen[i];int offset=(int)((long)Main.GameUpdateCount-item.Path.SampleTick);if(offset>=item.Path.Count){frozen.RemoveAt(i);continue;}if(offset<=0)continue;
                            var point=item.Path[offset];double error=Vector2.Distance(actor.position,new Vector2(point.Bounds.X,point.Bounds.Y));maximum=Math.Max(maximum,error);matches++;if(offset>=30)longMatches++;if(item.A0!=actor.ai[0] || item.A1!=actor.ai[1])cross++;if((item.A0>0)!=(actor.ai[0]>0))attackCross++;if(hit)afterHit++;
                            var p=Main.LocalPlayer;
                            Require(error<=.08 && point.NoTileCollide==actor.noTileCollide && point.CanReceive==(!actor.dontTakeDamage && !actor.immortal),"Frozen original AI107 "+scene+" offset="+offset+" error="+error.ToString("R",CultureInfo.InvariantCulture)+" predicted="+point.Bounds.X+","+point.Bounds.Y+" actual="+actor.position+" velocity="+actor.velocity+" phases="+actor.ai[0]+","+actor.ai[1]+" facing="+actor.direction+" targetRect="+actor.targetRect+" player="+p.position+" pv="+p.velocity+" aggro="+p.aggro+" itemAnimation="+p.itemAnimation+" noAggro="+p.npcTypeNoAggro[member]+" frozenPhase="+item.A0+","+item.A1+" frozenPC="+item.PlayerArea+" stop="+item.Path.Stop);
                        }
                        rows.Add(string.Join(",",scene,frame,member,actor.ai[0],actor.ai[1],actor.ai[3],actor.localAI[3],actor.position.X,actor.position.Y,actor.velocity.X,actor.velocity.Y,actor.wet,actor.noTileCollide,has,path!=null,path?.Count??0,path?.Stop.ToString()??"none",matches,longMatches,cross,changes,Main.LocalPlayer.position.X,Main.LocalPlayer.position.Y,actor.direction,actor.target,attackCross));
                    }
                    Require(selected>0 && published>0 && matches>=30 && longMatches>=30,"Natural walker has genuine long published native geometry "+scene+" selected="+selected+" pub="+published+" long="+longMatches);
                    Require(actionMatches>=30,"Current native action oracle distinguishes motor from future player premise scene="+scene+" actions="+actionMatches);
                    Require(hit && afterHit>=30,"Natural walker recovery includes a real original hit and newly sampled futures.");
                    if(member==620 && mode==1)Require(sawWet && sawDry,"Goblin Shark naturally enters and leaves the original water motor.");
                    if(member==555 || member==556 || member==557 || member==561 || member==562 || member==563 || member==570 || member==571 || member==572 || member==573 || member==576 || member==577)Require(sawAttack && attackCross>=30,"Attack/motor transitions require original A0 attack entry/exit, excluding hurt cooldown "+scene+" sawAttack="+sawAttack+" attackCross="+attackCross);
                    total+=matches;totalLong+=longMatches;totalCross+=cross;Console.WriteLine("GROUND107 scene="+scene+" birth="+birth+" selected="+selected+" published="+published+" matched="+matches+" long="+longMatches+" cross="+cross+" maxError="+maximum);
                }
                catch(Exception failure) when(!(failure is OutOfMemoryException)){failures.Add(scene+": "+failure.Message);Console.WriteLine("GROUND107 FAIL "+scene+": "+failure);}
                finally{beforeAction=afterAction=null;receivedHit=null;}
            }
            Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"invasion-ground-continuous.csv"),rows);
            Require(failures.Count==0,"Original AI107 natural failures="+failures.Count+": "+string.Join(" | ",failures));Console.WriteLine("PASS original AI107 natural scenes="+scenes+" matched="+total+" long="+totalLong+" cross="+totalCross);
            }
            finally{beforeAction=afterAction=null;receivedHit=null;oracle.UnpatchAll(oracle.Id);}
        }
        private sealed class Frozen
        {internal readonly NpcTrajectory Path;internal readonly float A0,A1;internal readonly MotionRect PlayerArea;internal Frozen(NpcTrajectory path,float a0,float a1){Path=path;A0=a0;A1=a1;var p=Main.LocalPlayer;PlayerArea=new MotionRect(p.position.X,p.position.Y,p.width,p.height);}}
        private static NpcMotionState[] Capture(MethodInfo read,MethodInfo tracking,IPredictionTerrain terrain,long session)
        {
            var values=new List<NpcMotionState>();for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active)
            {
                var actor=Main.npc[i];var state=(NpcMotionState)read.Invoke(null,new object[]{actor,session});object[] facts={actor,state,terrain};tracking.Invoke(null,facts);state=(NpcMotionState)facts[1];
                state.DD2TargetFactsCaptured=state.DD2TargetFactsComplete=state.DD2PlayerPremiseCaptured=true;state.DD2PlayerPremiseIndex=Main.myPlayer;state.DD2OldTarget=actor.oldTarget;
                if(actor.target>=300 && actor.target<300+Main.maxNPCs){var owner=Main.npc[actor.target-300];if(owner.active)state.DD2TargetIdentity=((NpcMotionState)read.Invoke(null,new object[]{owner,session})).Identity;}
                else if(actor.target>=0 && actor.target<255)state.DD2TargetPlayerToken=Main.player[actor.target];values.Add(state);
            }
            return values.ToArray();
        }
        private static int Find(NpcMotionState[] group,NPC actor){for(int i=0;i<group.Length;i++)if(ReferenceEquals(group[i].Identity.Token,actor))return i;throw new InvalidOperationException("Fixture actor is absent.");}
        private static PredictionEnvironment Environment(Player p,bool timeline)
        {
            var players=new List<PredictionInvasionPlayer>();for(int i=0;i<255;i++)if(Main.player[i].active)
            {var player=Main.player[i];uint mask=0;for(int type=552;type<=577;type++)if(player.npcTypeNoAggro[type])mask|=1u<<(type-552);if(player.npcTypeNoAggro[620])mask|=1u<<26;players.Add(new PredictionInvasionPlayer(i,player,new MotionRect(player.position.X,player.position.Y,player.width,player.height),player.velocity.X,player.velocity.Y,player.aggro,mask,!player.dead && !player.ghost,player.itemAnimation==0,null,default(MotionRect)));}
            return new PredictionEnvironment{PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerVx=p.velocity.X,PlayerVy=p.velocity.Y,PlayerVelocityCaptured=true,PlayerTimelineActive=timeline,PlayerDead=p.dead || !p.active,Day=Main.dayTime,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,WorldSurface=(float)Main.worldSurface,Expert=Main.expertMode,Multiplayer=Main.netMode==1,Remix=Main.remixWorld,InvasionType=Main.invasionType,InvasionPlayers=new PredictionInvasionPlayers(players.ToArray(),players.Count),DD2OngoingCaptured=true,DD2Ongoing=DD2Event.Ongoing};
        }
    }
}
