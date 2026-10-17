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
    internal static class NativeCombatInvasionFlightChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Action<NPC> before,after;
        private static Action<string> terrainFailure;
        private static bool inspectingTerrain;
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static void TerrainCellAfter(int x,int y,PredictionStop stop,bool __result)
        {
            if(inspectingTerrain && !__result)terrainFailure?.Invoke("CellAt="+x+","+y+" stop="+stop+" consumer="+System.Environment.StackTrace);
        }
        private static string currentScene;
        private static int currentFrame;
        private struct HurtSample { internal int Life,FallStart,FallStart2,ImmuneTime; internal bool Dead,Immune; internal Vector2 Position,Velocity; }
        private static void BeforeHurt(Player __instance,out HurtSample __state)
        {__state=new HurtSample{Life=__instance.statLife,Dead=__instance.dead,Immune=__instance.immune,ImmuneTime=__instance.immuneTime,FallStart=__instance.fallStart,FallStart2=__instance.fallStart2,Position=__instance.position,Velocity=__instance.velocity};}
        private static void AfterHurt(Player __instance,double __result,Terraria.DataStructures.PlayerDeathReason damageSource,int Damage,int hitDirection,HurtSample __state)
        {
            if(__result<=0)return;
            Entity cause;damageSource.TryGetCausingEntity(out cause);
            Console.WriteLine("INVASION ORIGINAL-HURT scene="+currentScene+" frame="+currentFrame+" slot="+__instance.whoAmI+" reasonOther="+damageSource.SourceOtherIndex+" reasonProjectile="+damageSource.SourceProjectileType+" cause="+(cause==null?"none":cause.GetType().Name+":"+cause.whoAmI)+" requested="+Damage+" result="+__result+" direction="+hitDirection+" life="+__state.Life+"->"+__instance.statLife+" dead="+__state.Dead+"->"+__instance.dead+" oldPosition="+__state.Position+" position="+__instance.position+" oldVelocity="+__state.Velocity+" velocity="+__instance.velocity+" oldFall="+__state.FallStart+","+__state.FallStart2+" fall="+__instance.fallStart+","+__instance.fallStart2+" oldImmune="+__state.Immune+","+__state.ImmuneTime+" immune="+__instance.immune+","+__instance.immuneTime+" respawn="+__instance.respawnTimer);
        }
        private static Exception ProjectileFailure(Projectile __instance,Exception __exception)
        {
            if(__exception!=null)Console.WriteLine("INVASION ORIGINAL-PROJECTILE-EXCEPTION type="+__instance.type+" slot="+__instance.whoAmI+" key="+(int)__instance.key+" position="+__instance.position+" size="+__instance.width+","+__instance.height+" velocity="+__instance.velocity+" ai="+string.Join("/",__instance.ai)+" owner="+__instance.owner+" world="+Main.maxTilesX+","+Main.maxTilesY+" exception="+__exception);
            return __exception;
        }
        internal static void Boundaries()
        {
            var normalize=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcInvasionFlightMotion",true).GetMethod("Unit",Flags);
            for(int i=1;i<=8192;i++)
            {
                float x=((i*7919)%100007-50003)/31f,y=((i*3571)%90007-45003)/29f;var native=new Vector2(x,y);native.Normalize();object[] values={x,y,0f,0f};
                Require((bool)normalize.Invoke(null,values) && (float)values[2]==native.X && (float)values[3]==native.Y,"Locked XNA flying Normalize exact noninteger components: "+i);
            }
            Console.WriteLine("PASS original XNA flight Normalize exact noninteger vector matrix cases=8192");
            var crystal=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcInvasionCrystalMotion",true).GetMethod("Step",Flags);
            var n=new NpcMotionState{Identity=new NpcIdentity(1,new object(),10,1,548,548),Style=105,Active=true,Life=1,LifeMax=1000,A1=2,A0=119};
            var e=new PredictionEnvironment{DD2OngoingCaptured=true};object[] args={n,e,null,1,PredictionStop.None,null,0,false};
            Require((bool)crystal.Invoke(null,args) && ((NpcMotionState)args[0]).HostileProtected && ((NpcMotionState)args[0]).NoGravity && ((NpcMotionState)args[0]).Life==1000,"Crystal protection and win motion are independent of selectable hostile health.");
            n.A0=599;args[0]=n;Require((bool)crystal.Invoke(null,args) && ((NpcMotionState)args[0]).A1==1 && ((NpcMotionState)args[0]).A0==0,"Nonongoing win checkDead enters the original loss phase.");
            e.DD2Ongoing=true;args[0]=n;args[1]=e;Require(!(bool)crystal.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.Despawn,"Server crystal stops the entire group before real event wipe.");
            e.Multiplayer=true;args[0]=n;args[1]=e;Require(!(bool)crystal.Invoke(null,args) && (PredictionStop)args[4]==PredictionStop.PhaseBoundary,"Client crystal stops before the global event flag changes without server wipe.");
            Console.WriteLine("PASS DD2 crystal protection/win-loss/server-client event boundaries");
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output,ProbeGraphics graphics=null)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");long session=(long)Get(host,"Session");
            Require(GetOptional(source,"Native")==null,"DD2 uses the scalar rolling producer.");
            var read=source.GetType().GetMethod("Read",Flags);var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);
            var terrain=(IPredictionTerrain)Get(source,"Terrain");var records=new List<string>{"scene,frame,type,selected,published,count,stop,matched,longMatched,error,tangent,playerX,playerY,playerVx,playerVy,playerLife,playerDead,playerImmune,playerImmuneTime,fallStart,fallStart2,respawn"};var failures=new List<string>();
            var owner=new Harmony("JueMingR.Probe.InvasionFlightAction");var update=typeof(NPC).GetMethod("UpdateNPC",Flags);
            owner.Patch(update,prefix:new HarmonyMethod(typeof(NativeCombatInvasionFlightChecks).GetMethod(nameof(Before),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatInvasionFlightChecks).GetMethod(nameof(After),Flags)));
            var projectileUpdate=typeof(Projectile).GetMethod("Update",Flags);owner.Patch(projectileUpdate,finalizer:new HarmonyMethod(typeof(NativeCombatInvasionFlightChecks).GetMethod(nameof(ProjectileFailure),Flags)));
            var hurt=typeof(Player).GetMethod("Hurt",Flags);owner.Patch(hurt,prefix:new HarmonyMethod(typeof(NativeCombatInvasionFlightChecks).GetMethod(nameof(BeforeHurt),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatInvasionFlightChecks).GetMethod(nameof(AfterHurt),Flags)));
            var terrainCell=terrain.GetType().GetMethod("CellAt",Flags);owner.Patch(terrainCell,postfix:new HarmonyMethod(typeof(NativeCombatInvasionFlightChecks).GetMethod(nameof(TerrainCellAfter),Flags)));
            int scenes=0,totalMatches=0,totalActions=0,totalCrystal=0,totalRandom=0,totalCrowd=0;var types=new[]{558,559,560,574,575,558};
            bool cpuOnly=System.Environment.GetEnvironmentVariable("JUEMINGR_INVASION_FLIGHT_CPU_ONLY")=="1",lossOnly=System.Environment.GetEnvironmentVariable("JUEMINGR_INVASION_FLIGHT_LOSS_ONLY")=="1";
            Require(!(cpuOnly && lossOnly),"CPU five-scene and original graphics loss-only modes are separate checks.");
            if(cpuOnly)Console.WriteLine("INVASION CPU subset: five native motor scenes; actual projectile672 screen effects remain UNVERIFIED by this process.");
            try
            {
                for(int variant=lossOnly?5:0;variant<(cpuOnly?5:types.Length);variant++)
                {
                    string scene="invasion-flight-"+types[variant]+(variant==5?"-crystal-loss":"");scenes++;
                    currentScene=scene;currentFrame=-1;
                    try
                    {
                        string birth;int slot=NativeCombatDd2CoverageChecks.Birth(types[variant],source.GetType().Assembly,out birth);var actor=Main.npc[slot];
                        Main.Map=new Terraria.Map.WorldMap(Main.maxTilesX,Main.maxTilesY);
                        if(Main.ItemDropSolver==null){Main.ItemDropsDB=new Terraria.GameContent.ItemDropRules.ItemDropDatabase();Main.ItemDropsDB.Populate();Main.ItemDropSolver=new Terraria.GameContent.ItemDropRules.ItemDropResolver(Main.ItemDropsDB);}
                        CrowdBirth(types[variant],source.GetType().Assembly);
                        if(variant==5)
                        {
                            Require(graphics!=null,"Original crystal loss requires its actual graphics/content screen-effect dependencies.");
                            graphics.LoadScreenEffects();
                            NPC crystal=null;for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].type==548){crystal=Main.npc[i];break;}
                            Require(crystal!=null && crystal.ai[1]==0,"Actual event still has its receiving crystal before the original lethal hostile strike.");
                            crystal.StrikeNPCNoInteraction(crystal.lifeMax+crystal.defense+100,0,0);Require(crystal.active && crystal.ai[1]==1 && crystal.dontTakeDamageFromHostiles,"Original checkDead establishes the actual loss phase.");
                        }
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                        var freezes=new List<NpcTrajectory>();var frozenPhases=new Dictionary<NpcTrajectory,int>();int published=0,selectedCount=0,matches=0,longMatches=0,actions=0,crystalActions=0,random=0,crowd=0,cross=0;double maxError=0,tangent=0;
                        NPC pending=null;NpcMotionState predicted=default(NpcMotionState),input=default(NpcMotionState);bool valid=false;var observedPhases=new HashSet<int>();int lossSamples=0,lossPursuit=0,protectedTargets=0,wipeBoundaries=0;bool sawOriginal672=false;
                        before=n=>
                        {
                            pending=n;valid=false;if(!n.active || !(n.type==548 || n.aiStyle==108 && Array.IndexOf(types,n.type)>=0))return;
                            var group=Capture(read,tracking,source,terrain,session);int index=-1;for(int i=0;i<group.Length;i++)if(ReferenceEquals(group[i].Identity.Token,n)){index=i;break;}if(index<0)return;
                            input=predicted=group[index];if(variant==5 && n.type==548){Require(input.A1==1 && input.HostileProtected && input.Life==n.lifeMax,"Fresh actual crystal observations preserve the original protected loss state.");lossSamples++;}var players=Players();var choice=Terraria.Utilities.NPCUtils.SearchForTarget(n,Terraria.Utilities.NPCUtils.TargetSearchFlag.All,Terraria.Utilities.NPCUtils.SearchFilters.OnlyPlayersInCertainDistance(n.Center,200),Terraria.Utilities.NPCUtils.SearchFilters.OnlyCrystal);
                            int player=choice.FoundTank?choice.NearestTankOwnerIndex:Main.myPlayer;var p=Main.player[player];
                            var e=new PredictionEnvironment{InvasionPlayers=players,DD2OngoingCaptured=true,DD2Ongoing=DD2Event.Ongoing,PlayerIndex=player,PlayerX=p.Center.X,PlayerY=p.Center.Y,PlayerVx=p.velocity.X,PlayerVy=p.velocity.Y,PlayerVelocityCaptured=true,PlayerTimelineActive=p.active && !p.dead && !p.ghost,PlayerWidth=p.width,PlayerHeight=p.height,PlayerDead=p.dead || !p.active,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,WorldSurface=(float)Main.worldSurface,Expert=Main.expertMode,Multiplayer=Main.netMode==1};
                            if(n.aiStyle==108)for(int i=0;i<group.Length;i++)if(group[i].EffectiveType==n.type && !ReferenceEquals(group[i].Identity.Token,n) && Math.Abs(n.position.X-group[i].X)+Math.Abs(n.position.Y-group[i].Y)<n.width){crowd++;break;}
                            string failedCell=null;terrainFailure=detail=>{if(failedCell==null)failedCell=detail;};terrain.Reset();PredictionStop stop;inspectingTerrain=true;
                            try{valid=NpcMotion.Step(ref predicted,group,group.Length,e,terrain,1,true,out stop);}
                            finally{inspectingTerrain=false;terrainFailure=null;}
                            if(!valid)
                            {
                                Console.WriteLine("INVASION PURE-BOUNDARY scene="+scene+" frame="+currentFrame+" tick="+Main.GameUpdateCount+" slot="+n.whoAmI+" generation="+n.generation+" type="+n.type+" position="+n.position+" size="+n.width+","+n.height+" velocity="+n.velocity+" noTile="+n.noTileCollide+" ai="+string.Join("/",n.ai)+" local="+string.Join("/",n.localAI)+" target="+n.target+" targetRect="+n.targetRect+" player="+p.position+" playerV="+p.velocity+" stop="+stop+" terrain="+(failedCell??"no failed CellAt"));
                                if(variant==5 && n.type==548 && n.ai[0]+1>=600 && stop==PredictionStop.Despawn)wipeBoundaries++;
                                if(stop==PredictionStop.RandomDestination)random++;else Require(stop==PredictionStop.Despawn || stop==PredictionStop.PhaseBoundary || stop==PredictionStop.MissingDependency || stop==PredictionStop.InvalidState,"Explicit DD2 action boundary: "+stop);
                            }
                        };
                        after=n=>
                        {
                            if(!valid || !ReferenceEquals(pending,n))return;
                            double error=Vector2.Distance(new Vector2(predicted.X,predicted.Y),n.position);
                            Require(error<=.08 && Math.Abs(predicted.Vx-n.velocity.X)<=.0001 && Math.Abs(predicted.Vy-n.velocity.Y)<=.0001 && predicted.Active==n.active,"Original DD2 slot motor "+scene+" type="+n.type+" phase="+n.ai[0]+" error="+error+" predicted="+predicted.Vx+","+predicted.Vy+" actual="+n.velocity);
                            if(n.type==548)crystalActions++;else actions++;
                            if(variant==5 && ReferenceEquals(n,actor) && input.A0==0 && n.ai[0]==0)
                            {
                                var target=n.GetTargetData();var delta=target.Center-new Vector2(input.Bounds.CenterX,input.Bounds.CenterY);var aim=delta;aim.Normalize();aim*=3;float angle=(float)Math.Atan2(aim.Y,aim.X);
                                if(delta.Length()>500 || angle<=(float)Math.PI/8 || angle>=(float)Math.PI-(float)Math.PI/8)lossPursuit++;
                                if(n.HasNPCTarget && Main.npc[n.TranslatedTargetIndex].dontTakeDamageFromHostiles)protectedTargets++;
                            }
                        };
                        string previousClosure=null;
                        for(int frame=0;frame<700 && actor.active;frame++)
                        {
                            currentFrame=frame;
                            NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();if(variant==5)for(int i=0;i<Main.maxProjectiles;i++)if(Main.projectile[i].active && Main.projectile[i].type==672)sawOriginal672=true;
                            // An actual new same-type birth is a new prediction
                            // premise, rather than an invented unborn peer.
                            string closure="";for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].type==actor.type)closure+=i+":"+Main.npc[i].generation+";";
                            if(previousClosure!=null && closure!=previousClosure)freezes.Clear();previousClosure=closure;
                            if(!actor.active)break;observedPhases.Add((int)actor.ai[0]);
                            bool selected=(bool)Get(Get(host,"Selection"),"HasTarget") && ReferenceEquals(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Token,actor);if(selected)selectedCount++;
                            var path=cache.Read(0);if(path!=null && ReferenceEquals(path.Identity.Token,actor)){Require(selected && path.CaptureTick==Main.GameUpdateCount,"Fresh production DD2 identity.");published++;if(frame%5==0 && path.Count>1){freezes.Add(path);frozenPhases[path]=(int)actor.ai[0];}}
                            for(int i=freezes.Count-1;i>=0;i--)
                            {
                                var frozen=freezes[i];int offset=(int)((long)Main.GameUpdateCount-frozen.CaptureTick);if(offset>=frozen.Count){freezes.RemoveAt(i);continue;}if(offset<=0)continue;
                                var point=frozen[offset];double error=Vector2.Distance(new Vector2(point.Bounds.X,point.Bounds.Y),actor.position);maxError=Math.Max(maxError,error);matches++;
                                if(offset>=30){longMatches++;tangent=Math.Max(tangent,Vector2.Distance(actor.position,new Vector2(frozen[0].Bounds.X+frozen[0].Vx*offset,frozen[0].Bounds.Y+frozen[0].Vy*offset)));}
                                Require(error<=.08 && point.CanReceive==(!actor.dontTakeDamage && !actor.immortal) && point.NoTileCollide==actor.noTileCollide,"Frozen DD2 motion "+scene+" frame="+frame+" capture="+frozen.CaptureTick+" offset="+offset+" error="+error+" stop="+frozen.Stop+" predicted="+point.Bounds.X+","+point.Bounds.Y+" predictedV="+point.Vx+","+point.Vy+" actual="+actor.position+" actualV="+actor.velocity+" phase="+string.Join("/",actor.ai)+" target="+actor.target+" player="+Main.LocalPlayer.position+" playerV="+Main.LocalPlayer.velocity+" playerSize="+Main.LocalPlayer.width+","+Main.LocalPlayer.height+" world="+Main.maxTilesX+","+Main.maxTilesY);
                                if(frozenPhases[frozen]!=(int)actor.ai[0])cross++;
                            }
                            var pc=Main.LocalPlayer;records.Add(string.Join(",",scene,frame,actor.type,selected,path!=null,path?.Count??0,path?.Stop.ToString()??"none",matches,longMatches,maxError.ToString("R",CultureInfo.InvariantCulture),tangent.ToString("R",CultureInfo.InvariantCulture),pc.position.X,pc.position.Y,pc.velocity.X,pc.velocity.Y,pc.statLife,pc.dead,pc.immune,pc.immuneTime,pc.fallStart,pc.fallStart2,pc.respawnTimer));
                        }
                        before=after=null;
                        Require(actions>=100 && crystalActions>=100,"Genuine event verifies the flying and crystal body motors for hundreds of ordinary actions.");
                        Require(selectedCount>0 && published>0 && longMatches>=30 && tangent>8,"Actual DD2 producer has curved original paths at thirty or more future actions: "+scene+" pub="+published+" long="+longMatches+" tangent="+tangent);
                        if(variant==5)
                        {
                            // The real protected/rising loss crystal changes
                            // the approach angle. This receiving scenario has
                            // its own pursuit and event-wipe obligations; the
                            // five normal scenarios still require every dive.
                            Require(observedPhases.Contains(0) && lossPursuit>=100 && protectedTargets>=100 && lossSamples>=100,"Actual loss retains its protected old target and repeatedly takes the native rejected-dive pursuit branch.");
                            Require(sawOriginal672 && wipeBoundaries>0 && !actor.active && !DD2Event.Ongoing,"Original projectile672 actually runs and the600-action crystal event boundary retires the real invasion and its published dependency.");
                            Require(cache.Read(0)==null,"The completed native event wipe retires the production publication.");
                            Console.WriteLine("INVASION LOSS phases="+string.Join("/",observedPhases)+" rejectedDive="+lossPursuit+" protectedTarget="+protectedTargets+" freshLossSamples="+lossSamples+" original672="+sawOriginal672+" wipeBoundaries="+wipeBoundaries);
                        }
                        else Require(observedPhases.Contains(0) && observedPhases.Contains(1) && observedPhases.Contains(2) && cross>=30,"Actual birth crosses approach, slowdown and observed dive phases.");
                        totalMatches+=matches;totalActions+=actions;totalCrystal+=crystalActions;totalRandom+=random;totalCrowd+=crowd;
                        Console.WriteLine("INVASION scene="+scene+" birth="+birth+" selected="+selectedCount+" published="+published+" matched="+matches+" long="+longMatches+" error="+maxError+" actions="+actions+" crystal="+crystalActions+" random="+random+" crowd="+crowd+" cross="+cross);
                    }
                    catch(Exception failure) when(!(failure is OutOfMemoryException)){before=after=null;failures.Add(scene+": "+failure.Message);Console.WriteLine("INVASION FAIL "+scene+": "+failure);}
                }
                Require(failures.Count==0,"DD2 flight natural failures="+failures.Count+": "+string.Join(" | ",failures));
                if(!lossOnly)Require(totalCrowd>0 && totalRandom>0,"The normal five-family set exercises real same-type crowd and Kobold spread boundaries.");
                Console.WriteLine("PASS DD2 "+(cpuOnly?"CPU subset; graphics672 UNVERIFIED":lossOnly?"original graphics672 loss-only":"full original graphics set")+" natural scenes="+scenes+" matched="+totalMatches+" actions="+totalActions+" crystal="+totalCrystal+" random="+totalRandom+" crowd="+totalCrowd);
            }
            finally{before=after=null;terrainFailure=null;inspectingTerrain=false;currentScene=null;owner.Unpatch(update,HarmonyPatchType.All,owner.Id);owner.Unpatch(projectileUpdate,HarmonyPatchType.Finalizer,owner.Id);owner.Unpatch(hurt,HarmonyPatchType.All,owner.Id);owner.Unpatch(terrainCell,HarmonyPatchType.All,owner.Id);Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"invasion-flight-continuous.csv"),records);}
        }
        private static PredictionInvasionPlayers Players()
        {
            var values=new List<PredictionInvasionPlayer>();
            for(int i=0;i<255;i++)
            {
                var p=Main.player[i];if(p==null || !p.active)continue;uint mask=0;for(int type=552;type<=577;type++)if(p.npcTypeNoAggro[type])mask|=1u<<(type-552);if(p.npcTypeNoAggro[620])mask|=1u<<26;
                Projectile pet=p.tankPet>=0 && p.tankPet<Main.maxProjectiles?Main.projectile[p.tankPet]:null;
                values.Add(new PredictionInvasionPlayer(i,p,new MotionRect(p.position.X,p.position.Y,p.width,p.height),p.velocity.X,p.velocity.Y,p.aggro,mask,!p.dead && !p.ghost,p.itemAnimation==0 && p.aggro<0,pet,pet==null?default(MotionRect):new MotionRect(pet.position.X,pet.position.Y,pet.width,pet.height)));
            }
            return new PredictionInvasionPlayers(values.ToArray(),values.Count);
        }
        private static void CrowdBirth(int type,Assembly host)
        {
            // The same source-conditioned entry used by the original gate
            // chooses every kind. Accelerate legal births, as the existing
            // event coverage fixture does; never pass a desired NPC kind.
            for(int attempt=0;attempt<256;attempt++)
            {
                int peers=0;for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].type==type)peers++;
                if(peers>=2)return;
                Require(DD2Event.Ongoing && !DD2Event.EnemySpawningIsOnHold,"Original event remains eligible for same-wave crowd births.");
                for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].type==549)DD2Event.SpawnMonsterFromGate(Main.npc[i].Bottom,Main.npc[i].Center.X<Main.maxTilesX*8);
                for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].type!=type && Main.npc[i].type>=551 && Main.npc[i].type<=578 && NativeCombatLegalCoverageChecks.Receives(host,Main.npc[i]))NativeCombatLegalCoverageChecks.Hit(host,i,1000000);
            }
            throw new InvalidOperationException("Original eligible DD2 source did not produce a same-type peer: "+type);
        }
        private static NpcMotionState[] Capture(MethodInfo read,MethodInfo tracking,object source,IPredictionTerrain terrain,long session)
        {
            var states=new List<NpcMotionState>();
            for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active)
            {
                var n=Main.npc[i];var s=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] args={n,s,terrain};tracking.Invoke(null,args);s=(NpcMotionState)args[1];
                s.DD2TargetFactsCaptured=s.DD2TargetFactsComplete=s.CrowdCaptured=s.CrowdComplete=s.DD2PlayerPremiseCaptured=true;s.HostileProtected=n.dontTakeDamageFromHostiles;s.DD2OldTarget=n.oldTarget;s.DD2PlayerPremiseIndex=-1;s.DD2FaceArea=new MotionRect(n.targetRect.X,n.targetRect.Y,n.targetRect.Width,n.targetRect.Height);
                if(n.target>=300 && n.target<300+Main.maxNPCs){var target=Main.npc[n.target-300];if(target!=null){var targetState=(NpcMotionState)read.Invoke(null,new object[]{target,session});s.DD2TargetIdentity=targetState.Identity;}}
                else if(n.target>=0 && n.target<255)s.DD2TargetPlayerToken=Main.player[n.target];
                var choice=Terraria.Utilities.NPCUtils.SearchForTarget(n,Terraria.Utilities.NPCUtils.TargetSearchFlag.All,Terraria.Utilities.NPCUtils.SearchFilters.OnlyPlayersInCertainDistance(n.Center,200),Terraria.Utilities.NPCUtils.SearchFilters.OnlyCrystal);if(choice.FoundTank)s.DD2PlayerPremiseIndex=choice.NearestTankOwnerIndex;
                states.Add(s);
            }
            return states.ToArray();
        }
    }
}
