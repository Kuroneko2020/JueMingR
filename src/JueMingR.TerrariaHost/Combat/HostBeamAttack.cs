using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;

namespace JueMingR.TerrariaHost.Combat
{
    internal static class HostBeamAttack
    {
        private static readonly System.Reflection.MethodInfo copy=typeof(object).GetMethod("MemberwiseClone",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        internal sealed class Child
        {
            internal readonly Projectile Shot;internal readonly ProjectileKey Key,Parent;
            internal Child(Projectile shot){Shot=shot;Key=shot.key;Parent=(ProjectileKey)shot.ai[1];}
            internal bool Valid(Projectile parent){return Shot.active && Shot.key.Equals(Key) && Parent.Equals(parent.key) && ((ProjectileKey)Shot.ai[1]).Equals(parent.key) && Shot.owner==parent.owner;}
        }
        internal static AttackContact Solve(Player player,Projectile parent,List<Child> children,Vector2 aim,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,out Child receipt)
        {HostPrismSteering.ViewLease view;return SolvePrepared(player,parent,children,aim,timeline,clock,terrain,out receipt,HostAttackPlayerFrame.Capture(player,timeline),out view);}
        internal static AttackContact SolvePrepared(Player player,Projectile parent,List<Child> children,Vector2 aim,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,out Child receipt,HostAttackPlayerFrame frame,out HostPrismSteering.ViewLease viewLease,HostPrismSteering.Camera? preparedCamera=null)
        {
            int age=clock.Age;receipt=null;viewLease=default(HostPrismSteering.ViewLease);
            if(children.Count==0 || !player.channel)return null;
            var receives=new HostAttackReceive[children.Count];for(int i=0;i<receives.Length;i++)receives[i]=HostAttackReceive.Capture(player,children[i].Shot,timeline.Identity.Slot,clock.BeforeNpc,clock.NextWorld);
            Vector2 direction=parent.velocity.SafeNormalize(Vector2.UnitY),mounted=player.RotatedRelativePoint(player.MountedCenter),armOffset=player.GetArmPosition()-mounted;
            var motion=frame.Motion;var environment=frame.Environment;var body=(Player)copy.Invoke(player,null);
            var lengths=new float[children.Count];for(int i=0;i<lengths.Length;i++)lengths[i]=children[i].Shot.localAI[1];
            var fees=new HostAttackResources.FeeClock(player,parent);
            var camera=parent.type==633?preparedCamera??new HostPrismSteering.Camera(player):default(HostPrismSteering.Camera);
            bool scannedRay=false,rayWindowKnown=true;Vector2 scannedOrigin=default(Vector2),scannedUnit=default(Vector2),firstInput=aim;float scannedLength=0;
            for(int step=0;step<120 && clock.FirstTick+step<timeline.Count;step++)
            {
                // A Draw between actions reads the PREVIOUS completed pose.
                // Projectiles/BeforeNpc action0 still reads the existing live
                // viewport. CompletedWorld action0 belongs to the next player.
                if(parent.type==633)
                {
                    if(clock.MovePlayer(step) && !camera.Next(body)){viewLease=step==0?new HostPrismSteering.ViewLease(camera,clock.NextWorld || clock.LaunchDelay>0,true):viewLease.UnknownTail();return null;}
                    if(step==0)viewLease=new HostPrismSteering.ViewLease(camera,clock.NextWorld || clock.LaunchDelay>0);
                }
                // The player's movement is already complete at action sampling;
                // a completed-world preparation predicts the next player's step.
                if(clock.MovePlayer(step)){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,environment,terrain,out stop)){if(parent.type==633)viewLease=viewLease.UnknownTail();return null;}}
                HostAttackPlayerFrame.ApplyPose(body,motion);if(parent.type==633)body.gfxOffY=motion.GfxOffset;Vector2 origin=body.RotatedRelativePoint(body.MountedCenter);armOffset=body.GetArmPosition()-origin;
                // 633 consumes feedback, not a compensating world point held
                // forever. Re-evaluate the same controller at each trusted
                // target slice and predicted holder pose. The loop's timeline
                // bound never manufactures an unknown future tail.
                Vector2 input=aim;
                if(parent.type==633)
                {
                    var bounds=timeline[clock.FirstTick+step].ProjectileReceiveBounds;
                    input=HostPrismSteering.PointInView(origin,direction,new Vector2(bounds.CenterX,bounds.CenterY),body.gravDir,camera.View);
                    if(step==0)firstInput=input;
                    input=HostDirectionalControl.Cursor(body,input,camera.View.Position,camera.View.Height);
                }
                Vector2 requested=(input-origin).SafeNormalize(parent.type==633?-Vector2.UnitY:Vector2.UnitY);
                float phase;bool readsDirection;if(!fees.Advance(out phase,out readsDirection))return null;
                if(parent.type==633)direction=Vector2.Lerp(requested,direction,.92f).SafeNormalize(Vector2.UnitY);
                else if(readsDirection)direction=requested;
                Vector2 parentCenter=parent.type==460?origin+armOffset-direction*(player.HeldItem.shootSpeed*parent.scale):origin;
                for(int i=0;i<children.Count;i++)
                {
                    var child=children[i];if(!child.Valid(parent))continue;
                    Vector2 center,unit;float scale;int nativeAge=clock.FirstTick+step;
                    if(parent.type==633)
                    {
                        if(phase<=30)continue;
                        float index=child.Shot.ai[0]-2.5f,spread=phase<180?1-phase/180:0;
                        float period=phase<120?20-4*(phase/120):phase<180?16-10*((phase-120)/60):1.75f;
                        float lateral=phase<180?20-phase/180*14:6,back=phase<180?-22+phase/180*20:-2;
                        float angle=(phase+index*period)/(period*6)*MathHelper.TwoPi;
                        Vector2 wave=Vector2.UnitY.RotatedBy(angle);
                        center=parentCenter+direction*(16+back)+new Vector2(wave.X*4,wave.Y*lateral).RotatedBy(direction.ToRotation())-new Vector2(0,parent.gfxOffY);
                        unit=direction.RotatedBy(wave.Y*MathHelper.Pi/6*spread);scale=1.4f*(1-spread);
                    }
                    else
                    {
                        if(phase<180 || child.Shot.ai[0]+step+1>=300)continue;
                        center=parentCenter+direction*16-new Vector2(0,parent.gfxOffY);unit=direction;
                        scale=Math.Min(1,(float)Math.Sin((child.Shot.ai[0]+step+1)*Math.PI/300)*10);
                    }
                    // Native LaserScan samples zero-width rays. A pure bounded
                    // trace subtracts two tiles on obstruction; this is a safe
                    // lower bound, not a claim to reproduce every slope scan.
                    Vector2 scan=parent.type==633 && phase>=180?parentCenter:center;
                    // Full-charge prism children share the exact scan ray,
                    // despite separate visible lateral positions and smoothed
                    // lengths. Reuse only exact origin/direction in this one
                    // immutable solve; changing rays or the next prepare scan
                    // afresh. Each child's native length smoothing stays independent.
                    if(!scannedRay || scan!=scannedOrigin || unit!=scannedUnit)
                    {var scanResult=Length(scan,unit,terrain);scannedLength=scanResult.Length;if(parent.type==633)rayWindowKnown&=scanResult.Known;scannedOrigin=scan;scannedUnit=unit;scannedRay=true;}
                    lengths[i]=MathHelper.Lerp(lengths[i],scannedLength,parent.type==633?.75f:.5f);
                    if(!receives[i].Allows(clock.LaunchDelay+step+1))continue;
                    var contact=AttackIntercept.LineContact(timeline,nativeAge,firstInput.X,firstInput.Y,center.X,center.Y,center.X+unit.X*lengths[i],center.Y+unit.Y*lengths[i],22*scale);
                    if(contact!=null){receipt=child;return parent.type==633?AttackIntercept.WithPointSemantics(contact,AttackPointSemantics.Direction):contact;}
                }
                // A known conservative prefix may already have proved a
                // same-action receive above. Otherwise do not carry an
                // unknown scan's private length history into later actions.
                if(parent.type==633 && !rayWindowKnown){viewLease=viewLease.UnknownTail();return null;}
            }
            // A full, naturally exhausted 633 prefix can ask the existing
            // candidate scheduler for a longer NPC window. Early motion/
            // resource/camera exits and exceptions above never claim this.
            if(parent.type==633)viewLease=rayWindowKnown?viewLease.CompleteWindow():viewLease.UnknownTail();
            return null;
        }
        private readonly struct RayScan
        {
            internal readonly float Length;internal readonly bool Known;
            internal RayScan(float length,bool known){Length=length;Known=known;}
        }
        private static RayScan Length(Vector2 origin,Vector2 unit,PredictionTerrain terrain)
        {
            Vector2 old=origin;
            for(int distance=8;distance<=2400;distance+=8)
            {
                Vector2 point=origin+unit*distance;float vx=point.X-old.X,vy=point.Y-old.Y,rx,ry;
                // Same TileContact query and comparisons as ProjectilePassage.
                // Preserve the conservative numeric scan for both beam types,
                // but retain whether its truncation was an actual obstruction
                // or missing terrain before claiming a 633 prefix completed.
                bool known=terrain.ProjectileCollision(old.X,old.Y,vx,vy,1,1,out rx,out ry);
                if(!known || !(Math.Abs(rx-vx)<.0001f && Math.Abs(ry-vy)<.0001f))return new RayScan(Math.Max(0,distance-32),known);
                old=point;
            }
            return new RayScan(2400,true);
        }
    }
}
