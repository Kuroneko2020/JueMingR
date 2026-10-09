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
        private Item weapon;
        private int slot,type,prefix;
        private long session;
        private uint prepared;
        private Vector2 origin,nextOrigin;
        private AttackAmmoSnapshot ammo;
        private AttackContact current,next;
        private readonly PredictionTerrain terrain=new PredictionTerrain();
        internal bool Failed {get;private set;}
        internal AttackContact ExpectedImpact {get{return Valid(false)?(Main.GameUpdateCount==prepared?current:next):null;}}
        internal HostAttackAim(HostCombat combat,HostCombatObservation observation){this.combat=combat;this.observation=observation;}
        internal bool Permission {get{return !Failed && observation.Settings.CanRun && observation.Options.Aim && combat.Player!=null && combat.Admitted(combat.Player) &&
            (combat.Left || combat.Player.controlUseItem || combat.Player.channel || combat.Use.Active);}}
        internal void Demand()
        {if(Permission && Eligible(combat.Player.HeldItem))observation.Prediction.Cache.Demand(1,1,NpcPredictionCache.Horizon);else{observation.Prediction.Cache.Release(1);Clear();}}
        internal void Clear(){weapon=null;ammo=null;current=next=null;}
        internal void Reset(){Clear();Failed=false;}
        internal void PrepareNatural(){observation.PrepareAction();}
        internal void Prepare()
        {PreparePhase(false);}
        internal void PrepareAction()
        {PreparePhase(true);}
        private void PreparePhase(bool beforeNpc)
        {
            try{PrepareCore(beforeNpc);}
            catch(Exception error){Clear();if(error is OutOfMemoryException || error is AccessViolationException)throw;Failed=true;}
        }
        private void PrepareCore(bool beforeNpc)
        {
            Clear();if(!Permission || !observation.Selection.HasTarget)return;
            terrain.Reset();
            var player=combat.Player;var item=player.HeldItem;if(!Eligible(item))return;
            var timeline=observation.Prediction.Cache.Read(1);if(timeline==null || !timeline.Identity.Equals(observation.Selection.Target))return;
            var captured=AttackAmmoSnapshot.Capture(player,item);if(captured==null)return;
            AttackMotion motion;if(!Model(captured,out motion))return;
            origin=player.RotatedRelativePoint(player.MountedCenter);int age=(int)((long)Main.GameUpdateCount-timeline.SampleTick)-(beforeNpc?1:0);if(age<0 || age>1)return;
            current=AttackIntercept.Solve(origin.X,origin.Y,motion,timeline,age,0,Passage);
            var movement=NpcPredictionSource.ReadPlayer(player);PredictionStop stop;
            if(!beforeNpc && PlayerMotionContinuation.Advance(ref movement,new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,Remix=Main.remixWorld},terrain,out stop))
            {
                nextOrigin=origin+new Vector2(movement.X-player.position.X,movement.Y-player.position.Y);
                // CompletedWorldUpdate T precedes player attack T+1, then NPC
                // movement T+1, then projectile AI/move/damage T+1. Solve's
                // first subupdate already reads NPC point age+1. Advancing age
                // here as well would consume point 2 in that first phase.
                next=AttackIntercept.Solve(nextOrigin.X,nextOrigin.Y,motion,timeline,age,0,Passage);
            }
            weapon=item;slot=player.selectedItem;type=item.type;prefix=item.prefix;session=observation.Session;prepared=Main.GameUpdateCount;ammo=captured;
        }
        private static bool Eligible(Item item){return item!=null && !item.IsAir && item.shoot>0 && item.damage>0 && item.pick==0 && item.axe==0 && item.hammer==0 && !item.summon && !item.sentry;}
        private static bool Model(AttackAmmoSnapshot ammo,out AttackMotion motion)
        {
            motion=default(AttackMotion);Projectile sample;
            if(!ContentSamples.ProjectilesByType.TryGetValue(ammo.Projectile,out sample))return false;
            float gravity=0;int start=0;
            // Explicit vanilla mechanism members. A new type isn't admitted
            // merely because it shares AI style; homing/derived behavior needs
            // its own confidence and stage adapter.
            switch(ammo.Projectile)
            {
                case 1:case 2:case 4:case 41:gravity=.1f;start=15;break;
                case 14:case 5:case 89:case 100:case 110:case 242:case 257:case 279:case 283:case 284:case 285:case 286:break;
                default:return false;
            }
            motion=new AttackMotion(ammo.Speed,gravity,start,sample.extraUpdates+1,sample.width,sample.height,sample.timeLeft);return true;
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
            if(!ammo.IdentityMatches(player,weapon))return false;
            var result=Main.GameUpdateCount==prepared?current:next;
            if(result==null || !result.Timeline.Identity.Equals(observation.Selection.Target) || !ReferenceEquals(result.Timeline,observation.Prediction.Cache.Read(1)) ||
                Vector2.DistanceSquared(player.RotatedRelativePoint(player.MountedCenter),Main.GameUpdateCount==prepared?origin:nextOrigin)>4)return false;
            return !checkAmmo || ammo.Matches(player,weapon);
        }
        internal CombatCursorScope BeginShot(Player player,Item item)
        {
            if(!ReferenceEquals(player,combat.Player))return null;
            if(!ReferenceEquals(item,weapon) || !Valid(true)){Clear();return null;}
            var result=Main.GameUpdateCount==prepared?current:next;
            // One prepared ordinary attack is a capability, not a reusable
            // cursor value. Native consumption retires it even if a later
            // refill happens to recreate the same ammo bytes in this step.
            current=next=null;
            return CombatCursorScope.Begin(new Vector2(result.AimX,result.AimY));
        }
    }
}
