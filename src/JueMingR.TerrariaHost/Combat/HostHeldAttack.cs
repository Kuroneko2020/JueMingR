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
        private static readonly System.Reflection.MethodInfo copy=typeof(object).GetMethod("MemberwiseClone",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        internal static bool Weapon(int type){Item item;return type==113 || type==218 || type==495 || type==3030 || type==2795 || type==2797 || type==3475 || type==3540 || type==3854 || type==3930 || type==3541 || type==2882 || type==4923 || ContentSamples.ItemsByType.TryGetValue(type,out item) && HostFlailAttack.Weapon(item);}
        internal static bool Handles(int type){return type==439 || type==615 || type==630 || type==705 || type==714 || type==633 || type==460 || type==927 || HostFlailAttack.Handles(type);}
        internal static bool Consumes(Player player,Projectile shot)
        {
            if(HostFlailAttack.Handles(shot.type))return shot.ai[0]==0;
            if(!player.channel)return false;
            if(shot.type==439)return HostLaserAttack.Consumes(player,shot);
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
            else if(item.type==3030)lead=(int)Math.Ceiling(distance/20);
            else if(item.type==2797)lead=60/2+(int)Math.Ceiling(distance/Math.Max(1,ammo.Speed)/2);
            else if(item.type==3541)lead=(int)Math.Ceiling(1/(1-.92f)); // native parent steering response
            else if(item.useAmmo>0)lead=(int)Math.Ceiling(distance/Math.Max(1,ammo.Speed));
            return DirectionPoint(player,timeline,lead);
        }
        internal static AttackContact Solve(Player player,Projectile shot,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,out Vector2 point,out AttackAmmoSnapshot ammo,out HostAttackObstacles obstacles)
        {return SolvePrepared(player,shot,timeline,clock,terrain,out point,out ammo,out obstacles,HostAttackPlayerFrame.Capture(player,timeline));}
        internal static AttackContact SolvePrepared(Player player,Projectile shot,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,out Vector2 point,out AttackAmmoSnapshot ammo,out HostAttackObstacles obstacles,HostAttackPlayerFrame frame)
        {
            int age=clock.Age;ammo=null;obstacles=null;point=DirectionPoint(player,timeline,clock.FirstTick);
            if(shot.type==714)return HostCelebrationAttack.SolvePrepared(player,shot,timeline,clock,terrain,out point,out ammo,out obstacles,frame);
            if(shot.type==439)return HostLaserAttack.SolvePrepared(player,shot,timeline,clock,terrain,out point,out obstacles,frame);
            if(HostFlailAttack.Handles(shot.type))return HostFlailAttack.SolvePrepared(player,shot,timeline,clock,terrain,out point,frame);
            if(shot.type==633){point=DirectionPoint(player,timeline,age+12);return null;}
            if(shot.type==927)return HostMeleeAttack.StarlightPrepared(player,shot,timeline,clock,terrain,out point,frame);
            if(shot.type==460)return null;
            float baseSpeed=shot.type==705?12:shot.type==714?8:14;
            ammo=AttackAmmoSnapshot.CaptureController(player,player.HeldItem,baseSpeed);if(ammo==null)return null;
            int projectile=ammo.Projectile;float speed=ammo.Speed;
            if(shot.type==705){if(projectile==1)projectile=2;if(player.phantomPhoneixCounter+1>=3){projectile=706;speed=16;}}
            AttackMotion motion;if(!HostAttackModels.TryResolved(projectile,speed,player.HeldItem.type,out motion))return null;
            motion=motion.WithConfidence(AttackConfidence.Representative);
            Vector2 origin=player.RotatedRelativePoint(player.MountedCenter);int delay=Math.Max(0,(int)Math.Ceiling(shot.ai[1])-1);
            // The next native firing window belongs to the moving holder.
            // Only this private motion advances through the observed delay.
            var body=frame.Motion;var world=frame.Environment;
            for(int step=0;step<delay+(clock.NextWorld?1:0);step++){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref body,world,terrain,out stop))return null;}
            var pose=(Player)copy.Invoke(player,null);HostAttackPlayerFrame.ApplyPose(pose,body);origin=pose.RotatedRelativePoint(pose.MountedCenter);
            Projectile sample;if(!ContentSamples.ProjectilesByType.TryGetValue(projectile,out sample))return null;
            if(player.magicQuiver && sample.arrow && sample.extraUpdates<1)motion=motion.WithUpdates(2,1);
            var receive=HostAttackReceive.Capture(player,sample,timeline.Identity.Slot,clock.BeforeNpc,clock.NextWorld);
            HostProjectileEnvironment environment;
            obstacles=new HostAttackObstacles(player,sample,timeline,clock.LaunchDelay+delay,clock.BeforeNpc,clock.NextWorld);
            var receivers=obstacles;
            var contact=HostProjectileEnvironment.Solve(sample,terrain,origin,motion,timeline,age,delay,clock.FirstTick-age,t=>receive.Allows(clock.LaunchDelay+t),out environment,(k,t,x,y)=>receivers.PassOuter(k,t,clock.LaunchDelay+delay+motion.OuterAtSubupdate(k),x,y),(k,t,x,y)=>motion.SubupdateInOuter(k)!=0 || receivers.BeforeNpc(t,clock.LaunchDelay+delay+motion.OuterAtSubupdate(k),x,y));
            if(contact!=null)point=new Vector2(contact.AimX,contact.AimY);return contact;
        }
    }
}
