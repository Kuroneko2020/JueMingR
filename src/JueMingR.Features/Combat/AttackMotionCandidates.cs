using System;
using System.Collections.Generic;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Input proposals for the deterministic, separable part of AttackMotion.
    // Unlike Solve these do not replay a projectile from birth for every
    // possible receive time. The host still owns terrain, OnHit, immunity,
    // reflections, fuses and children, and must validate the actual motor.
    public static class AttackMotionCandidates
    {
        public static bool Supports(AttackMotion motion)
        {
            return motion.Speed>0 && motion.MaxSpeed<=0 && !motion.StopSmallVelocity &&
                !motion.GravityClockUsesLaunchX && (!motion.ComponentBirthLimit || motion.Speed<16) &&
                (motion.Acceleration==1 || motion.HorizontalAccelerationOnly) &&
                (motion.Gravity>=0 ? motion.MaxRiseSpeed==0 || motion.Speed<=motion.MaxRiseSpeed : motion.MaxFallSpeed==0 || motion.Speed<=motion.MaxFallSpeed);
        }
        private sealed class Proposal
        {
            internal double X,Y;internal int Tick;internal bool Center;
        }
        public static AttackBallisticCandidates.Direction[] Generate(NpcTrajectory path,int age,float originX,float originY,AttackMotion motion)
        {
            if(path==null || age< -1 || age+1>=path.Count || !Supports(motion))return new AttackBallisticCandidates.Direction[0];
            int limit=Math.Min(motion.Lifetime,motion.SubupdatesForOuters(path.Count-age-1));
            var rows=new Dictionary<int,Proposal>();
            double factor=1,vx=0,response=0,drift=0;
            for(int step=1;step<=limit;step++)
            {
                if((step&15)==1)CombatPredictionWork.Check();
                vx+=motion.PreMotorWindX;
                if(step>=motion.AccelerationStart){factor*=motion.Acceleration;vx*=motion.Acceleration;}
                vx+=motion.WindX;response+=factor*motion.MoveScale;drift+=vx*motion.MoveScale;
                int tick=age+motion.OuterAtSubupdate(step);var point=path[tick];if(!point.CanReceive || response<=0)continue;
                var box=point.ProjectileReceiveBounds;
                // Inverse the expanded receiver, rather than assuming that
                // its centre intersects the fixed-speed launch circle. The
                // final native integer rectangle remains the host's test.
                double left=(box.X-motion.Width*.5+1.05-originX-drift)/response;
                double right=(box.X+box.Width+motion.Width*.5-.05-originX-drift)/response;
                double bottom=box.Y-motion.Height*.5+1.05-originY,top=box.Y+box.Height+motion.Height*.5-.05-originY;
                double low=InverseY(motion,step,bottom),high=InverseY(motion,step,top);
                double cx=(box.CenterX-originX-drift)/response,cy=InverseY(motion,step,box.CenterY-originY);
                double length=Math.Sqrt(cx*cx+cy*cy);
                if(length>0)Add(rows,cx/length*motion.Speed,cy/length*motion.Speed,tick,left,right,low,high,motion.Speed,true);
                for(int edge=0;edge<4;edge++)
                {
                    bool vertical=edge<2;double value=vertical?(edge==0?left:right):(edge==2?low:high);
                    double square=motion.Speed*motion.Speed-value*value;if(square<0)continue;
                    double other=Math.Sqrt(square);
                    Add(rows,vertical?value:other,vertical?other:value,tick,left,right,low,high,motion.Speed);
                    Add(rows,vertical?value:-other,vertical?-other:value,tick,left,right,low,high,motion.Speed);
                }
            }
            // Reserve a separate late-arc set. An arbitrary moving NPC can
            // have several disconnected receive intervals; earliest nearby
            // angles must not crowd out a high arc behind an obstruction.
            var result=new List<AttackBallisticCandidates.Direction>(8);
            Select(rows,result,false,4);Select(rows,result,true,8);
            return result.ToArray();
        }
        private static void Add(Dictionary<int,Proposal> rows,double vx,double vy,int tick,double left,double right,double low,double high,float speed,bool center=false)
        {
            const double epsilon=.00001;
            if(vx<left-epsilon || vx>right+epsilon || vy<low-epsilon || vy>high+epsilon)return;
            double x=vx/speed,y=vy/speed;
            const int buckets=2048;int bucket=(int)((Math.Atan2(y,x)+Math.PI)*buckets/(2*Math.PI));if(bucket==buckets)bucket=0;
            for(int offset=-2;offset<=2;offset++)
            {
                Proposal row;if(!rows.TryGetValue((bucket+offset+buckets)%buckets,out row) || row.X*x+row.Y*y<=.99999)continue;
                // A direction is replayed over the complete finite horizon,
                // so one copy covers every later contact for that direction.
                if(center && !row.Center){row.X=x;row.Y=y;row.Tick=tick;row.Center=true;}
                else if(center==row.Center && tick<row.Tick)row.Tick=tick;return;
            }
            Proposal previous;if(!rows.TryGetValue(bucket,out previous) || center && !previous.Center || center==previous.Center && tick<previous.Tick)rows[bucket]=new Proposal{X=x,Y=y,Tick=tick,Center=center};
        }
        private static void Select(Dictionary<int,Proposal> rows,List<AttackBallisticCandidates.Direction> result,bool latest,int count)
        {
            while(result.Count<count)
            {
                Proposal best=null;
                foreach(var row in rows.Values)
                {
                    // Body-centre solutions keep native spread margin. Early
                    // rectangle-edge solutions must not consume every slot
                    // before a central low/high arc has even been replayed.
                    if(best!=null && (!row.Center && best.Center || row.Center==best.Center && (latest?row.Tick<=best.Tick:row.Tick>=best.Tick)))continue;
                    bool duplicate=false;foreach(var prior in result)if(row.X*prior.X+row.Y*prior.Y>.9998){duplicate=true;break;}
                    if(!duplicate)best=row;
                }
                if(best==null)return;
                result.Add(new AttackBallisticCandidates.Direction(best.X,best.Y,best.Tick,0));
            }
        }
        private static double InverseY(AttackMotion motion,int step,double position)
        {
            double low=-motion.Speed,high=motion.Speed;
            // Values outside launch reach stay outside the circle instead of
            // being clamped into an invented reachable boundary.
            if(position<PositionY(motion,step,low))return low-1;
            if(position>PositionY(motion,step,high))return high+1;
            int active=Math.Max(0,step-Math.Max(1,motion.GravityStart)+1);
            double direct=(position/motion.MoveScale-motion.Gravity*active*(active+1)*.5)/step;
            if(Math.Abs(PositionY(motion,step,direct)-position)<.0000001)return direct;
            for(int i=0;i<24;i++)
            {double mid=(low+high)*.5;if(PositionY(motion,step,mid)<position)low=mid;else high=mid;}
            return (low+high)*.5;
        }
        private static double PositionY(AttackMotion motion,int step,double velocity)
        {
            int start=Math.Max(1,motion.GravityStart),free=step;
            double gravity=motion.Gravity;
            if(gravity==0)return step*Math.Max(motion.MaxRiseSpeed>0?-motion.MaxRiseSpeed:double.MinValue,Math.Min(motion.MaxFallSpeed>0?motion.MaxFallSpeed:double.MaxValue,velocity))*motion.MoveScale;
            if(gravity>0 && motion.MaxFallSpeed>0)
            {
                if(velocity>motion.MaxFallSpeed)return step*motion.MaxFallSpeed*motion.MoveScale;
                free=Math.Min(step,Math.Max(0,start-1+(int)Math.Floor((motion.MaxFallSpeed-velocity)/gravity)));
            }
            else if(gravity<0 && motion.MaxRiseSpeed>0)
            {
                if(velocity< -motion.MaxRiseSpeed)return -step*motion.MaxRiseSpeed*motion.MoveScale;
                free=Math.Min(step,Math.Max(0,start-1+(int)Math.Floor((-motion.MaxRiseSpeed-velocity)/gravity)));
            }
            int affected=Math.Max(0,free-start+1);
            double displacement=free*velocity+gravity*affected*(affected+1)*.5;
            if(free<step)displacement+=(step-free)*(gravity>0?motion.MaxFallSpeed:-motion.MaxRiseSpeed);
            return displacement*motion.MoveScale;
        }
    }
}
