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
    internal static class NativeCombatInvasionLightningChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Action<NPC> before,after;
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        internal static void Boundaries()
        {
            var model=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcInvasionLightningMotion",true);
            var unit=model.GetMethod("Unit",Flags);var length=model.GetMethod("Length",Flags);var smooth=model.GetMethod("Smooth",Flags);
            for(int i=1;i<=8192;i++)
            {
                float x=((i*7919)%100007-50003)/31f,y=((i*3571)%90007-45003)/29f;var vector=new Vector2(x,y).SafeNormalize(Vector2.UnitY);object[] args={x,y,0f,0f};unit.Invoke(null,args);
                Require((float)args[2]==vector.X && (float)args[3]==vector.Y,"Lightning Bug exact native SafeNormalize "+i);
                Require((float)length.Invoke(null,new object[]{x,y})==new Vector2(x,y).Length(),"Lightning Bug exact native Length "+i);
                var original=(new Vector2(x,y)*19+new Vector2(y,x))/20;
                Require((float)smooth.Invoke(null,new object[]{x,y})==original.X,"Lightning Bug exact native Vector2 smoothing stores "+i);
            }
            foreach(float value in new[]{0f,float.NaN}){object[] args={value,value,0f,0f};unit.Invoke(null,args);Require((float)args[2]==0 && (float)args[3]==1,"Actual zero/NaN SafeNormalize fallback is UnitY.");}
            Console.WriteLine("PASS original Lightning Bug SafeNormalize/Length/Vector2 smoothing matrix=8192 and zero/NaN fallback");
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");long session=(long)Get(host,"Session");Require(GetOptional(source,"Native")==null,"Lightning Bug uses the scalar DD2 producer.");
            var read=source.GetType().GetMethod("Read",Flags);var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);var terrain=(IPredictionTerrain)Get(source,"Terrain");
            var rows=new List<string>{"scene,frame,target,alpha,fade,shotClock,vx,vy,selected,published,count,stop,matched,longMatched,cross,error,pcX,pcY,pcVx,pcVy,pcLife,pcDead"};var failures=new List<string>();
            var owner=new Harmony("JueMingR.Probe.InvasionLightningOracle");var update=typeof(NPC).GetMethod("UpdateNPC",Flags);owner.Patch(update,prefix:new HarmonyMethod(typeof(NativeCombatInvasionLightningChecks).GetMethod(nameof(Before),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatInvasionLightningChecks).GetMethod(nameof(After),Flags)));
            int totalMatches=0,totalActions=0,totalCrystal=0,totalShots=0,totalCrowd=0;
            try
            {
                for(int scene=0;scene<4;scene++)
                {
                    try
                    {
                        string birth;var actor=Main.npc[NativeCombatDd2CoverageChecks.Birth(578,source.GetType().Assembly,out birth)];CrowdBirth(source.GetType().Assembly);
                        var p=Main.LocalPlayer;p.isControlledByFilm=true;p.releaseJump=true;p.controlLeft=p.controlRight=p.controlUp=p.controlDown=p.controlJump=false;p.immune=true;p.immuneTime=1000000;p.statLife=p.statLifeMax=p.statLifeMax2=5000;for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
                        if(scene==3)p.Center=actor.Center+new Vector2(0,-150);p.ResetFallDamage();
                        if(scene==2)
                        {
                            int x=(int)((actor.Center.X+p.Center.X)*.5f/16);for(int y=25;y<65;y++){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}
                        }
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                        var freezes=new List<NpcTrajectory>();var phases=new Dictionary<NpcTrajectory,string>();int matched=0,longMatched=0,cross=0,actions=0,crystal=0,shots=0,crowd=0,published=0,selected=0,blocked=0;double maxError=0,tangent=0;bool sawPlayer=false,sawNpc=false,sawFade=false,sawReady=false;
                        NPC pending=null;NpcMotionState predicted=default(NpcMotionState),input=default(NpcMotionState);bool valid=false;
                        before=n=>
                        {
                            pending=n;valid=false;if(!n.active || !(n.type==578 || n.type==548))return;
                            var group=Capture(read,tracking,terrain,session);int index=-1;for(int i=0;i<group.Length;i++)if(ReferenceEquals(group[i].Identity.Token,n)){index=i;break;}if(index<0)return;predicted=input=group[index];
                            if(n.type==578)foreach(var peer in group)if(peer.EffectiveType==578 && !peer.Identity.Equals(input.Identity) && Math.Abs(input.X-peer.X)+Math.Abs(input.Y-peer.Y)<input.Width){crowd++;break;}
                            terrain.Reset();PredictionStop stop;valid=NpcMotion.Step(ref predicted,group,group.Length,EnvironmentFor(),terrain,1,true,out stop);
                            Require(valid || stop==PredictionStop.Despawn || stop==PredictionStop.PhaseBoundary || stop==PredictionStop.MissingDependency,"Native self motor never truncates on dust or projectile-only RNG: "+stop);
                        };
                        after=n=>
                        {
                            if(!valid || !ReferenceEquals(pending,n))return;double error=Vector2.Distance(new Vector2(predicted.X,predicted.Y),n.position);
                            Require(error<=.08 && Math.Abs(predicted.Vx-n.velocity.X)<=.0001 && Math.Abs(predicted.Vy-n.velocity.Y)<=.0001 && predicted.Direction==n.direction && predicted.SpriteDirection==n.spriteDirection && predicted.Alpha==n.alpha,"Original Lightning Bug full slot action scene="+scene+" type="+n.type+" error="+error+" predictedV="+predicted.Vx+","+predicted.Vy+" actual="+n.velocity+" direction="+n.direction+" target="+n.target);
                            if(n.type==548){crystal++;return;}actions++;Require(predicted.L0==n.localAI[0] && predicted.L1==n.localAI[1],"Native fade and charge/shot clocks remain exact across firing actions.");
                            if(input.L0>=0 && n.localAI[0]==-30)shots++;
                            var target=n.GetTargetData();if(!Collision.CanHit(n.Center,1,1,target.Center,1,1))blocked++;
                            if(n.target>=0 && n.target<255)sawPlayer=true;if(n.target>=300)sawNpc=true;if(n.localAI[1]<60)sawFade=true;else if(n.alpha==0)sawReady=true;
                        };
                        string previousClosure=null;
                        for(int frame=0;frame<1200 && actor.active && DD2Event.Ongoing;frame++)
                        {
                            if(scene==1 && frame%240==0){p.controlRight=frame%480==0;p.controlLeft=!p.controlRight;freezes.Clear();}
                            NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();if(!actor.active)break;
                            string closure="";for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].type==578)closure+=i+":"+Main.npc[i].generation+";";if(previousClosure!=null && previousClosure!=closure)freezes.Clear();previousClosure=closure;
                            string phase=actor.target+"/"+(actor.localAI[0]<0)+"/"+(actor.localAI[1]>=60);bool chosen=(bool)Get(Get(host,"Selection"),"HasTarget") && ReferenceEquals(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Token,actor);if(chosen)selected++;
                            var path=cache.Read(0);if(path!=null && ReferenceEquals(path.Identity.Token,actor)){Require(chosen && path.CaptureTick==Main.GameUpdateCount,"The genuine wave actor owns the current production publication.");published++;if(frame%5==0 && path.Count>1){freezes.Add(path);phases[path]=phase;}}
                            for(int i=freezes.Count-1;i>=0;i--)
                            {
                                var frozen=freezes[i];int offset=(int)((long)Main.GameUpdateCount-frozen.CaptureTick);if(offset>=frozen.Count){freezes.RemoveAt(i);continue;}if(offset<=0)continue;var point=frozen[offset];double error=Vector2.Distance(new Vector2(point.Bounds.X,point.Bounds.Y),actor.position);maxError=Math.Max(maxError,error);matched++;
                                Require(error<=.08 && point.CanReceive==(!actor.dontTakeDamage && !actor.immortal) && point.NoTileCollide==actor.noTileCollide,"Frozen original Lightning Bug scene="+scene+" frame="+frame+" capture="+frozen.CaptureTick+" offset="+offset+" error="+error+" predicted="+point.Bounds.X+","+point.Bounds.Y+" actual="+actor.position+" predictedV="+point.Vx+","+point.Vy+" actualV="+actor.velocity+" target="+actor.target+" player="+p.position+" playerV="+p.velocity+" shotClock="+actor.localAI[0]+" stop="+frozen.Stop);
                                if(offset>=30){longMatched++;tangent=Math.Max(tangent,Vector2.Distance(actor.position,new Vector2(frozen[0].Bounds.X+frozen[0].Vx*offset,frozen[0].Bounds.Y+frozen[0].Vy*offset)));}if(phases[frozen]!=phase)cross++;
                            }
                            rows.Add(string.Join(",",scene,frame,actor.target,actor.alpha,actor.localAI[1],actor.localAI[0],actor.velocity.X,actor.velocity.Y,chosen,path!=null,path?.Count??0,path?.Stop.ToString()??"none",matched,longMatched,cross,maxError.ToString("R",CultureInfo.InvariantCulture),p.position.X,p.position.Y,p.velocity.X,p.velocity.Y,p.statLife,p.dead));
                        }
                        before=after=null;Require(actions>=500 && crystal>=500 && selected>0 && published>0 && longMatched>=30 && cross>=30 && tangent>8,"Natural DD2 curves cross fade and observed charge/cooldown branches through long windows: "+scene+" actions="+actions+" long="+longMatched+" cross="+cross);
                        Require(crowd>0 && sawFade && sawReady,"Actual fade, ready state and same-type spacing are exercised without replaying RNG.");
                        // Native's blocked-CanHit branch bypasses the shot-clock
                        // increment entirely. Keep the wall's pursuit proof
                        // separate from the clear arena's real firing proof.
                        if(scene==2)Require(blocked>0,"Actual wall blocks native CanHit and selects the pursuit branch.");else Require(shots>0,"Clear natural arenas must actually fire and cross the -30 cooldown clock.");if(scene==3)Require(sawPlayer && sawNpc,"Native target search crosses numbered player and encoded crystal targets within the same real scenario.");
                        totalMatches+=matched;totalActions+=actions;totalCrystal+=crystal;totalShots+=shots;totalCrowd+=crowd;Console.WriteLine("LIGHTNING scene="+scene+" birth="+birth+" matched="+matched+" long="+longMatched+" cross="+cross+" actions="+actions+" crystal="+crystal+" shots="+shots+" crowd="+crowd+" blocked="+blocked+" player="+sawPlayer+" npc="+sawNpc+" error="+maxError);
                    }
                    catch(Exception failure) when(!(failure is OutOfMemoryException)){before=after=null;failures.Add(scene+": "+failure.Message);Console.WriteLine("LIGHTNING FAIL scene="+scene+": "+failure);}
                }
                Require(failures.Count==0 && totalShots>0,"Natural Lightning Bug failures="+failures.Count+": "+string.Join(" | ",failures));Console.WriteLine("PASS original Lightning Bug scenes=4 matched="+totalMatches+" actions="+totalActions+" crystal="+totalCrystal+" shots="+totalShots+" crowd="+totalCrowd);
            }
            finally{before=after=null;owner.Unpatch(update,HarmonyPatchType.All,owner.Id);Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"invasion-lightning-continuous.csv"),rows);}
        }
        private static void CrowdBirth(Assembly host)
        {
            for(int attempt=0;attempt<256;attempt++)
            {
                int count=0;for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].type==578)count++;if(count>=2)return;
                Require(DD2Event.Ongoing && !DD2Event.EnemySpawningIsOnHold,"The original wave still permits legal same-type births.");for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].type==549)DD2Event.SpawnMonsterFromGate(Main.npc[i].Bottom,Main.npc[i].Center.X<Main.maxTilesX*8);
                for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].type!=578 && Main.npc[i].type>=551 && Main.npc[i].type<=577 && NativeCombatLegalCoverageChecks.Receives(host,Main.npc[i]))NativeCombatLegalCoverageChecks.Hit(host,i,1000000);
            }throw new InvalidOperationException("Bounded real gate births did not create Lightning Bug crowd.");
        }
        private static NpcMotionState[] Capture(MethodInfo read,MethodInfo tracking,IPredictionTerrain terrain,long session)
        {
            var rows=new List<NpcMotionState>();for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && (Main.npc[i].type==578 || Main.npc[i].type==548))
            {
                var n=Main.npc[i];var s=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] facts={n,s,terrain};tracking.Invoke(null,facts);s=(NpcMotionState)facts[1];s.DD2TargetFactsCaptured=s.DD2TargetFactsComplete=s.DD2PlayerPremiseCaptured=s.CrowdCaptured=s.CrowdComplete=true;s.DD2PlayerPremiseIndex=Main.myPlayer;s.DD2OldTarget=n.oldTarget;s.HostileProtected=n.dontTakeDamageFromHostiles;s.DD2FaceArea=new MotionRect(n.targetRect.X,n.targetRect.Y,n.targetRect.Width,n.targetRect.Height);
                if(n.target>=300 && n.target<300+Main.maxNPCs)s.DD2TargetIdentity=((NpcMotionState)read.Invoke(null,new object[]{Main.npc[n.target-300],session})).Identity;else if(n.target>=0 && n.target<255)s.DD2TargetPlayerToken=Main.player[n.target];rows.Add(s);
            }return rows.ToArray();
        }
        private static PredictionEnvironment EnvironmentFor()
        {
            var values=new List<PredictionInvasionPlayer>();for(int i=0;i<255;i++)
            {
                var p=Main.player[i];if(p==null || !p.active)continue;uint mask=0;for(int type=552;type<=577;type++)if(p.npcTypeNoAggro[type])mask|=1u<<(type-552);if(p.npcTypeNoAggro[620])mask|=1u<<26;if(p.npcTypeNoAggro[551])mask|=1u<<27;if(p.npcTypeNoAggro[578])mask|=1u<<28;
                Projectile pet=p.tankPet>=0 && p.tankPet<Main.maxProjectiles?Main.projectile[p.tankPet]:null;values.Add(new PredictionInvasionPlayer(i,p,new MotionRect(p.position.X,p.position.Y,p.width,p.height),p.velocity.X,p.velocity.Y,p.aggro,mask,!p.dead && !p.ghost,p.itemAnimation==0 && p.aggro<0,pet,pet==null?default(MotionRect):new MotionRect(pet.position.X,pet.position.Y,pet.width,pet.height)));
            }
            var pc=Main.LocalPlayer;return new PredictionEnvironment{InvasionPlayers=new PredictionInvasionPlayers(values.ToArray(),values.Count),DD2OngoingCaptured=true,DD2Ongoing=DD2Event.Ongoing,PlayerIndex=Main.myPlayer,PlayerX=pc.Center.X,PlayerY=pc.Center.Y,PlayerWidth=pc.width,PlayerHeight=pc.height,PlayerVx=pc.velocity.X,PlayerVy=pc.velocity.Y,PlayerVelocityCaptured=true,PlayerTimelineActive=pc.active && !pc.dead && !pc.ghost,PlayerDead=pc.dead || !pc.active,Expert=Main.expertMode,Multiplayer=Main.netMode==1,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,GravityWorldSurface=Main.worldSurface};
        }
    }
}
