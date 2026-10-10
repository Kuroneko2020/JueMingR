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
    internal static class NativeCombatSkeletonBossChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Action<NPC> before,after;
        private static Action<NPC> beforeDeath;
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static void BeforeDeath(NPC __instance){beforeDeath?.Invoke(__instance);}
        internal static void Boundaries()
        {
            var step=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcSkeletonBossMotion",true).GetMethod("Step",Flags);
            var n=new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,35,35),Style=11,Active=true,Width=80,Height=102,Life=1000,LifeMax=1000};
            var e=new PredictionEnvironment{PlayerX=1000,PlayerY=2000};object[] args={n,e,null,1,PredictionStop.None,null,0,false};
            Require(!(bool)step.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.PhaseBoundary,"Unborn original hands have no invented identities.");
            n.A0=1;n.A1=1;n.SkeletonRelationsCaptured=n.SkeletonRelationsComplete=true;e.Expert=true;args[0]=n;args[1]=e;args[5]=new[]{n};args[6]=1;
            Require((bool)step.Invoke(null,args),"Observed handless spin has a deterministic motor despite skull/Caster projectile randomness.");
            args[5]=null;args[6]=0;Require(!(bool)step.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.MissingDependency,"Expert global hand count requires its complete original collection.");
            Console.WriteLine("PASS Skeletron unborn/global-hands/deterministic-spin boundaries");
        }
        internal static void Lifecycle(object context)
        {
            object source=Get(Get(context,"CombatObservation"),"Prediction");long session=(long)Get(Get(context,"CombatObservation"),"Session");
            var read=source.GetType().GetMethod("Read",Flags);var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);
            var position=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcPositionObservation",true).GetMethod("Capture",Flags);
            var terrain=(IPredictionTerrain)Get(source,"Terrain");var orphan=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcParentMotion",true).GetMethod("Orphan",Flags);
            int actions=0;
            foreach(int mode in new[]{0,1,2})
            {
                Setup(context,1);var head=Birth(false);head.UpdateNPC(head.whoAmI);var hand=Hand(head,0);Require(hand!=null,"Original first head action allocates its real hand.");
                var ownerState=(NpcMotionState)read.Invoke(null,new object[]{hand,session});object[] ownerArgs={hand,ownerState,session};position.Invoke(null,ownerArgs);ownerState=(NpcMotionState)ownerArgs[1];
                var positionStep=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcPositionMotion",true).GetMethod("Step",Flags);object[] absent={ownerState,new[]{ownerState},1,0,EnvironmentFor(hand),false,PredictionStop.None};
                Require(ownerState.PositionOwnerValid && !(bool)positionStep.Invoke(null,absent) && (PredictionStop)absent[6]==PredictionStop.MissingDependency,"An observed valid full owner identity absent from the prediction group is missing, not dead.");
                bool zeroLifeValid=false;var deathOwner=new Harmony("JueMingR.Probe.SkeletonReceivingOwner");var checkDead=typeof(NPC).GetMethod("checkDead",Flags);
                beforeDeath=n=>
                {
                    if(!ReferenceEquals(n,head) || !n.active || n.life>0)return;
                    var s=(NpcMotionState)read.Invoke(null,new object[]{hand,session});object[] args={hand,s,session};position.Invoke(null,args);s=(NpcMotionState)args[1];
                    Require(s.PositionOwnerCaptured && s.PositionOwnerValid && ReferenceEquals(s.PositionOwner.Token,head),"The actual lethal strike's active zero-life owner remains valid until native checkDead retires it.");zeroLifeValid=true;
                };
                deathOwner.Patch(checkDead,prefix:new HarmonyMethod(typeof(NativeCombatSkeletonBossChecks).GetMethod(nameof(BeforeDeath),Flags)));
                try{head.StrikeNPCNoInteraction(head.lifeMax+head.defense+100,0,0);Require(zeroLifeValid && !head.active,"The original lethal strike samples active life-zero before real owner retirement.");}
                finally{beforeDeath=null;deathOwner.Unpatch(checkDead,HarmonyPatchType.Prefix,deathOwner.Id);}
                Main.netMode=mode;
                try
                {
                    for(int i=0;i<6 && hand.active;i++)
                    {
                        var state=(NpcMotionState)read.Invoke(null,new object[]{hand,session});object[] capture={hand,state,terrain};tracking.Invoke(null,capture);state=(NpcMotionState)capture[1];object[] relation={hand,state,session};position.Invoke(null,relation);state=(NpcMotionState)relation[1];
                        Require(state.PositionOwnerCaptured && !state.PositionOwnerValid,"Known inactive owner is observed independently of absent prediction rows.");
                        object[] args={state,PredictionStop.None};bool valid=(bool)orphan.Invoke(null,args);var predicted=(NpcMotionState)args[0];
                        // One original AI action advances this standalone native
                        // lifecycle. No duplicate AI action is run for capture.
                        hand.AI();actions++;
                        Require(predicted.A2==hand.ai[2] && predicted.Active==hand.active && predicted.SpriteDirection==hand.spriteDirection && predicted.L3==hand.localAI[3] && predicted.Vx==hand.velocity.X && predicted.Vy==hand.velocity.Y,"Native orphan action mode="+mode+" action="+i+" ai2="+hand.ai[2]);
                        Require(valid || (PredictionStop)args[1]==PredictionStop.Despawn,"Exact orphan retirement boundary.");
                    }
                    Require(!hand.active,"Client/single immediate retirement and server +10 past 50 are both original lifecycle outcomes.");
                }
                finally{Main.netMode=0;}
            }
            Console.WriteLine("PASS Skeletron actual owner death/orphan server-client lifecycle actions="+actions);
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");long session=(long)Get(host,"Session");
            var read=source.GetType().GetMethod("Read",Flags);var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);var terrain=(IPredictionTerrain)Get(source,"Terrain");
            var rows=new List<string>{"scene,frame,type,phase,clock,ownHands,globalHands,selected,published,count,stop,matched,longMatched,error,cross,headActions,handActions"};var failures=new List<string>();
            var owner=new Harmony("JueMingR.Probe.SkeletonBossOracle");var update=typeof(NPC).GetMethod("UpdateNPC",Flags);owner.Patch(update,prefix:new HarmonyMethod(typeof(NativeCombatSkeletonBossChecks).GetMethod(nameof(Before),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatSkeletonBossChecks).GetMethod(nameof(After),Flags)));
            bool oldGood=Main.getGoodWorld;int totalMatches=0,totalHeads=0,totalHands=0;
            try
            {
                for(int scene=0;scene<12;scene++)
                {
                    try
                    {
                        Setup(context,scene);NPC actor=scene>=10?Main.npc[NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1800,1800,NPCID.DungeonGuardian,Start:16,Target:Main.myPlayer)]:Birth(scene==3);
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                        var freezes=new List<NpcTrajectory>();var phases=new Dictionary<NpcTrajectory,int>();var owners=new Dictionary<NpcTrajectory,NPC>();var seen=new HashSet<int>();int matched=0,longMatches=0,cross=0,published=0,selected=0,heads=0,hands=0,birthStops=0,retire=0;double maxError=0,tangent=0;bool initialBirthObserved=false;
                        NPC pending=null;NpcMotionState predicted=default(NpcMotionState);bool valid=false;PredictionStop actionStop=PredictionStop.None;
                        before=n=>
                        {
                            pending=n;valid=false;if(!n.active || !(n.aiStyle==11 && (n.type==35 || n.type==68) || n.type==36 && n.aiStyle==12))return;
                            var group=Capture(read,tracking,terrain,session);int index=-1;for(int i=0;i<group.Length;i++)if(ReferenceEquals(group[i].Identity.Token,n)){index=i;break;}if(index<0)return;
                            predicted=group[index];terrain.Reset();valid=NpcMotion.Step(ref predicted,group,group.Length,EnvironmentFor(n),terrain,1,true,out actionStop);
                            if(!valid){if(actionStop==PredictionStop.PhaseBoundary && n.type==35 && n.ai[0]==0)birthStops++;else Require(actionStop==PredictionStop.Despawn || actionStop==PredictionStop.MissingDependency,"Explicit Skeletron original dependency boundary: "+actionStop);}
                        };
                        after=n=>
                        {
                            if(!ReferenceEquals(n,pending))return;
                            if(!valid){if(actionStop==PredictionStop.Despawn && !n.active)retire++;return;}
                            double error=Vector2.Distance(new Vector2(predicted.X,predicted.Y),n.position);
                            Require(error<=.08 && Math.Abs(predicted.Vx-n.velocity.X)<=.0001 && Math.Abs(predicted.Vy-n.velocity.Y)<=.0001 && predicted.A0==n.ai[0] && predicted.A1==n.ai[1] && predicted.A2==n.ai[2] && predicted.A3==n.ai[3] && predicted.Active==n.active && predicted.ReflectsProjectiles==n.reflectsProjectiles,"Original Skeletron slot action scene="+scene+" type="+n.type+" slot="+n.whoAmI+" error="+error+" predictedV="+predicted.Vx+","+predicted.Vy+" actual="+n.velocity+" phase="+string.Join("/",n.ai));
                            if(n.type==36)hands++;else{heads++;Require(predicted.Health.Defense==n.defense,"Native expert global-hands defense agrees with the same complete collection.");}
                        };
                        for(int frame=0;frame<1400 && actor.active;frame++)
                        {
                            if(scene==7 && frame==10){NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),2200,1700,NPCID.SkeletronHead,Start:1,Target:Main.myPlayer);freezes.Clear();}
                            if((scene==4 || scene==5) && frame==400 || scene==5 && frame==600){var hand=Hand(actor,0);Require(hand!=null,"Original own hand exists before its actual lethal receiving strike.");hand.StrikeNPCNoInteraction(hand.lifeMax+hand.defense+100,0,0);Require(!hand.active,"Actual hand loss changes the original global count.");freezes.Clear();}
                            if(scene==6 && frame==400){actor.StrikeNPCNoInteraction(actor.life-(int)(actor.lifeMax*.65f)+actor.defense+20,0,0);Require(actor.active && actor.life<actor.lifeMax*.75f,"Real health threshold starts skull attacks while preserving the deterministic head motor.");freezes.Clear();}
                            if(scene==8 && frame==1000){Main.dayTime=true;freezes.Clear();}
                            if(scene==9 && frame==1000){Main.LocalPlayer.KillMe(Terraria.DataStructures.PlayerDeathReason.ByOther(0),1000000,0);Require(Main.LocalPlayer.dead,"Actual player death enters original retreat.");freezes.Clear();}
                            NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();if(!actor.active)break;
                            if(actor.type==35 && frame==0){Require(actor.ai[0]==1 && Count(actor,true)==2,"The original first head action really creates its two encoded-owner hands.");initialBirthObserved=true;}
                            seen.Add((int)actor.ai[1]);bool chosen=(bool)Get(Get(host,"Selection"),"HasTarget") && ReferenceEquals(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Token,actor);if(chosen)selected++;
                            // Two real heads can overlap at the hover/spin change. The
                            // producer may select either full identity; every saved
                            // path must still compare against that actual head.
                            var path=cache.Read(0);var pathOwner=path?.Identity.Token as NPC;bool jointHead=scene==7 && pathOwner!=null && pathOwner.active && pathOwner.type==35; if(path!=null && (ReferenceEquals(pathOwner,actor) || jointHead)){Require((bool)Get(Get(host,"Selection"),"HasTarget") && ReferenceEquals(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Token,pathOwner) && path.CaptureTick==Main.GameUpdateCount,"Current selected Skeletron owns the real production window after actual hand birth.");published++;if(frame%5==0 && path.Count>1){freezes.Add(path);owners[path]=pathOwner;phases[path]=(int)pathOwner.ai[1];}}
                            for(int i=freezes.Count-1;i>=0;i--)
                            {
                                var frozen=freezes[i];int offset=(int)((long)Main.GameUpdateCount-frozen.CaptureTick);if(offset>=frozen.Count){freezes.RemoveAt(i);continue;}if(offset<=0)continue;var original=owners[frozen];Require(original.active,"Frozen head identity remains alive in the unchanged natural collection.");var point=frozen[offset];double error=Vector2.Distance(new Vector2(point.Bounds.X,point.Bounds.Y),original.position);maxError=Math.Max(maxError,error);matched++;
                                Require(error<=.08 && point.CanReceive==(!original.dontTakeDamage && !original.immortal) && point.ReflectsProjectiles==original.reflectsProjectiles && !point.ProjectileReflectionUnknown,"Frozen original Skeletron scene="+scene+" frame="+frame+" offset="+offset+" error="+error+" phase="+string.Join("/",original.ai)+" predicted="+point.Bounds.X+","+point.Bounds.Y+" actual="+original.position+" identity="+original.whoAmI+" stop="+frozen.Stop);
                                if(offset>=30){longMatches++;tangent=Math.Max(tangent,Vector2.Distance(original.position,new Vector2(frozen[0].Bounds.X+frozen[0].Vx*offset,frozen[0].Bounds.Y+frozen[0].Vy*offset)));}if(phases[frozen]!=(int)original.ai[1])cross++;
                            }
                            rows.Add(string.Join(",",scene,frame,actor.type,actor.ai[1],actor.ai[2],Count(actor,true),Count(actor,false),chosen,path!=null,path?.Count??0,path?.Stop.ToString()??"none",matched,longMatches,maxError.ToString("R",CultureInfo.InvariantCulture),cross,heads,hands));
                        }
                        before=after=null;Require(heads>=300 && selected>0 && published>0 && longMatches>=30 && tangent>8,"Skeletron long curved production futures recover after natural birth: scene="+scene+" pub="+published+" long="+longMatches);
                        if(scene<10){Require(initialBirthObserved && birthStops>0 && hands>=300 && seen.Contains(0) && seen.Contains(1) && cross>=30,"Natural original hand closure and head hover/spin phases are exercised across long windows.");}
                        if(scene==7)Require(Count(actor,false)>Count(actor,true),"Native global expert count includes another head's real hands.");
                        if(scene==8 || scene>=10)Require(seen.Contains(2),"Native day or DungeonGuardian selects lethal chase.");
                        if(scene==9)Require(!actor.active && retire>0,"Actual player death reaches the original head retirement action.");
                        totalMatches+=matched;totalHeads+=heads;totalHands+=hands;Console.WriteLine("SKELETON scene="+scene+" type="+actor.type+" matched="+matched+" long="+longMatches+" cross="+cross+" heads="+heads+" hands="+hands+" births="+birthStops+" retired="+retire+" error="+maxError);
                    }
                    catch(Exception failure) when(!(failure is OutOfMemoryException)){before=after=null;failures.Add(scene+": "+failure.Message);Console.WriteLine("SKELETON FAIL scene="+scene+": "+failure);}
                }
                Require(failures.Count==0,"Original Skeletron failures="+failures.Count+": "+string.Join(" | ",failures));Console.WriteLine("PASS original Skeletron natural scenes=12 matched="+totalMatches+" headActions="+totalHeads+" handActions="+totalHands);
            }
            finally{before=after=null;owner.Unpatch(update,HarmonyPatchType.All,owner.Id);Main.getGoodWorld=oldGood;Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"skeleton-boss-continuous.csv"),rows);}
        }
        private static NpcMotionState[] Capture(MethodInfo read,MethodInfo tracking,IPredictionTerrain terrain,long session)
        {
            var position=tracking.DeclaringType.Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcPositionObservation",true).GetMethod("Capture",Flags);
            var rows=new List<NpcMotionState>();for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && (Main.npc[i].type==35 || Main.npc[i].type==68 || Main.npc[i].type==36))
            {var n=Main.npc[i];var s=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] args={n,s,terrain};tracking.Invoke(null,args);s=(NpcMotionState)args[1];object[] relation={n,s,session};position.Invoke(null,relation);s=(NpcMotionState)relation[1];s.SkeletonRelationsCaptured=s.SkeletonRelationsComplete=true;rows.Add(s);}return rows.ToArray();
        }
        private static PredictionEnvironment EnvironmentFor(NPC n)
        {
            var p=Main.player[n.target>=0 && n.target<255?n.target:Main.myPlayer];var rect=p.Hitbox;return new PredictionEnvironment{Players=new PredictionPlayers(new[]{new MotionRect(rect.X,rect.Y,rect.Width,rect.Height)},1,new[]{p.whoAmI}),PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerVx=p.velocity.X,PlayerVy=p.velocity.Y,PlayerVelocityCaptured=true,PlayerTimelineActive=p.active && !p.dead && !p.ghost,PlayerDead=p.dead || !p.active,Expert=Main.expertMode,GoodWorld=Main.getGoodWorld,Day=Main.dayTime,Multiplayer=Main.netMode==1,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,GravityWorldSurface=Main.worldSurface,RockLayer=(float)Main.rockLayer};
        }
        private static int Count(NPC head,bool own){int count=0;for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].type==36 && (!own || (int)Main.npc[i].ai[1]==head.whoAmI))count++;return count;}
        private static NPC Hand(NPC head,int ordinal){for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].type==36 && (int)Main.npc[i].ai[1]==head.whoAmI && ordinal--==0)return Main.npc[i];return null;}
        private static NPC Birth(bool red)
        {
            if(red){NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1800,1800,NPCID.OldMan,Start:16,Target:Main.myPlayer);NPC.SpawnSkeletron(Main.myPlayer,true);for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].type==35){Require(Main.npc[i].ai[3]==1,"Original Red Hat spawn establishes the real variant.");return Main.npc[i];}throw new InvalidOperationException("Original Red Hat spawn did not produce a head.");}
            return Main.npc[NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1800,1800,NPCID.SkeletronHead,Start:16,Target:Main.myPlayer)];
        }
        private static void Setup(object context,int scene)
        {
            // CPU NPC lifecycle uses the original dedicated profile initialization:
            // texture requests are guarded by dedServ, while native name and
            // variation selection still run during genuine Old Man birth.
            bool dedicated=Main.dedServ;try{Main.dedServ=true;System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Terraria.GameContent.TownNPCProfiles).TypeHandle);}finally{Main.dedServ=dedicated;}
            NativeCombatLiveContextChecks.FlightWorld();Main.Map=new Terraria.Map.WorldMap(Main.maxTilesX,Main.maxTilesY);NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.GameMode=scene==0 || scene==10?0:1;Main.dayTime=false;Main.time=1800;Main.getGoodWorld=scene==2 || scene==11;
            if(Main.ItemDropSolver==null){Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);}for(int i=0;i<Main.item.Length;i++)Main.item[i].whoAmI=i;
            for(int i=0;i<255;i++)Main.player[i].active=false;Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};var p=Main.LocalPlayer;p.position=new Vector2(1100,2400-p.height);p.statLife=p.statLifeMax=p.statLifeMax2=5000;p.creativeGodMode=true;p.immune=true;p.immuneTime=1000000;p.fallStart=p.fallStart2=(int)p.position.Y/16;for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions(path:false,mouseCenter:true,clearLine:false,radius:50));
        }
    }
}
