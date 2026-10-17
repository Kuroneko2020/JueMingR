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
    // AI83 is not certified here.
    // Ritual sprite direction/localAI2/projectile keys are presentation effects,
    // explicitly outside these motor/health/receiving comparisons.
    internal static class NativeCombatCultistBossChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Action<NPC> before,after;
        private static readonly List<string> networkOutlets=new List<string>();
        private static bool IsolateNetwork(int msgType,int remoteClient,int ignoreClient,Terraria.Localization.NetworkText text,int number,float number2,float number3,float number4,int number5,int number6,int number7)
        {
            if((msgType!=23 && msgType!=28) || number<0 || number>=Main.maxNPCs || Main.npc[number]==null || (Main.npc[number].type!=439 && Main.npc[number].type!=440))return true;
            // Keep real Strike/checkDead/HitEffect/cancel and original arguments;
            // only the original network export leaves the isolated fixture.
            networkOutlets.Add(string.Join("/",msgType,remoteClient,ignoreClient,text,number,number2,number3,number4,number5,number6,number7));return false;
        }
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static Type Model{get{return typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcCultistBossMotion",true);}}
        internal static void Boundaries()
        {
            var step=Model.GetMethod("Step",Flags);var token=new object();
            var owner=new NpcMotionState{Identity=new NpcIdentity(1,token,20,1,439,439),Style=84,Active=true,Life=100,LifeMax=100,L0=1,A0=0,A1=39,A3=1,Width=24,Height=50,CultistRelationsCaptured=true,CultistRelationsComplete=true,CultistDoomsCaptured=true,CultistDoomsComplete=true,CultistForeignDoomBirthAt=int.MaxValue,TargetCaptured=true,HasPlayer=true,PlayerVelocityCaptured=true,PlayerActive=true,PlayerIndex=0,Target=0,PlayerCenterX=1000,PlayerCenterY=1000,PlayerArea=new MotionRect(990,979,20,42)};
            var e=new PredictionEnvironment{PlayerIndex=0,PlayerX=1000,PlayerY=1000,Expert=true};owner.Life=50;
            object[] args={owner,e,null,1,PredictionStop.None,new[]{owner},1,false};
            Require(!(bool)step.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.RandomDecision,"Meaningful Expert-half phase choice ends before actual RNG consumption.");
            owner.A3=3;args[0]=owner;
            Require((bool)step.Invoke(null,args) && ((NpcMotionState)args[0]).A0==7,"Expert-half scheduled522 cast ignores an irrelevant replacement roll.");
            owner.A3=1;var rows=new NpcMotionState[11];rows[0]=owner;
            for(int i=1;i<11;i++)rows[i]=new NpcMotionState{Identity=new NpcIdentity(1,new object(),40+i,1,523,523),Active=true,PositionOwnerCaptured=true,PositionOwnerValid=true,PositionOwner=owner.Identity};args[0]=owner;args[5]=rows;args[6]=rows.Length;
            Require((bool)step.Invoke(null,args) && ((NpcMotionState)args[0]).A0==3,"Actual global523 lower bound10 makes the replacement roll motor-irrelevant.");
            owner.CultistDoomsComplete=false;args[0]=owner;
            Require((bool)step.Invoke(null,args),"Ten motor-certified surviving owner relations remain sufficient even with an incomplete global count.");
            rows[0].CultistRelationsCaptured=false;args[0]=owner;
            Require(!(bool)step.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.MissingDependency,"Ten foreign health-only owner relations cannot certify future AI-retirement survival.");
            owner.CultistDoomsComplete=true;
            var clone=owner;clone.Identity=new NpcIdentity(1,new object(),19,1,440,440);clone.PositionOwnerCaptured=clone.PositionOwnerValid=true;clone.PositionOwner=owner.Identity;clone.A3=20;
            args[0]=clone;args[5]=new[]{clone};args[6]=1;
            Require(!(bool)step.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.MissingDependency,"Missing full parent identity is not known owner retirement.");
            clone.PositionOwnerValid=false;args[0]=clone;
            Require(!(bool)step.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.Despawn,"Observed invalid clone owner retires without a player dependency.");
            var birth=Model.GetMethod("FirstPotentialDoomBirthAction",Flags);var consumer=Model.GetMethod("FirstNecessaryDoomCountAction",Flags);
            Func<NpcMotionState,int> at=n=>(int)birth.Invoke(null,new object[]{n,e,120,new[]{n},1});
            owner.A0=8;owner.A1=0;Require(at(owner)==5,"Existing Doom cast first attempts birth at preclock4/action5.");
            owner.A1=4;Require(at(owner)==1,"Already observed clock4 attempts birth in the current native action.");
            owner.A0=0;owner.A1=39;owner.A3=1;Require(at(owner)==6,"Meaningful half choice precedes first Doom attempt by five complete actions.");
            Require((int)consumer.Invoke(null,new object[]{owner,e,120,new[]{owner},1})==1,"Count consumer is the choice action, not the later actor birth.");
            owner.Life=100;Require(at(owner)==int.MaxValue,"Healthy foreign body with no observed loss cannot reach a half-health choice under NoNewHits.");
            owner.UnmodeledDamageTicks=1;owner.A3=3;Require(at(owner)==int.MaxValue,"Unknown half source does not skip the scheduled522 cast/travel/wait clocks beyond this120-action horizon.");
            owner.UnmodeledDamageTicks=0;owner.A3=1;owner.Health.RegenCount=-6000;Require(at(owner)==6,"Captured residual loss settles before AI and can make the same genuine half-health choice reachable.");
            owner.CultistNetMode=1;Require(at(owner)==int.MaxValue,"Client never creates a future native523 identity.");
            Console.WriteLine("PASS Cultist necessary random/count/full-identity boundaries; ritual presentation not certified");
        }
        internal static void Lifecycle(object context)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");long session=(long)Get(host,"Session");var read=source.GetType().GetMethod("Read",Flags);var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);var terrain=(IPredictionTerrain)Get(source,"Terrain");
            var network=new Harmony("JueMingR.Probe.CultistLifecycleNetwork");var send=typeof(NetMessage).GetMethod("SendData",Flags);network.Patch(send,prefix:new HarmonyMethod(typeof(NativeCombatCultistBossChecks).GetMethod(nameof(IsolateNetwork),Flags)){priority=Priority.First+100});
            try
            {
            foreach(int mode in new[]{0,1,2})for(int variant=0;variant<2;variant++)
            {
                Setup(context,0);int parentSlot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1800,2100,439,Start:20,Target:Main.myPlayer);var parent=Main.npc[parentSlot];parent.localAI[0]=1;parent.ai[0]=0;parent.ai[1]=10;
                int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1700,2100,440,Start:16,Target:Main.myPlayer);var clone=Main.npc[slot];clone.ai[3]=parentSlot;clone.localAI[0]=1;
                // Precise receiver counterexamples, separate from the natural
                // birth/cycle matrix. Native active alone is the validity gate.
                if(variant==0)parent.life=0;else parent.active=false;
                Main.netMode=mode;
                var group=Capture(read,tracking,terrain,session,clone);var predicted=group[Index(group,clone)];terrain.Reset();PredictionStop stop;
                bool valid=NpcMotion.Step(ref predicted,group,group.Length,EnvironmentFor(clone),terrain,1,true,out stop);clone.UpdateNPC(clone.whoAmI);
                Require(valid==clone.active,"Native clone owner validity includes active-life0 and excludes inactive.");
                if(clone.active)Require(predicted.Active && Vector2.Distance(new Vector2(predicted.X,predicted.Y),clone.position)<=.08,"Active-life0 raw parent remains a real necessary clone identity.");
                else Require(stop==PredictionStop.Despawn && !predicted.Active,"Actual invalid owner retirement has an explicit stop.");
            }
            }
            finally{Main.netMode=0;network.Unpatch(send,HarmonyPatchType.Prefix,network.Id);}
            Console.WriteLine("PASS Cultist native owner active-life0/invalid retirement");
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");long session=(long)Get(host,"Session");var read=source.GetType().GetMethod("Read",Flags);var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);var terrain=(IPredictionTerrain)Get(source,"Terrain");
            var update=typeof(NPC).GetMethod("UpdateNPC",Flags);var observer=new Harmony("JueMingR.Probe.CultistOriginalSlots");observer.Patch(update,prefix:new HarmonyMethod(typeof(NativeCombatCultistBossChecks).GetMethod(nameof(Before),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatCultistBossChecks).GetMethod(nameof(After),Flags)));
            var send=typeof(NetMessage).GetMethod("SendData",Flags);networkOutlets.Clear();observer.Patch(send,prefix:new HarmonyMethod(typeof(NativeCombatCultistBossChecks).GetMethod(nameof(IsolateNetwork),Flags)){priority=Priority.First+100});
            var records=new List<string>{"scene,frame,phase,clock,life,target,clones,dooms,actions,body,clone,random,restored,selected,published,matched,long,cross,error"};var failures=new List<string>();bool oldGood=Main.getGoodWorld;var allPhases=new HashSet<int>();
            try
            {
                string selector=System.Environment.GetEnvironmentVariable("JUEMINGR_CULTIST_SCENE");int first=0,last=7;
                if(!string.IsNullOrEmpty(selector)){Require(int.TryParse(selector,out first) && first>=0 && first<=7,"Cultist selector names a real natural leaf.");last=first;}
                for(int scene=first;scene<=last;scene++)
                {
                    // Below one-third/quarter only changes the unknown
                    // replacement-roll probability. Scene1 covers the same
                    // half-health motor/receiving and actual RNG boundary;
                    // retain scene2 for an explicit leaf request.
                    if(string.IsNullOrEmpty(selector) && scene==2)
                    {Console.WriteLine("CULTIST equivalentLeaf=scene2 reused=scene1 reason=quarter-only-replacement-probability; RNG distribution not certified");continue;}
                    try
                    {
                        Console.WriteLine("CULTIST actualLeaf=scene"+scene);Setup(context,scene);
                        int slot=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),1800,2100,439,Start:16,Target:scene==0?255:Main.myPlayer);var boss=Main.npc[slot];Require(boss.active && boss.aiStyle==84 && boss.localAI[0]==0,"Real Cultist birth enters native intro without forcing an attack phase.");
                        NPC foreign=null;if(scene==4){int other=NPC.NewNPC(NPC.GetSpawnSourceForNaturalSpawn(),2200,2200,439,Start:50,Target:Main.myPlayer);foreign=Main.npc[other];}
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                        var freezes=new List<NpcTrajectory>();var capturedPhases=new Dictionary<NpcTrajectory,float>();var observedClones=new HashSet<NPC>();int actions=0,body=0,cloneActions=0,random=0,restored=0,selected=0,published=0,matched=0,longMatches=0,cross=0,cloneBirth=0,receiving=0,retired=0,fresh=0;
                        bool awaiting=false,hit=false,halfReceived=false;long changedAt=-1;double maxError=0;NPC pending=null;NpcMotionState initial=default(NpcMotionState),predicted=default(NpcMotionState);PredictionStop stop=PredictionStop.None;bool valid=false;
                        before=n=>
                        {
                            pending=n;valid=false;if(!Family(n,boss))return;
                            var group=Capture(read,tracking,terrain,session,n);initial=predicted=group[Index(group,n)];terrain.Reset();valid=NpcMotion.Step(ref predicted,group,group.Length,EnvironmentFor(n),terrain,1,true,out stop);
                            if(!valid){if(stop==PredictionStop.RandomDecision){random++;awaiting=true;}else Require(stop==PredictionStop.Despawn || stop==PredictionStop.MissingDependency,"Only actual Cultist motor choice or necessary relationship ends the sampled action: "+stop);}
                        };
                        after=n=>
                        {
                            if(!ReferenceEquals(n,pending) || !Member(n,boss))return;
                            if(!valid){if(stop==PredictionStop.Despawn && !n.active)retired++;return;}
                            double error=Vector2.Distance(new Vector2(predicted.X,predicted.Y),n.position);
                            Require(error<=.08 && Math.Abs(predicted.Vx-n.velocity.X)<=.0001 && Math.Abs(predicted.Vy-n.velocity.Y)<=.0001 && predicted.A0==n.ai[0] && predicted.A1==n.ai[1] && predicted.A3==n.ai[3] && predicted.Health.Defense==n.defense && predicted.CanReceive==(!n.dontTakeDamage && !n.immortal),"Original Cultist current action scene="+scene+" slot="+n.whoAmI+" type="+n.type+" error="+error+" before="+initial.A0+"/"+initial.A1+" after="+string.Join("/",n.ai)+" V="+predicted.Vx.ToString("R")+","+predicted.Vy.ToString("R")+" actual="+n.velocity);
                            actions++;if(n.type==439){body++;allPhases.Add((int)initial.A0);if(awaiting){restored++;awaiting=false;}}else cloneActions++;
                        };
                        for(int frame=0;frame<4800 && boss.active;frame++)
                        {
                            bool changed=false;
                            if((scene==1 || scene==2 || scene==4) && !halfReceived && frame>=500 && !boss.dontTakeDamage && (foreign==null || !foreign.dontTakeDamage))
                            {
                                float fraction=scene==1?.49f:.24f;boss.StrikeNPCNoInteraction(boss.life-(int)(boss.lifeMax*fraction)+boss.defense+50,0,0);Require(boss.active && boss.life<boss.lifeMax*fraction,"Actual receiving enters the native Expert half/quarter branch.");receiving++;changed=true;
                                if(foreign!=null){foreign.StrikeNPCNoInteraction(foreign.life-(int)(foreign.lifeMax*.24f)+foreign.defense+50,0,0);Require(foreign.active && foreign.life<foreign.lifeMax/4,"Independent actual parent has its own Doom health clock.");}
                                halfReceived=true;
                            }
                            // The completed clock120 still carries the
                            // protection written by native preclock119.
                            // Strike only an actually exposed completed body;
                            // do not force its receiving flag or attack clock.
                            if(!hit && (scene==5 || scene==6) && boss.ai[0]==5 && boss.ai[1]>=120 && boss.ai[1]<400 && !boss.dontTakeDamage)
                            {
                                NPC receiver=scene==6?boss:FirstClone(boss);Require(receiver!=null && !receiver.dontTakeDamage,"Actual ritual exposes the selected legal body/clone receiver.");int previous=receiver.life;receiver.StrikeNPCNoInteraction(receiver.defense+50,0,0);Require(receiver.justHit && receiver.life<previous,"Real receiving sets native ritual JustHit.");hit=true;receiving++;changed=true;
                            }
                            if(scene==7 && frame==800){Main.LocalPlayer.KillMe(Terraria.DataStructures.PlayerDeathReason.ByCustomReason("isolated Cultist retreat"),1000000,0);Require(Main.LocalPlayer.dead,"Actual player death consumes native retirement.");changed=true;}
                            if(changed){freezes.Clear();capturedPhases.Clear();changedAt=(long)Main.GameUpdateCount;}
                            NativeCombatModeledImpactChecks.SampleMouse(context,boss.Center);step();if(!boss.active)break;
                            for(int i=0;i<Main.maxNPCs;i++)if(Family(Main.npc[i],boss) && Main.npc[i].type==440 && observedClones.Add(Main.npc[i]))cloneBirth++;
                            bool chosen=(bool)Get(Get(host,"Selection"),"HasTarget") && ReferenceEquals(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Token,boss);if(chosen)selected++;
                            var path=cache.Read(0);if(path!=null && ReferenceEquals(path.Identity.Token,boss) && !freezes.Contains(path))
                            {published++;if(changedAt>=0 && path.CaptureTick>=changedAt){fresh++;changedAt=-1;}if(frame%5==0 && path.Count>1){freezes.Add(path);capturedPhases[path]=boss.ai[0];}}
                            for(int i=freezes.Count-1;i>=0;i--)
                            {
                                var frozen=freezes[i];int offset=(int)(Main.GameUpdateCount-frozen.CaptureTick);if(offset>=frozen.Count){freezes.RemoveAt(i);capturedPhases.Remove(frozen);continue;}if(offset<1)continue;
                                var point=frozen[offset];double error=Vector2.Distance(new Vector2(point.Bounds.X,point.Bounds.Y),boss.position);maxError=Math.Max(maxError,error);matched++;
                                Require(error<=.08 && point.CanReceive==(!boss.dontTakeDamage && !boss.immortal),"Frozen Cultist scene="+scene+" frame="+frame+" offset="+offset+" phase="+boss.ai[0]+"/"+boss.ai[1]+" error="+error+" stop="+frozen.Stop);
                                if(offset>=30)longMatches++;if(capturedPhases[frozen]!=boss.ai[0])cross++;
                            }
                            records.Add(string.Join(",",scene,frame,boss.ai[0],boss.ai[1],boss.life,boss.target,observedClones.Count,Count(523),actions,body,cloneActions,random,restored,selected,published,matched,longMatches,cross,maxError.ToString("R",CultureInfo.InvariantCulture)));
                        }
                        before=after=null;Require(body>=300 && selected>0 && published>0 && longMatches>=30 && cross>=30,"Natural Cultist known long prefixes remain useful across deterministic phase transitions.");
                        if(scene!=7)Require(cloneBirth>=2 && cloneActions>=30,"Actual ritual births restore full sibling identities and their slot-ordered motor.");
                        if(scene==1 || scene==2 || scene==4)Require(receiving>0 && fresh>0 && random>0 && restored>0,"True receiving/random choices retain native fresh-sample restoration.");
                        if(scene==5 || scene==6)Require(hit && receiving>0 && fresh>0,"Actual wrong-clone/body ritual cancellation was exercised and restored.");
                        if(scene==7)Require(!boss.active && retired>0 && networkOutlets.Exists(x=>x.StartsWith("28/",StringComparison.Ordinal)),"True dead-player branch retires the real Cultist and exports its original message28.");
                        Console.WriteLine("CULTIST scene="+scene+" actions="+actions+" body="+body+" clone="+cloneActions+" birth="+cloneBirth+" random="+random+" restored="+restored+" fresh="+fresh+" matched="+matched+" long="+longMatches+" cross="+cross+" error="+maxError);
                    }
                    catch(Exception failure) when(!(failure is OutOfMemoryException)){before=after=null;failures.Add(scene+": "+failure.Message);Console.WriteLine("CULTIST FAIL scene="+scene+": "+failure);}
                }
                Require(failures.Count==0,"Actual Cultist natural leaves remain required: "+string.Join(" | ",failures));
                if(first==0 && last==7)foreach(int phase in new[]{-1,0,1,2,3,4,5,6,7,8})Require(allPhases.Contains(phase),"Actual complete AI84 family observes phase "+phase);
                Console.WriteLine("PASS Cultist AI84 natural leaves; AI83 and ritual presentation not certified");
            }
            finally{before=after=null;Main.getGoodWorld=oldGood;observer.Unpatch(update,HarmonyPatchType.All,observer.Id);observer.Unpatch(send,HarmonyPatchType.Prefix,observer.Id);Console.WriteLine("CULTIST original isolated network parameters="+string.Join(";",networkOutlets));Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"cultist-continuous.csv"),records);}
        }
        private static NpcMotionState[] Capture(MethodInfo read,MethodInfo tracking,IPredictionTerrain terrain,long session,NPC selected)
        {
            var rows=new List<NpcMotionState>();
            for(int i=0;i<Main.maxNPCs;i++)
            {
                var n=Main.npc[i];if(n==null || !(n.active && (n.type==439 || n.type==440 || n.type==523) || ReferenceEquals(n,selected)))continue;
                var state=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] args={n,state,terrain};tracking.Invoke(null,args);state=(NpcMotionState)args[1];state.CultistRelationsCaptured=state.CultistRelationsComplete=state.CultistDoomsCaptured=state.CultistDoomsComplete=true;state.CultistForeignDoomBirthAt=int.MaxValue;state.CultistNetMode=Main.netMode;
                int parent=n.type==440?(int)n.ai[3]:n.type==523?(int)n.ai[0]:-1;
                if(n.type==440 || n.type==523)
                {
                    state.PositionOwnerCaptured=true;state.PositionOwnerValid=parent>=0 && parent<Main.maxNPCs && Main.npc[parent].active && Main.npc[parent].type==439;
                    if(state.PositionOwnerValid){var owner=Main.npc[parent];state.PositionOwner=new NpcIdentity(session,owner,parent,owner.generation,owner.type,owner.netID);}
                    if(n.type==523){state.PositionRelation=9;state.PositionParameter=state.PositionOwnerValid?0:-1;}
                }
                rows.Add(state);
            }
            return rows.ToArray();
        }
        private static int Index(NpcMotionState[] rows,NPC n){for(int i=0;i<rows.Length;i++)if(ReferenceEquals(rows[i].Identity.Token,n))return i;throw new InvalidOperationException("Original Cultist actor missing from observed group.");}
        private static bool Member(NPC n,NPC parent){return n!=null && (ReferenceEquals(n,parent) || n.type==440 && n.ai[3]==parent.whoAmI);}
        private static bool Family(NPC n,NPC parent){return n!=null && n.active && Member(n,parent);}
        private static NPC FirstClone(NPC boss){for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].type==440 && Main.npc[i].ai[3]==boss.whoAmI)return Main.npc[i];return null;}
        private static int Count(int type){int count=0;for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].type==type)count++;return count;}
        private static PredictionEnvironment EnvironmentFor(NPC n)
        {
            var p=Main.player[n.target>=0 && n.target<Main.player.Length?n.target:255];return new PredictionEnvironment{PlayerIndex=p.whoAmI,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerWidth=p.width,PlayerHeight=p.height,PlayerVx=p.velocity.X,PlayerVy=p.velocity.Y,PlayerVelocityCaptured=true,PlayerTimelineActive=p.active && !p.dead && !p.ghost,PlayerDead=p.dead || !p.active,Expert=Main.expertMode,GoodWorld=Main.getGoodWorld,Day=Main.dayTime,Multiplayer=Main.netMode==1,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,GravityWorldSurface=Main.worldSurface,RockLayer=(float)Main.rockLayer};
        }
        private static void Setup(object context,int scene)
        {
            NativeCombatLiveContextChecks.FlightWorld();NPC.ClearAll();Projectile.ClearAll();Main.netMode=0;Main.GameMode=scene==0?0:1;Main.dayTime=false;Main.getGoodWorld=scene==3;
            if(Main.ItemDropSolver==null){Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);}for(int i=0;i<Main.item.Length;i++)Main.item[i].whoAmI=i;
            for(int i=0;i<255;i++)Main.player[i].active=false;
            var p=Main.player[Main.myPlayer]=new Player{whoAmI=Main.myPlayer,active=true,isControlledByFilm=true,releaseJump=true};p.position=new Vector2(1100,2400-p.height);p.ResetFallDamage();p.statLife=p.statLifeMax=p.statLifeMax2=5000;p.immune=true;p.immuneTime=1000000;for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions(path:false,mouseCenter:true,clearLine:false,radius:50));
        }
    }
}
