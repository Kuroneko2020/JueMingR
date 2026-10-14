using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // AI99 release copies the parent's velocity and does not read a cursor.
    // Prepare the last legal held direction for next-action free flight; the
    // detached child is never lent input. Frequency does not change gravity.
    internal static class HostYoyoReleaseAttack
    {
        internal static bool TryPoint(Player player,Item item,float launchSpeed,NpcTrajectory future,HostAttackClock clock,PredictionTerrain terrain,out Vector2 point)
        {
            point=Vector2.Zero;int first=clock.FirstTick;
            if(first<0 || first+2>=future.Count || launchSpeed<=0)return false;
            var start=player.RotatedRelativePoint(player.MountedCenter);
            float multiplier=(1+player.meleeSpeed*3)/4;
            float speed=ProjectileID.Sets.YoyosTopSpeed[item.shoot]/multiplier;
            float range=ProjectileID.Sets.YoyosMaximumRange[item.shoot];
            if(player.yoyoString)range=range*1.25f+30;range/=multiplier;
            if(range<=1 || speed<=0)return false;
            float inertia=Math.Max(1,14-speed/2),heldSpeed=launchSpeed>speed?launchSpeed*.98f:launchSpeed;
            float inherited=(heldSpeed*(inertia-1)+Math.Min((range-1)/2,speed))/inertia;
            Projectile sample=ContentSamples.ProjectilesByType[item.shoot];
            float best=float.MaxValue,bestBound=float.MaxValue;Vector2 bestUnit=Vector2.Zero;
            for(int time=2;first+time<future.Count;time++)
            {
                var target=future[first+time].ProjectileReceiveBounds;
                var goal=new Vector2(target.CenterX,target.CenterY);
                Vector2 unit=(goal-start-new Vector2(0,.15f*time*(time+1))).SafeNormalize(Vector2.UnitX*player.direction);
                // Invert capped/accelerating discrete response at receive times,
                // rather than adding a dense all-angle replay search.
                for(int refine=0;refine<4;refine++)
                {
                    var end=End(start,unit,inherited,speed,time);
                    unit=(unit+(goal-end)/Math.Max(1,inherited*(time+1))).SafeNormalize(unit);
                }
                var actual=End(start,unit,inherited,speed,time);
                float dx=Math.Max(0,Math.Abs(actual.X-goal.X)-(target.Width+sample.width)/2);
                float dy=Math.Max(0,Math.Abs(actual.Y-goal.Y)-(target.Height+sample.height)/2);
                // Prefer the centre over the first grazing edge. Integer
                // cursor rounding and the real hand's subpixel origin must
                // not turn a representative seed into a repeated near miss.
                float error=Vector2.DistanceSquared(actual,goal);
                if(error>=best)continue;
                best=error;bestBound=dx*dx+dy*dy;bestUnit=unit;
                if(error<16)break;
            }
            if(bestBound>4)return false;
            point=start+bestUnit*(range-1);
            return terrain.ProjectilePassage(start.X,start.Y,start.X+bestUnit.X*inherited,start.Y+bestUnit.Y*inherited,sample.width,sample.height);
        }
        private static Vector2 End(Vector2 start,Vector2 unit,float inherited,float speed,int time)
        {
            var velocity=unit*inherited;var position=start+velocity;
            for(int i=0;i<time;i++)
            {
                velocity.Y+=.3f;
                if(velocity.LengthSquared()>256)velocity=velocity.SafeNormalize(Vector2.Zero)*16;
                else if(velocity.Y<=3)
                {
                    float top=Math.Min(16,speed);
                    if(Math.Abs(velocity.X)<.1f)velocity.X+=unit.X<0?-.1f:.1f;
                    else if(velocity.X>0 && velocity.X<top)velocity.X+=.1f;
                    else if(velocity.X<0 && velocity.X>-top)velocity.X-=.1f;
                }
                position+=velocity;
            }
            return position;
        }
        // Clear-line opening uses the same release seed, never the held rope
        // boundary. Only a dry, unobstructed representative child is proved;
        // native bounce/liquid/family uncertainty remains an unknown preview.
        internal static AttackContact Preview(Player player,Item item,float launchSpeed,Projectile child,NpcTrajectory future,HostAttackClock clock,PredictionTerrain terrain,out HostAttackObstacles obstacles)
        {
            obstacles=null;Vector2 point;
            if(!TryPoint(player,item,launchSpeed,future,clock,terrain,out point))return null;
            float multiplier=(1+player.meleeSpeed*3)/4,speed=ProjectileID.Sets.YoyosTopSpeed[item.shoot]/multiplier;
            float range=ProjectileID.Sets.YoyosMaximumRange[item.shoot];if(player.yoyoString)range=range*1.25f+30;range/=multiplier;
            float inertia=Math.Max(1,14-speed/2),heldSpeed=launchSpeed>speed?launchSpeed*.98f:launchSpeed;
            float inherited=(heldSpeed*(inertia-1)+Math.Min((range-1)/2,speed))/inertia;
            var start=player.RotatedRelativePoint(player.MountedCenter);var unit=(point-start).SafeNormalize(Vector2.UnitX*player.direction);
            if(!HostProjectileEnvironment.Dry(child,terrain,start.X,start.Y,unit.X*inherited))return null;
            var position=start+unit*inherited;var velocity=unit*inherited;child.ai[0]=-2;
            if(child.penetrate<0)child.penetrate=3; // Native 3..6 RNG: prove only its minimum.
            var receive=HostAttackReceive.Capture(player,child,future.Identity.Slot,clock.BeforeNpc,clock.NextWorld);
            obstacles=new HostAttackObstacles(player,child,future,clock.LaunchDelay,clock.BeforeNpc,clock.NextWorld);
            // A first held impact can change immunity before the split. This
            // finite opening does not promise a second hit through that stage.
            if(!obstacles.PassCustomOuter(1,clock.FirstTick,clock.LaunchDelay+1,position.X,position.Y,child.width,child.height,null,false,0,true) || obstacles.SelectedHits>0 || obstacles.OtherHits>0)return null;
            for(int time=1;clock.FirstTick+time<future.Count && time<child.timeLeft;time++)
            {
                int tick=clock.FirstTick+time,outer=clock.LaunchDelay+time+1;
                if(time>1 && !obstacles.BeforeNpc(tick,outer,position.X,position.Y))return null;
                // The near-vertical branch reads the future owner's side;
                // this opening has no continuing owner-side proof for it.
                if(Math.Abs(velocity.X)<.1f && velocity.Y+.3f<=3)return null;
                velocity.Y+=.3f;
                if(velocity.LengthSquared()>256)velocity=velocity.SafeNormalize(Vector2.Zero)*16;
                else if(velocity.Y<=3)
                {
                    float top=Math.Min(16,speed);
                    if(Math.Abs(velocity.X)<.1f)velocity.X+=unit.X<0?-.1f:.1f;
                    else if(velocity.X>0 && velocity.X<top)velocity.X+=.1f;
                    else if(velocity.X<0 && velocity.X>-top)velocity.X-=.1f;
                }
                var next=position+velocity;
                if(!HostProjectileEnvironment.Dry(child,terrain,position.X,position.Y,velocity.X) || !terrain.ProjectilePassage(position.X,position.Y,next.X,next.Y,child.width,child.height))return null;
                position=next;
                if(!obstacles.PassOuter(time+1,tick,outer,position.X,position.Y))return null;
                var contact=receive.Allows(outer) && obstacles.SelectedAllows(outer)?AttackIntercept.BodyContact(future,tick,0,point.X,point.Y,position.X,position.Y,child.width,child.height,AttackConfidence.Representative):null;
                if(contact!=null)return contact;
            }
            return null;
        }
    }
}
