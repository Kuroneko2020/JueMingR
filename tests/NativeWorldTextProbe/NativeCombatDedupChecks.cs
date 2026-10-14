using System;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // This leaf separates a frozen same-opportunity sharing oracle from normal
    // 6ms native Player/NPC/Projectile loops. Counters never enter the product.
    internal static class NativeCombatDedupChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static int solves,births,enhanced,basic,shares,hits,motionSteps,spearSteps,solveDepth;
        private static object attack;
        private static AttackContact proposed,recorded;
        private static int nativeFrame,scenarioItem;private static bool scenarioMoving;
        private static void Solve(){solves++;solveDepth++;}
        private static Exception Solved(Exception __exception){solveDepth--;return __exception;}
        private static void MotionStep(){motionSteps++;}
        private static void SpearStep(){if(solveDepth>0)spearSteps++;}
        private static void Share(bool __result){if(__result)shares++;}
        private static void Proposed(AttackContact __0){proposed=__0;}
        private static void Born(Player __instance,Projectile __0)
        {
            if(__instance.whoAmI!=0 || __0.owner!=0 || __0.type!=14 && __0.type!=222)return;births++;
            var packet=Get(attack,"consumedInput");bool current=(uint)Get(attack,"consumedInputStep")==Main.GameUpdateCount;
            Console.WriteLine("DEDUP birth item="+scenarioItem+" moving="+scenarioMoving+" frame="+nativeFrame+" tick="+Main.GameUpdateCount+" current="+current+" kind="+Get(packet,"Kind")+" target="+((NpcIdentity)Get(packet,"Target")).Slot+" point="+Get(packet,"Point")+" velocity="+__0.velocity+" sample="+(recorded?.Timeline.SampleTick??-1)+" capture="+(recorded?.Timeline.CaptureTick??-1)+" receive="+(recorded?.ReceiveTick??-1));
        }
        private static void Hit(NPC __instance,int __result){if(__instance.whoAmI==2 && __result>0)hits++;}
        private static void Input(object __instance)
        {if(!ReferenceEquals(__instance,attack))return;var kind=Get(Get(attack,"consumedInput"),"Kind").ToString();recorded=kind=="Enhanced"?proposed:null;proposed=null;if(kind=="Enhanced")enhanced++;else if(kind=="Basic")basic++;}
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");attack=Get(combat,"Attack");var input=Get(context,"Input");
            bool timing=Environment.GetEnvironmentVariable("JMR_DEDUP_TIMING")=="1";
            var audit=new Harmony("JueMingR.Tests.AimDedup");
            if(!timing)
            {
                audit.Patch(attack.GetType().GetMethod("SolveOnTerrain",Flags),prefix:new HarmonyMethod(typeof(NativeCombatDedupChecks),nameof(Solve)),finalizer:new HarmonyMethod(typeof(NativeCombatDedupChecks),nameof(Solved)));
                audit.Patch(typeof(AttackMotion).GetMethod("Advance",Flags),prefix:new HarmonyMethod(typeof(NativeCombatDedupChecks),nameof(MotionStep)));
                audit.Patch(typeof(Projectile).GetMethod("AI_019_Spears_GetSpearOffsetRelativeToPlayer",Flags),prefix:new HarmonyMethod(typeof(NativeCombatDedupChecks),nameof(SpearStep)));
                var share=attack.GetType().GetMethod("TryCandidateHand",Flags);if(share!=null)audit.Patch(share,postfix:new HarmonyMethod(typeof(NativeCombatDedupChecks),nameof(Share)));
            }
            audit.Patch(attack.GetType().GetMethod("RecordInput",Flags),postfix:new HarmonyMethod(typeof(NativeCombatDedupChecks),nameof(Input)));
            audit.Patch(attack.GetType().GetMethod("ContactInput",Flags),prefix:new HarmonyMethod(typeof(NativeCombatDedupChecks),nameof(Proposed)));
            audit.Patch(typeof(Player).GetMethod("TryUpdateChannel",Flags),postfix:new HarmonyMethod(typeof(NativeCombatDedupChecks),nameof(Born)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatDedupChecks),nameof(Hit)));
            foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            try
            {
                if(!timing && Environment.GetEnvironmentVariable("JMR_DEDUP_BASELINE")!="1"){Component(context,combat,host,input);Modes(context,combat,host,input);}
                foreach(int item in new[]{95,1228})foreach(bool moving in new[]{false,true})
                {
                    var p=Reset(context,combat,host,input,item);var n=Scene(2,new Vector2(item==95?965:870,667));
                    scenarioItem=item;scenarioMoving=moving;recorded=proposed=null;
                    if(moving)n.velocity=new Vector2(0,.7f);
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,false,50,true,true));
                    solves=births=enhanced=basic=shares=hits=motionSteps=spearSteps=solveDepth=0;var times=new List<double>(360);
                    for(int frame=0;frame<360;frame++)
                    {
                        // Fixed external transverse track: unlike natural closed
                        // combat dynamics this keeps paired solve inputs known.
                        nativeFrame=frame;var watch=Stopwatch.StartNew();NativeCombatSelectionPreviewChecks.Step(context,new Vector2(550,600),frame%90<45,true);watch.Stop();times.Add(watch.Elapsed.TotalMilliseconds);
                        if(timing)Console.WriteLine("DEDUP time item="+item+" moving="+moving+" frame="+frame+" ms="+watch.Elapsed.TotalMilliseconds.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
                        Require(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Slot==2,"single legal target remains the actual shared identity");
                    }
                    Console.WriteLine("DEDUP native item="+item+" fixedVelocityTrajectory="+moving+" frames=360 solves="+solves+" motionSteps="+motionSteps+" spearSteps="+spearSteps+" birth="+births+" Enhanced="+enhanced+" Basic="+basic+" hits="+hits+" share="+shares);
                    if(timing){var warm=times.Skip(30).OrderBy(v=>v).ToArray();Console.WriteLine("DEDUP time-summary item="+item+" moving="+moving+" n=330 mean="+warm.Average()+" p95="+warm[(int)(warm.Length*.95)]+" p99="+warm[(int)(warm.Length*.99)]+" max="+warm.Last());}
                    Require(births>0 && enhanced+basic==births,"actual ordinary native births and input consumers have matching denominators");
                    Require(enhanced>0,"single target still supplies an applicable enhanced input under normal budget");
                }
            }
            finally{audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());attack=null;}
        }
        private static Player Reset(object context,object combat,object host,object input,int item)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,item,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.ResetFallDamage();p.releaseUseItem=true;p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;Main.screenPosition=new Vector2(480,400);return p;
        }
        private static NPC Scene(int slot,Vector2 center)
        {var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.width=n.height=40;n.Center=center;n.velocity=Vector2.Zero;n.life=n.lifeMax=100000;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static void Sample(object context,object combat,object host,object input,Vector2 mouse)
        {NativeToolExecutionChecks.Sample(context,input,mouse,true);Call(combat,"Sample");Call(host,"SampleMouse");Call(Get(host,"npcs"),"BeginActions",Get(input,"Frame"));Call(Get(host,"Selection"),"Update",Get(host,"Options"),Get(host,"Session"),true,null);}
        private static void Modes(object context,object combat,object host,object input)
        {
            foreach(bool path in new[]{false,true})foreach(bool aim in new[]{false,true})
            {
                Reset(context,combat,host,input,95);var first=Scene(2,new Vector2(970,667));
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,path,true,false,false,50,true,aim));solves=0;
                for(int frame=0;frame<12;frame++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(550,600),false,true);
                Require(solves==0,"single target Path/idle Aim has no hidden ranking hand preview");
                Require((bool)Get(Get(host,"Selection"),"HasTarget")== (path || aim),"saved marker cannot independently wake selection");
                Scene(3,new Vector2(1100,667));NativeCombatSelectionPreviewChecks.Step(context,new Vector2(550,600),false,true);
                Main.npc[3].active=false;NativeCombatSelectionPreviewChecks.Step(context,new Vector2(550,600),false,true);
                Require(GetOptional(attack,"candidateHandPlan")==null && !(bool)Get(Get(host,"Selection"),"ClearLine"),"multi to one retires donor and unproved ranking fact");
                first.active=false;NativeCombatSelectionPreviewChecks.Step(context,new Vector2(550,600),false,true);
                Require(!(bool)Get(Get(host,"Selection"),"HasTarget") && GetOptional(attack,"ExpectedImpact")==null,"last candidate retirement revokes old input result");
                Console.WriteLine("DEDUP modes Path="+path+" Aim="+aim+" markerSaved=true 1/multi/1/0 lifecycle PASS");
            }
        }
        private static void Component(object context,object combat,object host,object input)
        {
            var p=Reset(context,combat,host,input,95);Scene(2,new Vector2(970,667));NativeCombatObservationChecks.Save(host,new ObservationOptions(false,false,true,false,false,50,true,true));
            // This oracle owns a frozen real starting state; it never uses the
            // larger allowance as production supply/performance evidence.
            NativeQuickItemChecks.BeginWorldStep();Sample(context,combat,host,input,new Vector2(550,600));
            foreach(var candidate in (Array)Get(attack,"candidatePlans"))if(candidate!=null)Call(candidate,"Clear");
            using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))
            {
                solves=0;Call(attack,"ResolveSelection",(long)Main.GameUpdateCount-1,true);
                Console.WriteLine("DEDUP single ranking solves="+solves);
                Require(solves==0 && !(bool)Get(Get(host,"Selection"),"ClearLine"),"one candidate has no ranking solve and no fabricated reachable fact");
            }
            Scene(3,new Vector2(1100,667));
            // Warm the actual solver and both Source owners without widening
            // selection's cumulative 3ms slice. This is an oracle setup only.
            for(int warm=0;warm<8;warm++)
            {
                NativeQuickItemChecks.BeginWorldStep();Sample(context,combat,host,input,new Vector2(550,600));
                using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))
                {Call(attack,"BeginActionDemand");Call(attack,"ResolveSelection",(long)Main.GameUpdateCount-1,true);Call(attack,"Demand",true);var id=(NpcIdentity)Get(Get(host,"Selection"),"Target");Call(Get(host,"Prediction"),"PrepareActionPrefix",id,(long)Main.GameUpdateCount-1,120,120);Call(attack,"PrepareAction");}
                Call(attack,"ClearExecution");
            }
            Main.npc[3].active=false;NativeQuickItemChecks.BeginWorldStep();Sample(context,combat,host,input,new Vector2(550,600));
            foreach(var candidate in (Array)Get(attack,"candidatePlans"))if(candidate!=null)Call(candidate,"Clear");
            using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))
            {solves=0;Call(attack,"ResolveSelection",(long)Main.GameUpdateCount-1,true);Console.WriteLine("DEDUP warm single ranking solves="+solves);if(Environment.GetEnvironmentVariable("JMR_DEDUP_RED_DUP_ONLY")!="1")Require(solves==0,"a warmed single target still performs no rank-only weapon solve");}
            Main.npc[3].active=true;
            NativeQuickItemChecks.BeginWorldStep();Sample(context,combat,host,input,new Vector2(550,600));
            foreach(var candidate in (Array)Get(attack,"candidatePlans"))if(candidate!=null)Call(candidate,"Clear");
            using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))
            {
                solves=shares=motionSteps=spearSteps=solveDepth=0;Call(attack,"BeginActionDemand");Call(attack,"ResolveSelection",(long)Main.GameUpdateCount-1,true);
                var selected=(NpcIdentity)Get(Get(host,"Selection"),"Target");
                Console.WriteLine("DEDUP component candidates="+Get(Get(host,"Selection"),"CandidateCount")+" rank="+solves+" depth="+((int[])Get(attack,"candidateDepths"))[selected.Slot*2+1]+" request="+((int[])Get(attack,"candidateRequests"))[selected.Slot*2+1]);
                Call(attack,"Demand",true);Call(Get(host,"Prediction"),"PrepareActionPrefix",selected,(long)Main.GameUpdateCount-1,(int)Get(attack,"ActionRequired"),NpcPredictionCache.Horizon);
                int ranked=solves,rankedSteps=motionSteps;Call(attack,"PrepareAction");
                Console.WriteLine("DEDUP two candidate ranking="+ranked+" total="+solves+" shares="+shares+" contact="+(GetOptional(attack,"ExpectedImpact")!=null));
                Require(ranked>0 && shares==1 && solves==ranked && GetOptional(attack,"ExpectedImpact")!=null,"completed same BeforeNpc zero-delay candidate hand is not solved twice");
                Require(rankedSteps>0 && motionSteps==rankedSteps,"actual simulated projectile advances, not just entry counts, are unchanged by donation");
                Console.WriteLine("DEDUP same hand motionSteps="+rankedSteps+" afterExecution="+motionSteps);
                Call(attack,"PrepareAction");Require(solves==ranked,"same phase repeated preparation reuses unconsumed result");
                var donor=((Array)Get(attack,"candidatePlans")).GetValue(selected.Slot*2+1);
                Action<Action,Action,string> reject=(change,restore,name)=>
                {
                    Call(attack,"ClearExecution");attack.GetType().GetField("candidateHandPlan",Flags).SetValue(attack,donor);change();shares=0;
                    try{Call(attack,"PrepareAction");Require(shares==0,"real dependency change rejects candidate hand: "+name);}
                    finally{restore();Call(attack,"ClearExecution");}
                };
                int stack=p.inventory[54].stack,animation=p.itemAnimation,time=p.itemTime,immune=Main.npc[selected.Slot].immune[0];Vector2 position=p.position;
                reject(()=>p.inventory[54].stack--,()=>p.inventory[54].stack=stack,"ammo count");
                reject(()=>p.itemAnimation++,()=>p.itemAnimation=animation,"native animation");
                reject(()=>p.itemTime=2,()=>p.itemTime=time,"native cooldown");
                reject(()=>p.position.X+=1,()=>p.position=position,"player starting pose");
                reject(()=>Main.npc[selected.Slot].immune[0]=120,()=>Main.npc[selected.Slot].immune[0]=immune,"native receiver immunity");
                var receiver=Main.npc[selected.Slot];var replacement=new NPC();replacement.SetDefaults(receiver.type);replacement.whoAmI=selected.Slot;replacement.active=true;replacement.position=receiver.position;
                reject(()=>Main.npc[selected.Slot]=replacement,()=>Main.npc[selected.Slot]=receiver,"target slot instance");
                bool wet=Main.tile[53,41].active();ushort tileType=Main.tile[53,41].type;
                reject(()=>{Main.tile[53,41].active(true);Main.tile[53,41].type=1;},()=>{Main.tile[53,41].active(wet);Main.tile[53,41].type=tileType;},"physical weapon passage cell");
                // Negative controls can legitimately replace Source's
                // publication/relation generation. Restoring live fields does
                // not restore that old proof. Acquire a fresh normal sample
                // before checking recovery; never rewrite a trajectory clock.
                NativeQuickItemChecks.BeginWorldStep();Sample(context,combat,host,input,new Vector2(550,600));
                using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))
                {
                    Call(attack,"BeginActionDemand");Call(attack,"ResolveSelection",(long)Main.GameUpdateCount-1,true);Call(attack,"Demand",true);
                    Call(Get(host,"Prediction"),"PrepareActionPrefix",selected,(long)Main.GameUpdateCount-1,(int)Get(attack,"ActionRequired"),NpcPredictionCache.Horizon);
                    shares=0;Call(attack,"PrepareAction");Require(shares==1 && !(bool)Get(attack,"Failed"),"fresh restored premises can still donate without a fault latch");
                    donor=((Array)Get(attack,"candidatePlans")).GetValue(selected.Slot*2+1);
                }
                var phase=Enum.Parse(Get(attack,"preparedOrdinaryPhase").GetType(),"BeforeNpc");var changedReceive=new object[]{p,p.HeldItem,phase,null};
                receiver.immune[0]=120;Require((bool)attack.GetType().GetMethod("TryOrdinaryInput",Flags).Invoke(attack,changedReceive) && Get(changedReceive[3],"Kind").ToString()=="Basic","changed receiver immunity after donation cannot consume the old Enhanced result");receiver.immune[0]=immune;
                p.position.X+=1;changedReceive=new object[]{p,p.HeldItem,phase,null};Require((bool)attack.GetType().GetMethod("TryOrdinaryInput",Flags).Invoke(attack,changedReceive) && Get(changedReceive[3],"Kind").ToString()=="Basic","even a one-pixel player translation after donation revokes the old hand start");p.position=position;
                // Source/reset/another candidate cannot erase the execution's
                // independent weapon and motion read leases.
                var plans=(Array)Get(attack,"candidatePlans");Call(plans.GetValue(selected.Slot*2+1),"Clear");
                Require(GetOptional(attack,"ExpectedImpact")!=null,"clearing mutable candidate does not invalidate or rewrite execution result");
                p.inventory[54].stack--;object supplied;var args=new object[]{p,p.HeldItem,Enum.Parse(Get(attack,"preparedOrdinaryPhase").GetType(),"BeforeNpc"),null};
                supplied=attack.GetType().GetMethod("TryOrdinaryInput",Flags).Invoke(attack,args);Require((bool)supplied && Get(args[3],"Kind").ToString()=="Basic","changed actual ammo rejects enhanced donor rather than consuming old contact");
                p.inventory[54].stack++;var scope=Call(attack,"BeginShot",p,p.HeldItem,true);
                Require(scope!=null && GetOptional(attack,"current")==null && GetOptional(attack,"candidateHandPlan")==null,"ordinary input acquisition retires execution and synchronous donor");Call(scope,"End");
                args=new object[]{p,p.HeldItem,Enum.Parse(Get(attack,"preparedOrdinaryPhase").GetType(),"BeforeNpc"),null};
                Require((bool)attack.GetType().GetMethod("TryOrdinaryInput",Flags).Invoke(attack,args) && Get(args[3],"Kind").ToString()=="Basic","same consumed Contact cannot grant a second Enhanced input; this component does not claim a native birth");
            }
            Call(attack,"EndPreparation");
        }
    }
}
