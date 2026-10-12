using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackAim
    {
        private readonly PreviewPlan[] candidatePlans=new PreviewPlan[NpcPredictionCache.Capacity*2];
        internal void ResolveSelection(long sampleTick,bool beforeNpc)
        {
            BeginFlightPreparation(beforeNpc);
            var selection=observation.Selection;var phase=beforeNpc?HostAttackPhase.BeforeNpc:HostAttackPhase.CompletedWorld;
            if(!observation.Options.ClearLine || !observation.PredictionAvailable || !PreviewDemand || selection.CandidateCount==0 || HostPlacementAttack.Handles(combat.Player.HeldItem))
            {candidates.Clear();return;}
            // Selection already sorts by the chosen centre's receive-box
            // distance. Prove that prefix in order, stopping at its first
            // feasible attack. An old farther success has no priority over a
            // nearer candidate whose wall, ammunition or motion has changed.
            // Unknown models do not veto later candidates; exact per-candidate
            // preparation premises retain both solved and unsolved results.
            candidates.Clear();
            for(int index=0;index<selection.CandidateCount;index++)
            {
                CombatPredictionWork.Check();
                var npc=selection.Candidate(index);var identity=CombatSelection.Identity(npc,observation.Session);
                if(!observation.PredictionAvailableFor(identity))continue;
                // Distinct native phases keep distinct exact premises. This
                // avoids invalidating a reusable completed-world answer merely
                // because a before-NPC action was prepared between two frames.
                int key=npc.whoAmI*2+(beforeNpc?1:0);
                var plan=candidatePlans[key];if(plan==null)candidatePlans[key]=plan=new PreviewPlan();
                var context=FaultContextFor(identity);if(plan.Faults.Blocks(context))continue;
                try
                {
                    int required=beforeNpc?ActionPreferred(identity):NpcPredictionCache.Horizon;
                    AttackContact contact=null;
                    for(int attempt=0;attempt<2;attempt++)
                    {
                        // Only Attack owns consumer1. An incumbent may expand
                        // its own same-phase request; candidate histories stay
                        // independent and Source never changes another demand.
                        if(beforeNpc && observation.Prediction.OwnsTarget(identity))SetActionDemand(required);
                        var timeline=observation.Prediction.TryCandidateReuse(identity,sampleTick,beforeNpc,required,CandidateMissCurrent(plan,identity,sampleTick,phase));
                        if(timeline!=null && timeline.Count>=2)contact=PreparePlan(plan,timeline,phase);
                        bool shortAction=beforeNpc && required<NpcPredictionCache.Horizon;
                        if(contact!=null && (!shortAction || !plan.Controlled && plan.Flight==null && plan.Delay==0))break;
                        contact=null;
                        if(!shortAction || timeline!=null && (timeline.Stop!=PredictionStop.None || timeline.Count>=NpcPredictionCache.Horizon+1))break;
                        // A short miss cannot skip a nearer late receive.
                        // Expand this candidate fully NOW before moving on.
                        required=NpcPredictionCache.Horizon;
                    }
                    if(contact!=null)
                    {if(beforeNpc)SetActionDemand(required);candidates.Reachable=identity;selection.Choose(npc,observation.Session,true);return;}
                }
                catch(CombatPredictionBudgetExceededException){plan.Clear();throw;}
                catch(Exception error){PropagateFatal(error);plan.Faults.Record(context);plan.Clear();}
            }
            // No enhanced proof leaves the deterministic normal receiver
            // available to admitted Basic input; preview never invents a
            // contact. No retained success or round-robin cursor can silently
            // replace the user's distance order.
        }
        private void ClearCandidatePlans()
        {for(int i=0;i<candidatePlans.Length;i++)candidatePlans[i]?.Clear();candidates.Clear();}
    }
}
