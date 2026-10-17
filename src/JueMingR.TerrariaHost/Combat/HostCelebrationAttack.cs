using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // AI75's seven-shot cycle is causal input to AI147. The same rocket item
    // has seven different motors; its ammo identity alone cannot select one.
    internal static class HostCelebrationAttack
    {
        private static readonly MethodInfo copy=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);
        private readonly struct Target
        {
            internal readonly int Slot;internal readonly MotionRect Bounds;internal readonly bool Chaseable,KeepLock;
            internal Target(NPC n){Slot=n.whoAmI;Bounds=new MotionRect(n.position.X,n.position.Y,n.width,n.height);Chaseable=n.CanBeChasedBy(null);KeepLock=n.CanBeChasedBy(null,true);}
        }
        internal static AttackContact Solve(Player player,Projectile parent,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,out Vector2 point,out AttackAmmoSnapshot ammo,out HostAttackObstacles obstacles)
        {return SolvePrepared(player,parent,timeline,clock,terrain,out point,out ammo,out obstacles,HostAttackPlayerFrame.Capture(player,timeline));}
        internal static AttackContact SolvePrepared(Player player,Projectile parent,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,out Vector2 point,out AttackAmmoSnapshot ammo,out HostAttackObstacles obstacles,HostAttackPlayerFrame frame)
        {
            point=HostHeldAttack.DirectionPoint(player,timeline,clock.FirstTick);obstacles=null;
            int delay=Math.Max(0,(int)Math.Ceiling(parent.ai[1])-1),stage=((int)(parent.ai[0]+delay+1)/8)%7;
            ammo=AttackAmmoSnapshot.CaptureController(player,player.HeldItem,stage==3?9:8);
            if(ammo==null || ammo.Projectile<715 || ammo.Projectile>718)return null;
            var sample=ContentSamples.ProjectilesByType[ammo.Projectile];var motion=frame.Motion;var world=frame.Environment;
            for(int step=0;step<delay+(clock.NextWorld?1:0);step++){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,world,terrain,out stop))return null;}
            var pose=(Player)copy.Invoke(player,null);HostAttackPlayerFrame.ApplyPose(pose,motion);Vector2 origin=pose.RotatedRelativePoint(pose.MountedCenter);
            var receive=HostAttackReceive.Capture(player,sample,timeline.Identity.Slot,clock.BeforeNpc,clock.NextWorld);
            var targets=new List<Target>();if(stage==3)for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active)targets.Add(new Target(Main.npc[i]));
            var directions=new Vector2[stage==4?15:9];int used=0,first=clock.FirstTick+delay;
            var initialObstacles=new HostAttackObstacles(player,sample,timeline,clock.LaunchDelay+delay,clock.BeforeNpc,clock.NextWorld);
            HostAttackObstacles unlimitedObstacles=null;var unlimitedReceive=receive;
            if(stage==0)
            {
                // At subupdate 20 the native rocket becomes unlimited and
                // starts respecting owner immunity. Before this boundary one
                // other body consumes it; no earlier surviving hits need to
                // be transferred to the later private receiver state.
                var unlimited=(Projectile)copy.Invoke(sample,null);unlimited.penetrate=unlimited.maxPenetrate=-1;
                unlimitedObstacles=new HostAttackObstacles(player,unlimited,timeline,clock.LaunchDelay+delay,clock.BeforeNpc,clock.NextWorld);
                unlimitedReceive=HostAttackReceive.Capture(player,unlimited,timeline.Identity.Slot,clock.BeforeNpc,clock.NextWorld);
            }
            obstacles=initialObstacles;
            for(int seed=0;seed<directions.Length && first<timeline.Count;seed++)
            {
                Vector2 unit;
                if(seed<9)
                {
                    var b=timeline[Math.Min(timeline.Count-1,first+seed*4)].ProjectileReceiveBounds;var delta=new Vector2(b.CenterX,b.CenterY)-origin;
                    int travel=Math.Min(360,Math.Max(1,(int)Math.Ceiling(delta.Length()/ammo.Speed)));
                    // A finite gravity/impulse seed is checked by the full motor;
                    // random muzzle spread stays a representative, never an RNG draw.
                    float drift=0,vy=0;for(int k=1;k<=travel;k++){Gravity(stage,k,ref vy);drift+=vy;}
                    unit=new Vector2(delta.X,delta.Y-drift).SafeNormalize(Vector2.UnitX*player.direction);
                }
                else
                {
                    // The native helix changes position before movement. A
                    // stationary target makes all nine gravity-only seeds
                    // identical, and both siblings can pass above/below it.
                    // Six bounded seeds invert the actual stage4 motor, with
                    // the second sibling's initial localAI=46 included. They
                    // still need the full terrain/receiver replay below.
                    var b=timeline[first].ProjectileReceiveBounds;var delta=new Vector2(b.CenterX,b.CenterY)-origin;
                    int travel=Math.Min(360,Math.Max(1,(int)Math.Ceiling(delta.Length()/ammo.Speed)+(seed-9)%3-1));
                    b=timeline[Math.Min(timeline.Count-1,first+(travel-1)/sample.MaxUpdates)].ProjectileReceiveBounds;
                    unit=HelixDirection(sample,origin,new Vector2(b.CenterX,b.CenterY),ammo.Speed,travel,(seed-9)/3);
                }
                bool duplicate=false;for(int i=0;i<used;i++)if(directions[i]==unit){duplicate=true;break;}if(duplicate)continue;directions[used++]=unit;
                int members=stage==4?2:stage==5?3:1;
                for(int member=0;member<members;member++)
                {
                    var shot=(Projectile)copy.Invoke(sample,null);shot.ai=new float[3];shot.localAI=new float[3];shot.ai[0]=stage;if(stage==4)shot.ai[1]=member;
                    shot.owner=player.whoAmI;shot.Center=origin;shot.velocity=unit.RotatedBy(stage==5?(member-1)*MathHelper.TwoPi/80:0)*ammo.Speed;
                    Vector2 aim=origin+unit*10000;int limit=Math.Min(sample.timeLeft,(timeline.Count-first)*sample.MaxUpdates);var activeObstacles=initialObstacles;var activeReceive=receive;
                    for(int k=1;k<=limit;k++)
                    {
                        int outer=(k-1)/sample.MaxUpdates,tick=first+outer;
                        if(!Advance(shot,timeline,tick,terrain,targets))break;
                        if(stage==0 && shot.localAI[0]==20){activeObstacles=unlimitedObstacles;activeReceive=unlimitedReceive;}
                        if(!HostProjectileEnvironment.Dry(sample,terrain,shot.Center.X,shot.Center.Y,shot.velocity.X))break;
                        float vx,ry;if(!terrain.ProjectileCollision(shot.Center.X,shot.Center.Y,shot.velocity.X,shot.velocity.Y,shot.width,shot.height,out vx,out ry))break;
                        int elapsed=clock.LaunchDelay+delay+outer+1;
                        if(vx!=shot.velocity.X || ry!=shot.velocity.Y)
                        {shot.position+=new Vector2(vx,ry);if(activeReceive.Allows(elapsed)){var hit=Explosion(timeline,tick,aim,shot.Center,sample.type);if(hit!=null){obstacles=activeObstacles;point=aim;return hit;}}break;}
                        shot.position+=shot.velocity;
                        int receiverStep=stage==0 && k>=20?k-19:k;
                        if(!activeObstacles.PassOuter(receiverStep,tick,elapsed,shot.Center.X,shot.Center.Y))
                        {if(activeObstacles.Remaining==0 && activeReceive.Allows(elapsed)){var hit=Explosion(timeline,tick,aim,shot.Center,sample.type);if(hit!=null){obstacles=activeObstacles;point=aim;return hit;}}break;}
                        if(activeReceive.Allows(elapsed))
                        {
                            var hit=AttackIntercept.BodyContact(timeline,tick,(k-1)%sample.MaxUpdates,aim.X,aim.Y,shot.Center.X,shot.Center.Y,shot.width,shot.height,AttackConfidence.Representative);
                            if(hit!=null){obstacles=activeObstacles;point=aim;return hit;}
                        }
                        if(k==limit && k==sample.timeLeft && activeReceive.Allows(elapsed)){var hit=Explosion(timeline,tick,aim,shot.Center,sample.type);if(hit!=null){obstacles=activeObstacles;point=aim;return hit;}}
                    }
                }
            }
            return null;
        }
        private static Vector2 HelixDirection(Projectile sample,Vector2 origin,Vector2 target,float speed,int steps,int sibling)
        {
            Vector2 delta=target-origin,unit=delta.SafeNormalize(Vector2.UnitX);
            var shot=(Projectile)copy.Invoke(sample,null);shot.ai=new float[3];shot.localAI=new float[3];shot.ai[0]=4;shot.ai[1]=sibling;
            for(int refinement=0;refinement<3;refinement++)
            {
                shot.Center=origin;shot.velocity=unit*speed;shot.localAI[0]=0;
                for(int step=0;step<steps;step++)
                {
                    Advance(shot,null,0,null,null);shot.position+=shot.velocity;
                }
                Vector2 displacement=shot.Center-origin;
                if(displacement.LengthSquared()==0 || delta.LengthSquared()==0)break;
                unit=unit.RotatedBy(MathHelper.WrapAngle(delta.ToRotation()-displacement.ToRotation()));
            }
            return unit;
        }
        private static AttackContact Explosion(NpcTrajectory timeline,int tick,Vector2 aim,Vector2 center,int type)
        {int side=type==717 || type==718?240:128;return AttackIntercept.BodyContact(timeline,tick,0,aim.X,aim.Y,center.X,center.Y,side,side,AttackConfidence.Representative);}
        private static void Gravity(int stage,int age,ref float vy)
        {
            if(stage==1 && age==10)vy-=10;
            if(stage==0 && age>=20)vy+=.12f;else if(stage==1 && age>=10)vy+=.25f;else if(stage==2 && age>=60)vy+=.15f;else if(stage==3 && age>=25)vy+=.15f;else if(stage==4 && age>=10)vy+=.04f;else if(stage==5 && age>=40)vy+=.08f;else if(stage==6 && age>=30)vy+=.1f;
            if(vy>16)vy=16;
        }
        private static bool Advance(Projectile shot,NpcTrajectory timeline,int tick,PredictionTerrain terrain,List<Target> targets)
        {
            int stage=(int)shot.ai[0];shot.localAI[0]++;shot.alpha=0;shot.rotation=shot.velocity.ToRotation()+MathHelper.PiOver2;
            if(stage==4)
            {
                if(shot.localAI[0]==1 && shot.ai[1]==1)shot.localAI[0]+=45;
                var unit=shot.velocity.SafeNormalize(Vector2.Zero);float x=unit.RotatedBy(shot.localAI[0]*((float)Math.PI/45)).X;
                shot.position+=unit.RotatedBy(1.5707963705062866)*x*3;shot.scale=2;
            }
            if(stage!=3 || shot.ai[1]==0)Gravity(stage,(int)shot.localAI[0],ref shot.velocity.Y);
            if(stage==0 && shot.localAI[0]>=20)shot.penetrate=shot.maxPenetrate=-1;
            if(stage==1 && shot.localAI[0]>=10)shot.scale=Math.Min(2.5f,shot.scale+.015f);
            if(stage!=3)return true;
            // Capture speed before this stage's gravity, matching native AI.
            // Its first update cannot reach the gravity boundary at 25.
            if(shot.localAI[1]==0)shot.localAI[1]=shot.velocity.Length();
            if(shot.localAI[0]<20)return true;
            if(shot.ai[1]==0)
            {
                float nearest=800;
                foreach(var candidate in targets)
                {
                    if(!candidate.Chaseable)continue;var b=candidate.Slot==timeline.Identity.Slot?timeline[tick].Bounds:candidate.Bounds;
                    float distance=Math.Abs(shot.Center.X-b.CenterX)+Math.Abs(shot.Center.Y-b.CenterY);bool clear;PredictionStop stop;
                    if(distance<nearest && terrain.CanHit(new MotionRect(shot.Center.X,shot.Center.Y,1,1),b,out clear,out stop) && clear){nearest=distance;shot.ai[1]=candidate.Slot+1;}
                }
            }
            if(shot.ai[1]>0)foreach(var candidate in targets)if(candidate.Slot==(int)shot.ai[1]-1 && candidate.KeepLock)
            {
                var b=candidate.Slot==timeline.Identity.Slot?timeline[tick].Bounds:candidate.Bounds;var delta=new Vector2(b.CenterX,b.CenterY)-shot.Center;
                if(Math.Abs(delta.X)+Math.Abs(delta.Y)<1000)shot.velocity=(shot.velocity*7+delta.SafeNormalize(Vector2.Zero)*shot.localAI[1])/8;
                break;
            }
            return true;
        }
    }
}
