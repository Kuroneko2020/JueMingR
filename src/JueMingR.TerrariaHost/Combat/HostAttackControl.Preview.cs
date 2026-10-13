using System;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackControl
    {
        private static readonly MethodInfo previewCopy=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);
        private readonly PredictionTerrain previewTerrain=new PredictionTerrain();
        private AttackContact previewContact;
        private Controlled previewSource;
        private HostBeamAttack.Child previewChild;
        private AttackAmmoSnapshot previewAmmo;
        private Item previewWeapon;
        private int previewSlot,previewType,previewPrefix;
        private uint previewTick;
        private long previewSession,previewSelection;
        private Vector2 previewCenter,previewVelocity;
        private float previewAi0,previewAi1,previewAi2;
        private HostProjectileEnvironment.State previewEnvironment;
        private HostAttackObstacles previewObstacles;
        private IdlePreview idlePreview;
        private PreviewDependencies previewDependencies;

        // A contact keeps the read set of its actual solver. The navigation,
        // controller and private opening own different terrain readers; later
        // Reset/another weapon must not replace this result's dependency lease.
        internal sealed class PreviewDependencies
        {
            private readonly PredictionTerrain.CellLease terrain;
            private readonly HostYoyoNavigation.PresentationDependencies navigation;
            private readonly HostAttackObstacles obstacles;
            private readonly HostProjectileEnvironment.State environment;
            internal PreviewDependencies(PredictionTerrain terrain,HostYoyoNavigation navigation,HostAttackObstacles obstacles,HostProjectileEnvironment.State environment,Player openingInput=null)
            {this.terrain=terrain.FreezeCells();this.navigation=navigation?.FreezePresentation(openingInput);this.obstacles=obstacles;this.environment=environment;}
            internal bool Current(Player player){return terrain.Current && (navigation==null || navigation.Current(player)) && environment.Current && (obstacles==null || obstacles.Unchanged);}
        }
        internal PreviewDependencies CapturePreviewDependencies(AttackContact contact)
        {return contact!=null && ReferenceEquals(contact,previewContact)?previewDependencies:null;}

        internal void ClearPreview(){previewContact=null;previewSource=null;previewChild=null;previewAmmo=null;previewWeapon=null;previewObstacles=null;previewDependencies=null;idlePreview=null;}

        // This result has no Source or cursor capability. Existing native
        // controllers are observed through their real birth receipt; an idle
        // opening uses private ContentSamples copies and never enters the pool.
        internal AttackContact Preview(NpcTrajectory timeline,HostAttackPhase phase,int delay,bool allowOpening=true)
        {
            if(allowOpening && TryReuseIdle(timeline,phase,delay))return previewContact;
            ClearPreview();var player=combat.Player;
            // Clear-line candidate plans are independent readers of live controls.
            // The retired display owner cannot disable their original qualification.
            if(timeline==null || player==null || !combat.Attack.AttackObservationDemand)return null;
            previewTerrain.Reset();previewEnvironment=new HostProjectileEnvironment.State(true);
            previewWeapon=player.HeldItem;previewSlot=player.selectedItem;previewType=previewWeapon.type;previewPrefix=previewWeapon.prefix;
            previewTick=Main.GameUpdateCount;previewSession=observation.Session;previewSelection=combat.Tools.SelectionIntent;
            bool live=false;retired.Clear();
            foreach(var pair in shots)
            {
                var entry=pair.Value;if(!Observed(entry)){retired.Add(pair.Key);continue;}if(!HostPulseAttack.Handles(entry.Type))live=true;
                // This same phase has already solved the real action. Keep
                // its immutable contact as an observation, not its consumable
                // capability; a later Clear cannot erase this preview fact.
                if(ReferenceEquals(preparedTimeline,timeline) && preparedPhase==phase && entry.Prepared==Main.GameUpdateCount && entry.Contact!=null && entry.Type!=444 && entry.Type!=1035)
                {BindPreview(entry.Contact,entry,entry.Ammo,entry.ContactChild);previewObstacles=entry.Obstacles;previewDependencies=new PreviewDependencies(terrain,entry.Navigation,entry.Obstacles,environmentState);break;}
                var view=new Controlled{Shot=entry.Shot,Key=entry.Key,Type=entry.Type,Source=entry.Source,Born=entry.Born,BornNext=entry.BornNext,Children=entry.Children};
                if(entry.Navigation!=null)view.Navigation=entry.PreviewNavigation??(entry.PreviewNavigation=new HostYoyoNavigation());
                var clock=new HostAttackClock(timeline,phase);
                if(entry.Type==611)view.Contact=HostMeleeAttack.KnownSolar(player,entry.Shot,timeline,clock,previewTerrain);
                else if(entry.Type==444)view.Contact=BubblePreview(player,entry.Shot,timeline,clock,false);
                else if(entry.Type==1035)view.Contact=HostWhipAttack.Contact(player,entry.Shot,timeline,clock,previewTerrain,out view.Point);
                else SolveEntry(view,timeline,phase,previewTerrain,player);
                if(view.Contact==null)continue;
                BindPreview(view.Contact,entry,view.Ammo,view.ContactChild);
                // BubblePreview supplies its child obstacle owner directly.
                if(entry.Type!=444)previewObstacles=view.Obstacles;
                previewDependencies=new PreviewDependencies(previewTerrain,view.Navigation,previewObstacles,previewEnvironment);break;
            }
            foreach(int key in retired)shots.Remove(key);
            // A current charge, bubble or release owns its stage even when it
            // has no contact. Do not replace it with a fictitious new opening.
            if(live || previewContact!=null || !allowOpening)return previewContact;
            if(!HostAttackResources.Initial(player,previewWeapon))return null;
            delay=Math.Max(delay,Math.Max(Math.Max(0,player.itemTime-1),Math.Max(0,player.reuseDelay)));
            var launchClock=new HostAttackClock(timeline,phase,delay);
            if(launchClock.Age<0 || launchClock.FirstTick>=timeline.Count)return null;
            previewAmmo=AttackAmmoSnapshot.Capture(player,previewWeapon);if(previewAmmo==null)return null;
            var body=(Player)previewCopy.Invoke(player,null);body.channel=true;
            var motion=NpcPredictionSource.ReadPlayer(player);var environment=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,Remix=Main.remixWorld,Expert=Main.expertMode,WallViewWidth=Main.MaxWorldViewSize.X};
            NpcPredictionSource.TryBindPlayerGravity(timeline,(long)Main.GameUpdateCount,ref motion,ref environment);
            NpcPredictionSource.TryBindPlayerWall(timeline,(long)Main.GameUpdateCount,ref motion,ref environment);
            for(int i=0;i<delay;i++){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,environment,previewTerrain,out stop))return null;}
            HostAttackPlayerFrame.ApplyPose(body,motion);var playerFrame=new HostAttackPlayerFrame(motion,environment);
            int type=previewType==2797?444:previewType==3475?615:previewType==3540?630:previewType==3854?705:previewType==3930?714:previewWeapon.shoot;
            // An unobserved already-live controller is not an idle opening.
            // Native counts may deny this hypothesis; they never authorize
            // adopting a projectile whose causal source we did not observe.
            if(OpeningBlocked(player,type))return null;
            Projectile shot=CopyProjectile(type,player.whoAmI);if(shot==null)return null;
            var aim=HostHeldAttack.OpeningPoint(body,previewWeapon,previewAmmo,timeline);
            Vector2 mounted=body.RotatedRelativePoint(body.MountedCenter),unit=(aim-mounted).SafeNormalize(Vector2.UnitX*body.direction);
            shot.Center=mounted;shot.velocity=unit*previewAmmo.Speed;
            var openingView=new Controlled{Shot=shot,Key=(int)shot.key,Type=type};
            if(HostYoyoNavigation.Handles(shot))
            {
                Vector2 waypoint;HostYoyoNavigation navigation;
                if(!HostYoyoNavigation.OpeningPoint(body,previewWeapon,timeline,out waypoint,out navigation))return null;
                shot.velocity=(waypoint-mounted).SafeNormalize(unit)*16;openingView.Navigation=navigation;
            }
            if(type==444)
            {
                // Native bubble births have random spread and 5..25% speed.
                // The middle member is a representative, not a random draw.
                bool clear;PredictionStop stop;Vector2 muzzle=mounted+unit*40*previewWeapon.scale;
                if(previewTerrain.CanHit(new MotionRect(mounted.X,mounted.Y,1,1),new MotionRect(muzzle.X,muzzle.Y,1,1),out clear,out stop) && clear)shot.Center=muzzle;
                shot.velocity*=.15f;shot.ai[0]=unit.ToRotation();shot.localAI[0]=previewAmmo.Projectile;shot.localAI[1]=previewAmmo.Speed;shot.timeLeft=60;
                previewContact=BubblePreview(body,shot,timeline,launchClock,true);
            }
            else if(type==1035)previewContact=HostWhipAttack.ContactPrepared(body,null,timeline,launchClock,previewTerrain,out aim,playerFrame);
            else
            {
                if(type==927)shot.ai[1]=body.GetAdjustedItemScale(previewWeapon)*Utils.Remap(body.meleeSpeed,1,1f/3,1,2);
                if(type==633 || type==460)
                {
                    shot.key=(Terraria.DataStructures.ProjectileKey)0f;
                    for(int i=0;i<(type==633?6:1);i++)
                    {var child=CopyProjectile(type==633?632:461,body.whoAmI);child.ai[0]=i;child.ai[1]=0;openingView.Children.Add(new HostBeamAttack.Child(child));}
                }
                SolveEntry(openingView,timeline,phase,previewTerrain,body,delay,playerFrame);previewContact=openingView.Contact;previewObstacles=openingView.Obstacles;
            }
            previewContact=AttackIntercept.WithLaunchDelay(previewContact,delay);
            if(previewContact!=null)previewDependencies=new PreviewDependencies(previewTerrain,openingView.Navigation,previewObstacles,previewEnvironment,player);
            if(CacheableOpening(player,type,delay))idlePreview=new IdlePreview(player,type,timeline,phase,launchClock.Age,previewAmmo);
            return previewContact;
        }

        // Only an idle opening may survive an equivalent new sample. A real
        // controller always owns its evolving charge/release state. The cache
        // retains the exact terrain and receiver dependencies of that opening;
        // rebinding moves the fact to the visible path's new time origin.
        private bool TryReuseIdle(NpcTrajectory timeline,HostAttackPhase phase,int delay)
        {
            var p=combat.Player;var prior=idlePreview;
            if(prior==null || p==null || timeline==null || !combat.Attack.AttackObservationDemand || !CacheableOpening(p,prior.Projectile,delay) ||
                previewSession!=observation.Session || previewSelection!=combat.Tools.SelectionIntent || p.selectedItem!=previewSlot || !ReferenceEquals(p.HeldItem,previewWeapon) || previewWeapon.type!=previewType || previewWeapon.prefix!=previewPrefix ||
                prior.Phase!=phase || prior.Age!=new HostAttackClock(timeline,phase,delay).Age || !prior.Current(p,timeline) || !HostAttackResources.Initial(p,p.HeldItem))return false;
            foreach(var entry in shots.Values)if(Observed(entry))return false;
            var captured=AttackAmmoSnapshot.Capture(p,p.HeldItem);
            if(!HostAttackAim.SameAmmo(previewAmmo,captured) || previewDependencies==null || !previewDependencies.Current(p) || !AttackIntercept.Equivalent(prior.Timeline,timeline))return false;
            previewContact=AttackIntercept.Rebind(previewContact,timeline);prior.Timeline=timeline;previewTick=Main.GameUpdateCount;return true;
        }
        private static bool CacheableOpening(Player p,int projectile,int delay)
        {
            // These motors consume only the captured holder/ammo state. A
            // navigator, guided cursor or swinging body has its own dependency
            // contract and is intentionally not generalized into this cache.
            bool known=projectile==439 || projectile==615 || projectile==630 || projectile==705 || projectile==714 || projectile==444 || projectile==633 || projectile==460;
            return known && delay==0 && p.itemAnimation==0 && p.itemTime==0 && p.reuseDelay==0 && p.ownedProjectileCounts[projectile]==0;
        }
        private static bool OpeningBlocked(Player player,int type)
        {
            if(type<0 || type>=player.ownedProjectileCounts.Length || player.ownedProjectileCounts[type]<=0)return false;
            // Player.Update counts the pool before the projectile pass. A
            // missile killed later in that pass still contributes to this
            // counter, but cannot block a hypothetical next legal use. Only
            // check the pool at this stale-positive opening boundary; never
            // register or control an unobserved surviving source.
            for(int i=0;i<Main.maxProjectiles;i++)
            {var shot=Main.projectile[i];if(shot.active && shot.owner==player.whoAmI && shot.type==type)return true;}
            return false;
        }
        private sealed class IdlePreview
        {
            internal readonly int Projectile,Age;internal readonly HostAttackPhase Phase;internal NpcTrajectory Timeline;
            private readonly PredictionPlayerMotion motion;private readonly Vector2 mounted;private readonly int direction,phoenix,mana,attackCd,meleeCd;private readonly float scale,manaCost,meleeSpeed,rotation;
            private readonly bool slow,manaV2;private readonly Terraria.Projectile receiver;private readonly HostAttackReceive receive;
            internal IdlePreview(Player p,int projectile,NpcTrajectory timeline,HostAttackPhase phase,int age,AttackAmmoSnapshot ammo)
            {
                Projectile=projectile;Age=age;Phase=phase;Timeline=timeline;motion=NpcPredictionSource.ReadPlayer(p);mounted=p.RotatedRelativePoint(p.MountedCenter);direction=p.direction;phoenix=p.phantomPhoneixCounter;mana=p.statMana;manaCost=p.manaCost;slow=p.slowMagicUse;manaV2=Terraria.Testing.DebugOptions.ManaV2;
                scale=p.GetAdjustedItemScale(p.HeldItem);meleeSpeed=p.meleeSpeed;rotation=p.fullRotation;attackCd=p.attackCD;meleeCd=p.meleeNPCHitCooldown[timeline.Identity.Slot];
                int damageType=projectile==439?440:projectile==633?632:projectile==460?461:ammo.Projectile;receiver=ContentSamples.ProjectilesByType[damageType];receive=HostAttackReceive.Capture(p,receiver,timeline.Identity.Slot,phase==HostAttackPhase.BeforeNpc,phase==HostAttackPhase.CompletedWorld);
            }
            internal bool Current(Player p,NpcTrajectory timeline)
            {
                return Timeline.Identity.Equals(timeline.Identity) && PlayerMotionContinuation.SameFixedInput(motion,NpcPredictionSource.ReadPlayer(p),timeline.WallSources!=null) && mounted==p.RotatedRelativePoint(p.MountedCenter) && direction==p.direction && phoenix==p.phantomPhoneixCounter && mana==p.statMana && manaCost==p.manaCost && slow==p.slowMagicUse && manaV2==Terraria.Testing.DebugOptions.ManaV2 && scale==p.GetAdjustedItemScale(p.HeldItem) && meleeSpeed==p.meleeSpeed && rotation==p.fullRotation && attackCd==p.attackCD && meleeCd==p.meleeNPCHitCooldown[timeline.Identity.Slot] && receive.Same(HostAttackReceive.Capture(p,receiver,timeline.Identity.Slot,Phase==HostAttackPhase.BeforeNpc,Phase==HostAttackPhase.CompletedWorld));
            }
        }

        private static Projectile CopyProjectile(int type,int owner)
        {
            Projectile sample;if(!ContentSamples.ProjectilesByType.TryGetValue(type,out sample))return null;
            var copy=(Projectile)previewCopy.Invoke(sample,null);copy.ai=(float[])sample.ai.Clone();copy.localAI=(float[])sample.localAI.Clone();copy.localNPCImmunity=(int[])sample.localNPCImmunity.Clone();copy.owner=owner;copy.active=true;return copy;
        }
        private void BindPreview(AttackContact contact,Controlled source,AttackAmmoSnapshot ammo,HostBeamAttack.Child child)
        {
            previewContact=contact;previewSource=source;previewAmmo=ammo;previewChild=child;
            previewCenter=source.Shot.Center;previewVelocity=source.Shot.velocity;previewAi0=source.Shot.ai[0];previewAi1=source.Shot.ai[1];previewAi2=source.Shot.ai[2];
        }
        internal bool PreviewCurrent(AttackContact contact)
        {
            var player=combat.Player;
            if(contact==null || !ReferenceEquals(contact,previewContact) || player==null || previewTick!=Main.GameUpdateCount || previewSession!=observation.Session || previewSelection!=combat.Tools.SelectionIntent || !previewEnvironment.Current || player.selectedItem!=previewSlot || !ReferenceEquals(player.HeldItem,previewWeapon) || previewWeapon.type!=previewType || previewWeapon.prefix!=previewPrefix || previewAmmo!=null && !previewAmmo.MembersMatch(player,previewWeapon) || previewObstacles!=null && !previewObstacles.BirthInputsCurrent)return false;
            if(previewSource==null)return true;
            var shot=previewSource.Shot;
            return Observed(previewSource) && shot.Center==previewCenter && shot.velocity==previewVelocity && shot.ai[0]==previewAi0 && shot.ai[1]==previewAi1 && shot.ai[2]==previewAi2 && (previewChild==null || previewChild.Valid(shot));
        }
        private AttackContact BubblePreview(Player player,Projectile bubble,NpcTrajectory timeline,HostAttackClock clock,bool representative)
        {
            AttackMotion motion;int type=(int)bubble.localAI[0];Projectile sample;
            if(!HostAttackModels.TryResolved(type,bubble.localAI[1],player.HeldItem.type,out motion) || !ContentSamples.ProjectilesByType.TryGetValue(type,out sample))return null;
            if(player.magicQuiver && sample.arrow && sample.extraUpdates<1)motion=motion.WithUpdates(2,1);
            if(representative)motion=motion.WithConfidence(AttackConfidence.Representative);
            int steps=Math.Min(60,bubble.timeLeft),updates=bubble.extraUpdates+1;Vector2 center=bubble.Center,velocity=bubble.velocity;float angle=bubble.ai[0];
            if(steps<=0)return null;
            for(int k=0;k<steps;k++)
            {
                if(!HostBubbleAttack.Advance(bubble,previewTerrain,ref center,ref velocity,ref angle))return null;
            }
            bool later=true;if(!representative && !TryChildLaterSlot(bubble,out later))return null;
            int delay=(steps-1)/updates+(later?0:1);
            var receive=HostAttackReceive.Capture(player,sample,timeline.Identity.Slot,clock.BeforeNpc,clock.NextWorld);
            var obstacles=new HostAttackObstacles(player,sample,timeline,clock.LaunchDelay+delay,clock.BeforeNpc,clock.NextWorld);
            previewObstacles=obstacles;
            HostProjectileEnvironment environment;
            return HostProjectileEnvironment.Solve(sample,previewTerrain,center,motion,timeline,clock.Age,delay,clock.FirstTick-clock.Age,t=>receive.Allows(clock.LaunchDelay+t),out environment,(k,t,x,y)=>obstacles.PassOuter(k,t,clock.LaunchDelay+delay+motion.OuterAtSubupdate(k),x,y),(k,t,x,y)=>motion.SubupdateInOuter(k)!=0 || obstacles.BeforeNpc(t,clock.LaunchDelay+delay+motion.OuterAtSubupdate(k),x,y));
        }
    }
}
