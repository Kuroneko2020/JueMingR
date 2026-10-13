using System;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackAim
    {
        private static readonly MethodInfo previewCopy=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);
        private readonly PreviewPlan preview=new PreviewPlan();
        private AttackContact previewPresentation;
        private uint previewStep;
        private PredictionTerrain.CellLease previewTerrain;
        private NpcTrajectory previewDisplaySource;
        private long previewLeaseTick=-1;
        private bool previewWindowMiss;
        internal bool PreviewDemand {get{return observation.Settings.CanRun && combat.Player!=null && combat.Player.active && !combat.Player.dead && !combat.Player.ghost && (observation.Path || observation.Options.Aim) && Eligible(combat.Player.HeldItem);}}
        internal AttackContact ExpectedPreview {get{return PreviewLeaseCurrent()?preview.Contact:null;}}
        internal bool PreviewNeedsMore {get{return preview.Contact==null && preview.Timeline!=null && preview.Timeline.Stop==PredictionStop.None && preview.Timeline.Count<=NpcPredictionCache.Horizon;}}
        internal int PreviewPreferred {get{return observation.Selection.HasTarget?CandidatePreferred(observation.Selection.Target):1;}}
        internal bool PreviewRefreshPending {get{return PreviewDemand && !previewWindowMiss && (preview.Contact!=null || PreviewNeedsMore) && !PreviewLeaseCurrent();}}
        private bool PreviewLeaseCurrent()
        {
            var path=observation.DisplayPath;
            // Only read-only drawing borrows this bounded-age condition. The
            // original receive sample/capture clock stay intact; input leases
            // continue to use their separate exact-phase validation.
            return previewLeaseTick==(long)Main.GameUpdateCount && CurrentReceiver && PreviewDemand && preview.Contact!=null && Main.GameUpdateCount>=previewStep && Main.GameUpdateCount-previewStep<=HostCombatObservation.DisplayMaximumAge && path!=null && preview.Timeline!=null && preview.Timeline.Identity.Equals(observation.Selection.Target) &&
                PreviewTimelineCurrent(path) && preview.Contact.ReceiveTick>(long)Main.GameUpdateCount-preview.Timeline.SampleTick && preview.WeaponCurrent(combat.Player) && PreviewInputCompatible();
        }
        private bool PreviewTimelineCurrent(NpcTrajectory path)
        {
            var timeline=preview.Timeline;
            return path!=null && timeline!=null && timeline.Identity.Equals(path.Identity) && timeline.CaptureTick==path.CaptureTick &&
                (ReferenceEquals(timeline,path) || ReferenceEquals(previewDisplaySource,path) || timeline.CalculationToken!=null && ReferenceEquals(timeline.CalculationToken,path.CalculationToken));
        }
        internal void BindPreview(AttackContact result){previewPresentation=result;}
        internal void InvalidatePreviewPresentation(){previewLeaseTick=-1;}
        internal bool PreviewCurrent(AttackContact result)
        {
            var player=combat.Player;
            // Draw uses only the preparation's identity and member lease. It
            // neither searches terrain nor acquires a native input capability.
            return result!=null && ReferenceEquals(result,previewPresentation) && ReferenceEquals(result,preview.Contact) && PreviewLeaseCurrent() && preview.PresentationMembersCurrent(player,(int)(Main.GameUpdateCount-previewStep)) && (!preview.Controlled || previewStep==Main.GameUpdateCount && Control.PreviewCurrent(result));
        }
        private bool PreviewInputCompatible()
        {
            // Idle/path-only preparation is an independent read-only hypothesis.
            // A sealed live flight already has its real birth/velocity premise.
            // Neither inherits the hand's currently borrowed cursor permission.
            if(preview.Contact==null || preview.Flight!=null)return true;
            var input=consumedInput;var origin=consumedOrigin;
            bool actual=hasConsumedInput && consumedInputStep==Main.GameUpdateCount && input.Kind==HostAttackInputKind.Basic && ReferenceEquals(input.Weapon,combat.Player.HeldItem) && input.Target.Equals(observation.Selection.Target);
            // A latched execution fault still selects Basic at the next hand
            // consumer between births. An idle/path-only hypothesis has no
            // actual permission and is intentionally independent of that gate.
            if(!actual)
            {
                var item=combat.Player.HeldItem;
                if(!Failed || !BasicPermission || !HostAttackWindow.Ordinary(item))return true;
                var receiver=Main.npc[observation.Selection.Target.Slot];
                input=new HostAttackInput(this,HostAttackInputKind.Basic,receiver.Center+receiver.netOffset,observation.Selection.Target,item,null,HostAttackPhase.CompletedWorld,true,ReferenceEquals(consumedInput.Weapon,item) && consumedInput.TileTarget);
                origin=HostAttackModels.LaunchOrigin(combat.Player,input.Weapon);
            }
            var proposed=new Vector2(preview.Contact.AimX,preview.Contact.AimY);
            if(input.Weapon.shoot<=0 && HostSwingAttack.Handles(input.Weapon))return Math.Sign(proposed.X-origin.X)==Math.Sign(input.Point.X-origin.X);
            if(HostSkyAttack.Handles(input.Weapon.type) || HostMeleeAttack.RawCursor(input.Weapon.type) || input.TileTarget || HostYoyoNavigation.Weapon(input.Weapon))return Vector2.DistanceSquared(proposed,input.Point)<=1;
            // Direction-only consumers may express the same ray at a different
            // distance. Do not display a free intercept requiring another ray.
            var direction=(input.Point-origin).SafeNormalize(Vector2.Zero);
            var hypothetical=(proposed-origin).SafeNormalize(Vector2.Zero);
            return direction!=Vector2.Zero && Vector2.DistanceSquared(direction,hypothetical)<=.000001f;
        }
        private void ClearPreview(){preview.Clear();previewPresentation=null;previewTerrain=null;previewDisplaySource=null;previewLeaseTick=-1;previewWindowMiss=false;}
        internal void ValidatePreviewPresentation()
        {
            previewLeaseTick=-1;
            var path=observation.DisplayPath;
            if(path==null || !PreviewDemand || !CurrentReceiver)return;
            try
            {
                bool members=preview.Contact!=null && preview.PresentationMembersCurrent(combat.Player,(int)(Main.GameUpdateCount-previewStep));
                bool dependencies=previewTerrain!=null && previewTerrain.Current && preview.EnvironmentState.Current && (preview.Environment==null || preview.Environment.Unchanged);
                bool receive=preview.Contact!=null && preview.Timeline!=null && preview.Contact.ReceiveTick>(long)Main.GameUpdateCount-preview.Timeline.SampleTick;
                // Invalid presentation withdraws only its lease. Production is
                // admitted by Host's optional cadence; even a completed null or
                // expired near contact must never solve from this consumer.
                if(PreviewTimelineCurrent(path) && members && dependencies && receive && PreviewInputCompatible())previewLeaseTick=(long)Main.GameUpdateCount;
            }
            catch(CombatPredictionBudgetExceededException){previewLeaseTick=-1;}
            catch(Exception error){PropagateFatal(error);preview.Faults.Record(FaultContextFor(path.Identity));ClearPreview();}
        }
        private void PreparePreview(HostAttackPhase phase)
        {
            try{PreparePreviewCore(phase);}
            finally{EndFlightPreparation();}
        }
        internal void PreparePreviewWindow(NpcTrajectory path)
        {
            try
            {
                using(observation.PredictionWork.Enter(Main.GameUpdateCount))
                {
                    if(path==null || !PreviewDemand || !CurrentReceiver || Permission && ExpectedImpact!=null || !CombatPredictionWork.TryContinue())return;
                    int age=(int)((long)Main.GameUpdateCount-path.SampleTick);NpcTrajectory window;
                    if(age<0 || !path.TryWindow((long)Main.GameUpdateCount,path.Count-age-1,path.Version,out window))return;
                    var context=FaultContextFor(path.Identity);if(preview.Faults.Blocks(context))return;
                    // Only Host's optional scheduler enters here. The aged
                    // immutable suffix retains its capture and calculation
                    // token and is never published to an actual input reader.
                    previewStep=Main.GameUpdateCount;previewPresentation=null;previewDisplaySource=path;
                    PreparePlan(preview,window,HostAttackPhase.CompletedWorld);
                    previewTerrain=preview.Contact==null?null:preview.Terrain.FreezeCells();
                    // A completed miss over this entire available suffix is
                    // not another refresh ticket. A new path/context cycle can
                    // reevaluate it; an interrupted solve does not set this fact.
                    previewWindowMiss=preview.Contact==null && preview.Prepared && !path.BudgetPaused;
                }
            }
            catch(CombatPredictionBudgetExceededException){previewLeaseTick=-1;}
            catch(Exception error){PropagateFatal(error);preview.Faults.Record(FaultContextFor(path.Identity));ClearPreview();}
            finally{EndFlightPreparation();}
        }
        private void PreparePreviewCore(HostAttackPhase phase)
        {
            previewWindowMiss=false;previewPresentation=null;previewStep=Main.GameUpdateCount;
            if(!PreviewDemand || !CurrentReceiver){preview.Clear();return;}
            var timeline=observation.Prediction.Cache.Read(1);
            if(timeline==null || !timeline.Identity.Equals(observation.Selection.Target)){preview.Clear();return;}
            // A candidate owns mutable solve state. Presentation copies only
            // its small result/member snapshot; clearing display never clears
            // a candidate or an actual input plan.
            var selectedPlan=observation.Options.ClearLine?candidatePlans[timeline.Identity.Slot*2]:null;
            if(selectedPlan!=null && ReferenceEquals(selectedPlan,candidatePresentationPlan) && selectedPlan.Prepared && !selectedPlan.Controlled && selectedPlan.Timeline!=null && selectedPlan.Timeline.Identity.Equals(timeline.Identity) && AttackIntercept.Equivalent(selectedPlan.Timeline,timeline))
            {
                preview.CopyPresentation(selectedPlan);preview.Contact=AttackIntercept.Rebind(selectedPlan.Contact,timeline);preview.Timeline=timeline;previewDisplaySource=timeline;
                previewTerrain=preview.Contact==null?null:selectedPlan.Terrain.FreezeCells();return;
            }
            var context=FaultContextFor(timeline.Identity);
            if(preview.Faults.Blocks(context)){preview.Clear();return;}
            try{PreparePlan(preview,timeline,phase);previewDisplaySource=timeline;previewTerrain=preview.Contact==null?null:preview.Terrain.FreezeCells();}
            catch(CombatPredictionBudgetExceededException){preview.Clear();throw;}
            catch(Exception error){PropagateFatal(error);preview.Faults.Record(context);preview.Clear();}
        }
        private AttackContact PreparePlan(PreviewPlan plan,NpcTrajectory timeline,HostAttackPhase phase)
        {
            try{return PreparePlanCore(plan,timeline,phase);}
            catch(CombatPredictionBudgetExceededException)
            {
                // Prepared captures the inputs before the expensive solve.
                // An interrupted null is unresolved, never a memoizable miss.
                // Retire only this plan; Source's completed prefix and the
                // independent actual input owner remain available for retry.
                plan.Clear();throw;
            }
        }
        private AttackContact PreparePlanCore(PreviewPlan plan,NpcTrajectory timeline,HostAttackPhase phase)
        {
            var player=combat.Player;var item=player.HeldItem;
            if(HostPlacementAttack.Handles(item)){plan.Clear();return null;}
            int delay=HostAttackWindow.PreviewDelay(player,item);var clock=new HostAttackClock(timeline,phase);
            if(clock.Age<0 || clock.Age>1){plan.Clear();return null;}
            bool hasFlight;var flight=ReadFlight(player,timeline,phase,out hasFlight);
            // All AI013 hand roles except GolemFist refresh native dummy use
            // timers until catch. Their current flight owns the preview even
            // when it was born before observation was enabled.
            if(hasFlight && item.shoot!=481 && !HostMeleeAttack.KnownSpearParent(item.shoot) && !HostMeleeAttack.KnownWhipParent(item.shoot) && !HostGemStaffAttack.Handles(item.shoot) && !HostAttachedShotAttack.Handles(item.shoot) && !HostArcSwingAttack.Weapon(item.type) && !HostIceRodAttack.Handles(item.shoot) && !HostPaperAirplaneAttack.Handles(item.shoot) && !HostOrnamentAttack.Handles(item.shoot) && !HostInfernoAttack.Handles(item.shoot) && !HostSonicBoomAttack.Handles(item.shoot) && !HostBookStaffAttack.Handles(item.shoot) && !HostSkullAttack.Handles(item.shoot))return OnlyFlight(plan,player,timeline,flight);
            // Inferno's first possible new-body receive is age+delay+1.
            // An earlier live-family contact already wins this read-only
            // comparison; searching every new hand direction cannot improve
            // it. Actual Shoot preparation retains its independent contract.
            if(item.type==1445 && item.shoot==295 && flight!=null && delay>=0 && flight.Contact.Tick<clock.Age+delay+1)
                return OnlyFlight(plan,player,timeline,flight);
            if(delay<0 || clock.Age+delay>=timeline.Count-1)
            {if(!HostSwingAttack.Mixed(item))return OnlyFlight(plan,player,timeline,flight);delay=-1;}
            if(HostHeldAttack.Weapon(item.type) || HostYoyoNavigation.Weapon(item) || HostWhipAttack.Weapon(item))
            {
                plan.SetMembers(player,item);plan.Controlled=true;plan.Timeline=timeline;
                plan.Contact=Control.Preview(timeline,phase,delay);return plan.Contact;
            }
            if(item.type==3473 && Control.HasObservedSolar)
            {
                var live=Control.Preview(timeline,phase,delay,false);
                if(live!=null){plan.Clear();plan.SetMembers(player,item);plan.Controlled=true;plan.Timeline=timeline;plan.Contact=live;return live;}
                // A late native arc may have no remaining receive opportunity.
                // Its existence does not prevent the next legal Solar use:
                // Include its remaining native dummy-timer ownership and the
                // following legal hand window. Never keep the exhausted
                // arc's old contact or hypothesize an immediate new source.
                delay=Math.Max(delay,Control.SolarOpeningDelay);
                if(clock.Age+delay>=timeline.Count-1){plan.Clear();return null;}
            }
            if(Control.HasObservedPulse)
            {
                var live=Control.Preview(timeline,phase,0,false);
                if(live!=null){plan.Clear();plan.SetMembers(player,item);plan.Controlled=true;plan.Timeline=timeline;plan.Contact=live;return live;}
            }
            var captured=AttackAmmoSnapshot.Capture(player,item);
            if(captured==null && !HostSwingAttack.Handles(item))return OnlyFlight(plan,player,timeline,flight);
            var motion=NpcPredictionSource.ReadPlayer(player);var receiver=Main.npc[timeline.Identity.Slot];
            Projectile sample=null;if(captured!=null && !ContentSamples.ProjectilesByType.TryGetValue(captured.Projectile,out sample))return OnlyFlight(plan,player,timeline,flight);
            var receive=sample==null?default(HostAttackReceive):HostAttackReceive.Capture(player,sample,timeline.Identity.Slot,phase==HostAttackPhase.BeforeNpc,phase!=HostAttackPhase.BeforeNpc);
            int immune=receiver.immune[player.whoAmI];
            // Candidate and final display may share the exact same memoized
            // flight observation in one synchronous preparation. Reuse the
            // independently solved HAND plan only in that pass; a sealed
            // flight contact (plan.Flight) is never a hand capability.
            bool sameFlightPass=plan.PreparedTick==Main.GameUpdateCount && plan.HadFlight==hasFlight && ReferenceEquals(plan.PreparedFlight,flight) && FlightPreparationMatches(timeline,phase,flight);
            bool reuse=(!hasFlight || sameFlightPass) && PlanInputsCurrent(plan,player,item,timeline.Identity.Slot,phase,clock.Age,delay,captured,motion,receive,immune) && AttackIntercept.Equivalent(plan.Timeline,timeline);
            if(reuse)
            {
                plan.Contact=AttackIntercept.Rebind(plan.Contact,timeline);plan.Timeline=timeline;return plan.Contact;
            }
            plan.Clear();plan.SetMembers(player,item);plan.Prepared=true;plan.PreparedTick=Main.GameUpdateCount;plan.PreparedFlight=flight;plan.HadFlight=hasFlight;plan.Phase=phase;plan.Age=clock.Age;plan.Delay=delay;plan.PlayerMotion=motion;plan.Immune=immune;plan.AttackCooldown=player.attackCD;plan.MeleeCooldown=player.meleeNPCHitCooldown[timeline.Identity.Slot];plan.Animation=player.itemAnimation;plan.AnimationMax=player.itemAnimationMax;plan.SwingRevision=swing.Revision;plan.MeleeSpeed=player.meleeSpeed;plan.Direction=player.direction;plan.Ammo=captured;plan.Timeline=timeline;
            plan.Receive=receive;plan.Terrain.Reset();
            plan.Using=combat.Left || player.controlUseItem;
            plan.CapsuleOnly=HostSwingAttack.CapsuleOnly(player,item);
            if(HostPhasebladeAttack.Handles(item.shoot))return PreparePhasebladePreview(plan,player,item,timeline,phase,delay);
            if(HostSwingAttack.Mixed(item))
            {
                PrepareMixedSwingPreview(plan,player,item,timeline,phase,delay);
                // A sword's autonomous child can receive before its next
                // hand/shot. Preserve both real clocks without changing use.
                if(flight!=null && Earlier(flight.Contact,plan.Contact)){plan.Contact=flight.Contact;plan.Flight=flight;}
                return plan.Contact;
            }
            if(HostHarpyAttack.Handles(player,captured))
            {
                // This stage owns both cooldown Player updates and the future
                // launch. Passing an already advanced private player would
                // apply motion/charm twice and move the marker off its path.
                plan.Obstacles=new HostAttackObstacles(player,sample,timeline,delay,phase==HostAttackPhase.BeforeNpc,phase!=HostAttackPhase.BeforeNpc);
                plan.Contact=AttackIntercept.WithLaunchDelay(HostHarpyAttack.Solve(player,captured,timeline,HostAttackModels.LaunchOrigin(player,item),new HostAttackClock(timeline,phase,delay),plan.Terrain,plan.Obstacles,out plan.HarpyState),delay);
                return plan.Contact;
            }
            var body=player;
            var playerWorld=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,Remix=Main.remixWorld,Expert=Main.expertMode,WallViewWidth=Main.MaxWorldViewSize.X};
            NpcPredictionSource.TryBindPlayerGravity(timeline,(long)Main.GameUpdateCount,ref motion,ref playerWorld);
            NpcPredictionSource.TryBindPlayerWall(timeline,(long)Main.GameUpdateCount,ref motion,ref playerWorld);
            // Cooldown is a future launch premise. Advance private motion and
            // preserve the live controls, RNG, timers, resources and inventory.
            if(delay>0)
            {
                for(int i=0;i<delay;i++){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,playerWorld,plan.Terrain,out stop))return OnlyFlight(plan,player,timeline,flight);}
                body=(Player)previewCopy.Invoke(player,null);HostAttackPlayerFrame.ApplyPose(body,motion);body.itemTime=0;body.itemAnimation=0;body.reuseDelay=0;
            }
            if(item.type==3852)
            {
                // A delayed burst still owns its stored direction and current
                // animation. Account for the next ItemCheck decrement once;
                // the model subtracts the remaining launch delay itself.
                if(ReferenceEquals(body,player))body=(Player)previewCopy.Invoke(player,null);
                body.itemAnimation=delay<player.itemAnimation-1?player.itemAnimation-1:0;
            }
            int age=clock.Age+delay;Vector2 start=HostAttackModels.LaunchOrigin(body,item);
            if(HostSwingAttack.Handles(item))
            {plan.Contact=AttackIntercept.WithLaunchDelay(swing.PreviewPrepared(body,item,timeline,new HostAttackClock(timeline,phase,delay),plan.Terrain,new HostAttackPlayerFrame(motion,playerWorld)),delay);return plan.Contact;}
            if(phase==HostAttackPhase.CompletedWorld && !HostMeleeAttack.Handles(sample))
            {
                PredictionStop stop;
                if(!PlayerMotionContinuation.Advance(ref motion,playerWorld,plan.Terrain,out stop))return OnlyFlight(plan,player,timeline,flight);
                if(ReferenceEquals(body,player))body=(Player)previewCopy.Invoke(player,null);
                HostAttackPlayerFrame.ApplyPose(body,motion);start=HostAttackModels.LaunchOrigin(body,item);
            }
            plan.Contact=AttackIntercept.WithLaunchDelay(SolveOnTerrain(body,item,captured,timeline,start,age,phase==HostAttackPhase.BeforeNpc,plan.Terrain,delay,out plan.Environment,out plan.Obstacles,new HostAttackPlayerFrame(motion,playerWorld)),delay);
            // GolemFist can legally launch again while an older fist flies.
            // Present the first useful contact of the two real stages.
            if(flight!=null && Earlier(flight.Contact,plan.Contact)){plan.Contact=flight.Contact;plan.Flight=flight;}
            return plan.Contact;
        }
        private bool PlanInputsCurrent(PreviewPlan plan,Player player,Item item,int target,HostAttackPhase phase,int age,int delay,AttackAmmoSnapshot captured,PredictionPlayerMotion motion,HostAttackReceive receive,int immune)
        {
            return plan.Flight==null && plan.Prepared && !plan.Controlled && plan.Phase==phase && plan.Age==age && plan.Delay==delay && plan.MembersCurrent(player) && PlayerMotionContinuation.SameFixedInput(plan.PlayerMotion,motion,plan.Timeline.WallSources!=null) && plan.Immune==immune && plan.AttackCooldown==player.attackCD && plan.MeleeCooldown==player.meleeNPCHitCooldown[target] && plan.Animation==player.itemAnimation && plan.AnimationMax==player.itemAnimationMax && plan.SwingRevision==swing.Revision && plan.CapsuleOnly==HostSwingAttack.CapsuleOnly(player,item) && plan.MeleeSpeed==player.meleeSpeed && plan.Direction==player.direction && plan.Terrain.Unchanged && (plan.Environment==null || plan.Environment.Unchanged) && (plan.Obstacles==null || plan.Obstacles.Unchanged) && SameAmmo(plan.Ammo,captured) && plan.Receive.Same(receive) && plan.Using==(combat.Left || player.controlUseItem) && (plan.HarpyState==null || plan.HarpyState.Current);
        }
        private bool CandidateMissCurrent(PreviewPlan plan,NpcIdentity target,long sampleTick,HostAttackPhase phase,int required)
        {
            if(plan==null || !plan.Prepared || plan.Controlled || plan.Contact!=null || plan.Timeline==null || !plan.Timeline.Identity.Equals(target))return false;
            // A display window has a newer start and an older capture. Its
            // shorter miss cannot authorize a Source proof for a fresh full
            // request. Only the completed requested prefix (or real despawn,
            // which closes the receive future) can carry a miss permission;
            // budget pauses and unknown mechanism stops remain unresolved.
            if(plan.Timeline.SampleTick!=plan.Timeline.CaptureTick || plan.Timeline.BudgetPaused || plan.Timeline.Count-1<required && plan.Timeline.Stop!=PredictionStop.Despawn)return false;
            var player=combat.Player;var item=player.HeldItem;
            // New independent flights are a changed premise even without a
            // new use or inventory change. Their own observation path checks
            // the pool; do not turn an old opening miss into a flight miss.
            if(HostMeleeAttack.KnownSpearParent(item.shoot) || HostMeleeAttack.KnownWhipParent(item.shoot) || HostArcSwingAttack.Weapon(item.type) || HostHarpoonAttack.Handles(item.shoot) || HostGemStaffAttack.Handles(item.shoot) || HostAttachedShotAttack.Handles(item.shoot) || HostPhasebladeAttack.Handles(item.shoot) || Control.HasObservedPulse || item.type==3473 && Control.HasObservedSolar)return false;
            var captured=AttackAmmoSnapshot.Capture(player,item);Projectile sample=null;
            if(captured!=null && !ContentSamples.ProjectilesByType.TryGetValue(captured.Projectile,out sample))return false;
            int age=(int)((long)Main.GameUpdateCount-sampleTick)-(phase==HostAttackPhase.BeforeNpc?1:0);
            var receive=sample==null?default(HostAttackReceive):HostAttackReceive.Capture(player,sample,target.Slot,phase==HostAttackPhase.BeforeNpc,phase!=HostAttackPhase.BeforeNpc);
            return PlanInputsCurrent(plan,player,item,target.Slot,phase,age,HostAttackWindow.PreviewDelay(player,item),captured,NpcPredictionSource.ReadPlayer(player),receive,Main.npc[target.Slot].immune[player.whoAmI]);
        }
        internal static bool SameAmmo(AttackAmmoSnapshot a,AttackAmmoSnapshot b)
        {return a==null?b==null:b!=null && ReferenceEquals(a.Ammo,b.Ammo) && a.Type==b.Type && a.Stack==b.Stack && a.Projectile==b.Projectile && a.Speed==b.Speed && a.Damage==b.Damage && a.GemFeatures==b.GemFeatures && a.Mode==b.Mode && a.Offset==b.Offset && a.CardFireType==b.CardFireType && a.NoteViewAxis==b.NoteViewAxis;}
        private sealed class PreviewPlan
        {
            // Clear retires a fact, not its bounded fault history. Only a new
            // concrete context or explicit feature retry permits another try.
            internal readonly FaultGate Faults=new FaultGate();
            internal readonly PredictionTerrain Terrain=new PredictionTerrain();
            internal AttackContact Contact;internal NpcTrajectory Timeline;internal AttackAmmoSnapshot Ammo;
            internal HostProjectileEnvironment Environment;internal HostProjectileEnvironment.State EnvironmentState;internal HostAttackObstacles Obstacles;
            internal HostHarpyAttack.State HarpyState;
            internal FlightPreview Flight;
            internal FlightPreview PreparedFlight;internal uint PreparedTick;internal bool HadFlight;
            internal HostMeleeAttack.SpearFlightState WhipOwner;
            internal HostAttackReceive Receive;
            internal PredictionPlayerMotion PlayerMotion;internal HostAttackPhase Phase;internal int Age,Delay,Immune,AttackCooldown,MeleeCooldown,Animation,AnimationMax;
            internal int Direction;internal long SwingRevision;internal float MeleeSpeed;internal bool Prepared,Controlled,CapsuleOnly,Using;private Item item;private int slot,type,prefix;private float scale;
            // Every plan kind, including controlled and existing-flight previews,
            // binds the environment used by its solve. Draw only validates it.
            internal void SetMembers(Player player,Item weapon){item=weapon;slot=player.selectedItem;type=weapon.type;prefix=weapon.prefix;scale=player.GetAdjustedItemScale(weapon);Direction=player.direction;EnvironmentState=new HostProjectileEnvironment.State(true,HostProjectileEnvironment.HandWindRelevant(player,weapon));WhipOwner=HostMeleeAttack.KnownWhipParent(weapon.shoot)?new HostMeleeAttack.SpearFlightState(player,System.Array.Empty<Projectile>()):null;}
            internal bool WeaponCurrent(Player player){return player!=null && item!=null && player.selectedItem==slot && ReferenceEquals(player.HeldItem,item) && item.type==type && item.prefix==prefix && player.GetAdjustedItemScale(item)==scale;}
            internal bool MembersCurrent(Player player){return WeaponCurrent(player) && Direction==player.direction && EnvironmentState.Current && (WhipOwner==null || WhipOwner.Current) && (Ammo==null || Ammo.MembersMatch(player,item)) && (Obstacles==null || Obstacles.BirthInputsCurrent) && (HarpyState==null || HarpyState.MembersCurrent) && (Flight==null || Flight.Current);}
            // An aged NPC window does not relax the hand owner's range/arm,
            // duration or captured birth inputs. Validate their existing scalar
            // leases even for an idle hypothesis with no live Flight. Ordinary
            // owner advancement can revoke it; preparation then uses the
            // remaining window again, while Draw never solves or scans terrain.
            internal bool PresentationMembersCurrent(Player player,int age){return age==0?MembersCurrent(player):WeaponCurrent(player) && Direction==player.direction && EnvironmentState.Current && !Controlled && (WhipOwner==null || WhipOwner.Current) && (Ammo==null || Ammo.MembersMatch(player,item)) && (Obstacles==null || Obstacles.BirthInputsCurrent) && (HarpyState==null || HarpyState.MembersCurrent) && (Flight==null || Flight.PresentationCurrent(age));}
            internal void CopyPresentation(PreviewPlan source)
            {
                Clear();Contact=source.Contact;Timeline=source.Timeline;Ammo=source.Ammo;Environment=source.Environment;EnvironmentState=source.EnvironmentState;Obstacles=source.Obstacles;
                HarpyState=source.HarpyState;Flight=source.Flight;WhipOwner=source.WhipOwner;Direction=source.Direction;Controlled=source.Controlled;
                item=source.item;slot=source.slot;type=source.type;prefix=source.prefix;scale=source.scale;
            }
            internal void Clear(){Contact=null;Timeline=null;Ammo=null;Environment=null;Obstacles=null;HarpyState=null;Flight=PreparedFlight=null;WhipOwner=null;item=null;Prepared=Controlled=HadFlight=false;}
        }
    }
}
