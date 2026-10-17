using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Component state-handoff oracle. Real Source/Plan/ranking execute; only
    // an existing local observation-failure input or required tile is removed.
    // Never supplies a trajectory, angle, Contact or Source return value.
    internal static class NativeCombatBalancedCandidateChecks
    {
        const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        static int watchedSlot,sourceCalls,sourceCount,publishedCount,readFaults;
        static void Source(NpcIdentity __0,NpcTrajectory __result)
        {if(__0.Slot==watchedSlot){sourceCalls++;sourceCount=__result==null?-1:__result.Count;Console.WriteLine("CANDIDATE SOURCE call="+sourceCalls+" slot="+__0.Slot+" count="+sourceCount+" stop="+(__result==null?"null":__result.Stop.ToString()));}}
        static void Published(NpcTrajectory __0)
        {if(__0!=null && __0.Identity.Slot==watchedSlot)publishedCount=__0.Count;}
        static Exception ReadDone(Exception __exception)
        {if(__exception!=null && __exception.GetType().Name=="NpcObservationFailure")readFaults++;return __exception;}
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");var source=Get(host,"Prediction");
            var audit=new Harmony("JueMingR.Tests.BalancedCandidateCurrentAttempt");var failures=new List<string>();
            audit.Patch(source.GetType().GetMethod("TryCandidateReuse",F),postfix:new HarmonyMethod(typeof(NativeCombatBalancedCandidateChecks),nameof(Source)));
            audit.Patch(typeof(NpcPredictionCache).GetMethod("Publish",F),postfix:new HarmonyMethod(typeof(NativeCombatBalancedCandidateChecks),nameof(Published)));
            audit.Patch(source.GetType().GetMethod("Read",F),finalizer:new HarmonyMethod(typeof(NativeCombatBalancedCandidateChecks),nameof(ReadDone)));
            try
            {
                foreach(string kind in new[]{"balanced-null","legacy-null","terrain-count1"})
                {
                    try{Case(context,host,combat,attack,kind,failures);}
                    catch(Exception error){failures.Add(kind+": fixture/entry "+error.GetBaseException().Message);Console.WriteLine("CANDIDATE RECOVERY EXCEPTION "+kind+" "+error);}
                }
                Require(failures.Count==0,string.Join(" | ",failures));
                Console.WriteLine("PASS candidate current-attempt Source null/terminal short publication, current rank and Unknown retry recovery");
            }
            finally{audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());watchedSlot=-1;}
        }
        static void Expect(bool value,string message,List<string> failures)
        {if(!value)failures.Add(message);}
        static bool Evaluate(object attack,NpcIdentity identity,object phase,int required)
        {return (bool)Call(attack,"EvaluateCandidate",identity,(long)Main.GameUpdateCount,false,phase,identity.Slot*2,required);}
        static void Case(object context,object host,object combat,object attack,string kind,List<string> failures)
        {
            bool legacy=kind=="legacy-null",terrain=kind=="terrain-count1";
            NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),legacy?3278:98,0,0);
            foreach(var item in p.armor)item.TurnToAir();p.ResetEffects();p.position=new Vector2(700,646);p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;p.releaseUseItem=true;
            foreach(var npc in Main.npc)npc.active=false;
            var near=NativeCombatControlPreviewChecks.Dummy(2,legacy?50:58,40);
            var far=NativeCombatControlPreviewChecks.Dummy(3,70,30);
            if(terrain)
            {
                near.SetDefaults(63);near.whoAmI=2;near.active=true;near.position=new Vector2(850,640);near.velocity=Vector2.UnitX;near.target=p.whoAmI;near.wet=true;near.wetCount=1;near.timeLeft=750;
                for(int x=49;x<=57;x++)for(int y=35;y<=45;y++)Main.tile[x,y].liquid=255;
            }
            Main.screenPosition=new Vector2(480,400);
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,50,false,false));
            NativeToolExecutionChecks.Sample(context,Get(context,"Input"),p.Center+new Vector2(-200,-50),false);Call(combat,"Sample");Call(host,"SampleMouse");
            for(int i=0;i<6;i++)NativeCombatObservationChecks.Fresh(context,host);
            var selection=Get(host,"Selection");var identity=(NpcIdentity)attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true).GetMethod("Identity",F).Invoke(null,new object[]{near,(long)Get(host,"Session")});
            var farIdentity=(NpcIdentity)attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true).GetMethod("Identity",F).Invoke(null,new object[]{far,(long)Get(host,"Session")});
            var phase=Enum.Parse(Get(Get(attack,"preview"),"Phase").GetType(),"CompletedWorld");
            int key=identity.Slot*2;var plans=(Array)Get(attack,"candidatePlans");var unknown=(bool[])Get(attack,"candidateUnknown");var changes=(long[])Get(attack,"candidateChanges");var significant=(bool[])Get(attack,"candidateSignificant");
            using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))Require(Evaluate(attack,identity,phase,60),"real candidate must have a completed positive before fault "+kind);
            var plan=plans.GetValue(key);bool wasBalanced=(bool)Get(plan,"Balanced");var faults=Get(plan,"Faults");
            Require(wasBalanced!=legacy && (bool)Call(attack,"PlanPositive",plan),"seed uses its actual balanced/legacy result kind "+kind);
            if(!legacy)using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))Require(Evaluate(attack,farIdentity,phase,60),"far current candidate has an independently produced reachable plan");
            var oldTimes=near.buffTime;int cx=(int)near.Center.X/16,cy=(int)near.Bottom.Y/16+1;var oldTile=Main.tile[cx,cy];
            watchedSlot=near.whoAmI;sourceCalls=readFaults=0;sourceCount=publishedCount=-1;
            long ticket=(long)Get(attack,"candidateChangeTicket")+1;Set(attack,"candidateChangeTicket",ticket);changes[near.whoAmI]=ticket;significant[near.whoAmI]=true;
            try
            {
                if(terrain)Main.tile[cx,cy]=null;else near.buffTime=null;
                bool positive;using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))positive=Evaluate(attack,identity,phase,60);
                Console.WriteLine("CANDIDATE RECOVERY kind="+kind+" priorBalanced="+wasBalanced+" sourceCalls="+sourceCalls+" sourceCount="+sourceCount+" rawPublished="+publishedCount+" readFaults="+readFaults+" positive="+positive+" planPositive="+Call(attack,"PlanPositive",plan)+" unknown="+unknown[key]+" ticket="+ticket+"->"+changes[near.whoAmI]);
                Require(sourceCalls>0 && sourceCount<2 && (terrain?publishedCount==1:readFaults>0),"actual Source null or terminal one-point publication must witness the failure input "+kind);
                Expect(!positive && !(bool)Call(attack,"PlanPositive",plan),kind+": unavailable current Source must retire prior positive",failures);
                Expect(unknown[key] && changes[near.whoAmI]>ticket && significant[near.whoAmI],kind+": Unknown owes a changed-fact retry at queue tail",failures);
                Expect(ReferenceEquals(faults,Get(plan,"Faults")),kind+": retiring a plan preserves bounded fault owner",failures);
                if(!legacy)
                {
                    // Admit one optional ranking pass explicitly. This tests
                    // the scheduler consumer, not natural Main timing/cost.
                    // Its caller first rebuilds the current distance list and
                    // clears prior selection flags. The far receiver must be
                    // reachable with the missing input still in effect.
                    using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))Require(Evaluate(attack,farIdentity,phase,60),"far remains CURRENTLY reachable while near input is absent "+kind);
                    Call(selection,"Update",Get(host,"Options"),(long)Get(host,"Session"),true,Get(host,"Geometry"));
                    Set(host,"OptionalPreparation",true);
                    using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))Call(attack,"ResolveSelection",(long)Main.GameUpdateCount,false);
                    var chosen=(NpcIdentity)Get(selection,"Target");bool reachable=(bool)Get(selection,"ClearLine");
                    Console.WriteLine("CANDIDATE RECOVERY rank kind="+kind+" selected="+chosen.Slot+" clear="+reachable+" expectedFar="+farIdentity.Slot+" sourceCalls="+sourceCalls+" lastSourceCount="+sourceCount);
                    Expect(chosen.Equals(farIdentity) && reachable,kind+": stale nearer result must not replace current far reachable",failures);
                }
            }
            finally{near.buffTime=oldTimes;Main.tile[cx,cy]=oldTile;}
            NativeQuickItemChecks.BeginWorldStep();Call(Get(context,"nativeNpcs"),"BeginTick");
            bool recovered;using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))recovered=Evaluate(attack,identity,phase,60);
            Console.WriteLine("CANDIDATE RECOVERY restored kind="+kind+" positive="+recovered+" unknown="+unknown[key]+" ticket="+changes[near.whoAmI]);
            Expect(recovered && !unknown[key] && changes[near.whoAmI]==0,kind+": restored actual Source retries and completes",failures);
        }
    }
}
