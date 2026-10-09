using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    public delegate bool AttackPassage(float x,float y,float nextX,float nextY,float width,float height);
    public sealed class AttackContact
    {
        public readonly NpcTrajectory Timeline;
        public readonly float AimX,AimY,ImpactX,ImpactY;
        public readonly int Tick,Subupdate,Delay;
        public readonly AttackConfidence Confidence;
        internal AttackContact(NpcTrajectory timeline,float aimX,float aimY,float impactX,float impactY,int tick,int subupdate,int delay,AttackConfidence confidence)
        {Timeline=timeline;AimX=aimX;AimY=aimY;ImpactX=impactX;ImpactY=impactY;Tick=tick;Subupdate=subupdate;Delay=delay;Confidence=confidence;}
    }
    public static class AttackIntercept
    {
        // Native flail spin tests the closest point in the actual NPC body
        // against an owner-centred asymmetric ellipse. It has no projectile
        // integer-box conversion; wrapping this edge in a one-pixel rectangle
        // would lose a legal edge after integer truncation.
        public static AttackContact EllipseContact(NpcTrajectory timeline,int tick,float aimX,float aimY,float x,float y,float radius,float upperScale,float lowerScale)
        {
            if(timeline==null || tick<0 || tick>=timeline.Count || radius<=0 || upperScale<=0 || lowerScale<=0 || !Finite(x) || !Finite(y) || !Finite(aimX) || !Finite(aimY))return null;
            var sample=timeline[tick];if(!sample.CanReceive)return null;var b=sample.Bounds;float px=Math.Max(b.X,Math.Min(b.X+b.Width,x)),py=Math.Max(b.Y,Math.Min(b.Y+b.Height,y));float dx=px-x,dy=(py-y)/(py>y?lowerScale:upperScale);
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
            return new AttackContact(timeline,aimX,aimY,Math.Max(b.X,Math.Min(b.X+b.Width,x+width/2)),Math.Max(b.Y,Math.Min(b.Y+b.Height,y+height/2)),receiveTick+1,0,0,AttackConfidence.Conditional);
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
        public static AttackContact Replay(float aimX,float aimY,float x,float y,float vx,float vy,AttackMotion motion,NpcTrajectory timeline,int age,AttackPassage passage=null,Func<int,bool> receive=null)
        {
            if(timeline==null || age<0 || age>=timeline.Count-1)return null;
            motion.Launch(ref vx,ref vy);int limit=Math.Min(motion.Lifetime,(timeline.Count-age-1)*motion.Updates);
            for(int k=1;k<=limit;k++)
            {
                float oldX=x,oldY=y;motion.Advance(ref x,ref y,ref vx,ref vy,k);
                if(passage!=null && !passage(oldX,oldY,x,y,motion.Width,motion.Height))return null;
                int t=age+(k-1)/motion.Updates+1;var sample=timeline[t];
                if(sample.CanReceive && (receive==null || receive((k-1)/motion.Updates+1)) && Intersects(x,y,motion.Width,motion.Height,sample.ProjectileReceiveBounds))
                {var b=sample.ProjectileReceiveBounds;return new AttackContact(timeline,aimX,aimY,Math.Max(b.X,Math.Min(b.X+b.Width,x)),Math.Max(b.Y,Math.Min(b.Y+b.Height,y)),t,(k-1)%motion.Updates,0,motion.Confidence);}
            }
            return null;
        }
        // One selected timeline and a finite <=120*16 candidate search. No NPC
        // sampling, native AI, resource consumption or random calls occur here.
        public static AttackContact Solve(float originX,float originY,AttackMotion motion,NpcTrajectory timeline,int age,int delay,AttackPassage passage=null,int firstTickOffset=1,Func<int,bool> receive=null)
        {
            if(timeline==null || age<0 || delay<0 || firstTickOffset<0 || firstTickOffset>1 || age+delay>=timeline.Count || !Finite(originX) || !Finite(originY))return null;
            int limit=Math.Min(motion.Lifetime,(timeline.Count-age-delay-firstTickOffset)*motion.Updates);
            float basis=motion.Acceleration==1?1:motion.Speed;
            float gx=0,gy=0,gvx=0,gvy=0,ax=0,ay=0,avx=basis,avy=0;
            for(int step=1;step<=limit;step++)
            {
                motion.Advance(ref gx,ref gy,ref gvx,ref gvy,step);motion.Advance(ref ax,ref ay,ref avx,ref avy,step);
                int tick=age+delay+(step-1)/motion.Updates+firstTickOffset;
                var target=timeline[tick];if(!target.CanReceive || receive!=null && !receive(delay+(step-1)/motion.Updates+1))continue;
                // Linear velocity response permits an exact direction seed for
                // delayed constant gravity. Acceleration is verified by replay;
                // a nonlinear/clamped miss never fabricates a contact.
                float response=(ax-gx)/basis;if(response<=0)continue;
                float dx=(target.ProjectileReceiveBounds.CenterX-originX-gx)/response;
                float dy=(target.ProjectileReceiveBounds.CenterY-originY-gy)/response;
                float length=(float)Math.Sqrt(dx*dx+dy*dy);if(length<=0 || !Finite(length))continue;
                var rectangle=target.ProjectileReceiveBounds;
                float extent=(float)Math.Sqrt((rectangle.Width+motion.Width)*(rectangle.Width+motion.Width)+(rectangle.Height+motion.Height)*(rectangle.Height+motion.Height))*.5f;
                float launchX=dx/length*motion.Speed,launchY=dy/length*motion.Speed;motion.Launch(ref launchX,ref launchY);
                float launchSpeed=(float)Math.Sqrt(launchX*launchX+launchY*launchY);
                if(Math.Abs(length-launchSpeed)*response>extent)continue;
                float radius=launchSpeed*response;
                // A centre ray can miss a small translated receive rectangle
                // even when the fixed-speed circle crosses one of its edges.
                // Use at most eight circle/inner-edge intersections as well;
                // these are finite geometric candidates, not a finer NPC replay.
                for(int seed=-1;seed<8;seed++)
                {
                    float sx=dx*response,sy=dy*response;
                    if(seed>=0)
                    {
                        bool vertical=seed<4;int side=seed%4/2;float edge=vertical?
                            (side==0?rectangle.X-motion.Width/2+1.05f:rectangle.X+rectangle.Width+motion.Width/2-.05f)-originX-gx:
                            (side==0?rectangle.Y-motion.Height/2+1.05f:rectangle.Y+rectangle.Height+motion.Height/2-.05f)-originY-gy;
                        float squared=radius*radius-edge*edge;if(squared<0)continue;
                        float other=(float)Math.Sqrt(squared)*(seed%2==0?1:-1);sx=vertical?edge:other;sy=vertical?other:edge;
                        if(!Intersects(originX+gx+sx,originY+gy+sy,motion.Width,motion.Height,rectangle))continue;
                    }
                    float norm=(float)Math.Sqrt(sx*sx+sy*sy);if(norm<=0)continue;
                    float vx=sx/norm*motion.Speed,vy=sy/norm*motion.Speed,x=originX,y=originY;
                    // Screen coordinates are integral at the true consumer.
                    // A long direction point keeps that rounding below the
                    // inner-edge margin without changing projectile speed.
                    float aimX=originX+vx*1000,aimY=originY+vy*1000;
                    motion.Launch(ref vx,ref vy);
                for(int k=1;k<=step;k++)
                {
                    float oldX=x,oldY=y;motion.Advance(ref x,ref y,ref vx,ref vy,k);
                    if(passage!=null && !passage(oldX,oldY,x,y,motion.Width,motion.Height))break;
                    int t=age+delay+(k-1)/motion.Updates+firstTickOffset;var sample=timeline[t];
                    // Native ordinary projectiles hit with their translated
                    // rectangle at each subupdate, not an infinite swept ray.
                    if(sample.CanReceive && (receive==null || receive(delay+(k-1)/motion.Updates+1)) && Intersects(x,y,motion.Width,motion.Height,sample.ProjectileReceiveBounds))
                    {
                        var box=sample.ProjectileReceiveBounds;
                        float impactX=Math.Max(box.X,Math.Min(box.X+box.Width,x)),impactY=Math.Max(box.Y,Math.Min(box.Y+box.Height,y));
                        return new AttackContact(timeline,aimX,aimY,impactX,impactY,t,(k-1)%motion.Updates,delay,motion.Confidence);
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
