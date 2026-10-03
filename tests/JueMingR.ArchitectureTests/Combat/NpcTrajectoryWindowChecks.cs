using System;
using System.Reflection;
using JueMingR.Platform.Combat;

namespace JueMingR.ArchitectureTests
{
    internal static class NpcTrajectoryWindowChecks
    {
        internal static void Run()
        {
            var window=typeof(NpcTrajectory).GetMethod("TryWindow");
            var capture=typeof(NpcTrajectory).GetProperty("CaptureTick");
            Require(window!=null && capture!=null,"Late native results need an immutable current-time window and original capture identity.");
            var identity=new NpcIdentity(11,new object(),7,3,48,48);
            var points=new NpcTrajectoryPoint[181];
            for(int i=0;i<points.Length;i++)
            {
                var state=new NpcMotionState{Identity=new NpcIdentity(11,identity.Token,7,3,414,414),X=100+i*i,Y=200-i,Vx=2*i,Vy=-1,Width=12,Height=18,A0=i,NetOffsetX=3,NetOffsetY=-4,CanReceive=i%2==0,CanHarm=i%3==0,NewSegment=i==40};
                points[i]=new NpcTrajectoryPoint(i,state);
            }
            var source=new NpcTrajectory(identity,1000,1,PredictionAssumption.RandomRepresentative,PredictionStop.None,points,points.Length,PredictionStrategy.SegmentedTrend,17,PredictionQuality.ObservedTrend);
            NpcTrajectory first=Get(window,source,1005,120,2,true);
            Require(first.Strategy==source.Strategy && first.RelationVersion==17 && first.Quality==source.Quality,"Shared windows preserve strategy, relation and quality without recategorizing the prediction.");
            Require(first.Count==121 && first.SampleTick==1005 && (long)capture.GetValue(first)==1000 && first.Version==2 && first.Identity.Equals(identity),"Five-tick late response keeps 120 future updates and its original identity.");
            for(int i=0;i<first.Count;i++)Same(first[i],points[i+5],i);
            Require(first[35].NewSegment && !first[34].NewSegment,"Teleport/discontinuity flag stays with its actual interval.");
            NpcTrajectory last=Get(window,first,1060,120,3,true);
            Require(last.Count==121 && (long)capture.GetValue(last)==1000,"Repeated windows do not renew original capture age.");
            Same(last[0],points[60],0);Same(last[120],points[180],120);
            Get(window,last,1061,120,4,false);Get(window,first,1004,120,4,false);Get(window,source,999,120,4,false);
            Get(window,source,long.MaxValue,120,4,false);Get(window,source,1000,int.MaxValue,4,false);Get(window,source,1000,-1,4,false);
            Require(source.Count==181 && source.SampleTick==1000 && source[5].TickOffset==5,"Slicing never mutates another consumer's timeline.");
            points[5]=default(NpcTrajectoryPoint);Require(first[0].Bounds.X==125,"Mutable constructor input cannot affect shared window storage.");
            bool refused=false;try{var ignored=first[-1];}catch(IndexOutOfRangeException){refused=true;}Require(refused,"Negative view index rejected.");
            refused=false;try{var ignored=first[121];}catch(IndexOutOfRangeException){refused=true;}Require(refused,"View cannot expose hidden reserve points by index.");
            refused=false;try{first.Republish(2000,5);}catch(InvalidOperationException){refused=true;}Require(refused,"Timestamp-only republish cannot revive an asynchronous window.");
            var refreshed=source.Republish(1005,6);Require(refreshed.SampleTick==1005 && (long)capture.GetValue(refreshed)==1005 && refreshed.Count==181,"Synchronous identical-input resampling retains its existing rebase contract.");
            var shortPoints=new NpcTrajectoryPoint[51];for(int i=0;i<shortPoints.Length;i++)shortPoints[i]=source[i];
            var ended=new NpcTrajectory(identity,1000,1,PredictionAssumption.None,PredictionStop.Despawn,shortPoints,shortPoints.Length);
            var terminal=Get(window,ended,1040,120,7,true);Require(terminal.Count==11 && terminal.Stop==PredictionStop.Despawn,"Actual natural ending is preserved without fabricated 120-point padding.");
            var beforeEnd=Get(window,ended,1000,20,8,true);Require(beforeEnd.Stop==PredictionStop.None,"An ending beyond the visible window is not reported as already reached.");
            Get(window,ended,1051,120,8,false);
            foreach(PredictionStop stop in new[]{PredictionStop.MissingDependency,PredictionStop.TerrainUnavailable,PredictionStop.InvalidState,PredictionStop.None})
                Get(window,new NpcTrajectory(identity,1000,1,PredictionAssumption.None,stop,shortPoints,shortPoints.Length),1000,120,9,false);
        }
        private static NpcTrajectory Get(MethodInfo method,NpcTrajectory source,long tick,int future,long version,bool expected)
        {object[] args={tick,future,version,null};bool accepted=(bool)method.Invoke(source,args);Require(accepted==expected && (accepted==(args[3]!=null)),"Exact current-window acceptance: tick="+tick+" future="+future);return (NpcTrajectory)args[3];}
        private static void Same(NpcTrajectoryPoint value,NpcTrajectoryPoint source,int offset)
        {Require(value.TickOffset==offset && PredictionPlayers.Same(value.Bounds,source.Bounds) && PredictionPlayers.Same(value.ReceiveBounds,source.ReceiveBounds) && PredictionPlayers.Same(value.ProjectileReceiveBounds,source.ProjectileReceiveBounds) && value.HasProjectileExtension==source.HasProjectileExtension && value.Vx==source.Vx && value.Vy==source.Vy && value.Phase==source.Phase && value.CanReceive==source.CanReceive && value.CanHarm==source.CanHarm && value.NewSegment==source.NewSegment,"Window preserves exact geometry, state and discontinuities at offset="+offset);}
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
