using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCombatAttackCostChecks
    {
        private struct CandidateStateSample
        {internal long Tick;internal NpcMotionState Next;internal PredictionPlayerMotion Player;internal bool PlayerCaptured;}
        private static readonly Dictionary<int,CandidateStateSample> candidateStateSamples=new Dictionary<int,CandidateStateSample>();
        private static readonly HashSet<string> candidateStateDifferences=new HashSet<string>();
        private static int candidateStateKey=-1;private static long candidateStateTick;
        private static void CandidateStateAudit(Harmony audit)
        {
            candidateStateSamples.Clear();candidateStateDifferences.Clear();candidateStateKey=-1;
            audit.Patch(typeof(RollingNpcPrediction).GetMethod("Prepare",Flags),prefix:new HarmonyMethod(typeof(NativeCombatAttackCostChecks),nameof(CandidatePrepareBefore)),postfix:new HarmonyMethod(typeof(NativeCombatAttackCostChecks),nameof(CandidatePrepareAfter)));
            audit.Patch(typeof(NpcMotion).GetMethods(Flags).Single(m=>m.Name=="Step" && m.GetParameters().Length==8),postfix:new HarmonyMethod(typeof(NativeCombatAttackCostChecks),nameof(CandidateStepAfter)));
            audit.Patch(typeof(RollingNpcPrediction).GetMethod("AdvancePlayer",Flags),postfix:new HarmonyMethod(typeof(NativeCombatAttackCostChecks),nameof(CandidatePlayerAfter)));
        }
        private static PredictionPlayerMotion candidateNextPlayer;private static bool candidatePlayerCaptured;
        private static void CandidatePrepareBefore(NpcMotionState[] source,int count,int selected,long tick,PredictionPlayerMotion player,bool firstPlayerStepCompleted)
        {
            candidateStateKey=-1;if(count!=1 || selected!=0 || source[0].EffectiveType!=2 && source[0].EffectiveType!=49)return;
            int key=source[0].Identity.Slot*2+(firstPlayerStepCompleted?1:0);CandidateStateSample prior;
            if(candidateStateSamples.TryGetValue(key,out prior) && prior.Tick+1==tick && prior.Next.Identity.Equals(source[0].Identity))
            {
                foreach(var field in typeof(NpcMotionState).GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance))
                {
                    object expected=field.GetValue(prior.Next),actual=field.GetValue(source[0]);
                    if(!Equals(expected,actual) && candidateStateDifferences.Add(field.Name))Console.WriteLine("CANDIDATE STATE firstDifference="+field.Name+" expected="+expected+" actual="+actual+" slot="+source[0].Identity.Slot+" beforeNpc="+firstPlayerStepCompleted+" tick="+tick);
                }
                if(prior.PlayerCaptured)foreach(var field in typeof(PredictionPlayerMotion).GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance))
                {object expected=field.GetValue(prior.Player),actual=field.GetValue(player);if(!Equals(expected,actual) && candidateStateDifferences.Add("Player."+field.Name))Console.WriteLine("CANDIDATE STATE firstDifference=Player."+field.Name+" expected="+expected+" actual="+actual+" tick="+tick);}
            }
            candidateStateKey=key;candidateStateTick=tick;candidatePlayerCaptured=false;
        }
        private static void CandidateStepAfter(NpcMotionState n,int elapsed,bool __result)
        {if(candidateStateKey>=0 && elapsed==1 && __result)candidateStateSamples[candidateStateKey]=new CandidateStateSample{Tick=candidateStateTick,Next=n};}
        private static void CandidatePlayerAfter(PredictionPlayerMotion p,bool __result)
        {if(candidateStateKey>=0 && !candidatePlayerCaptured && __result){candidateNextPlayer=p;candidatePlayerCaptured=true;}}
        private static void CandidatePrepareAfter()
        {if(candidateStateKey>=0 && candidatePlayerCaptured && candidateStateSamples.ContainsKey(candidateStateKey)){var state=candidateStateSamples[candidateStateKey];state.Player=candidateNextPlayer;state.PlayerCaptured=true;candidateStateSamples[candidateStateKey]=state;}candidateStateKey=-1;}
    }
}
