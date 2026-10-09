using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed class HostAttackAim
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
        private HostProjectileEnvironment environment;
        private HostProjectileEnvironment.State environmentState;
        private HostAttackObstacles obstacles;
        private readonly PredictionTerrain terrain=new PredictionTerrain();
        private readonly HostSwingAttack swing=new HostSwingAttack();
        private readonly HostCadenceAim cadence;
        private readonly HostAttackCandidates candidates=new HostAttackCandidates();
        private AttackContact presentation;private uint presentationStep;private bool presentationOrdinary;
        private int preferred=4,expansion;
        private Item demandWeapon;private NpcIdentity demandTarget;
        internal bool Failed {get;private set;}
        // Identity continuity alone is not permission to receive an attack.
        // Read only the already selected live receiver; phase displacement
        // never revokes it, but current damage/friendly/death gates do.
        internal bool CurrentReceiver {get{return observation.Selection.HasTarget && CombatSelection.Valid(observation.Selection.Target,observation.Session) && CombatSelection.Receives(Main.npc[observation.Selection.Target.Slot],observation.Options.Dummy);}}
        internal AttackContact ExpectedImpact {get{if(!CurrentReceiver){Clear();return null;}return Valid(false)?(Main.GameUpdateCount==prepared?current:next):Control.ExpectedImpact;}}
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
                Vector2.DistanceSquared(player.RotatedRelativePoint(player.MountedCenter),Main.GameUpdateCount==prepared?origin:nextOrigin)<=4 && environmentState.Current && (environment==null || environment.Unchanged);
        }
        internal HostAttackAim(HostCombat combat,HostCombatObservation observation)
        {
            this.combat=combat;this.observation=observation;Control=new HostAttackControl(combat,observation);
            cadence=new HostCadenceAim(combat,observation);combat.Aim.Provider=cadence.Read;
            combat.Facing.TargetProvider=SharedFacing;combat.Facing.SharedTargetRequired=()=>Permission;
            observation.Selection.CandidateAllowed=CandidateSuitable;
            observation.Selection.BeginCandidates=BeginCandidates;
            observation.Selection.CandidatePriority=CandidatePriority;
        }
        private void BeginCandidates()
        {try{candidates.Begin(combat.Player,Permission && Eligible(combat.Player.HeldItem));}catch(Exception error){FailLocal(error);}}
        private int CandidatePriority(NPC npc)
        {if(!Permission)return 0;try{return candidates.Priority(npc);}catch(Exception error){FailLocal(error);return 0;}}
        private bool CandidateSuitable(NPC npc)
        {
            if(!Permission)return true;var player=combat.Player;var item=player.HeldItem;float range;
            if(HostYoyoNavigation.Weapon(item))
            {range=ProjectileID.Sets.YoyosMaximumRange[item.shoot];if(player.yoyoString)range=range*1.25f+30;range/=(1+player.meleeSpeed*3)/4;}
            else if(HostSwingAttack.Handles(item))range=Math.Max(item.width,item.height)*player.GetAdjustedItemScale(item)+player.width+60;
            else return true;
            // Current cheap reachability uses the receiver's legal edge, never
            // requires its centre. Navigation/contact still proves its own
            // future motor/shape; candidates get no separate world prediction.
            var b=CombatSelection.ReceiveBounds(npc);Vector2 nearest=new Vector2(MathHelper.Clamp(player.Center.X,b.Left,b.Right),MathHelper.Clamp(player.Center.Y,b.Top,b.Bottom));
            return Vector2.DistanceSquared(player.Center,nearest)<=range*range;
        }
        private FacingTarget SharedFacing(Player player)
        {
            if(!ReferenceEquals(player,combat.Player) || !Permission || !observation.Selection.HasTarget || !CombatSelection.Valid(observation.Selection.Target,observation.Session))return null;
            var npc=Main.npc[observation.Selection.Target.Slot];return CombatSelection.Receives(npc,observation.Options.Dummy)?new FacingTarget(npc,observation.Session,true):null;
        }
        internal bool Permission {get{return !Failed && observation.Settings.CanRun && observation.Options.Aim && combat.Player!=null && combat.Admitted(combat.Player) &&
            (combat.Left || combat.Player.controlUseItem || combat.Player.channel || combat.Use.Active || Control.Pending);}}
        internal void Demand()
        {
            if(Permission && Eligible(combat.Player.HeldItem) && HostAttackWindow.NextAction(combat,combat.Player,combat.Player.HeldItem))
            {preferred=Preferred();observation.Prediction.Cache.Demand(1,1,preferred);}
            else{observation.Prediction.Cache.Release(1);expansion=0;Clear();}
        }
        private int Preferred()
        {
            var player=combat.Player;var item=player.HeldItem;var target=observation.Selection.Target;
            if(!ReferenceEquals(item,demandWeapon) || !target.Equals(demandTarget)){expansion=0;demandWeapon=item;demandTarget=target;}
            if(!observation.Selection.HasTarget)return 4;
            if(HostSwingAttack.Handles(item))return Math.Min(120,Math.Max(4,player.itemAnimation>0?player.itemAnimation+1:item.useAnimation+1));
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
        internal void Clear(){presentation=null;weapon=null;ammo=null;current=next=null;environment=null;obstacles=null;cadence.Clear();Control.Clear();}
        internal void Reset(){Clear();swing.Clear();Control.Reset();Failed=false;}
        internal void ObserveSwing(Player player,Item item,Rectangle frame,float offset)
        {
            if(!ReferenceEquals(player,combat.Player) || !HostSwingAttack.Handles(item) || !Permission)return;
            try{swing.Observe(item,frame,offset);PrepareSwing(player,item,observation.Prediction.Cache.Read(1),HostAttackPhase.BeforeNpc,true);}catch(Exception error){FailLocal(error);}
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
        }
        internal void FailLocal(Exception error){Clear();if(error is OutOfMemoryException || error is AccessViolationException)throw error;Failed=true;}
        private void PrepareCore(HostAttackPhase phase)
        {
            bool beforeNpc=phase==HostAttackPhase.BeforeNpc;Clear();if(!Permission || !CurrentReceiver)return;
            terrain.Reset();environmentState=new HostProjectileEnvironment.State(true);
            var player=combat.Player;var item=player.HeldItem;if(!Eligible(item) || !HostAttackWindow.NextAction(combat,player,item))return;
            var timeline=observation.Prediction.Cache.Read(1);if(timeline==null || !timeline.Identity.Equals(observation.Selection.Target))return;
            if(HostSwingAttack.Handles(item)){PrepareSwing(player,item,timeline,phase,false);return;}
            Control.Prepare(timeline,phase);
            var captured=AttackAmmoSnapshot.Capture(player,item);if(captured==null)return;
            cadence.Prepare(player,item,captured,timeline,phase,null);
            if(HostHeldAttack.Weapon(item.type) || HostYoyoNavigation.Weapon(item) || HostWhipAttack.Weapon(item))return; // Controller birth is not its later ordinary/beam damage phase.
            Projectile sample;bool melee=ContentSamples.ProjectilesByType.TryGetValue(captured.Projectile,out sample) && HostMeleeAttack.Handles(sample);
            AttackMotion motion;bool sky=HostSkyAttack.Handles(item.type);if(!sky && !melee && !HostEffectAttack.Handles(captured.Projectile) && !HostAttackModels.TryRead(captured,out motion))return;
            origin=player.RotatedRelativePoint(player.MountedCenter);int age=(int)((long)Main.GameUpdateCount-timeline.SampleTick)-(beforeNpc?1:0);if(age<0 || age>1)return;
            current=Solve(player,item,captured,timeline,origin,age,beforeNpc);
            // A finite preferred prefix can yield no contact. Expand only on
            // later preparation, bounded by the accepted horizon; consumers
            // never pad the future or start another prediction to force a hit.
            if(current==null && preferred<120 && timeline.Count<=preferred+1)expansion=Math.Min(120,expansion+preferred);else if(current!=null)expansion=0;
            cadence.Prepare(player,item,captured,timeline,phase,current);
            var movement=NpcPredictionSource.ReadPlayer(player);PredictionStop stop;
            if(!beforeNpc && PlayerMotionContinuation.Advance(ref movement,new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,Remix=Main.remixWorld},terrain,out stop))
            {
                nextOrigin=origin+new Vector2(movement.X-player.position.X,movement.Y-player.position.Y);
                // CompletedWorldUpdate T precedes player attack T+1, then NPC
                // movement T+1, then projectile AI/move/damage T+1. Solve's
                // first subupdate already reads NPC point age+1. Advancing age
                // here as well would consume point 2 in that first phase.
                next=Solve(player,item,captured,timeline,nextOrigin,age,false);
            }
            weapon=item;slot=player.selectedItem;type=item.type;prefix=item.prefix;session=observation.Session;prepared=Main.GameUpdateCount;ammo=captured;
        }
        private static bool Eligible(Item item){return HostSwingAttack.Handles(item) || item!=null && !item.IsAir && item.shoot>0 && (item.damage>0 || item.type==905) && item.pick==0 && item.axe==0 && item.hammer==0 && (!item.summon || ProjectileID.Sets.IsAWhip[item.shoot]) && !item.sentry;}
        private AttackContact Solve(Player player,Item item,AttackAmmoSnapshot captured,NpcTrajectory timeline,Vector2 start,int age,bool beforeNpc)
        {
            if(HostSkyAttack.Handles(item.type))
            {var sky=ContentSamples.ProjectilesByType[captured.Projectile];obstacles=new HostAttackObstacles(player,sky,timeline,0,beforeNpc,!beforeNpc);return HostSkyAttack.Solve(player,item,captured,timeline,start,age,terrain,beforeNpc,obstacles);}
            if(HostEffectAttack.Handles(captured.Projectile))
            {var effect=ContentSamples.ProjectilesByType[captured.Projectile];obstacles=new HostAttackObstacles(player,effect,timeline,0,beforeNpc,!beforeNpc);return HostEffectAttack.Solve(player,captured,timeline,start,age,beforeNpc,terrain,obstacles);}
            Projectile melee;if(ContentSamples.ProjectilesByType.TryGetValue(captured.Projectile,out melee) && HostMeleeAttack.Handles(melee))return HostMeleeAttack.Solve(player,item,captured,timeline,age,beforeNpc,terrain);
            if(item.type==2624){var b=timeline[Math.Min(age+1,timeline.Count-1)].ProjectileReceiveBounds;start+=(new Vector2(b.CenterX,b.CenterY)-start).SafeNormalize(Vector2.UnitX)*40;}
            Projectile sample;if(!ContentSamples.ProjectilesByType.TryGetValue(captured.Projectile,out sample))return null;
            var receive=HostAttackReceive.Capture(player,sample,timeline.Identity.Slot,beforeNpc,!beforeNpc);
            obstacles=new HostAttackObstacles(player,sample,timeline,0,beforeNpc,!beforeNpc);
            AttackMotion motion;return HostAttackModels.TryRead(captured,out motion)?HostProjectileEnvironment.Solve(sample,terrain,start,motion,timeline,age,0,1,receive.Allows,out environment,obstacles.Pass):null;
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
                Vector2.DistanceSquared(player.RotatedRelativePoint(player.MountedCenter),Main.GameUpdateCount==prepared?origin:nextOrigin)>4)return false;
            return environmentState.Current && terrain.Unchanged && (environment==null || environment.Unchanged) && (obstacles==null || obstacles.Unchanged) && (!checkAmmo || ammo!=null && ammo.Matches(player,weapon));
        }
        internal CombatCursorScope BeginShot(Player player,Item item,bool regular)
        {
            if(!ReferenceEquals(player,combat.Player))return null;
            if(!CurrentReceiver){Clear();return null;}
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
            if(!ReferenceEquals(item,weapon) || !Valid(true)){Clear();return null;}
            var result=Main.GameUpdateCount==prepared?current:next;
            // One prepared ordinary attack is a capability, not a reusable
            // cursor value. Native consumption retires it even if a later
            // refill happens to recreate the same ammo bytes in this step.
            current=next=null;
            return CombatCursorScope.Begin(new Vector2(result.AimX,result.AimY),!HostSkyAttack.RawCursor(type));
        }
    }
}
