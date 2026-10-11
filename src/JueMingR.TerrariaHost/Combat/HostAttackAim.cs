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
        private readonly HostSwingAttack swing=new HostSwingAttack();
        private readonly HostCadenceAim cadence;
        private readonly HostAttackCandidates candidates=new HostAttackCandidates();
        private AttackContact presentation;private uint presentationStep;private bool presentationOrdinary;
        private int preferred=4,expansion;
        private Item demandWeapon;private NpcIdentity demandTarget;
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
            if(result==null || !ReferenceEquals(presentation,result) || presentationStep!=Main.GameUpdateCount || Failed || !observation.Options.Aim || !observation.Settings.CanRun || player==null || !combat.Admitted(player) || !observation.Selection.HasTarget || !result.Timeline.Identity.Equals(observation.Selection.Target) || !CombatSelection.Valid(observation.Selection.Target,observation.Session) || !ReferenceEquals(result.Timeline,observation.Prediction.Cache.Read(1)))return false;
            if(!presentationOrdinary)return Control.PresentationCurrent(result);
            return (combat.Left || player.controlUseItem || player.channel || combat.Use.Active) && session==observation.Session && player.selectedItem==slot && ReferenceEquals(player.HeldItem,weapon) && weapon.type==type && weapon.prefix==prefix &&
                ReferenceEquals(result,Main.GameUpdateCount==prepared?current:next) && HostAttackWindow.NextAction(combat,player,weapon) && (ammo==null?HostSwingAttack.Handles(weapon):ammo.MembersMatch(player,weapon)) &&
                Vector2.DistanceSquared(HostAttackModels.LaunchOrigin(player,weapon),Main.GameUpdateCount==prepared?origin:nextOrigin)<=4 && environmentState.Current && (environment==null || environment.Unchanged) && (obstacles==null || obstacles.BirthInputsCurrent) && (harpyState==null || harpyState.MembersCurrent);
        }
        internal HostAttackAim(HostCombat combat,HostCombatObservation observation)
        {
            this.combat=combat;this.observation=observation;Control=new HostAttackControl(combat,observation);
            cadence=new HostCadenceAim(combat,observation);combat.Aim.Provider=cadence.Read;
            combat.Facing.TargetProvider=SharedFacing;combat.Facing.SharedTargetRequired=()=>Permission;
        }
        private FacingTarget SharedFacing(Player player)
        {
            if(!ReferenceEquals(player,combat.Player) || !Permission || !observation.Selection.HasTarget || !CombatSelection.Valid(observation.Selection.Target,observation.Session))return null;
            var npc=Main.npc[observation.Selection.Target.Slot];return CombatSelection.Receives(npc,observation.Options.Dummy)?new FacingTarget(npc,observation.Session,true):null;
        }
        internal bool Permission {get{return !Failed && observation.Settings.CanRun && observation.Options.Aim && combat.Player!=null && combat.Admitted(combat.Player) &&
            (combat.Left || combat.Player.controlUseItem || combat.Player.channel || combat.Use.Active || Control.Pending || HostPhasebladeAttack.Handles(combat.Player.HeldItem.shoot) && combat.Player.itemAnimation>0);}}
        internal void Demand()
        {
            if((PreviewDemand || Permission && HostAttackWindow.NextAction(combat,combat.Player,combat.Player.HeldItem)) && Eligible(combat.Player.HeldItem) && !HostPlacementAttack.Handles(combat.Player.HeldItem))
            {preferred=Preferred();observation.Prediction.Cache.Demand(1,1,preferred);}
            else{observation.Prediction.Cache.Release(1);expansion=0;Clear();}
        }
        private int Preferred()
        {
            var player=combat.Player;var item=player.HeldItem;var target=observation.Selection.Target;
            if(!ReferenceEquals(item,demandWeapon) || !target.Equals(demandTarget)){expansion=0;demandWeapon=item;demandTarget=target;}
            if(!observation.Selection.HasTarget)return 4;
            if(HostPhasebladeAttack.Handles(item.shoot))return NpcPredictionCache.Horizon;
            if(HostSwingAttack.Handles(item) && item.shoot<=0)return Math.Min(120,Math.Max(4,player.itemAnimation>0?player.itemAnimation+1:item.useAnimation+1));
            if(item.type==3541)return 32;
            if(HostAttackWindow.Ordinary(item))
            {
                var captured=AttackAmmoSnapshot.Capture(player,item);AttackMotion motion;
                if(captured!=null && HostAttackModels.TryRead(captured,out motion) && motion.Gravity==0 && motion.Acceleration==1)
                {
                    var npc=Main.npc[target.Slot];float distance=Vector2.Distance(player.MountedCenter,npc.Center+npc.netOffset);
                    float closing=Math.Max(1,motion.Speed*motion.Updates-npc.velocity.Length());
                    return Math.Min(120,Math.Max(16,(int)Math.Ceiling(distance/closing)+8+expansion));
                }
            }
            return NpcPredictionCache.Horizon;
        }
        internal void ClearExecution(){presentation=null;weapon=null;ammo=null;current=next=null;environment=null;obstacles=null;harpyState=null;preparedOrdinary=false;cadence.Clear();Control.Clear();}
        internal void Clear(){ClearExecution();ClearPreview();}
        internal void Reset(){Clear();ClearCandidatePlans();RetryFaults();swing.Clear();Control.Reset();}
        internal void ObserveSwing(Player player,Item item,Rectangle frame,float offset)
        {
            if(!ReferenceEquals(player,combat.Player) || !HostSwingAttack.Handles(item) || !PreviewDemand)return;
            try{swing.Observe(item,frame,offset);if(Permission && player.itemAnimation>0 && !HostSwingAttack.Mixed(item) && (!HostPhasebladeAttack.Handles(item.shoot) || combat.Left || player.controlUseItem))PrepareSwing(player,item,observation.Prediction.Cache.Read(1),HostAttackPhase.BeforeNpc,true);}catch(Exception error){FailLocal(error);}
        }
        private void PrepareSwing(Player player,Item item,NpcTrajectory timeline,HostAttackPhase phase,bool naturalPose)
        {
            environmentState=new HostProjectileEnvironment.State(true);
            if(timeline==null || !timeline.Identity.Equals(observation.Selection.Target))return;
            current=swing.Solve(player,item,timeline,new HostAttackClock(timeline,phase),terrain,naturalPose);next=null;ammo=null;
            weapon=item;slot=player.selectedItem;type=item.type;prefix=item.prefix;session=observation.Session;prepared=Main.GameUpdateCount;origin=player.RotatedRelativePoint(player.MountedCenter);
        }
        internal void PrepareNatural(){observation.PrepareAction();}
        internal void Prepare()
        {PreparePhase(HostAttackPhase.CompletedWorld);}
        internal void PrepareAction()
        {PreparePhase(HostAttackPhase.BeforeNpc);}
        internal void PrepareProjectiles()
        {PreparePhase(HostAttackPhase.Projectiles);}
        private void PreparePhase(HostAttackPhase phase)
        {
            try{PrepareCore(phase);}
            catch(Exception error){FailLocal(error);}
            if(phase==HostAttackPhase.CompletedWorld)PreparePreview(phase);
        }
        internal void FailLocal(Exception error){PropagateFatal(error);executionFaults.Record(FaultContextFor(observation.Selection.Target));Clear();}
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
            terrain.Reset();environmentState=new HostProjectileEnvironment.State(true);
            var player=combat.Player;var item=player.HeldItem;if(!Eligible(item) || !HostAttackWindow.NextAction(combat,player,item))return;
            if(HostPlacementAttack.Handles(item))return; // Placement has its own native use receipt, not a damage contact.
            var timeline=observation.Prediction.Cache.Read(1);if(timeline==null || !timeline.Identity.Equals(observation.Selection.Target))return;
            if(HostPhasebladeAttack.Handles(item.shoot))
            {
                if(combat.Left || player.controlUseItem)PrepareSwing(player,item,timeline,phase,false);
                else PreparePhasebladeRelease(player,item,timeline,phase);
                return;
            }
            if(HostSwingAttack.Handles(item) && item.shoot<=0){PrepareSwing(player,item,timeline,phase,false);return;}
            Control.Prepare(timeline,phase);
            var captured=AttackAmmoSnapshot.Capture(player,item);if(captured==null)return;
            cadence.Prepare(player,item,captured,timeline,phase,null);
            if(HostHeldAttack.Weapon(item.type) || HostYoyoNavigation.Weapon(item) || HostWhipAttack.Weapon(item))return; // Controller birth is not its later ordinary/beam damage phase.
            Projectile sample;bool melee=ContentSamples.ProjectilesByType.TryGetValue(captured.Projectile,out sample) && HostMeleeAttack.Handles(sample);
            AttackMotion motion;bool sky=HostSkyAttack.Handles(item.type);if(!sky && !melee && !HostEffectAttack.Handles(captured.Projectile) && !HostAttackModels.TryRead(captured,out motion))return;
            origin=HostAttackModels.LaunchOrigin(player,item);int age=(int)((long)Main.GameUpdateCount-timeline.SampleTick)-(beforeNpc?1:0);if(age<0 || age>1)return;
            bool harpy=HostHarpyAttack.Handles(player,captured);
            if(harpy)
            {
                // Charm runs in Player before ItemCheck, then each arrow runs
                // after NPC movement. Its solver owns that whole phase prefix,
                // including other live arrows competing for the charm.
                obstacles=new HostAttackObstacles(player,sample,timeline,0,beforeNpc,!beforeNpc);
                current=HostHarpyAttack.Solve(player,captured,timeline,origin,new HostAttackClock(timeline,phase),terrain,obstacles,out harpyState);
            }
            else current=Solve(player,item,captured,timeline,origin,age,beforeNpc);
            // A finite preferred prefix can yield no contact. Expand only on
            // later preparation, bounded by the accepted horizon; consumers
            // never pad the future or start another prediction to force a hit.
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
                next=Solve(player,item,captured,timeline,nextOrigin,age,false);
            }
            weapon=item;slot=player.selectedItem;type=item.type;prefix=item.prefix;session=observation.Session;prepared=Main.GameUpdateCount;ammo=captured;
            preparedOrdinary=true;preparedOrdinaryPhase=phase;preparedMotion=NpcPredictionSource.ReadPlayer(player);preparedDirection=player.direction;preparedAnimation=player.itemAnimation;preparedAnimationMax=player.itemAnimationMax;preparedTime=player.itemTime;preparedReuse=player.reuseDelay;preparedImmune=Main.npc[timeline.Identity.Slot].immune[player.whoAmI];
            preparedAttackCooldown=player.attackCD;preparedMeleeCooldown=player.meleeNPCHitCooldown[timeline.Identity.Slot];preparedMeleeSpeed=player.meleeSpeed;preparedScale=player.GetAdjustedItemScale(item);
        }
        private static bool Eligible(Item item){return HostPlacementAttack.Handles(item) || HostSwingAttack.Handles(item) || item!=null && !item.IsAir && item.useStyle>0 && item.useTime>0 && item.shoot>0 && (item.damage>0 || item.type==905) && item.pick==0 && (item.axe==0 || item.type==5519 && item.shoot==1052) && item.hammer==0 && (!item.summon || ProjectileID.Sets.IsAWhip[item.shoot]) && !item.sentry;}
        private AttackContact Solve(Player player,Item item,AttackAmmoSnapshot captured,NpcTrajectory timeline,Vector2 start,int age,bool beforeNpc)
        {return SolveOnTerrain(player,item,captured,timeline,start,age,beforeNpc,terrain,0,out environment,out obstacles);}
        private static AttackContact SolveOnTerrain(Player player,Item item,AttackAmmoSnapshot captured,NpcTrajectory timeline,Vector2 start,int age,bool beforeNpc,PredictionTerrain terrain,int delay,out HostProjectileEnvironment environment,out HostAttackObstacles obstacles,HostAttackPlayerFrame? preparedPlayer=null)
        {
            environment=null;obstacles=null;
            if(HostSkyAttack.Handles(item.type))
            {var sky=ContentSamples.ProjectilesByType[captured.Projectile];obstacles=new HostAttackObstacles(player,sky,timeline,delay,beforeNpc,!beforeNpc);return HostSkyAttack.Solve(player,item,captured,timeline,start,age,terrain,beforeNpc,obstacles,delay);}
            if(HostAttachedShotAttack.Handles(captured.Projectile))
            {var attached=ContentSamples.ProjectilesByType[captured.Projectile];obstacles=new HostAttackObstacles(player,attached,timeline,delay,beforeNpc,!beforeNpc);return HostAttachedShotAttack.Solve(player,captured,timeline,start,age,beforeNpc,terrain,obstacles,delay,preparedPlayer);}
            if(HostEffectAttack.Handles(captured.Projectile))
            {var effect=ContentSamples.ProjectilesByType[captured.Projectile];obstacles=new HostAttackObstacles(player,effect,timeline,delay,beforeNpc,!beforeNpc);return HostEffectAttack.Solve(player,captured,timeline,start,age,beforeNpc,terrain,obstacles,delay,preparedPlayer);}
            Projectile melee;if(ContentSamples.ProjectilesByType.TryGetValue(captured.Projectile,out melee) && HostMeleeAttack.Handles(melee))
            {
                // A real intervening boomerang hit can enter its return phase.
                // Lease those receivers with both execution and idle preview;
                // anchored sword/spear poses do not need this extra scan.
                if(HostBoomerangAttack.Handles(melee.type) || HostArcSwingAttack.Weapon(item.type))obstacles=new HostAttackObstacles(player,melee,timeline,delay,beforeNpc,!beforeNpc);
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
        {
            var player=combat.Player;
            if(weapon==null || !Permission || session!=observation.Session || Main.GameUpdateCount<prepared || Main.GameUpdateCount-prepared>1 ||
                player.selectedItem!=slot || !ReferenceEquals(player.HeldItem,weapon) || weapon.type!=type || weapon.prefix!=prefix ||
                !CurrentReceiver)return false;
            if(ammo==null?!HostSwingAttack.Handles(weapon):!ammo.IdentityMatches(player,weapon))return false;
            var result=Main.GameUpdateCount==prepared?current:next;
            if(!checkAmmo && !HostAttackWindow.NextAction(combat,player,weapon))return false;
            if(result==null || !result.Timeline.Identity.Equals(observation.Selection.Target) || !ReferenceEquals(result.Timeline,observation.Prediction.Cache.Read(1)) ||
                Vector2.DistanceSquared(HostAttackModels.LaunchOrigin(player,weapon),Main.GameUpdateCount==prepared?origin:nextOrigin)>4)return false;
            return environmentState.Current && terrain.Unchanged && (environment==null || environment.Unchanged) && (obstacles==null || (checkAmmo?obstacles.UnchangedForHandStart:obstacles.Unchanged)) && (harpyState==null || harpyState.Current) && (!checkAmmo || ammo!=null && ammo.Matches(player,weapon));
        }
        internal CombatCursorScope BeginShot(Player player,Item item,bool regular)
        {
            if(!ReferenceEquals(player,combat.Player))return null;
            if(!CurrentReceiver){ClearExecution();return null;}
            if(HostPlacementAttack.Handles(item))return regular?BeginPlacement(player,item,true):null;
            if(!regular && item!=null && ProjectileID.Sets.IsAWhip[item.shoot])
            {
                // Snake-band extra swings are real consumers, with random
                // direction and a different duration/role. They may borrow a
                // representative input, never a primary's precise contact.
                current=next=null;var timeline=observation.Prediction.Cache.Read(1);
                if(!Permission || !ReferenceEquals(item,player.HeldItem) || timeline==null || !timeline.Identity.Equals(observation.Selection.Target))return null;
                Vector2 point;terrain.Reset();return HostWhipAttack.TryExtraPoint(player,timeline,new HostAttackClock(timeline,HostAttackPhase.BeforeNpc),terrain,out point)?CombatCursorScope.Begin(point):null;
            }
            if(HostHeldAttack.Weapon(item.type) || HostYoyoNavigation.Weapon(item) || HostWhipAttack.Weapon(item))return Control.BeginOpening(player,item);
            // These native births establish G11A's real charge controller.
            // Their Shoot does not consume its later AI direction packet.
            if(item.type==5462 || item.type==6153)return null;
            if(!ReferenceEquals(item,weapon) || !Valid(true)){ClearExecution();return null;}
            var result=Main.GameUpdateCount==prepared?current:next;
            // One prepared ordinary attack is a capability, not a reusable
            // cursor value. Native consumption retires it even if a later
            // refill happens to recreate the same ammo bytes in this step.
            current=next=null;
            var aimPoint=new Vector2(result.AimX,result.AimY);bool reverse=!HostSkyAttack.RawCursor(type) && !HostMeleeAttack.RawCursor(type);
            return ammo!=null && (ammo.Projectile==442 || ammo.Projectile==80)?CombatCursorScope.BeginTileTarget(aimPoint,reverse):CombatCursorScope.Begin(aimPoint,reverse);
        }
    }
}
