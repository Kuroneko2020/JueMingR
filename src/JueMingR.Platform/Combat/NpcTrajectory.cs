using System;

namespace JueMingR.Platform.Combat
{
    // Token is compared by reference only. Neither models nor consumers may
    // dereference it. Slot reuse, transformation and Session changes all retire
    // a result even if the new occupant has identical coordinates.
    public struct NpcIdentity : IEquatable<NpcIdentity>
    {
        public readonly long Session;
        public readonly object Token;
        public readonly int Slot,Generation,Type,NetId;
        public NpcIdentity(long session,object token,int slot,int generation,int type,int netId)
        {Session=session;Token=token;Slot=slot;Generation=generation;Type=type;NetId=netId;}
        public bool Equals(NpcIdentity b){return Session==b.Session && ReferenceEquals(Token,b.Token) && Slot==b.Slot && Generation==b.Generation && Type==b.Type && NetId==b.NetId;}
        public override bool Equals(object b){return b is NpcIdentity && Equals((NpcIdentity)b);}
        public override int GetHashCode(){return Slot^Generation^Type^Session.GetHashCode();}
    }
    [Flags]
    public enum PredictionAssumption { None=0, TargetPlayerStationary=1, FixedTarget=2, NoNewHits=4, RandomRepresentative=8, LocalTerrain=16, NetworkObservation=32, ApproximateMechanism=64 }
    public enum PredictionStop { None, UnsupportedMechanism, RandomDestination, MissingDependency, TerrainUnavailable, TerrainLimit, Slope, LiquidEffect, BuffTransition, Despawn, PhaseBoundary, InvalidState }
    public struct MotionRect
    {
        public float X,Y,Width,Height;
        public MotionRect(float x,float y,float width,float height){X=x;Y=y;Width=width;Height=height;}
        public float CenterX {get{return X+Width*.5f;}}
        public float CenterY {get{return Y+Height*.5f;}}
    }
    // Independent scalar state: no shared ai/localAI/buff arrays, delegates or
    // Terraria entities. Every field here has one game-thread prediction owner.
    public struct NpcMotionState
    {
        public NpcIdentity Identity;
        public float X,Y,Vx,Vy,OldVx,OldVy,Scale,Gravity,MaxFall,WaterSpeed,HoneySpeed;
        public float A0,A1,A2,A3,L0,L1,L2,L3;
        public int Width,Height,Style,Direction,DirectionY,Target,ParentSlot,TimeLeft,ConfusedTicks,Life,LifeMax,BuffFingerprint,BuffExpires;
        public bool Active,NoGravity,NoTileCollide,Wet,Honey,CollideX,CollideY,CanReceive,CanHarm,NewSegment,JustHit;
        public MotionRect Bounds {get{return new MotionRect(X,Y,Width,Height);}}
    }
    public struct PredictionEnvironment : IEquatable<PredictionEnvironment>
    {
        public float PlayerX,PlayerY,PlayerWidth,PlayerHeight,Wind,WorldSurface;
        public bool Expert,Enraged,Day,PlayerWet,ClearLine,Multiplayer,Remix,SlimeRain;
        public int WorldWidth;
        public bool Equals(PredictionEnvironment b)
        {return PlayerX==b.PlayerX && PlayerY==b.PlayerY && PlayerWidth==b.PlayerWidth && PlayerHeight==b.PlayerHeight && Wind==b.Wind && WorldSurface==b.WorldSurface && Expert==b.Expert && Enraged==b.Enraged && Day==b.Day && PlayerWet==b.PlayerWet && ClearLine==b.ClearLine && Multiplayer==b.Multiplayer && Remix==b.Remix && SlimeRain==b.SlimeRain && WorldWidth==b.WorldWidth;}
    }
    public interface IPredictionTerrain
    {
        // Implementations copy only queried tile values on the game thread.
        // Unknown/complex terrain terminates a segment; it never becomes air.
        bool Solid(MotionRect area,out bool solid,out PredictionStop stop);
        bool Move(ref NpcMotionState state,out PredictionStop stop);
        bool Unchanged {get;}
        void Reset();
    }
    public struct NpcTrajectoryPoint
    {
        public readonly int TickOffset;
        public readonly MotionRect Bounds;
        public readonly float Vx,Vy,Phase;
        public readonly bool CanReceive,CanHarm,NewSegment;
        public NpcTrajectoryPoint(int tick,NpcMotionState state)
        {TickOffset=tick;Bounds=state.Bounds;Vx=state.Vx;Vy=state.Vy;Phase=state.A0;CanReceive=state.CanReceive;CanHarm=state.CanHarm;NewSegment=state.NewSegment;}
    }
    public sealed class NpcTrajectory
    {
        private readonly NpcTrajectoryPoint[] points;
        public NpcIdentity Identity {get;}
        public long SampleTick {get;}
        public long Version {get;}
        public const string SamplePhase="CompletedWorldUpdate";
        public PredictionAssumption Assumptions {get;}
        public PredictionStop Stop {get;}
        public int Count {get{return points.Length;}}
        public NpcTrajectoryPoint this[int index] {get{return points[index];}}
        public NpcTrajectory(NpcIdentity identity,long sampleTick,long version,PredictionAssumption assumptions,PredictionStop stop,NpcTrajectoryPoint[] source,int count)
        {Identity=identity;SampleTick=sampleTick;Version=version;Assumptions=assumptions;Stop=stop;points=new NpcTrajectoryPoint[count];Array.Copy(source,points,count);}
    }
}
