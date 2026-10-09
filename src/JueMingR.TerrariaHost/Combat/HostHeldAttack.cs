using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // Controller input windows are separate from the ordinary damage projectile
    // role. Direction packets may exist without a reliable contact/red box.
    internal static class HostHeldAttack
    {
        internal static bool Weapon(int type){Item item;return type==113 || type==218 || type==495 || type==2797 || type==3475 || type==3540 || type==3854 || type==3930 || type==3541 || type==2882 || type==4923 || ContentSamples.ItemsByType.TryGetValue(type,out item) && HostFlailAttack.Weapon(item);}
        internal static bool Handles(int type){return type==615 || type==630 || type==705 || type==714 || type==633 || type==460 || type==927 || HostFlailAttack.Handles(type);}
        internal static bool Consumes(Player player,Projectile shot)
        {
            if(HostFlailAttack.Handles(shot.type))return shot.ai[0]==0;
            if(!player.channel)return false;
            if(shot.type==615 || shot.type==630 || shot.type==705 || shot.type==714)return shot.ai[1]<=1;
            if(shot.type==460){float charge=shot.ai[0]+player.GetSlowMagicUseRate();return charge<180?shot.ai[1]+1>=5:(int)charge%5==0;}
            return shot.type==633 || shot.type==927;
        }
        internal static Vector2 DirectionPoint(Player player,NpcTrajectory timeline,int tick)
        {var b=timeline[Math.Min(timeline.Count-1,Math.Max(0,tick))].ProjectileReceiveBounds;return new Vector2(b.CenterX,b.CenterY);}
        internal static Vector2 OpeningPoint(Player player,Item item,AttackAmmoSnapshot ammo,NpcTrajectory timeline)
        {
            Vector2 target=DirectionPoint(player,timeline,1);float distance=Vector2.Distance(target,player.RotatedRelativePoint(player.MountedCenter));int lead=1;
            if(item.type==113 || item.type==218 || item.type==495)lead=(int)Math.Ceiling(distance/32);
            else if(item.type==2797)lead=60/2+(int)Math.Ceiling(distance/Math.Max(1,ammo.Speed)/2);
            else if(item.type==3541)lead=(int)Math.Ceiling(1/(1-.92f)); // native parent steering response
            else if(item.useAmmo>0)lead=(int)Math.Ceiling(distance/Math.Max(1,ammo.Speed));
            return DirectionPoint(player,timeline,lead);
        }
        internal static AttackContact Solve(Player player,Projectile shot,NpcTrajectory timeline,int age,bool beforeNpc,PredictionTerrain terrain,out Vector2 point,out AttackAmmoSnapshot ammo)
        {
            ammo=null;point=DirectionPoint(player,timeline,age+1);
            if(HostFlailAttack.Handles(shot.type))return HostFlailAttack.Solve(player,shot,timeline,age,beforeNpc,terrain,out point);
            if(shot.type==633){point=DirectionPoint(player,timeline,age+12);return null;}
            if(shot.type==927)return HostMeleeAttack.Starlight(player,shot,timeline,age,beforeNpc,terrain,out point);
            if(shot.type==460)return null;
            float baseSpeed=shot.type==705?12:shot.type==714?8:14;
            ammo=AttackAmmoSnapshot.CaptureController(player,player.HeldItem,baseSpeed);if(ammo==null)return null;
            int projectile=ammo.Projectile;float speed=ammo.Speed;
            if(shot.type==705){if(projectile==1)projectile=2;if(player.phantomPhoneixCounter+1>=3){projectile=706;speed=16;}}
            AttackMotion motion;if(!HostAttackModels.TryResolved(projectile,speed,player.HeldItem.type,out motion))return null;
            motion=new AttackMotion(motion.Speed,motion.Gravity,motion.GravityStart,motion.Updates,motion.Width,motion.Height,motion.Lifetime,AttackConfidence.Representative,motion.Acceleration,motion.MaxSpeed,motion.ComponentBirthLimit,motion.ComponentAccelerationLimit,motion.DragAfterGravity,motion.StopSmallVelocity);
            Vector2 origin=player.RotatedRelativePoint(player.MountedCenter);int delay=Math.Max(0,(int)Math.Ceiling(shot.ai[1])-1);
            Projectile sample;if(!ContentSamples.ProjectilesByType.TryGetValue(projectile,out sample))return null;
            var receive=HostAttackReceive.Capture(player,sample,timeline.Identity.Slot,beforeNpc);
            var contact=AttackIntercept.Solve(origin.X,origin.Y,motion,timeline,age,delay,(x,y,nx,ny,w,h)=>terrain.ProjectilePassage(x,y,nx,ny,(int)w,(int)h),beforeNpc?1:0,receive.Allows);
            if(contact!=null)point=new Vector2(contact.AimX,contact.AimY);return contact;
        }
    }
}
