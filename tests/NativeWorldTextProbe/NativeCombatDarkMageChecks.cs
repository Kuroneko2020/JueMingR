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
    internal static class NativeCombatDarkMageChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static readonly FieldInfo CorpseField=typeof(DD2Event).GetField("_deadGoblinSpots",Flags);
        private static Action<NPC> before,after;
        private static Action<NPC,Vector2,bool> raising;
        private static Action<Projectile,bool> pulse;
        private static void Before(NPC __instance){before?.Invoke(__instance);}
        private static void After(NPC __instance){after?.Invoke(__instance);}
        private static void RaiseBefore(NPC caller,Vector2 spot){raising?.Invoke(caller,spot,false);}
        private static void RaiseAfter(NPC caller,Vector2 spot){raising?.Invoke(caller,spot,true);}
        private static void PulseBefore(Projectile __instance){if(__instance.type==674)pulse?.Invoke(__instance,false);}
        private static void PulseAfter(Projectile __instance){if(__instance.type==674)pulse?.Invoke(__instance,true);}
        internal static void Boundaries()
        {
            var model=typeof(NpcMotion).Assembly.GetType("JueMingR.Features.Combat.NpcDarkMageMotion",true);
            var unit=model.GetMethod("Unit",Flags);var length=model.GetMethod("Length",Flags);var squared=model.GetMethod("Squared",Flags);var ready=model.GetMethod("FirstReadyAction",Flags);var canRaise=model.GetMethod("CanRaise",Flags);
            for(int i=1;i<=8192;i++)
            {
                float x=((i*7919)%100007-50003)/31f,y=((i*3571)%90007-45003)/29f;var vector=new Vector2(x,y).SafeNormalize(Vector2.UnitY);object[] args={x,y,0f,0f};unit.Invoke(null,args);
                Require((float)args[2]==vector.X && (float)args[3]==vector.Y,"Dark Mage exact native SafeNormalize "+i);
                Require((float)length.Invoke(null,new object[]{x,y})==new Vector2(x,y).Length() && (float)squared.Invoke(null,new object[]{x,y})==new Vector2(x,y).LengthSquared(),"Dark Mage exact native distance stores "+i);
            }
            foreach(int type in new[]{564,565})
            {
                var n=new NpcMotionState{Identity=new NpcIdentity(1,new object(),17,1,type,type),Style=109,Active=true,Width=80,Height=80,L3=60};
                foreach(var pair in new[]{new[]{97,0,117},new[]{127,1,247},new[]{183,2,303}})
                {n.A0=pair[0];n.A1=pair[1];Require((int)ready.Invoke(null,new object[]{n,pair[2]})==pair[2] && (int)ready.Invoke(null,new object[]{n,pair[2]-1})==-1,"Exact cast-end action followed by its native cooldown "+type+"/"+pair[1]);}
                n.A0=-1;n.L3=60;Require((int)ready.Invoke(null,new object[]{n,1})==1,"Cooldown zero selects on the same native action.");n.L3=59;Require((int)ready.Invoke(null,new object[]{n,1})==-1 && (int)ready.Invoke(null,new object[]{n,2})==2,"Fade increment does not precede the same-action ready test.");
                n.A0=0;n.L3=60;float[] xs={n.Bounds.CenterX+800,n.Bounds.CenterX+800,n.Bounds.CenterX+800},ys={n.Bounds.CenterY,n.Bounds.CenterY,n.Bounds.CenterY};var corpses=new PredictionMageCorpses(xs,ys,3);xs[0]+=1;
                Require(corpses.XAt(0)==n.Bounds.CenterX+800 && (bool)canRaise.Invoke(null,new object[]{n,corpses,new[]{n},1}),"Immutable corpses and the inclusive actual 800 radius.");
                var other=n;other.Identity=new NpcIdentity(1,new object(),18,1,type,type);other.MageRaiseOccurred=true;other.MageRaiseX=n.Bounds.CenterX-50;other.MageRaiseY=n.Bounds.CenterY;
                Require(!(bool)canRaise.Invoke(null,new object[]{n,corpses,new[]{n,other},2}),"The lower-slot actual raise consumes its inclusive 850 radius for the next caster.");other.MageRaiseX-=.25f;
                Require((bool)canRaise.Invoke(null,new object[]{n,corpses,new[]{n,other},2}),"A corpse outside the prior caster's real 850 radius remains available.");
            }
            // Read the original projectile facts; this does not replay its AI.
            // Continuous below observes the actual pulse and new health sample.
            NativeCombatWorkerChecks.Scene(false);Projectile.ClearAll();
            var host=Array.Find(AppDomain.CurrentDomain.GetAssemblies(),a=>a.GetType("JueMingR.TerrariaHost.Combat.NpcPredictionSource",false)!=null);
            Require(host!=null,"The genuine host source is loaded.");var deadline=host.GetType("JueMingR.TerrariaHost.Combat.NpcPredictionSource",true).GetMethod("FirstMageHealingConsumer",Flags);
            var q=Main.projectile[0];q.SetDefaults(674);q.active=true;q.position=new Vector2(1000,500);q.velocity=Vector2.Zero;q.owner=Main.myPlayer;q.timeLeft=0;q.ai[0]=39;
            Require((int)deadline.Invoke(null,new object[]{120})==2,"Original AI133 pulse precedes timeLeft retirement and reaches the following NPC action.");
            q.timeLeft=1;q.ai[0]=38;Require((int)deadline.Invoke(null,new object[]{120})==int.MaxValue,"A marker that retires at clock39 cannot heal next frame.");
            q.ai[0]=39;bool active=Main.LocalPlayer.active;Main.LocalPlayer.active=false;Require((int)deadline.Invoke(null,new object[]{120})==2,"Original heal precedes inactive owner retirement.");Main.LocalPlayer.active=active;
            q.position.X=Main.leftWorld;Require((int)deadline.Invoke(null,new object[]{120})==int.MaxValue,"Original world boundary rejects before AI133.");q.active=false;
            var terrain=(IPredictionTerrain)Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);
            var caster=new NpcMotionState{Identity=new NpcIdentity(1,new object(),17,1,564,564),Style=109,Active=true,Width=80,Height=80,A0=65,A1=2,L3=60,MageRelationsCaptured=true,MageRelationsComplete=true,MageHealingAt=int.MaxValue};var peer=caster;peer.Identity=new NpcIdentity(1,new object(),18,1,565,565);
            var group=new[]{caster,peer};var env=new PredictionEnvironment{MageCorpses=new PredictionMageCorpses(new[]{40f},new[]{40f},1)};object[] action={caster,env,terrain,1,PredictionStop.None,group,2,false};
            Require((bool)model.GetMethod("Step",Flags).Invoke(null,action) && ((NpcMotionState)action[0]).A0==64 && ((NpcMotionState)action[0]).MageRaiseOccurred && !((NpcMotionState)action[0]).MageRelationsComplete && !group[1].MageRelationsComplete,"The actual raise64 boundary consumes private corpses and invalidates every caster's newborn-sensitive relation closure while the known cast continues.");
            env.Multiplayer=true;group=new[]{caster,peer};action[0]=caster;action[1]=env;action[5]=group;
            Require((bool)model.GetMethod("Step",Flags).Invoke(null,action) && !((NpcMotionState)action[0]).MageRaiseOccurred && ((NpcMotionState)action[0]).MageRelationsComplete && group[1].MageRelationsComplete,"A client waiting for the server does not invent corpse consumption or newborns.");
            Console.WriteLine("PASS Dark Mage native math8192 / full cast clocks / immutable slot-ordered corpse consumption / original 674 consumer ordering");
        }
        internal static void Continuous(object context,NpcPredictionCache cache,Action step,string output,ProbeGraphics graphics=null)
        {
            object host=Get(context,"CombatObservation"),source=Get(host,"Prediction");long session=(long)Get(host,"Session");Require(GetOptional(source,"Native")==null,"Dark Mage uses the scalar producer.");
            var read=source.GetType().GetMethod("Read",Flags);var tracking=source.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcTrackingObservation",true).GetMethod("Capture",Flags);var terrain=(IPredictionTerrain)Get(source,"Terrain");
            var owner=new Harmony("JueMingR.Probe.DarkMageOracle");var update=typeof(NPC).GetMethod("UpdateNPC",Flags);var raise=typeof(DD2Event).GetMethod("RaiseGoblins",Flags);var projectileAI=typeof(Projectile).GetMethod("AI",Flags);
            owner.Patch(update,prefix:new HarmonyMethod(typeof(NativeCombatDarkMageChecks).GetMethod(nameof(Before),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatDarkMageChecks).GetMethod(nameof(After),Flags)));
            owner.Patch(raise,prefix:new HarmonyMethod(typeof(NativeCombatDarkMageChecks).GetMethod(nameof(RaiseBefore),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatDarkMageChecks).GetMethod(nameof(RaiseAfter),Flags)));
            owner.Patch(projectileAI,prefix:new HarmonyMethod(typeof(NativeCombatDarkMageChecks).GetMethod(nameof(PulseBefore),Flags)),postfix:new HarmonyMethod(typeof(NativeCombatDarkMageChecks).GetMethod(nameof(PulseAfter),Flags)));
            var rows=new List<string>{"scene,frame,cast,clock,fade,target,selected,published,count,stop,actions,crystal,matched,longMatched,cross,attack,heal,raise,consumed,healed,receiving,restored,error"};var failures=new List<string>();int totalMatched=0,totalActions=0,totalCrystal=0,totalAttack=0,totalHeal=0,totalRaise=0,totalConsumed=0,totalHealed=0,totalRestored=0;
            try
            {
                Require(graphics!=null,"Actual DD2 attacks, healing and crystal loss retain the original projectile effects.");graphics.LoadScreenEffects();
                for(int scene=0;scene<6;scene++)
                {
                    try
                    {
                        int type=scene<3?564:565,mode=scene%3;string birth;var actor=Main.npc[NativeCombatDd2CoverageChecks.Birth(type,source.GetType().Assembly,out birth)];
                        var p=Main.LocalPlayer;p.isControlledByFilm=true;p.releaseJump=true;p.controlLeft=p.controlRight=p.controlUp=p.controlDown=p.controlJump=false;p.Center=actor.Center+new Vector2(120,0);p.ResetFallDamage();p.statLife=p.statLifeMax=p.statLifeMax2=5000;p.immune=true;p.immuneTime=1000000;for(int i=0;i<p.hurtCooldowns.Length;i++)p.hurtCooldowns[i]=1000000;
                        // These siblings enter through the original source API
                        // with default AI. Their casts and choices remain native.
                        NPC sibling=null;if(mode>0)sibling=Main.npc[NPC.NewNPC(actor.GetSpawnSourceForNPCFromNPCAI(),(int)actor.Center.X+60,(int)actor.Bottom.Y,type,Start:1,Target:Main.myPlayer)];
                        int actualDeaths=0;if(mode==1)
                        {
                            // AI133 explicitly excludes564/565. Preserve the
                            // two-caster clock relation but wound genuine
                            // nonmage actors through real receiving actions.
                            int goblin=type==565?554:552;
                            for(int at=0;at<2;at++){int slot=NPC.NewNPC(actor.GetSpawnSourceForNPCFromNPCAI(),(int)actor.Center.X+at*24,(int)actor.Bottom.Y,goblin,Start:1,Target:Main.myPlayer);Wound(Main.npc[slot]);}
                        }
                        else if(mode==2)
                        {
                            int goblin=type==565?554:552;
                            for(int at=0;at<5;at++){int slot=NPC.NewNPC(actor.GetSpawnSourceForNPCFromNPCAI(),(int)actor.Center.X+at*24-48,(int)actor.Bottom.Y,goblin,Start:1,Target:Main.myPlayer);var n=Main.npc[slot];int prior=Corpses().Count;NativeCombatLegalCoverageChecks.Hit(source.GetType().Assembly,slot,1000000);Require(!n.active && Corpses().Count>prior,"Actual goblin death adds its original Bottom to the corpse pool.");actualDeaths++;}
                        }
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(path:true,mouseCenter:true,clearLine:false,radius:50));
                        var freezes=new List<NpcTrajectory>();var phases=new Dictionary<NpcTrajectory,string>();int actions=0,crystal=0,matched=0,longMatched=0,cross=0,selected=0,published=0,attack=0,heal=0,raised=0,consumed=0,healed=0,receiving=0,restored=0,pulses=0;double maxError=0,tangent=0;bool sawFade=false,sawReady=false,awaitingRecovery=false;
                        NPC pending=null;NpcMotionState predicted=default(NpcMotionState),input=default(NpcMotionState);bool valid=false;int nativeFrame=-1,priorCorpse=0;string previousPremise=Premise();long changedTick=-1;NpcTrajectory beforeChange=null;
                        raising=(caller,spot,completed)=>
                        {
                            if(!completed){priorCorpse=Corpses().Count;return;}int afterCount=Corpses().Count;Require(afterCount<=priorCorpse,"Original raise consumes rather than invents corpse points.");raised++;consumed+=priorCorpse-afterCount;
                            Console.WriteLine("DARKMAGE ORIGINAL-RAISE scene="+scene+" frame="+nativeFrame+" tick="+Main.GameUpdateCount+" slot="+caller.whoAmI+" generation="+caller.generation+" clock="+caller.ai[0]+" center="+spot+" corpses="+priorCorpse+"->"+afterCount);
                        };
                        var pulseHealth=new Dictionary<NPC,int>();Projectile pulseActor=null;bool reachesPulse=false;
                        pulse=(q,completed)=>
                        {
                            if(!completed)
                            {
                                pulseActor=q;reachesPulse=q.active && q.ai[0]+1>=40;pulseHealth.Clear();if(!reachesPulse)return;
                                for(int at=0;at<Main.maxNPCs;at++){var n=Main.npc[at];if(n.active && n.damage>=1 && n.lifeMax>=30 && n.type!=564 && n.type!=565 && q.Distance(n.Center)<=1000)pulseHealth[n]=n.life;}
                                return;
                            }
                            if(!reachesPulse || !ReferenceEquals(q,pulseActor))return;
                            Require(q.ai[0]>=40 && !q.active,"Original674 heals and kills in the same AI action; it cannot be counted as an active post-frame marker.");pulses++;
                            foreach(var pair in pulseHealth)if(pair.Key.active && pair.Key.life>pair.Value){healed++;Require(pair.Key.life-pair.Value==Math.Min(500,pair.Key.lifeMax-pair.Value),"The actual nonmage pulse applies its bounded native missing-health amount.");}
                            Console.WriteLine("DARKMAGE ORIGINAL-HEAL scene="+scene+" frame="+nativeFrame+" tick="+Main.GameUpdateCount+" projectile="+q.whoAmI+" key="+(int)q.key+" clock="+q.ai[0]+" active="+q.active+" eligible="+pulseHealth.Count+" healed="+healed);reachesPulse=false;
                        };
                        before=n=>
                        {
                            pending=n;valid=false;if(!n.active || !(n.aiStyle==109 && (n.type==564 || n.type==565) || n.type==548))return;
                            var group=Capture(read,tracking,terrain,session);int index=-1;for(int i=0;i<group.Length;i++)if(ReferenceEquals(group[i].Identity.Token,n)){index=i;break;}if(index<0)return;predicted=input=group[index];
                            // This is a current native-slot oracle: the whole
                            // actual active group is freshly observed, not frozen.
                            terrain.Reset();PredictionStop stop;valid=NpcMotion.Step(ref predicted,group,group.Length,EnvironmentFor(),terrain,1,true,out stop);
                            Require(valid || stop==PredictionStop.Despawn || stop==PredictionStop.PhaseBoundary || stop==PredictionStop.MissingDependency,"Actual Dark Mage deterministic motor boundary: "+stop);
                        };
                        after=n=>
                        {
                            if(!valid || !ReferenceEquals(pending,n))return;double error=Vector2.Distance(new Vector2(predicted.X,predicted.Y),n.position);
                            bool receivingCrystal=n.type==548 && input.A1!=1 && n.ai[1]==1;
                            Require(error<=.08 && Math.Abs(predicted.Vx-n.velocity.X)<=.0001 && Math.Abs(predicted.Vy-n.velocity.Y)<=.0001 && (receivingCrystal || predicted.A0==n.ai[0] && predicted.A1==n.ai[1]),"Original Dark Mage full action scene="+scene+" frame="+nativeFrame+" type="+n.type+" input="+input.A0+"/"+input.A1+" predicted="+predicted.A0+"/"+predicted.A1+" actual="+string.Join("/",n.ai)+" error="+error+" predictedV="+predicted.Vx+","+predicted.Vy+" actualV="+n.velocity+" target="+n.target);
                            if(n.type==548){crystal++;if(receivingCrystal)Require(n.life==n.lifeMax && n.dontTakeDamageFromHostiles,"The original crystal receiving action owns its new loss health/phase.");return;}actions++;
                            Require(predicted.L0==n.localAI[0] && predicted.L1==n.localAI[1] && predicted.L2==n.localAI[2] && predicted.L3==n.localAI[3] && predicted.Alpha==n.alpha && predicted.Direction==n.direction && predicted.SpriteDirection==n.spriteDirection,"Original fade, stuck reset, facing and hover facts remain exact.");
                            if(n.localAI[3]<60)sawFade=true;else sawReady=true;if(input.A0>0 && n.ai[0]==32 && n.ai[1]==0)attack++;
                            if(input.A0>0 && n.ai[1]==1 && (n.ai[0]==40 || n.ai[0]==48 || n.ai[0]==56))heal++;
                        };
                        for(int frame=0;frame<2400 && actor.active && DD2Event.Ongoing;frame++)
                        {
                            nativeFrame=frame;beforeChange=cache.Read(0);NativeCombatModeledImpactChecks.SampleMouse(context,actor.Center);step();if(!actor.active)break;
                            string premise=Premise();if(previousPremise!=premise)
                            {
                                receiving++;changedTick=(long)Main.GameUpdateCount;freezes.Clear();phases.Clear();awaitingRecovery=true;previousPremise=premise;
                                var current=cache.Read(0);var prepared=GetOptional(Get(host,"World"),"preparedPath") as NpcTrajectory;
                                Require(current==null || current.CaptureTick==Main.GameUpdateCount && !ReferenceEquals(current,beforeChange),"The completed health/corpse/newborn input frame retires its old production publication.");Require(prepared==null || ReferenceEquals(prepared,current),"The real consumer never retains the pre-change Dark Mage path.");
                            }
                            string phase=actor.ai[1]+"/"+(actor.ai[0]>0)+"/"+(actor.localAI[3]>=60);bool chosen=(bool)Get(Get(host,"Selection"),"HasTarget") && ReferenceEquals(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Token,actor);if(chosen)selected++;
                            var path=cache.Read(0);if(path!=null && ReferenceEquals(path.Identity.Token,actor)){Require(chosen && path.CaptureTick==Main.GameUpdateCount,"The original event identity owns the current Dark Mage publication.");published++;if(awaitingRecovery && path.Count>30){restored++;awaitingRecovery=false;}if(frame%5==0 && path.Count>1){freezes.Add(path);phases[path]=phase;}}
                            for(int i=freezes.Count-1;i>=0;i--)
                            {
                                var frozen=freezes[i];int offset=(int)((long)Main.GameUpdateCount-frozen.CaptureTick);if(offset>=frozen.Count){freezes.RemoveAt(i);phases.Remove(frozen);continue;}if(offset<=0)continue;var point=frozen[offset];double error=Vector2.Distance(new Vector2(point.Bounds.X,point.Bounds.Y),actor.position);maxError=Math.Max(maxError,error);matched++;
                                Require(error<=.08 && point.CanReceive==(!actor.dontTakeDamage && !actor.immortal) && point.NoTileCollide==actor.noTileCollide,"Frozen original Dark Mage scene="+scene+" frame="+frame+" capture="+frozen.CaptureTick+" offset="+offset+" error="+error+" predicted="+point.Bounds.X+","+point.Bounds.Y+" actual="+actor.position+" phase="+string.Join("/",actor.ai)+" V="+actor.velocity+" target="+actor.target+" pc="+p.position+" pcV="+p.velocity+" stop="+frozen.Stop+" latestReceiving="+changedTick);
                                if(offset>=30){longMatched++;tangent=Math.Max(tangent,Vector2.Distance(actor.position,new Vector2(frozen[0].Bounds.X+frozen[0].Vx*offset,frozen[0].Bounds.Y+frozen[0].Vy*offset)));}if(phases[frozen]!=phase)cross++;
                            }
                            rows.Add(string.Join(",",scene,frame,actor.ai[1],actor.ai[0],actor.localAI[3],actor.target,chosen,path!=null,path?.Count??0,path?.Stop.ToString()??"none",actions,crystal,matched,longMatched,cross,attack,heal,raised,consumed,healed,receiving,restored,maxError.ToString("R",CultureInfo.InvariantCulture)));
                            if(frame%600==0)Console.WriteLine("DARKMAGE progress scene="+scene+" frame="+frame+" phase="+string.Join("/",actor.ai)+" actions="+actions+" long="+longMatched+" attack="+attack+" heal="+heal+" raise="+raised);
                        }
                        before=after=null;raising=null;pulse=null;Require(actions>=500 && crystal>=500 && selected>0 && published>0 && longMatched>=30 && cross>=30 && tangent>8,"Actual Dark Mage long windows cross fade/cast/cooldown rather than a tangent: scene="+scene+" actions="+actions+" long="+longMatched+" cross="+cross+" tangent="+tangent);
                        Require(sawReady && attack>0,"The bounded natural stage route must actually attack.");if(mode==1)Require(heal>0 && pulses>0 && healed>0 && receiving>0 && restored>0,"Actual wounded nonmage actors select heal; the original674 pulse changes their health and a new long forecast recovers.");if(mode==2)Require(actualDeaths==5 && raised>0 && consumed>=3 && restored>0,"Actual deaths, original slot-ordered consumption and newborn observation restore known casts.");
                        totalMatched+=matched;totalActions+=actions;totalCrystal+=crystal;totalAttack+=attack;totalHeal+=heal;totalRaise+=raised;totalConsumed+=consumed;totalHealed+=healed;totalRestored+=restored;
                        Console.WriteLine("DARKMAGE scene="+scene+" birth="+birth+" matched="+matched+" long="+longMatched+" cross="+cross+" actions="+actions+" crystal="+crystal+" attack="+attack+" heal="+heal+" pulses="+pulses+" raises="+raised+" consumed="+consumed+" healed="+healed+" deaths="+actualDeaths+" receiving="+receiving+" restored="+restored+" fade="+sawFade+" error="+maxError);
                    }
                    catch(Exception failure) when(!(failure is OutOfMemoryException)){before=after=null;raising=null;pulse=null;failures.Add(scene+": "+failure.Message);Console.WriteLine("DARKMAGE FAIL scene="+scene+": "+failure);}
                }
                Require(failures.Count==0,"Dark Mage natural failures="+failures.Count+": "+string.Join(" | ",failures));Console.WriteLine("PASS original Dark Mage scenes=6 matched="+totalMatched+" actions="+totalActions+" crystal="+totalCrystal+" attacks="+totalAttack+" heals="+totalHeal+" raises="+totalRaise+" consumed="+totalConsumed+" healed="+totalHealed+" restored="+totalRestored);
            }
            finally{before=after=null;raising=null;pulse=null;owner.Unpatch(update,HarmonyPatchType.All,owner.Id);owner.Unpatch(raise,HarmonyPatchType.All,owner.Id);owner.Unpatch(projectileAI,HarmonyPatchType.All,owner.Id);Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"dark-mage-continuous.csv"),rows);}
        }
        private static void Wound(NPC n)
        {int damage=Math.Max(1,n.lifeMax/3)+n.defense;n.StrikeNPCNoInteraction(damage,0,0);Require(n.active && n.life>0 && n.life<n.lifeMax,"The actual receiving action leaves a wounded living DD2 actor.");}
        private static List<Vector2> Corpses(){return (List<Vector2>)CorpseField.GetValue(null);}
        private static string Premise()
        {
            var values=new System.Text.StringBuilder();for(int i=0;i<Main.maxNPCs;i++){var n=Main.npc[i];if(n.active)values.Append(i).Append(':').Append(n.generation).Append(':').Append(n.type).Append(':').Append(n.life).Append(';');}foreach(var spot in Corpses())values.Append(spot.X.ToString("R",CultureInfo.InvariantCulture)).Append('/').Append(spot.Y.ToString("R",CultureInfo.InvariantCulture)).Append(';');return values.ToString();
        }
        private static NpcMotionState[] Capture(MethodInfo read,MethodInfo tracking,IPredictionTerrain terrain,long session)
        {
            var states=new List<NpcMotionState>();for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active)
            {
                var n=Main.npc[i];var s=(NpcMotionState)read.Invoke(null,new object[]{n,session});object[] args={n,s,terrain};tracking.Invoke(null,args);s=(NpcMotionState)args[1];s.DD2TargetFactsCaptured=s.DD2TargetFactsComplete=s.DD2PlayerPremiseCaptured=true;s.DD2PlayerPremiseIndex=Main.myPlayer;s.HostileProtected=n.dontTakeDamageFromHostiles;s.DD2OldTarget=n.oldTarget;s.DD2FaceArea=new MotionRect(n.targetRect.X,n.targetRect.Y,n.targetRect.Width,n.targetRect.Height);s.MageRelationsCaptured=s.MageRelationsComplete=true;s.MageHealingAt=int.MaxValue;
                if(n.target>=300 && n.target<300+Main.maxNPCs)s.DD2TargetIdentity=((NpcMotionState)read.Invoke(null,new object[]{Main.npc[n.target-300],session})).Identity;else if(n.target>=0 && n.target<255)s.DD2TargetPlayerToken=Main.player[n.target];states.Add(s);
            }return states.ToArray();
        }
        private static PredictionEnvironment EnvironmentFor()
        {
            var values=new List<PredictionInvasionPlayer>();for(int i=0;i<255;i++)
            {
                var p=Main.player[i];if(p==null || !p.active)continue;uint mask=0;for(int type=552;type<=577;type++)if(p.npcTypeNoAggro[type])mask|=1u<<(type-552);if(p.npcTypeNoAggro[620])mask|=1u<<26;if(p.npcTypeNoAggro[551])mask|=1u<<27;if(p.npcTypeNoAggro[578])mask|=1u<<28;
                Projectile pet=p.tankPet>=0 && p.tankPet<Main.maxProjectiles?Main.projectile[p.tankPet]:null;values.Add(new PredictionInvasionPlayer(i,p,new MotionRect(p.position.X,p.position.Y,p.width,p.height),p.velocity.X,p.velocity.Y,p.aggro,mask,!p.dead && !p.ghost,p.itemAnimation==0 && p.aggro<0,pet,pet==null?default(MotionRect):new MotionRect(pet.position.X,pet.position.Y,pet.width,pet.height)));
            }
            var spots=Corpses();var xs=new float[spots.Count];var ys=new float[spots.Count];for(int i=0;i<spots.Count;i++){xs[i]=spots[i].X;ys[i]=spots[i].Y;}var pc=Main.LocalPlayer;
            return new PredictionEnvironment{MageCorpses=new PredictionMageCorpses(xs,ys,spots.Count),InvasionPlayers=new PredictionInvasionPlayers(values.ToArray(),values.Count),DD2OngoingCaptured=true,DD2Ongoing=DD2Event.Ongoing,PlayerIndex=Main.myPlayer,PlayerX=pc.Center.X,PlayerY=pc.Center.Y,PlayerWidth=pc.width,PlayerHeight=pc.height,PlayerVx=pc.velocity.X,PlayerVy=pc.velocity.Y,PlayerVelocityCaptured=true,PlayerTimelineActive=pc.active && !pc.dead && !pc.ghost,PlayerDead=pc.dead || !pc.active,Expert=Main.expertMode,Multiplayer=Main.netMode==1,WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,WorldSurface=(float)Main.worldSurface,GravityWorldSurface=Main.worldSurface};
        }
    }
}
