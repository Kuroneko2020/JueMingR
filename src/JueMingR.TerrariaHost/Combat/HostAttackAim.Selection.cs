using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackAim
    {
        private readonly PreviewPlan[] candidatePlans=new PreviewPlan[NpcPredictionCache.Capacity*2];
        private readonly NpcIdentity[] candidateIdentities=new NpcIdentity[NpcPredictionCache.Capacity*2];
        private readonly int[] candidateDepths=new int[NpcPredictionCache.Capacity*2];
        private readonly int[] candidateRequests=new int[NpcPredictionCache.Capacity*2];
        private readonly bool[] candidateUnknown=new bool[NpcPredictionCache.Capacity*2];
        private readonly long[] candidateAttempt=new long[NpcPredictionCache.Capacity*2];
        private readonly long[] candidateMembership=new long[NpcPredictionCache.Capacity*2];
        // Scratch for one synchronous World pass, indexed by current distance
        // rank. It prevents fair exploration and nearer-positive validation
        // from paying twice for the same candidate; it grants no cached permit.
        private readonly bool[] candidateWorldVisited=new bool[NpcPredictionCache.Capacity];
        private readonly CandidateFact[] candidateFacts=new CandidateFact[NpcPredictionCache.Capacity];
        private readonly long[] candidateChanges=new long[NpcPredictionCache.Capacity];
        private readonly bool[] candidateSignificant=new bool[NpcPredictionCache.Capacity];
        private long candidateChangeTicket;
        private long candidateActionPass,candidateWorldPass;
        private NpcIdentity presentationSelection,presentationNearest;
        private long presentationSelectionTick=-1;
        private long candidateWarmTick=-1;
        private bool presentationReachable;
        private ObservationOptions presentationOptions;
        private NpcIdentity actionHint;
        private int actionHintRequired;
        private Item selectionWeapon,candidateWeapon;
        private int candidateSlot,candidateType,candidatePrefix;
        private ObservationOptions candidateOptions;
        // Borrowed only between this synchronous selection pass and preview
        // publication. A repeated real entry clears it before checking facts.
        private PreviewPlan candidatePresentationPlan;
        // Borrowed only within ResolveSelection -> PrepareCore. It is never
        // an input capability and EndPreparation retires it. Execution copies
        // immutable contact/dependency leases before the plan can change.
        private PreviewPlan candidateHandPlan;
        private NpcIdentity singleCandidate;
        // This lease prevents optional display from flipping back to nearest
        // unknown between refreshes. BeforeNpc always evaluates real candidates;
        // no contact or input capability is inherited by this lease.
        internal void RestorePresentationSelection(long tick)
        {
            var selection=observation.Selection;
            if(!presentationReachable || !observation.Options.ClearLine || !ReferenceEquals(selectionWeapon,combat.Player.HeldItem) || !ReferenceEquals(presentationOptions,observation.Options) || tick-presentationSelectionTick>HostCombatObservation.DisplayMaximumAge || !selection.HasTarget || !selection.Target.Equals(presentationNearest))return;
            for(int i=0;i<selection.CandidateCount;i++)
            {var npc=selection.Candidate(i);if(CombatSelection.Identity(npc,observation.Session).Equals(presentationSelection)){selection.Choose(npc,observation.Session,true);return;}}
        }
        internal void ResolveSelection(long sampleTick,bool beforeNpc)
        {
            candidatePresentationPlan=null;
            candidateHandPlan=null;
            if(!CombatPredictionWork.TryContinue())return;
            BeginFlightPreparation(beforeNpc);
            var selection=observation.Selection;var phase=beforeNpc?HostAttackPhase.BeforeNpc:HostAttackPhase.CompletedWorld;
            if(!observation.Options.ClearLine || !observation.PredictionAvailable || !AttackObservationDemand || selection.CandidateCount==0 || HostPlacementAttack.Handles(combat.Player.HeldItem))
            {if(selection.CandidateCount==0)ClearCandidateProgress();candidates.Clear();return;}
            // A held clear-line intent can prepare a future opening during
            // ordinary cooldown even when Path is OFF. This is a small
            // candidate-only cadence, not selected demand, preview or input.
            bool warm=!beforeNpc && !UrgentPreparation && !observation.OptionalPreparation && BasicPermission &&
                (candidateWarmTick<0 || (long)Main.GameUpdateCount-candidateWarmTick>=3);
            var player=combat.Player;var item=player.HeldItem;
            if(!ReferenceEquals(candidateWeapon,item) || candidateSlot!=player.selectedItem || candidateType!=item.type || candidatePrefix!=item.prefix || !ReferenceEquals(candidateOptions,observation.Options))
            {
                ClearCandidateProgress();candidateWeapon=item;candidateSlot=player.selectedItem;candidateType=item.type;candidatePrefix=item.prefix;candidateOptions=observation.Options;
            }
            if(warm)candidateWarmTick=(long)Main.GameUpdateCount;
            if(selection.CandidateCount==1)
            {
                var identity=CombatSelection.Identity(selection.Candidate(0),observation.Session);
                if(!singleCandidate.Equals(identity))
                {ClearCandidateProgress();singleCandidate=identity;candidateWeapon=item;candidateSlot=player.selectedItem;candidateType=item.type;candidatePrefix=item.prefix;candidateOptions=observation.Options;}
                // There is no rank to solve. Keep reachability unproved and
                // retire old multi-candidate hints/display on the edge only.
                candidates.Clear();presentationReachable=false;candidatePresentationPlan=null;
                if(beforeNpc)
                {
                    // Transfer the last warm range once, at a real hand/control
                    // admission. Cooldown callbacks must neither consume that
                    // first useful hint nor turn it into selected urgent work.
                    // The selected owner solves current premises; this range
                    // is scheduling information, never the old warm Contact.
                    if(UrgentPreparation && actionHint.Equals(identity) && actionHintRequired>0)
                    {
                        int required=Math.Max(ActionPreferred(identity),actionHintRequired);
                        actionHint=default(NpcIdentity);actionHintRequired=0;
                        SetActionDemand(required);
                    }
                    return;
                }
                // The old cooldown pass was useful work for the next actual
                // hand. Retain that one owner's range progress without making
                // an idle Path-only hand hypothesis or a ranking calculation.
                if(!UrgentPreparation && BasicPermission && (warm || observation.OptionalPreparation))
                {
                    if(warm)candidateWarmTick=(long)Main.GameUpdateCount;int key=identity.Slot*2;
                    candidateIdentities[key]=identity;
                    // Removing rank work does not remove the old range's
                    // discontinuity boundary. A new AI/receive phase or a
                    // body-sized departure replaces the old unknown range;
                    // ordinary motion keeps progressive search unchanged.
                    var fact=new CandidateFact(selection.Candidate(0),observation.Options.Dummy,sampleTick);
                    var prior=candidateFacts[identity.Slot];
                    if(prior.Present && !prior.Same(fact))
                    {
                        candidateChanges[identity.Slot]=++candidateChangeTicket;
                        if(prior.Significant(fact)){candidateSignificant[identity.Slot]=true;candidateRequests[key]=0;}
                    }
                    candidateFacts[identity.Slot]=fact;
                    // A fresh admitted warm attempt replaces the prior hint,
                    // including miss/pause. Retaining its old positive range
                    // after this attempt would repeatedly prioritize stale work.
                    actionHint=default(NpcIdentity);actionHintRequired=0;
                    using(CombatPredictionWork.Limit(3))
                    if(EvaluateCandidate(identity,sampleTick,false,phase,key,CandidateProgressRequired(identity,key,false)))
                    {actionHint=identity;actionHintRequired=Math.Min(NpcPredictionCache.Horizon,Math.Max(1,candidatePlans[key].Contact.ReceiveTick+1));}
                }
                return;
            }
            if(singleCandidate.Token!=null){ClearCandidateProgress();candidateWeapon=item;candidateSlot=player.selectedItem;candidateType=item.type;candidatePrefix=item.prefix;candidateOptions=observation.Options;}
            if(!beforeNpc && !UrgentPreparation && !observation.OptionalPreparation && !warm)return;
            if(!beforeNpc)Array.Clear(candidateWorldVisited,0,candidateWorldVisited.Length);
            long pass=beforeNpc?++candidateActionPass:++candidateWorldPass;
            int parity=beforeNpc?1:0,probe=-1,changeProbe=-1;long oldest=long.MaxValue,oldestChange=long.MaxValue;bool changeSignificant=false,unfinished=false;
            for(int index=0;index<selection.CandidateCount;index++)
            {
                var npc=selection.Candidate(index);int key=npc.whoAmI*2+parity;var identity=CombatSelection.Identity(npc,observation.Session);
                if(!candidateIdentities[key].Equals(identity))
                {candidatePlans[key]?.Clear();candidateIdentities[key]=identity;candidateDepths[key]=candidateRequests[key]=0;candidateAttempt[key]=0;candidateUnknown[key]=true;if(!beforeNpc){candidateFacts[npc.whoAmI]=default(CandidateFact);candidateChanges[npc.whoAmI]=0;candidateSignificant[npc.whoAmI]=false;}}
                candidateMembership[key]=pass;
                if(!beforeNpc)
                {
                    // These small live facts wake work only. Source and Plan
                    // still validate all premises before publishing a result;
                    // this is neither a validity lease nor a reachability proof.
                    var fact=new CandidateFact(npc,observation.Options.Dummy,sampleTick);
                    var prior=candidateFacts[npc.whoAmI];
                    if(prior.Present && !prior.Same(fact))
                    {
                        if(candidateChanges[npc.whoAmI]==0)candidateChanges[npc.whoAmI]=++candidateChangeTicket;
                        // Ordinary travel can keep every candidate pending.
                        // A body-sized discontinuity or key receive/phase fact
                        // wakes a higher work priority without renewing its
                        // ticket. This still proves nothing about the solve.
                        if(prior.Significant(fact)){candidateSignificant[npc.whoAmI]=true;candidateRequests[key]=0;}
                    }
                    candidateFacts[npc.whoAmI]=fact;
                    long ticket=candidateChanges[npc.whoAmI];bool significant=candidateSignificant[npc.whoAmI];
                    if(ticket>0 && (significant && !changeSignificant || significant==changeSignificant && ticket<oldestChange))
                    {oldestChange=ticket;changeProbe=index;changeSignificant=significant;}
                }
                // A missing progress stamp wins first. Thereafter the oldest
                // reserved attempt wins, including unknowns needing extension.
                // Cheap distance revisits must not renew this stamp, or every
                // fully scanned short prefix would starve its longer unknowns.
                if(candidateAttempt[key]==0 || candidateUnknown[key])unfinished=true;
                if(candidateAttempt[key]<oldest){oldest=candidateAttempt[key];probe=index;}
            }
            for(int key=parity;key<candidateMembership.Length;key+=2)
                if(candidateMembership[key]!=pass)
                {candidatePlans[key]?.Clear();candidateIdentities[key]=default(NpcIdentity);candidateDepths[key]=candidateRequests[key]=0;candidateAttempt[key]=0;candidateUnknown[key]=false;if(!beforeNpc){candidateFacts[key/2]=default(CandidateFact);candidateChanges[key/2]=0;candidateSignificant[key/2]=false;}}
            // Alternate significant changes with reserved progress. Ordinary
            // travel uses the changed queue after progress completes; it does
            // not renew pending tickets or reset the other queue's stamps.
            // Only an actual admission removes a ticket; unfinished work goes
            // to a new tail ticket, never back to this pass's chosen priority
            // slot. An unresolved significant change keeps its work priority,
            // not its old queue position; admission alone is not completion.
            // Alternating ordinary first admissions still guarantees that
            // unfinished significant work cannot monopolize every pass.
            // Ordinary travel cannot displace an unfinished reserved turn.
            // Significant receive/phase changes still alternate with it; once
            // progress is complete, ordinary changed facts keep their queue.
            if(!beforeNpc && (pass&1)==0 && changeProbe>=0 && (changeSignificant || !unfinished))probe=changeProbe;
            candidates.Clear();int best=-1,distanceStart=0;
            var nearest=CombatSelection.Identity(selection.Candidate(0),observation.Session);
            int hint=-1;
            if(actionHint.Token!=null)
                for(int index=0;index<selection.CandidateCount;index++)
                    if(candidateIdentities[selection.Candidate(index).whoAmI*2+parity].Equals(actionHint)){hint=index;break;}
            if(hint>=0)
            {
                var hintIdentity=candidateIdentities[selection.Candidate(hint).whoAmI*2+parity];
                var hintPlan=candidatePlans[hintIdentity.Slot*2+parity];
                if(!observation.PredictionAvailableFor(hintIdentity) || hintPlan!=null && hintPlan.Faults.Blocks(FaultContextFor(hintIdentity)))hint=-1;
            }
            if(hint<0){actionHint=default(NpcIdentity);actionHintRequired=0;}
            // The whole candidate pass borrows only a lower cumulative slice
            // of this real update. Leaving it restores the shared allowance for
            // the selected target's actual firing/control preparation.
            using(CombatPredictionWork.Limit(3))
            {
                // This retained value is only the last positive's identity and
                // work range, never an old Contact. A real action validates it
                // in BeforeNpc BEFORE optional exploration can spend the slice.
                // Context changes above or leaving the candidate set retire it.
                if(beforeNpc && hint>=0 && CombatPredictionWork.TryContinue())
                {
                    var npc=selection.Candidate(hint);int key=npc.whoAmI*2+parity;
                    int required=Math.Min(NpcPredictionCache.Horizon,Math.Max(ActionPreferred(candidateIdentities[key]),actionHintRequired));
                    if(candidateAttempt[key]==0)candidateAttempt[key]=pass;
                    if(EvaluateCandidate(candidateIdentities[key],sampleTick,true,phase,key,required))best=hint;
                }
                // A fresh actual-phase positive is a usable opening. Check
                // its nearer competitors before optional fair exploration can
                // spend this slice; World keeps probe-first long/unknown
                // progress. No old hint contact participates in this rank.
                if(beforeNpc && best>=0)
                {
                    for(;distanceStart<best;distanceStart++)
                    {
                        if(!CombatPredictionWork.TryContinue())break;
                        var npc=selection.Candidate(distanceStart);int key=npc.whoAmI*2+parity;
                        if(candidateAttempt[key]==0)candidateAttempt[key]=pass;
                        if(EvaluateCandidate(candidateIdentities[key],sampleTick,true,phase,key,CandidatePreferred(candidateIdentities[key])))
                        {best=distanceStart;break;}
                    }
                }
                // Once the actual-phase hint and every nearer prefix have been
                // checked, reserve the remaining update for its real consumer.
                // A second, longer fair probe of an already missed nearer NPC
                // cannot change this completed rank without repeating work;
                // World owns fair progress and later fresh facts still rerank.
                if((!beforeNpc || best<0) && probe>=0 && probe!=best && (!beforeNpc || probe!=hint) && CombatPredictionWork.TryContinue())
                {
                    var npc=selection.Candidate(probe);int key=npc.whoAmI*2+parity;var identity=candidateIdentities[key];
                    int required=CandidateProgressRequired(identity,key,beforeNpc);
                    // Charge progress at actual admission, even if this attempt
                    // later pauses. A costly unknown cannot monopolize the next
                    // pass by preventing a remainder-only cursor from advancing.
                    candidateAttempt[key]=pass;
                    if(!beforeNpc)candidateWorldVisited[probe]=true;
                    if(EvaluateCandidate(identity,sampleTick,beforeNpc,phase,key,required) && (best<0 || probe<best))best=probe;
                }
                if(!beforeNpc)
                {
                    // With no fresh positive, spending every remainder on the
                    // same nearest short misses delays unvisited receivers even
                    // though the reserved probe makes eventual progress. Use
                    // the existing attempt order for additional real progress.
                    // The changed/significant queue still owns the first probe.
                    while(CombatPredictionWork.TryContinue())
                    {
                        int next=-1;long least=long.MaxValue;
                        for(int index=0;index<(best<0?selection.CandidateCount:best);index++)
                        {
                            if(candidateWorldVisited[index])continue;
                            if(best>=0){next=index;break;}
                            int key=selection.Candidate(index).whoAmI*2+parity;
                            if(candidateAttempt[key]<least){least=candidateAttempt[key];next=index;}
                        }
                        if(next<0)break;
                        var npc=selection.Candidate(next);int nextKey=npc.whoAmI*2+parity;var identity=candidateIdentities[nextKey];
                        bool progress=best<0;
                        int required=progress?CandidateProgressRequired(identity,nextKey,false):CandidatePreferred(identity);
                        // Fresh positive ranking keeps the original nearer
                        // prefix first. A tiny remainder may publish no prefix
                        // or interrupt Plan. It cannot consume a reserved turn
                        // merely by entering Source. Completed fresh work can,
                        // so it need not take another first slice unnecessarily.
                        candidateWorldVisited[next]=true;
                        if(EvaluateCandidate(identity,sampleTick,false,phase,nextKey,required))best=next;
                        var completed=candidatePlans[nextKey];var completedPath=completed?.Timeline;
                        if(completed?.Prepared==true && completed.PreparedTick==Main.GameUpdateCount && completed.Phase==phase &&
                            completedPath!=null && completedPath.SampleTick==sampleTick && !completedPath.BudgetPaused && completedPath.Count>required)
                            candidateAttempt[nextKey]=pass;
                    }
                }
                // Positives are only this pass's freshly validated contacts.
                // Visit the nearer prefix in the original receive-box distance
                // order, so the progress slot does not create a new target rank.
                for(int index=distanceStart;beforeNpc && index<selection.CandidateCount && (best<0 || index<best);index++)
                {
                    if(index==probe || beforeNpc && index==hint)continue;
                    if(!CombatPredictionWork.TryContinue())break;
                    var npc=selection.Candidate(index);int key=npc.whoAmI*2+parity;
                    // Record the initial actual admission once. Subsequent
                    // cheap visits do not renew the reserved progress stamp.
                    if(candidateAttempt[key]==0)candidateAttempt[key]=pass;
                    // Linear arrival estimates already include this plan's
                    // legal cooldown. Keep their full required prefix; only
                    // mechanisms without that estimate start progressively.
                    if(EvaluateCandidate(candidateIdentities[key],sampleTick,beforeNpc,phase,key,CandidatePreferred(candidateIdentities[key]))){best=index;break;}
                }
            }
            if(best>=0)
            {
                var npc=selection.Candidate(best);var identity=CombatSelection.Identity(npc,observation.Session);
                candidates.Reachable=identity;selection.Choose(npc,observation.Session,true);
                var plan=candidatePlans[npc.whoAmI*2+parity];
                // Exploration depth is historical progress, not current work.
                // Donate the complete freshly solved prefix: truncating to its
                // receive tick would invalidate the hand's direction/dependency
                // proof and replay the weapon solve in the selected owner.
                if(beforeNpc && plan?.Timeline!=null)
                    SetActionDemand(Math.Max(ActionPreferred(identity),plan.Timeline.Count-1));
                if(beforeNpc && CandidateHandPrepared(plan) && plan.MotionTerrain!=null)
                    candidateHandPlan=plan;
                // A farther optional positive does not erase the opportunity
                // to revalidate a nearer hint in the next real input phase.
                // It still determines this pass's selection normally. Before
                // uses only fresh results after consuming that work opportunity;
                // its new best is never limited by the old World hint's rank.
                if(beforeNpc || actionHint.Token==null || hint<0 || best<=hint)
                {actionHint=identity;actionHintRequired=plan?.Contact==null?0:Math.Min(NpcPredictionCache.Horizon,Math.Max(1,plan.Contact.ReceiveTick+1));}
            }
            if(!beforeNpc && best>=0)
            {
                candidatePresentationPlan=candidatePlans[selection.Target.Slot*2];
                presentationSelection=selection.Target;presentationNearest=nearest;presentationSelectionTick=sampleTick;selectionWeapon=item;presentationOptions=observation.Options;presentationReachable=best>=0;
            }
        }
        private int CandidateProgressRequired(NpcIdentity identity,int key,bool beforeNpc)
        {
            int preferred=CandidatePreferred(identity),requested=candidateRequests[key];
            var plan=candidatePlans[key];var path=plan?.Timeline;
            // A previous positive supplies a work range, never a current proof.
            // Keep enough of the fresh sample to retry that receive instead of
            // replacing a late complex contact with the initial short prefix.
            if(plan?.Contact!=null && path!=null && path.Identity.Equals(identity))
            {
                long sample=(long)Main.GameUpdateCount-(beforeNpc?1:0),remaining=plan.Contact.ReceiveTick-(sample-path.SampleTick);
                if(remaining>=0)preferred=Math.Max(preferred,(int)Math.Min(NpcPredictionCache.Horizon,remaining+1));
            }
            // A newly observed discontinuity clears requested once above.
            // The significant queue ticket may survive a complete short miss;
            // it is priority, not permission to restart at the same short range
            // forever. Normal movement retains progress and its fair stamp.
            if(requested==0 || !candidateUnknown[key])return preferred;
            // Source completion is not solve completion. Repeating a budget-
            // interrupted Plan at twice the range pays more Source work before
            // retrying the missing solve and can starve an otherwise reachable
            // receiver. Grow only a fully solved, nonpaused requested prefix.
            bool complete=plan!=null && plan.Prepared && plan.Contact==null && path!=null && !path.BudgetPaused && path.Count>requested;
            return Math.Min(NpcPredictionCache.Horizon,Math.Max(preferred,complete?Math.Max(32,requested*2):requested));
        }
        private bool EvaluateCandidate(NpcIdentity identity,long tick,bool beforeNpc,HostAttackPhase phase,int key,int required)
        {
            candidateUnknown[key]=true;
            // An unavailable/fault-latched receiver has no calculation permit.
            // Retire its wake-up ticket, rather than calling this admission or
            // letting it permanently occupy the changed queue's first slot.
            // The fault latch remains intact; later changes still pass this
            // same front door and cannot trigger a blocked solve.
            if(!observation.PredictionAvailableFor(identity))
            {if(!beforeNpc){candidateChanges[identity.Slot]=0;candidateSignificant[identity.Slot]=false;}if(actionHint.Equals(identity)){actionHint=default(NpcIdentity);actionHintRequired=0;}return false;}
            var plan=candidatePlans[key];if(plan==null)candidatePlans[key]=plan=new PreviewPlan();
            var context=FaultContextFor(identity);if(plan.Faults.Blocks(context))
            {if(!beforeNpc){candidateChanges[identity.Slot]=0;candidateSignificant[identity.Slot]=false;}if(actionHint.Equals(identity)){actionHint=default(NpcIdentity);actionHintRequired=0;}return false;}
            bool changedAdmission=!beforeNpc && candidateChanges[identity.Slot]!=0;
            bool significantAdmission=changedAdmission && candidateSignificant[identity.Slot];
            if(changedAdmission){candidateChanges[identity.Slot]=0;candidateSignificant[identity.Slot]=false;}
            // required was captured by the caller before this real admission.
            // Consume one BeforeNpc re-evaluation opportunity, not an old
            // contact. Budget denial before entry leaves the hint pending;
            // unavailable/fault gates above retire it without claiming a solve.
            if(beforeNpc && actionHint.Equals(identity)){actionHint=default(NpcIdentity);actionHintRequired=0;}
            try
            {
                // Cheap nearer-rank revisits may request a shorter prefix than
                // this candidate's unfinished reserved turn. They must not
                // erase that work range; otherwise each cooldown pass restarts
                // a late receiver before its longer solve can complete. A real
                // significant fact above explicitly resets this range once.
                candidateRequests[key]=Math.Max(candidateRequests[key],required);
                if(beforeNpc && observation.Prediction.OwnsTarget(identity))SetActionDemand(required);
                var timeline=observation.Prediction.TryCandidateReuse(identity,tick,beforeNpc,required,CandidateMissCurrent(plan,identity,tick,phase,required));
                var publication=timeline;
                // An incumbent may own a longer immutable display publication.
                // Candidate work consumes only its admitted prefix; the view
                // retains capture/input/calculation identity and does not copy,
                // republish or reduce the independent selected cache's future.
                if(timeline!=null && timeline.Count>required+1)
                {NpcTrajectory prefix;if(timeline.TryWindow(timeline.SampleTick,required,timeline.Version,out prefix))timeline=prefix;}
                // A request is not completed work. Only a published prefix grows
                // this candidate's future search; pauses remain unresolved.
                if(timeline!=null)candidateDepths[key]=Math.Max(candidateDepths[key],timeline.Count-1);
                var contact=timeline!=null && timeline.Count>=2?PreparePlan(plan,timeline,phase):null;
                // A complete primary-hand miss does not disprove its own
                // not-yet-born OnHit family. In particular Volcano's finite
                // explosion can reach this receiver after another body is hit.
                // Keep that causal stage unresolved until its family solver
                // provides a contact; a genuine despawn still closes the tail.
                candidateUnknown[key]=contact==null && (timeline==null || timeline.BudgetPaused || timeline.Stop==PredictionStop.None && (timeline.Count<=NpcPredictionCache.Horizon || HasOwnMeleeHitFamily(combat.Player.HeldItem)));
                plan.MotionTerrain=null;plan.MotionSource=null;
                // Seal only a completed positive before another candidate can
                // replace the producing owner's cells. This bounded copy ends
                // the already admitted work; a second producer admission here
                // would discard a complete result without saving any solve.
                // Its cost still belongs to the same optional slice. Host must
                // separately admit/validate display under the unchanged shared
                // allowance; this snapshot grants no display or actual input.
                if(contact!=null && (beforeNpc && CandidateHandPrepared(plan) || !beforeNpc && observation.Path && !BasicPermission))
                {
                    plan.MotionSource=observation.Prediction.CandidateTerrainOwner(publication);
                    plan.MotionTerrain=plan.MotionSource?.Terrain.FreezeCells();
                }
                return contact!=null;
            }
            catch(CombatPredictionBudgetExceededException){return false;}
            catch(Exception error){PropagateFatal(error);plan.Faults.Record(context);plan.Clear();candidateUnknown[key]=false;return false;}
            finally
            {
                // A paused prefix or interrupted solve is still work owed to
                // this changed fact. Only a Contact, complete miss or modeled
                // terminal/fault retires it; retry goes behind other tickets.
                if(changedAdmission && candidateUnknown[key])
                {candidateChanges[identity.Slot]=++candidateChangeTicket;candidateSignificant[identity.Slot]=significantAdmission;}
            }
        }
        private void ClearCandidateProgress()
        {
            Array.Clear(candidateIdentities,0,candidateIdentities.Length);Array.Clear(candidateDepths,0,candidateDepths.Length);Array.Clear(candidateRequests,0,candidateRequests.Length);Array.Clear(candidateUnknown,0,candidateUnknown.Length);Array.Clear(candidateAttempt,0,candidateAttempt.Length);Array.Clear(candidateMembership,0,candidateMembership.Length);
            Array.Clear(candidateFacts,0,candidateFacts.Length);Array.Clear(candidateChanges,0,candidateChanges.Length);Array.Clear(candidateSignificant,0,candidateSignificant.Length);candidateChangeTicket=0;
            candidateActionPass=candidateWorldPass=0;candidateWarmTick=-1;candidateWeapon=null;candidateOptions=null;presentationReachable=false;presentationOptions=null;actionHint=default(NpcIdentity);actionHintRequired=0;
            candidatePresentationPlan=null;
            candidateHandPlan=null;singleCandidate=default(NpcIdentity);
            for(int i=0;i<candidatePlans.Length;i++)candidatePlans[i]?.Clear();
        }
        private void ClearCandidatePlans()
        {ClearCandidateProgress();presentationSelectionTick=-1;selectionWeapon=null;candidates.Clear();}
        private readonly struct CandidateFact
        {
            internal readonly bool Present;
            private readonly Vector2 position,velocity;
            private readonly float phase;
            private readonly int life;
            private readonly bool receives;
            private readonly long tick;
            private readonly float bodyScaleSquared;
            internal CandidateFact(NPC npc,bool dummy,long tick)
            {Present=true;position=npc.position+npc.netOffset;velocity=npc.velocity;phase=npc.ai[0];life=npc.life;receives=CombatSelection.Receives(npc,dummy);this.tick=tick;bodyScaleSquared=Math.Max(1,(float)npc.width*npc.width+(float)npc.height*npc.height);}
            internal bool Same(CandidateFact other)
            {return position==other.position && velocity==other.velocity && phase==other.phase && life==other.life && receives==other.receives;}
            internal bool Significant(CandidateFact other)
            {
                // Use the entire real observation interval, not a one-update
                // distance threshold that upgrades steady travel at a slower
                // preparation cadence. The native body's diagonal supplies a
                // scheduling scale; Source remains the complete premise owner.
                long elapsed=other.tick-tick;if(elapsed<0)return true;
                var expected=position+velocity*elapsed;
                return phase!=other.phase || life!=other.life || receives!=other.receives ||
                    Vector2.DistanceSquared(expected,other.position)>Math.Max(bodyScaleSquared,other.bodyScaleSquared);
            }
        }
    }
}
