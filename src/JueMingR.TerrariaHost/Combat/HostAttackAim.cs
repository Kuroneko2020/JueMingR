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
        private readonly PredictionTerrain terrain=new PredictionTerrain();
        private readonly HostSwingAttack swing=new HostSwingAttack();
        internal bool Failed {get;private set;}
        internal AttackContact ExpectedImpact {get{return Valid(false)?(Main.GameUpdateCount==prepared?current:next):Control.ExpectedImpact;}}
        internal HostAttackAim(HostCombat combat,HostCombatObservation observation){this.combat=combat;this.observation=observation;Control=new HostAttackControl(combat,observation);}
        internal bool Permission {get{return !Failed && observation.Settings.CanRun && observation.Options.Aim && combat.Player!=null && combat.Admitted(combat.Player) &&
            (combat.Left || combat.Player.controlUseItem || combat.Player.channel || combat.Use.Active || Control.Pending);}}
        internal void Demand()
        {if(Permission && Eligible(combat.Player.HeldItem))observation.Prediction.Cache.Demand(1,1,NpcPredictionCache.Horizon);else{observation.Prediction.Cache.Release(1);Clear();}}
        internal void Clear(){weapon=null;ammo=null;current=next=null;Control.Clear();}
        internal void Reset(){Clear();swing.Clear();Control.Reset();Failed=false;}
        internal void ObserveSwing(Player player,Item item,Rectangle frame,float offset)
        {
            if(!ReferenceEquals(player,combat.Player) || !HostSwingAttack.Handles(item) || !Permission)return;
            try{swing.Observe(item,frame,offset);PrepareSwing(player,item,observation.Prediction.Cache.Read(1),HostAttackPhase.BeforeNpc,true);}catch(Exception error){FailLocal(error);}
        }
        private void PrepareSwing(Player player,Item item,NpcTrajectory timeline,HostAttackPhase phase,bool naturalPose)
        {
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
            bool beforeNpc=phase==HostAttackPhase.BeforeNpc;Clear();if(!Permission || !observation.Selection.HasTarget)return;
            terrain.Reset();
            var player=combat.Player;var item=player.HeldItem;if(!Eligible(item))return;
            var timeline=observation.Prediction.Cache.Read(1);if(timeline==null || !timeline.Identity.Equals(observation.Selection.Target))return;
            if(HostSwingAttack.Handles(item)){PrepareSwing(player,item,timeline,phase,false);return;}
            Control.Prepare(timeline,phase);
            var captured=AttackAmmoSnapshot.Capture(player,item);if(captured==null)return;
            if(HostHeldAttack.Weapon(item.type) || HostYoyoNavigation.Weapon(item) || HostWhipAttack.Weapon(item))return; // Controller birth is not its later ordinary/beam damage phase.
            Projectile sample;bool melee=ContentSamples.ProjectilesByType.TryGetValue(captured.Projectile,out sample) && HostMeleeAttack.Handles(sample);
            AttackMotion motion;bool sky=HostSkyAttack.Handles(item.type);if(!sky && !melee && !HostAttackModels.TryRead(captured,out motion))return;
            origin=player.RotatedRelativePoint(player.MountedCenter);int age=(int)((long)Main.GameUpdateCount-timeline.SampleTick)-(beforeNpc?1:0);if(age<0 || age>1)return;
            current=Solve(player,item,captured,timeline,origin,age,beforeNpc);
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
            if(HostSkyAttack.Handles(item.type))return HostSkyAttack.Solve(player,item,captured,timeline,start,age,terrain,beforeNpc);
            Projectile melee;if(ContentSamples.ProjectilesByType.TryGetValue(captured.Projectile,out melee) && HostMeleeAttack.Handles(melee))return HostMeleeAttack.Solve(player,item,captured,timeline,age,beforeNpc,terrain);
            if(item.type==2624){var b=timeline[Math.Min(age+1,timeline.Count-1)].ProjectileReceiveBounds;start+=(new Vector2(b.CenterX,b.CenterY)-start).SafeNormalize(Vector2.UnitX)*40;}
            Projectile sample;if(!ContentSamples.ProjectilesByType.TryGetValue(captured.Projectile,out sample))return null;
            var receive=HostAttackReceive.Capture(player,sample,timeline.Identity.Slot,beforeNpc,!beforeNpc);
            AttackMotion motion;return HostAttackModels.TryRead(captured,out motion)?AttackIntercept.Solve(start.X,start.Y,motion,timeline,age,0,Passage,1,receive.Allows):null;
        }
        private bool Passage(float x,float y,float nx,float ny,float width,float height)
        {
            return terrain.ProjectilePassage(x,y,nx,ny,(int)width,(int)height);
        }
        private bool Valid(bool checkAmmo)
        {
            var player=combat.Player;
            if(weapon==null || !Permission || session!=observation.Session || Main.GameUpdateCount<prepared || Main.GameUpdateCount-prepared>1 ||
                player.selectedItem!=slot || !ReferenceEquals(player.HeldItem,weapon) || weapon.type!=type || weapon.prefix!=prefix ||
                !observation.Selection.HasTarget || !CombatSelection.Valid(observation.Selection.Target,session))return false;
            if(ammo==null?!HostSwingAttack.Handles(weapon):!ammo.IdentityMatches(player,weapon))return false;
            var result=Main.GameUpdateCount==prepared?current:next;
            if(result==null || !result.Timeline.Identity.Equals(observation.Selection.Target) || !ReferenceEquals(result.Timeline,observation.Prediction.Cache.Read(1)) ||
                Vector2.DistanceSquared(player.RotatedRelativePoint(player.MountedCenter),Main.GameUpdateCount==prepared?origin:nextOrigin)>4)return false;
            return !checkAmmo || terrain.Unchanged && ammo!=null && ammo.Matches(player,weapon);
        }
        internal CombatCursorScope BeginShot(Player player,Item item,bool regular)
        {
            if(!ReferenceEquals(player,combat.Player))return null;
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
