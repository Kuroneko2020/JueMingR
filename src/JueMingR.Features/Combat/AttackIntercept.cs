using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    public delegate bool AttackPassage(float x,float y,float nextX,float nextY,float width,float height);
    public sealed class AttackContact
    {
        public readonly NpcTrajectory Timeline;
        public readonly float AimX,AimY,ImpactX,ImpactY;
        public readonly int Tick,ReceiveTick,Subupdate,Delay;
        public readonly AttackConfidence Confidence;
        internal AttackContact(NpcTrajectory timeline,float aimX,float aimY,float impactX,float impactY,int tick,int subupdate,int delay,AttackConfidence confidence,int receiveTick=-1)
        {Timeline=timeline;AimX=aimX;AimY=aimY;ImpactX=impactX;ImpactY=impactY;Tick=tick;ReceiveTick=receiveTick<0?tick:receiveTick;Subupdate=subupdate;Delay=delay;Confidence=confidence;}
    }
    public static class AttackIntercept
    {
        // A derived body's contact belongs to the parent's aimed use. Keep
        // its receive time/position while carrying the actual parent input.
        public static AttackContact WithAimInput(AttackContact contact,float x,float y)
        {return contact==null?null:new AttackContact(contact.Timeline,x,y,contact.ImpactX,contact.ImpactY,contact.Tick,contact.Subupdate,contact.Delay,contact.Confidence,contact.ReceiveTick);}
        public static AttackContact WithLaunchDelay(AttackContact contact,int delay)
        {
            if(contact==null || delay<0)return null;
            return new AttackContact(contact.Timeline,contact.AimX,contact.AimY,contact.ImpactX,contact.ImpactY,contact.Tick,contact.Subupdate,contact.Delay+delay,contact.Confidence,contact.ReceiveTick);
        }
        // A new observation may publish the identical relative future with a
        // new sampling identity. Rebind only exact geometry/phase equality;
        // this never moves a contact onto an unrelated path or extends it.
        public static AttackContact Rebind(AttackContact contact,NpcTrajectory timeline)
        {
            if(contact==null || !Equivalent(contact.Timeline,timeline))return null;
            return new AttackContact(timeline,contact.AimX,contact.AimY,contact.ImpactX,contact.ImpactY,contact.Tick,contact.Subupdate,contact.Delay,contact.Confidence,contact.ReceiveTick);
        }
        public static bool Equivalent(NpcTrajectory a,NpcTrajectory b)
        {
            if(a==null || b==null || !a.Identity.Equals(b.Identity) || a.Count!=b.Count || a.Assumptions!=b.Assumptions || a.Stop!=b.Stop || a.Strategy!=b.Strategy || a.Quality!=b.Quality)return false;
            for(int i=0;i<a.Count;i++)
            {
                var x=a[i];var y=b[i];
                if(!PredictionPlayers.Same(x.Bounds,y.Bounds) || !PredictionPlayers.Same(x.OwnerBounds,y.OwnerBounds) || !PredictionPlayers.Same(x.ProjectileReceiveBounds,y.ProjectileReceiveBounds) || x.CanReceive!=y.CanReceive || x.CanHarm!=y.CanHarm || x.NoTileCollide!=y.NoTileCollide || x.Wet!=y.Wet || x.ReflectsProjectiles!=y.ReflectsProjectiles || x.ProjectileReflectionUnknown!=y.ProjectileReflectionUnknown || x.NewSegment!=y.NewSegment || x.Phase!=y.Phase || x.Vx!=y.Vx || x.Vy!=y.Vy)return false;
            }
            return true;
        }
        // Native flail Colliding receives the integer NPC projectile rectangle,
        // including netOffset and type414's extension. Its closest-point ellipse
        // is separate from owner-hit-check's floating body and melee's body box.
        public static AttackContact EllipseContact(NpcTrajectory timeline,int tick,float aimX,float aimY,float x,float y,float radius,float upperScale,float lowerScale)
        {
            if(timeline==null || tick<0 || tick>=timeline.Count || radius<=0 || upperScale<=0 || lowerScale<=0 || !Finite(x) || !Finite(y) || !Finite(aimX) || !Finite(aimY))return null;
            var sample=timeline[tick];if(!sample.CanReceive)return null;var b=sample.ProjectileReceiveBounds;float px=Math.Max(b.X,Math.Min(b.X+b.Width,x)),py=Math.Max(b.Y,Math.Min(b.Y+b.Height,y));float dx=px-x,dy=(py-y)/(py>y?lowerScale:upperScale);
            return dx*dx+dy*dy<=radius*radius?new AttackContact(timeline,aimX,aimY,px,py,tick,0,0,AttackConfidence.Conditional):null;
        }
        // Locked native finite strip/rectangle mathematics. A target's edge
        // can intersect while its centre remains outside; centre-only tests
        // incorrectly reject arm-offset melee and finite beam contacts.
        public static AttackContact LineContact(NpcTrajectory timeline,int tick,float aimX,float aimY,float x,float y,float endX,float endY,float width,AttackConfidence confidence=AttackConfidence.Conditional)
        {
            if(timeline==null || tick<0 || tick>=timeline.Count || width<=0 || !Finite(aimX) || !Finite(aimY) || !Finite(x) || !Finite(y) || !Finite(endX) || !Finite(endY))return null;
            var sample=timeline[tick];if(!sample.CanReceive)return null;var b=sample.ProjectileReceiveBounds;
            float dx=endX-x,dy=endY-y,half=width*.5f;
            if(b.X+b.Width<=Math.Min(x,endX)-half || b.X>=Math.Max(x,endX)+half || b.Y+b.Height<=Math.Min(y,endY)-half || b.Y>=Math.Max(y,endY)+half)return null;
            float length=(float)Math.Sqrt(dx*dx+dy*dy);if(length<=0)return null;
            double angle=Math.Atan2(dy,dx),cos=Math.Cos(angle),sin=Math.Sin(angle);float nearest=length,impactX=0,impactY=0;bool found=false;
            for(int corner=0;corner<4;corner++)
            {
                float px=b.X+(corner==1 || corner==2?b.Width:0)-x,py=b.Y+(corner>=2?b.Height:0)-y;
                float cx=(float)(px*cos+py*sin),cy=(float)(py*cos-px*sin);
                if(Math.Abs(cy)<half && cx<nearest && cx>=0){nearest=cx;impactX=cx;impactY=cy;found=true;}
            }
            for(int edge=0;edge<4;edge++)
            {
                int next=(edge+1)%4;float px=b.X+(edge==1 || edge==2?b.Width:0)-x,py=b.Y+(edge>=2?b.Height:0)-y;
                float qx=b.X+(next==1 || next==2?b.Width:0)-x,qy=b.Y+(next>=2?b.Height:0)-y;
                float cx=(float)(px*cos+py*sin),cy=(float)(py*cos-px*sin),ex=(float)(qx*cos+qy*sin)-cx,ey=(float)(qy*cos-qx*sin)-cy;
                if(ey==0)continue;
                for(int side=-1;side<=1;side+=2)
                {
                    float u=(side*half-cy)/ey,t=(cx+u*ex)/length;
                    if(t<0 || t>1 || u<0 || u>1)continue;found=true;
                    if(t*length<=nearest){nearest=t*length;impactX=nearest;impactY=side*half;}
                }
            }
            if(!found)return null;
            float worldX=x+(float)(impactX*cos-impactY*sin),worldY=y+(float)(impactX*sin+impactY*cos);
            return new AttackContact(timeline,aimX,aimY,Math.Max(b.X,Math.Min(b.X+b.Width,worldX)),Math.Max(b.Y,Math.Min(b.Y+b.Height,worldY)),tick,0,0,confidence);
        }
        // Specialized native motors can supply their own finite replayed body.
        // Player melee damages before NPC movement. Its receive sample and
        // absolute damage tick therefore differ by one; never substitute the
        // next NPC position merely to align the display's damage clock.
        public static AttackContact MeleeContact(NpcTrajectory timeline,int receiveTick,float x,float y,float width,float height,float aimX,float aimY)
        {
            if(timeline==null || receiveTick<0 || receiveTick>=timeline.Count || width<=0 || height<=0 || !Finite(x) || !Finite(y) || !Finite(width) || !Finite(height) || !Finite(aimX) || !Finite(aimY))return null;
            var sample=timeline[receiveTick];var b=sample.ReceiveBounds;
            if(!sample.CanReceive || x>=b.X+b.Width || x+width<=b.X || y>=b.Y+b.Height || y+height<=b.Y)return null;
            return new AttackContact(timeline,aimX,aimY,Math.Max(b.X,Math.Min(b.X+b.Width,x+width/2)),Math.Max(b.Y,Math.Min(b.Y+b.Height,y+height/2)),receiveTick+1,0,0,AttackConfidence.Conditional,receiveTick);
        }
        // The host has already checked the native melee shape against this
        // receive sample. Preserve its phase without replacing that shape
        // with a second rectangle approximation.
        public static AttackContact MeleeShapeContact(NpcTrajectory timeline,int receiveTick,bool overlaps,float aimX,float aimY,float impactX,float impactY)
        {
            if(!overlaps || timeline==null || receiveTick<0 || receiveTick>=timeline.Count || !Finite(aimX) || !Finite(aimY) || !Finite(impactX) || !Finite(impactY))return null;
            var sample=timeline[receiveTick];if(!sample.CanReceive)return null;var b=sample.ReceiveBounds;
            return new AttackContact(timeline,aimX,aimY,Math.Max(b.X,Math.Min(b.X+b.Width,impactX)),Math.Max(b.Y,Math.Min(b.Y+b.Height,impactY)),receiveTick+1,0,0,AttackConfidence.Conditional,receiveTick);
        }
        // Contact creation still enforces the SAME timeline, receive phase
        // and native integer rectangle; an arbitrary aim point is insufficient.
        public static AttackContact BodyContact(NpcTrajectory timeline,int tick,int subupdate,float aimX,float aimY,float x,float y,float width,float height,AttackConfidence confidence)
        {
            if(timeline==null || tick<0 || tick>=timeline.Count || subupdate<0 || !Finite(aimX) || !Finite(aimY) || !Finite(x) || !Finite(y) || width<=0 || height<=0)return null;
            var sample=timeline[tick];if(!sample.CanReceive || !Intersects(x,y,width,height,sample.ProjectileReceiveBounds))return null;
            var b=sample.ProjectileReceiveBounds;return new AttackContact(timeline,aimX,aimY,Math.Max(b.X,Math.Min(b.X+b.Width,x)),Math.Max(b.Y,Math.Min(b.Y+b.Height,y)),tick,subupdate,0,confidence);
        }
        // A specialized consumer may have a cursor-dependent birth position.
        // Replay its explicit launch without reinterpreting the cursor as a
        // muzzle ray. The exact same timeline/time and discrete boxes apply.
        public static AttackContact Replay(float aimX,float aimY,float x,float y,float vx,float vy,AttackMotion motion,NpcTrajectory timeline,int age,AttackPassage passage=null,Func<int,bool> receive=null,Func<int,int,float,float,bool> damagePassage=null,Func<int,int,float,float,bool> beforeMotion=null)
        {
            if(timeline==null || age<0 || age>=timeline.Count-1)return null;
            motion=motion.ForLaunch(vx);motion.Launch(ref vx,ref vy);int limit=Math.Min(motion.Lifetime,motion.SubupdatesForOuters(timeline.Count-age-1));
            for(int k=1;k<=limit;k++)
            {
                int t=age+motion.OuterAtSubupdate(k);
                if(beforeMotion!=null && !beforeMotion(k,t,x,y))return null;
                float oldX=x,oldY=y;motion.Advance(ref x,ref y,ref vx,ref vy,k);
                if(passage!=null && !passage(oldX,oldY,x,y,motion.Width,motion.Height))return null;
                var sample=timeline[t];
                if(damagePassage!=null && !damagePassage(k,t,x,y))return null;
                if(sample.CanReceive && (receive==null || receive(motion.OuterAtSubupdate(k))) && Intersects(x,y,motion.Width,motion.Height,sample.ProjectileReceiveBounds))
                {var b=sample.ProjectileReceiveBounds;return new AttackContact(timeline,aimX,aimY,Math.Max(b.X,Math.Min(b.X+b.Width,x)),Math.Max(b.Y,Math.Min(b.Y+b.Height,y)),t,motion.SubupdateInOuter(k),0,motion.Confidence);}
            }
            return null;
        }
        // One selected timeline and a finite <=120*16 candidate search. No NPC
        // sampling, native AI, resource consumption or random calls occur here.
        public static AttackContact Solve(float originX,float originY,AttackMotion motion,NpcTrajectory timeline,int age,int delay,AttackPassage passage=null,int firstTickOffset=1,Func<int,bool> receive=null,Func<int,int,float,float,bool> damagePassage=null,Func<int,int,float,float,bool> beforeMotion=null)
        {
            if(!motion.GravityClockUsesLaunchX)return SolveCore(originX,originY,motion,timeline,age,delay,passage,firstTickOffset,receive,damagePassage,null,beforeMotion);
            if(timeline==null || age<0 || delay<0 || firstTickOffset<0 || firstTickOffset>1 || age+delay>=timeline.Count)return null;
            int limit=Math.Min(motion.Lifetime,motion.SubupdatesForOuters(timeline.Count-age-delay-firstTickOffset));if(limit<=0)return null;
            // Launch X changes an integer gravity-start class. Enumerate its
            // finite reachable classes so radial inversion and edge seeds
            // use the SAME clock as replay. All starts after the horizon are
            // one no-gravity class; ordinary shots retain the single search.
            int first=(int)Math.Max(1,Math.Min(limit+1,Math.Ceiling((double)motion.GravityStart-motion.Speed)));
            int last=(int)Math.Max(1,Math.Min(limit+1,Math.Ceiling((double)motion.GravityStart+motion.Speed)));
            AttackContact best=null;
            for(int start=first;start<=last;start++)
            {
                var candidate=SolveCore(originX,originY,motion.ForLaunch(motion.GravityStart-start),timeline,age,delay,passage,firstTickOffset,receive,damagePassage,motion,beforeMotion);
                if(candidate!=null && (best==null || candidate.Tick<best.Tick || candidate.Tick==best.Tick && candidate.Subupdate<best.Subupdate))best=candidate;
            }
            return best;
        }
        private static AttackContact SolveCore(float originX,float originY,AttackMotion motion,NpcTrajectory timeline,int age,int delay,AttackPassage passage,int firstTickOffset,Func<int,bool> receive,Func<int,int,float,float,bool> damagePassage,AttackMotion? launchClock,Func<int,int,float,float,bool> beforeMotion)
        {
            if(timeline==null || age<0 || delay<0 || firstTickOffset<0 || firstTickOffset>1 || age+delay>=timeline.Count || !Finite(originX) || !Finite(originY))return null;
            int limit=Math.Min(motion.Lifetime,motion.SubupdatesForOuters(timeline.Count-age-delay-firstTickOffset));
            float basis=motion.Acceleration==1?1:motion.Speed;
            float gx=0,gy=0,gvx=0,gvy=0,ax=0,ay=0,avx=basis,avy=0;
            for(int step=1;step<=limit;step++)
            {
                motion.Advance(ref gx,ref gy,ref gvx,ref gvy,step);motion.Advance(ref ax,ref ay,ref avx,ref avy,step);
                int tick=age+delay+motion.OuterAtSubupdate(step)-1+firstTickOffset;
                var target=timeline[tick];if(!target.CanReceive || receive!=null && !receive(delay+motion.OuterAtSubupdate(step)))continue;
                // Linear velocity response permits an exact direction seed for
                // delayed constant gravity. Acceleration is verified by replay;
                // a nonlinear/clamped miss never fabricates a contact.
                float response=(ax-gx)/basis;if(response<=0)continue;
                float responseY=motion.HorizontalAccelerationOnly?step*motion.MoveScale:response;
                float dx=(target.ProjectileReceiveBounds.CenterX-originX-gx)/response;
                float dy=(target.ProjectileReceiveBounds.CenterY-originY-gy)/responseY;
                float length=(float)Math.Sqrt(dx*dx+dy*dy);if(length<=0 || !Finite(length))continue;
                var rectangle=target.ProjectileReceiveBounds;
                float extent=(float)Math.Sqrt((rectangle.Width+motion.Width)*(rectangle.Width+motion.Width)+(rectangle.Height+motion.Height)*(rectangle.Height+motion.Height))*.5f;
                float launchX=dx/length*motion.Speed,launchY=dy/length*motion.Speed;motion.Launch(ref launchX,ref launchY);
                float launchSpeed=(float)Math.Sqrt(launchX*launchX+launchY*launchY);
                if(Math.Abs(length-launchSpeed)*Math.Min(response,responseY)>extent)continue;
                bool elliptical=motion.HorizontalAccelerationOnly;float radius=launchSpeed*(elliptical?1:response);
                // A centre ray can miss a small translated receive rectangle
                // even when the fixed-speed circle crosses one of its edges.
                // Use at most eight circle/inner-edge intersections as well;
                // these are finite geometric candidates, not a finer NPC replay.
                for(int seed=-1;seed<8;seed++)
                {
                    float sx=dx*(elliptical?1:response),sy=dy*(elliptical?1:responseY);
                    if(seed>=0)
                    {
                        bool vertical=seed<4;int side=seed%4/2;float edge=vertical?
                            (side==0?rectangle.X-motion.Width/2+1.05f:rectangle.X+rectangle.Width+motion.Width/2-.05f)-originX-gx:
                            (side==0?rectangle.Y-motion.Height/2+1.05f:rectangle.Y+rectangle.Height+motion.Height/2-.05f)-originY-gy;
                        // Horizontal-only damping maps a launch-speed circle
                        // to a displacement ellipse. Intersect its edges in
                        // launch space, then replay the native finite motor.
                        if(elliptical)edge/=vertical?response:responseY;
                        float squared=radius*radius-edge*edge;if(squared<0)continue;
                        float other=(float)Math.Sqrt(squared)*(seed%2==0?1:-1);sx=vertical?edge:other;sy=vertical?other:edge;
                        if(!Intersects(originX+gx+sx*(elliptical?response:1),originY+gy+sy*(elliptical?responseY:1),motion.Width,motion.Height,rectangle))continue;
                    }
                    float norm=(float)Math.Sqrt(sx*sx+sy*sy);if(norm<=0)continue;
                    float vx=sx/norm*motion.Speed,vy=sy/norm*motion.Speed,x=originX,y=originY;
                    if(launchClock.HasValue && Math.Min(limit+1,launchClock.Value.ForLaunch(vx).GravityStart)!=motion.GravityStart)continue;
                    // Screen coordinates are integral at the true consumer.
                    // A long direction point keeps that rounding below the
                    // inner-edge margin without changing projectile speed.
                    float aimX=originX+vx*1000,aimY=originY+vy*1000;
                    motion.Launch(ref vx,ref vy);
                for(int k=1;k<=step;k++)
                {
                    int t=age+delay+motion.OuterAtSubupdate(k)-1+firstTickOffset;
                    // The host can test a preceding native NPC-phase action
                    // against the old projectile body. Damage's endpoint
                    // callback cannot reconstruct that position afterwards.
                    if(beforeMotion!=null && !beforeMotion(k,t,x,y))break;
                    float oldX=x,oldY=y;motion.Advance(ref x,ref y,ref vx,ref vy,k);
                    if(passage!=null && !passage(oldX,oldY,x,y,motion.Width,motion.Height))break;
                    var sample=timeline[t];
                    // The host may reject a native damage endpoint consumed
                    // by an earlier qualified NPC. k==1 begins a new candidate;
                    // this callback neither samples nor advances an NPC world.
                    if(damagePassage!=null && !damagePassage(k,t,x,y))break;
                    // Native ordinary projectiles hit with their translated
                    // rectangle at each subupdate, not an infinite swept ray.
                    if(sample.CanReceive && (receive==null || receive(delay+motion.OuterAtSubupdate(k))) && Intersects(x,y,motion.Width,motion.Height,sample.ProjectileReceiveBounds))
                    {
                        var box=sample.ProjectileReceiveBounds;
                        float impactX=Math.Max(box.X,Math.Min(box.X+box.Width,x)),impactY=Math.Max(box.Y,Math.Min(box.Y+box.Height,y));
                        return new AttackContact(timeline,aimX,aimY,impactX,impactY,t,motion.SubupdateInOuter(k),delay,motion.Confidence);
                    }
                }
                }
            }
            return null;
        }
        private static bool Intersects(float x,float y,float width,float height,MotionRect b)
        {
            // Vanilla Damage_GetHitbox truncates the projectile's top-left
            // before Rectangle.Intersects. Keep that separate from mouse
            // rounding; a floating rectangle invents contacts at thin edges.
            int left=(int)(x-width/2),top=(int)(y-height/2);
            return left+width>b.X && left<b.X+b.Width && top+height>b.Y && top<b.Y+b.Height;
        }
        private static bool Finite(float n){return !float.IsNaN(n) && !float.IsInfinity(n);}
    }
}
