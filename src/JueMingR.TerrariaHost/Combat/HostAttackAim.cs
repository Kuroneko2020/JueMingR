using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackAim
    {
        private readonly HostCombat combat;
        private readonly HostCombatObservation observation;
        internal readonly HostAttackControl Control;
        private Item weapon;
        private int slot,type,prefix;
        private long session;
        private uint prepared;
        private Vector2 origin,nextOrigin;
        private AttackAmmoSnapshot ammo;
        private AttackContact current,next;
        private bool preparedOrdinary;
        private HostAttackPhase preparedOrdinaryPhase;
        private PredictionPlayerMotion preparedMotion;
        private int preparedDirection,preparedAnimation,preparedAnimationMax,preparedTime,preparedReuse,preparedImmune;
        private int preparedAttackCooldown,preparedMeleeCooldown;private float preparedMeleeSpeed,preparedScale;
        private HostProjectileEnvironment environment;
        private HostProjectileEnvironment.State environmentState;
        private HostAttackObstacles obstacles;
        private HostHarpyAttack.State harpyState;
        private readonly PredictionTerrain terrain=new PredictionTerrain();
        private PredictionTerrain.CellLease handTerrain,handMotionTerrain;
        private Player sharedHandOwner;
        private readonly HostSwingAttack swing=new HostSwingAttack();
        private readonly HostCadenceAim cadence;
        private readonly HostAttackCandidates candidates=new HostAttackCandidates();
        private AttackContact presentation;private uint presentationStep;private bool presentationOrdinary;
        private int preferred=4,expansion,actionRequired;
        private bool demandActive;
        private Item demandWeapon;private NpcIdentity demandTarget;
        internal bool TimelineCurrent(NpcTrajectory timeline)
        {
            var published=observation.Prediction.Cache.Read(1);
            return ReferenceEquals(timeline,published) || timeline!=null && published!=null && timeline.CalculationToken!=null && ReferenceEquals(timeline.CalculationToken,published.CalculationToken) && timeline.Identity.Equals(published.Identity) && timeline.SampleTick==published.SampleTick && timeline.CaptureTick==published.CaptureTick;
        }
        // A fault belongs to a concrete attack context. Changing a target or
        // weapon restores healthy work without retrying the retained bad one.
        internal bool Failed {get{return executionFaults.Blocks(FaultContextFor(observation.Selection.Target));}}
        // Identity continuity alone is not permission to receive an attack.
        // Read only the already selected live receiver; phase displacement
        // never revokes it, but current damage/friendly/death gates do.
        internal bool CurrentReceiver {get{return observation.Selection.HasTarget && CombatSelection.Valid(observation.Selection.Target,observation.Session) && CombatSelection.Receives(Main.npc[observation.Selection.Target.Slot],observation.Options.Dummy);}}
        internal AttackContact ExpectedImpact {get{if(!CurrentReceiver){ClearExecution();return null;}return Valid(false)?(Main.GameUpdateCount==prepared?current:next):Control.ExpectedImpact;}}
        internal void BindPresentation(AttackContact result)
        {presentation=result;presentationStep=Main.GameUpdateCount;presentationOrdinary=ReferenceEquals(result,current) || ReferenceEquals(result,next);if(!presentationOrdinary)Control.BindPresentation(result);}
        // Capture already performed full dependency checks during Prepare.
        // Draw reads that same-frame lease and known identity/consumption fields
        // only. Arbitrary unobserved world changes wait for the next preparation;
        // native consumers still revalidate terrain/qualification before borrowing.
        internal bool PresentationCurrent(AttackContact result)
        {
            if(!CurrentReceiver)return false;
            var player=combat.Player;
            if(result==null || !ReferenceEquals(presentation,result) || presentationStep!=Main.GameUpdateCount || Failed || !observation.Options.Aim || !observation.Settings.CanRun || player==null || !combat.Admitted(player) || !observation.Selection.HasTarget || !result.Timeline.Identity.Equals(observation.Selection.Target) || !CombatSelection.Valid(observation.Selection.Target,observation.Session) || !TimelineCurrent(result.Timeline))return false;
            if(!presentationOrdinary)return Control.PresentationCurrent(result);
            return (combat.Left || player.controlUseItem || player.channel || combat.Use.Active) && session==observation.Session && player.selectedItem==slot && ReferenceEquals(player.HeldItem,weapon) && weapon.type==type && weapon.prefix==prefix &&
                ReferenceEquals(result,Main.GameUpdateCount==prepared?current:next) && HostAttackWindow.NextAction(combat,player,weapon) && (ammo==null?HostSwingAttack.Handles(weapon):ammo.MembersMatch(player,weapon)) &&
                Vector2.DistanceSquared(HostAttackModels.LaunchOrigin(player,weapon),Main.GameUpdateCount==prepared?origin:nextOrigin)<=4 && environmentState.Current && (environment==null || environment.Unchanged) && (obstacles==null || obstacles.BirthInputsCurrent) && (harpyState==null || harpyState.MembersCurrent);
        }
        internal HostAttackAim(HostCombat combat,HostCombatObservation observation)
        {
            this.combat=combat;this.observation=observation;Control=new HostAttackControl(combat,observation);
            cadence=new HostCadenceAim(combat,observation);combat.Aim.Provider=cadence.Read;
            combat.Facing.TargetProvider=SharedFacing;combat.Facing.SharedTargetRequired=()=>BasicPermission;
        }
        private FacingTarget SharedFacing(Player player)
        {
            if(!ReferenceEquals(player,combat.Player) || !BasicPermission || !observation.Selection.HasTarget || !CombatSelection.Valid(observation.Selection.Target,observation.Session))return null;
            var npc=Main.npc[observation.Selection.Target.Slot];if(!CombatSelection.Receives(npc,observation.Options.Dummy))return null;
            // This finite hand solver can choose either facing at native start,
            // but an already progressing hand is replayed in its real facing.
            // A later unavailable family Contact must not turn that admitted
            // hand back toward the final receiver before its trigger is hit.
            if(HasOwnMeleeHitFamily(player.HeldItem) && player.itemAnimation>0)return new FacingTarget(npc,observation.Session,player.MountedCenter+Vector2.UnitX*player.direction*100);
            Vector2 point;return TryPreparedMeleeFacing(player,out point)?new FacingTarget(npc,observation.Session,point):new FacingTarget(npc,observation.Session,true);
        }
        internal bool Permission {get{return !Failed && BasicPermission;}}
        internal bool ActionPreparationNeeded {get{return combat.Player!=null && HostAttackWindow.NextAction(combat,combat.Player,combat.Player.HeldItem) && Control.FiniteHeldActionNeeded;}}
        internal bool UrgentPreparation {get{return Permission && (ActionPreparationNeeded || Control.Pending);}}
        internal int ActionRequired {get{return actionRequired;}}
        internal void BeginActionDemand(){actionRequired=0;}
        internal void SetActionDemand(int required)
        {actionRequired=preferred=Math.Min(NpcPredictionCache.Horizon,Math.Max(1,required));observation.Prediction.Cache.Demand(1,1,preferred);}
        internal void Demand(bool beforeNpc=false)
        {
            if(UrgentPreparation && Eligible(combat.Player.HeldItem) && !HostPlacementAttack.Handles(combat.Player.HeldItem))
            {demandActive=true;preferred=Preferred();if(beforeNpc)preferred=actionRequired>0?actionRequired:ActionPreferred(observation.Selection.Target);observation.Prediction.Cache.Demand(1,1,preferred);}
            else
            {
                observation.Prediction.Cache.Release(1);expansion=0;
                if(!Permission || combat.Player==null || !combat.Player.channel)Control.ReleaseFiniteHeldDemand();
                // An empty outer input epoch does not end a live controller's
                // observed source. No producer is requested, and consumption
                // still uses Permission/phase/member gates. Retire on the real
                // feature/weapon edge, not merely the absence of urgent work.
                if(demandActive){demandActive=false;if(!AttackObservationDemand || !observation.Options.Aim)Clear();}
            }
        }
        private int Preferred()
        {
            var player=combat.Player;var item=player.HeldItem;var target=observation.Selection.Target;
            if(!ReferenceEquals(item,demandWeapon) || !target.Equals(demandTarget)){expansion=0;demandWeapon=item;demandTarget=target;}
            if(!observation.Selection.HasTarget)return 4;
            return PreferredFor(target,expansion);
        }
        // Candidate hints are pure and use that candidate's actual identity.
        // They never update the selected owner's expansion/history fields.
        internal int ActionPreferred(NpcIdentity target)
        {return ActionPreferred(target,false);}
        private int CandidatePreferred(NpcIdentity target)
        {return ActionPreferred(target,true);}
        private int ActionPreferred(NpcIdentity target,bool futureLaunch)
        {
            var player=combat.Player;var item=player.HeldItem;
            var captured=AttackAmmoSnapshot.Capture(player,item);Projectile body;AttackMotion motion;int finiteRange=16;
            // The sealed primary whip ends at its native animation clock.
            // Request that finite hand once instead of replaying 16 -> 32.
            // This schedules NPC work only; actual rope/receive/input checks
            // and any independently born damage roles keep their own owners.
            if(!futureLaunch && captured!=null && HostMeleeAttack.KnownWhipParent(captured.Projectile))
                return Math.Min(NpcPredictionCache.Horizon,HostMeleeAttack.WhipSearchRequired(player,item));
            // A current hand may avoid blind short-prefix restarts. Optional
            // cooldown candidates keep their cheaper progressive range: using
            // this hint there repeatedly enlarged NPC work with no supply gain.
            if(!futureLaunch && captured!=null && CombatSelection.Valid(target,observation.Session) && ContentSamples.ProjectilesByType.TryGetValue(captured.Projectile,out body) && body.aiStyle==19 && HostMeleeAttack.DerivedSpearChild(body.type)!=0)
                return HostMeleeAttack.PreferredSpearRange(player,item,captured,Main.npc[target.Slot],futureLaunch?Math.Max(0,HostAttackWindow.PreviewDelay(player,item)):0);
            if(!futureLaunch && captured!=null && CombatSelection.Valid(target,observation.Session))
            {
                if(HostBetsyFlameAttack.Handles(captured.Projectile))return HostBetsyFlameAttack.PreferredRange(player,captured,Main.npc[target.Slot]);
                if(HostBottleAttack.Handles(captured.Projectile))return HostBottleAttack.PreferredRange(player,captured,Main.npc[target.Slot]);
                if(HostNativeArrowBirthAttack.Weapon(item.type))return HostNativeArrowBirthAttack.PreferredRange(player,captured,Main.npc[target.Slot]);
                if(HostYoyoNavigation.Weapon(item) && combat.Use.ManagedAttackIntent(player))return Math.Min(120,Math.Max(16,(int)Math.Ceiling(Vector2.Distance(player.MountedCenter,Main.npc[target.Slot].Center)/Math.Max(1,captured.Speed))+16));
                // A registered finite held gun now prepares only at the real
                // post-NPC pass. Its old generic 16-point action prefix could
                // repeatedly expire just before an ordinary bullet arrived,
                // exhausting whole-scene directions without asking the source
                // for the needed future. Reuse the ordinary arrival+8 demand
                // hint with the CONTROLLER's actual selected-ammo speed.
                // This is desired NPC coverage, never a Contact/miss proof or
                // a new horizon/budget. Complex kernels keep progressive work.
                if((item.shoot==615 || item.shoot==630 || item.shoot==705) && Control.HasFiniteHeldSources)
                {
                    finiteRange=Control.RequiredFiniteHeldRange(target);
                    var selected=AttackAmmoSnapshot.CaptureController(player,item,item.shoot==705?12:14);
                    if((item.shoot!=705 || player.phantomPhoneixCounter+1<3) && selected!=null && HostAttackModels.TryResolved(selected.Projectile,selected.Speed,item.type,out motion) && motion.Gravity==0 && motion.Acceleration==1 && motion.MaxSpeed<=0 && !motion.StopSmallVelocity)
                    {
                        float speed=motion.Speed;
                        // Native component limiting preserves diagonal length.
                        // The axial branch is the slowest legal .97^n class;
                        // use it only for this conservative scheduling hint.
                        if(motion.ComponentBirthLimit)while(speed>=16)speed*=.97f;
                        var npc=Main.npc[target.Slot];float closing=Math.Max(1,speed*motion.Updates-npc.velocity.Length());
                        return Math.Min(NpcPredictionCache.Horizon,Math.Max(finiteRange,(int)Math.Ceiling(Vector2.Distance(player.MountedCenter,npc.Center+npc.netOffset)/closing)+8));
                    }
                }
            }
            // Complex models have no general panel-speed arrival estimate.
            // Start a positive search, then extend the same sample only if its
            // real consumer remains unresolved; this is not a range cap.
            if(!HostAttackWindow.Ordinary(item) || captured==null || !ContentSamples.ProjectilesByType.TryGetValue(captured.Projectile,out body) || HostMeleeAttack.Handles(body) || HostSkyAttack.Handles(item.type) || (captured.WeaponType==4953 || captured.WeaponType==3788) || HostEffectAttack.Handles(captured.Projectile) || HostHarpyAttack.Handles(player,captured) || !HostAttackModels.TryRead(captured,out motion) || motion.Gravity!=0 || motion.Acceleration!=1)return finiteRange;
            // A candidate plan can describe the next hand after the current
            // cooldown. Its NPC prefix must cover that existing launch delay
            // as well as linear travel; a generic short cap loses valid nearer
            // receivers. Complex mechanisms retain their progressive search.
            int delay=futureLaunch?Math.Max(0,HostAttackWindow.PreviewDelay(player,item)):0;
            return Math.Min(NpcPredictionCache.Horizon,PreferredFor(target,0)+delay);
        }
        private int PreferredFor(NpcIdentity target,int extra)
        {
            var player=combat.Player;var item=player.HeldItem;
            if(!CombatSelection.Valid(target,observation.Session))return 4;
            if(HostPhasebladeAttack.Handles(item.shoot))return NpcPredictionCache.Horizon;
            if(HasOwnMeleeHitFamily(item))return NpcPredictionCache.Horizon;
            if(HostSwingAttack.Handles(item) && (item.shoot<=0 || HostGrassCurveAttack.Family(item.shoot) || HostLightsBaneAttack.Family(item.shoot)))return Math.Min(120,Math.Max(4,player.itemAnimation>0?player.itemAnimation+1:item.useAnimation+1));
            if(item.type==3541)return 32;
            if(HostAttackWindow.Ordinary(item))
            {
                var captured=AttackAmmoSnapshot.Capture(player,item);AttackMotion motion;
                if(captured!=null && HostAttackModels.TryRead(captured,out motion) && motion.Gravity==0 && motion.Acceleration==1)
                {
                    var npc=Main.npc[target.Slot];float distance=Vector2.Distance(player.MountedCenter,npc.Center+npc.netOffset);
                    float closing=Math.Max(1,motion.Speed*motion.Updates-npc.velocity.Length());
                    return Math.Min(120,Math.Max(16,(int)Math.Ceiling(distance/closing)+8+extra));
                }
            }
            return NpcPredictionCache.Horizon;
        }
        internal void ClearExecution(){presentation=null;weapon=null;ammo=null;current=next=null;environment=null;obstacles=null;harpyState=null;handTerrain=handMotionTerrain=null;sharedHandOwner=null;preparedOrdinary=false;cadence.Clear();Control.Clear();}
        internal void Clear(){ClearExecution();ClearPreview();}
        internal void Reset(){demandActive=false;releaseHand=null;Clear();ClearCandidatePlans();RetryFaults();swing.Clear();Control.Reset();hasConsumedInput=false;}
        internal void ObserveSwing(Player player,Item item,Rectangle frame,float offset)
        {
            if(!ReferenceEquals(player,combat.Player) || !HostSwingAttack.Handles(item) || !AttackObservationDemand)return;
            try{using(observation.PredictionWork.Enter(Main.GameUpdateCount)){swing.Observe(item,frame,offset);if(Permission && player.itemAnimation>0 && !HostSwingAttack.Mixed(item) && (!HostPhasebladeAttack.Handles(item.shoot) || combat.Left || player.controlUseItem))PrepareSwing(player,item,observation.Prediction.Cache.Read(1),HostAttackPhase.BeforeNpc,true);}}
            catch(CombatPredictionBudgetExceededException){ClearExecution();}
            catch(Exception error){FailLocal(error);}
        }
        private void PrepareSwing(Player player,Item item,NpcTrajectory timeline,HostAttackPhase phase,bool naturalPose)
        {
            environmentState=new HostProjectileEnvironment.State(true,HostProjectileEnvironment.HandWindRelevant(player,item),player);
            if(timeline==null || !timeline.Identity.Equals(observation.Selection.Target))return;
            current=swing.Solve(player,item,timeline,new HostAttackClock(timeline,phase),terrain,naturalPose);obstacles=swing.FamilyRoot;next=null;ammo=null;
            weapon=item;slot=player.selectedItem;type=item.type;prefix=item.prefix;session=observation.Session;prepared=Main.GameUpdateCount;origin=player.RotatedRelativePoint(player.MountedCenter);
        }
        internal void PrepareNatural(){ObserveReleaseHand();observation.PrepareAction();}
        internal void Prepare()
        {PreparePhase(HostAttackPhase.CompletedWorld);}
        internal void PrepareAction()
        {PreparePhase(HostAttackPhase.BeforeNpc);}
        internal void PrepareProjectiles()
        {PreparePhase(HostAttackPhase.Projectiles);}
        internal void PrepareControlledProjectileWorld()
        {
            if(!BasicPermission || !CurrentReceiver || !Control.HasFiniteHeldSources)return;
            try
            {
                // This borrows the existing update's total allowance. It does
                // not refresh the budget or resample/reselect a target in AI.
                using(observation.PredictionWork.Enter(Main.GameUpdateCount))
                    Control.PrepareProjectileWorld(observation.Prediction.Cache.Read(1));
            }
            catch(CombatPredictionBudgetExceededException){ /* Native Basic remains available. */ }
            catch(Exception error){FailLocal(error);}
        }
        internal bool PreparePreviewOnly()
        {
            if(!PreviewDemand)return false;
            bool attempted=false;
            try{using(observation.PredictionWork.Enter(Main.GameUpdateCount)){if(Permission && ExpectedImpact!=null)return false;if(CombatPredictionWork.TryContinue())PreparePreview(HostAttackPhase.CompletedWorld,out attempted);}}
            catch(CombatPredictionBudgetExceededException){EndFlightPreparation();}
            catch(Exception error){PropagateFatal(error);preview.Faults.Record(FaultContextFor(observation.Selection.Target));ClearPreview();}
            return attempted;
        }
        internal void EndPreparation(){candidateHandPlan=null;EndFlightPreparation();}
        private void PreparePhase(HostAttackPhase phase)
        {
            try{using(observation.PredictionWork.Enter(Main.GameUpdateCount))using(new SolvePreparation()){if(CombatPredictionWork.TryContinue())PrepareCore(phase);}}
            catch(CombatPredictionBudgetExceededException){EndFlightPreparation();}
            catch(Exception error){FailLocal(error);}
        }
        internal void FailLocal(Exception error){PropagateFatal(error);executionFaults.Record(FaultContextFor(observation.Selection.Target));Clear();}
        // This synchronous scope survives only recursive extension of one
        // preparation. ClearExecution still revokes every input/contact. A
        // caller storing scratch must additionally key its own phase, origin,
        // player/ammo and clock; neither this cache nor a matching index is a
        // permit. In particular current and next hands have distinct origins.
        internal sealed class SolvePreparation:IDisposable
        {
            [ThreadStatic]private static SolvePreparation active;
            private readonly SolvePreparation prior;
            private readonly System.Collections.Generic.Dictionary<object,object> values=new System.Collections.Generic.Dictionary<object,object>();
            private NpcTrajectory timeline;
            internal static SolvePreparation Current {get{return active;}}
            internal SolvePreparation(){prior=active;active=this;}
            internal object Read(object key){object value;return values.TryGetValue(key,out value)?value:null;}
            internal void Store(object key,object value){values[key]=value;}
            internal static bool Extends(NpcTrajectory prior,NpcTrajectory next)
            {
                if(prior==null || next==null || prior.Stop!=PredictionStop.None || prior.Count>next.Count || prior.SampleTick!=next.SampleTick || prior.CaptureTick!=next.CaptureTick)return false;
                NpcTrajectory prefix=next;
                if(next.Count!=prior.Count && !next.TryWindow(next.SampleTick,prior.Count-1,next.Version,out prefix))return false;
                // Complete immutable point comparison includes identity and
                // generation, owner/raw/projectile bodies, motion/phase,
                // reflection, segment and receive eligibility. Versions from
                // different producers do not substitute for content equality.
                return AttackIntercept.Equivalent(prior,prefix);
            }
            internal bool Bind(NpcTrajectory value)
            {bool same=Extends(timeline,value);if(!same)values.Clear();timeline=value;return same;}
            public void Dispose(){active=prior;}
        }
        private void PrepareCore(HostAttackPhase phase)
        {
            // Re-entering the same native phase is not a new firing window.
            // Reuse only an unconsumed action with identical current premises;
            // BeginShot clears both contacts, so it cannot mint a second lease.
            var held=combat.Player;
            if(preparedOrdinary && preparedOrdinaryPhase==phase && prepared==Main.GameUpdateCount && held!=null && current!=null && CurrentReceiver &&
                preparedDirection==held.direction && preparedAnimation==held.itemAnimation && preparedAnimationMax==held.itemAnimationMax && preparedTime==held.itemTime && preparedReuse==held.reuseDelay &&
                preparedAttackCooldown==held.attackCD && preparedMeleeCooldown==held.meleeNPCHitCooldown[observation.Selection.Target.Slot] && preparedMeleeSpeed==held.meleeSpeed && preparedScale==held.GetAdjustedItemScale(held.HeldItem) &&
                preparedMotion.SameSample(NpcPredictionSource.ReadPlayer(held)) && CurrentReceiver && preparedImmune==Main.npc[observation.Selection.Target.Slot].immune[held.whoAmI] &&
                HostAttackWindow.NextAction(combat,held,held.HeldItem) && Valid(true))return;
            bool beforeNpc=phase==HostAttackPhase.BeforeNpc;ClearExecution();if(!Permission || !CurrentReceiver)return;
            environmentState=new HostProjectileEnvironment.State(true,HostProjectileEnvironment.HandWindRelevant(held,held.HeldItem),held);
            var player=combat.Player;var item=player.HeldItem;if(!Eligible(item))return;
            if(HostPlacementAttack.Handles(item))return; // Placement has its own native use receipt, not a damage contact.
            var timeline=observation.Prediction.Cache.Read(1);if(timeline==null || !timeline.Identity.Equals(observation.Selection.Target))return;
            // Replayed prefixes and their tail share the geometry read union.
            // Reset only when the complete sample prefix cannot be retained;
            // clearing it on every extension would discard earlier dependencies.
            if(SolvePreparation.Current==null || !SolvePreparation.Current.Bind(timeline))terrain.Reset();
            // A released bubble or a pending controlled role can still read
            // direction after the hand's ordinary cooldown has closed. Its
            // true phase is prepared independently of another hand emission.
            Control.Prepare(timeline,phase);
            if(!HostAttackWindow.NextAction(combat,player,item))
            {if(Control.Pending && !Control.HasPreparedInput && observation.ExpandControlPrediction(beforeNpc))PrepareCore(phase);return;}
            if(HostPhasebladeAttack.Handles(item.shoot))
            {
                if(combat.Left || player.controlUseItem)PrepareSwing(player,item,timeline,phase,false);
                else PreparePhasebladeRelease(player,item,timeline,phase);
                if(beforeNpc && current==null && observation.ExpandActionPrediction())PrepareCore(phase);
                return;
            }
            // These native nearby spawns ignore Shoot velocity. Keep their
            // real melee hand here and observe sealed children in Flight; a
            // missing ordinary shot model must not hide the independent hand.
            if(HostSwingAttack.Handles(item) && (item.shoot<=0 || HostGrassCurveAttack.Family(item.shoot) || HostLightsBaneAttack.Family(item.shoot))){PrepareSwing(player,item,timeline,phase,false);if(beforeNpc && current==null && observation.ExpandActionPrediction())PrepareCore(phase);return;}
            var captured=AttackAmmoSnapshot.Capture(player,item);if(captured==null)return;
            cadence.Prepare(player,item,captured,timeline,phase,null);
            if(HostHeldAttack.Weapon(item.type) || HostYoyoNavigation.Weapon(item) || HostWhipAttack.Weapon(item) || HostDirectionalControl.Weapon(player,item))
            {
                if(beforeNpc && !Control.HasPreparedInput && observation.ExpandActionPrediction())PrepareCore(phase);
                return; // Controller birth is not its later ordinary/beam damage phase.
            }
            Projectile sample;bool melee=ContentSamples.ProjectilesByType.TryGetValue(captured.Projectile,out sample) && HostMeleeAttack.Handles(sample);
            AttackMotion motion;bool sky=HostSkyAttack.Handles(item.type);if(!sky && !melee && captured.WeaponType!=4953 && !HostEffectAttack.Handles(captured.Projectile) && !HostAttackModels.TryRead(captured,out motion))return;
            origin=HostAttackModels.LaunchOrigin(player,item);int age=(int)((long)Main.GameUpdateCount-timeline.SampleTick)-(beforeNpc?1:0);if(age<0 || age>1)return;
            bool harpy=HostHarpyAttack.Handles(player,captured);
            if(!TryCandidateHand(player,item,captured,timeline,phase,age))
            {
                if(harpy)
                {
                    // Charm runs in Player before ItemCheck, then each arrow runs
                    // after NPC movement. Its solver owns that whole phase prefix,
                    // including other live arrows competing for the charm.
                    obstacles=new HostAttackObstacles(player,sample,timeline,0,beforeNpc,!beforeNpc);
                    current=HostHarpyAttack.Solve(player,captured,timeline,origin,new HostAttackClock(timeline,phase),terrain,obstacles,out harpyState);
                }
                else current=Solve(player,item,captured,timeline,origin,age,beforeNpc);
            }
            // Actual use may need a late receive: grow the SAME sample in
            // stages. A paused prefix remains unknown and Basic remains legal;
            // the optional display never turns its desired 120 into this gate.
            bool fullWhipHand=HostMeleeAttack.KnownWhipParent(captured.Projectile) && timeline.Count>age+HostMeleeAttack.WhipSearchRequired(player,item);
            // A longer NPC tail cannot extend this finite primary whip's life.
            // Unknown owner motion still remains unknown/Basic; this does not
            // cache a miss or a permit, or suppress another native damage role.
            if(beforeNpc && current==null && !fullWhipHand && timeline.Stop==PredictionStop.None && timeline.Count<NpcPredictionCache.Horizon+1 && !observation.PredictionWork.Exceeded && observation.ExpandActionPrediction())
            {PrepareCore(phase);return;}
            // BeforeNpc has already expanded a nonterminal action miss once
            // above. CompletedWorld keeps the existing later-preparation
            // expansion; neither route pads beyond the accepted horizon or a
            // terminal modeled boundary, and consumers still validate the plan.
            if(current==null && preferred<120 && timeline.Count<=preferred+1)expansion=Math.Min(120,expansion+preferred);else if(current!=null)expansion=0;
            cadence.Prepare(player,item,captured,timeline,phase,current);
            var playerFrame=HostAttackPlayerFrame.Capture(player,timeline);var movement=playerFrame.Motion;PredictionStop stop;
            if(!harpy && !beforeNpc && PlayerMotionContinuation.Advance(ref movement,playerFrame.Environment,terrain,out stop))
            {
                nextOrigin=origin+new Vector2(movement.X-player.position.X,movement.Y-player.position.Y);
                // CompletedWorldUpdate T precedes player attack T+1, then NPC
                // movement T+1, then projectile AI/move/damage T+1. Solve's
                // first subupdate already reads NPC point age+1. Advancing age
                // here as well would consume point 2 in that first phase.
                next=SolveOnTerrain(player,item,captured,timeline,nextOrigin,age,false,terrain,0,out environment,out obstacles,launchOriginAdvanced:true);
            }
            weapon=item;slot=player.selectedItem;type=item.type;prefix=item.prefix;session=observation.Session;prepared=Main.GameUpdateCount;ammo=captured;
            preparedOrdinary=true;preparedOrdinaryPhase=phase;preparedMotion=NpcPredictionSource.ReadPlayer(player);preparedDirection=player.direction;preparedAnimation=player.itemAnimation;preparedAnimationMax=player.itemAnimationMax;preparedTime=player.itemTime;preparedReuse=player.reuseDelay;preparedImmune=Main.npc[timeline.Identity.Slot].immune[player.whoAmI];
            preparedAttackCooldown=player.attackCD;preparedMeleeCooldown=player.meleeNPCHitCooldown[timeline.Identity.Slot];preparedMeleeSpeed=player.meleeSpeed;preparedScale=player.GetAdjustedItemScale(item);
        }
        private static bool Eligible(Item item){return item!=null && !item.sentry && (HostPlacementAttack.Handles(item) || HostSwingAttack.Handles(item) || !item.IsAir && item.useStyle>0 && item.useTime>0 && item.shoot>0 && (item.damage>0 || item.type==905) && item.pick==0 && (item.axe==0 || item.type==5519 && item.shoot==1052) && item.hammer==0 && (!item.summon || ProjectileID.Sets.IsAWhip[item.shoot]));}
        private AttackContact Solve(Player player,Item item,AttackAmmoSnapshot captured,NpcTrajectory timeline,Vector2 start,int age,bool beforeNpc)
        {return SolveOnTerrain(player,item,captured,timeline,start,age,beforeNpc,terrain,0,out environment,out obstacles);}
        private static AttackContact SolveOnTerrain(Player player,Item item,AttackAmmoSnapshot captured,NpcTrajectory timeline,Vector2 start,int age,bool beforeNpc,PredictionTerrain terrain,int delay,out HostProjectileEnvironment environment,out HostAttackObstacles obstacles,HostAttackPlayerFrame? preparedPlayer=null,bool launchOriginAdvanced=false)
        {
            environment=null;obstacles=null;
            if(HostNativeArrowBirthAttack.Weapon(item.type))
            {var native=ContentSamples.ProjectilesByType[captured.Projectile];obstacles=new HostAttackObstacles(player,native,timeline,delay,beforeNpc,!beforeNpc);return HostNativeArrowBirthAttack.Solve(player,captured,timeline,start,new HostAttackClock(timeline,beforeNpc?HostAttackPhase.BeforeNpc:HostAttackPhase.CompletedWorld,delay),terrain,obstacles,launchOriginAdvanced || preparedPlayer.HasValue);}
            if(HostSkyAttack.Handles(item.type))
            {var sky=ContentSamples.ProjectilesByType[captured.Projectile];obstacles=new HostAttackObstacles(player,sky,timeline,delay,beforeNpc,!beforeNpc);return HostSkyAttack.Solve(player,item,captured,timeline,start,age,terrain,beforeNpc,obstacles,delay);}
            if(HostAttachedShotAttack.Handles(captured.Projectile))
            {var attached=ContentSamples.ProjectilesByType[captured.Projectile];obstacles=new HostAttackObstacles(player,attached,timeline,delay,beforeNpc,!beforeNpc);return HostAttachedShotAttack.Solve(player,captured,timeline,start,age,beforeNpc,terrain,obstacles,delay,preparedPlayer);}
            if((captured.WeaponType==4953 || captured.WeaponType==3788) || HostEffectAttack.Handles(captured.Projectile))
            {var effect=ContentSamples.ProjectilesByType[captured.Projectile];obstacles=new HostAttackObstacles(player,effect,timeline,delay,beforeNpc,!beforeNpc);return HostEffectAttack.Solve(player,captured,timeline,start,age,beforeNpc,terrain,obstacles,delay,preparedPlayer);}
            Projectile melee;if(ContentSamples.ProjectilesByType.TryGetValue(captured.Projectile,out melee) && HostMeleeAttack.Handles(melee))
            {
                // A real intervening boomerang hit can enter its return phase.
                // Lease those receivers with both execution and idle preview;
                // Ordinary whips also have ordered rope receivers: an earlier
                // qualified OnHit can exhaust this body's future damage.
                // Intrinsic finite melee families retain the same root that
                // their motor/children captured, through actual consumption.
                // A locally allocated solver root would lose these leases at
                // this public hand boundary even when its Contact is valid.
                if(HostBoomerangAttack.Handles(melee.type) || HostArcSwingAttack.Weapon(item.type) || HostMeleeAttack.KnownWhipParent(melee.type) || HostSpearShardAttack.Handles(melee.type) || HostNorthPoleAttack.Handles(melee.type) || HostJoustingLanceAttack.Handles(melee.type) || HostSolarEruptionAttack.Handles(melee.type) || HostMonkStaffAttack.Handles(melee.type))obstacles=new HostAttackObstacles(player,melee,timeline,delay,beforeNpc,!beforeNpc);
                return HostMeleeAttack.SolvePrepared(player,item,captured,timeline,age,beforeNpc,terrain,delay,obstacles,preparedPlayer??HostAttackPlayerFrame.Capture(player,timeline));
            }
            if(item.type==2624){var b=timeline[Math.Min(age+1,timeline.Count-1)].ProjectileReceiveBounds;start+=(new Vector2(b.CenterX,b.CenterY)-start).SafeNormalize(Vector2.UnitX)*40;}
            Projectile sample;if(!ContentSamples.ProjectilesByType.TryGetValue(captured.Projectile,out sample))return null;
            var receive=HostAttackReceive.Capture(player,sample,timeline.Identity.Slot,beforeNpc,!beforeNpc);
            obstacles=new HostAttackObstacles(player,sample,timeline,delay,beforeNpc,!beforeNpc);
            // age already names the delayed launch sample. Native immunity
            // still counts all elapsed world actions, including the delay.
            AttackMotion motion;if(!HostAttackModels.TryRead(captured,out motion))return null;
            var receivers=obstacles;
            return HostProjectileEnvironment.Solve(sample,terrain,start,motion,timeline,age,0,1,t=>receive.Allows(t+delay),out environment,(k,t,x,y)=>receivers.PassOuter(k,t,delay+motion.OuterAtSubupdate(k),x,y),(k,t,x,y)=>motion.SubupdateInOuter(k)!=0 || receivers.BeforeNpc(t,delay+motion.OuterAtSubupdate(k),x,y));
        }
        private bool Valid(bool checkAmmo)
        {return ValidCore(checkAmmo,false);}
        // Pure melee families consume a prepared facing only at their real
        // native hand boundary. Their absence of ammunition is intentional,
        // not permission for other null-ammo ordinary cursor inputs.
        private bool ValidCore(bool checkAmmo,bool pureMeleeFamily)
        {
            var player=combat.Player;
            if(weapon==null || !Permission || session!=observation.Session || Main.GameUpdateCount<prepared || Main.GameUpdateCount-prepared>1 ||
                player.selectedItem!=slot || !ReferenceEquals(player.HeldItem,weapon) || weapon.type!=type || weapon.prefix!=prefix ||
                !CurrentReceiver)return false;
            if(ammo==null?!HostSwingAttack.Handles(weapon):!ammo.IdentityMatches(player,weapon))return false;
            var result=Main.GameUpdateCount==prepared?current:next;
            if(!checkAmmo && !HostAttackWindow.NextAction(combat,player,weapon))return false;
            if(result==null || !result.Timeline.Identity.Equals(observation.Selection.Target) || !TimelineCurrent(result.Timeline) ||
                Vector2.DistanceSquared(HostAttackModels.LaunchOrigin(player,weapon),Main.GameUpdateCount==prepared?origin:nextOrigin)>4)return false;
            if(handTerrain!=null && (!ReferenceEquals(sharedHandOwner,player) || player.position.X!=preparedMotion.X || player.position.Y!=preparedMotion.Y || player.GetAdjustedItemScale(weapon)!=preparedScale || Main.npc[observation.Selection.Target.Slot].immune[player.whoAmI]!=preparedImmune))return false;
            return environmentState.Current && (handTerrain==null?terrain.Unchanged:handTerrain.Current && handMotionTerrain!=null && handMotionTerrain.Current) && (environment==null || environment.Unchanged) && (obstacles==null || (checkAmmo?obstacles.UnchangedForHandStart:obstacles.Unchanged)) && (harpyState==null || harpyState.Current) && (!checkAmmo || (ammo==null?pureMeleeFamily && HasOwnMeleeHitFamily(weapon):ammo.Matches(player,weapon)));
        }
        internal CombatCursorScope BeginShot(Player player,Item item,bool regular)
        {
            if(player==null || !ReferenceEquals(player,combat.Player) || item==null || !ReferenceEquals(item,player.HeldItem) || !BasicPermission || !Eligible(item))return null;
            if(!CurrentReceiver){ClearExecution();return null;}
            if(item!=null && item.type==3852)
            {
                // Native ItemCheck has now resolved alt1->2 and slow-mana
                // timing. Only this actual cursor-consuming birth may borrow
                // input; ordinary later burst members retain stored direction.
                if(!regular || !HostBookStaffAttack.CursorInput(player,0)){ClearExecution();return null;}
                // Selection/NPC preparation already ran at ItemCheck entry.
                // Its one-per-input-frame gate must not hide this native
                // animation/mode transition from the actual Shoot consumer.
                PrepareAction();
            }
            if(HostPlacementAttack.Handles(item))return regular?BeginPlacement(player,item,true):null;
            if(!regular && item!=null && ProjectileID.Sets.IsAWhip[item.shoot])
            {
                // Snake-band extra swings are real consumers, with random
                // direction and a different duration/role. They may borrow a
                // representative input, never a primary's precise contact.
                current=next=null;var timeline=observation.Prediction.Cache.Read(1);
                if(!Permission || timeline==null || !timeline.Identity.Equals(observation.Selection.Target))return BeginBasic(player,item,null,HostAttackPhase.BeforeNpc);
                try
                {
                    using(observation.PredictionWork.Enter(Main.GameUpdateCount))
                    {
                        Vector2 point;terrain.Reset();return HostWhipAttack.TryExtraPoint(player,timeline,new HostAttackClock(timeline,HostAttackPhase.BeforeNpc),terrain,out point)?CombatCursorScope.Begin(point):BeginBasic(player,item,null,HostAttackPhase.BeforeNpc);
                    }
                }
                catch(CombatPredictionBudgetExceededException){return BeginBasic(player,item,null,HostAttackPhase.BeforeNpc);}
            }
            if(HostHeldAttack.Weapon(item.type) || HostYoyoNavigation.Weapon(item) || HostWhipAttack.Weapon(item) || HostDirectionalControl.Weapon(player,item))return Control.BeginOpening(player,item);
            // These native births establish G11A's real charge controller.
            // Their Shoot does not consume its later AI direction packet.
            if(item.type==5462 || item.type==6153)return null;
            HostAttackInput input;
            if(!TryOrdinaryInput(player,item,HostAttackPhase.BeforeNpc,out input))return null;
            // One prepared ordinary attack is a capability, not a reusable
            // cursor value. Native consumption retires it even if a later
            // refill happens to recreate the same ammo bytes in this step.
            current=next=null;
            candidateHandPlan=null;
            return input.Begin();
        }
    }
}
