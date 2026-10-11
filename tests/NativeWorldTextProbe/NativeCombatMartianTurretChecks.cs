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
    // Current-action original AI and full UpdateNPC are separate oracles.
    // Frozen production futures use the existing NoNewHits condition; actual
    // receiving is a new sample, never a rewrite of solver-private ai clocks.
    internal static class NativeCombatMartianTurretChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Action<NPC> before,after;
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static Type Model{get{return typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcMartianTurretMotion",true);}}
        internal static void Boundaries()
        {
            Require(Main.MaxWorldViewSize.X==1920 && Main.MaxWorldViewSize.Y==1200,"Locked native firing maximum is readonly1920x1200, not the current screen size.");
            var method=Model.GetMethod("FiringDistance",Flags);int count=0;
            foreach(float fraction in new[]{0f,.125f,.875f})
            foreach(int dx in new[]{-911,-910,909,910})foreach(int dy in new[]{-551,-550,549,550})
            {
                var pc=new Vector2(3200+fraction,1800+fraction);var center=pc+new Vector2(dx,dy);
                var n=new NpcMotionState{X=center.X-10,Y=center.Y-25,Width=20,Height=50};
                var e=new PredictionEnvironment{PlayerX=pc.X,PlayerY=pc.Y};
                Require((bool)method.Invoke(null,new object[]{n,e})==NPC.AI_GlobalFiringDistanceCheck(center,pc),"AI73 original ToPoint/strict rectangle edge dx="+dx+" dy="+dy);count++;
            }
            Console.WriteLine("PASS MartianTurret original firing boundaries="+count);
        }
        internal static void Lifecycle(object context)
        {
            int actions=0,shots=0,justHits=0;
            // All clocks below are reached by the original AI. The small
            // observed velocity cases protect damping and its strict deadband.
            foreach(int variant in new[]{0,1,2,3,4,5,6,7,8,9,10,11,12,13})
            {
                Setup(context);var actor=Birth();var p=Main.LocalPlayer;
                int prior=variant==0?0:variant==1?60:variant==2?119:variant==3 || variant==13?120:121;
                for(int i=0;i<prior;i++)actor.AI();
                if(variant==5 || variant==11)for(int i=0;i<58;i++)actor.AI();
                if(variant==6){int life=actor.life;actor.StrikeNPCNoInteraction(80,0,0);Require(actor.life<life && actor.justHit,"Actual positive Strike establishes native justHit.");justHits++;}
                if(variant==7 || variant==13){p.position.X=actor.Center.X+2000-p.width*.5f;p.ResetFallDamage();}
                if(variant==8)p.dead=true;
                if(variant==9)
                {
                    // Locked NPCID.Sets.DebuffImmunityData[387] explicitly
                    // excludes31. Keep the actual rejected AddBuff consumer;
                    // do not force a confused flag the native turret cannot gain.
                    Require(actor.buffImmune[BuffID.Confused],"Original MartianTurret defaults declare confusion immunity.");
                    actor.AddBuff(BuffID.Confused,120);actor.UpdateNPC_BuffSetFlags(false);
                    Require(actor.FindBuffIndex(BuffID.Confused)<0 && !actor.confused,"Actual native AddBuff rejects the immune confusion effect.");
                }
                if(variant==10)actor.velocity=new Vector2(2.375f,-.25f);
                if(variant==11)Main.netMode=1;
                if(variant==12)p.active=false;
                var source=Get(Get(context,"CombatObservation"),"Prediction");var terrain=(IPredictionTerrain)Get(source,"Terrain");terrain.Reset();
                var state=Capture(context,actor);object[] args={state,EnvironmentFor(actor),terrain,0,PredictionStop.None,new[]{state},1,actor.confused};
                Require((bool)Model.GetMethod("Step",Flags).Invoke(null,args),"Observed native AI73 action remains deterministic variant="+variant+" stop="+args[4]);
                state=(NpcMotionState)args[0];int beforeShots=CountShots();actor.AI();shots+=CountShots()-beforeShots;
                Require(state.X==actor.position.X && state.Y==actor.position.Y && state.Vx==actor.velocity.X && state.Vy==actor.velocity.Y && state.A0==actor.ai[0] && state.A1==actor.ai[1] && state.Alpha==actor.alpha && state.Health.DontTakeDamage==actor.dontTakeDamage && state.CanReceive==(!actor.dontTakeDamage && !actor.immortal && !actor.friendly) && state.Direction==actor.direction && state.SpriteDirection==actor.spriteDirection,"Original AI73 scalar variant="+variant+" predicted="+state.A0+"/"+state.A1+"/"+state.Vx.ToString("R")+"/"+state.Alpha+" actual="+actor.ai[0]+"/"+actor.ai[1]+"/"+actor.velocity.X.ToString("R")+"/"+actor.alpha);
                if(variant==0 || variant==1 || variant==2)Require(actor.dontTakeDamage,"Actual introduction retains protection through action120.");
                if(variant==3)Require(actor.ai[1]==121 && !actor.dontTakeDamage,"Action121 actually enables receiving after the120th protected action.");
                if(variant==5)Require(CountShots()>beforeShots && actor.ai[0]==-120,"Native server fire creates435 while the deterministic body clock resets.");
                if(variant==13)Require(actor.ai[0]==-119 && CountShots()==beforeShots,"Actual out-of-view first receiving action starts the longer cooldown rather than prohibiting future fire.");
                if(variant==11)Require(CountShots()==beforeShots && actor.ai[0]==60,"Native client retains60 and does not duplicate the authoritative projectile birth.");
                actions++;
            }
            Require(shots>0 && justHits==1,"Actual firing and received-hit clock branches both ran.");
            Console.WriteLine("PASS MartianTurret original lifecycle actions="+actions+" shots="+shots+" positiveJustHits="+justHits);
        }
        private sealed class Frozen
        {
            internal readonly NpcTrajectory Path;internal readonly float Clock;
            internal Frozen(NpcTrajectory path,float clock){Path=path;Clock=clock;}
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            string selection=System.Environment.GetEnvironmentVariable("JUEMINGR_MARTIAN_TURRET_SCENE");int leaf=-1;
            if(!string.IsNullOrEmpty(selection) && (!int.TryParse(selection,out leaf) || leaf<0 || leaf>2))throw new ArgumentException("JUEMINGR_MARTIAN_TURRET_SCENE must be0..2 or unset.");
            Console.WriteLine("MARTIAN-TURRET actualLeaves="+(leaf<0?"0,1,2":leaf.ToString(CultureInfo.InvariantCulture)));
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var terrain=(IPredictionTerrain)Get(source,"Terrain");
            var audit=new Harmony("JueMingR.Probe.MartianTurret");audit.Patch(typeof(NPC).GetMethod("UpdateNPC",Flags),prefix:new HarmonyMethod(typeof(NativeCombatMartianTurretChecks),nameof(Before)),postfix:new HarmonyMethod(typeof(NativeCombatMartianTurretChecks),nameof(After)));
            var csv=new List<string>{"scene,frame,a0,a1,alpha,actions,matched,long,shotBirths,receiving,hit,fresh,firstShotFrame,timeLeft,outsideRestarts,maxError"};
            int total=0,totalActions=0;
            try
            {
                for(int scene=0;scene<3;scene++)
                {
                    if(leaf>=0 && scene!=leaf)continue;Setup(context);var actor=Birth();var p=Main.LocalPlayer;
                    int birthTimeLeft=actor.timeLeft;int frameLimit=scene==2?birthTimeLeft+1:900;
                    if(scene==2){p.position.X=5200;p.ResetFallDamage();}
                    if(scene==1){for(int x=100;x<125;x++){Main.tile[x,149].active(true);Main.tile[x,149].type=1;}Main.tile[112,148].active(true);Main.tile[112,148].type=1;Main.tile[112,148].slope(1);}
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                    var frozen=new List<Frozen>();NpcMotionState expected=default(NpcMotionState);PredictionStop stop=PredictionStop.None;bool valid=false;
                    int actions=0,matched=0,longMatches=0,receiving=0,protectedActions=0,shots=0,firstShotFrame=-1,outsideRestarts=0,fresh=0,restoredLong=0;bool hit=false,retired=false,outsideStart=false;long hitTick=-1;NpcTrajectory priorHit=null;double maximum=0;
                    var seenShots=new HashSet<int>();
                    before=n=>
                    {
                        if(!ReferenceEquals(n,actor) || !n.active)return;
                        outsideStart=scene==2 && n.ai[1]>=120 && n.ai[0]==0;
                        expected=Capture(context,n);var env=EnvironmentFor(n);terrain.Reset();
                        valid=NpcMotion.Step(ref expected,new[]{expected},1,env,terrain,1,true,out stop);
                        Require(valid || stop==PredictionStop.Despawn,"Natural MartianTurret full action must not reject its observed deterministic clock stop="+stop+" ai="+n.ai[0]+"/"+n.ai[1]);
                    };
                    after=n=>
                    {
                        if(!ReferenceEquals(n,actor))return;
                        if(!valid){Require(!n.active,"Original MartianTurret retirement agrees with the pure full action.");retired=true;return;}
                        double error=Vector2.Distance(new Vector2(expected.X,expected.Y),n.position);
                        Require(error<=.08 && Math.Abs(expected.Vx-n.velocity.X)<=.0001 && Math.Abs(expected.Vy-n.velocity.Y)<=.0001 && expected.A0==n.ai[0] && expected.A1==n.ai[1] && expected.Alpha==n.alpha && expected.CanReceive==(!n.dontTakeDamage && !n.immortal && !n.friendly),"Original current AI73 full action error="+error.ToString("R")+" predicted="+expected.X+","+expected.Y+" V="+expected.Vx+","+expected.Vy+" clocks="+expected.A0+","+expected.A1+" actual="+n.position+" V="+n.velocity+" clocks="+n.ai[0]+","+n.ai[1]+" alpha="+expected.Alpha+"/"+n.alpha);
                        if(outsideStart){Require(n.ai[0]==-119,"Original out-of-view zero clock restarts cooldown on every return to zero.");outsideRestarts++;}
                        if(n.dontTakeDamage)protectedActions++;else receiving++;actions++;
                    };
                    // NewNPC sets timeLeft=(int)(750*1.25)=937; consume that
                    // observed lifetime instead of inventing a shorter retirement.
                    for(int frame=0;frame<frameLimit && actor.active;frame++)
                    {
                        if(scene==1 && frame==240)
                        {
                            priorHit=cache.Read(0);int life=actor.life;actor.StrikeNPCNoInteraction(80,0,0);
                            Require(actor.life<life && actor.justHit,"Actual natural receiving changes life and justHit, not a private solver clock.");hit=true;hitTick=(long)Main.GameUpdateCount;frozen.Clear();
                            Console.WriteLine("MARTIAN-TURRET actual-hit scene="+scene+" tick="+hitTick+" previousPublication="+(priorHit?.CaptureTick??-1)+" life="+life+"->"+actor.life);
                        }
                        NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();
                        foreach(var q in Main.projectile)if(q.active && q.type==435 && seenShots.Add((int)q.key)){shots++;if(firstShotFrame<0)firstShotFrame=frame;}
                        if(!actor.active){retired=true;break;}
                        if(hit && Main.GameUpdateCount>hitTick)
                        {
                            var current=cache.Read(0);var prepared=GetOptional(Get(host,"World"),"preparedPath") as NpcTrajectory;
                            Require(current==null || !ReferenceEquals(current,priorHit),"Actual positive receive withdraws the preceding production publication.");
                            Require(prepared==null || ReferenceEquals(prepared,current),"Presentation consumes the replacement rather than the pre-hit publication.");
                        }
                        var path=cache.Read(0);
                        if(path!=null && ReferenceEquals(path.Identity.Token,actor) && (frozen.Count==0 || !ReferenceEquals(frozen[frozen.Count-1].Path,path)))
                        {frozen.Add(new Frozen(path,actor.ai[0]));if(hit && path.CaptureTick>hitTick)fresh++;}
                        foreach(var f in frozen)
                        {
                            int age=(int)((long)Main.GameUpdateCount-f.Path.CaptureTick);if(age<=0 || age>=f.Path.Count)continue;
                            var point=f.Path[age];double error=Vector2.Distance(new Vector2(point.Bounds.X,point.Bounds.Y),actor.position);maximum=Math.Max(maximum,error);
                            Require(error<=.08 && Math.Abs(point.Vx-actor.velocity.X)<=.0001 && Math.Abs(point.Vy-actor.velocity.Y)<=.0001 && point.CanReceive==(!actor.dontTakeDamage && !actor.immortal && !actor.friendly),"Frozen native AI73 scene="+scene+" frame="+frame+" offset="+age+" error="+error.ToString("R")+" predicted="+point.Bounds.X+","+point.Bounds.Y+" actual="+actor.position+" V="+actor.velocity+" ai="+actor.ai[0]+"/"+actor.ai[1]+" stop="+f.Path.Stop);
                            matched++;if(age>=30){longMatches++;if(hit && f.Path.CaptureTick>hitTick)restoredLong++;}
                        }
                        csv.Add(string.Join(",",scene,frame,actor.ai[0],actor.ai[1],actor.alpha,actions,matched,longMatches,shots,receiving,hit,fresh,firstShotFrame,actor.timeLeft,outsideRestarts,maximum.ToString("R",CultureInfo.InvariantCulture)));
                    }
                    Require(protectedActions==120 && receiving>30 && actions>=300 && matched>=30 && longMatches>=30,"Actual birth crosses protected intro and has useful receiving futures. protected="+protectedActions+" receiving="+receiving+" actions="+actions+" matched="+matched+" long="+longMatches);
                    if(scene<2)Require(shots>=2,"Native server emits repeated435 while its body stays predictable.");
                    if(scene==1)Require(hit && fresh>0 && restoredLong>=30,"Actual positive receiving has newly published useful long futures.");
                    if(scene==2)Require(shots==0 && firstShotFrame==-1 && outsideRestarts>=2 && retired && actor.timeLeft<=0,"Native out-of-view clock repeatedly restarts at zero; observed NewNPC lifetime ends in actual retirement. birthTimeLeft="+birthTimeLeft+" timeLeft="+actor.timeLeft+" restarts="+outsideRestarts+" shots="+shots+" active="+actor.active);
                    total+=matched;totalActions+=actions;Console.WriteLine("PASS MartianTurret natural scene="+scene+" actions="+actions+" matched="+matched+" long="+longMatches+" shots="+shots+" firstShotFrame="+firstShotFrame+" hit="+hit+" fresh="+fresh+" restoredLong="+restoredLong+" birthTimeLeft="+birthTimeLeft+" timeLeft="+actor.timeLeft+" outsideRestarts="+outsideRestarts+" retired="+retired+" max="+maximum.ToString("R",CultureInfo.InvariantCulture));
                    before=after=null;
                }
                Console.WriteLine("PASS MartianTurret natural actualActions="+totalActions+" matched="+total);
            }
            finally{before=after=null;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"martian-turret-scenes.csv"),csv);}
        }
        private static NpcMotionState Capture(object context,NPC actor)
        {
            var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");var terrain=(IPredictionTerrain)Get(source,"Terrain");long session=(long)Get(host,"Session");
            var state=(NpcMotionState)source.GetType().GetMethod("Read",Flags).Invoke(null,new object[]{actor,session});
            var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);object[] args={actor,state,terrain};tracking.Invoke(null,args);return (NpcMotionState)args[1];
        }
        private static PredictionEnvironment EnvironmentFor(NPC actor)
        {
            var slots=new List<int>();var areas=new List<MotionRect>();for(int i=0;i<Main.maxPlayers;i++){var value=Main.player[i];if(value!=null && value.active){slots.Add(i);areas.Add(new MotionRect((int)value.position.X,(int)value.position.Y,value.width,value.height));}}
            int target=actor.target>=0 && actor.target<255?actor.target:Main.myPlayer;var p=Main.player[target];
            return new PredictionEnvironment{Players=new PredictionPlayers(areas.ToArray(),areas.Count,slots.ToArray()),PlayerIndex=target,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerVx=p.velocity.X,PlayerVy=p.velocity.Y,PlayerVelocityCaptured=true,PlayerTimelineActive=p.active && !p.dead && !p.ghost,PlayerDead=!p.active || p.dead,Expert=Main.expertMode,Day=Main.dayTime,GoodWorld=Main.getGoodWorld,Multiplayer=Main.netMode==1,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,GravityWorldSurface=Main.worldSurface,RockLayer=(float)Main.rockLayer};
        }
        private static NPC Birth()
        {
            int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1800,2200,387,Start:16,Target:Main.myPlayer);
            Require(slot>=0 && slot<Main.maxNPCs && Main.npc[slot].active && Main.npc[slot].aiStyle==73,"Actual MartianTurret birth allocates the originalAI73 identity.");return Main.npc[slot];
        }
        private static int CountShots(){int count=0;foreach(var q in Main.projectile)if(q.active && q.type==435)count++;return count;}
        private static void Setup(object context)
        {
            NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.GameMode=0;Main.dayTime=false;Main.getGoodWorld=false;
            for(int i=0;i<2;i++){NativeQuickItemChecks.BeginWorldStep();Main.EverLastingTicker++;NPC.UpdateProtectedSpawnSlots();}
            for(int i=0;i<255;i++)Main.player[i].active=false;
            var p=Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};p.position=new Vector2(1750,2400-p.height);p.ResetFallDamage();p.statLife=p.statLifeMax=p.statLifeMax2=5000;p.immune=true;p.immuneTime=1000000;for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());
        }
    }
}