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
    internal static class NativeCombatBetsyChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Action<NPC> before,after,checkDeadBefore,checkDeadAfter;
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static void CheckDeadBefore(NPC __instance){checkDeadBefore?.Invoke(__instance);}
        private static void CheckDeadAfter(NPC __instance){checkDeadAfter?.Invoke(__instance);}
        private static Exception ProjectileFailure(Projectile __instance,Exception __exception)
        {
            if(__exception!=null)Console.WriteLine("BETSY ORIGINAL-PROJECTILE-EXCEPTION type="+__instance.type+" slot="+__instance.whoAmI+" key="+(int)__instance.key+" position="+__instance.position+" size="+__instance.width+","+__instance.height+" velocity="+__instance.velocity+" ai="+string.Join("/",__instance.ai)+" owner="+__instance.owner+" world="+Main.maxTilesX+","+Main.maxTilesY+" exception="+__exception);
            return __exception;
        }
        internal static void Boundaries()
        {
            var model=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcBetsyMotion",true);
            var unit=model.GetMethod("Unit",Flags);var lerp=model.GetMethod("Lerp",Flags);var distance=model.GetMethod("Distance",Flags);
            for(int i=1;i<=8192;i++)
            {
                float x=((i*7919)%100007-50003)/31f,y=((i*3571)%90007-45003)/29f;
                var vector=new Vector2(x,y);vector.Normalize();object[] args={x,y,0f,0f};
                Require((bool)unit.Invoke(null,args) && (float)args[2]==vector.X && (float)args[3]==vector.Y,"Betsy exact native XNA normalization "+i);
                float amount=1f/30;float original=Vector2.Lerp(new Vector2(x,y),new Vector2(y,x),amount).X;
                Require((float)lerp.Invoke(null,new object[]{x,y,amount})==original,"Betsy exact native XNA Lerp "+i);
                var state=new NpcMotionState{X=x,Y=y,Width=170,Height=120};var center=new Vector2(state.Bounds.CenterX,state.Bounds.CenterY);
                Require((float)distance.Invoke(null,new object[]{state,y,x})==Vector2.Distance(center,new Vector2(y,x)),"Betsy exact native XNA Distance "+i);
            }
            var token=new object();var n=new NpcMotionState{Identity=new NpcIdentity(1,new object(),16,1,551,551),Style=110,Active=true,Width=170,Height=120,X=1000,Y=700,A0=1,A1=29,A3=5,DD2TargetFactsCaptured=true,DD2TargetFactsComplete=true,DD2PlayerPremiseCaptured=true,DD2PlayerPremiseIndex=0,DD2TargetPlayerToken=token,Target=0};
            var players=new PredictionInvasionPlayers(new[]{new PredictionInvasionPlayer(0,token,new MotionRect(1500,1000,20,42),0,0,0,0,true,false,null,default(MotionRect))},1);
            var e=new PredictionEnvironment{InvasionPlayers=players,PlayerIndex=0,PlayerX=1510,PlayerY=1021,PlayerWidth=20,PlayerHeight=42};
            foreach(bool client in new[]{false,true})
            {
                e.Multiplayer=client;object[] args={n,e,null,0,PredictionStop.None,new[]{n},1,false};
                Require(!(bool)model.GetMethod("Step",Flags).Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.RandomDecision,"Native circle motor choice is a boundary on client and server before the random call.");
                n.A3=4;args[0]=n;Require((bool)model.GetMethod("Step",Flags).Invoke(null,args) && ((NpcMotionState)args[0]).A0==4,"Deterministic fireball entry survives unrelated projectile randomness.");n.A3=5;
            }
            // The actual target helper has no 200-pixel player filter here.
            n.A1=0;n.A3=0;object[] far={n,e,null,0,PredictionStop.None,new[]{n},1,false};
            Require((bool)model.GetMethod("Step",Flags).Invoke(null,far) && ((NpcMotionState)far[0]).Target==0,"Betsy retains its unlimited native player search.");
            n.A0=4;n.A1=60;n.Direction=-1;n.X=e.PlayerX-n.Width*.5f;n.Y=e.PlayerY-n.Height*.5f;far[0]=n;
            Require((bool)model.GetMethod("Step",Flags).Invoke(null,far) && ((NpcMotionState)far[0]).Vx==-12 && ((NpcMotionState)far[0]).Vy==0,"Native coincident fireball launch SafeNormalize uses the observed facing fallback.");
            n.A0=5;n.A1=0;n.X=1000;n.Y=700;
            for(int i=1;i<=8192;i++)
            {
                n.Vx=((i*7919)%100007-50003)/31f;n.Vy=((i*3571)%90007-45003)/29f;n.Direction=i%2==0?1:-1;
                float angle=(float)Math.PI*2*(1f/60);var original=new Vector2(n.Vx,n.Vy).RotatedBy(-angle*n.Direction);far[0]=n;
                Require((bool)model.GetMethod("Step",Flags).Invoke(null,far) && ((NpcMotionState)far[0]).Vx==original.X && ((NpcMotionState)far[0]).Vy==original.Y,"Original Utils circle rotation exact components "+i);
            }
            Console.WriteLine("PASS Betsy native math 8192 vectors, unlimited target and actual circle-RNG boundaries");
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output,ProbeGraphics graphics=null)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");long session=(long)Get(host,"Session");
            Require(GetOptional(source,"Native")==null,"Betsy runs the scalar rolling producer.");
            var read=source.GetType().GetMethod("Read",Flags);var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);var terrain=(IPredictionTerrain)Get(source,"Terrain");
            var rows=new List<string>{"scene,frame,phase,clock,cycle,target,playerX,playerY,playerVx,playerVy,selected,published,count,stop,matched,longMatched,cross,error"};var failures=new List<string>();
            var owner=new Harmony("JueMingR.Probe.BetsyOracle");var update=typeof(NPC).GetMethod("UpdateNPC",Flags);owner.Patch(update,prefix:new HarmonyMethod(typeof(NativeCombatBetsyChecks).GetMethod(nameof(Before),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatBetsyChecks).GetMethod(nameof(After),Flags)));
            // Observe the real receive/death tail separately from the scalar
            // motor. These patches preserve the original mutation and failure.
            var checkDead=typeof(NPC).GetMethod("checkDead",Flags);owner.Patch(checkDead,prefix:new HarmonyMethod(typeof(NativeCombatBetsyChecks).GetMethod(nameof(CheckDeadBefore),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatBetsyChecks).GetMethod(nameof(CheckDeadAfter),Flags)));
            var projectileUpdate=typeof(Projectile).GetMethod("Update",Flags);owner.Patch(projectileUpdate,finalizer:new HarmonyMethod(typeof(NativeCombatBetsyChecks).GetMethod(nameof(ProjectileFailure),Flags)));
            int totalMatches=0,totalActions=0,totalCrystal=0,totalRandom=0,totalRecovered=0,totalReceiving=0,totalLossSamples=0;var allPhases=new HashSet<int>();
            try
            {
                Require(graphics!=null,"Full natural Betsy includes the actual crystal death and original projectile672 screen effects.");graphics.LoadScreenEffects();
                for(int scene=0;scene<3;scene++)
                {
                    try
                    {
                        string birth;var actor=Main.npc[NativeCombatDd2CoverageChecks.Birth(551,source.GetType().Assembly,out birth)];
                        var p=Main.LocalPlayer;p.isControlledByFilm=true;p.releaseJump=true;p.controlLeft=p.controlRight=p.controlUp=p.controlDown=p.controlJump=false;p.ResetFallDamage();p.statLife=p.statLifeMax=p.statLifeMax2=5000;p.immune=true;p.immuneTime=1000000;for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
                        if(scene==2)p.npcTypeNoAggro[551]=true;
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                        var freezes=new List<NpcTrajectory>();var phases=new Dictionary<NpcTrajectory,int>();var seen=new HashSet<int>();int actions=0,crystal=0,random=0,recovered=0,published=0,selected=0,matched=0,longMatched=0,cross=0;double maxError=0,tangent=0;bool awaitingRecovery=false;
                        NPC pending=null,receivingCrystal=null;NpcMotionState predicted=default(NpcMotionState),input=default(NpcMotionState);bool valid=false;int nativeFrame=-1,receiving=0,lossSamples=0;float beforeReceivingPhase=0;bool genuineLethalReceiving=false,receivingChangedThisFrame=false,awaitingLossSample=false;long receivingTick=-1;NpcTrajectory publicationBeforeReceiving=null;
                        checkDeadBefore=n=>
                        {
                            if(n.type!=548)return;beforeReceivingPhase=n.ai[1];genuineLethalReceiving=n.life<=0 && n.ai[1]!=1;
                            if(genuineLethalReceiving)publicationBeforeReceiving=cache.Read(0);
                            Console.WriteLine("BETSY CRYSTAL-CHECKDEAD before scene="+scene+" frame="+nativeFrame+" tick="+Main.GameUpdateCount+" slot="+n.whoAmI+" life="+n.life+" active="+n.active+" phase="+string.Join("/",n.ai)+" protected="+n.dontTakeDamageFromHostiles+" position="+n.position);
                        };
                        checkDeadAfter=n=>
                        {
                            if(n.type!=548)return;
                            if(genuineLethalReceiving && beforeReceivingPhase!=1 && n.ai[1]==1)
                            {
                                Require(n.active && n.life==n.lifeMax && n.ai[0]==0 && n.dontTakeDamageFromHostiles,"The real lethal receive action establishes the original protected loss state.");
                                receiving++;receivingCrystal=n;receivingTick=(long)Main.GameUpdateCount;receivingChangedThisFrame=awaitingLossSample=true;
                                // NoNewHits freezes end at this observed new
                                // input. The native hit/event keeps executing;
                                // a fresh loss sample must restore the oracle.
                                freezes.Clear();
                            }
                            Console.WriteLine("BETSY CRYSTAL-CHECKDEAD after scene="+scene+" frame="+nativeFrame+" tick="+Main.GameUpdateCount+" slot="+n.whoAmI+" life="+n.life+" active="+n.active+" phase="+string.Join("/",n.ai)+" protected="+n.dontTakeDamageFromHostiles+" position="+n.position);
                        };
                        before=n=>
                        {
                            pending=n;valid=false;if(!n.active || !(n.type==551 || n.type==548))return;
                            var group=Capture(read,tracking,terrain,session);int index=-1;for(int i=0;i<group.Length;i++)if(ReferenceEquals(group[i].Identity.Token,n)){index=i;break;}if(index<0)return;
                            if(awaitingLossSample && ReferenceEquals(n,receivingCrystal) && n.ai[1]==1)
                            {
                                var sampled=group[index];Require(sampled.Active && sampled.A1==1 && sampled.A0==n.ai[0] && sampled.Life==n.life && sampled.HostileProtected && ReferenceEquals(sampled.Identity.Token,n) && sampled.Identity.Generation==n.generation,"The next genuine 548 observation captures the complete loss identity/phase/life/protection.");lossSamples++;awaitingLossSample=false;
                            }
                            input=predicted=group[index];terrain.Reset();PredictionStop stop;valid=NpcMotion.Step(ref predicted,group,group.Length,EnvironmentFor(),terrain,1,true,out stop);
                            if(!valid){if(stop==PredictionStop.RandomDecision && n.type==551){Require(n.ai[0]==1 && n.ai[1]+1>=30 && n.ai[3]==5,"Only the real circle-selection action truncates Betsy's own motor.");random++;awaitingRecovery=true;}else Require(stop==PredictionStop.Despawn || stop==PredictionStop.PhaseBoundary || stop==PredictionStop.MissingDependency,"Actual Betsy dependency boundary: "+stop);}
                        };
                        after=n=>
                        {
                            if(!valid || !ReferenceEquals(pending,n))return;double error=Vector2.Distance(new Vector2(predicted.X,predicted.Y),n.position);
                            bool receivingBoundary=n.type==548 && ReferenceEquals(n,receivingCrystal) && receivingTick==(long)Main.GameUpdateCount && input.A1!=1;
                            Require(error<=.08 && Math.Abs(predicted.Vx-n.velocity.X)<=.0001 && Math.Abs(predicted.Vy-n.velocity.Y)<=.0001 && (receivingBoundary || predicted.A0==n.ai[0] && predicted.A1==n.ai[1] && predicted.A2==n.ai[2] && predicted.A3==n.ai[3]),"Original Betsy full slot action scene="+scene+" frame="+nativeFrame+" tick="+Main.GameUpdateCount+" type="+n.type+" inputPhase="+input.A0+"/"+input.A1+"/"+input.A2+"/"+input.A3+" predictedPhase="+predicted.A0+"/"+predicted.A1+"/"+predicted.A2+"/"+predicted.A3+" actualPhase="+string.Join("/",n.ai)+" inputLife="+input.Life+" predictedLife="+predicted.Life+" actualLife="+n.life+" protected="+input.HostileProtected+"/"+predicted.HostileProtected+"/"+n.dontTakeDamageFromHostiles+" receivingBoundary="+receivingBoundary+" error="+error+" predictedV="+predicted.Vx+","+predicted.Vy+" actual="+n.velocity);
                            if(receivingBoundary)Require(receiving>0 && n.life==n.lifeMax && n.ai[0]==0 && n.ai[1]==1 && n.dontTakeDamageFromHostiles,"Only the observed native lethal checkDead input separates the receiving tail from the no-new-hit scalar motor.");
                            if(n.type==548)crystal++;else{actions++;Require(predicted.L0==n.localAI[0] && predicted.L1==n.localAI[1],"Betsy's actual animation clock follows each observed motor branch.");if(awaitingRecovery){recovered++;awaitingRecovery=false;}}
                        };
                        for(int frame=0;frame<3000 && actor.active && DD2Event.Ongoing;frame++)
                        {
                            nativeFrame=frame;
                            // Real stable player input intervals. A changed
                            // intent starts a new forecast premise, while the
                            // original player and emitted attacks still run.
                            if(scene==1 && frame%480==0){p.controlRight=frame%960==0;p.controlLeft=!p.controlRight;freezes.Clear();}
                            NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();if(!actor.active)break;
                            if(receivingChangedThisFrame)
                            {
                                var current=cache.Read(0);Require(current==null || current.CaptureTick==Main.GameUpdateCount && !ReferenceEquals(current,publicationBeforeReceiving),"The completed native receiving frame retires the pre-hit production publication before a new path is consumed.");
                                Console.WriteLine("BETSY RECEIVING-PUBLICATION scene="+scene+" frame="+frame+" tick="+Main.GameUpdateCount+" oldCapture="+(publicationBeforeReceiving?.CaptureTick??-1)+" currentCapture="+(current?.CaptureTick??-1)+" replaced="+!ReferenceEquals(current,publicationBeforeReceiving));receivingChangedThisFrame=false;
                            }
                            seen.Add((int)actor.ai[0]);allPhases.Add((int)actor.ai[0]);bool chosen=(bool)Get(Get(host,"Selection"),"HasTarget") && ReferenceEquals(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Token,actor);if(chosen)selected++;
                            var path=cache.Read(0);if(path!=null && ReferenceEquals(path.Identity.Token,actor)){Require(chosen && path.CaptureTick==Main.GameUpdateCount,"Actual event-created Betsy owns the current production path.");published++;if(frame%5==0 && path.Count>1){freezes.Add(path);phases[path]=(int)actor.ai[0];}}
                            for(int i=freezes.Count-1;i>=0;i--)
                            {
                                var frozen=freezes[i];int offset=(int)((long)Main.GameUpdateCount-frozen.CaptureTick);if(offset>=frozen.Count){freezes.RemoveAt(i);continue;}if(offset<=0)continue;
                                var point=frozen[offset];double error=Vector2.Distance(new Vector2(point.Bounds.X,point.Bounds.Y),actor.position);maxError=Math.Max(maxError,error);matched++;
                                Require(error<=.08 && point.CanReceive==(!actor.dontTakeDamage && !actor.immortal) && point.NoTileCollide==actor.noTileCollide,"Original frozen Betsy scene="+scene+" frame="+frame+" offset="+offset+" error="+error+" predicted="+point.Bounds.X+","+point.Bounds.Y+" actual="+actor.position+" phase="+string.Join("/",actor.ai)+" target="+actor.target+" player="+p.position+" playerV="+p.velocity+" stop="+frozen.Stop);
                                if(offset>=30){longMatched++;tangent=Math.Max(tangent,Vector2.Distance(actor.position,new Vector2(frozen[0].Bounds.X+frozen[0].Vx*offset,frozen[0].Bounds.Y+frozen[0].Vy*offset)));}if(phases[frozen]!=(int)actor.ai[0])cross++;
                            }
                            rows.Add(string.Join(",",scene,frame,actor.ai[0],actor.ai[1],actor.ai[3],actor.target,p.position.X,p.position.Y,p.velocity.X,p.velocity.Y,chosen,path!=null,path?.Count??0,path?.Stop.ToString()??"none",matched,longMatched,cross,maxError.ToString("R",CultureInfo.InvariantCulture)));
                            if(frame%600==0)Console.WriteLine("BETSY progress scene="+scene+" frame="+frame+" phase="+string.Join("/",actor.ai)+" actions="+actions+" random="+random+" recovered="+recovered);
                        }
                        before=after=checkDeadBefore=checkDeadAfter=null;Require(actions>=1000 && crystal>=1000 && selected>0 && published>0 && longMatched>=30 && cross>=30 && tangent>8,"Natural Betsy and crystal preserve long curved, cross-stage original paths: scene="+scene+" actions="+actions+" long="+longMatched+" cross="+cross);
                        Require(seen.Contains(1) && seen.Contains(2) && seen.Contains(3) && seen.Contains(4) && seen.Contains(6) && random>0 && recovered>0,"Native attack cycle reaches charge, breath, fireballs and summon, with capture recovery after the actual optional-circle random choice.");
                        Require(receiving>0 && lossSamples>0 && !awaitingLossSample,"The unchanged natural window includes actual crystal death and a subsequent genuine loss-state sampling recovery.");
                        totalMatches+=matched;totalActions+=actions;totalCrystal+=crystal;totalRandom+=random;totalRecovered+=recovered;totalReceiving+=receiving;totalLossSamples+=lossSamples;Console.WriteLine("BETSY scene="+scene+" birth="+birth+" matched="+matched+" long="+longMatched+" cross="+cross+" actions="+actions+" crystal="+crystal+" random="+random+" recovered="+recovered+" receiving="+receiving+" lossSamples="+lossSamples+" phases="+string.Join("/",seen)+" error="+maxError);
                    }
                    catch(Exception failure) when(!(failure is OutOfMemoryException)){before=after=checkDeadBefore=checkDeadAfter=null;failures.Add(scene+": "+failure.Message);Console.WriteLine("BETSY FAIL scene="+scene+": "+failure);}
                }
                Require(failures.Count==0,"Natural Betsy failures="+failures.Count+": "+string.Join(" | ",failures));Require(allPhases.Contains(5),"The optional circle must be genuinely selected in at least one bounded native cycle; an unobserved random outcome is not coverage.");
                Console.WriteLine("PASS original Betsy scenes=3 matched="+totalMatches+" actions="+totalActions+" crystal="+totalCrystal+" random="+totalRandom+" recovered="+totalRecovered+" receiving="+totalReceiving+" lossSamples="+totalLossSamples);
            }
            finally{before=after=checkDeadBefore=checkDeadAfter=null;owner.Unpatch(update,HarmonyPatchType.All,owner.Id);owner.Unpatch(checkDead,HarmonyPatchType.All,owner.Id);owner.Unpatch(projectileUpdate,HarmonyPatchType.All,owner.Id);Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"betsy-continuous.csv"),rows);}
        }
        private static NpcMotionState[] Capture(MethodInfo read,MethodInfo tracking,IPredictionTerrain terrain,long session)
        {
            var states=new List<NpcMotionState>();for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && (Main.npc[i].type==551 || Main.npc[i].type==548))
            {
                var n=Main.npc[i];var s=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] args={n,s,terrain};tracking.Invoke(null,args);s=(NpcMotionState)args[1];s.DD2TargetFactsCaptured=s.DD2TargetFactsComplete=s.DD2PlayerPremiseCaptured=true;s.DD2PlayerPremiseIndex=Main.myPlayer;s.HostileProtected=n.dontTakeDamageFromHostiles;s.DD2OldTarget=n.oldTarget;s.DD2FaceArea=new MotionRect(n.targetRect.X,n.targetRect.Y,n.targetRect.Width,n.targetRect.Height);
                if(n.target>=300 && n.target<300+Main.maxNPCs)s.DD2TargetIdentity=((NpcMotionState)read.Invoke(null,new object[]{Main.npc[n.target-300],session})).Identity;else if(n.target>=0 && n.target<255)s.DD2TargetPlayerToken=Main.player[n.target];states.Add(s);
            }return states.ToArray();
        }
        private static PredictionEnvironment EnvironmentFor()
        {
            var values=new List<PredictionInvasionPlayer>();for(int i=0;i<255;i++)
            {
                var p=Main.player[i];if(p==null || !p.active)continue;uint mask=0;for(int type=552;type<=577;type++)if(p.npcTypeNoAggro[type])mask|=1u<<(type-552);if(p.npcTypeNoAggro[620])mask|=1u<<26;if(p.npcTypeNoAggro[551])mask|=1u<<27;
                Projectile pet=p.tankPet>=0 && p.tankPet<Main.maxProjectiles?Main.projectile[p.tankPet]:null;values.Add(new PredictionInvasionPlayer(i,p,new MotionRect(p.position.X,p.position.Y,p.width,p.height),p.velocity.X,p.velocity.Y,p.aggro,mask,!p.dead && !p.ghost,p.itemAnimation==0 && p.aggro<0,pet,pet==null?default(MotionRect):new MotionRect(pet.position.X,pet.position.Y,pet.width,pet.height)));
            }
            var pc=Main.LocalPlayer;return new PredictionEnvironment{InvasionPlayers=new PredictionInvasionPlayers(values.ToArray(),values.Count),DD2OngoingCaptured=true,DD2Ongoing=DD2Event.Ongoing,PlayerIndex=Main.myPlayer,PlayerX=pc.Center.X,PlayerY=pc.Center.Y,PlayerWidth=pc.width,PlayerHeight=pc.height,PlayerVx=pc.velocity.X,PlayerVy=pc.velocity.Y,PlayerVelocityCaptured=true,PlayerTimelineActive=pc.active && !pc.dead && !pc.ghost,PlayerDead=pc.dead || !pc.active,Expert=Main.expertMode,Multiplayer=Main.netMode==1,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,GravityWorldSurface=Main.worldSurface};
        }
    }
}
