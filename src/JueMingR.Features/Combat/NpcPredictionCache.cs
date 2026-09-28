using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // A bounded, game-thread cache for one selected target and its dependency
    // chain. Demand is explicit; Read never samples or simulates. Cancelling a
    // consumer removes only its demand. Published trajectories are immutable.
    public sealed class NpcPredictionCache
    {
        public const int Horizon=120, Capacity=200;
        private readonly int[] demands=new int[4];
        private readonly NpcMotionState[] initial=new NpcMotionState[Capacity],first=new NpcMotionState[Capacity],tail=new NpcMotionState[Capacity],work=new NpcMotionState[Capacity];
        private readonly NpcTrajectoryPoint[] points=new NpcTrajectoryPoint[Horizon+1];
        private int count,selected,length;
        private long tick=-1,version;
        private PredictionEnvironment environment;
        private PredictionStop stop;
        private PredictionAssumption assumptions;
        private NpcTrajectory result;
#if DEBUG
        public int Builds {get;private set;}
        public int Steps {get;private set;}
        public int Reuses {get;private set;}
        public int Rolls {get;private set;}
#endif
        public void Demand(int consumer,int ticks)
        {if(consumer<0 || consumer>=demands.Length || ticks<1 || ticks>Horizon)throw new ArgumentOutOfRangeException();demands[consumer]=ticks;}
        public void Release(int consumer){if(consumer<0 || consumer>=demands.Length)throw new ArgumentOutOfRangeException();demands[consumer]=0;if(Required==0)Clear();}
        public int Required {get{int n=0;for(int i=0;i<demands.Length;i++)n=Math.Max(n,demands[i]);return n;}}
        public NpcTrajectory Read(int consumer){return consumer>=0 && consumer<demands.Length && demands[consumer]>0?result:null;}
        public void Clear(){result=null;count=length=0;tick=-1;Array.Clear(initial,0,initial.Length);Array.Clear(first,0,first.Length);Array.Clear(tail,0,tail.Length);}
        public void EndSession(){Array.Clear(demands,0,demands.Length);Clear();}
        public void Prepare(NpcMotionState[] source,int sourceCount,int chosen,long sampleTick,PredictionEnvironment env,IPredictionTerrain terrain)
        {
            try{PrepareCore(source,sourceCount,chosen,sampleTick,env,terrain);}
            catch{Clear();throw;}
        }
        private void PrepareCore(NpcMotionState[] source,int sourceCount,int chosen,long sampleTick,PredictionEnvironment env,IPredictionTerrain terrain)
        {
            int required=Required;if(required==0)return;
            if(source==null || sourceCount<1 || sourceCount>Capacity || chosen<0 || chosen>=sourceCount){Clear();return;}
            bool same=count==sourceCount && chosen==selected && environment.Equals(env) && terrain.Unchanged;
            bool equal=same && Equal(source,initial,count);
            if(equal && (length>=required || stop!=PredictionStop.None))
            {
                // A new update number alone cannot change a stationary or
                // failed forecast with identical dependencies. Republish its
                // sampling identity without repeating the same failed work.
                if(sampleTick!=tick){tick=sampleTick;result=new NpcTrajectory(source[chosen].Identity,tick,++version,assumptions,stop,points,length+1);}
#if DEBUG
                Reuses++;
#endif
                return;
            }
            bool rolling=same && sampleTick==tick+1 && length>1 && Equal(source,first,count) && stop==PredictionStop.None;
            if(rolling)
            {
                // A native observed step agrees with the prior predicted step.
                // Preserve its future prefix and simulate only the new tail.
                for(int i=1;i<length;i++)points[i]=Rebase(points[i+1],i);
                length--;Array.Copy(source,work,sourceCount);
                PredictionStop ignored;Advance(work,sourceCount,env,terrain,1,out ignored);Array.Copy(work,first,sourceCount);
#if DEBUG
                Rolls++;
#endif
            }
            else if(!equal)
            {
                terrain.Reset();length=0;stop=PredictionStop.None;assumptions=NpcMotion.Assumptions(source[chosen]);
                if(env.Multiplayer)assumptions|=PredictionAssumption.NetworkObservation;
                Array.Copy(source,tail,sourceCount);
#if DEBUG
                Builds++;
#endif
            }
            count=sourceCount;selected=chosen;tick=sampleTick;environment=env;Array.Copy(source,initial,count);points[0]=new NpcTrajectoryPoint(0,source[chosen]);
            while(length<required && stop==PredictionStop.None)
            {
                // Each slot sees already-updated lower slots, exactly the native
                // ordering. Source must be sorted by actual slot, not head-first.
                Array.Copy(tail,work,count);
                if(!Advance(work,count,env,terrain,length+1,out stop))break;
                Array.Copy(work,tail,count);length++;points[length]=new NpcTrajectoryPoint(length,tail[selected]);
                if(length==1)Array.Copy(tail,first,count);
            }
            result=new NpcTrajectory(source[chosen].Identity,tick,++version,assumptions,stop,points,length+1);
        }
        private bool Advance(NpcMotionState[] states,int size,PredictionEnvironment env,IPredictionTerrain terrain,int elapsed,out PredictionStop reason)
        {
            reason=PredictionStop.None;
            for(int i=0;i<size;i++)
            {
#if DEBUG
                Steps++;
#endif
                var n=states[i];if(!NpcMotion.Step(ref n,states,size,env,terrain,elapsed,out reason))return false;states[i]=n;
            }
            return true;
        }
        private static NpcTrajectoryPoint Rebase(NpcTrajectoryPoint p,int offset)
        {return new NpcTrajectoryPoint(offset,new NpcMotionState{X=p.Bounds.X,Y=p.Bounds.Y,Width=(int)p.Bounds.Width,Height=(int)p.Bounds.Height,Vx=p.Vx,Vy=p.Vy,A0=p.Phase,CanReceive=p.CanReceive,CanHarm=p.CanHarm,NewSegment=p.NewSegment});}
        private static bool Equal(NpcMotionState[] a,NpcMotionState[] b,int size)
        {for(int i=0;i<size;i++)if(!Same(a[i],b[i]))return false;return true;}
        public static bool Same(NpcMotionState a,NpcMotionState b)
        {
            if(a.TimeLeft!=b.TimeLeft || a.BuffFingerprint!=b.BuffFingerprint || a.BuffExpires!=b.BuffExpires || a.WaterSpeed!=b.WaterSpeed || a.HoneySpeed!=b.HoneySpeed)return false;
            return a.Identity.Equals(b.Identity) && a.X==b.X && a.Y==b.Y && a.Vx==b.Vx && a.Vy==b.Vy && a.OldVx==b.OldVx && a.OldVy==b.OldVy && a.Scale==b.Scale && a.A0==b.A0 && a.A1==b.A1 && a.A2==b.A2 && a.A3==b.A3 && a.L0==b.L0 && a.L1==b.L1 && a.L2==b.L2 && a.L3==b.L3 && a.Width==b.Width && a.Height==b.Height && a.Style==b.Style && a.Direction==b.Direction && a.DirectionY==b.DirectionY && a.Target==b.Target && a.ParentSlot==b.ParentSlot && a.ConfusedTicks==b.ConfusedTicks && a.Life==b.Life && a.LifeMax==b.LifeMax && a.Active==b.Active && a.NoGravity==b.NoGravity && a.NoTileCollide==b.NoTileCollide && a.Wet==b.Wet && a.Honey==b.Honey && a.CollideX==b.CollideX && a.CollideY==b.CollideY && a.CanReceive==b.CanReceive && a.CanHarm==b.CanHarm && a.JustHit==b.JustHit;
        }
    }
}
