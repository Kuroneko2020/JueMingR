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
            // One natural phase has a finite endpoint even if every model is
            // unknown. A later phase has different origins and damage timing.
            int known=-1;
            for(int i=0;i<selection.CandidateCount;i++)if(CombatSelection.Identity(selection.Candidate(i),observation.Session).Equals(candidates.Reachable)){known=i;break;}
            if(!candidates.Begin(sampleTick,phase))
            {if(known>=0)selection.Choose(selection.Candidate(known),observation.Session,true);return;}
            int best=-1,tried=-1;
            for(int attempt=0;attempt<2;attempt++)
            {
                int index;
                if(attempt==0 && known>=0)index=known;
                else
                {
                    index=candidates.Next(selection.CandidateCount,tried);if(index<0)break;
                }
                tried=index;var npc=selection.Candidate(index);var identity=CombatSelection.Identity(npc,observation.Session);
                // Distinct native phases keep distinct exact premises. This
                // avoids invalidating a reusable completed-world answer merely
                // because a before-NPC action was prepared between two frames.
                int key=npc.whoAmI*2+(beforeNpc?1:0);
                var plan=candidatePlans[key];if(plan==null)candidatePlans[key]=plan=new PreviewPlan();
                var context=FaultContextFor(identity);if(plan.Faults.Blocks(context))continue;
                try
                {
                    var timeline=observation.Prediction.TryCandidate(identity,sampleTick,beforeNpc,NpcPredictionCache.Horizon);
                    if(timeline==null || timeline.Count<2)continue;
                    var contact=PreparePlan(plan,timeline,phase);
                    if(contact!=null && (best<0 || index<best))best=index;
                }
                catch(Exception error){PropagateFatal(error);plan.Faults.Record(context);plan.Clear();}
            }
            if(best>=0)
            {var npc=selection.Candidate(best);candidates.Reachable=CombatSelection.Identity(npc,observation.Session);selection.Choose(npc,observation.Session,true);}
            else candidates.Reachable=default(NpcIdentity);
            // No proof leaves the deterministic nearest receiver and normal
            // manual attack intact. The traversal cursor still advances, so
            // an unresolved A cannot permanently deny a solvable B a turn.
        }
        private void ClearCandidatePlans()
        {for(int i=0;i<candidatePlans.Length;i++)candidatePlans[i]?.Clear();candidates.Clear();}
    }
}
