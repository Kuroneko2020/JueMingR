using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Finite diagnosis only. The normal stress/timing entry never installs
    // these reflection observers; records flush after the complete window.
    internal static class NativeCombatCandidateFailureChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private static object attack,host;private static NpcIdentity evaluating;private static bool inCandidate,positive;
        private static uint passTick;private static int repeatedAfterPositive;
        private static readonly HashSet<int> admissions=new HashSet<int>();
        private static readonly List<string> events=new List<string>();
        private static void Enter(object __instance,NpcIdentity __0,long __1,bool __2,int __5)
        {
            if(!ReferenceEquals(__instance,attack) || !__2)return;
            if(passTick!=Main.GameUpdateCount){passTick=Main.GameUpdateCount;positive=false;admissions.Clear();}
            if(!admissions.Add(__0.Slot) && positive)repeatedAfterPositive++;
            evaluating=__0;inCandidate=true;
            events.Add(string.Join(",",Main.GameUpdateCount,"admit",__0.Slot,__5,Get(Get(host,"PredictionWork"),"UsedMilliseconds"),__1));
        }
        private static void Returned(object __instance,bool __result)
        {
            if(!ReferenceEquals(__instance,attack) || !inCandidate)return;
            var plan=((Array)Get(attack,"candidatePlans")).GetValue(evaluating.Slot*2+1);
            var path=plan==null?null:(NpcTrajectory)GetOptional(plan,"Timeline");
            var contact=plan==null?null:(AttackContact)GetOptional(plan,"Contact");
            events.Add(string.Join(",",Main.GameUpdateCount,"return",evaluating.Slot,__result,Get(Get(host,"PredictionWork"),"UsedMilliseconds"),path?.Count??0,path?.BudgetPaused??false,path?.Stop.ToString()??"none",plan!=null && (bool)Get(plan,"Prepared"),contact?.ReceiveTick??-1));positive|=__result;inCandidate=false;
        }
        private static void Published(NpcTrajectory __result)
        {if(inCandidate)events.Add(string.Join(",",Main.GameUpdateCount,"source",evaluating.Slot,__result?.SampleTick??-1,__result?.CaptureTick??-1,__result?.RelationVersion??-1,__result?.Count??0,__result?.BudgetPaused??false,__result?.Stop.ToString()??"none"));}
        private static Exception PlanExit(Exception __exception)
        {
            if(inCandidate && __exception!=null)
            {
                var owner=typeof(CombatPredictionWork).GetField("active",Flags).GetValue(null);
                events.Add(string.Join(",",Main.GameUpdateCount,"plan-exit",evaluating.Slot,__exception.GetType().Name,
                    owner==null?0:Get(owner,"UsedMilliseconds"),owner!=null && (bool)Get(owner,"Exceeded"),
                    owner==null?0:Get(owner,"softAllowance"),owner==null?0:Get(owner,"allowance"),owner==null?0:Get(owner,"optionalDepth")));
            }
            return __exception;
        }
        private static void Input(object __instance)
        {
            if(!ReferenceEquals(__instance,attack))return;
            var input=Get(attack,"consumedInput");var id=(NpcIdentity)Get(input,"Target");
            events.Add(string.Join(",",Main.GameUpdateCount,"input",id.Slot,Get(input,"Kind"),Get(Get(host,"PredictionWork"),"UsedMilliseconds"),Get(attack,"ActionRequired")));
        }
        internal static void Run(object context)
        {
            host=Get(context,"CombatObservation");attack=Get(Get(context,"Combat"),"Attack");events.Clear();passTick=uint.MaxValue;repeatedAfterPositive=0;
            var audit=new Harmony("JueMingR.Tests.CandidateFirstFailure");
            try
            {
                audit.Patch(attack.GetType().GetMethod("EvaluateCandidate",Flags),prefix:new HarmonyMethod(typeof(NativeCombatCandidateFailureChecks),nameof(Enter)),postfix:new HarmonyMethod(typeof(NativeCombatCandidateFailureChecks),nameof(Returned)));
                audit.Patch(attack.GetType().GetMethod("PreparePlan",Flags),finalizer:new HarmonyMethod(typeof(NativeCombatCandidateFailureChecks),nameof(PlanExit)));
                audit.Patch(attack.GetType().GetMethod("RecordInput",Flags),postfix:new HarmonyMethod(typeof(NativeCombatCandidateFailureChecks),nameof(Input)));
                audit.Patch(Get(host,"Prediction").GetType().GetMethod("TryCandidateReuse",Flags),postfix:new HarmonyMethod(typeof(NativeCombatCandidateFailureChecks),nameof(Published)));
                NativeCombatCandidateDemandChecks.RunStress(context);
                Console.WriteLine("CANDIDATE-FAILURE repeatedNearerAfterPositive="+repeatedAfterPositive);
                Require(repeatedAfterPositive==0,"A fresh positive and checked nearer prefix hand over to the actual consumer before repeating a longer optional near probe.");
            }
            finally
            {
                foreach(var method in audit.GetPatchedMethods().ToArray())audit.Unpatch(method,HarmonyPatchType.All,audit.Id);
                var root=Path.Combine(Terraria.Program.SavePath,"candidate-failure");Directory.CreateDirectory(root);File.WriteAllLines(Path.Combine(root,"events.csv"),events);Console.WriteLine("CANDIDATE-FAILURE raw="+root);attack=host=null;inCandidate=false;
            }
        }
    }
}
