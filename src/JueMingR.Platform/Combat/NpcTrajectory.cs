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
    public enum PredictionStop { None, UnsupportedMechanism, RandomDestination, MissingDependency, TerrainUnavailable, TerrainLimit, Slope, LiquidEffect, BuffTransition, Despawn, PhaseBoundary, InvalidState, RandomDecision }
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
        public NpcIdentity ChildIdentity;
        public NpcHealthState Health;
        public bool Friendly;
        public float NetOffsetX,NetOffsetY,SmoothingRange;
        public bool ResetNetOffset;
        public float X,Y,Vx,Vy,OldX,OldY,OldVx,OldVy,Scale,Gravity,MaxFall,WaterSpeed,HoneySpeed,LavaSpeed,ShimmerSpeed;
        public float A0,A1,A2,A3,L0,L1,L2,L3;
        public int Width,Height,Style,Direction,DirectionY,SpriteDirection,Target,ParentSlot,ChildSlot,TimeLeft,ConfusedTicks,Life,LifeMax,BuffFingerprint,BuffExpires;
        public bool Active,NoGravity,NoTileCollide,Wet,Honey,Lava,Shimmer,CollideX,CollideY,CanReceive,CanHarm,NewSegment,JustHit,StairFall,SpawnedFromStatue,Boss,InactivityImmune,TargetNoAggro;
        public MotionRect Bounds {get{return new MotionRect(X,Y,Width,Height);}}
    }
    public struct NpcHealthState : IEquatable<NpcHealthState>
    {
        public NpcBuffLayout Buffs;
        public int Poison,Fire,Cursed,Venom,Frost,Fire3,Frost2,Shadow,Slimed,Oil,Accelerated,WetBuff,ShimmerTicks,Regen,RegenCount,RealLife,Immune255,Defense;
        public float ShimmerTransparency,DamageMultiplier;
        public bool DontTakeDamage,Immortal,LavaImmune,FireImmune,ShimmerImmune,ShimmerAction;
        public bool Equals(NpcHealthState b)
        {return Buffs.Equals(b.Buffs) && Poison==b.Poison && Fire==b.Fire && Cursed==b.Cursed && Venom==b.Venom && Frost==b.Frost && Fire3==b.Fire3 && Frost2==b.Frost2 && Shadow==b.Shadow && Slimed==b.Slimed && Oil==b.Oil && Accelerated==b.Accelerated && WetBuff==b.WetBuff && ShimmerTicks==b.ShimmerTicks && Regen==b.Regen && RegenCount==b.RegenCount && RealLife==b.RealLife && Immune255==b.Immune255 && Defense==b.Defense && ShimmerTransparency==b.ShimmerTransparency && DamageMultiplier==b.DamageMultiplier && DontTakeDamage==b.DontTakeDamage && Immortal==b.Immortal && LavaImmune==b.LavaImmune && FireImmune==b.FireImmune && ShimmerImmune==b.ShimmerImmune && ShimmerAction==b.ShimmerAction;}
    }
    public struct PredictionEnvironment : IEquatable<PredictionEnvironment>
    {
        public PredictionPlayers Players;
        public bool BloodMoon,PlayerProtected;
        public float PlayerX,PlayerY,PlayerWidth,PlayerHeight,Wind,WorldSurface;
        public float RockLayer;
        public bool Expert,Enraged,Day,PlayerWet,ClearLine,Multiplayer,Remix,SlimeRain,Eclipse,Graveyard,GoodWorld,PlayerDead,PlayerIdleWithNegativeAggro,Corrupt,Crimson,AnyLivingCorrupt,SkyblockLowTiles,MechQueenUp;
        public int WorldWidth,WorldHeight,InvasionType,PlayerIndex;
        public bool Equals(PredictionEnvironment b)
        {return (ReferenceEquals(Players,b.Players) || Players!=null && Players.Equals(b.Players)) && BloodMoon==b.BloodMoon && PlayerProtected==b.PlayerProtected && PlayerIndex==b.PlayerIndex && MechQueenUp==b.MechQueenUp && RockLayer==b.RockLayer && WorldHeight==b.WorldHeight && PlayerDead==b.PlayerDead && PlayerIdleWithNegativeAggro==b.PlayerIdleWithNegativeAggro && Corrupt==b.Corrupt && Crimson==b.Crimson && AnyLivingCorrupt==b.AnyLivingCorrupt && SkyblockLowTiles==b.SkyblockLowTiles && PlayerX==b.PlayerX && PlayerY==b.PlayerY && PlayerWidth==b.PlayerWidth && PlayerHeight==b.PlayerHeight && Wind==b.Wind && WorldSurface==b.WorldSurface && Expert==b.Expert && Enraged==b.Enraged && Day==b.Day && PlayerWet==b.PlayerWet && ClearLine==b.ClearLine && Multiplayer==b.Multiplayer && Remix==b.Remix && SlimeRain==b.SlimeRain && WorldWidth==b.WorldWidth && Eclipse==b.Eclipse && Graveyard==b.Graveyard && GoodWorld==b.GoodWorld && InvasionType==b.InvasionType;}
    }
    public sealed class PredictionPlayers : IEquatable<PredictionPlayers>
    {
        private readonly MotionRect[] areas;
        public PredictionPlayers(MotionRect[] source,int count){if(source==null || count<0 || count>255 || count>source.Length)throw new ArgumentOutOfRangeException();areas=new MotionRect[count];Array.Copy(source,areas,count);}
        public int Count {get{return areas.Length;}}
        public MotionRect this[int index] {get{return areas[index];}}
        public bool Equals(PredictionPlayers b){if(b==null || b.Count!=Count)return false;for(int i=0;i<Count;i++)if(!Same(areas[i],b.areas[i]))return false;return true;}
        public static bool Same(MotionRect a,MotionRect b){return a.X==b.X && a.Y==b.Y && a.Width==b.Width && a.Height==b.Height;}
    }
    public struct PredictionTile
    {
        public bool Active,Solid,SolidTop,Platform,Half,ProperPlatformFrame,SurfacePlatform;
        public byte Slope,Liquid;
        public ushort Type;
        public bool TopSlope {get{return Slope==1 || Slope==2;}}
        public bool SolidNoPlatform {get{return Active && !Platform && (Solid || SolidTop);}}
        public bool SolidBottomSlope {get{return Active && (Solid || SolidTop) && !Half && (!TopSlope || Platform && ProperPlatformFrame);}}
    }
    public interface IPredictionTerrain
    {
        // Implementations copy only queried tile values on the game thread.
        // Unknown/complex terrain terminates a segment; it never becomes air.
        bool Solid(MotionRect area,out bool solid,out PredictionStop stop);
        bool CanHit(MotionRect source,MotionRect target,out bool clear,out PredictionStop stop);
        bool Tile(int x,int y,out PredictionTile tile,out PredictionStop stop);
        bool Move(ref NpcMotionState state,PredictionEnvironment environment,out PredictionStop stop);
        bool Unchanged {get;}
        void Reset();
    }
    public struct NpcTrajectoryPoint
    {
        public readonly int TickOffset;
        public readonly MotionRect Bounds;
        // Bounds is physical motion. These integer rectangles follow native
        // hit testing after its observed network offset. Only projectile hits
        // use the extra eight-pixel margin on NPC 414; melee uses ReceiveBounds.
        public readonly MotionRect ReceiveBounds,ProjectileReceiveBounds;
        public readonly bool HasProjectileExtension;
        public readonly float Vx,Vy,Phase;
        public readonly bool CanReceive,CanHarm,NewSegment;
        public NpcTrajectoryPoint(int tick,NpcMotionState state)
        {TickOffset=tick;Bounds=state.Bounds;Vx=state.Vx;Vy=state.Vy;Phase=state.A0;CanReceive=state.CanReceive;CanHarm=state.CanHarm;NewSegment=state.NewSegment;ReceiveBounds=new MotionRect((int)(state.X+state.NetOffsetX),(int)(state.Y+state.NetOffsetY),state.Width,state.Height);HasProjectileExtension=state.Identity.Type==414;ProjectileReceiveBounds=HasProjectileExtension?new MotionRect(ReceiveBounds.X-8,ReceiveBounds.Y-8,ReceiveBounds.Width+16,ReceiveBounds.Height+16):ReceiveBounds;}
        private NpcTrajectoryPoint(int tick,NpcTrajectoryPoint source)
        {TickOffset=tick;Bounds=source.Bounds;ReceiveBounds=source.ReceiveBounds;ProjectileReceiveBounds=source.ProjectileReceiveBounds;HasProjectileExtension=source.HasProjectileExtension;Vx=source.Vx;Vy=source.Vy;Phase=source.Phase;CanReceive=source.CanReceive;CanHarm=source.CanHarm;NewSegment=source.NewSegment;}
        public NpcTrajectoryPoint AtOffset(int tick){return new NpcTrajectoryPoint(tick,this);}
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
        private NpcTrajectory(NpcTrajectory prior,long sampleTick,long version)
        {Identity=prior.Identity;SampleTick=sampleTick;Version=version;Assumptions=prior.Assumptions;Stop=prior.Stop;points=prior.points;}
        // Only an already-published immutable array may be shared. The public
        // construction path still copies mutable model work buffers. A newer
        // sampling identity never mutates records retained by another consumer.
        public NpcTrajectory Republish(long sampleTick,long version){return new NpcTrajectory(this,sampleTick,version);}
    }
}
