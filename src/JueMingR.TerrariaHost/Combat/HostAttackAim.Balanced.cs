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
        private HostAttackSolution currentSolution,nextSolution;
        private bool balancedOrdinary;
        private PredictionPlayerMotion nextBalancedMotion;
        private int BalancedPreferred(Player player,Item item,AttackAmmoSnapshot captured,NPC target,bool futureLaunch)
        {
            int handDelay=futureLaunch?Math.Max(0,HostAttackWindow.PreviewDelay(player,item)):0;
            if(captured!=null && HostSkyAttack.Handles(item.type) && item.type!=3029)
                return Math.Min(NpcPredictionCache.Horizon,HostSkyAttack.PreferredRange(player,item,captured,target)+handDelay);
            if(captured!=null && HostNativeArrowBirthAttack.Weapon(item.type))
                return Math.Min(NpcPredictionCache.Horizon,HostNativeArrowBirthAttack.PreferredRange(player,captured,target)+handDelay);
            if(captured!=null && (HostNorthPoleAttack.Handles(captured.Projectile) || HostSpearShardAttack.Handles(captured.Projectile)))
            {
                // The old spear hint only covered three older child types and
                // returned 16 for 343/1107. Their representative now needs its
                // own actual emission plus flight prefix, without a Contact-
                // driven restart that blindly grows every miss to 120.
                bool north=HostNorthPoleAttack.Handles(captured.Projectile);
                bool newHand=futureLaunch && handDelay>0;
                int maximum=!newHand && player.itemAnimation>0 && player.itemAnimationMax>0?player.itemAnimationMax:HostMeleeAttack.AnimationDuration(player,item);
                int animation=!newHand && player.itemAnimation>0?player.itemAnimation:maximum;
                int emission=north?Math.Max(0,animation-maximum/3+1):0;
                var child=ContentSamples.ProjectilesByType[north?343:1107];
                float velocity=captured.Speed*(north?2.4f:2)*(child.extraUpdates+1);
                float closingSpeed=Math.Max(1,velocity-target.velocity.Length());
                return Math.Min(NpcPredictionCache.Horizon,Math.Max(16,handDelay+emission+(int)Math.Ceiling(Vector2.Distance(player.MountedCenter,target.Center+target.netOffset)/closingSpeed)+16));
            }
            if(BalancedHeld(item))captured=AttackAmmoSnapshot.CaptureController(player,item,item.shoot==705?12:14);
            AttackMotion motion;float speed=captured==null?1:captured.Speed;int updates=1;
            if(captured!=null && HostRepresentativeAttack.TryMotion(captured,out motion)){speed=motion.Speed;updates=motion.Updates;}
            if(item.shoot==705 && player.phantomPhoneixCounter+1>=3)speed=16;
            // Include the actual next launch delay and a modest finite tail.
            // Display's 120-point preference never becomes an attack demand.
            int delay=handDelay;
            if(BalancedHeld(item))delay=Math.Max(delay,Control.BalancedLaunchDelay);
            float closing=Math.Max(1,speed*updates-target.velocity.Length());
            return Math.Min(NpcPredictionCache.Horizon,Math.Max(16,delay+(int)Math.Ceiling(Vector2.Distance(player.MountedCenter,target.Center+target.netOffset)/closing)+16));
        }

        private static bool BalancedHeld(Item item)
        {return item.shoot==615 || item.shoot==630 || item.shoot==705;}
        private static bool BalancedHand(Player player,Item item,AttackAmmoSnapshot captured)
        {
            if(captured==null || HostHeldAttack.Weapon(item.type) || HostYoyoNavigation.Weapon(item) || HostWhipAttack.Weapon(item) || HostDirectionalControl.Weapon(player,item))return false;
            if(HostNativeArrowBirthAttack.Weapon(item.type))return true;
            if(HostSkyAttack.Handles(item.type))return true;
            Projectile body;if(!ContentSamples.ProjectilesByType.TryGetValue(captured.Projectile,out body))return false;
            if(HostNorthPoleAttack.Handles(body.type) || HostSpearShardAttack.Handles(body.type))return true;
            AttackMotion motion;
            if(!HostSkyAttack.Handles(item.type) && !HostAttachedShotAttack.Handles(body.type) &&
                (item.useAmmo==AmmoID.Arrow || item.useAmmo==AmmoID.Bullet) &&
                (body.arrow || HostAmmoSteeringAttack.Handles(body.type) || HostAmmoImpactAttack.Handles(body.type) || HostArrowFamilyAttack.Handles(body.type) || HostSpecialArrowAttack.Handles(body.type)) &&
                HostRepresentativeAttack.TryMotion(captured,out motion))return true;
            // Dedicated remote births, melee motors and major bounce/explosion
            // mechanisms retain their own solver. This replaces the ordinary
            // projectile branch before it captures a receive/ambient root.
            return !HostSkyAttack.Handles(item.type) && !HostAttachedShotAttack.Handles(captured.Projectile) &&
                captured.WeaponType!=4953 && !HostEffectAttack.Handles(captured.Projectile) &&
                !HostMeleeAttack.Handles(body) && HostAttackModels.TryRead(captured,out motion);
        }
        private static HostAttackSolution SolveBalancedHand(Player player,Item item,AttackAmmoSnapshot captured,NpcTrajectory timeline,Vector2 start,HostAttackClock clock,PredictionTerrain cells,out HostAttackObstacles root,bool advanced=false)
        {
            root=null;
            if(HostNativeArrowBirthAttack.Weapon(item.type))return HostNativeArrowBirthAttack.Prepare(player,captured,timeline,start,clock,cells,null,advanced);
            if(HostSkyAttack.Handles(item.type))
            {
                root=new HostAttackObstacles(player,ContentSamples.ProjectilesByType[captured.Projectile],timeline,clock.LaunchDelay,clock.BeforeNpc,clock.NextWorld);
                return HostSkyAttack.Prepare(player,item,captured,timeline,start,clock.Age,cells,clock.BeforeNpc,root,clock.LaunchDelay);
            }
            if(HostNorthPoleAttack.Handles(captured.Projectile))return HostNorthPoleAttack.Prepare(player,item,captured,timeline,clock,cells,HostAttackPlayerFrame.Capture(player,timeline));
            if(HostSpearShardAttack.Handles(captured.Projectile))return HostSpearShardAttack.Prepare(player,item,captured,timeline,clock,cells,HostAttackPlayerFrame.Capture(player,timeline));
            return HostRepresentativeAttack.Prepare(player,captured,timeline,start,clock,cells);
        }
        private void PrepareBalancedOrdinary(Player player,Item item,AttackAmmoSnapshot captured,NpcTrajectory timeline,HostAttackPhase phase)
        {
            origin=HostAttackModels.LaunchOrigin(player,item);
            var clock=new HostAttackClock(timeline,phase);
            if(clock.Age<0 || clock.Age>1)return;
            var plan=candidateHandPlan;candidateHandPlan=null;NpcTrajectory shared;
            if(phase==HostAttackPhase.BeforeNpc && plan!=null && plan.Balanced && plan.PreparedTick==Main.GameUpdateCount && plan.Phase==phase && plan.Delay==0 &&
                plan.Solution.HasPoint && ReferenceEquals(plan.HandOwner,player) && plan.WeaponCurrent(player) && SameAmmo(plan.Ammo,captured) &&
                plan.PlayerMotion.SameSample(NpcPredictionSource.ReadPlayer(player)) && plan.Time==player.itemTime && plan.Reuse==player.reuseDelay &&
                plan.EnvironmentState.Current && plan.MotionTerrain!=null && plan.MotionTerrain.Current && plan.Terrain.Unchanged && TryCandidatePresentationPrefix(plan,timeline,out shared) &&
                Equals(plan.Timeline.GravitySources,shared.GravitySources) && Equals(plan.Timeline.WallSources,shared.WallSources) &&
                (!ReferenceEquals(plan.MotionSource,observation.Prediction) || plan.Timeline.RelationVersion==shared.RelationVersion))
            {
                currentSolution=new HostAttackSolution(shared,plan.Solution.Point,true,plan.Solution.Reachability,plan.Solution.ReceiveTick,AttackIntercept.Rebind(plan.Solution.Contact,shared),plan.Solution.DirectionIndependent,plan.Solution.PointSemantics);
                obstacles=plan.Obstacles;
                handTerrain=plan.Terrain.FreezeCells();handMotionTerrain=plan.MotionTerrain;sharedHandOwner=player;
            }
            else if(phase!=HostAttackPhase.CompletedWorld)currentSolution=SolveBalancedHand(player,item,captured,timeline,origin,clock,terrain,out obstacles);
            current=currentSolution.Contact;
            // World preparation predicts the next native player cursor read.
            // It uses the same NPC future, with only the holder origin advanced.
            if(phase==HostAttackPhase.CompletedWorld)
            {
                var frame=HostAttackPlayerFrame.Capture(player,timeline);var motion=frame.Motion;PredictionStop stop;
                if(PlayerMotionContinuation.Advance(ref motion,frame.Environment,terrain,out stop))
                {
                    nextOrigin=origin+new Vector2(motion.X-player.position.X,motion.Y-player.position.Y);
                    nextBalancedMotion=motion;
                    nextSolution=SolveBalancedHand(player,item,captured,timeline,nextOrigin,clock,terrain,out obstacles,true);next=nextSolution.Contact;currentSolution=nextSolution;current=next;
                }
            }
            weapon=item;slot=player.selectedItem;type=item.type;prefix=item.prefix;session=observation.Session;prepared=Main.GameUpdateCount;ammo=captured;
            balancedOrdinary=preparedOrdinary=true;preparedOrdinaryPhase=phase;preparedMotion=NpcPredictionSource.ReadPlayer(player);preparedDirection=player.direction;
            preparedAnimation=player.itemAnimation;preparedAnimationMax=player.itemAnimationMax;preparedTime=player.itemTime;preparedReuse=player.reuseDelay;preparedScale=player.GetAdjustedItemScale(item);
            cadence.Prepare(player,item,captured,timeline,phase,currentSolution);
        }
        private bool BalancedInputCurrent(bool checkAmmo,bool checkDirection=true)
        {
            var player=combat.Player;var solution=Main.GameUpdateCount==prepared?currentSolution:nextSolution;
            return balancedOrdinary && weapon!=null && Permission && CurrentReceiver && session==observation.Session && Main.GameUpdateCount>=prepared && Main.GameUpdateCount-prepared<=1 &&
                player.selectedItem==slot && ReferenceEquals(player.HeldItem,weapon) && weapon.type==type && weapon.prefix==prefix && ammo!=null &&
                solution.HasPoint && solution.Timeline!=null && solution.Timeline.Identity.Equals(observation.Selection.Target) && TimelineCurrent(solution.Timeline) &&
                // Native Shoot has already admitted this action and may have
                // started its animation. The preparation-only scheduling gate
                // must not reject that same shootsEveryUse consumer afterward.
                (checkAmmo || HostAttackWindow.NextAction(combat,player,weapon)) && (Main.GameUpdateCount==prepared?preparedMotion:nextBalancedMotion).SameSample(NpcPredictionSource.ReadPlayer(player)) &&
                // Pure facing may leave this birth unchanged; physical samples,
                // muzzle, target and freshness still qualify the same solution.
                (!checkDirection || solution.DirectionIndependent || preparedDirection==player.direction) &&
                Vector2.DistanceSquared(HostAttackModels.LaunchOrigin(player,weapon),Main.GameUpdateCount==prepared?origin:nextOrigin)<=4 &&
                environmentState.Current && (handTerrain==null?terrain.Unchanged:handTerrain.Current && handMotionTerrain!=null && handMotionTerrain.Current) &&
                (solution.Contact==null || obstacles==null || (checkAmmo?obstacles.UnchangedForHandStart:obstacles.Unchanged)) && (checkAmmo?ammo.Matches(player,weapon):ammo.IdentityMatches(player,weapon));
        }
        private bool PrepareBalancedPlan(PreviewPlan plan,NpcTrajectory timeline,HostAttackPhase phase)
        {
            var player=combat.Player;var item=player.HeldItem;var captured=AttackAmmoSnapshot.Capture(player,item);
            if(!BalancedHeld(item) && !BalancedHand(player,item,captured))return false;
            int delay=HostAttackWindow.PreviewDelay(player,item);
            plan.Clear();plan.SetMembers(player,item);plan.Prepared=true;plan.Balanced=true;plan.PreparedTick=Main.GameUpdateCount;plan.Phase=phase;plan.Timeline=timeline;plan.Ammo=captured;plan.Delay=delay;
            if(delay<0)return true;
            plan.PlayerMotion=NpcPredictionSource.ReadPlayer(player);plan.Time=player.itemTime;plan.Reuse=player.reuseDelay;plan.Terrain.Reset();
            if(BalancedHeld(item))plan.Solution=Control.PrepareBalancedPreview(timeline,phase,delay,plan.Terrain);
            else
            {
                var body=player;var frame=HostAttackPlayerFrame.Capture(player,timeline);var motion=frame.Motion;
                int advance=delay+(phase==HostAttackPhase.CompletedWorld && !HostNorthPoleAttack.Handles(captured.Projectile) && !HostSpearShardAttack.Handles(captured.Projectile)?1:0);
                for(int i=0;i<advance;i++){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,frame.Environment,plan.Terrain,out stop))return true;}
                if(advance>0){body=(Player)previewCopy.Invoke(player,null);HostAttackPlayerFrame.ApplyPose(body,motion);}
                if(delay>0)
                {
                    // PreviewDelay denotes a new hand after cooldown. Retaining
                    // the old spear animation would emit its child too early.
                    // Eventide alone may be the next shot INSIDE this animation;
                    // its birth adapter advances that original timer once.
                    body.itemTime=0;body.itemAnimation=0;body.reuseDelay=0;
                    if(item.type==4953 && player.itemAnimation>delay+(phase==HostAttackPhase.CompletedWorld?1:0))
                    {body.itemAnimation=player.itemAnimation;body.itemAnimationMax=player.itemAnimationMax;}
                }
                plan.Solution=SolveBalancedHand(body,item,captured,timeline,HostAttackModels.LaunchOrigin(body,item),new HostAttackClock(timeline,phase,delay),plan.Terrain,out plan.Obstacles,advance>0);
            }
            plan.Balanced=true;plan.Contact=plan.Solution.Contact;return true;
        }
        private bool BalancedWeapon
        {get{var p=combat.Player;return p!=null && (BalancedHeld(p.HeldItem) || BalancedHand(p,p.HeldItem,AttackAmmoSnapshot.Capture(p,p.HeldItem)));}}
    }
}
