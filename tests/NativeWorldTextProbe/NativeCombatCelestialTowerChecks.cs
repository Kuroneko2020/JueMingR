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
    internal static class NativeCombatCelestialTowerChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static readonly int[] Types={517,422,507,493};
        private static readonly int[] Producers={518,425,424,411};
        private static Action<NPC> before,after;
        private static Action<Projectile> projectileBefore,projectileAfter;
        private static readonly List<string> outlets=new List<string>();
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static void ProjectileBefore(Projectile __instance){projectileBefore?.Invoke(__instance);}
        private static void ProjectileAfter(Projectile __instance){projectileAfter?.Invoke(__instance);}
        private static bool Network(int msgType,int remoteClient,int ignoreClient,Terraria.Localization.NetworkText text,int number,float number2,float number3,float number4,int number5,int number6,int number7)
        {
            bool towerStrike=msgType==28 && number>=0 && number<Main.maxNPCs && (Family(Main.npc[number]) || Producer(Main.npc[number].type));
            bool shield=msgType==101;
            bool soul=msgType==27 && number>=0 && number<Main.maxProjectiles && Main.projectile[number].type==629;
            bool worldReject=msgType==29 && ignoreClient>=0 && ignoreClient<Main.maxProjectiles && Main.projectile[ignoreClient].type==629 && (int)Main.projectile[ignoreClient].key==number;
            if(!towerStrike && !shield && !soul && !worldReject)return true;
            // Only the real network export is isolated. Birth, StrikeNPC,
            // checkDead, NPCLoot and original projectile AI all still run.
            outlets.Add(string.Join("/",msgType,remoteClient,ignoreClient,text,number,number2,number3,number4,number5,number6,number7));return false;
        }
        private static Harmony NetworkScope(string id)
        {var h=new Harmony(id);h.Patch(typeof(NetMessage).GetMethod("SendData",Flags),prefix:new HarmonyMethod(typeof(NativeCombatCelestialTowerChecks).GetMethod(nameof(Network),Flags)){priority=Priority.First+100});return h;}
        private static Type Model{get{return typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcCelestialTowerMotion",true);}}
        internal static void Boundaries()
        {
            var normalize=Model.GetMethod("Normalize",Flags);
            for(int i=1;i<=8192;i++)
            {float x=((i*7919)%100007-50003)/31f,y=((i*3571)%90007-45003)/29f;object[] a={x,y};normalize.Invoke(null,a);var native=Vector2.Normalize(new Vector2(x,y));Require((float)a[0]==native.X && (float)a[1]==native.Y,"Original XNA soul normalization index="+i);}
            var create=Model.GetMethod("CreateConsumers",Flags);var end=Model.GetMethod("EndFrame",Flags);var step=Model.GetMethod("Step",Flags);var first=Model.GetMethod("FirstNewConsumerAction",Flags);
            var n=new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,422,422),Active=true,Style=94,X=2000,Y=1800,Width=80,Height=300,Life=20000,LifeMax=20000,TowerRelationsCaptured=true,TowerRelationsComplete=true,TowerShieldCaptured=true,TowerShieldStrength=2,TowerShieldMaximum=100,TowerNewConsumerAt=int.MaxValue};
            var e=new PredictionEnvironment{WorldWidth=1000,WorldHeight=700,GravityWorldSurface=180,RockLayer=230,TowerConsumers=Empty()};
            object[] args={n,e,new RawTerrain(10),1,PredictionStop.None,null,0,false};
            Require((bool)step.Invoke(null,args) && ((NpcMotionState)args[0]).Y==n.Y,"Raw first solid at10 belongs to the native stable band.");
            args[2]=new RawTerrain(0);args[0]=n;Require((bool)step.Invoke(null,args) && ((NpcMotionState)args[0]).Y==n.Y-1.5f,"Raw active solid at0 lifts the tower regardless of tile collision shape.");
            args[2]=new RawTerrain(-1);args[0]=n;Require((bool)step.Invoke(null,args) && ((NpcMotionState)args[0]).Y==n.Y+1.5f,"Absent first30 solid lowers the body.");
            n.TowerNewConsumerAt=2;args[0]=n;args[3]=2;Require(!(bool)step.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.PhaseBoundary,"Known new soul stops before its first NPC shield consumer.");
            n.TowerShieldStrength=0;args[0]=n;Require((bool)step.Invoke(null,args),"A zero shield cannot regrow from new souls; receiving motion remains known.");
            n.TowerShieldStrength=2;n.TowerNewConsumerAt=int.MaxValue;var group=new[]{n};
            var fact=new PredictionTowerShieldConsumer(new object(),0,1,255,true,n.Identity,new MotionRect(n.X+10,n.Y+10,8,8),0,0,3600,2,-1,true,false,true);
            e.TowerConsumers=new PredictionTowerShieldConsumers(new[]{fact},1,0,16000,0,11200);var work=create.Invoke(null,new object[]{e});object[] consume={work,group,1,e,PredictionStop.None};
            Require((bool)end.Invoke(null,consume) && group[0].TowerShieldStrength==1 && group[0].A3==1,"Integer contact consumes the shared counter after NPC actions.");
            consume[0]=null;Require((bool)end.Invoke(null,consume),"Non-tower ordinary forecasts have no consumer allocation or scan.");
            var damaged=n;damaged.Identity=new NpcIdentity(1,new object(),20,1,425,425);damaged.Life=1;damaged.Health.Fire=60;damaged.Health.RegenCount=-119;damaged.Health.RealLife=-1;var copy=damaged;
            Require((int)first.Invoke(null,new object[]{damaged,e,120,null,0})==2 && damaged.SameSample(copy),"Observed lethal DOT is a copied earliest consumer, never a Source mutation.");
            damaged.Health.Fire=0;damaged.Health.RegenCount=0;Require((int)first.Invoke(null,new object[]{damaged,e,120,null,0})==int.MaxValue,"Healthy producer does not become a world motor dependency.");
            damaged.MotionType=413;Require((int)first.Invoke(null,new object[]{damaged,e,120,null,0})==2,"Solar healthy chain can checkDead on link loss independently of DOT.");
            Console.WriteLine("PASS CelestialTower native8192roots/rawheight/shieldorder/copiedDOT/new-consumer boundaries");
        }
        internal static void Lifecycle(object context)
        {
            var network=NetworkScope("JueMingR.Probe.TowerLifecycleNetwork");outlets.Clear();
            try
            {
                Setup(context,false);WorldGen.TriggerLunarApocalypse();
                foreach(int type in Types)Require(Find(type)!=null && Shield(type)==NPC.ShieldStrengthTowerMax,"Actual lunar event births each protected full-identity tower.");
                var host=Get(context,"CombatObservation");var source=Get(host,"Prediction");long session=(long)Get(host,"Session");var read=source.GetType().GetMethod("Read",Flags);var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);var terrain=(IPredictionTerrain)Get(source,"Terrain");int actualSouls=0;
                // Original AI and pure AI branch from the same real tile
                // layout. This locks raw IsSolid rather than proving a fake
                // collision implementation against its own expected result.
                var rawActor=Find(422);int rawQueries=0;
                foreach(int shape in new[]{0,1,2,3,4})foreach(int distance in new[]{0,9,10,19,20,29,30})
                {
                    rawActor.Bottom=new Vector2(2500.25f,2600.5f);int x=(int)rawActor.Bottom.X>>4,y=(int)rawActor.Bottom.Y>>4;
                    for(int d=0;d<=30;d++)Main.tile[x,y+d]=new Tile();var tile=Main.tile[x,y+distance];tile.active(true);tile.type=(ushort)(shape==4?TileID.Platforms:TileID.Stone);if(shape==1)tile.inActive(true);if(shape==2)tile.slope(1);if(shape==3)tile.halfBrick(true);
                    var group=Capture(read,tracking,terrain,session);object[] args={group[Index(group,rawActor)],EnvironmentFor(rawActor),terrain,1,PredictionStop.None,group,group.Length,false};terrain.Reset();Require((bool)Model.GetMethod("Step",Flags).Invoke(null,args),"Original raw tile branch has captured geometry.");var prediction=(NpcMotionState)args[0];rawActor.AI();Require(Math.Abs(prediction.Y-rawActor.position.Y)<=.001 && Math.Abs(prediction.Vy-rawActor.velocity.Y)<=.0001,"Native raw first-solid shape="+shape+" distance="+distance+" predicted="+prediction.Y.ToString("R")+" actual="+rawActor.position.Y.ToString("R"));rawQueries++;
                }
                for(int family=0;family<Types.Length;family++)
                {
                    var actor=Find(Types[family]);actor.UpdateNPC(actor.whoAmI);int life=actor.life,priorOutlets=outlets.Count;
                    Require(Shield(actor.type)>0 && actor.dontTakeDamage,"Real tower shield sets the native player attack admission gate.");
                    // StrikeNPCNoInteraction is the internal forced-strike
                    // path and deliberately has no dontTakeDamage gate.
                    // Exercise the actual player attack admission instead.
                    Main.LocalPlayer.TryHittingNPC(actor,100,0);
                    Require(actor.life==life && outlets.Count==priorOutlets,"Actual shield rejects the original player attack before StrikeNPC/network export.");
                    Console.WriteLine("TOWER SHIELD-REJECT type="+actor.type+" shield="+Shield(actor.type)+" dontTakeDamage="+actor.dontTakeDamage+" life="+life+"/"+actor.life+" strikeExports="+(outlets.Count-priorOutlets));
                    for(int mode=0;mode<5;mode++)
                    {
                        var p=BirthSoul(actor,Producers[family],mode==0?actor.Center:actor.Center+new Vector2(120,40));actualSouls++;
                        if(mode==1)p.timeLeft=1;
                        if(mode==2)p.wet=true;
                        if(mode==3){Main.player[p.owner].active=false;}
                        if(mode==4){p.position.X=Main.leftWorld;}
                        var group=Capture(read,tracking,terrain,session);var e=EnvironmentFor(actor);e.TowerConsumers=Snapshot(p,group);
                        var work=Model.GetMethod("CreateConsumers",Flags).Invoke(null,new object[]{e});object[] args={work,group,group.Length,e,PredictionStop.None};
                        Require((bool)Model.GetMethod("EndFrame",Flags).Invoke(null,args),"Actual full soul action has its complete raw target facts.");p.Update(p.whoAmI);
                        var row=((Array)work).GetValue(0);bool active=(bool)Get(row,"Active");Require(active==p.active,"Original629 Update retirement mode="+mode);
                        Require(group[Index(group,actor)].TowerShieldStrength==Shield(actor.type),"Original629 Kill vs outworld rejection retains exact shield semantics.");
                        if(active)Require(Math.Abs((float)Get(row,"X")-p.position.X)<=.001 && Math.Abs((float)Get(row,"Y")-p.position.Y)<=.001 && (int)Get(row,"TimeLeft")==p.timeLeft,"Original629 subupdates retain wet preAI motion and every timeLeft decrement.");
                        Main.LocalPlayer.active=true;Projectile.ClearAll();
                    }
                    while(Shield(actor.type)>0){var p=BirthSoul(actor,Producers[family],actor.Center);p.Update(p.whoAmI);}
                    actor.UpdateNPC(actor.whoAmI);Require(!actor.dontTakeDamage,"Real soul depletion exposes the body on its next NPC action.");
                    actor.StrikeNPCNoInteraction(actor.life+actor.defense+1000,0,0);Require(actor.active && actor.ai[2]==1 && actor.life==actor.lifeMax && actor.dontTakeDamage,"Actual lethal receiving hit enters the native death animation before retirement.");
                    // Keep the other real towers alive while this one dies;
                    // the last global lunar shutdown is a separate event.
                    for(int action=0;action<180 && actor.active;action++)
                    {
                        var group=Capture(read,tracking,terrain,session);int index=Index(group,actor);var predicted=group[index];terrain.Reset();PredictionStop stop;bool valid=NpcMotion.Step(ref predicted,group,group.Length,EnvironmentFor(actor),terrain,1,true,out stop);actor.UpdateNPC(actor.whoAmI);
                        if(actor.active)Require(valid && Vector2.Distance(new Vector2(predicted.X,predicted.Y),actor.position)<=.08 && predicted.A1==actor.ai[1] && predicted.Alpha==actor.alpha,"Original tower death clock/alpha/motor action="+action);
                        else Require(!valid && stop==PredictionStop.Despawn,"Original death180 ends the same full actor identity.");
                    }
                    Require(!actor.active,"Natural death animation actually completes.");
                    if(family<3)NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),(int)actor.Center.X,(int)actor.Center.Y,actor.type,Target:Main.myPlayer);
                }
                Setup(context,false);WorldGen.TriggerLunarApocalypse();var exposed=Find(493);
                while(Shield(493)>0){var p=BirthSoul(exposed,411,exposed.Center);p.Update(p.whoAmI);}exposed.UpdateNPC(exposed.whoAmI);
                int worm=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),3000,2500,412,Start:16,Target:Main.myPlayer);Require(Main.npc[worm].active && Main.npc[worm].life==Main.npc[worm].lifeMax && Shield(517)>0,"Actual healthy Solar producer exists alongside an exposed different counter.");
                var observed=(NpcMotionState)read.Invoke(null,new object[]{exposed,session});var candidate=(NpcTrajectory)source.GetType().GetMethod("TryCandidate",Flags).Invoke(source,new object[]{observed.Identity,(long)Main.GameUpdateCount,false,120});Require(candidate!=null && candidate.Count>30,"Production Source does not let unrelated shielded Solar chain truncate exposed Stardust long motion.");
                Require(actualSouls==20 && rawQueries==35 && outlets.Count>0,"Actual minion deaths and real network exports remain counted.");Console.WriteLine("PASS CelestialTower real event/shield/miniondeath/629subupdates/death180 souls="+actualSouls+" nativeRawQueries="+rawQueries+" network="+outlets.Count);
            }
            finally{Main.netMode=0;network.Unpatch(typeof(NetMessage).GetMethod("SendData",Flags),HarmonyPatchType.Prefix,network.Id);}
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");long session=(long)Get(host,"Session");var read=source.GetType().GetMethod("Read",Flags);var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);var terrain=(IPredictionTerrain)Get(source,"Terrain");
            var rows=new List<string>{"scene,frame,type,phase,clock,shield,life,selected,published,matched,long,cross,actions,souls,error"};var failures=new List<string>();int totalActions=0,totalMatches=0,totalSouls=0;bool oldGood=Main.getGoodWorld;
            var network=NetworkScope("JueMingR.Probe.TowerContinuousNetwork");var observer=new Harmony("JueMingR.Probe.TowerOriginalActions");var update=typeof(NPC).GetMethod("UpdateNPC",Flags);var projectileUpdate=typeof(Projectile).GetMethod("Update",Flags);
            observer.Patch(update,prefix:new HarmonyMethod(typeof(NativeCombatCelestialTowerChecks).GetMethod(nameof(Before),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatCelestialTowerChecks).GetMethod(nameof(After),Flags)));
            observer.Patch(projectileUpdate,prefix:new HarmonyMethod(typeof(NativeCombatCelestialTowerChecks).GetMethod(nameof(ProjectileBefore),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatCelestialTowerChecks).GetMethod(nameof(ProjectileAfter),Flags)));
            try
            {
                for(int scene=0;scene<8;scene++)try
                {
                    Setup(context,false);WorldGen.TriggerLunarApocalypse();Main.getGoodWorld=scene>=4;int family=scene%4;var actor=Find(Types[family]);Require(actor!=null,"Actual randomized event retains all four tower types.");
                    // Depletion is caused by genuine producer deaths. Do not
                    // overwrite a native shield/AI clock to create a phase.
                    while(Shield(actor.type)>0){var p=BirthSoul(actor,Producers[family],actor.Center);p.Update(p.whoAmI);}
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                    var freezes=new List<NpcTrajectory>();var clocks=new Dictionary<NpcTrajectory,float>();int actions=0,matched=0,longMatches=0,cross=0,published=0,selected=0,souls=0;double maxError=0,tangent=0;NpcMotionState predicted=default(NpcMotionState);bool valid=false;PredictionStop stop=PredictionStop.None;NPC pending=null;
                    before=n=>{pending=n;valid=false;if(!ReferenceEquals(n,actor) || !n.active)return;var group=Capture(read,tracking,terrain,session);predicted=group[Index(group,n)];terrain.Reset();valid=NpcMotion.Step(ref predicted,group,group.Length,EnvironmentFor(n),terrain,1,true,out stop);Require(valid || stop==PredictionStop.Despawn,"Original tower action has no guessed motor boundary: "+stop);};
                    after=n=>{if(!ReferenceEquals(n,pending) || !ReferenceEquals(n,actor) || !valid)return;double error=Vector2.Distance(new Vector2(predicted.X,predicted.Y),n.position);Require(error<=.08 && Math.Abs(predicted.Vx-n.velocity.X)<=.0001 && Math.Abs(predicted.Vy-n.velocity.Y)<=.0001 && predicted.A0==n.ai[0] && predicted.A3==n.ai[3] && predicted.L0==n.localAI[0] && predicted.Life==n.life && predicted.CanReceive==(!n.dontTakeDamage && !n.immortal),"Actual tower fullStep scene="+scene+" error="+error+" predicted="+predicted.X+","+predicted.Y+" actual="+n.position+" V="+predicted.Vy.ToString("R")+"/"+n.velocity.Y.ToString("R"));actions++;};
                    object soulWork=null;NpcMotionState[] soulGroup=null;PredictionEnvironment soulEnvironment=default(PredictionEnvironment);Projectile pendingSoul=null;
                    projectileBefore=p=>{pendingSoul=null;if(!p.active || p.type!=629)return;soulGroup=Capture(read,tracking,terrain,session);soulEnvironment=EnvironmentFor(actor);soulEnvironment.TowerConsumers=Snapshot(p,soulGroup);soulWork=Model.GetMethod("CreateConsumers",Flags).Invoke(null,new object[]{soulEnvironment});object[] args={soulWork,soulGroup,soulGroup.Length,soulEnvironment,PredictionStop.None};Require((bool)Model.GetMethod("EndFrame",Flags).Invoke(null,args),"Actual existing629 necessary owner is observed.");pendingSoul=p;};
                    projectileAfter=p=>{if(!ReferenceEquals(p,pendingSoul))return;var value=((Array)soulWork).GetValue(0);Require((bool)Get(value,"Active")==p.active,"Real629 retirement preserves original effect order.");foreach(int type in Types){int i=IndexType(soulGroup,type);if(i>=0)Require(soulGroup[i].TowerShieldStrength==Shield(type),"Only the raw target's tower counter is consumed.");}souls++;};
                    for(int frame=0;frame<1800 && actor.active;frame++)
                    {
                        if(frame==900){actor.StrikeNPCNoInteraction(400,0,0);freezes.Clear();clocks.Clear();Require(actor.life<actor.lifeMax,"Real receiving hit changes health without resetting the sine clock.");}
                        NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();if(!actor.active)break;var path=cache.Read(0);bool chosen=(bool)Get(Get(host,"Selection"),"HasTarget") && ReferenceEquals(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Token,actor);if(chosen)selected++;
                        if(path!=null && ReferenceEquals(path.Identity.Token,actor) && !freezes.Contains(path)){published++;if(frame%7==0 && path.Count>1){freezes.Add(path);clocks[path]=actor.ai[0];}}
                        for(int i=freezes.Count-1;i>=0;i--)
                        {var frozen=freezes[i];int offset=(int)(Main.GameUpdateCount-frozen.CaptureTick);if(offset>=frozen.Count){freezes.RemoveAt(i);clocks.Remove(frozen);continue;}if(offset<1)continue;var point=frozen[offset];double error=Vector2.Distance(new Vector2(point.Bounds.X,point.Bounds.Y),actor.position);maxError=Math.Max(maxError,error);matched++;Require(error<=.08 && point.CanReceive==(!actor.dontTakeDamage && !actor.immortal),"Frozen tower scene="+scene+" frame="+frame+" offset="+offset+" error="+error+" stop="+frozen.Stop);if(offset>=30){longMatches++;tangent=Math.Max(tangent,Vector2.Distance(actor.position,new Vector2(frozen[0].Bounds.X+frozen[0].Vx*offset,frozen[0].Bounds.Y+frozen[0].Vy*offset)));}if(actor.ai[0]<clocks[frozen])cross++;}
                        rows.Add(string.Join(",",scene,frame,actor.type,actor.ai[2],actor.ai[0],Shield(actor.type),actor.life,selected,published,matched,longMatches,cross,actions,souls,maxError.ToString("R",CultureInfo.InvariantCulture)));
                    }
                    before=after=null;projectileBefore=projectileAfter=null;
                    Require(actions>=1500 && selected>0 && published>0 && longMatches>=100 && cross>0 && tangent>8,"Each actual receiving tower keeps a curved long future across native sine wraps scene="+scene+" actions="+actions+" long="+longMatches+" cross="+cross+" tangent="+tangent);
                    totalActions+=actions;totalMatches+=matched;totalSouls+=souls;Console.WriteLine("TOWER scene="+scene+" actions="+actions+" matched="+matched+" long="+longMatches+" cross="+cross+" souls="+souls+" error="+maxError);
                }
                catch(Exception failure) when(!(failure is OutOfMemoryException)){before=after=null;projectileBefore=projectileAfter=null;failures.Add(scene+": "+failure.Message);Console.WriteLine("TOWER FAIL scene="+scene+": "+failure);}
                Require(failures.Count==0,"All four celestial tower natural members/environment variants remain required: "+string.Join(" | ",failures));Console.WriteLine("PASS CelestialTower scenes8 matched="+totalMatches+" actions="+totalActions+" souls="+totalSouls);
            }
            finally{before=after=null;projectileBefore=projectileAfter=null;Main.getGoodWorld=oldGood;observer.Unpatch(update,HarmonyPatchType.All,observer.Id);observer.Unpatch(projectileUpdate,HarmonyPatchType.All,observer.Id);network.Unpatch(typeof(NetMessage).GetMethod("SendData",Flags),HarmonyPatchType.Prefix,network.Id);Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"celestial-tower-continuous.csv"),rows);}
        }
        private static PredictionTowerShieldConsumers Empty(){return new PredictionTowerShieldConsumers(new PredictionTowerShieldConsumer[0],0,Main.leftWorld,Main.rightWorld,Main.topWorld,Main.bottomWorld);}
        private static PredictionTowerShieldConsumers Snapshot(Projectile p,NpcMotionState[] group)
        {int owner=(int)p.ai[0];NpcIdentity id=default(NpcIdentity);bool captured=false;for(int i=0;i<group.Length;i++)if(group[i].Identity.Slot==owner){id=group[i].Identity;captured=true;break;}var value=new PredictionTowerShieldConsumer(p,p.whoAmI,(int)p.key,p.owner,captured,id,new MotionRect(p.position.X,p.position.Y,p.width,p.height),p.velocity.X,p.velocity.Y,p.timeLeft,p.extraUpdates,p.penetrate,p.owner>=0 && p.owner<Main.player.Length && Main.player[p.owner].active,p.wet,p.aiStyle==122 && !p.tileCollide && p.ignoreWater && p.damage==0 && !p.friendly);return new PredictionTowerShieldConsumers(new[]{value},1,Main.leftWorld,Main.rightWorld,Main.topWorld,Main.bottomWorld);}
        private static NpcMotionState[] Capture(MethodInfo read,MethodInfo tracking,IPredictionTerrain terrain,long session)
        {var list=new List<NpcMotionState>();foreach(var n in Main.npc)if(n!=null && Family(n) && n.active){var state=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] args={n,state,terrain};tracking.Invoke(null,args);state=(NpcMotionState)args[1];state.TowerRelationsCaptured=state.TowerRelationsComplete=state.TowerShieldCaptured=true;state.TowerShieldStrength=Shield(n.type);state.TowerShieldMaximum=NPC.ShieldStrengthTowerMax;state.TowerNewConsumerAt=int.MaxValue;list.Add(state);}return list.ToArray();}
        private static PredictionEnvironment EnvironmentFor(NPC n)
        {var p=Main.player[n.target>=0 && n.target<Main.player.Length?n.target:255];return new PredictionEnvironment{PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerDead=!p.active || p.dead,PlayerTimelineActive=p.active && !p.dead && !p.ghost,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,WorldSurface=(float)Main.worldSurface,RockLayer=(float)Main.rockLayer,Expert=Main.expertMode,GoodWorld=Main.getGoodWorld,Remix=Main.remixWorld,Multiplayer=Main.netMode==1,TowerConsumers=Empty()};}
        private static Projectile BirthSoul(NPC tower,int type,Vector2 center)
        {
            // This lifecycle slice supplies only the real frame-entry pool
            // consumer. A dead slot stays protected for two entries; hundreds
            // of births in one entry cannot stand in for natural depletion.
            // Do not clear protection or run unrelated player/tower AI here.
            NativeQuickItemChecks.BeginWorldStep();Main.EverLastingTicker++;NPC.UpdateProtectedSpawnSlots();
            int protectedSlots=0;for(int i=16;i<Main.maxNPCs;i++)if(!Main.npc[i].active && NPC.spawnSlotProtected[i]>0)protectedSlots++;
            int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),(int)center.X,(int)center.Y,type,Start:16,Target:Main.myPlayer);
            Require(slot>=0 && slot<Main.maxNPCs,"Actual producer allocation has a native slot: requestedType="+type+" result="+slot+" protectedInactive="+protectedSlots+" tick="+Main.GameUpdateCount+" shield="+Shield(tower.type));
            var minion=Main.npc[slot];minion.Center=center;int before=CountSouls();
            minion.StrikeNPCNoInteraction(minion.life+minion.defense+10000,0,0);
            Require(!minion.active && CountSouls()>before,"Real producer death creates a629 shield soul: requestedType="+type+" actualType="+minion.type+" slot="+slot+" generation="+minion.generation+" protected="+NPC.spawnSlotProtected[slot]+" protectedInactive="+protectedSlots+" souls="+before+"/"+CountSouls()+" shield="+Shield(tower.type)+" active="+minion.active+" life="+minion.life);
            foreach(var p in Main.projectile)if(p.active && p.type==629 && p.ai[0]==tower.whoAmI)return p;
            throw new InvalidOperationException("Actual soul target missing type="+tower.type);
        }
        private static int CountSouls(){int count=0;foreach(var p in Main.projectile)if(p.active && p.type==629)count++;return count;}
        private static bool Family(NPC n){return n!=null && (n.type==517 || n.type==422 || n.type==507 || n.type==493);}
        private static bool Producer(int type){return type==518 || type==425 || type==424 || type==411;}
        private static NPC Find(int type){foreach(var n in Main.npc)if(n.active && n.type==type)return n;return null;}
        private static int Index(NpcMotionState[] group,NPC n){for(int i=0;i<group.Length;i++)if(ReferenceEquals(group[i].Identity.Token,n))return i;return -1;}
        private static int IndexType(NpcMotionState[] group,int type){for(int i=0;i<group.Length;i++)if(group[i].EffectiveType==type)return i;return -1;}
        private static int Shield(int type){return type==517?NPC.ShieldStrengthTowerSolar:type==422?NPC.ShieldStrengthTowerVortex:type==507?NPC.ShieldStrengthTowerNebula:NPC.ShieldStrengthTowerStardust;}
        private static void Setup(object context,bool good)
        {
            NativeCombatLiveContextChecks.FlightWorld();Main.maxTilesX=1000;Main.maxTilesY=700;Main.leftWorld=Main.topWorld=0;Main.rightWorld=16000;Main.bottomWorld=11200;Main.worldSurface=180;Main.rockLayer=230;Main.tile=new Tile[1000,700];
            for(int x=0;x<1000;x++)for(int y=0;y<700;y++){var tile=Main.tile[x,y]=new Tile();if(y>=220){tile.active(true);tile.type=TileID.Stone;}}
            Main.Map=new Terraria.Map.WorldMap(Main.maxTilesX,Main.maxTilesY);NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.GameMode=0;Main.dayTime=false;Main.getGoodWorld=good;Main.remixWorld=false;
            if(Main.ItemDropSolver==null){Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);}for(int i=0;i<Main.item.Length;i++)Main.item[i].whoAmI=i;
            for(int i=0;i<255;i++)Main.player[i].active=false;var p=Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};p.position=new Vector2(8000,3520-p.height);p.statLife=p.statLifeMax=p.statLifeMax2=5000;p.immune=true;p.immuneTime=1000000;p.ResetFallDamage();for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions(path:false,mouseCenter:true,clearLine:false,radius:50));
        }
        private sealed class RawTerrain:IPredictionTerrain
        {
            private readonly int distance;private int origin=int.MinValue;internal RawTerrain(int d){distance=d;}
            public bool Tile(int x,int y,out PredictionTile tile,out PredictionStop stop){if(origin==int.MinValue)origin=y;tile=new PredictionTile{RawActive=distance>=0 && y==origin+distance,RawSolid=true};stop=PredictionStop.None;return true;}
            public bool CanHit(MotionRect a,MotionRect b,out bool result,out PredictionStop stop){result=true;stop=PredictionStop.None;return true;}public bool Solid(MotionRect area,out bool result,out PredictionStop stop){result=false;stop=PredictionStop.None;return true;}public bool Move(ref NpcMotionState n,PredictionEnvironment e,out PredictionStop stop){stop=PredictionStop.None;return true;}public bool Unchanged{get{return true;}}public void Reset(){origin=int.MinValue;}
        }
    }
}
