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
            var selection=observation.Selection;var phase=beforeNpc?HostAttackPhase.BeforeNpc:HostAttackPhase.CompletedWorld;
            if(!observation.Options.ClearLine || !PreviewDemand || selection.CandidateCount==0 || HostPlacementAttack.Handles(combat.Player.HeldItem))
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
                var npc=selection.Candidate(index);var identity=CombatSelection.Identity(npc,observation.Session);
                // Distinct native phases keep distinct exact premises. This
                // avoids invalidating a reusable completed-world answer merely
                // because a before-NPC action was prepared between two frames.
                int key=npc.whoAmI*2+(beforeNpc?1:0);
                var plan=candidatePlans[key];if(plan==null)candidatePlans[key]=plan=new PreviewPlan();
                var context=FaultContextFor(identity);if(plan.Faults.Blocks(context))continue;
                try
                {
                    var timeline=observation.Prediction.TryCandidateReuse(identity,sampleTick,beforeNpc,NpcPredictionCache.Horizon,CandidateMissCurrent(plan,identity,sampleTick,phase));
                    if(timeline==null || timeline.Count<2)continue;
                    var contact=PreparePlan(plan,timeline,phase);
                    if(contact!=null)
                    {candidates.Reachable=identity;selection.Choose(npc,observation.Session,true);return;}
                }
                catch(Exception error){PropagateFatal(error);plan.Faults.Record(context);plan.Clear();}
            }
            // No proof leaves the deterministic nearest receiver and normal
            // manual attack intact. No retained success or round-robin cursor
            // can silently replace the user's distance order.
        }
        private void ClearCandidatePlans()
        {for(int i=0;i<candidatePlans.Length;i++)candidatePlans[i]?.Clear();candidates.Clear();}
    }
}
